using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.UI;

namespace AshenThrone.Tests.UI
{
    [TestFixture]
    public class PrivacyConsentManagerTests
    {
        private GameObject _go;
        private PrivacyConsentManager _privacy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            PlayerPrefs.DeleteKey("privacy_consent_given");
            PlayerPrefs.DeleteKey("privacy_consent_version");
            _go = new GameObject("PrivacyTest");
            _privacy = _go.AddComponent<PrivacyConsentManager>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("privacy_consent_given");
            PlayerPrefs.DeleteKey("privacy_consent_version");
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void Register_InServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<PrivacyConsentManager>(out var svc));
            Assert.AreSame(_privacy, svc);
        }

        [Test]
        public void InitialState_NeedsConsent()
        {
            Assert.IsTrue(_privacy.NeedsConsent);
            Assert.IsFalse(_privacy.HasConsented);
        }

        [Test]
        public void AcceptConsent_SetsHasConsented()
        {
            _privacy.AcceptConsent();
            Assert.IsTrue(_privacy.HasConsented);
            Assert.IsFalse(_privacy.NeedsConsent);
        }

        [Test]
        public void AcceptConsent_FiresEvent()
        {
            bool fired = false;
            var sub = EventBus.Subscribe<PrivacyConsentAcceptedEvent>(_ => fired = true);
            _privacy.AcceptConsent();
            Assert.IsTrue(fired);
            sub.Dispose();
        }

        [Test]
        public void DeclineConsent_FiresEvent()
        {
            bool fired = false;
            var sub = EventBus.Subscribe<PrivacyConsentDeclinedEvent>(_ => fired = true);
            _privacy.DeclineConsent();
            Assert.IsTrue(fired);
            sub.Dispose();
        }

        [Test]
        public void DeclineConsent_KeepsNeedsConsent()
        {
            _privacy.DeclineConsent();
            Assert.IsFalse(_privacy.HasConsented);
            Assert.IsTrue(_privacy.NeedsConsent);
        }

        [Test]
        public void ResetConsent_ClearsState()
        {
            _privacy.AcceptConsent();
            Assert.IsTrue(_privacy.HasConsented);
            _privacy.ResetConsent();
            Assert.IsFalse(_privacy.HasConsented);
            Assert.IsTrue(_privacy.NeedsConsent);
        }

        [Test]
        public void AcceptConsent_PersistsToPlayerPrefs()
        {
            _privacy.AcceptConsent();
            Assert.AreEqual(1, PlayerPrefs.GetInt("privacy_consent_given", 0));
        }
    }
}
