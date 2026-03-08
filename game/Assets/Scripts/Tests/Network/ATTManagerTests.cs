using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;

namespace AshenThrone.Tests.Network
{
    [TestFixture]
    public class ATTManagerTests
    {
        private GameObject _go;
        private ATTManager _att;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            _go = new GameObject("ATTTest");
            _att = _go.AddComponent<ATTManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            ServiceLocator.Shutdown();
        }

        [Test]
        public void InitialStatus_IsNotDetermined()
        {
            Assert.AreEqual(ATTManager.TrackingStatus.NotDetermined, _att.Status);
        }

        [Test]
        public void RequestTracking_InEditor_AutoAuthorizes()
        {
            _att.RequestTrackingAuthorization();
            Assert.AreEqual(ATTManager.TrackingStatus.Authorized, _att.Status);
            Assert.IsTrue(_att.HasResponded);
        }

        [Test]
        public void RequestTracking_InvokesCallback()
        {
            ATTManager.TrackingStatus? received = null;
            _att.RequestTrackingAuthorization(status => received = status);
            Assert.AreEqual(ATTManager.TrackingStatus.Authorized, received);
        }

        [Test]
        public void RequestTracking_FiresEvent()
        {
            ATTManager.TrackingStatus? eventStatus = null;
            _att.OnTrackingStatusDetermined += status => eventStatus = status;
            _att.RequestTrackingAuthorization();
            Assert.AreEqual(ATTManager.TrackingStatus.Authorized, eventStatus);
        }

        [Test]
        public void RegistersWithServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<ATTManager>(out var svc));
            Assert.AreSame(_att, svc);
        }
    }
}
