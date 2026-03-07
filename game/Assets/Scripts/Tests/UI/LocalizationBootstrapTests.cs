using System;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.UI.Localization;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.UI
{
    /// <summary>
    /// Unit tests for LocalizationBootstrap.
    /// </summary>
    [TestFixture]
    public class LocalizationBootstrapTests
    {
        private GameObject _go;
        private LocalizationBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LocalizationBootstrapTest");
            _bootstrap = _go.AddComponent<LocalizationBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // -------------------------------------------------------------------
        // Initialize
        // -------------------------------------------------------------------

        [Test]
        public void Initialize_NullSave_AutoDetectsLanguage()
        {
            _bootstrap.Initialize(null);
            Assert.IsTrue(_bootstrap.IsInitialized);
            // Cannot assert exact language (depends on machine locale), but should be valid
            Assert.IsTrue(Enum.IsDefined(typeof(GameLanguage), _bootstrap.CurrentLanguage));
        }

        [Test]
        public void Initialize_WithSave_RestoresLanguage()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = (int)GameLanguage.Japanese });
            Assert.AreEqual(GameLanguage.Japanese, _bootstrap.CurrentLanguage);
        }

        [Test]
        public void Initialize_InvalidSaveIndex_FallsBackToAutoDetect()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = 999 });
            Assert.IsTrue(_bootstrap.IsInitialized);
            Assert.IsTrue(Enum.IsDefined(typeof(GameLanguage), _bootstrap.CurrentLanguage));
        }

        [Test]
        public void Initialize_NegativeSaveIndex_FallsBackToAutoDetect()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = -1 });
            Assert.IsTrue(_bootstrap.IsInitialized);
        }

        // -------------------------------------------------------------------
        // SetLanguage
        // -------------------------------------------------------------------

        [Test]
        public void SetLanguage_ChangesCurrentLanguage()
        {
            _bootstrap.Initialize(null);
            _bootstrap.SetLanguage(GameLanguage.Korean);
            Assert.AreEqual(GameLanguage.Korean, _bootstrap.CurrentLanguage);
        }

        [Test]
        public void SetLanguage_SameLanguage_IsNoOp()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = (int)GameLanguage.French });
            _bootstrap.SetLanguage(GameLanguage.French);
            Assert.AreEqual(GameLanguage.French, _bootstrap.CurrentLanguage);
        }

        // -------------------------------------------------------------------
        // GetLanguageCode
        // -------------------------------------------------------------------

        [Test]
        public void GetLanguageCode_ReturnsCorrectCodes()
        {
            Assert.AreEqual("en", LocalizationBootstrap.GetLanguageCode(GameLanguage.English));
            Assert.AreEqual("es", LocalizationBootstrap.GetLanguageCode(GameLanguage.Spanish));
            Assert.AreEqual("fr", LocalizationBootstrap.GetLanguageCode(GameLanguage.French));
            Assert.AreEqual("de", LocalizationBootstrap.GetLanguageCode(GameLanguage.German));
            Assert.AreEqual("pt", LocalizationBootstrap.GetLanguageCode(GameLanguage.Portuguese));
            Assert.AreEqual("ja", LocalizationBootstrap.GetLanguageCode(GameLanguage.Japanese));
            Assert.AreEqual("ko", LocalizationBootstrap.GetLanguageCode(GameLanguage.Korean));
            Assert.AreEqual("zh", LocalizationBootstrap.GetLanguageCode(GameLanguage.ChineseSimplified));
        }

        // -------------------------------------------------------------------
        // CurrentLanguageCode
        // -------------------------------------------------------------------

        [Test]
        public void CurrentLanguageCode_MatchesCurrentLanguage()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = (int)GameLanguage.German });
            Assert.AreEqual("de", _bootstrap.CurrentLanguageCode);
        }

        // -------------------------------------------------------------------
        // GetSupportedLanguages
        // -------------------------------------------------------------------

        [Test]
        public void GetSupportedLanguages_Returns8Languages()
        {
            var languages = LocalizationBootstrap.GetSupportedLanguages();
            Assert.AreEqual(LocalizationBootstrap.SupportedLanguageCount, languages.Length);
        }

        // -------------------------------------------------------------------
        // GetDisplayName
        // -------------------------------------------------------------------

        [Test]
        public void GetDisplayName_ReturnsNonEmpty_ForAllLanguages()
        {
            foreach (GameLanguage lang in LocalizationBootstrap.GetSupportedLanguages())
            {
                string name = LocalizationBootstrap.GetDisplayName(lang);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"Display name is empty for {lang}");
            }
        }

        // -------------------------------------------------------------------
        // BuildSaveData
        // -------------------------------------------------------------------

        [Test]
        public void BuildSaveData_ReflectsCurrentLanguage()
        {
            _bootstrap.Initialize(null);
            _bootstrap.SetLanguage(GameLanguage.Spanish);
            var save = _bootstrap.BuildSaveData();
            Assert.AreEqual((int)GameLanguage.Spanish, save.LanguageIndex);
        }

        [Test]
        public void BuildSaveData_RoundTrips()
        {
            _bootstrap.Initialize(new LocalizationSaveData { LanguageIndex = (int)GameLanguage.Portuguese });
            var save = _bootstrap.BuildSaveData();
            Assert.AreEqual((int)GameLanguage.Portuguese, save.LanguageIndex);
        }

        // -------------------------------------------------------------------
        // SupportedLanguageCount constant
        // -------------------------------------------------------------------

        [Test]
        public void SupportedLanguageCount_Equals8()
        {
            Assert.AreEqual(8, LocalizationBootstrap.SupportedLanguageCount);
        }
    }
}
