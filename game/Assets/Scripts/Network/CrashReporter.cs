using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Network
{
    /// <summary>
    /// Wrapper for Firebase Crashlytics. Captures unhandled exceptions and custom logs.
    /// Currently runs in stub mode. To activate:
    /// 1. Install Firebase Unity SDK (com.google.firebase.crashlytics)
    /// 2. Define FIREBASE_SDK in Player Settings > Scripting Define Symbols
    /// </summary>
    public class CrashReporter : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register<CrashReporter>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CrashReporter>();
        }

        public void Initialize()
        {
#if FIREBASE_SDK
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            IsInitialized = true;
            Debug.Log("[CrashReporter] Firebase Crashlytics initialized.");
#else
            Debug.LogWarning("[CrashReporter] Running in stub mode. Define FIREBASE_SDK to enable.");
            IsInitialized = true;
#endif
            // Register Unity's unhandled exception handler
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                LogException(condition, stackTrace);
            }
        }

        /// <summary>Set the user identifier for crash reports.</summary>
        public void SetUserId(string userId)
        {
#if FIREBASE_SDK
            Firebase.Crashlytics.Crashlytics.SetUserId(userId);
#else
            Debug.Log($"[CrashReporter] Stub: SetUserId({userId})");
#endif
        }

        /// <summary>Add a custom key-value pair to crash reports.</summary>
        public void SetCustomKey(string key, string value)
        {
#if FIREBASE_SDK
            Firebase.Crashlytics.Crashlytics.SetCustomKey(key, value);
#else
            Debug.Log($"[CrashReporter] Stub: SetCustomKey({key}, {value})");
#endif
        }

        /// <summary>Log a breadcrumb message for crash context.</summary>
        public void Log(string message)
        {
#if FIREBASE_SDK
            Firebase.Crashlytics.Crashlytics.Log(message);
#else
            Debug.Log($"[CrashReporter] Stub: Log({message})");
#endif
        }

        /// <summary>Report a non-fatal exception.</summary>
        public void LogException(string message, string stackTrace)
        {
#if FIREBASE_SDK
            Firebase.Crashlytics.Crashlytics.LogException(new System.Exception($"{message}\n{stackTrace}"));
#else
            Debug.LogWarning($"[CrashReporter] Stub: LogException({message})");
#endif
        }
    }
}
