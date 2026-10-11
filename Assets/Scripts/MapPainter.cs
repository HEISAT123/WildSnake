using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Рисует подложку карты по данным поля: луг, трава, камыш и ежевика, рощи и березняки, вода реки, озера и болота,
    /// гряда и горы, брёвна и камни, солнечные камни, мосты и полые брёвна, тёплые ключи, мышиные городки и осиные гнёзда. Рельеф — отмывка со светом с северо-запада
    /// и сглаженные горизонтали. Цвета не зависят от времени суток, поэтому карта одинаково читается днём и ночью.
    /// Чтобы не было «клеточек», области (вода, рощи, почва) сначала размываются по сетке, а края потом собираются
    /// плавным порогом — получаются ровные сглаженные контуры; деревья и камни — круглые точки, мосты — линии.
    /// Вызывается один раз при старте: поле статично (генерируется по сиду).
    /// </summary>
    public static class MapPainter
    {
        private static readonly Color Meadow = new Color(0.46f, 0.55f, 0.29f);
        private static readonly Color TallGrass = new Color(0.55f, 0.62f, 0.3f);
        private static readonly Color Reed = new Color(0.5f, 0.6f, 0.4f);
        private static readonly Color Forest = new Color(0.2f, 0.34f, 0.19f);
        private static readonly Color Birch = new Color(0.36f, 0.48f, 0.27f);
        private static readonly Color Swamp = new Color(0.37f, 0.41f, 0.28f);
        private static readonly Color Ridge = new Color(0.6f, 0.57f, 0.49f);
        private static readonly Color MountainLow = new Color(0.5f, 0.48f, 0.43f);
        private static readonly Color MountainHigh = new Color(0.84f, 0.82f, 0.78f);
        private static readonly Color Lake = new Color(0.2f, 0.43f, 0.62f);
        private static readonly Color River = new Color(0.25f, 0.52f, 0.72f);
        private static readonly Color SwampWater = new Color(0.22f, 0.37f, 0.38f);
        private static readonly Color Wood = new Color(0.58f, 0.4f, 0.22f);
        private static readonly Color SunStone = new Color(0.9f, 0.76f, 0.46f);
        private static readonly Color Rock = new Color(0.4f, 0.39f, 0.36f);
        private static readonly Color Shore = new Color(0.07f, 0.11f, 0.13f);
        private static readonly Color Bramble = new Color(0.44f, 0.33f, 0.36f);
        private static readonly Color Spring = new Color(0.45f, 0.86f, 0.84f);
        private static readonly Color Mineral = new Color(0.9f, 0.87f, 0.76f);
        private static readonly Color Clearing = new Color(0.58f, 0.56f, 0.36f);
        private static readonly Color Burrow = new Color(0.22f, 0.16f, 0.1f);
        private static readonly Color Wasp = new Color(0.98f, 0.78f, 0.15f);

        /// <summary>Шаг горизонталей, м.</summary>
        private const float ContourStep = 2.5f;

        /// <summary>Порог размытого поля воды, по которому идёт берег (ниже 0.5 — чтобы узкие протоки не исчезали).</summary>
        private const float WaterEdge = 0.4f;

        /// <summary>Текстура карты с mip-картами: клетка (0, 0) — левый нижний угол, +Z мира — вверх.</summary>
        public static Texture2D Paint(GridManager grid, int pixelsPerCell)
        {
            int w = grid.Width, h = grid.Height;
            int n = w * h;
            var land = new Color[n];
            var forestColor = new Color[n];
            var waterColor = new Color[n];
            var forest = new float[n];
            var water = new float[n];
            var mountain = new float[n];

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
                    land[i] = LandColor(grid, cell);
                    mountain[i] = grid.IsMountain(cell) ? 1f : 0f;
                    forest[i] = grid.IsForest(cell) ? 1f : 0f;
                    forestColor[i] = grid.IsBirch(cell) ? Birch : Forest;
                    bool wet = grid.IsWater(cell) && !grid.IsBridge(cell);
                    water[i] = wet ? 1f : 0f;
                    waterColor[i] = grid.IsRiver(cell) ? River : grid.IsSwamp(cell) ? SwampWater : Lake;
                }
            }

            // Размываем по сетке: из «ступенек» клеток получаются плавные поля, которые дальше режутся гладким порогом.
            Blur(land, w, h, 1);
            Blur(forestColor, w, h, 2);
            Blur(waterColor, w, h, 2);
            Blur(forest, w, h, 2);
            // Воду размываем слабее: у истока река в клетку шириной и не должна пропасть.
            Blur(water, w, h, 1);
            Blur(mountain, w, h, 2);

            int tw = w * pixelsPerCell, th = h * pixelsPerCell;
            var heights = new float[tw * th];
            for (int py = 0; py < th; py++)
                for (int px = 0; px < tw; px++)
                    heights[py * tw + px] = CornerHeight(grid, (px + 0.5f) / pixelsPerCell, (py + 0.5f) / pixelsPerCell);

            var pixels = new Color32[tw * th];
            float metersPerPixel = grid.CellSize / pixelsPerCell;
            float[] shading = CornerShading(grid);

            for (int py = 0; py < th; py++)
            {
                for (int px = 0; px < tw; px++)
                {
                    int i = py * tw + px;
                    float cx = (px + 0.5f) / pixelsPerCell - 0.5f;
                    float cy = (py + 0.5f) / pixelsPerCell - 0.5f;

                    float f = SmoothStep(0.35f, 0.6f, Sample(forest, w, h, cx, cy));
                    Color color = Color.Lerp(Sample(land, w, h, cx, cy), Sample(forestColor, w, h, cx, cy), f);
                    // Горы: цвет по высоте в самой точке (без ступенек клеток), граница с долиной — гладкий порог.
                    float rise = Mathf.InverseLerp(mountainMin, mountainMax, heights[i]);
                    Color rock = Color.Lerp(MountainLow, MountainHigh, rise * rise);
                    color = Color.Lerp(color, rock, SmoothStep(0.3f, 0.7f, Sample(mountain, w, h, cx, cy)));

                    // Отмывка рельефа: освещённость посчитана в углах клеток и плавно интерполируется —
                    // наклон билинейной поверхности ломается на границах клеток и давал бы «лесенку».
                    color *= Sample(shading, w + 1, h + 1, cx + 0.5f, cy + 0.5f);
                    int xl = Mathf.Max(px - 1, 0), xr = Mathf.Min(px + 1, tw - 1);
                    int yd = Mathf.Max(py - 1, 0), yu = Mathf.Min(py + 1, th - 1);
                    float dx = (heights[py * tw + xr] - heights[py * tw + xl]) / ((xr - xl) * metersPerPixel);
                    float dz = (heights[yu * tw + px] - heights[yd * tw + px]) / ((yu - yd) * metersPerPixel);

                    // Горизонтали: расстояние до ближайшей линии в пикселях — линия сглажена.
                    float slope = Mathf.Sqrt(dx * dx + dz * dz) * metersPerPixel;
                    if (slope > 1e-4f)
                    {
                        float band = heights[i] / ContourStep;
                        float frac = band - Mathf.Floor(band);
                        float distance = Mathf.Min(frac, 1f - frac) * ContourStep / slope;
                        color *= 1f - 0.16f * (1f - SmoothStep(0.4f, 1.2f, distance));
                    }

                    // Вода: гладкий порог по размытому полю и тёмная береговая кромка.
                    float wet = Sample(water, w, h, cx, cy);
                    color = Color.Lerp(color, Sample(waterColor, w, h, cx, cy), SmoothStep(WaterEdge - 0.06f, WaterEdge + 0.06f, wet));
                    float shore = 1f - SmoothStep(0.03f, 0.09f, Mathf.Abs(wet - WaterEdge));
                    color = Color.Lerp(color, Shore, shore * 0.7f);

                    color.a = 1f;
                    pixels[i] = color;
                }
            }

            DrawFeatures(grid, pixels, tw, th, pixelsPerCell);

            var texture = new Texture2D(tw, th, TextureFormat.RGBA32, true)
            {
                name = "MapPainted",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            texture.SetPixels32(pixels);
            // Mip-карты — чтобы карта, уменьшенная на экране, не рябила; копию на процессоре потом отпускаем.
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Освещённость в углах клеток по центральным разностям высот (свет с северо-запада).</summary>
        private static float[] CornerShading(GridManager grid)
        {
            int cw = grid.Width + 1, ch = grid.Height + 1;
            var shade = new float[cw * ch];
            // Свет с северо-запада (слева сверху на карте): склоны к нему светлее, от него — темнее.
            Vector3 light = new Vector3(-1f, 1.4f, 1f).normalized;
            float cs = grid.CellSize;
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    int xl = Mathf.Max(x - 1, 0), xr = Mathf.Min(x + 1, cw - 1);
                    int yd = Mathf.Max(y - 1, 0), yu = Mathf.Min(y + 1, ch - 1);
                    float dx = (grid.GetCornerHeight(xr, y) - grid.GetCornerHeight(xl, y)) / ((xr - xl) * cs);
                    float dz = (grid.GetCornerHeight(x, yu) - grid.GetCornerHeight(x, yd)) / ((yu - yd) * cs);
                    Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
                    shade[y * cw + x] = Mathf.Clamp(Vector3.Dot(normal, light) / light.y, 0.55f, 1.3f);
                }
            }
            Blur(shade, cw, ch, 1);
            return shade;
        }

        /// <summary>Мелкие объекты поверх областей: камни и деревья — круглые точки, солнечные камни, мосты и полые брёвна.</summary>
        private static void DrawFeatures(GridManager grid, Color32[] pixels, int tw, int th, int ppc)
        {
            float cs = grid.CellSize;
            Vector3 origin = grid.CornerOrigin;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    ObstacleType obstacle = grid.GetObstacle(cell);
                    if (obstacle == ObstacleType.None || obstacle == ObstacleType.Mountain) continue;
                    bool tree = grid.IsForest(cell);
                    // Стволы в роще — мягкая тень кроны; на открытом месте — камень или бревно: круги чуть больше
                    // половины клетки, поэтому соседние клетки бревна сливаются в одну капсулу, а не в пунктир.
                    float radius = (tree ? 0.6f : 0.55f) * ppc;
                    DrawDisc(pixels, tw, th, (x + 0.5f) * ppc, (y + 0.5f) * ppc, radius, tree ? Color.black : Rock, tree ? 0.22f : 0.6f);
                }
            }

            for (int i = 0; i < grid.SunStoneCount; i++)
            {
                Vector2Int stone = grid.GetSunStone(i);
                DrawDisc(pixels, tw, th, (stone.x + 1f) * ppc, (stone.y + 1f) * ppc, 0.95f * ppc, SunStone, 1f);
            }

            // Тёплые ключи — бирюзовая лужица в светлой корке.
            for (int i = 0; i < grid.HotSpringCount; i++)
            {
                Vector2 p = ToPixel(grid.GetHotSpring(i), origin, cs, ppc);
                DrawDisc(pixels, tw, th, p.x, p.y, 1.5f * ppc, Mineral, 0.9f);
                DrawDisc(pixels, tw, th, p.x, p.y, 0.95f * ppc, Spring, 1f);
            }

            // Мышиные городки — вытоптанная полянка и точки норок.
            for (int i = 0; i < grid.MouseColonyCount; i++)
            {
                Vector2 p = ToPixel(grid.GetMouseColony(i), origin, cs, ppc);
                DrawDisc(pixels, tw, th, p.x, p.y, 3f * ppc, Clearing, 0.45f);
            }
            for (int i = 0; i < grid.ColonyHoleCount; i++)
            {
                Vector2 p = ToPixel(grid.GetColonyHole(i), origin, cs, ppc);
                DrawDisc(pixels, tw, th, p.x, p.y, 0.32f * ppc, Burrow, 0.9f);
            }

            // Осиные гнёзда — жёлтая точка с тёмной каймой: видно, куда лучше не соваться.
            for (int i = 0; i < grid.WaspNestCount; i++)
            {
                Vector2 p = ToPixel(grid.GetWaspNest(i), origin, cs, ppc);
                DrawDisc(pixels, tw, th, p.x, p.y, 0.9f * ppc, Shore, 0.85f);
                DrawDisc(pixels, tw, th, p.x, p.y, 0.6f * ppc, Wasp, 1f);
            }

            for (int i = 0; i < grid.BridgeCount; i++)
            {
                grid.GetBridge(i, out Vector3 a, out Vector3 b);
                DrawSegment(pixels, tw, th, ToPixel(a, origin, cs, ppc), ToPixel(b, origin, cs, ppc), 0.45f * ppc, Wood);
            }
            for (int i = 0; i < grid.HollowLogCount; i++)
            {
                grid.GetHollowLog(i, out Vector3 a, out Vector3 b);
                DrawSegment(pixels, tw, th, ToPixel(a, origin, cs, ppc), ToPixel(b, origin, cs, ppc), 0.4f * ppc, Wood);
            }
        }

        private static Vector2 ToPixel(Vector3 world, Vector3 origin, float cellSize, int ppc)
        {
            return new Vector2((world.x - origin.x) / cellSize * ppc, (world.z - origin.z) / cellSize * ppc);
        }

        /// <summary>Сглаженный круг: край растушёван на пиксель.</summary>
        private static void DrawDisc(Color32[] pixels, int tw, int th, float cx, float cy, float radius, Color color, float opacity)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f)), x1 = Mathf.Min(tw - 1, Mathf.CeilToInt(cx + radius + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f)), y1 = Mathf.Min(th - 1, Mathf.CeilToInt(cy + radius + 1f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float k = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * opacity;
                    if (k <= 0f) continue;
                    int i = y * tw + x;
                    pixels[i] = Color.Lerp(pixels[i], color, k);
                }
            }
        }

        /// <summary>Сглаженный отрезок толщиной 2·halfWidth с тёмной кромкой (мост, полое бревно).</summary>
        private static void DrawSegment(Color32[] pixels, int tw, int th, Vector2 a, Vector2 b, float halfWidth, Color color)
        {
            float pad = halfWidth + 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad)), x1 = Mathf.Min(tw - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad)), y1 = Mathf.Min(th - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad));
            Vector2 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    float outline = Mathf.Clamp01(halfWidth + 1.5f - d);
                    if (outline <= 0f) continue;
                    float fill = Mathf.Clamp01(halfWidth + 0.5f - d);
                    int i = y * tw + x;
                    Color c = Color.Lerp(pixels[i], Shore, outline * 0.8f);
                    pixels[i] = Color.Lerp(c, color, fill);
                }
            }
        }

        private static Color LandColor(GridManager grid, Vector2Int cell)
        {
            // Под горами — цвет подножия, чтобы размытие не тянуло в долину ни белого, ни зелёного.
            if (grid.IsMountain(cell)) return MountainLow;
            if (grid.IsBramble(cell)) return Bramble;
            if (grid.IsReed(cell)) return Reed;
            if (grid.IsCover(cell)) return TallGrass;
            if (grid.IsSwamp(cell)) return Swamp;
            if (grid.IsRidge(cell)) return Ridge;
            return Meadow;
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

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // ---------- Выборка и размытие полей клеток ----------

        private static Color Sample(Color[] field, int w, int h, float x, float y)
        {
            Bilinear(w, h, x, y, out int i00, out float fx, out float fy);
            return Color.Lerp(Color.Lerp(field[i00], field[i00 + 1], fx), Color.Lerp(field[i00 + w], field[i00 + w + 1], fx), fy);
        }

        private static float Sample(float[] field, int w, int h, float x, float y)
        {
            Bilinear(w, h, x, y, out int i00, out float fx, out float fy);
            return Mathf.Lerp(Mathf.Lerp(field[i00], field[i00 + 1], fx), Mathf.Lerp(field[i00 + w], field[i00 + w + 1], fx), fy);
        }

        private static void Bilinear(int w, int h, float x, float y, out int i00, out float fx, out float fy)
        {
            x = Mathf.Clamp(x, 0f, w - 1);
            y = Mathf.Clamp(y, 0f, h - 1);
            int x0 = Mathf.Min((int)x, w - 2);
            int y0 = Mathf.Min((int)y, h - 2);
            fx = x - x0;
            fy = y - y0;
            i00 = y0 * w + x0;
        }

        /// <summary>Размытие ядром 1-2-1 по строкам и столбцам, passes раз (на краях — повтор крайней клетки).</summary>
        private static void Blur(float[] field, int w, int h, int passes)
        {
            var tmp = new float[field.Length];
            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tmp[y * w + x] = (field[y * w + Mathf.Max(x - 1, 0)] + 2f * field[y * w + x] + field[y * w + Mathf.Min(x + 1, w - 1)]) * 0.25f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        field[y * w + x] = (tmp[Mathf.Max(y - 1, 0) * w + x] + 2f * tmp[y * w + x] + tmp[Mathf.Min(y + 1, h - 1) * w + x]) * 0.25f;
            }
        }

        private static void Blur(Color[] field, int w, int h, int passes)
        {
            var tmp = new Color[field.Length];
            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tmp[y * w + x] = (field[y * w + Mathf.Max(x - 1, 0)] + 2f * field[y * w + x] + field[y * w + Mathf.Min(x + 1, w - 1)]) * 0.25f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        field[y * w + x] = (tmp[Mathf.Max(y - 1, 0) * w + x] + 2f * tmp[y * w + x] + tmp[Mathf.Min(y + 1, h - 1) * w + x]) * 0.25f;
            }
        }
    }
}
