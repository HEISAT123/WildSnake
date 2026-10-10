using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Графика интерфейса, нарисованная кодом: плашка со скошенными углами и её контур (9-slice),
/// тонкое кольцо, рамка и градиенты затемнения. Спрайты — PNG в Assets/Materials/UI,
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

    /// <summary>Тонкое кольцо (компас еды).</summary>
    public static Sprite Ring => CreateSprite("Ring", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(SIZE * 0.5f, SIZE * 0.5f));
        return d <= SIZE * 0.5f - 0.5f && d >= SIZE * 0.5f - 3.5f;
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

    /// <summary>Вертикальное затемнение сверху вниз — подложка под HUD у краёв экрана.</summary>
    public static Texture2D TopFade()
    {
        const int h = 128;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { name = "TopFade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float a = 0.78f * Smooth(y / (h - 1f));
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0.043f, 0.055f, 0.07f, a));
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

    private static Sprite CreateSprite(string name, Func<float, float, bool> inside, int border)
    {
        string path = FOLDER + "/" + name + ".png";
        var cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (cached != null) return cached;
        EnsureFolder();

        // Покрытие пикселя считаем по 4x4 подвыборкам — края получаются сглаженными.
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
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
