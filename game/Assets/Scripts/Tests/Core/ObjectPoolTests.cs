using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Core;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class ObjectPoolTests
    {
        private GameObject _prefabGo;
        private Transform _prefab;
        private GameObject _parentGo;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _prefabGo = new GameObject("PoolPrefab");
            _prefabGo.SetActive(false);
            _prefab = _prefabGo.transform;

            _parentGo = new GameObject("PoolParent");
            _parent = _parentGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all children of parent
            for (int i = _parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_parent.GetChild(i).gameObject);
            Object.DestroyImmediate(_parentGo);
            Object.DestroyImmediate(_prefabGo);
        }

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------

        [Test]
        public void Constructor_NullPrefab_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ObjectPool<Transform>(null, _parent, initialSize: 0, maxSize: 5));
        }

        [Test]
        public void Constructor_PreWarmsInstances()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 3, maxSize: 10);
            Assert.AreEqual(3, pool.CountAvailable);
            Assert.AreEqual(0, pool.CountInUse);
        }

        // -------------------------------------------------------------------
        // Get
        // -------------------------------------------------------------------

        [Test]
        public void Get_ReturnsActiveInstance()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var instance = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.gameObject.activeSelf);
        }

        [Test]
        public void Get_SetsPositionAndRotation()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var pos = new Vector3(1, 2, 3);
            var rot = Quaternion.Euler(0, 90, 0);
            var instance = pool.Get(pos, rot);
            Assert.That(instance.position.x, Is.EqualTo(pos.x).Within(0.01f));
            Assert.That(instance.position.y, Is.EqualTo(pos.y).Within(0.01f));
            Assert.That(instance.position.z, Is.EqualTo(pos.z).Within(0.01f));
        }

        [Test]
        public void Get_DecrementsAvailable_IncrementsInUse()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 3, maxSize: 5);
            pool.Get(Vector3.zero, Quaternion.identity);
            Assert.AreEqual(2, pool.CountAvailable);
            Assert.AreEqual(1, pool.CountInUse);
        }

        [Test]
        public void Get_BeyondPreWarm_CreatesNew()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var a = pool.Get(Vector3.zero, Quaternion.identity);
            var b = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(2, pool.CountInUse);
        }

        [Test]
        public void Get_AtMaxSize_ReturnsNull()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 2, maxSize: 2);
            pool.Get(Vector3.zero, Quaternion.identity);
            pool.Get(Vector3.zero, Quaternion.identity);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Pool exhausted"));
            var third = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.IsNull(third);
        }

        // -------------------------------------------------------------------
        // Return
        // -------------------------------------------------------------------

        [Test]
        public void Return_DeactivatesInstance()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var instance = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(instance);
            Assert.IsFalse(instance.gameObject.activeSelf);
        }

        [Test]
        public void Return_IncrementsAvailable_DecrementsInUse()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var instance = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(instance);
            Assert.AreEqual(1, pool.CountAvailable);
            Assert.AreEqual(0, pool.CountInUse);
        }

        [Test]
        public void Return_Null_DoesNotThrow()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            Assert.DoesNotThrow(() => pool.Return(null));
        }

        [Test]
        public void Return_NotTracked_LogsWarning()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 5);
            var rogue = new GameObject("Rogue").transform;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not tracked as in-use"));
            pool.Return(rogue);

            Object.DestroyImmediate(rogue.gameObject);
        }

        [Test]
        public void Return_ThenGet_ReusesInstance()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 1, maxSize: 1);
            var first = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(first);
            var second = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.AreSame(first, second);
        }

        // -------------------------------------------------------------------
        // ReturnAll
        // -------------------------------------------------------------------

        [Test]
        public void ReturnAll_ReturnsAllInUse()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 3, maxSize: 5);
            pool.Get(Vector3.zero, Quaternion.identity);
            pool.Get(Vector3.zero, Quaternion.identity);
            pool.Get(Vector3.zero, Quaternion.identity);
            Assert.AreEqual(3, pool.CountInUse);

            pool.ReturnAll();
            Assert.AreEqual(0, pool.CountInUse);
            Assert.AreEqual(3, pool.CountAvailable);
        }

        [Test]
        public void ReturnAll_NoInUse_DoesNotThrow()
        {
            var pool = new ObjectPool<Transform>(_prefab, _parent, initialSize: 2, maxSize: 5);
            Assert.DoesNotThrow(() => pool.ReturnAll());
        }
    }
}
