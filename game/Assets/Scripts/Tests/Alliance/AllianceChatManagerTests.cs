using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Alliance;

namespace AshenThrone.Tests.Alliance
{
    [TestFixture]
    public class AllianceChatManagerTests
    {
        private GameObject _go;
        private AllianceChatManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AllianceChatManagerTest");
            _manager = _go.AddComponent<AllianceChatManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // --- DefaultChatSanitizer ---

        [Test]
        public void Sanitizer_StripHtmlTags()
        {
            var sanitizer = new DefaultChatSanitizer();
            string result = sanitizer.Sanitize("<script>alert('xss')</script>Hello");
            Assert.IsFalse(result.Contains("<script>"));
            Assert.IsTrue(result.Contains("Hello"));
        }

        [Test]
        public void Sanitizer_RemovesSqlPatterns()
        {
            var sanitizer = new DefaultChatSanitizer();
            string result = sanitizer.Sanitize("'; DROP TABLE users; --");
            Assert.IsFalse(result.Contains("'"));
            Assert.IsFalse(result.Contains("--"));
        }

        [Test]
        public void Sanitizer_RemovesJavascriptProtocol()
        {
            var sanitizer = new DefaultChatSanitizer();
            string result = sanitizer.Sanitize("javascript:alert(1)");
            Assert.IsFalse(result.ToLower().Contains("javascript:"));
        }

        [Test]
        public void Sanitizer_ClampsToMaxLength()
        {
            var sanitizer = new DefaultChatSanitizer();
            string longInput = new string('a', AllianceChatManager.MaxMessageLength + 100);
            string result = sanitizer.Sanitize(longInput);
            Assert.LessOrEqual(result.Length, AllianceChatManager.MaxMessageLength);
        }

        [Test]
        public void Sanitizer_ReturnsEmpty_ForNullInput()
        {
            var sanitizer = new DefaultChatSanitizer();
            Assert.AreEqual(string.Empty, sanitizer.Sanitize(null));
        }

        [Test]
        public void Sanitizer_ContainsViolation_ReturnsFalse_ForCleanText()
        {
            var sanitizer = new DefaultChatSanitizer();
            Assert.IsFalse(sanitizer.ContainsViolation("Hello alliance!"));
        }

        [Test]
        public void Sanitizer_ContainsViolation_ReturnsTrue_ForHtmlTag()
        {
            var sanitizer = new DefaultChatSanitizer();
            Assert.IsTrue(sanitizer.ContainsViolation("<b>bold</b>"));
        }

        // --- ValidateSend ---

        [Test]
        public void ValidateSend_ReturnsFalse_ForEmptyMessage()
        {
            bool ok = _manager.ValidateSend("", ChatChannel.Alliance, out _, out string error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ValidateSend_ReturnsFalse_ForWhitespaceOnlyMessage()
        {
            bool ok = _manager.ValidateSend("   ", ChatChannel.Alliance, out _, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void ValidateSend_ReturnsTrue_ForValidMessage()
        {
            bool ok = _manager.ValidateSend("Hello!", ChatChannel.Alliance, out string sanitized, out _);
            Assert.IsTrue(ok);
            Assert.AreEqual("Hello!", sanitized);
        }

        [Test]
        public void ValidateSend_SanitizesMessageBody()
        {
            bool ok = _manager.ValidateSend("<b>bold</b>message", ChatChannel.Alliance, out string sanitized, out _);
            Assert.IsTrue(ok);
            Assert.IsFalse(sanitized.Contains("<b>"));
        }

        // --- ReceiveMessage ---

        [Test]
        public void ReceiveMessage_DoesNotThrow_ForNullMessage()
        {
            Assert.DoesNotThrow(() => _manager.ReceiveMessage(null));
        }

        [Test]
        public void ReceiveMessage_AddsToHistory()
        {
            _manager.ReceiveMessage(MakeMessage("msg1", ChatChannel.Alliance));
            Assert.AreEqual(1, _manager.GetHistory(ChatChannel.Alliance).Count);
        }

        [Test]
        public void ReceiveMessage_FiresOnMessageReceivedEvent()
        {
            bool fired = false;
            _manager.OnMessageReceived += _ => fired = true;
            _manager.ReceiveMessage(MakeMessage("msg1", ChatChannel.Alliance));
            Assert.IsTrue(fired);
        }

        [Test]
        public void ReceiveMessage_EvictsOldest_WhenHistoryFull()
        {
            for (int i = 0; i < AllianceChatManager.MaxHistorySize; i++)
                _manager.ReceiveMessage(MakeMessage($"msg_{i}", ChatChannel.Alliance));

            // Add one more
            _manager.ReceiveMessage(MakeMessage("msg_overflow", ChatChannel.Alliance));
            Assert.AreEqual(AllianceChatManager.MaxHistorySize, _manager.GetHistory(ChatChannel.Alliance).Count);
        }

        [Test]
        public void ReceiveMessage_OfficerMessages_StoreInOfficerChannel()
        {
            _manager.ReceiveMessage(MakeMessage("officer_msg", ChatChannel.Officer));
            Assert.AreEqual(0, _manager.GetHistory(ChatChannel.Alliance).Count);
            Assert.AreEqual(1, _manager.GetHistory(ChatChannel.Officer).Count);
        }

        // --- GetHistory ---

        [Test]
        public void GetHistory_ReturnsEmpty_WhenNoMessages()
        {
            Assert.AreEqual(0, _manager.GetHistory(ChatChannel.Alliance).Count);
        }

        // --- ClearHistory ---

        [Test]
        public void ClearHistory_RemovesAllMessages_ForChannel()
        {
            _manager.ReceiveMessage(MakeMessage("m1", ChatChannel.Alliance));
            _manager.ReceiveMessage(MakeMessage("m2", ChatChannel.Alliance));
            _manager.ClearHistory(ChatChannel.Alliance);
            Assert.AreEqual(0, _manager.GetHistory(ChatChannel.Alliance).Count);
        }

        [Test]
        public void ClearHistory_DoesNotAffectOtherChannels()
        {
            _manager.ReceiveMessage(MakeMessage("m1", ChatChannel.Alliance));
            _manager.ReceiveMessage(MakeMessage("m2", ChatChannel.Officer));
            _manager.ClearHistory(ChatChannel.Alliance);
            Assert.AreEqual(1, _manager.GetHistory(ChatChannel.Officer).Count);
        }

        // --- SetSanitizer ---

        [Test]
        public void SetSanitizer_ThrowsArgumentNull_WhenNull()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.SetSanitizer(null));
        }

        // --- Helpers ---

        private static ChatMessage MakeMessage(string id, ChatChannel channel)
        {
            return new ChatMessage(
                id,
                "player_1",
                "TestPlayer",
                AllianceRole.Member,
                "Hello world",
                DateTime.UtcNow,
                channel);
        }
    }
}
