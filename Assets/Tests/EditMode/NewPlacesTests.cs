using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WildSnake.Tests
{
    /// <summary>
    /// Новые места карты: ежевика, тёплые ключи, мышиные городки, осиные гнёзда — разложены и не мешают остальному.
    /// И тёплый ключ как место для температуры.
    /// </summary>
    public class NewPlacesTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GridManager CreateGrid()
        {
            var go = new GameObject("TestGrid");
            _created.Add(go);
            var grid = go.AddComponent<GridManager>();
            grid.GenerateLayout();
            return grid;
        }

        [Test]
        public void AllNewPlaces_ArePlaced()
        {
            GridManager grid = CreateGrid();
            Assert.Greater(grid.HotSpringCount, 0, "Тёплых ключей нет");
            Assert.Greater(grid.MouseColonyCount, 0, "Мышиных городков нет");
            Assert.Greater(grid.ColonyHoleCount, grid.MouseColonyCount, "У городков должны быть норки");
            Assert.Greater(grid.WaspNestCount, 0, "Осиных гнёзд нет");
            int bramble = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsBramble(new Vector2Int(x, y))) bramble++;
            Assert.Greater(bramble, 30, "Ежевики слишком мало");
        }

        [Test]
        public void Bramble_IsDryCover_WithoutObstacles()
        {
            GridManager grid = CreateGrid();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!grid.IsBramble(cell)) continue;
                    Assert.IsTrue(grid.IsCover(cell), "Ежевика — укрытие: " + cell);
                    Assert.IsFalse(grid.IsWater(cell), "Ежевика в воде: " + cell);
                    Assert.AreEqual(ObstacleType.None, grid.GetObstacle(cell), "Ежевика на препятствии: " + cell);
                    Assert.IsFalse(grid.IsReed(cell));
                }
            }
        }

        [Test]
        public void HotSprings_AreOpenDryGround()
        {
            GridManager grid = CreateGrid();
            for (int i = 0; i < grid.HotSpringCount; i++)
            {
                Vector3 p = grid.GetHotSpring(i);
                Assert.IsTrue(grid.IsHotSpringAt(p), "Центр ключа должен быть на клетке ключа");
                Vector2Int cell = grid.WorldToCell(p);
                Assert.IsFalse(grid.IsWater(cell));
                Assert.AreEqual(ObstacleType.None, grid.GetObstacle(cell));
                Assert.IsFalse(grid.IsCover(cell), "Вокруг ключа трава выгорает");
            }
        }

        [Test]
        public void ColonyHoles_AreFree_AndNotSpawnCells()
        {
            GridManager grid = CreateGrid();
            for (int i = 0; i < grid.ColonyHoleCount; i++)
            {
                Vector2Int cell = grid.WorldToCell(grid.GetColonyHole(i));
                Assert.IsTrue(grid.IsColonyHole(cell));
                Assert.AreEqual(ObstacleType.None, grid.GetObstacle(cell));
                Assert.IsFalse(grid.IsWater(cell));
            }
            for (int k = 0; k < 300; k++)
            {
                Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int cell));
                Assert.IsFalse(grid.IsColonyHole(cell), "Добыча не появляется прямо в норке");
                Assert.IsFalse(grid.IsHotSpring(cell), "Добыча не появляется в ключе");
            }
        }

        [Test]
        public void ColonyCell_IsNearColony()
        {
            GridManager grid = CreateGrid();
            Assert.IsTrue(grid.TryGetColonyCell(out Vector2Int cell));
            int hole = grid.NearestColonyHole(grid.CellToWorld(cell), out float dist);
            Assert.GreaterOrEqual(hole, 0);
            Assert.Less(dist, 7f * grid.CellSize, "Мышь из городка появляется рядом с норками");
        }

        [Test]
        public void WaspNests_StandOnStumps_AwayFromStart()
        {
            GridManager grid = CreateGrid();
            for (int i = 0; i < grid.WaspNestCount; i++)
            {
                Vector2Int cell = grid.GetWaspNestCell(i);
                Assert.AreEqual(ObstacleType.Pillar, grid.GetObstacle(cell), "Гнездо висит на пне");
                Assert.IsFalse(grid.IsForest(cell));
                Vector3 d = grid.GetWaspNest(i) - grid.Center;
                d.y = 0f;
                Assert.Greater(d.magnitude, 15f * grid.CellSize, "Осы не у самого старта");
            }
        }

        [Test]
        public void Blueprint_NestHasEverythingForTheFinale_AndBurrowsHaveStones()
        {
            GridManager grid = CreateGrid();
            Assert.IsTrue(grid.HasNest);
            Vector3 nest = grid.NestPosition;
            Assert.Less(Nearest(nest, grid.HotSpringCount, grid.GetHotSpring), 16f, "У гнезда должен быть тёплый ключ — ночью охранять кладку");
            Assert.Less(Nearest(nest, grid.BurrowCount, grid.GetBurrow), 18f, "У гнезда должна быть нора — сохраниться перед кладкой");
            Assert.Less(Nearest(nest, grid.MouseColonyCount, grid.GetMouseColony), 16f, "У гнезда должна быть еда");
            Assert.AreEqual(WorldBlueprint.Burrows.Length, grid.BurrowCount, "Все норы плана вырыты");
            for (int b = 0; b < grid.BurrowCount; b++)
            {
                float best = float.MaxValue;
                for (int s = 0; s < grid.SunStoneCount; s++)
                    best = Mathf.Min(best, Vector3.Distance(grid.CellToWorld(grid.GetSunStone(s)), grid.GetBurrow(b)));
                Assert.Less(best, 9f, "У норы " + b + " нет утреннего солнечного камня");
            }
            Assert.AreEqual(WorldBlueprint.Bridges.Length, grid.BridgeCount, "Все мосты плана на месте");
        }

        private static float Nearest(Vector3 from, int count, System.Func<int, Vector3> get)
        {
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 d = get(i) - from;
                d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }

        [Test]
        public void HotSpring_WarmsAtNight_ButNotAboveNoonStone()
        {
            var spring = new BodyTemperature();
            var open = new BodyTemperature();
            spring.Reset(0.2f);
            open.Reset(0.2f);
            for (int i = 0; i < 30; i++)
            {
                spring.Tick(1f, 0f, TemperatureSpot.HotSpring);
                open.Tick(1f, 0f, TemperatureSpot.Open);
            }
            Assert.IsFalse(spring.IsCold, "Ночью у ключа не холодно");
            Assert.IsTrue(open.IsCold);
            Assert.Less(BodyTemperature.SpotTarget(TemperatureSpot.HotSpring, 0f), BodyTemperature.SpotTarget(TemperatureSpot.SunStone, 1f));
        }

        [Test]
        public void Config_NewMechanics_AreBalanced()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.Less(config.BrambleSpeed, 1f, "Ежевика замедляет");
                Assert.Greater(config.ThornDamage, 0f);
                Assert.Greater(config.WaspSpeed, config.SprintSpeed, "Рой быстрее бегущей змеи — убежать можно только в укрытие");
                Assert.Greater(config.WaspAngerRate.y, config.WaspAngerRate.x, "Бег злит ос сильнее шага");
                Assert.Less(config.HedgehogSwimSpeed, config.SneakSpeed, "Плывущего ежа можно догнать");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
