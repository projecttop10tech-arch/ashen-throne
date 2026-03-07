using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Typesafe publish/subscribe event bus for loose coupling between systems.
    /// All event types must be structs (readonly preferred) to avoid heap allocations.
    /// Always unsubscribe in OnDestroy/OnDisable to prevent memory leaks.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _handlers.Clear();
            _initialized = true;
        }

        public static void Shutdown()
        {
            _handlers.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Subscribe to an event type. Unsubscribe using the returned subscription or call Unsubscribe manually.
        /// </summary>
        public static EventSubscription Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            Type type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
            return new EventSubscription(() => Unsubscribe(handler));
        }

        /// <summary>
        /// Unsubscribe a previously registered handler.
        /// </summary>
        public static void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            Type type = typeof(TEvent);
            if (_handlers.TryGetValue(type, out List<Delegate> list))
                list.Remove(handler);
        }

        /// <summary>
        /// Publish an event to all registered subscribers. Dispatched synchronously on calling thread.
        /// Caller must ensure this is invoked on the Unity main thread when touching UnityEngine APIs.
        /// </summary>
        public static void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            Type type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out List<Delegate> list) || list.Count == 0)
                return;

            // Iterate over snapshot to safely allow unsubscription during dispatch.
            Delegate[] snapshot = list.ToArray();
            foreach (Delegate d in snapshot)
            {
                try
                {
                    ((Action<TEvent>)d)(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventBus] Handler threw exception for event {type.Name}: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Disposable subscription handle. Store as a field and call Dispose in OnDestroy.
    /// </summary>
    public sealed class EventSubscription : IDisposable
    {
        private readonly Action _unsubscribeAction;
        private bool _disposed;

        public EventSubscription(Action unsubscribeAction)
        {
            _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribeAction();
        }
    }
}
