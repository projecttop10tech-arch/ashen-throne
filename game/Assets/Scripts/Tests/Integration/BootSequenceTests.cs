using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;

namespace AshenThrone.Tests.Integration
{
    [TestFixture]
    public class BootSequenceTests
    {
        private GameObject _managerGo;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _managerGo = new GameObject("BootTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_managerGo);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void ServiceLocator_Initialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ServiceLocator.Initialize());
        }

        [Test]
        public void EventBus_Initialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Initialize());
        }

        [Test]
        public void PlayFabService_Registers_OnAwake()
        {
            var pfService = _managerGo.AddComponent<PlayFabService>();
            Assert.IsTrue(ServiceLocator.TryGet<PlayFabService>(out var svc));
            Assert.AreSame(pfService, svc);
        }

        [Test]
        public void AnalyticsService_Registers_OnAwake()
        {
            var analytics = _managerGo.AddComponent<AnalyticsService>();
            Assert.IsTrue(ServiceLocator.TryGet<AnalyticsService>(out var svc));
            Assert.AreSame(analytics, svc);
        }

        [Test]
        public void CrashReporter_Registers_OnAwake()
        {
            var crash = _managerGo.AddComponent<CrashReporter>();
            Assert.IsTrue(ServiceLocator.TryGet<CrashReporter>(out var svc));
            Assert.AreSame(crash, svc);
        }

        [Test]
        public void ATTManager_Registers_OnAwake()
        {
            var att = _managerGo.AddComponent<ATTManager>();
            Assert.IsTrue(ServiceLocator.TryGet<ATTManager>(out var svc));
            Assert.AreSame(att, svc);
        }

        [Test]
        public void PhotonManager_Registers_OnAwake()
        {
            var photon = _managerGo.AddComponent<PhotonManager>();
            Assert.IsTrue(ServiceLocator.TryGet<PhotonManager>(out var svc));
            Assert.AreSame(photon, svc);
        }

        [Test]
        public void AllBootServices_CanCoexist()
        {
            _managerGo.AddComponent<PlayFabService>();
            _managerGo.AddComponent<AnalyticsService>();
            _managerGo.AddComponent<CrashReporter>();
            _managerGo.AddComponent<ATTManager>();
            _managerGo.AddComponent<PhotonManager>();

            Assert.IsTrue(ServiceLocator.TryGet<PlayFabService>(out _));
            Assert.IsTrue(ServiceLocator.TryGet<AnalyticsService>(out _));
            Assert.IsTrue(ServiceLocator.TryGet<CrashReporter>(out _));
            Assert.IsTrue(ServiceLocator.TryGet<ATTManager>(out _));
            Assert.IsTrue(ServiceLocator.TryGet<PhotonManager>(out _));
        }

        [Test]
        public void ATT_AutoAuthorizes_InEditor()
        {
            var att = _managerGo.AddComponent<ATTManager>();
            att.RequestTrackingAuthorization();
            Assert.IsTrue(att.HasResponded);
            Assert.AreEqual(ATTManager.TrackingStatus.Authorized, att.Status);
        }

        [Test]
        public void Analytics_InitializesAfterATT()
        {
            var att = _managerGo.AddComponent<ATTManager>();
            var analytics = _managerGo.AddComponent<AnalyticsService>();

            att.RequestTrackingAuthorization();
            Assert.IsTrue(att.HasResponded);

            analytics.Initialize();
            Assert.IsTrue(analytics.IsInitialized);
        }

        [Test]
        public void CrashReporter_InitializesBeforeAnalytics()
        {
            var crash = _managerGo.AddComponent<CrashReporter>();
            crash.Initialize();
            Assert.IsTrue(crash.IsInitialized);
        }
    }
}
