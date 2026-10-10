using UnityEngine;

/// <summary>
/// Генерация текстур окружения кодом: трава поля, каменная кладка, скала, мрамор, кора, листва,
/// травинки, рельеф и карты нормалей. Все бесшовные (кроме рельефа), вызываются только из редактора.
/// </summary>
public static class ProceduralTextures
{
    // ===================== Шум =====================

    /// <summary>Бесшовный шум Перлина: смешиваем четыре сдвинутых отсчёта по билинейным весам.</summary>
    public static float TileableNoise(float x, float y, float period, float seed)
    {
        float u = x / period;
        float v = y / period;
        float a = Mathf.PerlinNoise(x + seed, y + seed);
        float b = Mathf.PerlinNoise(x - period + seed, y + seed);
        float c = Mathf.PerlinNoise(x + seed, y - period + seed);
        float d = Mathf.PerlinNoise(x - period + seed, y - period + seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    /// <summary>Бесшовный фрактальный шум: октавы с удвоением частоты (период тоже удваивается).</summary>
    public static float TileableFbm(float x, float y, float period, int octaves, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += TileableNoise(x * freq, y * freq, period * freq, seed + o * 17.3f) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    /// <summary>Обычный (не бесшовный) фрактальный шум для рельефа.</summary>
    public static float Fbm(float x, float y, int octaves, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Mathf.PerlinNoise(x * freq + seed + o * 31.7f, y * freq + seed - o * 13.1f) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.03f;
        }
        return sum / norm;
    }

    /// <summary>«Хребтовый» шум: острые гребни — хорошо для гор и трещин.</summary>
    public static float RidgedFbm(float x, float y, int octaves, float seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
        for (int o = 0; o < octaves; o++)
        {
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + seed + o * 7.7f, y * freq + seed) * 2f - 1f);
            sum += n * n * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return sum / norm;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 144665;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    // ===================== Текстуры =====================

    /// <summary>
    /// Трава поля: текстура покрывает 2x2 клетки — две шахматные клетки чуть разного оттенка,
    /// мелкие травинки и тонкие бороздки между клетками (сетка читается, но не бросается в глаза).
    /// </summary>
    public static void GrassFloor(int size, out Texture2D albedo, out Texture2D normal)
    {
        var pixels = new Color[size * size];
        var height = new float[size * size];
        var light = new Color(0.33f, 0.47f, 0.2f);
        var dark = new Color(0.27f, 0.4f, 0.17f);
        var dry = new Color(0.52f, 0.5f, 0.28f);
        int cell = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool checker = ((x / cell) + (y / cell)) % 2 == 0;
                Color c = checker ? light : dark;

                float patches = TileableFbm(x * 0.02f, y * 0.02f, size * 0.02f, 4, 3.1f);
                c = Color.Lerp(c, dry, Mathf.Clamp01((patches - 0.55f) * 2.5f) * 0.45f);

                // Травинки — высокочастотный шум, растянутый по одной оси.
                float blades = TileableNoise(x * 0.9f, y * 0.25f, size * 0.25f, 9.7f);
                float fine = TileableNoise(x * 0.5f, y * 0.5f, size * 0.5f, 5.3f);
                float h = blades * 0.6f + fine * 0.4f;
                c *= 0.82f + 0.3f * h;

                // Бороздка по краю клетки.
                int lx = x % cell, ly = y % cell;
                int edge = Mathf.Min(Mathf.Min(lx, cell - 1 - lx), Mathf.Min(ly, cell - 1 - ly));
                if (edge < 2)
                {
                    c *= 0.78f;
                    h -= 0.5f;
                }

                c.a = 1f;
                pixels[y * size + x] = c;
                height[y * size + x] = h;
            }
        }

        albedo = Create(size, pixels, "GrassFloor", false);
        normal = NormalFromHeight(height, size, 2.2f, "GrassFloor_N");
    }

    /// <summary>Каменная кладка: ряды кирпичей со смещением, раствор, разный оттенок камней, сколы.</summary>
    public static void StoneBricks(int size, out Texture2D albedo, out Texture2D normal)
    {
        var pixels = new Color[size * size];
        var height = new float[size * size];
        int rows = 4;
        int cols = 4;
        int brickH = size / rows;
        int brickW = size / cols;
        int mortar = Mathf.Max(2, size / 96);
        var mortarColor = new Color(0.55f, 0.52f, 0.46f);

        for (int y = 0; y < size; y++)
        {
            int row = y / brickH;
            int offset = (row % 2) * brickW / 2;
            for (int x = 0; x < size; x++)
            {
                int bx = (x + offset) % size;
                int col = bx / brickW;
                int lx = bx % brickW, ly = y % brickH;
                int edge = Mathf.Min(Mathf.Min(lx, brickW - 1 - lx), Mathf.Min(ly, brickH - 1 - ly));

                float n = TileableFbm(x * 0.05f, y * 0.05f, size * 0.05f, 4, 1.7f);
                float tone = Hash(col, row, 11);
                var stone = Color.Lerp(new Color(0.46f, 0.43f, 0.4f), new Color(0.62f, 0.58f, 0.52f), tone);
                stone = Color.Lerp(stone, new Color(0.4f, 0.47f, 0.33f), Mathf.Clamp01((n - 0.6f) * 3f) * 0.5f); // мох

                Color c;
                float h;
                if (edge < mortar)
                {
                    c = mortarColor * (0.85f + 0.2f * n);
                    h = 0.1f * n;
                }
                else
                {
                    float bevel = Mathf.Clamp01((edge - mortar) / (float)(mortar * 2));
                    c = stone * (0.8f + 0.35f * n);
                    h = 0.6f * bevel + 0.4f * n;
                }

                c.a = 1f;
                pixels[y * size + x] = c;
                height[y * size + x] = h;
            }
        }

        albedo = Create(size, pixels, "StoneBricks", false);
        normal = NormalFromHeight(height, size, 4f, "StoneBricks_N");
    }

    /// <summary>Скала: серо-коричневый камень с трещинами (хребтовый шум) и пятнами лишайника.</summary>
    public static void Rock(int size, out Texture2D albedo, out Texture2D normal)
    {
        var pixels = new Color[size * size];
        var height = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = TileableFbm(x * 0.03f, y * 0.03f, size * 0.03f, 5, 4.4f);
                float cracks = 1f - TileableNoise(x * 0.08f, y * 0.08f, size * 0.08f, 8.8f);
                cracks = Mathf.Pow(Mathf.Abs(cracks * 2f - 1f), 0.25f);
                float lichen = TileableFbm(x * 0.06f, y * 0.06f, size * 0.06f, 3, 2.2f);

                Color c = Color.Lerp(new Color(0.38f, 0.36f, 0.34f), new Color(0.6f, 0.56f, 0.5f), n);
                c *= 0.6f + 0.4f * cracks;
                c = Color.Lerp(c, new Color(0.55f, 0.6f, 0.35f), Mathf.Clamp01((lichen - 0.62f) * 4f) * 0.6f);
                c.a = 1f;
                pixels[y * size + x] = c;
                height[y * size + x] = n * 0.7f + cracks * 0.3f;
            }
        }

        albedo = Create(size, pixels, "Rock", false);
        normal = NormalFromHeight(height, size, 3f, "Rock_N");
    }

    /// <summary>Мрамор для колонн: светлый камень с прожилками.</summary>
    public static void Marble(int size, out Texture2D albedo, out Texture2D normal)
    {
        var pixels = new Color[size * size];
        var height = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = TileableFbm(x * 0.04f, y * 0.04f, size * 0.04f, 5, 6.6f);
                float veins = Mathf.Abs(Mathf.Sin((x / (float)size * 2f + n * 3f) * Mathf.PI * 2f));
                veins = Mathf.Pow(veins, 0.15f);
                Color c = Color.Lerp(new Color(0.55f, 0.53f, 0.5f), new Color(0.88f, 0.86f, 0.82f), veins);
                c *= 0.92f + 0.12f * n;
                c.a = 1f;
                pixels[y * size + x] = c;
                height[y * size + x] = n * 0.5f + veins * 0.1f;
            }
        }

        albedo = Create(size, pixels, "Marble", false);
        normal = NormalFromHeight(height, size, 1.5f, "Marble_N");
    }

    /// <summary>Кора: вертикальные волокна и тёмные борозды.</summary>
    public static Texture2D Bark(int size)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fibers = TileableNoise(x * 0.35f, y * 0.04f, size * 0.04f, 3.3f);
                float n = TileableFbm(x * 0.05f, y * 0.05f, size * 0.05f, 3, 1.1f);
                Color c = Color.Lerp(new Color(0.22f, 0.15f, 0.1f), new Color(0.45f, 0.33f, 0.22f), fibers * 0.7f + n * 0.3f);
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }
        return Create(size, pixels, "Bark", false);
    }

    /// <summary>Листва: пятнистая зелень с бликами — для крон и кустов.</summary>
    public static Texture2D Foliage(int size, Color dark, Color bright, string name)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float leaves = TileableNoise(x * 0.25f, y * 0.25f, size * 0.25f, 12.1f);
                float n = TileableFbm(x * 0.04f, y * 0.04f, size * 0.04f, 3, 7.5f);
                Color c = Color.Lerp(dark, bright, Mathf.Clamp01(leaves * 0.7f + n * 0.5f - 0.1f));
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }
        return Create(size, pixels, name, false);
    }

    /// <summary>Лесная подстилка: россыпь опавших листьев на прозрачном фоне, к краям реже (пятна под деревьями).</summary>
    public static Texture2D LeafLitter(int size)
    {
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.3f, 0.22f, 0.12f, 0f);
        var rng = new System.Random(31);
        Color[] palette =
        {
            new Color(0.45f, 0.3f, 0.14f), new Color(0.6f, 0.42f, 0.16f), new Color(0.36f, 0.24f, 0.12f),
            new Color(0.5f, 0.48f, 0.2f), new Color(0.68f, 0.36f, 0.12f)
        };
        float half = size * 0.5f;
        for (int k = 0; k < 420; k++)
        {
            float cx = (float)rng.NextDouble() * size;
            float cy = (float)rng.NextDouble() * size;
            float edge = Mathf.Sqrt((cx - half) * (cx - half) + (cy - half) * (cy - half)) / half;
            if (rng.NextDouble() < edge * edge) continue;
            float angle = (float)rng.NextDouble() * Mathf.PI;
            float len = size * (0.025f + 0.025f * (float)rng.NextDouble());
            float wid = len * 0.45f;
            Color col = palette[rng.Next(palette.Length)] * (0.8f + 0.3f * (float)rng.NextDouble());
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            int r = Mathf.CeilToInt(len);
            for (int y = Mathf.FloorToInt(cy) - r; y <= Mathf.CeilToInt(cy) + r; y++)
            {
                for (int x = Mathf.FloorToInt(cx) - r; x <= Mathf.CeilToInt(cx) + r; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    float dx = x - cx, dy = y - cy;
                    float u = (dx * ca + dy * sa) / len;
                    float v = (-dx * sa + dy * ca) / wid;
                    float d = u * u + v * v;
                    if (d > 1f) continue;
                    // Жилка посередине листа чуть темнее.
                    Color c = Mathf.Abs(v) < 0.12f ? col * 0.75f : col;
                    c.a = Mathf.Clamp01((1f - d) * 4f) * (1f - edge * 0.5f);
                    pixels[y * size + x] = c;
                }
            }
        }
        Texture2D tex = Create(size, pixels, "LeafLitter", false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>Травинки с прозрачностью для пучков травы (шейдер Cutout).</summary>
    public static Texture2D GrassBlades(int size)
    {
        var pixels = new Color[size * size];
        var rng = new System.Random(5);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

        for (int b = 0; b < 18; b++)
        {
            float baseX = (float)rng.NextDouble() * size;
            float bend = ((float)rng.NextDouble() - 0.5f) * size * 0.35f;
            float tall = (0.55f + 0.45f * (float)rng.NextDouble()) * size;
            float width = size * (0.025f + 0.02f * (float)rng.NextDouble());
            var col = Color.Lerp(new Color(0.25f, 0.45f, 0.15f), new Color(0.55f, 0.75f, 0.3f), (float)rng.NextDouble());
            for (int y = 0; y < tall && y < size; y++)
            {
                float k = y / tall;
                float cx = baseX + bend * k * k;
                float w = width * (1f - k);
                for (int x = Mathf.FloorToInt(cx - w); x <= Mathf.CeilToInt(cx + w); x++)
                {
                    if (x < 0 || x >= size) continue;
                    Color c = col * (0.7f + 0.4f * k);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
        }

        Texture2D tex = Create(size, pixels, "GrassBlades", false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>Мелкая зернистость для «детальной» текстуры: вблизи поверхности не выглядят размытыми.</summary>
    public static Texture2D Detail(int size)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = TileableNoise(x * 0.4f, y * 0.4f, size * 0.4f, 21.3f) * 0.6f
                          + TileableNoise(x * 0.1f, y * 0.1f, size * 0.1f, 4.2f) * 0.4f;
                // Для режима MULX2 серый 0.5 — нейтральный.
                float v = 0.38f + n * 0.24f;
                pixels[y * size + x] = new Color(v, v, v, 1f);
            }
        }
        return Create(size, pixels, "Detail", false);
    }

    /// <summary>Рябь на воде — только карта нормалей.</summary>
    public static Texture2D WaterNormal(int size)
    {
        var height = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                height[y * size + x] = TileableFbm(x * 0.06f, y * 0.06f, size * 0.06f, 4, 15.5f);
        return NormalFromHeight(height, size, 2f, "Water_N");
    }

    /// <summary>Цвет рельефа по высоте и крутизне: трава, земля, скала, снег.</summary>
    public static Texture2D Terrain(int size, System.Func<float, float, float> heightAt, float worldSize, Vector3 center)
    {
        var pixels = new Color[size * size];
        float step = worldSize / size;
        var grassA = new Color(0.26f, 0.4f, 0.15f);
        var grassB = new Color(0.36f, 0.46f, 0.19f);
        var dirt = new Color(0.4f, 0.32f, 0.21f);
        var rock = new Color(0.36f, 0.34f, 0.31f);
        var snow = new Color(0.93f, 0.95f, 0.98f);
        var sand = new Color(0.76f, 0.7f, 0.52f);

        // Высоты считаем один раз в сетку (size+1)², уклон — разностью соседей.
        int side = size + 1;
        var heights = new float[side * side];
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
                heights[y * side + x] = heightAt(center.x - worldSize * 0.5f + (x + 0.5f) * step, center.z - worldSize * 0.5f + (y + 0.5f) * step);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float wx = center.x - worldSize * 0.5f + (x + 0.5f) * step;
                float wz = center.z - worldSize * 0.5f + (y + 0.5f) * step;
                float h = heights[y * side + x];
                float hx = heights[y * side + x + 1] - h;
                float hz = heights[(y + 1) * side + x] - h;
                float slope = Mathf.Sqrt(hx * hx + hz * hz) / step;

                float n = Fbm(wx * 0.05f, wz * 0.05f, 4, 2.2f);
                Color c = Color.Lerp(grassA, grassB, n);
                c = Color.Lerp(c, dirt, Mathf.Clamp01((Fbm(wx * 0.02f, wz * 0.02f, 3, 8.1f) - 0.62f) * 4f));
                c = Color.Lerp(c, sand, Mathf.Clamp01((-h - 2.2f) * 1.5f)); // дно водоёмов, а не каждая низина
                c = Color.Lerp(c, rock, Mathf.Clamp01((slope - 0.55f) * 3f));
                // Скала темнеет с высотой, снег — только на вершинах и пологих местах.
                c = Color.Lerp(c, rock * 0.85f, Mathf.Clamp01((h - 12f) * 0.08f));
                c = Color.Lerp(c, snow, Mathf.Clamp01((h - 33f + n * 8f) * 0.2f) * Mathf.Clamp01(1.6f - slope));
                c *= 0.88f + 0.24f * Fbm(wx * 0.4f, wz * 0.4f, 2, 5.5f);
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        Texture2D tex = Create(size, pixels, "Terrain", false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ===================== Утилиты =====================

    private static Texture2D Create(int size, Color[] pixels, string name, bool linear)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };
        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>
    /// Карта нормалей из высот (оператор Собеля, с заворотом краёв — бесшовно).
    /// Нормаль кладём в RGB, A = 1: Unity распаковывает такой формат (UnpackNormalmapRGorAG).
    /// </summary>
    public static Texture2D NormalFromHeight(float[] height, int size, float strength, string name)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float l = height[y * size + (x - 1 + size) % size];
                float r = height[y * size + (x + 1) % size];
                float d = height[((y - 1 + size) % size) * size + x];
                float u = height[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        return Create(size, pixels, name, true);
    }
}
