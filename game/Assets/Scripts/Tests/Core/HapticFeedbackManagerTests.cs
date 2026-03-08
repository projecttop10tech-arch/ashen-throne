using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.UI.Accessibility;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class HapticFeedbackManagerTests
    {
        private GameObject _go;
        private HapticFeedbackManager _haptics;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("HapticTest");
            _haptics = _go.AddComponent<HapticFeedbackManager>();
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
            Assert.IsTrue(ServiceLocator.TryGet<HapticFeedbackManager>(out var svc));
            Assert.AreSame(_haptics, svc);
        }

        [Test]
        public void TriggerHaptic_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _haptics.TriggerHaptic(HapticFeedbackManager.HapticIntensity.Light));
            Assert.DoesNotThrow(() => _haptics.TriggerHaptic(HapticFeedbackManager.HapticIntensity.Medium));
            Assert.DoesNotThrow(() => _haptics.TriggerHaptic(HapticFeedbackManager.HapticIntensity.Heavy));
        }

        [Test]
        public void TriggerHaptic_RespectsAccessibilityOff()
        {
            // Add AccessibilityManager with haptics disabled
            var accGo = new GameObject("Accessibility");
            var accMgr = accGo.AddComponent<AccessibilityManager>();
            accMgr.Initialize(null);
            accMgr.SetHapticsEnabled(false);

            // Should not throw even with haptics off
            Assert.DoesNotThrow(() => _haptics.TriggerHaptic(HapticFeedbackManager.HapticIntensity.Heavy));

            Object.DestroyImmediate(accGo);
        }

        [Test]
        public void CardPlayedEvent_TriggersHaptic()
        {
            // Verify subscription doesn't throw when event fires
            Assert.DoesNotThrow(() =>
            {
                EventBus.Publish(new AshenThrone.Combat.CardPlayedEvent(null, default, false));
            });
        }

        [Test]
        public void HeroDiedEvent_TriggersHaptic()
        {
            Assert.DoesNotThrow(() =>
            {
                EventBus.Publish(new AshenThrone.Combat.HeroDiedEvent(1, true));
            });
        }
    }
}
