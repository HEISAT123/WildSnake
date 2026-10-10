using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Спрайты интерфейса, нарисованные кодом: скруглённая плашка (9-slice), «пилюля», круг и мягкая тень.
/// Сохраняются PNG в Assets/Materials/UI и импортируются как спрайты с границами для растягивания.
/// </summary>
internal static class UISpriteFactory
{
    private const string FOLDER = "Assets/Materials/UI";

    public static Sprite Rounded => Create("Rounded", 64, 14f, 0f, 18);
    public static Sprite Pill => Create("Pill", 64, 32f, 0f, 31);
    public static Sprite Circle => Create("Circle", 64, 32f, 0f, 0);
    public static Sprite SoftShadow => Create("SoftShadow", 128, 28f, 22f, 60);

    private static Sprite Create(string name, int size, float radius, float blur, int border)
    {
        string path = FOLDER + "/" + name + ".png";
        var cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (cached != null) return cached;

        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets/Materials", "UI");

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        var halfSize = new Vector2(half - blur, half - blur);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = RoundedRectDistance(p, halfSize, Mathf.Min(radius, halfSize.x));
                // Без размытия — сглаженный край в 1 пиксель; с размытием — плавный спад для тени.
                float alpha = blur > 0f ? Mathf.Clamp01(1f - (d + blur * 0.15f) / blur) : Mathf.Clamp01(0.5f - d);
                if (blur > 0f) alpha = alpha * alpha * (3f - 2f * alpha);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
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

    /// <summary>Знаковое расстояние до скруглённого прямоугольника (отрицательное внутри).</summary>
    private static float RoundedRectDistance(Vector2 p, Vector2 halfSize, float radius)
    {
        var q = new Vector2(Mathf.Abs(p.x) - halfSize.x + radius, Mathf.Abs(p.y) - halfSize.y + radius);
        var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }
}
