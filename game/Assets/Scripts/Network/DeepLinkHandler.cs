using System;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Network
{
    /// <summary>
    /// Handles deep links (URL scheme: ashenthrone://).
    /// Supported routes:
    ///   ashenthrone://alliance/join?id={allianceId}
    ///   ashenthrone://event?id={eventId}
    ///   ashenthrone://hero?id={heroId}
    /// </summary>
    public class DeepLinkHandler : MonoBehaviour
    {
        public event Action<string, string> OnDeepLinkReceived; // (route, queryParams)

        private void Awake()
        {
            ServiceLocator.Register<DeepLinkHandler>(this);
            Application.deepLinkActivated += OnDeepLinkActivated;

            // Check for deep link on cold start
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLinkActivated(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
            ServiceLocator.Unregister<DeepLinkHandler>();
        }

        private void OnDeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            Debug.Log($"[DeepLinkHandler] Received: {url}");

            // Parse: ashenthrone://route?params
            if (!url.StartsWith("ashenthrone://")) return;

            string path = url.Substring("ashenthrone://".Length);
            string route = path;
            string query = "";

            int queryIdx = path.IndexOf('?');
            if (queryIdx >= 0)
            {
                route = path.Substring(0, queryIdx);
                query = path.Substring(queryIdx + 1);
            }

            OnDeepLinkReceived?.Invoke(route, query);
            EventBus.Publish(new DeepLinkEvent(route, query));
        }
    }

    public readonly struct DeepLinkEvent
    {
        public readonly string Route;
        public readonly string QueryParams;
        public DeepLinkEvent(string r, string q) { Route = r; QueryParams = q; }
    }
}
