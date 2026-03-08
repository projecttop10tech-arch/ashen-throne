using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Heroes;
using System.Diagnostics;

namespace AshenThrone.Tests.Integration
{
    [TestFixture]
    public class PerformanceBenchmarkTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            CombatHero.ResetInstanceIdForTesting();
            _go = new GameObject("PerfTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void CombatHero_Create100_Under50ms()
        {
            var heroData = ScriptableObject.CreateInstance<HeroData>();
            var progConfig = ScriptableObject.CreateInstance<ProgressionConfig>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                var hero = new CombatHero(heroData, 1, true, progConfig);
            }
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 50, $"100 hero creation took {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void EventBus_Publish1000Events_Under10ms()
        {
            int count = 0;
            var sub = EventBus.Subscribe<AppStateChangedEvent>(e => count++);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                EventBus.Publish(new AppStateChangedEvent(AppState.Boot, AppState.Combat));
            }
            sw.Stop();
            sub.Dispose();

            Assert.AreEqual(1000, count);
            Assert.Less(sw.ElapsedMilliseconds, 10, $"1000 events took {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void ServiceLocator_Lookup1000Times_Under5ms()
        {
            var pfGo = new GameObject("PFService");
            pfGo.AddComponent<AshenThrone.Network.PlayFabService>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                ServiceLocator.TryGet<AshenThrone.Network.PlayFabService>(out _);
            }
            sw.Stop();
            Object.DestroyImmediate(pfGo);

            Assert.Less(sw.ElapsedMilliseconds, 5, $"1000 lookups took {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void ObjectPool_GetReturn100_Under50ms()
        {
            var prefab = new GameObject("PoolPrefab");
            var pool = new ObjectPool<Transform>(prefab.transform, _go.transform, 20, 200);

            var sw = Stopwatch.StartNew();
            var items = new Transform[100];
            for (int i = 0; i < 100; i++)
                items[i] = pool.Get(Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 100; i++)
                pool.Return(items[i]);
            sw.Stop();

            pool.ReturnAll();
            Object.DestroyImmediate(prefab);

            Assert.Less(sw.ElapsedMilliseconds, 50, $"100 get/return took {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void CombatHero_TakeDamage1000Times_Under50ms()
        {
            var heroData = ScriptableObject.CreateInstance<HeroData>();
            var progConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            var hero = new CombatHero(heroData, 99, true, progConfig);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
                hero.TakeDamage(1, DamageType.Physical);
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 50, $"1000 damage ops took {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void CombatGrid_PlaceAndRemove100Units_Under50ms()
        {
            var grid = _go.AddComponent<CombatGrid>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                int col = i % CombatGrid.Columns;
                int row = (i / CombatGrid.Columns) % CombatGrid.Rows;
                grid.PlaceUnit(i + 1, new GridPosition(col, row));
                grid.RemoveUnit(i + 1);
            }
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 50, $"100 place/remove took {sw.ElapsedMilliseconds}ms");
        }
    }
}
