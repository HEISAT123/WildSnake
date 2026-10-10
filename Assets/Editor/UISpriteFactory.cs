using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Графика интерфейса, нарисованная кодом: плашка со скошенными углами и её контур (9-slice),
/// кольца-дуги, точки, шеврон, значок змеи, рамка, градиенты краёв и иней. Спрайты — PNG в Assets/Materials/UI,
/// градиенты — текстуры-ассеты там же.
/// </summary>
internal static class UISpriteFactory
{
    private const string FOLDER = "Assets/Materials/UI";
    private const int SIZE = 64;
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

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
