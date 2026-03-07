using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.UI.Accessibility;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.UI
{
    /// <summary>
    /// Unit tests for AccessibilityManager.
    /// AccessibilityConfig uses [field: SerializeField] — set via backing field reflection.
    /// </summary>
    [TestFixture]
    public class AccessibilityManagerTests
    {
        private GameObject _go;
        private AccessibilityManager _manager;
        private AccessibilityConfig _config;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AccessibilityManagerTest");
            _manager = _go.AddComponent<AccessibilityManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void InjectConfig(float minScale = 0.8f, float maxScale = 1.5f, float defaultScale = 1.0f,
            bool defaultHaptics = true, float minBright = 0.7f, float maxBright = 1.3f)
        {
            _config = ScriptableObject.CreateInstance<AccessibilityConfig>();
            SetBacking(_config, "MinTextScale", minScale);
            SetBacking(_config, "MaxTextScale", maxScale);
            SetBacking(_config, "DefaultTextScale", defaultScale);
            SetBacking(_config, "DefaultHapticsEnabled", defaultHaptics);
            SetBacking(_config, "MinBrightness", minBright);
            SetBacking(_config, "MaxBrightness", maxBright);

            var field = typeof(AccessibilityManager).GetField("_config",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "AccessibilityManager._config field not found.");
            field.SetValue(_manager, _config);
        }

        private static void SetBacking(object target, string propName, object value)
        {
            string name = $"<{propName}>k__BackingField";
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        // -------------------------------------------------------------------
        // Initialize
        // -------------------------------------------------------------------

        [Test]
        public void Initialize_NullSave_SetsDefaults()
        {
            InjectConfig();
            _manager.Initialize(null);
            Assert.AreEqual(ColorblindMode.None, _manager.ColorblindMode);
            Assert.AreEqual(1.0f, _manager.TextSizeScale, 0.001f);
            Assert.IsTrue(_manager.HapticsEnabled);
            Assert.IsFalse(_manager.ScreenReaderEnabled);
            Assert.IsFalse(_manager.ReduceMotion);
            Assert.AreEqual(1.0f, _manager.UiBrightness, 0.001f);
        }

        [Test]
        public void Initialize_NullSave_NoConfig_SetsHardDefaults()
        {
            // No config injected — should use hardcoded fallbacks
            _manager.Initialize(null);
            Assert.AreEqual(1.0f, _manager.TextSizeScale, 0.001f);
            Assert.IsTrue(_manager.HapticsEnabled);
        }

        [Test]
        public void Initialize_RestoresFromSave()
        {
            InjectConfig();
            var save = new AccessibilitySaveData
            {
                ColorblindModeIndex = 2, // Deuteranopia
                TextSizeScale = 1.2f,
                HapticsEnabled = false,
                ScreenReaderEnabled = true,
                ReduceMotion = true,
                UiBrightness = 0.9f
            };
            _manager.Initialize(save);
            Assert.AreEqual(ColorblindMode.Deuteranopia, _manager.ColorblindMode);
            Assert.AreEqual(1.2f, _manager.TextSizeScale, 0.001f);
            Assert.IsFalse(_manager.HapticsEnabled);
            Assert.IsTrue(_manager.ScreenReaderEnabled);
            Assert.IsTrue(_manager.ReduceMotion);
            Assert.AreEqual(0.9f, _manager.UiBrightness, 0.001f);
        }

        [Test]
        public void Initialize_ClampsColorblindModeIndex()
        {
            InjectConfig();
            _manager.Initialize(new AccessibilitySaveData { ColorblindModeIndex = 99 });
            // Clamped to max valid (3 = Tritanopia)
            Assert.AreEqual(ColorblindMode.Tritanopia, _manager.ColorblindMode);
        }

        [Test]
        public void Initialize_ClampsTextScale_ToConfigRange()
        {
            InjectConfig(minScale: 0.8f, maxScale: 1.5f);
            _manager.Initialize(new AccessibilitySaveData { TextSizeScale = 5.0f });
            Assert.AreEqual(1.5f, _manager.TextSizeScale, 0.001f);
        }

        [Test]
        public void Initialize_ClampsTextScale_BelowMin()
        {
            InjectConfig(minScale: 0.8f, maxScale: 1.5f);
            _manager.Initialize(new AccessibilitySaveData { TextSizeScale = 0.1f });
            Assert.AreEqual(0.8f, _manager.TextSizeScale, 0.001f);
        }

        [Test]
        public void Initialize_ClampsBrightness()
        {
            InjectConfig(minBright: 0.7f, maxBright: 1.3f);
            _manager.Initialize(new AccessibilitySaveData { UiBrightness = 2.0f });
            Assert.AreEqual(1.3f, _manager.UiBrightness, 0.001f);
        }

        // -------------------------------------------------------------------
        // Setters
        // -------------------------------------------------------------------

        [Test]
        public void SetColorblindMode_ChangesMode()
        {
            InjectConfig();
            _manager.Initialize(null);
            _manager.SetColorblindMode(ColorblindMode.Protanopia);
            Assert.AreEqual(ColorblindMode.Protanopia, _manager.ColorblindMode);
        }

        [Test]
        public void SetColorblindMode_SameValue_IsNoOp()
        {
            InjectConfig();
            _manager.Initialize(null);
            _manager.SetColorblindMode(ColorblindMode.None); // already None
            Assert.AreEqual(ColorblindMode.None, _manager.ColorblindMode);
        }

        [Test]
        public void SetTextSizeScale_ClampsToRange()
        {
            InjectConfig(minScale: 0.8f, maxScale: 1.5f);
            _manager.Initialize(null);
            _manager.SetTextSizeScale(3.0f);
            Assert.AreEqual(1.5f, _manager.TextSizeScale, 0.001f);

            _manager.SetTextSizeScale(0.1f);
            Assert.AreEqual(0.8f, _manager.TextSizeScale, 0.001f);
        }

        [Test]
        public void SetHapticsEnabled_TogglesValue()
        {
            _manager.Initialize(null);
            _manager.SetHapticsEnabled(false);
            Assert.IsFalse(_manager.HapticsEnabled);
            _manager.SetHapticsEnabled(true);
            Assert.IsTrue(_manager.HapticsEnabled);
        }

        [Test]
        public void SetScreenReaderEnabled_TogglesValue()
        {
            _manager.Initialize(null);
            _manager.SetScreenReaderEnabled(true);
            Assert.IsTrue(_manager.ScreenReaderEnabled);
        }

        [Test]
        public void SetReduceMotion_TogglesValue()
        {
            _manager.Initialize(null);
            _manager.SetReduceMotion(true);
            Assert.IsTrue(_manager.ReduceMotion);
        }

        [Test]
        public void SetUiBrightness_ClampsToRange()
        {
            InjectConfig(minBright: 0.7f, maxBright: 1.3f);
            _manager.Initialize(null);
            _manager.SetUiBrightness(0.5f);
            Assert.AreEqual(0.7f, _manager.UiBrightness, 0.001f);
        }

        // -------------------------------------------------------------------
        // BuildSaveData
        // -------------------------------------------------------------------

        [Test]
        public void BuildSaveData_ReflectsCurrentState()
        {
            InjectConfig();
            _manager.Initialize(null);
            _manager.SetColorblindMode(ColorblindMode.Tritanopia);
            _manager.SetTextSizeScale(1.3f);
            _manager.SetHapticsEnabled(false);
            _manager.SetReduceMotion(true);

            var save = _manager.BuildSaveData();
            Assert.AreEqual(3, save.ColorblindModeIndex); // Tritanopia = 3
            Assert.AreEqual(1.3f, save.TextSizeScale, 0.001f);
            Assert.IsFalse(save.HapticsEnabled);
            Assert.IsTrue(save.ReduceMotion);
        }

        [Test]
        public void BuildSaveData_RoundTrips()
        {
            InjectConfig();
            var original = new AccessibilitySaveData
            {
                ColorblindModeIndex = 1,
                TextSizeScale = 1.1f,
                HapticsEnabled = false,
                ScreenReaderEnabled = true,
                ReduceMotion = true,
                UiBrightness = 0.8f
            };
            _manager.Initialize(original);
            var rebuilt = _manager.BuildSaveData();

            Assert.AreEqual(original.ColorblindModeIndex, rebuilt.ColorblindModeIndex);
            Assert.AreEqual(original.TextSizeScale, rebuilt.TextSizeScale, 0.001f);
            Assert.AreEqual(original.HapticsEnabled, rebuilt.HapticsEnabled);
            Assert.AreEqual(original.ScreenReaderEnabled, rebuilt.ScreenReaderEnabled);
            Assert.AreEqual(original.ReduceMotion, rebuilt.ReduceMotion);
            Assert.AreEqual(original.UiBrightness, rebuilt.UiBrightness, 0.001f);
        }
    }
}
