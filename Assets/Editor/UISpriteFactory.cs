using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Графика интерфейса, нарисованная кодом: плашка со скошенными углами и её контур (9-slice),
/// кольца-дуги, точки, шеврон, значок змеи, рамка, значки карты, градиенты краёв и иней. Спрайты — PNG в Assets/Materials/UI,
/// градиенты — текстуры-ассеты там же.
/// </summary>
internal static class UISpriteFactory
{
    private const string FOLDER = "Assets/Materials/UI";
    private const int SIZE = 64;
    // Значки карты рисуются в 128 px с mip-картами: на экране они 15–40 px и без mip-карт «рябят» лесенкой.
    private const int ICON_SIZE = 128;
    private const float CUT = 14f;

    /// <summary>Плашка со срезанными левым верхним и правым нижним углами.</summary>
    public static Sprite Chamfer => CreateSprite("Chamfer", (x, y) => InChamfer(x, y, 0f), 18);

    /// <summary>Контур той же формы толщиной 2 px — для клавиш-подсказок.</summary>
    public static Sprite ChamferOutline => CreateSprite("ChamferOutline", (x, y) => InChamfer(x, y, 0f) && !InChamfer(x, y, 2f), 18);

    /// <summary>Тонкое кольцо высокого разрешения — дуги показателей, солнца и прицела (Image Filled).</summary>
    public static Sprite ArcRing => CreateSprite("ArcRing", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(128f, 128f));
        return d <= 127.5f && d >= 121.5f;
    }, 0, 256);

    /// <summary>Сплошной круг (светило, точки легенды).</summary>
    public static Sprite Dot => CreateSprite("Dot", (x, y) =>
        Vector2.Distance(new Vector2(x, y), new Vector2(SIZE * 0.5f, SIZE * 0.5f)) <= SIZE * 0.5f - 1f, 0);

    /// <summary>Наконечник-шеврон, смотрит вверх (метки угроз).</summary>
    public static Sprite Chevron => CreateSprite("Chevron", (x, y) =>
        InTriangle(x, y, 32f, 60f, 6f, 8f, 58f, 8f) && !InTriangle(x, y, 32f, 40f, 16f, 8f, 48f, 8f), 0);

    /// <summary>Значок змеи: S-образное тело с головой сверху (центр блока показателей).</summary>
    public static Sprite SnakeGlyph => CreateSprite("SnakeGlyph", (x, y) =>
    {
        if (Vector2.Distance(new Vector2(x, y), new Vector2(40f, 52f)) <= 7.5f) return true;
        for (int i = 0; i <= 48; i++)
        {
            float t = i / 48f;
            float py = 50f - t * 42f;
            float px = 32f + Mathf.Sin(t * Mathf.PI * 2f + 0.9f) * 11f;
            float width = Mathf.Lerp(5f, 1.6f, t * t);
            if ((x - px) * (x - px) + (y - py) * (y - py) <= width * width) return true;
        }
        return false;
    }, 0);

    /// <summary>Квадратная рамка толщиной 2 px (мини-карта).</summary>
    public static Sprite Frame => CreateSprite("Frame", (x, y) => x < 2f || y < 2f || x > SIZE - 2f || y > SIZE - 2f, 6);

    // ---------- Значки карты ----------
    // Белая заливка с тёмной обводкой: цвет задаёт Image.color (тёмная обводка от умножения не меняется),
    // поэтому значок читается и на светлой траве, и на тёмном лесу. Мелкие зазоры внутри формы становятся тёмными линиями.

    /// <summary>Стрелка-дротик носом вверх: вы и соперница (поворачивается по направлению головы).</summary>
    public static Sprite MapArrow => CreateIcon("MapArrow", (x, y) =>
        InTriangle(x, y, 32f, 59f, 9f, 7f, 32f, 19f) || InTriangle(x, y, 32f, 59f, 32f, 19f, 55f, 7f));

    /// <summary>Тонкое кольцо вокруг вашей стрелки — пульсирует, чтобы себя было легко найти.</summary>
    public static Sprite MapRing => CreateIcon("MapRing", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
        return d <= 27f && d >= 23f;
    }, 2.5f);

    /// <summary>След лапы: мышь, лягушка, птица, кролик, ёж.</summary>
    public static Sprite MapPaw => CreateIcon("MapPaw", (x, y) =>
        InEllipse(x, y, 32f, 22f, 14f, 11.5f) || InCircle(x, y, 13f, 38f, 6.5f) || InCircle(x, y, 25f, 49f, 6.5f)
        || InCircle(x, y, 39f, 49f, 6.5f) || InCircle(x, y, 51f, 38f, 6.5f));

    /// <summary>Рыба носом влево, с глазом.</summary>
    public static Sprite MapFish => CreateIcon("MapFish", (x, y) =>
        (InEllipse(x, y, 27f, 32f, 19f, 11f) || InTriangle(x, y, 40f, 32f, 59f, 47f, 59f, 17f)) && !InCircle(x, y, 17f, 35f, 3f));

    /// <summary>Жук: тело с бороздкой между надкрыльями и голова.</summary>
    public static Sprite MapBeetle => CreateIcon("MapBeetle", (x, y) =>
        (InEllipse(x, y, 32f, 28f, 13f, 17f) && Mathf.Abs(x - 32f) > 1.2f) || InCircle(x, y, 32f, 50f, 7f));

    /// <summary>Орёл с расправленными крыльями.</summary>
    public static Sprite MapEagle => CreateIcon("MapEagle", (x, y) =>
        NearPolyline(x, y, EagleWings, 4.5f) || InEllipse(x, y, 32f, 30f, 6.5f, 13f) || InTriangle(x, y, 32f, 22f, 24f, 8f, 40f, 8f));

    private static readonly float[] EagleWings = { 4f, 38f, 17f, 46f, 32f, 36f, 47f, 46f, 60f, 38f };

    /// <summary>Тёплый ключ: чаша-лужица и две струйки пара.</summary>
    public static Sprite MapSpring => CreateIcon("MapSpring", (x, y) =>
        InEllipse(x, y, 32f, 15f, 25f, 9f) || NearPolyline(x, y, SteamLeft, 3.4f) || NearPolyline(x, y, SteamRight, 3.4f));

    private static readonly float[] SteamLeft = { 24f, 29f, 19f, 38f, 25f, 47f, 20f, 57f };
    private static readonly float[] SteamRight = { 41f, 29f, 36f, 38f, 42f, 47f, 37f, 57f };

    /// <summary>Мышиный городок: голова мыши с круглыми ушами.</summary>
    public static Sprite MapMouse => CreateIcon("MapMouse", (x, y) =>
        (InCircle(x, y, 32f, 25f, 15f) || InCircle(x, y, 17f, 44f, 10.5f) || InCircle(x, y, 47f, 44f, 10.5f) || InTriangle(x, y, 22f, 18f, 42f, 18f, 32f, 4f))
        && !InCircle(x, y, 26f, 28f, 2.6f) && !InCircle(x, y, 38f, 28f, 2.6f));

    /// <summary>Осиное гнездо: полосатая оса с крыльями (пояски — тёмные прорези).</summary>
    public static Sprite MapWasp => CreateIcon("MapWasp", (x, y) =>
        (InEllipse(x, y, 32f, 24f, 11f, 17f) && !(y > 17f && y < 21f) && !(y > 27f && y < 31f))
        || InCircle(x, y, 32f, 47f, 7f) || InEllipse(x, y, 17f, 42f, 11f, 7f) || InEllipse(x, y, 47f, 42f, 11f, 7f));

    /// <summary>Голова лисы: морда треугольником, уши и глаза-прорези.</summary>
    public static Sprite MapFox => CreateIcon("MapFox", (x, y) =>
        (InTriangle(x, y, 7f, 42f, 57f, 42f, 32f, 7f) || InTriangle(x, y, 7f, 42f, 11f, 60f, 26f, 42f)
         || InTriangle(x, y, 57f, 42f, 53f, 60f, 38f, 42f))
        && !InCircle(x, y, 23f, 31f, 3.2f) && !InCircle(x, y, 41f, 31f, 3.2f));

    /// <summary>Нора: холмик-купол с тёмным входом.</summary>
    public static Sprite MapBurrow => CreateIcon("MapBurrow", (x, y) =>
        y >= 12f && InCircle(x, y, 32f, 12f, 25f) && !InCircle(x, y, 32f, 12f, 11f));

    /// <summary>Гнездо для кладки: чаша и три яйца.</summary>
    public static Sprite MapNest => CreateIcon("MapNest", (x, y) =>
        (y <= 22f && InEllipse(x, y, 32f, 22f, 27f, 14f))
        || InEllipse(x, y, 21f, 33f, 8f, 10.5f) || InEllipse(x, y, 43f, 33f, 8f, 10.5f) || InEllipse(x, y, 32f, 45f, 8f, 10.5f));

    /// <summary>Птичье гнездо: яйцо, утопленное в чашу (между ними тёмный зазор).</summary>
    public static Sprite MapBirdNest => CreateIcon("MapBirdNest", (x, y) =>
        InEllipse(x, y, 32f, 34f, 10f, 13f)
        || (y <= 28f && InEllipse(x, y, 32f, 28f, 26f, 18f) && !InEllipse(x, y, 32f, 34f, 13f, 16f)));

    /// <summary>Наконечник стрелки метки угрозы (смотрит вверх), с обводкой — читается на любом фоне.</summary>
    public static Sprite ThreatArrow => CreateIcon("ThreatArrow", (x, y) =>
        InTriangle(x, y, 32f, 60f, 6f, 12f, 58f, 12f) && !InTriangle(x, y, 32f, 30f, 16f, 12f, 48f, 12f));

    /// <summary>Круг высокого разрешения — маска мини-карты.</summary>
    public static Sprite MapDisc => CreateSprite("MapDisc", (x, y) =>
        Vector2.Distance(new Vector2(x, y), new Vector2(128f, 128f)) <= 127f, 0, 256);

    /// <summary>
    /// Мягкая тень из левого верхнего угла: плотная в углу и плавно гаснущая и вправо, и вниз — без видимых краёв.
    /// Для других углов RawImage отражает её через uvRect.
    /// </summary>
    public static Texture2D CornerShade()
    {
        const int w = 256;
        const int h = 160;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "CornerShade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                float v = 1f - y / (h - 1f);
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Pow(1f - Smooth(d), 1.4f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Save(tex);
    }

    /// <summary>Солнечный камень: солнце с лучами.</summary>
    public static Sprite MapSun => CreateIcon("MapSun", (x, y) =>
    {
        if (InCircle(x, y, 32f, 32f, 12f)) return true;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
            if (DistanceToSegment(x, y, 32f + cx * 18f, 32f + cy * 18f, 32f + cx * 26f, 32f + cy * 26f) <= 3f) return true;
        }
        return false;
    });

    /// <summary>Горизонтальное затемнение: плотное слева, прозрачное справа (меню, пауза, итоги).</summary>
    public static Texture2D Vignette(string name, float leftAlpha, float midAlpha, float rightAlpha)
    {
        const int w = 256;
        var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var ink = new Color(0.043f, 0.055f, 0.07f);
        for (int x = 0; x < w; x++)
        {
            float u = x / (w - 1f);
            float a = u < 0.38f ? Mathf.Lerp(leftAlpha, midAlpha, Smooth(u / 0.38f)) : Mathf.Lerp(midAlpha, rightAlpha, Smooth((u - 0.38f) / 0.62f));
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(ink.r, ink.g, ink.b, a));
        }
        tex.Apply();
        return Save(tex);
    }

    /// <summary>Белая виньетка по краям экрана, прозрачная в центре: красится цветом RawImage (вспышка урона).</summary>
    public static Texture2D EdgeVignette()
    {
        const int w = 128;
        const int h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "EdgeVignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f) / h * 2f - 1f;
                // Скруглённый прямоугольник: к углам темнее, центр чистый.
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                float a = Smooth(Mathf.InverseLerp(0.55f, 1.05f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Save(tex);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Внутри ли точка формы со срезанными углами, сжатой на inset пикселей.</summary>
    private static bool InChamfer(float x, float y, float inset)
    {
        float min = inset, max = SIZE - inset;
        if (x < min || y < min || x > max || y > max) return false;
        float cut = CUT - inset * 0.41f;
        // Левый верхний угол (y растёт вверх) и правый нижний.
        if (x - min + (max - y) < cut) return false;
        if ((max - x) + (y - min) < cut) return false;
        return true;
    }

    /// <summary>Горизонтальный край: белый слева, к середине прозрачный (направленная вспышка урона).</summary>
    public static Texture2D EdgeGradient(bool vertical)
    {
        const int n = 128;
        int w = vertical ? 4 : n;
        int h = vertical ? n : 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = vertical ? "EdgeGradientV" : "EdgeGradientH", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // По горизонтали плотно у левого края, по вертикали — у верхнего.
                float t = vertical ? 1f - y / (h - 1f) : x / (w - 1f);
                float a = Mathf.Pow(1f - Smooth(t / 0.45f), 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Save(tex);
    }

    /// <summary>Иней по краям экрана: игольчатые кристаллы растут от краёв к центру (красится цветом RawImage).</summary>
    public static Texture2D FrostEdge()
    {
        const int w = 384;
        const int h = 216;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "FrostEdge", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var rng = new System.Random(77);
        var noise = new float[w * h];
        // Иглы: короткие тонкие штрихи у самого края, направленные внутрь; мягкая кисть без «лесенки».
        for (int k = 0; k < 520; k++)
        {
            float x = (float)rng.NextDouble() * w;
            float y = (float)rng.NextDouble() * h;
            float ex = Mathf.Min(x, w - 1 - x) / w;
            float ey = Mathf.Min(y, h - 1 - y) / h;
            if (Mathf.Min(ex, ey) > 0.1f) continue;
            float angle = Mathf.Atan2(h * 0.5f - y, w * 0.5f - x) + ((float)rng.NextDouble() - 0.5f) * 1.4f;
            float len = 3f + (float)rng.NextDouble() * 10f;
            for (float s = 0f; s < len; s += 0.35f)
            {
                float fx = x + Mathf.Cos(angle) * s;
                float fy = y + Mathf.Sin(angle) * s;
                float strength = (1f - s / len) * 0.8f;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int px = Mathf.FloorToInt(fx) + ox;
                        int py = Mathf.FloorToInt(fy) + oy;
                        if (px < 0 || py < 0 || px >= w || py >= h) continue;
                        float dx = px + 0.5f - fx;
                        float dy = py + 0.5f - fy;
                        float brush = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 1.1f);
                        noise[py * w + px] = Mathf.Max(noise[py * w + px], strength * brush);
                    }
                }
            }
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 6f) + Mathf.Pow(Mathf.Abs(v), 6f), 1f / 6f);
                float edge = Smooth(Mathf.InverseLerp(0.7f, 1.04f, d));
                float a = Mathf.Clamp01(edge * 0.4f + noise[y * w + x] * edge * 0.7f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Save(tex);
    }

    private static bool InCircle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    private static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float u = (x - cx) / rx, v = (y - cy) / ry;
        return u * u + v * v <= 1f;
    }

    private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float ex = ax + dx * t - px, ey = ay + dy * t - py;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    /// <summary>Точка ближе width к ломаной (x0, y0, x1, y1, ...).</summary>
    private static bool NearPolyline(float x, float y, float[] points, float width)
    {
        for (int i = 0; i + 3 < points.Length; i += 2)
            if (DistanceToSegment(x, y, points[i], points[i + 1], points[i + 2], points[i + 3]) <= width) return true;
        return false;
    }

    private static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
        float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
        float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    private static Sprite CreateSprite(string name, Func<float, float, bool> inside, int border, int size = SIZE)
    {
        string path = FOLDER + "/" + name + ".png";
        var cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (cached != null) return cached;
        EnsureFolder();

        // Покрытие пикселя считаем по 4x4 подвыборкам — края получаются сглаженными.
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                        if (inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hits++;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 16f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return ImportSprite(path, border);
    }

    private static Sprite ImportSprite(string path, int border, bool mipmaps = false)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = mipmaps;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Значок карты: белая заливка формы и тёмная обводка толщиной outline (форма задана в координатах 0..64,
    /// растр — ICON_SIZE). Старые 64-пиксельные значки перерисовываются по тому же пути — ссылки на них сохраняются.
    /// </summary>
    private static Sprite CreateIcon(string name, Func<float, float, bool> inside, float outline = 4f)
    {
        string path = FOLDER + "/" + name + ".png";
        var cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (cached != null && cached.texture.width == ICON_SIZE) return cached;
        EnsureFolder();

        var ink = new Color(0.03f, 0.04f, 0.05f);
        const int directions = 16;
        const float scale = (float)SIZE / ICON_SIZE;
        var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
        for (int y = 0; y < ICON_SIZE; y++)
        {
            for (int x = 0; x < ICON_SIZE; x++)
            {
                int fill = 0, ring = 0;
                for (int sy = 0; sy < 4; sy++)
                {
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float px = (x + (sx + 0.5f) / 4f) * scale, py = (y + (sy + 0.5f) / 4f) * scale;
                        if (inside(px, py))
                        {
                            fill++;
                            continue;
                        }
                        // Обводка — форма, раздутая на outline: проверяем точки на двух окружностях вокруг.
                        for (int d = 0; d < directions; d++)
                        {
                            float a = d * Mathf.PI * 2f / directions;
                            if (inside(px + Mathf.Cos(a) * outline, py + Mathf.Sin(a) * outline)
                                || inside(px + Mathf.Cos(a) * outline * 0.5f, py + Mathf.Sin(a) * outline * 0.5f))
                            {
                                ring++;
                                break;
                            }
                        }
                    }
                }
                float fillA = fill / 16f, alpha = (fill + ring * 0.9f) / 16f;
                Color c = alpha > 0f ? Color.Lerp(ink, Color.white, fillA / alpha) : Color.white;
                c.a = alpha;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return ImportSprite(path, 0, true);
    }

    private static Texture2D Save(Texture2D tex)
    {
        EnsureFolder();
        return SnakeSceneBuilder.SaveTexture(tex, FOLDER + "/" + tex.name + ".asset");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets/Materials", "UI");
    }
}
