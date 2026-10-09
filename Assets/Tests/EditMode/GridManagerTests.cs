using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Snake3D.Tests
{
    /// <summary>
    /// Логика сетки: раскладка по сиду, стартовая зона, перевод клетка/мир, границы арены,
    /// выбор свободной клетки для предметов. Используются значения по умолчанию (44x44, сид 2024).
    /// </summary>
    public class GridManagerTests
    {
        private const int ITEM_MARGIN = 2;      // _itemBorderMargin по умолчанию
        private const int START_CLEAR_RADIUS = 6; // _startClearRadius по умолчанию

        private readonly List<GameObject> _created = new List<GameObject>();
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = Random.state;
            Random.InitState(12345);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            Random.state = _randomState;
        }

        private GridManager CreateGrid(Vector3 position = default)
        {
            var go = new GameObject("TestGrid");
            _created.Add(go);
            go.transform.position = position;
            var grid = go.AddComponent<GridManager>();
            grid.GenerateLayout(); // Awake в EditMode не вызывается
            return grid;
        }

        private static bool HasObstacleNear(GridManager grid, Vector2Int c)
        {
            for (int y = c.y - 1; y <= c.y + 1; y++)
                for (int x = c.x - 1; x <= c.x + 1; x++)
                {
                    var n = new Vector2Int(x, y);
                    if (grid.IsInside(n) && grid.GetObstacle(n) != ObstacleType.None) return true;
                }
            return false;
        }

        private static IEnumerable<Vector2Int> AllCells(GridManager grid)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    yield return new Vector2Int(x, y);
        }

        private static void OccupyAllExcept(GridManager grid, Vector2Int keep)
        {
            foreach (Vector2Int c in AllCells(grid))
                grid.SetOccupied(c, c != keep);
        }

        // ---------------- Раскладка ----------------

        [Test]
        public void Defaults_Are44x44()
        {
            GridManager grid = CreateGrid();
            Assert.AreEqual(44, grid.Width);
            Assert.AreEqual(44, grid.Height);
            Assert.AreEqual(44 * 44, grid.CellCount);
        }

        [Test]
        public void SameSeed_GivesSameLayout()
        {
            GridManager a = CreateGrid();
            GridManager b = CreateGrid(new Vector3(100f, 0f, 100f));

            int obstacles = 0;
            foreach (Vector2Int c in AllCells(a))
            {
                Assert.AreEqual(a.GetObstacle(c), b.GetObstacle(c), $"Раскладки различаются в клетке {c}");
                if (a.GetObstacle(c) != ObstacleType.None) obstacles++;
            }
            Assert.Greater(obstacles, 0, "Сгенерировано ноль препятствий");
            Assert.AreEqual(a.LayoutVersion, b.LayoutVersion);
        }

        [Test]
        public void GenerateLayout_Twice_GivesSameLayout()
        {
            GridManager grid = CreateGrid();
            var first = new List<ObstacleType>();
            foreach (Vector2Int c in AllCells(grid)) first.Add(grid.GetObstacle(c));

            grid.GenerateLayout();
            int i = 0;
            foreach (Vector2Int c in AllCells(grid))
                Assert.AreEqual(first[i++], grid.GetObstacle(c), $"Повторная генерация дала другую клетку {c}");
        }

        [Test]
        public void Layout_ContainsAllObstacleTypes()
        {
            GridManager grid = CreateGrid();
            bool low = false, pillar = false, block = false;
            foreach (Vector2Int c in AllCells(grid))
            {
                switch (grid.GetObstacle(c))
                {
                    case ObstacleType.LowWall: low = true; break;
                    case ObstacleType.Pillar: pillar = true; break;
                    case ObstacleType.Block: block = true; break;
                }
            }
            Assert.IsTrue(low, "Нет низких стенок");
            Assert.IsTrue(pillar, "Нет колонн");
            Assert.IsTrue(block, "Нет блоков");
        }

        [Test]
        public void StartArea_HasNoObstacles()
        {
            GridManager grid = CreateGrid();
            float cx = (grid.Width - 1) * 0.5f;
            float cy = (grid.Height - 1) * 0.5f;
            float r2 = START_CLEAR_RADIUS * START_CLEAR_RADIUS;

            foreach (Vector2Int c in AllCells(grid))
            {
                float dx = c.x - cx, dy = c.y - cy;
                if (dx * dx + dy * dy < r2)
                    Assert.AreEqual(ObstacleType.None, grid.GetObstacle(c), $"Препятствие в стартовой зоне: {c}");
            }
        }

        [Test]
        public void Obstacles_KeepTwoCellPassageAlongEdges()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid))
            {
                bool nearEdge = c.x < 2 || c.y < 2 || c.x >= grid.Width - 2 || c.y >= grid.Height - 2;
                if (nearEdge)
                    Assert.AreEqual(ObstacleType.None, grid.GetObstacle(c), $"Препятствие у края поля: {c}");
            }
        }

        [Test]
        public void ObstacleHeight_ZeroOnlyForEmptyCells()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid))
            {
                float h = grid.GetObstacleHeight(c);
                if (grid.GetObstacle(c) == ObstacleType.None) Assert.AreEqual(0f, h, $"Клетка {c}");
                else Assert.Greater(h, 0f, $"Клетка {c}");
            }
        }

        // ---------------- Клетка <-> мир ----------------

        [Test]
        public void WorldToCell_InvertsCellToWorld()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid))
                Assert.AreEqual(c, grid.WorldToCell(grid.CellToWorld(c)), $"Клетка {c}");
        }

        [Test]
        public void WorldToCell_InvertsCellToWorld_WithOffsetGrid()
        {
            GridManager grid = CreateGrid(new Vector3(13.3f, 2f, -7.6f));
            foreach (Vector2Int c in AllCells(grid))
                Assert.AreEqual(c, grid.WorldToCell(grid.CellToWorld(c)), $"Клетка {c}");
        }

        [Test]
        public void CellToWorld_IsCenteredOnTransform()
        {
            GridManager grid = CreateGrid(new Vector3(5f, 1f, 5f));
            Vector3 a = grid.CellToWorld(new Vector2Int(0, 0));
            Vector3 b = grid.CellToWorld(new Vector2Int(grid.Width - 1, grid.Height - 1));
            Vector3 mid = (a + b) * 0.5f;
            Assert.AreEqual(5f, mid.x, 1e-4f);
            Assert.AreEqual(1f, mid.y, 1e-4f);
            Assert.AreEqual(5f, mid.z, 1e-4f);
        }

        [Test]
        public void WorldToCell_PointsInsideCell_MapToThatCell()
        {
            GridManager grid = CreateGrid();
            var c = new Vector2Int(10, 30);
            Vector3 center = grid.CellToWorld(c);
            float d = grid.CellSize * 0.45f;
            Assert.AreEqual(c, grid.WorldToCell(center + new Vector3(d, 0f, d)));
            Assert.AreEqual(c, grid.WorldToCell(center + new Vector3(-d, 0f, -d)));
            Assert.AreEqual(c, grid.WorldToCell(center + new Vector3(d, 5f, -d)), "Высота не должна влиять");
        }

        // ---------------- Границы ----------------

        [Test]
        public void IsInside_Boundaries()
        {
            GridManager grid = CreateGrid();
            Assert.IsTrue(grid.IsInside(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsInside(new Vector2Int(grid.Width - 1, grid.Height - 1)));
            Assert.IsFalse(grid.IsInside(new Vector2Int(-1, 0)));
            Assert.IsFalse(grid.IsInside(new Vector2Int(0, -1)));
            Assert.IsFalse(grid.IsInside(new Vector2Int(grid.Width, 0)));
            Assert.IsFalse(grid.IsInside(new Vector2Int(0, grid.Height)));
        }

        [Test]
        public void IsCellInArena_NoInset_MatchesIsInside()
        {
            GridManager grid = CreateGrid();
            grid.ArenaInset = 0f;
            for (int y = -2; y < grid.Height + 2; y++)
                for (int x = -2; x < grid.Width + 2; x++)
                {
                    var c = new Vector2Int(x, y);
                    Assert.AreEqual(grid.IsInside(c), grid.IsCellInArena(c, 0), $"Клетка {c}");
                }
        }

        [Test]
        public void IsCellInArena_Inset5_CutsEdges()
        {
            GridManager grid = CreateGrid();
            grid.ArenaInset = 5f;
            int w = grid.Width, h = grid.Height;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool expected = x >= 5 && x < w - 5 && y >= 5 && y < h - 5;
                    Assert.AreEqual(expected, grid.IsCellInArena(new Vector2Int(x, y), 0), $"Клетка ({x},{y})");
                }

            // С отступом margin граница сдвигается ещё на margin клеток.
            Assert.IsFalse(grid.IsCellInArena(new Vector2Int(6, 20), 2));
            Assert.IsTrue(grid.IsCellInArena(new Vector2Int(7, 20), 2));
            Assert.IsTrue(grid.IsCellInArena(new Vector2Int(w - 8, 20), 2));
            Assert.IsFalse(grid.IsCellInArena(new Vector2Int(w - 7, 20), 2));
        }

        [Test]
        public void IsCellInArena_FractionalInset_RoundsUp()
        {
            GridManager grid = CreateGrid();
            grid.ArenaInset = 4.2f;
            Assert.IsFalse(grid.IsCellInArena(new Vector2Int(4, 20), 0), "Частично срезанная клетка не должна считаться в арене");
            Assert.IsTrue(grid.IsCellInArena(new Vector2Int(5, 20), 0));
            Assert.IsFalse(grid.IsCellInArena(new Vector2Int(grid.Width - 5, 20), 0));
            Assert.IsTrue(grid.IsCellInArena(new Vector2Int(grid.Width - 6, 20), 0));
        }

        [Test]
        public void ArenaHalfExtents_ShrinkWithInset()
        {
            GridManager grid = CreateGrid();
            grid.ArenaInset = 0f;
            Assert.AreEqual(22f, grid.ArenaHalfExtents.x, 1e-4f);
            Assert.AreEqual(22f, grid.ArenaHalfExtents.y, 1e-4f);
            grid.ArenaInset = 5f;
            Assert.AreEqual(17f, grid.ArenaHalfExtents.x, 1e-4f);
            Assert.AreEqual(17f, grid.ArenaHalfExtents.y, 1e-4f);
        }

        // ---------------- Свободная клетка для еды ----------------

        private static void AssertSpawnable(GridManager grid, Vector2Int c, int margin)
        {
            Assert.IsTrue(grid.IsInside(c), $"Клетка вне поля: {c}");
            Assert.AreEqual(ObstacleType.None, grid.GetObstacle(c), $"Клетка с препятствием: {c}");
            Assert.IsFalse(HasObstacleNear(grid, c), $"Клетка вплотную к препятствию: {c}");
            Assert.IsTrue(grid.IsCellInArena(c, margin), $"Клетка ближе {margin} клеток к краю арены: {c}");
        }

        [Test]
        public void TryGetRandomFreeCell_NeverReturnsBadCell()
        {
            GridManager grid = CreateGrid();
            for (int i = 0; i < 500; i++)
            {
                Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int c), $"Итерация {i}: нет свободной клетки");
                AssertSpawnable(grid, c, ITEM_MARGIN);
                Assert.GreaterOrEqual(c.x, 2);
                Assert.GreaterOrEqual(c.y, 2);
                Assert.LessOrEqual(c.x, grid.Width - 3);
                Assert.LessOrEqual(c.y, grid.Height - 3);
            }
        }

        [Test]
        public void TryGetRandomFreeCell_IsSpreadOverField()
        {
            GridManager grid = CreateGrid();
            var seen = new HashSet<Vector2Int>();
            for (int i = 0; i < 500; i++)
            {
                grid.TryGetRandomFreeCell(out Vector2Int c);
                seen.Add(c);
            }
            Assert.Greater(seen.Count, 100, "Случайные клетки слишком однообразны");
        }

        [Test]
        public void TryGetRandomFreeCell_RespectsArenaInset()
        {
            GridManager grid = CreateGrid();
            grid.ArenaInset = 5f;
            for (int i = 0; i < 500; i++)
            {
                Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int c));
                AssertSpawnable(grid, c, ITEM_MARGIN);
                Assert.GreaterOrEqual(c.x, 7);
                Assert.LessOrEqual(c.x, grid.Width - 8);
                Assert.GreaterOrEqual(c.y, 7);
                Assert.LessOrEqual(c.y, grid.Height - 8);
            }
        }

        [Test]
        public void TryGetRandomFreeCell_SkipsOccupiedCells()
        {
            GridManager grid = CreateGrid();
            // Занимаем всю левую половину поля.
            foreach (Vector2Int c in AllCells(grid))
                if (c.x < grid.Width / 2) grid.SetOccupied(c, true);

            for (int i = 0; i < 300; i++)
            {
                Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int c));
                Assert.GreaterOrEqual(c.x, grid.Width / 2, $"Выдана занятая клетка {c}");
            }
        }

        [Test]
        public void TryGetRandomFreeCell_OnlyOneFreeCell_ReturnsIt()
        {
            GridManager grid = CreateGrid();
            Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int keep));
            OccupyAllExcept(grid, keep);

            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int c));
                Assert.AreEqual(keep, c);
            }
        }

        [Test]
        public void TryGetRandomFreeCell_OnlyFreeCellAtEdge_ReturnsFalse()
        {
            GridManager grid = CreateGrid();
            OccupyAllExcept(grid, new Vector2Int(1, 20));
            Assert.IsFalse(grid.TryGetRandomFreeCell(out _), "Клетка у края арены не должна выдаваться");

            OccupyAllExcept(grid, new Vector2Int(0, 0));
            Assert.IsFalse(grid.TryGetRandomFreeCell(out _), "Угловая клетка не должна выдаваться");
        }

        [Test]
        public void TryGetRandomFreeCell_OnlyFreeCellNearObstacle_ReturnsFalse()
        {
            GridManager grid = CreateGrid();
            Vector2Int nearObstacle = new Vector2Int(-1, -1);
            Vector2Int obstacle = new Vector2Int(-1, -1);
            foreach (Vector2Int c in AllCells(grid))
            {
                if (grid.GetObstacle(c) != ObstacleType.None)
                {
                    if (obstacle.x < 0) obstacle = c;
                    continue;
                }
                if (nearObstacle.x < 0 && HasObstacleNear(grid, c) && grid.IsCellInArena(c, ITEM_MARGIN)) nearObstacle = c;
            }
            Assert.GreaterOrEqual(nearObstacle.x, 0, "Не нашли клетку рядом с препятствием");
            Assert.GreaterOrEqual(obstacle.x, 0, "Не нашли препятствие");

            OccupyAllExcept(grid, nearObstacle);
            Assert.IsFalse(grid.TryGetRandomFreeCell(out _), $"Клетка рядом с препятствием {nearObstacle} не должна выдаваться");

            OccupyAllExcept(grid, obstacle);
            Assert.IsFalse(grid.TryGetRandomFreeCell(out _), $"Клетка с препятствием {obstacle} не должна выдаваться");
        }

        [Test]
        public void TryGetRandomFreeCell_OnlyFreeCellOutsideShrunkArena_ReturnsFalse()
        {
            GridManager grid = CreateGrid();
            Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int keep));
            // Сужаем арену так, чтобы выбранная клетка оказалась за границей.
            int distToEdge = Mathf.Min(Mathf.Min(keep.x, keep.y), Mathf.Min(grid.Width - 1 - keep.x, grid.Height - 1 - keep.y));
            grid.ArenaInset = distToEdge - ITEM_MARGIN + 1;
            OccupyAllExcept(grid, keep);
            Assert.IsFalse(grid.TryGetRandomFreeCell(out _), $"Клетка {keep} за границей суженной арены (inset {grid.ArenaInset})");
        }

        [Test]
        public void TryGetRandomFreeCell_AllOccupied_ReturnsFalse()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid)) grid.SetOccupied(c, true);
            Assert.IsFalse(grid.TryGetRandomFreeCell(out Vector2Int cell));
            Assert.AreEqual(default(Vector2Int), cell);
        }

        [Test]
        public void ClearAll_FreesOccupiedCells()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid)) grid.SetOccupied(c, true);
            grid.ClearAll();
            Assert.IsTrue(grid.TryGetRandomFreeCell(out Vector2Int cell));
            AssertSpawnable(grid, cell, ITEM_MARGIN);
        }

        [Test]
        public void GenerateLayout_ResetsOccupancy()
        {
            GridManager grid = CreateGrid();
            foreach (Vector2Int c in AllCells(grid)) grid.SetOccupied(c, true);
            grid.GenerateLayout();
            Assert.IsTrue(grid.TryGetRandomFreeCell(out _));
        }
    }
}
