using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;

namespace AshenThrone.Tests.Network
{
    [TestFixture]
    public class CrashReporterTests
    {
        private GameObject _go;
        private CrashReporter _crash;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            _go = new GameObject("CrashTest");
            _crash = _go.AddComponent<CrashReporter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            ServiceLocator.Shutdown();
        }

        [Test]
        public void Initialize_SetsIsInitializedTrue()
        {
            _crash.Initialize();
            Assert.IsTrue(_crash.IsInitialized);
        }

        [Test]
        public void SetUserId_DoesNotThrow()
        {
            _crash.Initialize();
            Assert.DoesNotThrow(() => _crash.SetUserId("player_123"));
        }

        [Test]
        public void SetCustomKey_DoesNotThrow()
        {
            _crash.Initialize();
            Assert.DoesNotThrow(() => _crash.SetCustomKey("scene", "Combat"));
        }

        [Test]
        public void Log_DoesNotThrow()
        {
            _crash.Initialize();
            Assert.DoesNotThrow(() => _crash.Log("Player entered combat"));
        }

        [Test]
        public void LogException_DoesNotThrow()
        {
            _crash.Initialize();
            Assert.DoesNotThrow(() => _crash.LogException("NullRef", "at GameManager.cs:50"));
        }

        [Test]
        public void RegistersWithServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<CrashReporter>(out var svc));
            Assert.AreSame(_crash, svc);
        }
    }
}
