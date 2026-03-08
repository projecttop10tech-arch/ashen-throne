using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;
using System.Collections.Generic;

namespace AshenThrone.Tests.Network
{
    [TestFixture]
    public class AnalyticsServiceTests
    {
        private GameObject _go;
        private AnalyticsService _analytics;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            _go = new GameObject("AnalyticsTest");
            _analytics = _go.AddComponent<AnalyticsService>();
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
            _analytics.Initialize();
            Assert.IsTrue(_analytics.IsInitialized);
        }

        [Test]
        public void LogEvent_BeforeInitialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _analytics.LogEvent("test_event"));
        }

        [Test]
        public void LogEvent_AfterInitialize_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.LogEvent("test_event",
                new Dictionary<string, object> { { "key", "value" }, { "count", 42 } }));
        }

        [Test]
        public void SetUserId_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.SetUserId("player_123"));
        }

        [Test]
        public void SetUserProperty_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.SetUserProperty("level", "5"));
        }

        [Test]
        public void LogBattleStart_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.LogBattleStart("ch1_lv1", 1500));
        }

        [Test]
        public void LogBattleEnd_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.LogBattleEnd("ch1_lv1", true, 8));
        }

        [Test]
        public void LogPurchase_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.LogPurchase("gems_small", "USD", 0.99));
        }

        [Test]
        public void LogTutorialStep_DoesNotThrow()
        {
            _analytics.Initialize();
            Assert.DoesNotThrow(() => _analytics.LogTutorialStep(1, "first_battle"));
        }

        [Test]
        public void RegistersWithServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<AnalyticsService>(out var svc));
            Assert.AreSame(_analytics, svc);
        }
    }
}
