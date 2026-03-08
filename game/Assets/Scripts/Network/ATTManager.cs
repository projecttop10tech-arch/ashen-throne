using System;
using UnityEngine;
using AshenThrone.Core;
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace AshenThrone.Network
{
    /// <summary>
    /// iOS App Tracking Transparency (ATT) manager.
    /// Must be shown BEFORE any analytics collection that uses IDFA.
    /// On Android, this is a no-op (always returns authorized).
    /// </summary>
    public class ATTManager : MonoBehaviour
    {
        public enum TrackingStatus
        {
            NotDetermined,
            Restricted,
            Denied,
            Authorized
        }

        public TrackingStatus Status { get; private set; } = TrackingStatus.NotDetermined;
        public bool HasResponded { get; private set; }

        public event Action<TrackingStatus> OnTrackingStatusDetermined;

        private void Awake()
        {
            ServiceLocator.Register<ATTManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ATTManager>();
        }

        /// <summary>
        /// Request tracking authorization from the user. On Android, immediately returns Authorized.
        /// Must be called before Firebase Analytics initialization (compliance check #48).
        /// </summary>
        public void RequestTrackingAuthorization(Action<TrackingStatus> callback = null)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var iosStatus = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (iosStatus == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
                // Poll until status changes (iOS doesn't provide a callback in older Unity versions)
                StartCoroutine(PollATTStatus(callback));
            }
            else
            {
                Status = ConvertIOSStatus(iosStatus);
                HasResponded = true;
                callback?.Invoke(Status);
                OnTrackingStatusDetermined?.Invoke(Status);
            }
#else
            // Android or Editor: no ATT required, auto-authorize
            Status = TrackingStatus.Authorized;
            HasResponded = true;
            callback?.Invoke(Status);
            OnTrackingStatusDetermined?.Invoke(Status);
            Debug.Log("[ATTManager] Non-iOS platform: auto-authorized.");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private System.Collections.IEnumerator PollATTStatus(Action<TrackingStatus> callback)
        {
            float timeout = 30f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var s = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
                if (s != ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                {
                    Status = ConvertIOSStatus(s);
                    HasResponded = true;
                    callback?.Invoke(Status);
                    OnTrackingStatusDetermined?.Invoke(Status);
                    yield break;
                }
                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            // Timeout: treat as denied
            Status = TrackingStatus.Denied;
            HasResponded = true;
            callback?.Invoke(Status);
            OnTrackingStatusDetermined?.Invoke(Status);
        }

        private static TrackingStatus ConvertIOSStatus(ATTrackingStatusBinding.AuthorizationTrackingStatus s)
        {
            return s switch
            {
                ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED => TrackingStatus.Authorized,
                ATTrackingStatusBinding.AuthorizationTrackingStatus.DENIED => TrackingStatus.Denied,
                ATTrackingStatusBinding.AuthorizationTrackingStatus.RESTRICTED => TrackingStatus.Restricted,
                _ => TrackingStatus.NotDetermined,
            };
        }
#endif
    }
}
