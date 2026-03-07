using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Utils
{
    /// <summary>
    /// Dispatches actions to the Unity main thread from background threads (PlayFab callbacks, async tasks).
    /// Add as a component to the GameManager GameObject.
    /// Usage: MainThreadDispatcher.Enqueue(() => { /* UI update here */ });
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _pendingActions = new();
        private static readonly object _lock = new();
        private static MainThreadDispatcher _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_pendingActions.Count > 0)
                {
                    Action action = _pendingActions.Dequeue();
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Exception in dispatched action: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Enqueue an action to run on the main thread next Update.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_lock)
            {
                _pendingActions.Enqueue(action);
            }
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                _pendingActions.Clear();
            }
        }
    }
}
