using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Economy
{
    // ---------------------------------------------------------------------------
    // IAP product definitions
    // ---------------------------------------------------------------------------

    /// <summary>Platform for receipt validation routing.</summary>
    public enum StorePlatform
    {
        AppleAppStore,
        GooglePlayStore
    }

    /// <summary>Registered IAP product with its store IDs and reward mapping.</summary>
    [System.Serializable]
    public class IAPProductDefinition
    {
        /// <summary>Internal product ID used by the game (not the store ID).</summary>
        public string ProductId { get; }

        /// <summary>Apple App Store product ID.</summary>
        public string AppleProductId { get; }

        /// <summary>Google Play product ID.</summary>
        public string GoogleProductId { get; }

        /// <summary>Display price string (e.g., "$9.99") — set by store SDK at runtime.</summary>
        public string DisplayPrice { get; set; }

        /// <summary>
        /// Human-readable product name (must match App Store Connect / Google Play listing).
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// HARD CHECK: No product may grant combat power advantage (check #40).
        /// Set via constructor to prevent accidental flagging.
        /// </summary>
        public bool IsCombatPowerProduct { get; }

        public IAPProductDefinition(string productId, string appleId, string googleId,
            string displayName, bool isCombatPower = false)
        {
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentException("ProductId cannot be empty.");

            ProductId = productId;
            AppleProductId = appleId;
            GoogleProductId = googleId;
            DisplayName = displayName;
            IsCombatPowerProduct = isCombatPower;
        }
    }

    /// <summary>A pending purchase awaiting server-side receipt validation.</summary>
    public class PendingPurchase
    {
        public string ProductId { get; }
        public string TransactionId { get; }
        public string ReceiptJson { get; }
        public StorePlatform Platform { get; }
        public DateTime InitiatedAtUtc { get; }

        public PendingPurchase(string productId, string transactionId,
            string receiptJson, StorePlatform platform)
        {
            ProductId = productId;
            TransactionId = transactionId;
            ReceiptJson = receiptJson;
            Platform = platform;
            InitiatedAtUtc = DateTime.UtcNow;
        }
    }

    // ---------------------------------------------------------------------------
    // IAPManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages in-app purchases via Unity IAP (wraps Apple/Google IAP).
    ///
    /// Architecture:
    /// - All purchases are server-validated via PlayFab receipt validation (check #49).
    /// - Client NEVER grants rewards locally — only after server confirms receipt.
    /// - Receipts are sent to PlayFab Cloud Script which validates with Apple/Google servers.
    /// - Pending purchases survive app restarts (serialized in PlayerPrefs as fallback).
    ///
    /// Anti-P2W enforcement (check #40):
    /// - RegisterProduct throws if IsCombatPowerProduct is true — design-time enforcement.
    /// - All products are cosmetic or QoL only (Battle Pass, cosmetic store, season pass).
    ///
    /// Platform compliance (check #44, #45):
    /// - ATT prompt is presented before any analytics that requires tracking (check #48).
    /// - All product prices displayed via store-provided localized strings.
    ///
    /// Note: Unity IAP SDK integration is stubbed here. Uncomment real calls after
    /// installing com.unity.purchasing from Package Manager.
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        // Catalog of all purchasable products
        private readonly Dictionary<string, IAPProductDefinition> _catalog = new();

        // Pending purchases (transactionId → purchase)
        private readonly Dictionary<string, PendingPurchase> _pendingPurchases = new();

        public event Action<string> OnPurchaseSuccess; // productId
        public event Action<string, string> OnPurchaseFailed; // productId, reason

        private void Awake()
        {
            ServiceLocator.Register<IAPManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IAPManager>();
        }

        // -------------------------------------------------------------------
        // Catalog setup
        // -------------------------------------------------------------------

        /// <summary>
        /// Register a product in the IAP catalog.
        /// THROWS if IsCombatPowerProduct is true — design-time P2W enforcement (check #40).
        /// </summary>
        public void RegisterProduct(IAPProductDefinition product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));

            if (product.IsCombatPowerProduct)
                throw new InvalidOperationException(
                    $"[IAPManager] DESIGN VIOLATION: Product '{product.ProductId}' is flagged as " +
                    "CombatPowerProduct. No IAP product may grant combat advantage. " +
                    "Remove this product or change IsCombatPowerProduct to false.");

            _catalog[product.ProductId] = product;
        }

        /// <summary>Returns the product definition for a given product ID, or null.</summary>
        public IAPProductDefinition GetProduct(string productId)
        {
            return _catalog.TryGetValue(productId, out var p) ? p : null;
        }

        /// <summary>Returns all registered products (for store UI display).</summary>
        public IReadOnlyCollection<IAPProductDefinition> GetCatalog() => _catalog.Values;

        // -------------------------------------------------------------------
        // Purchase flow
        // -------------------------------------------------------------------

        /// <summary>
        /// Initiate a purchase for the given product.
        /// In production: calls UnityPurchasing.Instance.InitiatePurchase(product).
        /// Returns false if the product is not registered.
        /// </summary>
        public bool InitiatePurchase(string productId)
        {
            if (!_catalog.TryGetValue(productId, out _))
            {
                Debug.LogWarning($"[IAPManager] Product '{productId}' not found in catalog.");
                return false;
            }

            EventBus.Publish(new IAPPurchaseInitiatedEvent(productId));
            // Stub: In production, call Unity IAP SDK here.
            // UnityPurchasing.Instance.InitiatePurchase(AppleProductId or GoogleProductId);
            return true;
        }

        /// <summary>
        /// Called by Unity IAP SDK when a purchase succeeds and a receipt is available.
        /// Queues the receipt for server-side validation — NEVER grants rewards here.
        /// </summary>
        public void OnPurchaseDeferred(string productId, string transactionId,
            string receiptJson, StorePlatform platform)
        {
            if (string.IsNullOrEmpty(receiptJson))
            {
                Debug.LogWarning($"[IAPManager] Purchase {transactionId} has empty receipt. Skipping.");
                return;
            }

            var pending = new PendingPurchase(productId, transactionId, receiptJson, platform);
            _pendingPurchases[transactionId] = pending;

            // Dispatch to PlayFab for server-side validation (check #49)
            ValidateReceiptWithServer(pending);
        }

        /// <summary>
        /// Called when server confirms receipt is valid and grants have been applied.
        /// Removes from pending queue and fires success event.
        /// Called on main thread via MainThreadDispatcher (check #9).
        /// </summary>
        public void OnServerValidationSuccess(string transactionId, string productId)
        {
            _pendingPurchases.Remove(transactionId);
            OnPurchaseSuccess?.Invoke(productId);
            EventBus.Publish(new IAPPurchaseCompletedEvent(productId, transactionId));
        }

        /// <summary>
        /// Called when server validation fails (tampered receipt, already consumed, etc.).
        /// </summary>
        public void OnServerValidationFailed(string transactionId, string productId, string reason)
        {
            _pendingPurchases.Remove(transactionId);
            OnPurchaseFailed?.Invoke(productId, reason);
            Debug.LogWarning($"[IAPManager] Receipt validation failed for {productId} / {transactionId}: {reason}");
            EventBus.Publish(new IAPPurchaseFailedEvent(productId, reason));
        }

        /// <summary>Returns pending (unvalidated) purchases — used for retry on app restart.</summary>
        public IReadOnlyCollection<PendingPurchase> GetPendingPurchases() => _pendingPurchases.Values;

        // -------------------------------------------------------------------
        // Private — server validation dispatch stub
        // -------------------------------------------------------------------

        private void ValidateReceiptWithServer(PendingPurchase purchase)
        {
            // In production: call PlayFab ValidateIOSReceipt / ValidateGooglePlayPurchase
            // Then on callback (main thread via MainThreadDispatcher):
            //   OnServerValidationSuccess or OnServerValidationFailed
            //
            // Stub: log and do nothing (no reward granted without server confirmation).
            Debug.Log($"[IAPManager] Stub: Would validate receipt for '{purchase.ProductId}' " +
                      $"on {purchase.Platform} with transaction '{purchase.TransactionId}'.");
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct IAPPurchaseInitiatedEvent
    {
        public readonly string ProductId;
        public IAPPurchaseInitiatedEvent(string id) { ProductId = id; }
    }

    public readonly struct IAPPurchaseCompletedEvent
    {
        public readonly string ProductId;
        public readonly string TransactionId;
        public IAPPurchaseCompletedEvent(string pid, string tid) { ProductId = pid; TransactionId = tid; }
    }

    public readonly struct IAPPurchaseFailedEvent
    {
        public readonly string ProductId;
        public readonly string Reason;
        public IAPPurchaseFailedEvent(string pid, string reason) { ProductId = pid; Reason = reason; }
    }
}
