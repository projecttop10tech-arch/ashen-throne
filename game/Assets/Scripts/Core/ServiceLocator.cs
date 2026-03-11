using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Lightweight service locator for cross-system dependency access.
    /// Systems register themselves during initialization; consumers resolve via Get<T>().
    /// Prefer constructor injection for pure C# classes; use this for MonoBehaviour services.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
        }

        public static void Shutdown()
        {
            _services.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Register a service instance. Overwrites existing registration for the same type.
        /// </summary>
        public static void Register<TService>(TService instance) where TService : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _services[typeof(TService)] = instance;
        }

        /// <summary>
        /// Unregister a service. Safe to call even if not registered.
        /// </summary>
        public static void Unregister<TService>() where TService : class
        {
            _services.Remove(typeof(TService));
        }

        /// <summary>
        /// Resolve a registered service. Throws InvalidOperationException if not found.
        /// Use TryGet for optional services.
        /// </summary>
        public static TService Get<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object service))
                return (TService)service;
            throw new InvalidOperationException(
                $"[ServiceLocator] Service '{typeof(TService).Name}' is not registered. " +
                "Ensure it is registered before being requested.");
        }

        /// <summary>
        /// Attempt to resolve a registered service without throwing. Returns false if not found.
        /// </summary>
        public static bool TryGet<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object obj))
            {
                service = (TService)obj;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>
        /// Returns true if a service of the given type is registered.
        /// </summary>
        public static bool IsRegistered<TService>() where TService : class =>
            _services.ContainsKey(typeof(TService));
    }
}
