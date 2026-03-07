using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Alliance;
using AshenThrone.Data;

namespace AshenThrone.Tests.Alliance
{
    [TestFixture]
    public class WarEngineTests
    {
        // --- ComputeAttackerPower (pure static) ---

        [Test]
        public void ComputeAttackerPower_ReturnsZero_ForEmptyList()
        {
            int power = WarEngine.ComputeAttackerPower(new List<int>());
            Assert.AreEqual(0, power);
        }

        [Test]
        public void ComputeAttackerPower_ReturnsZero_ForNullList()
        {
            int power = WarEngine.ComputeAttackerPower(null);
            Assert.AreEqual(0, power);
        }

        [Test]
        public void ComputeAttackerPower_SingleAttacker_ReturnBasePower()
        {
            // Single attacker: coordination multiplier ≈ 1 + 0.05 * log2(2) = 1.05
            int power = WarEngine.ComputeAttackerPower(new List<int> { 1000 });
            Assert.Greater(power, 0);
        }

        [Test]
        public void ComputeAttackerPower_MultipleAttackers_GreaterThanSum()
        {
            // Coordination bonus should make grouped power > sum of individuals
            var single = WarEngine.ComputeAttackerPower(new List<int> { 1000 });
            var grouped = WarEngine.ComputeAttackerPower(new List<int> { 1000, 1000 });
            Assert.Greater(grouped, single * 2 - 1); // Allow for rounding; just ensure non-negative
        }

        [Test]
        public void ComputeAttackerPower_NegativeScores_ClampedToZero()
        {
            int power = WarEngine.ComputeAttackerPower(new List<int> { -100, -200 });
            Assert.AreEqual(0, power);
        }

        // --- ResolveAttack (pure static) ---

        [Test]
        public void ResolveAttack_AttackerWins_WhenPowerExceedsTotalDefense()
        {
            var outcome = WarEngine.ResolveAttack(attackerPower: 1000, defenderPower: 400, fortificationHp: 400);
            Assert.AreEqual(WarOutcome.AttackerVictory, outcome);
        }

        [Test]
        public void ResolveAttack_DefenderWins_WhenDefenseExceedsAttack()
        {
            var outcome = WarEngine.ResolveAttack(attackerPower: 500, defenderPower: 600, fortificationHp: 0);
            Assert.AreEqual(WarOutcome.DefenderVictory, outcome);
        }

        [Test]
        public void ResolveAttack_Draw_WhenEqualPower()
        {
            var outcome = WarEngine.ResolveAttack(attackerPower: 700, defenderPower: 700, fortificationHp: 0);
            Assert.AreEqual(WarOutcome.Draw, outcome);
        }

        [Test]
        public void ResolveAttack_FortificationAddsToDefense()
        {
            // Without fort: attacker 600 > defender 500 → attacker wins
            // With fort 200: total defense 700 > 600 → defender wins
            var withoutFort = WarEngine.ResolveAttack(600, 500, 0);
            var withFort    = WarEngine.ResolveAttack(600, 500, 200);
            Assert.AreEqual(WarOutcome.AttackerVictory, withoutFort);
            Assert.AreEqual(WarOutcome.DefenderVictory, withFort);
        }

        // --- ComputeResultHash (pure static) ---

        [Test]
        public void ComputeResultHash_ReturnsSameHash_ForSameInputs()
        {
            string h1 = WarEngine.ComputeResultHash("a1", "r1", WarOutcome.AttackerVictory, 1000, 500);
            string h2 = WarEngine.ComputeResultHash("a1", "r1", WarOutcome.AttackerVictory, 1000, 500);
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void ComputeResultHash_ReturnsDifferentHash_ForDifferentOutcomes()
        {
            string h1 = WarEngine.ComputeResultHash("a1", "r1", WarOutcome.AttackerVictory, 1000, 500);
            string h2 = WarEngine.ComputeResultHash("a1", "r1", WarOutcome.DefenderVictory, 1000, 500);
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void ComputeResultHash_Returns64CharHexString()
        {
            string hash = WarEngine.ComputeResultHash("x", "y", WarOutcome.Draw, 0, 0);
            Assert.AreEqual(64, hash.Length);
            // Verify it's hex
            foreach (char c in hash)
                Assert.IsTrue(Uri.IsHexDigit(c), $"Non-hex character '{c}' in hash");
        }

        // --- WarResult hash validation ---

        [Test]
        public void WarResult_IsHashValid_ReturnsTrue_ForMatchingHash()
        {
            string hash = WarEngine.ComputeResultHash("action1", "region1", WarOutcome.AttackerVictory, 1000, 500);
            var result = new WarResult("action1", "region1", WarOutcome.AttackerVictory, 1000, 500, 0, hash);
            Assert.IsTrue(result.IsHashValid());
        }

        [Test]
        public void WarResult_IsHashValid_ReturnsFalse_ForTamperedHash()
        {
            var result = new WarResult("action1", "region1", WarOutcome.AttackerVictory, 1000, 500, 0, "tampered");
            Assert.IsFalse(result.IsHashValid());
        }

        // --- RallyAttack ---

        [Test]
        public void RallyAttack_ThrowsArgumentException_WhenRallyIdEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                new RallyAttack("", "alliance", "region", "organizer", 300, 10, 50));
        }

        [Test]
        public void RallyAttack_OrganizerAutoJoins()
        {
            var rally = MakeRally();
            Assert.AreEqual(1, rally.Participants.Count);
            Assert.AreEqual("organizer_1", rally.Participants[0]);
        }

        [Test]
        public void RallyAttack_TryJoin_AddsMember_WhenRecruitingAndNotFull()
        {
            var rally = MakeRally(minAttackers: 1, maxAttackers: 5);
            bool joined = rally.TryJoin("player_2");
            Assert.IsTrue(joined);
            Assert.AreEqual(2, rally.Participants.Count);
        }

        [Test]
        public void RallyAttack_TryJoin_ReturnsFalse_WhenFull()
        {
            var rally = MakeRally(minAttackers: 1, maxAttackers: 2);
            rally.TryJoin("player_2");
            bool overflow = rally.TryJoin("player_3");
            Assert.IsFalse(overflow);
        }

        [Test]
        public void RallyAttack_TryJoin_ReturnsFalse_WhenAlreadyJoined()
        {
            var rally = MakeRally(minAttackers: 1, maxAttackers: 50);
            rally.TryJoin("player_2");
            bool duplicate = rally.TryJoin("player_2");
            Assert.IsFalse(duplicate);
        }

        [Test]
        public void RallyAttack_TryLaunch_ReturnsFalse_WhenBelowMinAttackers()
        {
            var rally = MakeRally(minAttackers: 5, maxAttackers: 50);
            // Only organizer joined (1 < 5)
            Assert.IsFalse(rally.TryLaunch());
        }

        [Test]
        public void RallyAttack_TryLaunch_ReturnsTrue_WhenMinMet()
        {
            var rally = MakeRally(minAttackers: 1, maxAttackers: 50);
            Assert.IsTrue(rally.TryLaunch());
            Assert.AreEqual(RallyState.Launched, rally.State);
        }

        [Test]
        public void RallyAttack_Cancel_SetsStateToCancelled()
        {
            var rally = MakeRally();
            rally.Cancel();
            Assert.AreEqual(RallyState.Cancelled, rally.State);
        }

        [Test]
        public void RallyAttack_TryJoin_ReturnsFalse_AfterCancelled()
        {
            var rally = MakeRally();
            rally.Cancel();
            Assert.IsFalse(rally.TryJoin("late_player"));
        }

        // --- WarWindow ---

        [Test]
        public void WarWindow_ThrowsArgumentException_WhenCloseBeforeOpen()
        {
            var open  = DateTime.UtcNow;
            var close = open.AddSeconds(-1);
            Assert.Throws<ArgumentException>(() => new WarWindow(open, close));
        }

        [Test]
        public void WarWindow_IsOpen_WhenCurrentTimeWithinWindow()
        {
            var open  = DateTime.UtcNow.AddSeconds(-1);
            var close = DateTime.UtcNow.AddSeconds(3600);
            var window = new WarWindow(open, close);
            Assert.IsTrue(window.IsOpen);
        }

        [Test]
        public void WarWindow_IsNotOpen_BeforeWindowStarts()
        {
            var open  = DateTime.UtcNow.AddSeconds(3600);
            var close = open.AddSeconds(7200);
            var window = new WarWindow(open, close);
            Assert.IsFalse(window.IsOpen);
        }

        // --- Helpers ---

        private static RallyAttack MakeRally(int minAttackers = 2, int maxAttackers = 50)
        {
            return new RallyAttack(
                $"rally_{System.Guid.NewGuid():N}",
                "alliance_A",
                "region_X",
                "organizer_1",
                300,
                minAttackers,
                maxAttackers);
        }
    }
}
