using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Рисует подложку карты по данным поля: луг, трава и камыш, рощи и березняки, вода реки, озера и болота,
    /// гряда и горы, брёвна и камни, солнечные камни. Рельеф — отмывка со светом с северо-запада и горизонтали.
    /// Цвета не зависят от времени суток, поэтому карта одинаково читается днём и ночью.
    /// Вызывается один раз при старте: поле статично (генерируется по сиду).
    /// </summary>
    public static class MapPainter
    {
        private static readonly Color Meadow = new Color(0.46f, 0.55f, 0.29f);
        private static readonly Color TallGrass = new Color(0.56f, 0.63f, 0.3f);
        private static readonly Color Reed = new Color(0.5f, 0.6f, 0.4f);
        private static readonly Color Forest = new Color(0.22f, 0.37f, 0.2f);
        private static readonly Color Canopy = new Color(0.16f, 0.3f, 0.17f);
        private static readonly Color Birch = new Color(0.38f, 0.5f, 0.28f);
        private static readonly Color Swamp = new Color(0.37f, 0.41f, 0.28f);
        private static readonly Color Ridge = new Color(0.6f, 0.57f, 0.49f);
        private static readonly Color MountainLow = new Color(0.5f, 0.48f, 0.43f);
        private static readonly Color MountainHigh = new Color(0.82f, 0.8f, 0.76f);
        private static readonly Color Lake = new Color(0.2f, 0.43f, 0.62f);
        private static readonly Color River = new Color(0.25f, 0.52f, 0.72f);
        private static readonly Color SwampWater = new Color(0.22f, 0.37f, 0.38f);
        private static readonly Color Wood = new Color(0.56f, 0.39f, 0.22f);
        private static readonly Color SunStone = new Color(0.88f, 0.74f, 0.44f);
        private static readonly Color Shore = new Color(0.08f, 0.12f, 0.14f);

        /// <summary>Шаг горизонталей, м.</summary>
        private const float ContourStep = 2.5f;

        /// <summary>Текстура карты: клетка (0, 0) — левый нижний угол, +Z мира — вверх.</summary>
        public static Texture2D Paint(GridManager grid, int pixelsPerCell)
        {
            int w = grid.Width, h = grid.Height;
            var cells = new Color[w * h];
            var wet = new float[w * h];

            float mountainMin = float.MaxValue, mountainMax = float.MinValue;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!grid.IsMountain(new Vector2Int(x, y))) continue;
                    float height = CellHeight(grid, x, y);
                    mountainMin = Mathf.Min(mountainMin, height);
                    mountainMax = Mathf.Max(mountainMax, height);
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var cell = new Vector2Int(x, y);
                    cells[i] = CellColor(grid, cell, mountainMin, mountainMax);
                    wet[i] = grid.IsWater(cell) && !grid.IsBridge(cell) ? 1f : 0f;
                }
            }

            int tw = w * pixelsPerCell, th = h * pixelsPerCell;
            var heights = new float[tw * th];
            var pixels = new Color32[tw * th];
            float cellSize = grid.CellSize;
            // Свет с северо-запада (слева сверху на карте): склоны к нему светлее, от него — темнее.
            Vector3 light = new Vector3(-1f, 1.4f, 1f).normalized;

            for (int py = 0; py < th; py++)
            {
                for (int px = 0; px < tw; px++)
                {
                    // Координаты в клетках: центр клетки x — в x + 0.5.
                    float cx = (px + 0.5f) / pixelsPerCell;
                    float cy = (py + 0.5f) / pixelsPerCell;
                    heights[py * tw + px] = CornerHeight(grid, cx, cy);

                    Color color = SampleCells(cells, w, h, cx - 0.5f, cy - 0.5f);
                    float water = SampleWet(wet, w, h, cx - 0.5f, cy - 0.5f);

                    // Наклон рельефа по разнице высот углов вокруг точки.
                    float e = 0.5f;
                    float dx = (CornerHeight(grid, cx + e, cy) - CornerHeight(grid, cx - e, cy)) / (2f * e * cellSize);
                    float dz = (CornerHeight(grid, cx, cy + e) - CornerHeight(grid, cx, cy - e)) / (2f * e * cellSize);
                    Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
                    float shade = Mathf.Clamp(Vector3.Dot(normal, light) / light.y, 0.55f, 1.3f);
                    // Вода ровная — отмывку на ней ослабляем.
                    shade = Mathf.Lerp(shade, 1f, water);
                    color *= shade;

                    // Береговая линия — тёмная кромка там, где вода переходит в сушу.
                    float edge = 1f - Mathf.Abs(water - 0.5f) * 2f;
                    if (water > 0.05f && water < 0.95f) color = Color.Lerp(color, Shore, Mathf.Clamp01(edge * 1.4f) * 0.75f);

                    color.a = 1f;
                    pixels[py * tw + px] = color;
                }
            }

            // Горизонтали: тонкая линия, где высота пересекает очередной шаг (на воде не рисуем).
            for (int py = 0; py < th - 1; py++)
            {
                for (int px = 0; px < tw - 1; px++)
                {
                    int i = py * tw + px;
                    int band = Mathf.FloorToInt(heights[i] / ContourStep);
                    if (band == Mathf.FloorToInt(heights[i + 1] / ContourStep) && band == Mathf.FloorToInt(heights[i + tw] / ContourStep)) continue;
                    if (SampleWet(wet, w, h, (px + 0.5f) / pixelsPerCell - 0.5f, (py + 0.5f) / pixelsPerCell - 0.5f) > 0.3f) continue;
                    Color32 c = pixels[i];
                    pixels[i] = new Color32((byte)(c.r * 0.82f), (byte)(c.g * 0.82f), (byte)(c.b * 0.82f), 255);
                }
            }

            var texture = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                name = "MapPainted",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            // Больше не читаем на процессоре — копию в памяти можно отпустить.
            texture.Apply(false, true);
            return texture;
        }

        private static Color CellColor(GridManager grid, Vector2Int cell, float mountainMin, float mountainMax)
        {
            if (grid.IsBridge(cell) || grid.IsHollow(cell)) return Wood;
            if (grid.IsWater(cell))
            {
                if (grid.IsRiver(cell)) return River;
                return grid.IsSwamp(cell) ? SwampWater : Lake;
            }
            if (grid.IsMountain(cell))
            {
                float t = Mathf.InverseLerp(mountainMin, mountainMax, CellHeight(grid, cell.x, cell.y));
                return Color.Lerp(MountainLow, MountainHigh, t * t);
            }
            if (grid.IsSunStone(cell)) return SunStone;

            Color color;
            if (grid.IsForest(cell)) color = grid.IsBirch(cell) ? Birch : grid.IsCanopy(cell) ? Canopy : Forest;
            else if (grid.IsReed(cell)) color = Reed;
            else if (grid.IsCover(cell)) color = TallGrass;
            else if (grid.IsSwamp(cell)) color = Swamp;
            else if (grid.IsRidge(cell)) color = Ridge;
            else color = Meadow;

            // Стволы, валуны, брёвна и пни — тёмные крапинки.
            ObstacleType obstacle = grid.GetObstacle(cell);
            if (obstacle != ObstacleType.None) color *= 0.75f;
            return color;
        }

        private static float CellHeight(GridManager grid, int x, int y)
        {
            return (grid.GetCornerHeight(x, y) + grid.GetCornerHeight(x + 1, y)
                    + grid.GetCornerHeight(x, y + 1) + grid.GetCornerHeight(x + 1, y + 1)) * 0.25f;
        }

        /// <summary>Высота рельефа в точке (в клетках от левого нижнего угла): билинейно по углам клеток.</summary>
        private static float CornerHeight(GridManager grid, float x, float y)
        {
            x = Mathf.Clamp(x, 0f, grid.Width);
            y = Mathf.Clamp(y, 0f, grid.Height);
            int x0 = Mathf.Min((int)x, grid.Width - 1);
            int y0 = Mathf.Min((int)y, grid.Height - 1);
            float fx = x - x0, fy = y - y0;
            float a = Mathf.Lerp(grid.GetCornerHeight(x0, y0), grid.GetCornerHeight(x0 + 1, y0), fx);
            float b = Mathf.Lerp(grid.GetCornerHeight(x0, y0 + 1), grid.GetCornerHeight(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>Билинейная выборка цветов клеток (координаты в центрах клеток) — края областей мягкие, без лесенки.</summary>
        private static Color SampleCells(Color[] cells, int w, int h, float x, float y)
        {
            Bilinear(w, h, x, y, out int i00, out int i10, out int i01, out int i11, out float fx, out float fy);
            return Color.Lerp(Color.Lerp(cells[i00], cells[i10], fx), Color.Lerp(cells[i01], cells[i11], fx), fy);
        }

        private static float SampleWet(float[] wet, int w, int h, float x, float y)
        {
            Bilinear(w, h, x, y, out int i00, out int i10, out int i01, out int i11, out float fx, out float fy);
            return Mathf.Lerp(Mathf.Lerp(wet[i00], wet[i10], fx), Mathf.Lerp(wet[i01], wet[i11], fx), fy);
        }

        private static void Bilinear(int w, int h, float x, float y, out int i00, out int i10, out int i01, out int i11, out float fx, out float fy)
        {
            x = Mathf.Clamp(x, 0f, w - 1);
            y = Mathf.Clamp(y, 0f, h - 1);
            int x0 = Mathf.Min((int)x, w - 2);
            int y0 = Mathf.Min((int)y, h - 2);
            fx = x - x0;
            fy = y - y0;
            i00 = y0 * w + x0;
            i10 = i00 + 1;
            i01 = i00 + w;
            i11 = i01 + 1;
        }
    }
}
