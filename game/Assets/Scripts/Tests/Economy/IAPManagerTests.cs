using System;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Economy;

namespace AshenThrone.Tests.Economy
{
    [TestFixture]
    public class IAPManagerTests
    {
        private GameObject _go;
        private IAPManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("IAPManagerTest");
            _manager = _go.AddComponent<IAPManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // --- RegisterProduct ---

        [Test]
        public void RegisterProduct_ThrowsArgumentNull_WhenProductNull()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.RegisterProduct(null));
        }

        [Test]
        public void RegisterProduct_ThrowsInvalidOperation_WhenCombatPowerFlagged()
        {
            var product = new IAPProductDefinition(
                "p2w_item", "p2w.apple", "p2w.google", "P2W Item", isCombatPower: true);
            Assert.Throws<InvalidOperationException>(() => _manager.RegisterProduct(product),
                "P2W products must be rejected at registration time");
        }

        [Test]
        public void RegisterProduct_DoesNotThrow_ForCosmeticProduct()
        {
            var product = new IAPProductDefinition(
                "skin_pack", "skin.apple", "skin.google", "Skin Pack", isCombatPower: false);
            Assert.DoesNotThrow(() => _manager.RegisterProduct(product));
        }

        [Test]
        public void RegisterProduct_MakesProductQueryable()
        {
            var product = new IAPProductDefinition("bp_premium", "bp.apple", "bp.google", "Battle Pass Premium");
            _manager.RegisterProduct(product);
            Assert.IsNotNull(_manager.GetProduct("bp_premium"));
        }

        // --- GetProduct ---

        [Test]
        public void GetProduct_ReturnsNull_ForUnregisteredId()
        {
            Assert.IsNull(_manager.GetProduct("nonexistent"));
        }

        [Test]
        public void GetProduct_ReturnsCorrectProduct()
        {
            var product = new IAPProductDefinition("test_id", "a", "g", "Test Product");
            _manager.RegisterProduct(product);
            var retrieved = _manager.GetProduct("test_id");
            Assert.AreEqual("test_id", retrieved.ProductId);
        }

        // --- GetCatalog ---

        [Test]
        public void GetCatalog_ReturnsAllRegisteredProducts()
        {
            _manager.RegisterProduct(new IAPProductDefinition("p1", "a1", "g1", "Product 1"));
            _manager.RegisterProduct(new IAPProductDefinition("p2", "a2", "g2", "Product 2"));
            Assert.AreEqual(2, _manager.GetCatalog().Count);
        }

        // --- InitiatePurchase ---

        [Test]
        public void InitiatePurchase_ReturnsFalse_ForUnregisteredProduct()
        {
            Assert.IsFalse(_manager.InitiatePurchase("unregistered"));
        }

        [Test]
        public void InitiatePurchase_ReturnsTrue_ForRegisteredProduct()
        {
            _manager.RegisterProduct(new IAPProductDefinition("bp", "bp.apple", "bp.google", "Battle Pass"));
            Assert.IsTrue(_manager.InitiatePurchase("bp"));
        }

        // --- OnPurchaseDeferred / Pending queue ---

        [Test]
        public void OnPurchaseDeferred_DoesNotAddToPending_WhenReceiptEmpty()
        {
            _manager.RegisterProduct(new IAPProductDefinition("bp", "bp.apple", "bp.google", "BP"));
            _manager.OnPurchaseDeferred("bp", "txn_1", "", StorePlatform.AppleAppStore);
            Assert.AreEqual(0, _manager.GetPendingPurchases().Count);
        }

        [Test]
        public void OnPurchaseDeferred_AddsToPendingQueue_WhenReceiptValid()
        {
            _manager.RegisterProduct(new IAPProductDefinition("bp", "bp.apple", "bp.google", "BP"));
            _manager.OnPurchaseDeferred("bp", "txn_1", "{\"receipt\":\"fake\"}", StorePlatform.AppleAppStore);
            Assert.AreEqual(1, _manager.GetPendingPurchases().Count);
        }

        // --- OnServerValidationSuccess ---

        [Test]
        public void OnServerValidationSuccess_RemovesFromPendingQueue()
        {
            _manager.RegisterProduct(new IAPProductDefinition("bp", "bp.apple", "bp.google", "BP"));
            _manager.OnPurchaseDeferred("bp", "txn_1", "{\"r\":\"fake\"}", StorePlatform.AppleAppStore);
            Assert.AreEqual(1, _manager.GetPendingPurchases().Count);
            _manager.OnServerValidationSuccess("txn_1", "bp");
            Assert.AreEqual(0, _manager.GetPendingPurchases().Count);
        }

        [Test]
        public void OnServerValidationSuccess_FiresOnPurchaseSuccessEvent()
        {
            bool fired = false;
            _manager.OnPurchaseSuccess += _ => fired = true;
            _manager.OnServerValidationSuccess("txn_1", "bp");
            Assert.IsTrue(fired);
        }

        // --- OnServerValidationFailed ---

        [Test]
        public void OnServerValidationFailed_RemovesFromPendingQueue()
        {
            _manager.RegisterProduct(new IAPProductDefinition("bp", "bp.apple", "bp.google", "BP"));
            _manager.OnPurchaseDeferred("bp", "txn_1", "{\"r\":\"fake\"}", StorePlatform.GooglePlayStore);
            _manager.OnServerValidationFailed("txn_1", "bp", "Receipt already consumed.");
            Assert.AreEqual(0, _manager.GetPendingPurchases().Count);
        }

        [Test]
        public void OnServerValidationFailed_FiresOnPurchaseFailedEvent()
        {
            string failedProduct = null;
            _manager.OnPurchaseFailed += (pid, _) => failedProduct = pid;
            _manager.OnServerValidationFailed("txn_1", "bp_premium", "Invalid receipt.");
            Assert.AreEqual("bp_premium", failedProduct);
        }

        // --- IAPProductDefinition ---

        [Test]
        public void IAPProductDefinition_ThrowsArgumentException_WhenIdEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                new IAPProductDefinition("", "a", "g", "Name"));
        }
    }
}
