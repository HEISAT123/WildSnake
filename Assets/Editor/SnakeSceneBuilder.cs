using System.Collections.Generic;
using Snake3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Собирает сцену Snake3D с нуля: материалы, поле, змейка, еда, свет, камера, UI, эффекты.
/// Запуск: Tools/Build Snake Scene. Сцену можно пересобирать сколько угодно раз.
/// </summary>
public static class SnakeSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/Snake3D.unity";
    private const string MATERIALS_FOLDER = "Assets/Materials";

    private static Font _font;

    [MenuItem("Tools/Build Snake Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Materials");
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        bool urp = GraphicsSettings.currentRenderPipeline != null;

        // --- Материалы ---
        Material tileLight = CreateLitMaterial("TileLight", new Color(0.80f, 0.86f, 0.74f), 0.1f, urp);
        Material tileDark = CreateLitMaterial("TileDark", new Color(0.62f, 0.72f, 0.56f), 0.1f, urp);
        Material wall = CreateLitMaterial("Wall", new Color(0.36f, 0.30f, 0.42f), 0.2f, urp);
        Material head = CreateLitMaterial("SnakeHead", new Color(0.10f, 0.50f, 0.22f), 0.5f, urp);
        Material body = CreateLitMaterial("SnakeBody", new Color(0.22f, 0.72f, 0.32f), 0.4f, urp);
        Material eye = CreateLitMaterial("SnakeEye", new Color(0.05f, 0.05f, 0.05f), 0.8f, urp);
        Material food = CreateLitMaterial("Food", new Color(0.95f, 0.22f, 0.22f), 0.7f, urp);
        SetEmission(food, new Color(0.6f, 0.08f, 0.05f));
        Material particles = CreateParticleMaterial("Particles");

        // --- Сцена и окружение ---
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.25f);
        RenderSettings.fog = false;

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        // --- Поле ---
        var gridGo = new GameObject("Grid");
        var grid = gridGo.AddComponent<GridManager>();

        var boardGo = new GameObject("Board");
        var board = boardGo.AddComponent<BoardView>();
        Wire(board, ("_grid", grid), ("_lightTileMaterial", tileLight), ("_darkTileMaterial", tileDark), ("_wallMaterial", wall));
        board.Build();

        // --- Змейка ---
        var snakeGo = new GameObject("Snake");
        var snake = snakeGo.AddComponent<SnakeController>();

        Transform headTr = CreatePrimitive(PrimitiveType.Cube, "Head", snakeGo.transform, head).transform;
        headTr.localScale = Vector3.one * 0.95f;
        CreateEye(headTr, new Vector3(-0.25f, 0.22f, 0.45f), eye);
        CreateEye(headTr, new Vector3(0.25f, 0.22f, 0.45f), eye);

        var bodyRoot = new GameObject("Body").transform;
        bodyRoot.SetParent(snakeGo.transform, false);

        GameObject segment = CreatePrimitive(PrimitiveType.Cube, "SegmentTemplate", snakeGo.transform, body);
        segment.transform.localScale = Vector3.one * 0.82f;
        segment.SetActive(false);

        // --- Еда ---
        var foodSpawner = new GameObject("FoodSpawner").AddComponent<FoodSpawner>();
        GameObject foodGo = CreatePrimitive(PrimitiveType.Sphere, "Food", foodSpawner.transform, food);
        foodGo.transform.localScale = Vector3.one * 0.7f;
        Wire(foodSpawner, ("_grid", grid), ("_food", foodGo.transform));

        Wire(snake, ("_grid", grid), ("_food", foodSpawner), ("_head", headTr),
            ("_segmentTemplate", segment.transform), ("_segmentRoot", bodyRoot));

        // --- Эффекты ---
        ParticleSystem eatFx = CreateParticles("EatParticles", particles,
            new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.2f), 2f, 5f, 0.4f);
        ParticleSystem deathFx = CreateParticles("DeathParticles", particles,
            new Color(0.25f, 0.8f, 0.35f), new Color(0.1f, 0.4f, 0.15f), 3f, 8f, 1.2f);

        // --- Камера ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.13f, 0.15f, 0.21f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        camGo.AddComponent<AudioListener>();
        var rig = camGo.AddComponent<CameraRig>();
        Wire(rig, ("_target", headTr));

        // --- Системы ---
        var systems = new GameObject("Systems");
        var score = systems.AddComponent<ScoreService>();
        var game = systems.AddComponent<GameManager>();
        Wire(game, ("_snake", snake), ("_food", foodSpawner), ("_score", score), ("_cameraRig", rig),
            ("_eatParticles", eatFx), ("_deathParticles", deathFx));

        // --- UI ---
        BuildUI(game, score, camGo.transform, foodSpawner, foodGo.transform, headTr);

        // --- Сохранение ---
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AddSceneToBuildSettings(SCENE_PATH);

        Debug.Log("Snake3D: сцена собрана и сохранена в " + SCENE_PATH + (urp ? " (URP)" : " (Built-in)"));
    }

    // ===================== UI =====================

    private static readonly Color PlateColor = new Color(0.06f, 0.08f, 0.12f, 0.72f);
    private static readonly Color CardColor = new Color(0.07f, 0.09f, 0.13f, 0.94f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f);
    private static readonly Color AccentGold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color AccentRed = new Color(1f, 0.45f, 0.4f);
    private static readonly Color MutedText = new Color(0.8f, 0.84f, 0.9f);

    private const string CONTROLS_HINT = "← → / A D — руль     Space — прыжок     Esc — пауза";

    private static void BuildUI(GameManager game, ScoreService score, Transform cameraTransform, FoodSpawner foodSpawner,
        Transform food, Transform head)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif

        // --- HUD: всё на тёмных плашках, чтобы читалось на любом фоне при любом виде камеры ---
        RectTransform hud = CreateRect("HUD", root);
        Stretch(hud);

        RectTransform scorePlate = CreatePlate(hud, "ScorePlate", new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(340f, 84f), PlateColor);
        Text scoreText = CreateFillText(scorePlate, "ScoreText", "Счёт: 0", 44, TextAnchor.MiddleLeft, Color.white, 24f);

        RectTransform bestPlate = CreatePlate(hud, "BestPlate", new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(340f, 84f), PlateColor);
        Text bestText = CreateFillText(bestPlate, "BestText", "Рекорд: 0", 44, TextAnchor.MiddleRight, AccentGold, 24f);

        // Стрелка на еду: без вида сверху это главный ориентир.
        RectTransform pointer = CreatePlate(hud, "FoodPointer", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(150f, 150f), PlateColor);
        var pointerImg = pointer.GetComponent<Image>();
        pointerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        pointerImg.type = Image.Type.Simple;
        RectTransform arrowPivot = CreateRect("ArrowPivot", pointer);
        arrowPivot.anchorMin = new Vector2(0.5f, 0.56f);
        arrowPivot.anchorMax = new Vector2(0.5f, 0.56f);
        arrowPivot.sizeDelta = new Vector2(80f, 80f);
        RectTransform arrow = CreateRect("Arrow", arrowPivot);
        Stretch(arrow);
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // спрайт смотрит вниз, разворачиваем вверх
        var arrowImg = arrow.gameObject.AddComponent<Image>();
        arrowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        arrowImg.color = AccentRed;
        arrowImg.raycastTarget = false;
        Text pointerLabel = CreateText(pointer, "Label", "еда", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(140f, 30f));
        pointerLabel.color = MutedText;

        RectTransform hintPlate = CreatePlate(hud, "HintPlate", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(980f, 60f), PlateColor);
        CreateFillText(hintPlate, "HintText", CONTROLS_HINT, 28, TextAnchor.MiddleCenter, MutedText, 12f);

        // --- Меню ---
        RectTransform menu = CreateOverlay(root, "MenuPanel", out RectTransform menuCard, new Vector2(820f, 600f));
        CreateCardText(menuCard, "Title", "ЗМЕЙКА 3D", 100, new Vector2(0f, 200f), new Vector2(780f, 130f), AccentGreen);
        CreateCardText(menuCard, "Controls",
            "← → или A D — руль (держите для поворота)\nSpace — прыжок, можно перепрыгнуть свой хвост\nEsc — пауза     R — заново",
            32, new Vector2(0f, 40f), new Vector2(760f, 160f), MutedText);
        CreateCardText(menuCard, "StartHint", "Enter / Space — начать", 30, new Vector2(0f, -80f), new Vector2(760f, 50f), MutedText);
        Button playButton = CreateButton(menuCard, "PlayButton", "Играть", new Vector2(0f, -195f), AccentGreen);

        // --- Пауза ---
        RectTransform pause = CreateOverlay(root, "PausePanel", out RectTransform pauseCard, new Vector2(720f, 500f));
        CreateCardText(pauseCard, "Title", "Пауза", 96, new Vector2(0f, 160f), new Vector2(680f, 120f), Color.white);
        CreateCardText(pauseCard, "Subtitle", "Esc — продолжить     R — заново", 32, new Vector2(0f, 60f), new Vector2(680f, 50f), MutedText);
        Button resumeButton = CreateButton(pauseCard, "ResumeButton", "Продолжить", new Vector2(0f, -50f), AccentGreen);
        Button pauseRestartButton = CreateButton(pauseCard, "RestartButton", "Заново", new Vector2(0f, -165f), new Color(0.35f, 0.4f, 0.5f));

        // --- Game Over ---
        RectTransform over = CreateOverlay(root, "GameOverPanel", out RectTransform overCard, new Vector2(760f, 560f));
        Text overTitle = CreateCardText(overCard, "Title", "Игра окончена", 84, new Vector2(0f, 180f), new Vector2(720f, 110f), AccentRed);
        Text finalScore = CreateCardText(overCard, "FinalScoreText", "Счёт: 0", 56, new Vector2(0f, 60f), new Vector2(720f, 70f), Color.white);
        Text finalBest = CreateCardText(overCard, "FinalBestText", "Рекорд: 0", 40, new Vector2(0f, -10f), new Vector2(720f, 56f), AccentGold);
        CreateCardText(overCard, "Hint", "R / Enter — заново", 28, new Vector2(0f, -75f), new Vector2(720f, 40f), MutedText);
        Button restartButton = CreateButton(overCard, "RestartButton", "Заново", new Vector2(0f, -180f), AccentGreen);

        pause.gameObject.SetActive(false);
        over.gameObject.SetActive(false);

        var ui = canvasGo.AddComponent<UIController>();
        Wire(ui,
            ("_game", game), ("_score", score), ("_cameraTransform", cameraTransform), ("_foodSpawner", foodSpawner),
            ("_food", food), ("_head", head),
            ("_scoreText", scoreText), ("_bestText", bestText),
            ("_foodPointer", pointer.gameObject), ("_foodArrow", arrowPivot),
            ("_menuPanel", menu.gameObject), ("_pausePanel", pause.gameObject), ("_gameOverPanel", over.gameObject),
            ("_gameOverTitle", overTitle), ("_finalScoreText", finalScore), ("_finalBestText", finalBest),
            ("_playButton", playButton), ("_resumeButton", resumeButton),
            ("_pauseRestartButton", pauseRestartButton), ("_restartButton", restartButton));
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Скруглённая полупрозрачная плашка; anchor одновременно служит pivot.</summary>
    private static RectTransform CreatePlate(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>Затемнение всего экрана и карточка по центру. Возвращает корень панели.</summary>
    private static RectTransform CreateOverlay(Transform parent, string name, out RectTransform card, Vector2 cardSize)
    {
        RectTransform panel = CreateRect(name, parent);
        Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = DimColor;
        card = CreatePlate(panel, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, cardSize, CardColor);
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment,
        Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;
        return SetupText(rt, value, size, alignment, Color.white);
    }

    /// <summary>Текст на всю плашку с горизонтальным отступом.</summary>
    private static Text CreateFillText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color, float padding)
    {
        RectTransform rt = CreateRect(name, parent);
        Stretch(rt);
        rt.offsetMin = new Vector2(padding, 0f);
        rt.offsetMax = new Vector2(-padding, 0f);
        return SetupText(rt, value, size, alignment, color);
    }

    private static Text CreateCardText(Transform parent, string name, string value, int size, Vector2 position, Vector2 sizeDelta, Color color)
    {
        Text text = CreateText(parent, name, value, size, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), position, sizeDelta);
        text.color = color;
        return text;
    }

    private static Text SetupText(RectTransform rt, string value, int size, TextAnchor alignment, Color color)
    {
        var text = rt.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        // Обводка вместо тени: текст читается и на светлом поле, и на тёмном фоне.
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Color color)
    {
        RectTransform rt = CreatePlate(parent, name, new Vector2(0.5f, 0.5f), position, new Vector2(420f, 90f), color);
        rt.GetComponent<Image>().raycastTarget = true;

        var button = rt.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        button.colors = colors;

        CreateFillText(rt, "Label", label, 40, TextAnchor.MiddleCenter, Color.white, 8f);
        return button;
    }

    // ===================== Объекты =====================

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        // Физика не нужна: столкновения считаются по сетке.
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void CreateEye(Transform head, Vector3 localPos, Material mat)
    {
        GameObject eye = CreatePrimitive(PrimitiveType.Cube, "Eye", head, mat);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = new Vector3(0.18f, 0.18f, 0.14f);
        eye.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static ParticleSystem CreateParticles(string name, Material mat, Color colorA, Color colorB,
        float minSpeed, float maxSpeed, float gravity)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        // Длительность нельзя менять у играющей системы, поэтому сначала останавливаем.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        // Частицы выпускаются вручную через Emit, постоянная эмиссия не нужна.
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;
        return ps;
    }

    // ===================== Материалы =====================

    private static Material CreateLitMaterial(string name, Color color, float smoothness, bool urp)
    {
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        Material mat = LoadOrCreateMaterial(name, shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetEmission(Material mat, Color emission)
    {
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Sprites/Default — прозрачный unlit-шейдер с вертекс-цветом, работает и в Built-in, и в URP.</summary>
    private static Material CreateParticleMaterial(string name)
    {
        Material mat = LoadOrCreateMaterial(name, Shader.Find("Sprites/Default"));
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Переиспользуем существующий ассет, чтобы не ломать ссылки при пересборке.</summary>
    private static Material LoadOrCreateMaterial(string name, Shader shader)
    {
        string path = MATERIALS_FOLDER + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    // ===================== Утилиты =====================

    /// <summary>Записываем приватные [SerializeField] поля через SerializedObject.</summary>
    private static void Wire(Object target, params (string field, Object value)[] links)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in links)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"SnakeSceneBuilder: поле {field} не найдено в {target.GetType().Name}");
                continue;
            }
            prop.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
