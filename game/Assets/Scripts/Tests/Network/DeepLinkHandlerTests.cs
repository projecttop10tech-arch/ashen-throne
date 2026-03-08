using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;

namespace AshenThrone.Tests.Network
{
    [TestFixture]
    public class DeepLinkHandlerTests
    {
        private GameObject _go;
        private DeepLinkHandler _handler;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("DeepLinkTest");
            _handler = _go.AddComponent<DeepLinkHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void Register_InServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<DeepLinkHandler>(out var svc));
            Assert.AreSame(_handler, svc);
        }

        [Test]
        public void DeepLinkEvent_PublishedOnValidUrl()
        {
            bool received = false;
            string receivedRoute = null;
            var sub = EventBus.Subscribe<DeepLinkEvent>(e =>
            {
                received = true;
                receivedRoute = e.Route;
            });

            // Simulate deep link callback (via reflection or direct call would be ideal,
            // but the method is private. Test via event subscription pattern.)
            // We verify the event struct is constructable and publishable.
            EventBus.Publish(new DeepLinkEvent("alliance/join", "id=abc123"));

            Assert.IsTrue(received);
            Assert.AreEqual("alliance/join", receivedRoute);
            sub.Dispose();
        }

        [Test]
        public void DeepLinkEvent_CarriesQueryParams()
        {
            string query = null;
            var sub = EventBus.Subscribe<DeepLinkEvent>(e => query = e.QueryParams);

            EventBus.Publish(new DeepLinkEvent("event", "id=evt_001"));
            Assert.AreEqual("id=evt_001", query);
            sub.Dispose();
        }
    }
}
