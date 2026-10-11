using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if SNAKE_URP
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Конвейер рендеринга — URP (Built-in в Unity 6 устарел). Ассеты URP создаются кодом, как и всё остальное:
/// три уровня (низкий, средний, высокий) с общим рендерером, назначаются в Graphics и во все уровни Quality,
/// так что переключатель «Качество графики» в настройках меняет и тени, и сглаживание.
/// Вызывается из SnakeSceneBuilder до создания материалов; без пакета URP ничего не делает (сцена соберётся под Built-in).
/// </summary>
public static class RenderPipelineSetup
{
    private const string FOLDER = "Assets/Settings/URP";
    private const string RENDERER_PATH = FOLDER + "/Snake3D_Renderer.asset";
    private const string PROFILE_PATH = FOLDER + "/PostProcess.asset";
    private const string DEFAULT_POST_PROCESS_DATA = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

    [MenuItem("Tools/Setup URP")]
    private static void SetupMenu()
    {
        Debug.Log(EnsureUrp() ? "Snake3D: URP назначен" : "Snake3D: пакет URP не установлен");
    }

    /// <summary>Создать и назначить ассеты URP. false — пакета нет, остаёмся на Built-in.</summary>
    public static bool EnsureUrp()
    {
#if SNAKE_URP
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets/Settings", "URP");

        UniversalRendererData renderer = LoadOrCreateRenderer();
        // Уровни: тени (дистанция, каскады, разрешение, мягкость), MSAA и HDR.
        UniversalRenderPipelineAsset low = LoadOrCreateAsset("URP_Low", renderer, 45f, 1, 1024, false, 1, false);
        UniversalRenderPipelineAsset medium = LoadOrCreateAsset("URP_Medium", renderer, 90f, 2, 2048, true, 2, true);
        UniversalRenderPipelineAsset high = LoadOrCreateAsset("URP_High", renderer, 150f, 4, 4096, true, 4, true);

        GraphicsSettings.defaultRenderPipeline = high;
        // Very Low, Low → низкий; Medium, High → средний; Very High, Ultra → высокий.
        int current = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            float t = names.Length > 1 ? i / (float)(names.Length - 1) : 1f;
            QualitySettings.renderPipeline = t < 0.3f ? low : t < 0.7f ? medium : high;
        }
        QualitySettings.SetQualityLevel(current, false);

        EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
        AssetDatabase.SaveAssets();
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Ключевые слова и очередь материалов URP по их свойствам (_Surface, _AlphaClip, _Cull, карты) — то же,
    /// что делает инспектор материала. Сборщик задаёт свойства кодом, поэтому без этого прозрачность и вырез по альфе не включатся.
    /// </summary>
    public static void FixupMaterials(string folder)
    {
#if SNAKE_URP
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null) continue;
            string shader = mat.shader.name;
            if (!shader.StartsWith("Universal Render Pipeline/")) continue;

            // Свечение сборщик включает без вклада в GI — инспектор URP посчитал бы его выключенным.
            bool emission = mat.IsKeywordEnabled("_EMISSION");
            if (shader.Contains("/Particles/")) BaseShaderGUI.SetMaterialKeywords(mat, null, ParticleGUI.SetMaterialKeywords);
            else if (shader.EndsWith("/Lit")) BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);
            else BaseShaderGUI.SetMaterialKeywords(mat);
            if (emission) mat.EnableKeyword("_EMISSION");
            // Деталь-карта (зерно земли): LitDetailGUI внутренний, ключевое слово ставим сами.
            if (mat.HasProperty("_DetailAlbedoMap") && mat.GetTexture("_DetailAlbedoMap") != null) mat.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(mat);
        }
#endif
    }

    /// <summary>Глобальный Volume: мягкое свечение ярких мест (светлячки, луна, маяк гнезда); палитра сцены не меняется.</summary>
    public static void CreatePostProcessing(Camera camera)
    {
#if SNAKE_URP
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PROFILE_PATH);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.45f);
        bloom.scatter.Override(0.6f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = new GameObject("PostProcess");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
#endif
    }

#if SNAKE_URP
    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;
        component = profile.Add<T>(false);
        // Компонент профиля — подобъект ассета, иначе после перезагрузки профиль окажется пустым.
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static UniversalRendererData LoadOrCreateRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RENDERER_PATH);
        if (renderer != null) return renderer;
        renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DEFAULT_POST_PROCESS_DATA);
        AssetDatabase.CreateAsset(renderer, RENDERER_PATH);
        return renderer;
    }

    private static UniversalRenderPipelineAsset LoadOrCreateAsset(string name, UniversalRendererData renderer,
        float shadowDistance, int cascades, int shadowResolution, bool softShadows, int msaa, bool hdr)
    {
        string path = FOLDER + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (asset == null)
        {
            asset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(asset, path);
        }

        // Большинство полей ассета без публичных сеттеров — пишем через сериализацию, как инспектор.
        var so = new SerializedObject(asset);
        so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        so.FindProperty("m_DefaultRendererIndex").intValue = 0;
        so.FindProperty("m_SupportsHDR").boolValue = hdr;
        so.FindProperty("m_MSAA").intValue = msaa;
        so.FindProperty("m_RenderScale").floatValue = 1f;
        so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowResolution;
        so.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
        so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
        so.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
        // Луна — второй направленный свет: попиксельно, но без теней.
        so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }
#endif
}
