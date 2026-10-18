using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Сборка готовой игры для Windows одной командой: настраивает Player Settings (название, иконка,
/// полный экран), собирает в Builds/WildSnake_Windows, убирает служебные файлы и пакует в zip,
/// который можно просто отправить другу. Меню: Tools/Build Game (Windows).
/// Batch: -executeMethod GameBuilder.BuildWindows
/// </summary>
public static class GameBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/WildSnake.unity";
    private const string ICON_PATH = "Assets/Settings/GameIcon.png";
    private const string OUTPUT_ROOT = "Builds";
    private const string FOLDER_NAME = "WildSnake_Windows";
    private const string EXE_NAME = "WildSnake.exe";

    [MenuItem("Tools/Build Game (Windows)")]
    public static void BuildWindows()
    {
        if (!File.Exists(SCENE_PATH)) SnakeSceneBuilder.Build();
        ApplyPlayerSettings();

        string folder = Path.Combine(OUTPUT_ROOT, FOLDER_NAME);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { SCENE_PATH },
            locationPathName = Path.Combine(folder, EXE_NAME),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("GameBuilder: сборка не удалась: " + report.summary.result + ", ошибок: " + report.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        RemoveDoNotShip(folder);

        string zip = Path.Combine(OUTPUT_ROOT, FOLDER_NAME + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        // includeBaseDirectory: в архиве будет одна папка, а не россыпь файлов.
        ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, true);

        long size = new FileInfo(zip).Length;
        Debug.Log($"GameBuilder: готово. Папка: {Path.GetFullPath(folder)}, архив: {Path.GetFullPath(zip)} ({size / (1024f * 1024f):0.0} МБ)");
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(zip);
    }

    /// <summary>Название, версия, иконка, полноэкранный режим и Mono (не требует доп. модулей Unity).</summary>
    private static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "WildSnake";
        PlayerSettings.productName = "Wild Snake";
        PlayerSettings.bundleVersion = "1.4";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        Texture2D icon = LoadOrCreateIcon();
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Папки *_DoNotShip — отладочные символы, игроку они не нужны.</summary>
    private static void RemoveDoNotShip(string folder)
    {
        foreach (string dir in Directory.GetDirectories(folder, "*DoNotShip*", SearchOption.TopDirectoryOnly))
            Directory.Delete(dir, true);
    }

    /// <summary>Иконка игры рисуется кодом: зелёная змейка-«S» с глазом на круглом фоне.</summary>
    private static Texture2D LoadOrCreateIcon()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
        if (existing != null) return existing;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size * 0.5f, size * 0.5f);
        var bodyColor = new Color(0.36f, 0.62f, 0.22f);
        var bodyDark = new Color(0.2f, 0.4f, 0.12f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x, y);
                float r = Vector2.Distance(p, center) / (size * 0.5f);
                Color c = r < 0.96f
                    ? Color.Lerp(new Color(0.1f, 0.2f, 0.28f), new Color(0.05f, 0.09f, 0.14f), r)
                    : new Color(0f, 0f, 0f, 0f);

                // Тело — синусоида с толщиной, сужающейся к хвосту (снизу вверх).
                float t = y / (float)size;
                float curveX = size * 0.5f + Mathf.Sin(t * Mathf.PI * 2f) * size * 0.2f;
                float thickness = Mathf.Lerp(size * 0.03f, size * 0.09f, t);
                float dist = Mathf.Abs(x - curveX);
                if (t > 0.12f && t < 0.78f && dist < thickness && r < 0.96f)
                    c = Color.Lerp(bodyColor, bodyDark, dist / thickness);

                c.a = r < 0.96f ? 1f : 0f;
                tex.SetPixel(x, y, c);
            }
        }

        // Голова и глаз.
        var head = new Vector2(size * 0.5f + Mathf.Sin(0.8f * Mathf.PI * 2f) * size * 0.2f, size * 0.8f);
        DrawDisc(tex, head, size * 0.12f, bodyColor);
        DrawDisc(tex, head + new Vector2(size * 0.04f, size * 0.03f), size * 0.035f, new Color(1f, 0.85f, 0.25f));
        DrawDisc(tex, head + new Vector2(size * 0.045f, size * 0.03f), size * 0.012f, Color.black);
        tex.Apply();

        File.WriteAllBytes(ICON_PATH, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ICON_PATH);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
    }

    private static void DrawDisc(Texture2D tex, Vector2 c, float radius, Color color)
    {
        int r = Mathf.CeilToInt(radius);
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= radius * radius)
                    tex.SetPixel(Mathf.RoundToInt(c.x) + x, Mathf.RoundToInt(c.y) + y, color);
    }
}
