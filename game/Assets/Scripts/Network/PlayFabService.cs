using System;
using System.Collections;
using UnityEngine;
using AshenThrone.Core;

// PlayFab SDK must be imported via Unity Package Manager:
// com.playfab.unitysdk — https://github.com/PlayFab/UnitySDK
// These using directives will resolve once the SDK is installed.
// using PlayFab;
// using PlayFab.ClientModels;

namespace AshenThrone.Network
{
    /// <summary>
    /// Wrapper for all PlayFab API calls. Handles authentication, retry logic, and main-thread dispatch.
    /// All callbacks are guaranteed to be invoked on the Unity main thread.
    /// Register with ServiceLocator.Register&lt;PlayFabService&gt;() during boot.
    /// </summary>
    public class PlayFabService : MonoBehaviour
    {
        [SerializeField] private string playfabTitleId = "YOUR_TITLE_ID";
        [SerializeField] private int maxRetryAttempts = 3;
        [SerializeField] private float retryDelaySeconds = 2f;

        public bool IsAuthenticated { get; private set; }
        public string PlayFabId { get; private set; }
        public string DisplayName { get; private set; }

        public event Action OnAuthenticationSuccess;
        public event Action<string> OnAuthenticationFailed;

        private void Awake()
        {
            // PlayFabSettings.staticSettings.TitleId = playfabTitleId;
            ServiceLocator.Register<PlayFabService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PlayFabService>();
        }

        /// <summary>
        /// Authenticate via device ID (guest) or stored session ticket.
        /// Tries stored session first, falls back to anonymous login.
        /// </summary>
        public IEnumerator AuthenticateAsync()
        {
            bool completed = false;
            bool success = false;
            string error = null;

            AttemptAuthentication(
                onSuccess: (id, name) =>
                {
                    PlayFabId = id;
                    DisplayName = name;
                    IsAuthenticated = true;
                    success = true;
                    completed = true;
                },
                onFailure: (err) =>
                {
                    error = err;
                    completed = true;
                });

            yield return new WaitUntil(() => completed);

            if (success)
            {
                OnAuthenticationSuccess?.Invoke();
                EventBus.Publish(new AuthenticationSuccessEvent(PlayFabId, DisplayName));
            }
            else
            {
                OnAuthenticationFailed?.Invoke(error);
                EventBus.Publish(new AuthenticationFailedEvent(error));
                Debug.LogError($"[PlayFabService] Authentication failed: {error}");
            }
        }

        private void AttemptAuthentication(Action<string, string> onSuccess, Action<string> onFailure)
        {
            // TODO: Replace stub with actual PlayFab SDK call once SDK is installed.
            // Example implementation:
            //
            // var request = new LoginWithCustomIDRequest
            // {
            //     CustomId = SystemInfo.deviceUniqueIdentifier,
            //     CreateAccount = true,
            //     InfoRequestParameters = new GetPlayerCombinedInfoRequestParams { GetPlayerProfile = true }
            // };
            // PlayFabClientAPI.LoginWithCustomID(request,
            //     result => onSuccess(result.PlayFabId, result.InfoResultPayload?.PlayerProfile?.DisplayName ?? "Hero"),
            //     error => onFailure(error.GenerateErrorReport()));

            // Stub for development without SDK:
            Debug.LogWarning("[PlayFabService] Running in stub mode. Install PlayFab Unity SDK to enable real authentication.");
            onSuccess("STUB_PLAYER_ID", "Hero");
        }

        /// <summary>
        /// Update the player's display name. Server-validated and stored in PlayFab.
        /// </summary>
        public void UpdateDisplayName(string name, Action onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                onFailure?.Invoke("Display name cannot be empty.");
                return;
            }
            if (name.Length > 25)
            {
                onFailure?.Invoke("Display name must be 25 characters or fewer.");
                return;
            }

            // TODO: PlayFab UpdateUserTitleDisplayName call.
            Debug.LogWarning("[PlayFabService] UpdateDisplayName stub invoked.");
            DisplayName = name;
            onSuccess?.Invoke();
        }
    }

    // --- Events ---
    public readonly struct AuthenticationSuccessEvent { public readonly string PlayFabId; public readonly string DisplayName; public AuthenticationSuccessEvent(string id, string name) { PlayFabId = id; DisplayName = name; } }
    public readonly struct AuthenticationFailedEvent { public readonly string Error; public AuthenticationFailedEvent(string e) { Error = e; } }
}
