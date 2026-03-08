using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Economy;

namespace AshenThrone.Tests.Economy
{
    [TestFixture]
    public class IAPCatalogRegistrarTests
    {
        private GameObject _go;
        private IAPManager _iap;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("IAPTest");
            _iap = _go.AddComponent<IAPManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void RegisterAllProducts_Registers6Products()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            Assert.AreEqual(6, _iap.GetCatalog().Count);
        }

        [Test]
        public void RegisterAllProducts_BattlePassExists()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            Assert.IsNotNull(_iap.GetProduct("battle_pass_premium"));
        }

        [Test]
        public void RegisterAllProducts_AllGemsExist()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            Assert.IsNotNull(_iap.GetProduct("gems_small"));
            Assert.IsNotNull(_iap.GetProduct("gems_medium"));
            Assert.IsNotNull(_iap.GetProduct("gems_large"));
        }

        [Test]
        public void RegisterAllProducts_CosmeticStarterPackExists()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            Assert.IsNotNull(_iap.GetProduct("cosmetic_starter_pack"));
        }

        [Test]
        public void RegisterAllProducts_ValorPassExists()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            Assert.IsNotNull(_iap.GetProduct("monthly_valor_pass"));
        }

        [Test]
        public void RegisterAllProducts_NoneAreCombatPower()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            foreach (var product in _iap.GetCatalog())
            {
                Assert.IsFalse(product.IsCombatPowerProduct,
                    $"Product '{product.ProductId}' must NOT be combat power (P2W check #40).");
            }
        }

        [Test]
        public void RegisterAllProducts_AllHaveAppleIds()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            foreach (var product in _iap.GetCatalog())
            {
                Assert.IsNotEmpty(product.AppleProductId,
                    $"Product '{product.ProductId}' missing Apple product ID.");
            }
        }

        [Test]
        public void RegisterAllProducts_AllHaveGoogleIds()
        {
            IAPCatalogRegistrar.RegisterAllProducts(_iap);
            foreach (var product in _iap.GetCatalog())
            {
                Assert.IsNotEmpty(product.GoogleProductId,
                    $"Product '{product.ProductId}' missing Google product ID.");
            }
        }
    }
}
