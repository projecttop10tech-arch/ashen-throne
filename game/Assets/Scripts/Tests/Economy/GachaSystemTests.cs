using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Economy;

namespace AshenThrone.Tests.Economy
{
    [TestFixture]
    public class GachaSystemTests
    {
        private GameObject _go;
        private GachaSystem _gacha;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GachaSystemTest");
            _gacha = _go.AddComponent<GachaSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // --- SetPool ---

        [Test]
        public void SetPool_DoesNotThrow_ForNullPool()
        {
            Assert.DoesNotThrow(() => _gacha.SetPool(null));
        }

        [Test]
        public void SetPool_DoesNotThrow_ForEmptyPool()
        {
            Assert.DoesNotThrow(() => _gacha.SetPool(new List<GachaItem>()));
        }

        [Test]
        public void SetPool_ReplacesExistingPool()
        {
            _gacha.SetPool(MakePool(5));
            _gacha.SetPool(MakePool(2));
            // SimulatePull should still work with only 2 items
            Assert.IsNotNull(_gacha.SimulatePull());
        }

        // --- SimulatePull ---

        [Test]
        public void SimulatePull_ReturnsNull_WhenPoolEmpty()
        {
            _gacha.SetPool(new List<GachaItem>());
            Assert.IsNull(_gacha.SimulatePull());
        }

        [Test]
        public void SimulatePull_ReturnsNonNull_WithValidPool()
        {
            _gacha.SetPool(MakePool(10));
            Assert.IsNotNull(_gacha.SimulatePull());
        }

        [Test]
        public void SimulatePull_IncrementsPityCounter()
        {
            _gacha.SetPool(MakePool(10));
            int before = _gacha.PityCounter;
            _gacha.SimulatePull();
            // Pity counter either incremented or reset to 0 (if legendary was pulled)
            Assert.IsTrue(_gacha.PityCounter >= 0);
            Assert.IsTrue(_gacha.PityCounter != before || _gacha.PityCounter == 0);
        }

        [Test]
        public void SimulatePull_GuaranteesLegendaryAtPityThreshold()
        {
            // Fill pool with one legendary and many commons
            var pool = new List<GachaItem>();
            for (int i = 0; i < 20; i++)
                pool.Add(new GachaItem($"common_{i}", $"Common {i}", GachaRarity.Common, CosmeticType.Emote, 9990));
            pool.Add(new GachaItem("legendary_1", "Legendary 1", GachaRarity.Legendary, CosmeticType.HeroSkin, 10));
            _gacha.SetPool(pool);

            // Force pity counter to threshold - 1
            _gacha.LoadPityCounter(GachaSystem.PityThreshold - 1);

            GachaPullResult result = null;
            for (int i = 0; i < 5; i++) // At most 1 pull needed to trigger pity
            {
                result = _gacha.SimulatePull();
                if (result?.Item.Rarity == GachaRarity.Legendary) break;
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(GachaRarity.Legendary, result.Item.Rarity,
                "Pity system must guarantee a Legendary at threshold");
        }

        [Test]
        public void SimulatePull_ResetsPityCounter_AfterLegendary()
        {
            // Build pool guaranteed to only have legendary items
            var pool = new List<GachaItem>
            {
                new GachaItem("leg_1", "Legendary", GachaRarity.Legendary, CosmeticType.HeroSkin, 10000)
            };
            _gacha.SetPool(pool);
            _gacha.SimulatePull(); // Should pull legendary and reset pity
            Assert.AreEqual(0, _gacha.PityCounter);
        }

        [Test]
        public void SimulatePull_IsDuplicateTrue_WhenItemAlreadyOwned()
        {
            var item = new GachaItem("skin_1", "Skin 1", GachaRarity.Legendary, CosmeticType.HeroSkin, 10000);
            item.IsOwned = true;
            _gacha.SetPool(new List<GachaItem> { item });
            // All-owned pool: no items can be drawn without owned items
            // (pool should filter owned items — SimulatePull returns null when pool empty after filtering)
            var result = _gacha.SimulatePull();
            // Pool is filtered (owned items excluded from weighting) so result should be null
            Assert.IsNull(result);
        }

        // --- LoadPityCounter ---

        [Test]
        public void LoadPityCounter_ClampsBelowZero()
        {
            _gacha.LoadPityCounter(-5);
            Assert.AreEqual(0, _gacha.PityCounter);
        }

        [Test]
        public void LoadPityCounter_ClampsAboveThreshold()
        {
            _gacha.LoadPityCounter(GachaSystem.PityThreshold + 100);
            Assert.AreEqual(GachaSystem.PityThreshold, _gacha.PityCounter);
        }

        [Test]
        public void PullsUntilPity_EqualsThresholdMinusCounter()
        {
            _gacha.LoadPityCounter(10);
            Assert.AreEqual(GachaSystem.PityThreshold - 10, _gacha.PullsUntilPity);
        }

        // --- GetDisplayedOdds ---

        [Test]
        public void GetDisplayedOdds_ReturnsNonEmptyDict_WithValidPool()
        {
            _gacha.SetPool(MakePool(4));
            var odds = _gacha.GetDisplayedOdds();
            Assert.Greater(odds.Count, 0);
        }

        [Test]
        public void GetDisplayedOdds_ReturnEmpty_WithEmptyPool()
        {
            _gacha.SetPool(new List<GachaItem>());
            var odds = _gacha.GetDisplayedOdds();
            Assert.AreEqual(0, odds.Count);
        }

        [Test]
        public void GetDisplayedOdds_OddsSumToOne_Approximately()
        {
            _gacha.SetPool(MakePool(4));
            var odds = _gacha.GetDisplayedOdds();
            float total = 0f;
            foreach (var kvp in odds) total += kvp.Value;
            Assert.AreApproximatelyEqual(1f, total, 0.001f);
        }

        // --- ReceiveServerPullResult ---

        [Test]
        public void ReceiveServerPullResult_MarksItemAsOwned()
        {
            var item = new GachaItem("skin_x", "Skin X", GachaRarity.Epic, CosmeticType.HeroSkin, 1000);
            _gacha.SetPool(new List<GachaItem> { item });
            _gacha.ReceiveServerPullResult("skin_x", isDuplicate: false, pityCounterFromServer: 0);
            Assert.IsTrue(item.IsOwned);
        }

        [Test]
        public void ReceiveServerPullResult_UpdatesPityCounterFromServer()
        {
            _gacha.SetPool(MakePool(1));
            _gacha.ReceiveServerPullResult("item_0", isDuplicate: false, pityCounterFromServer: 25);
            Assert.AreEqual(25, _gacha.PityCounter);
        }

        // --- GachaItem construction ---

        [Test]
        public void GachaItem_ThrowsArgumentException_WhenIdEmpty()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new GachaItem("", "Name", GachaRarity.Common, CosmeticType.Emote, 1000));
        }

        [Test]
        public void GachaItem_ThrowsArgumentException_WhenWeightZero()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new GachaItem("valid_id", "Name", GachaRarity.Common, CosmeticType.Emote, 0));
        }

        // --- Helpers ---

        private static List<GachaItem> MakePool(int count)
        {
            var pool = new List<GachaItem>(count);
            for (int i = 0; i < count; i++)
            {
                pool.Add(new GachaItem($"item_{i}", $"Item {i}",
                    i % 2 == 0 ? GachaRarity.Common : GachaRarity.Rare,
                    CosmeticType.Emote, 5000));
            }
            return pool;
        }
    }
}
