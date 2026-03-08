using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Economy
{
    /// <summary>
    /// Registers the 6 IAP SKUs at boot. All products are cosmetic or QoL only (zero P2W).
    /// SKU list from MonetizationDesign.md.
    /// </summary>
    public class IAPCatalogRegistrar : MonoBehaviour
    {
        private void Start()
        {
            if (!ServiceLocator.TryGet<IAPManager>(out var iap))
            {
                Debug.LogWarning("[IAPCatalogRegistrar] IAPManager not registered. Skipping catalog setup.");
                return;
            }

            RegisterAllProducts(iap);
        }

        public static void RegisterAllProducts(IAPManager iap)
        {
            // Battle Pass (seasonal, non-consumable per season)
            iap.RegisterProduct(new IAPProductDefinition(
                "battle_pass_premium",
                "com.ashenthrone.battlepass.premium",
                "com.ashenthrone.battlepass.premium",
                "Premium Battle Pass",
                isCombatPower: false
            ));

            // Gem Packs (consumable, premium currency)
            iap.RegisterProduct(new IAPProductDefinition(
                "gems_small",
                "com.ashenthrone.gems.100",
                "com.ashenthrone.gems.100",
                "100 Gems",
                isCombatPower: false
            ));

            iap.RegisterProduct(new IAPProductDefinition(
                "gems_medium",
                "com.ashenthrone.gems.500",
                "com.ashenthrone.gems.500",
                "500 Gems + Bonus",
                isCombatPower: false
            ));

            iap.RegisterProduct(new IAPProductDefinition(
                "gems_large",
                "com.ashenthrone.gems.1200",
                "com.ashenthrone.gems.1200",
                "1200 Gems + Bonus",
                isCombatPower: false
            ));

            // Cosmetic Bundles
            iap.RegisterProduct(new IAPProductDefinition(
                "cosmetic_starter_pack",
                "com.ashenthrone.cosmetic.starter",
                "com.ashenthrone.cosmetic.starter",
                "Starter Cosmetic Pack",
                isCombatPower: false
            ));

            // Monthly Subscription (QoL benefits, no combat power)
            iap.RegisterProduct(new IAPProductDefinition(
                "monthly_valor_pass",
                "com.ashenthrone.sub.valor",
                "com.ashenthrone.sub.valor",
                "Valor Pass (Monthly)",
                isCombatPower: false
            ));

            Debug.Log("[IAPCatalogRegistrar] Registered 6 IAP products. All cosmetic/QoL — zero P2W.");
        }
    }
}
