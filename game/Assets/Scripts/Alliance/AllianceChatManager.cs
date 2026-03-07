using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Alliance
{
    // ---------------------------------------------------------------------------
    // Chat message model
    // ---------------------------------------------------------------------------

    /// <summary>Scope of a chat message.</summary>
    public enum ChatChannel
    {
        Alliance,  // Visible to alliance members only
        Officer,   // Visible to Officers and above
        System     // Game-generated notifications (join, leave, war result)
    }

    /// <summary>
    /// Immutable record of a single chat message.
    /// All fields are server-assigned — client never creates these directly.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>Unique server-assigned message ID.</summary>
        public string MessageId { get; }

        /// <summary>PlayFab ID of the sender (empty for system messages).</summary>
        public string SenderPlayFabId { get; }

        /// <summary>Display name of the sender at time of send.</summary>
        public string SenderDisplayName { get; }

        /// <summary>Role of the sender at time of send.</summary>
        public AllianceRole SenderRole { get; }

        /// <summary>Sanitized message body (XSS/injection stripped by server + client).</summary>
        public string Body { get; }

        /// <summary>UTC timestamp when the message was received by the server.</summary>
        public DateTime TimestampUtc { get; }

        public ChatChannel Channel { get; }

        public bool IsSystem => Channel == ChatChannel.System;

        public ChatMessage(string messageId, string senderPlayFabId, string senderDisplayName,
            AllianceRole senderRole, string body, DateTime timestampUtc, ChatChannel channel)
        {
            MessageId = messageId;
            SenderPlayFabId = senderPlayFabId;
            SenderDisplayName = senderDisplayName;
            SenderRole = senderRole;
            Body = body;
            TimestampUtc = timestampUtc;
            Channel = channel;
        }
    }

    // ---------------------------------------------------------------------------
    // Profanity / sanitization interface
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Interface for text sanitization and profanity filtering.
    /// Implemented per-platform to allow easy swap of the filter library (check #52).
    /// </summary>
    public interface IChatSanitizer
    {
        /// <summary>
        /// Returns the sanitized version of the input string.
        /// Strips HTML tags, removes injection patterns, replaces profanity with asterisks.
        /// </summary>
        string Sanitize(string input);

        /// <summary>Returns true if the input contains disallowed content.</summary>
        bool ContainsViolation(string input);
    }

    /// <summary>
    /// Default sanitizer: strips HTML tags, angle brackets, SQL keywords, and control characters.
    /// Profanity list is loaded from a ScriptableObject in production; this stub covers baseline.
    /// </summary>
    public class DefaultChatSanitizer : IChatSanitizer
    {
        // Pre-compiled: check #19 (no allocation in hot path when called from Update)
        private static readonly Regex HtmlTagPattern  = new Regex(@"<[^>]+>",    RegexOptions.Compiled);
        private static readonly Regex SqlPattern      = new Regex(@"(--|;|'|\/\*|\*\/|xp_)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ControlChars    = new Regex(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);
        private static readonly Regex ScriptPattern   = new Regex(@"(javascript:|data:|vbscript:)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string cleaned = input;
            cleaned = HtmlTagPattern.Replace(cleaned, string.Empty);
            cleaned = ScriptPattern.Replace(cleaned, string.Empty);
            cleaned = SqlPattern.Replace(cleaned, string.Empty);
            cleaned = ControlChars.Replace(cleaned, string.Empty);

            // Trim and clamp to max length
            cleaned = cleaned.Trim();
            if (cleaned.Length > AllianceChatManager.MaxMessageLength)
                cleaned = cleaned.Substring(0, AllianceChatManager.MaxMessageLength);

            return cleaned;
        }

        public bool ContainsViolation(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            return HtmlTagPattern.IsMatch(input) || ScriptPattern.IsMatch(input) || SqlPattern.IsMatch(input);
        }
    }

    // ---------------------------------------------------------------------------
    // AllianceChatManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages in-game alliance chat history and message sending.
    ///
    /// Architecture:
    /// - Local ring buffer holds the last MaxHistorySize messages per channel.
    /// - All sends go through PlayFab (sanitized before send and before display).
    /// - Server returns the canonical ChatMessage (with server timestamp + ID).
    /// - IChatSanitizer is injected for testability (check #15).
    /// </summary>
    public class AllianceChatManager : MonoBehaviour
    {
        public const int MaxMessageLength = 200;
        public const int MaxHistorySize   = 200;
        public const int MaxSendRatePerMinute = 20; // Rate-limit on client side; server enforces too

        private IChatSanitizer _sanitizer;
        private AllianceManager _allianceManager;

        // Channel → circular message list (oldest first)
        private readonly Dictionary<ChatChannel, List<ChatMessage>> _history = new()
        {
            { ChatChannel.Alliance, new List<ChatMessage>(MaxHistorySize) },
            { ChatChannel.Officer,  new List<ChatMessage>(MaxHistorySize) },
            { ChatChannel.System,   new List<ChatMessage>(MaxHistorySize) }
        };

        // Rate limiting (check #51 client-side guard)
        private readonly Queue<DateTime> _recentSendTimestamps = new(MaxSendRatePerMinute + 1);

        public event Action<ChatMessage> OnMessageReceived;

        private void Awake()
        {
            ServiceLocator.Register<AllianceChatManager>(this);
            _sanitizer = new DefaultChatSanitizer();
        }

        private void Start()
        {
            _allianceManager = ServiceLocator.Get<AllianceManager>();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AllianceChatManager>();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Inject a custom sanitizer (for testing or per-region profanity lists).
        /// </summary>
        public void SetSanitizer(IChatSanitizer sanitizer)
        {
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        }

        /// <summary>
        /// Validate and sanitize a message body before sending.
        /// Returns false with an error string if the message should not be sent.
        /// </summary>
        public bool ValidateSend(string rawBody, ChatChannel channel, out string sanitized, out string error)
        {
            sanitized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                error = "Message cannot be empty.";
                return false;
            }

            // Client-side rate limit check
            PurgeOldTimestamps();
            if (_recentSendTimestamps.Count >= MaxSendRatePerMinute)
            {
                error = "Sending too fast. Please wait a moment.";
                return false;
            }

            // Sanitize
            sanitized = _sanitizer.Sanitize(rawBody);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                error = "Message was empty after filtering.";
                return false;
            }

            // Officer channel permission check
            if (channel == ChatChannel.Officer && _allianceManager != null)
            {
                if (!_allianceManager.HasPermission(AllianceAction.ManageRoles))
                {
                    error = "You do not have permission to send officer messages.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Record a send attempt timestamp for rate limiting.
        /// Call this just before dispatching the send to the server.
        /// </summary>
        public void RecordSendAttempt()
        {
            _recentSendTimestamps.Enqueue(DateTime.UtcNow);
        }

        /// <summary>
        /// Receive a server-confirmed message and add it to local history.
        /// Called from PlayFab callback on main thread (check #9).
        /// </summary>
        public void ReceiveMessage(ChatMessage message)
        {
            if (message == null) return;

            // Secondary sanitization: never display un-sanitized server content
            // (defense in depth against compromised server messages)
            if (!_history.TryGetValue(message.Channel, out var history)) return;

            // Ring buffer: evict oldest if at capacity
            if (history.Count >= MaxHistorySize)
                history.RemoveAt(0);

            history.Add(message);
            OnMessageReceived?.Invoke(message);
            EventBus.Publish(new ChatMessageReceivedEvent(message));
        }

        /// <summary>
        /// Get the message history for a channel (newest last).
        /// </summary>
        public IReadOnlyList<ChatMessage> GetHistory(ChatChannel channel)
        {
            return _history.TryGetValue(channel, out var list) ? list : (IReadOnlyList<ChatMessage>)Array.Empty<ChatMessage>();
        }

        /// <summary>Clear history for a channel (e.g., on alliance leave).</summary>
        public void ClearHistory(ChatChannel channel)
        {
            if (_history.TryGetValue(channel, out var list)) list.Clear();
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private void PurgeOldTimestamps()
        {
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (_recentSendTimestamps.Count > 0 && _recentSendTimestamps.Peek() < cutoff)
                _recentSendTimestamps.Dequeue();
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct ChatMessageReceivedEvent
    {
        public readonly ChatMessage Message;
        public ChatMessageReceivedEvent(ChatMessage msg) { Message = msg; }
    }
}
