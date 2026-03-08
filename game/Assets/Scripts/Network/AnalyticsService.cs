using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Network
{
    /// <summary>
    /// Wrapper for Firebase Analytics. All analytics calls go through this service.
    /// Currently runs in stub mode. To activate:
    /// 1. Install Firebase Unity SDK (com.google.firebase.analytics)
    /// 2. Place google-services.json (Android) / GoogleService-Info.plist (iOS) in Assets/
    /// 3. Define FIREBASE_SDK in Player Settings > Scripting Define Symbols
    /// </summary>
    public class AnalyticsService : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register<AnalyticsService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AnalyticsService>();
        }

        public void Initialize()
        {
#if FIREBASE_SDK
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                if (task.Result == Firebase.DependencyStatus.Available)
                {
                    Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        IsInitialized = true;
                        Debug.Log("[AnalyticsService] Firebase Analytics initialized.");
                    });
                }
                else
                {
                    Debug.LogError($"[AnalyticsService] Firebase dependency error: {task.Result}");
                }
            });
#else
            Debug.LogWarning("[AnalyticsService] Running in stub mode. Define FIREBASE_SDK to enable.");
            IsInitialized = true;
#endif
        }

        /// <summary>Log a named event with optional parameters.</summary>
        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!IsInitialized) return;

#if FIREBASE_SDK
            if (parameters == null || parameters.Count == 0)
            {
                Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName);
            }
            else
            {
                var firebaseParams = new List<Firebase.Analytics.Parameter>();
                foreach (var kvp in parameters)
                {
                    if (kvp.Value is string s)
                        firebaseParams.Add(new Firebase.Analytics.Parameter(kvp.Key, s));
                    else if (kvp.Value is int i)
                        firebaseParams.Add(new Firebase.Analytics.Parameter(kvp.Key, i));
                    else if (kvp.Value is long l)
                        firebaseParams.Add(new Firebase.Analytics.Parameter(kvp.Key, l));
                    else if (kvp.Value is double d)
                        firebaseParams.Add(new Firebase.Analytics.Parameter(kvp.Key, d));
                    else if (kvp.Value is float f)
                        firebaseParams.Add(new Firebase.Analytics.Parameter(kvp.Key, (double)f));
                }
                Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, firebaseParams.ToArray());
            }
#else
            Debug.Log($"[AnalyticsService] Stub: LogEvent({eventName})");
#endif
        }

        /// <summary>Set the current user's ID for analytics correlation.</summary>
        public void SetUserId(string userId)
        {
#if FIREBASE_SDK
            Firebase.Analytics.FirebaseAnalytics.SetUserId(userId);
#else
            Debug.Log($"[AnalyticsService] Stub: SetUserId({userId})");
#endif
        }

        /// <summary>Set a user property for segmentation.</summary>
        public void SetUserProperty(string name, string value)
        {
#if FIREBASE_SDK
            Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value);
#else
            Debug.Log($"[AnalyticsService] Stub: SetUserProperty({name}, {value})");
#endif
        }

        // Convenience methods for common events
        public void LogSceneLoaded(string sceneName) =>
            LogEvent("scene_loaded", new Dictionary<string, object> { { "scene", sceneName } });

        public void LogBattleStart(string levelId, int teamPower) =>
            LogEvent("battle_start", new Dictionary<string, object>
                { { "level_id", levelId }, { "team_power", teamPower } });

        public void LogBattleEnd(string levelId, bool victory, int turnCount) =>
            LogEvent("battle_end", new Dictionary<string, object>
                { { "level_id", levelId }, { "victory", victory ? 1 : 0 }, { "turns", turnCount } });

        public void LogPurchase(string productId, string currency, double value) =>
            LogEvent("purchase", new Dictionary<string, object>
                { { "product_id", productId }, { "currency", currency }, { "value", value } });

        public void LogTutorialStep(int step, string name) =>
            LogEvent("tutorial_step", new Dictionary<string, object>
                { { "step", step }, { "name", name } });
    }
}
