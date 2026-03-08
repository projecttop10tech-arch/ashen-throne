using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Economy;

namespace AshenThrone.Tests.Integration
{
    [TestFixture]
    public class EconomyFlowTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("EconomyFlowTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void IAPManager_RejectsCombatPowerProduct()
        {
            var iap = _go.AddComponent<IAPManager>();

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                iap.RegisterProduct(new IAPProductDefinition(
                    "p2w_sword", "com.test.sword", "com.test.sword",
                    "Power Sword", isCombatPower: true));
            });
        }

        [Test]
        public void IAPManager_AcceptsNonCombatProduct()
        {
            var iap = _go.AddComponent<IAPManager>();

            Assert.DoesNotThrow(() =>
            {
                iap.RegisterProduct(new IAPProductDefinition(
                    "cosmetic_hat", "com.test.hat", "com.test.hat",
                    "Fancy Hat", isCombatPower: false));
            });
        }

        [Test]
        public void IAPCatalog_AllSixProducts_Registered()
        {
            var iap = _go.AddComponent<IAPManager>();
            IAPCatalogRegistrar.RegisterAllProducts(iap);
            Assert.AreEqual(6, iap.GetCatalog().Count);
        }

        [Test]
        public void IAPCatalog_NoProductIs_CombatPower()
        {
            var iap = _go.AddComponent<IAPManager>();
            IAPCatalogRegistrar.RegisterAllProducts(iap);

            foreach (var product in iap.GetCatalog())
                Assert.IsFalse(product.IsCombatPowerProduct,
                    $"P2W VIOLATION: {product.ProductId} grants combat power!");
        }

        [Test]
        public void BattlePassSeason_HasCorrectStructure()
        {
            var season = ScriptableObject.CreateInstance<BattlePassSeason>();
            season.SeasonId = "test_season";
            season.PointsPerTier = new int[50];
            for (int i = 0; i < 50; i++)
                season.PointsPerTier[i] = 100 + i * 20;

            season.FreeRewards = new BattlePassReward[50];
            season.PremiumRewards = new BattlePassReward[50];
            for (int i = 0; i < 50; i++)
            {
                season.FreeRewards[i] = new BattlePassReward
                {
                    RewardId = $"free_{i}", IsCombatPowerReward = false,
                    RewardType = BattlePassRewardType.ResourceBundle, Quantity = 100
                };
                season.PremiumRewards[i] = new BattlePassReward
                {
                    RewardId = $"prem_{i}", IsCombatPowerReward = false,
                    RewardType = BattlePassRewardType.Emote, Quantity = 1
                };
            }

            Assert.AreEqual(50, season.PointsPerTier.Length);
            Assert.AreEqual(50, season.FreeRewards.Length);
            Assert.AreEqual(50, season.PremiumRewards.Length);
        }

        [Test]
        public void BattlePassManager_AddPoints_AdvancesTier()
        {
            var bpManager = _go.AddComponent<BattlePassManager>();
            var season = ScriptableObject.CreateInstance<BattlePassSeason>();
            season.PointsPerTier = new int[] { 100, 120, 140 };
            season.FreeRewards = new BattlePassReward[3];
            season.PremiumRewards = new BattlePassReward[3];
            for (int i = 0; i < 3; i++)
            {
                season.FreeRewards[i] = new BattlePassReward { RewardId = $"f{i}", IsCombatPowerReward = false };
                season.PremiumRewards[i] = new BattlePassReward { RewardId = $"p{i}", IsCombatPowerReward = false };
            }

            bpManager.LoadState(new BattlePassSaveData(), season);
            bpManager.AddPoints(100, "test");
            Assert.AreEqual(1, bpManager.CurrentTier);
        }

        [Test]
        public void BattlePassManager_PremiumReward_BlockedWithoutPremium()
        {
            var bpManager = _go.AddComponent<BattlePassManager>();
            var season = ScriptableObject.CreateInstance<BattlePassSeason>();
            season.PointsPerTier = new int[] { 100 };
            season.FreeRewards = new BattlePassReward[]
            {
                new BattlePassReward { RewardId = "free1", IsCombatPowerReward = false }
            };
            season.PremiumRewards = new BattlePassReward[]
            {
                new BattlePassReward { RewardId = "prem1", IsCombatPowerReward = false }
            };

            bpManager.LoadState(new BattlePassSaveData(), season);
            bpManager.AddPoints(100, "test");

            var result = bpManager.ClaimReward(1, isPremiumReward: true);
            Assert.IsNull(result);
        }

        [Test]
        public void BattlePassManager_P2W_PremiumReward_Blocked()
        {
            var bpManager = _go.AddComponent<BattlePassManager>();
            var season = ScriptableObject.CreateInstance<BattlePassSeason>();
            season.PointsPerTier = new int[] { 100 };
            season.FreeRewards = new BattlePassReward[]
            {
                new BattlePassReward { RewardId = "free1", IsCombatPowerReward = false }
            };
            season.PremiumRewards = new BattlePassReward[]
            {
                new BattlePassReward { RewardId = "p2w_sword", IsCombatPowerReward = true }
            };

            var saveData = new BattlePassSaveData { IsPremiumActive = true };
            bpManager.LoadState(saveData, season);
            bpManager.AddPoints(100, "test");

            var result = bpManager.ClaimReward(1, isPremiumReward: true);
            Assert.IsNull(result, "P2W premium reward should be blocked by IsCombatPowerReward gate");
        }

        [Test]
        public void GachaSystem_PityCounter_StartsAtZero()
        {
            var gacha = _go.AddComponent<GachaSystem>();
            Assert.AreEqual(0, gacha.PityCounter);
            Assert.AreEqual(GachaSystem.PityThreshold, gacha.PullsUntilPity);
        }
    }
}
