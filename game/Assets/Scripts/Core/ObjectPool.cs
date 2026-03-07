using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Generic object pool to eliminate runtime allocation for frequently spawned GameObjects
    /// (particles, projectiles, UI notifications). Prevents GC pressure.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _available = new();
        private readonly HashSet<T> _inUse = new();
        private readonly int _maxSize;

        public int CountAvailable => _available.Count;
        public int CountInUse => _inUse.Count;

        /// <param name="prefab">Template object to clone.</param>
        /// <param name="parent">Parent transform for pooled instances. Keeps hierarchy clean.</param>
        /// <param name="initialSize">Number of instances to pre-warm on creation.</param>
        /// <param name="maxSize">Hard cap on total instances. Requests beyond cap return null.</param>
        public ObjectPool(T prefab, Transform parent, int initialSize = 10, int maxSize = 50)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _maxSize = maxSize;

            for (int i = 0; i < initialSize; i++)
                CreateInstance();
        }

        /// <summary>
        /// Retrieve an instance from the pool. Returns null if pool is exhausted (maxSize reached).
        /// Caller must call Return when done.
        /// </summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance;
            if (_available.Count > 0)
            {
                instance = _available.Dequeue();
            }
            else if (_inUse.Count < _maxSize)
            {
                instance = CreateInstance();
            }
            else
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Pool exhausted (max {_maxSize}). Returning null.");
                return null;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            _inUse.Add(instance);
            return instance;
        }

        /// <summary>
        /// Return an instance to the pool. Deactivates and resets transform parent.
        /// </summary>
        public void Return(T instance)
        {
            if (instance == null) return;
            if (!_inUse.Contains(instance))
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Returning an instance that is not tracked as in-use.");
                return;
            }

            _inUse.Remove(instance);
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_parent);
            _available.Enqueue(instance);
        }

        /// <summary>
        /// Return all in-use instances to the pool. Use on scene cleanup.
        /// </summary>
        public void ReturnAll()
        {
            T[] snapshot = new T[_inUse.Count];
            _inUse.CopyTo(snapshot);
            foreach (T instance in snapshot)
                Return(instance);
        }

        private T CreateInstance()
        {
            T instance = UnityEngine.Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            _available.Enqueue(instance);
            return instance;
        }
    }
}
