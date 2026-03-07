using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Economy;

namespace AshenThrone.Tests.Economy
{
    [TestFixture]
    public class BattlePassManagerTests
    {
        private GameObject _go;
        private BattlePassManager _manager;
        private BattlePassSeason _season;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BattlePassManagerTest");
            _manager = _go.AddComponent<BattlePassManager>();
            _season = MakeSeason();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_season);
        }

        // --- LoadState ---

        [Test]
        public void LoadState_ThrowsArgumentNull_WhenSeasonNull()
        {
            var save = new BattlePassSaveData();
            Assert.Throws<System.ArgumentNullException>(() => _manager.LoadState(save, null));
        }

        [Test]
        public void LoadState_ClampsTierToTotalTiers()
        {
            var save = new BattlePassSaveData { CurrentTier = 9999 };
            _manager.LoadState(save, _season);
            Assert.AreEqual(BattlePassManager.TotalTiers, _manager.CurrentTier);
        }

        [Test]
        public void LoadState_SetsPremiumActive_FromSave()
        {
            var save = new BattlePassSaveData { IsPremiumActive = true };
            _manager.LoadState(save, _season);
            Assert.IsTrue(_manager.IsPremiumActive);
        }

        // --- AddPoints ---

        [Test]
        public void AddPoints_IncreasesCurrentPoints()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            _manager.AddPoints(100, "test");
            Assert.AreEqual(100, _manager.CurrentPoints);
        }

        [Test]
        public void AddPoints_IgnoresZeroOrNegative()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            _manager.AddPoints(0, "test");
            _manager.AddPoints(-50, "test");
            Assert.AreEqual(0, _manager.CurrentPoints);
        }

        [Test]
        public void AddPoints_AdvancesTier_WhenThresholdMet()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            _manager.AddPoints(500, "test"); // Season points-per-tier = 500
            Assert.AreEqual(1, _manager.CurrentTier);
        }

        [Test]
        public void AddPoints_FiresOnTierAdvancedEvent()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            int tierReceived = -1;
            _manager.OnTierAdvanced += t => tierReceived = t;
            _manager.AddPoints(500, "test");
            Assert.AreEqual(1, tierReceived);
        }

        [Test]
        public void AddPoints_DoesNotAdvanceBeyondTotalTiers()
        {
            var save = new BattlePassSaveData { CurrentTier = BattlePassManager.TotalTiers };
            _manager.LoadState(save, _season);
            _manager.AddPoints(99999, "test");
            Assert.AreEqual(BattlePassManager.TotalTiers, _manager.CurrentTier);
        }

        // --- ActivatePremiumTrack ---

        [Test]
        public void ActivatePremiumTrack_SetsPremiumActiveTrue()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            _manager.ActivatePremiumTrack();
            Assert.IsTrue(_manager.IsPremiumActive);
        }

        [Test]
        public void ActivatePremiumTrack_IsIdempotent()
        {
            _manager.LoadState(new BattlePassSaveData(), _season);
            int callCount = 0;
            _manager.OnPremiumActivated += () => callCount++;
            _manager.ActivatePremiumTrack();
            _manager.ActivatePremiumTrack();
            Assert.AreEqual(1, callCount);
        }

        // --- ClaimReward ---

        [Test]
        public void ClaimReward_ReturnsNull_WhenTierNotReached()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 0 }, _season);
            var reward = _manager.ClaimReward(1, false);
            Assert.IsNull(reward);
        }

        [Test]
        public void ClaimReward_ReturnsFreeReward_WhenTierReached()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 1 }, _season);
            var reward = _manager.ClaimReward(1, false);
            Assert.IsNotNull(reward);
        }

        [Test]
        public void ClaimReward_ReturnsNull_WhenAlreadyClaimed()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 1 }, _season);
            _manager.ClaimReward(1, false);
            var second = _manager.ClaimReward(1, false);
            Assert.IsNull(second);
        }

        [Test]
        public void ClaimReward_ReturnsNull_WhenPremiumNotActive_ForPremiumReward()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 1, IsPremiumActive = false }, _season);
            var reward = _manager.ClaimReward(1, true);
            Assert.IsNull(reward);
        }

        [Test]
        public void ClaimReward_ReturnsPremiumReward_WhenPremiumActive()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 1, IsPremiumActive = true }, _season);
            var reward = _manager.ClaimReward(1, true);
            Assert.IsNotNull(reward);
        }

        [Test]
        public void ClaimReward_RejectsReward_IfMarkedAsCombatPower()
        {
            _manager.LoadState(new BattlePassSaveData { CurrentTier = 2, IsPremiumActive = true }, _season);
            // Tier 2 premium reward is flagged as combat power in MakeSeason()
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DESIGN VIOLATION"));
            var reward = _manager.ClaimReward(2, true);
            Assert.IsNull(reward, "Combat power rewards must be rejected by ClaimReward");
        }

        // --- Helpers ---

        private static BattlePassSeason MakeSeason()
        {
            var season = ScriptableObject.CreateInstance<BattlePassSeason>();
            season.SeasonId = "test_season";
            season.SeasonName = "Test Season";

            season.PointsPerTier = new int[BattlePassManager.TotalTiers];
            for (int i = 0; i < season.PointsPerTier.Length; i++)
                season.PointsPerTier[i] = 500;

            season.FreeRewards = new BattlePassReward[BattlePassManager.TotalTiers];
            season.PremiumRewards = new BattlePassReward[BattlePassManager.TotalTiers];

            for (int i = 0; i < BattlePassManager.TotalTiers; i++)
            {
                season.FreeRewards[i] = new BattlePassReward
                {
                    RewardId = $"free_{i + 1}",
                    RewardType = BattlePassRewardType.ResourceBundle,
                    Quantity = 100,
                    IsCombatPowerReward = false
                };
                season.PremiumRewards[i] = new BattlePassReward
                {
                    RewardId = $"premium_{i + 1}",
                    RewardType = i == 1 ? BattlePassRewardType.HeroSkin : BattlePassRewardType.Emote,
                    // Tier 2 (index 1) premium reward is intentionally flagged as combat power
                    // to test the P2W gate in ClaimReward
                    IsCombatPowerReward = (i == 1)
                };
            }

            return season;
        }
    }
}
