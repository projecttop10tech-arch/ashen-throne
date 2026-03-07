using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Alliance;
using AshenThrone.Combat;
using AshenThrone.Data;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Alliance
{
    [TestFixture]
    public class AsyncPvpManagerTests
    {
        private GameObject _go;
        private AsyncPvpManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AsyncPvpManagerTest");
            _manager = _go.AddComponent<AsyncPvpManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // --- RecordLoadout ---

        [Test]
        public void RecordLoadout_ReturnsFalse_WhenOwnerIdEmpty()
        {
            Assert.IsFalse(_manager.RecordLoadout("", MakeHeroList(1), "1.0.0"));
        }

        [Test]
        public void RecordLoadout_ReturnsFalse_WhenHeroListNull()
        {
            Assert.IsFalse(_manager.RecordLoadout("player1", null, "1.0.0"));
        }

        [Test]
        public void RecordLoadout_ReturnsFalse_WhenHeroListEmpty()
        {
            Assert.IsFalse(_manager.RecordLoadout("player1", new List<PvpHeroRecord>(), "1.0.0"));
        }

        [Test]
        public void RecordLoadout_ReturnsFalse_WhenTooManyHeroes()
        {
            Assert.IsFalse(_manager.RecordLoadout("player1", MakeHeroList(4), "1.0.0"));
        }

        [Test]
        public void RecordLoadout_ReturnsTrue_WithValidInput()
        {
            Assert.IsTrue(_manager.RecordLoadout("player1", MakeHeroList(3), "1.0.0"));
        }

        [Test]
        public void RecordLoadout_SetsLocalLoadout()
        {
            _manager.RecordLoadout("player1", MakeHeroList(2), "1.0.0");
            Assert.IsNotNull(_manager.GetLocalLoadout());
            Assert.AreEqual("player1", _manager.GetLocalLoadout().OwnerPlayFabId);
        }

        [Test]
        public void RecordLoadout_ComputesNonEmptyIntegrityHash()
        {
            _manager.RecordLoadout("player1", MakeHeroList(1), "1.0.0");
            string hash = _manager.GetLocalLoadout().IntegrityHash;
            Assert.IsFalse(string.IsNullOrEmpty(hash));
        }

        // --- ComputeLoadoutHash (pure static) ---

        [Test]
        public void ComputeLoadoutHash_ReturnsSameHash_ForIdenticalLoadouts()
        {
            var loadout1 = MakeLoadout("player1", 2);
            var loadout2 = MakeLoadout("player1", 2);
            Assert.AreEqual(
                AsyncPvpManager.ComputeLoadoutHash(loadout1),
                AsyncPvpManager.ComputeLoadoutHash(loadout2));
        }

        [Test]
        public void ComputeLoadoutHash_ReturnsDifferentHash_WhenHeroLevelChanges()
        {
            var loadout1 = MakeLoadout("player1", 2);
            var loadout2 = MakeLoadout("player1", 2);
            loadout2.Heroes[0].Level = 80; // Change level
            Assert.AreNotEqual(
                AsyncPvpManager.ComputeLoadoutHash(loadout1),
                AsyncPvpManager.ComputeLoadoutHash(loadout2));
        }

        [Test]
        public void ComputeLoadoutHash_ReturnsEmpty_ForNullLoadout()
        {
            Assert.AreEqual(string.Empty, AsyncPvpManager.ComputeLoadoutHash(null));
        }

        // --- ComputeReplayHash (pure static) ---

        [Test]
        public void ComputeReplayHash_ReturnsSameHash_ForSameInputs()
        {
            string h1 = AsyncPvpManager.ComputeReplayHash("r1", "aHash", "dHash", "PlayerVictory", 10);
            string h2 = AsyncPvpManager.ComputeReplayHash("r1", "aHash", "dHash", "PlayerVictory", 10);
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void ComputeReplayHash_ReturnsDifferentHash_WhenOutcomeChanges()
        {
            string h1 = AsyncPvpManager.ComputeReplayHash("r1", "aHash", "dHash", "PlayerVictory", 10);
            string h2 = AsyncPvpManager.ComputeReplayHash("r1", "aHash", "dHash", "PlayerDefeat", 10);
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void ComputeReplayHash_Returns64CharHexString()
        {
            string hash = AsyncPvpManager.ComputeReplayHash("r", "a", "d", "Draw", 5);
            Assert.AreEqual(64, hash.Length);
        }

        // --- ReceiveReplay ---

        [Test]
        public void ReceiveReplay_ReturnsFalse_ForNullReplay()
        {
            Assert.IsFalse(_manager.ReceiveReplay(null));
        }

        [Test]
        public void ReceiveReplay_ReturnsFalse_WhenHashInvalid()
        {
            var replay = MakeValidReplay();
            replay.ValidationHash = "badhash";
            Assert.IsFalse(_manager.ReceiveReplay(replay));
        }

        [Test]
        public void ReceiveReplay_ReturnsTrue_WhenHashValid()
        {
            var replay = MakeValidReplay();
            Assert.IsTrue(_manager.ReceiveReplay(replay));
        }

        [Test]
        public void ReceiveReplay_AddsToHistory_WhenValid()
        {
            var replay = MakeValidReplay();
            _manager.ReceiveReplay(replay);
            Assert.AreEqual(1, _manager.GetReplayHistory().Count);
        }

        [Test]
        public void ReceiveReplay_EvictsOldest_WhenHistoryFull()
        {
            // Fill history with valid replays
            for (int i = 0; i < AsyncPvpManager.MaxReplayHistory; i++)
            {
                var r = MakeValidReplay(replayId: $"replay_{i}");
                _manager.ReceiveReplay(r);
            }
            var newest = MakeValidReplay(replayId: "replay_newest");
            _manager.ReceiveReplay(newest);

            Assert.AreEqual(AsyncPvpManager.MaxReplayHistory, _manager.GetReplayHistory().Count);
            // Oldest should have been evicted
            Assert.IsNull(_manager.GetReplay("replay_0"));
            Assert.IsNotNull(_manager.GetReplay("replay_newest"));
        }

        [Test]
        public void ReceiveReplay_FiresOnReplayReceivedEvent()
        {
            bool fired = false;
            _manager.OnReplayReceived += _ => fired = true;
            _manager.ReceiveReplay(MakeValidReplay());
            Assert.IsTrue(fired);
        }

        // --- RequestAttack ---

        [Test]
        public void RequestAttack_ReturnsNull_WhenNoLoadoutRecorded()
        {
            string requestId = _manager.RequestAttack("player1", "player2");
            Assert.IsNull(requestId);
        }

        [Test]
        public void RequestAttack_ReturnsNonNullRequestId_WhenLoadoutRecorded()
        {
            _manager.RecordLoadout("player1", MakeHeroList(1), "1.0.0");
            string requestId = _manager.RequestAttack("player1", "player2");
            Assert.IsNotNull(requestId);
        }

        [Test]
        public void RequestAttack_ReturnsNull_WhenAttackerIdEmpty()
        {
            _manager.RecordLoadout("player1", MakeHeroList(1), "1.0.0");
            Assert.IsNull(_manager.RequestAttack("", "player2"));
        }

        // --- GetReplay ---

        [Test]
        public void GetReplay_ReturnsNull_ForNullId()
        {
            Assert.IsNull(_manager.GetReplay(null));
        }

        [Test]
        public void GetReplay_ReturnsCorrectReplay_ByReplayId()
        {
            var replay = MakeValidReplay(replayId: "find_me");
            _manager.ReceiveReplay(replay);
            var found = _manager.GetReplay("find_me");
            Assert.IsNotNull(found);
            Assert.AreEqual("find_me", found.ReplayId);
        }

        // --- CombatReplayData hash ---

        [Test]
        public void CombatReplayData_IsHashValid_ReturnsFalse_WhenLoadoutsNull()
        {
            var replay = new CombatReplayData { ReplayId = "r", ValidationHash = "x" };
            Assert.IsFalse(replay.IsHashValid());
        }

        // --- Helpers ---

        private static List<PvpHeroRecord> MakeHeroList(int count)
        {
            var list = new List<PvpHeroRecord>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new PvpHeroRecord
                {
                    HeroDataId = $"hero_{i}",
                    Level = 10,
                    StarTier = 0,
                    DeckCardIds = new List<string> { "card_1" },
                    PreferredRow = CombatRow.Front
                });
            }
            return list;
        }

        private static PvpLoadoutRecord MakeLoadout(string ownerId, int heroCount)
        {
            var loadout = new PvpLoadoutRecord
            {
                OwnerPlayFabId = ownerId,
                Heroes = MakeHeroList(heroCount),
                RecordedAtUtc = System.DateTime.UtcNow.ToString("O"),
                GameVersion = "1.0.0"
            };
            loadout.IntegrityHash = AsyncPvpManager.ComputeLoadoutHash(loadout);
            return loadout;
        }

        private static CombatReplayData MakeValidReplay(string replayId = "test_replay_1")
        {
            var attackerLoadout = MakeLoadout("attacker", 1);
            var defenderLoadout = MakeLoadout("defender", 1);
            string outcome = "PlayerVictory";
            int turns = 5;
            string hash = AsyncPvpManager.ComputeReplayHash(
                replayId, attackerLoadout.IntegrityHash,
                defenderLoadout.IntegrityHash, outcome, turns);

            return new CombatReplayData
            {
                ReplayId = replayId,
                AttackerLoadout = attackerLoadout,
                DefenderLoadout = defenderLoadout,
                Outcome = outcome,
                TotalTurns = turns,
                SimulatedAtUtc = System.DateTime.UtcNow.ToString("O"),
                ValidationHash = hash
            };
        }
    }
}
