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
// В UnityEditor есть свой SettingsService — явно выбираем игровой.
using SettingsService = Snake3D.SettingsService;

/// <summary>
/// Собирает сцену Snake3D с нуля: текстуры, материалы, конфиг, поле, змеи, предметы, опасности,
/// эффекты, звук, камеры, мини-карта и весь UI. Запуск: Tools/Build Snake Scene.
/// Сцену можно пересобирать сколько угодно раз; GameConfig при этом сохраняет ваши значения.
/// </summary>
public static class SnakeSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/Snake3D.unity";
    private const string MATERIALS_FOLDER = "Assets/Materials";
    private const string SETTINGS_FOLDER = "Assets/Settings";
    private const string MINIMAP_LAYER = "Minimap";

    // Материалы прошлых версий сборщика, которые больше не нужны.
    private static readonly string[] ObsoleteMaterials = { "TileLight", "TileDark", "SnakeStripe", "SnakeEyeWhite" };

    private static readonly Color PlateColor = new Color(0.06f, 0.08f, 0.12f, 0.72f);
    private static readonly Color CardColor = new Color(0.07f, 0.09f, 0.13f, 0.95f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color AccentGreen = new Color(0.35f, 0.8f, 0.42f);
    private static readonly Color AccentGold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color AccentRed = new Color(1f, 0.45f, 0.4f);
    private static readonly Color AccentBlue = new Color(0.4f, 0.6f, 0.95f);
    private static readonly Color ButtonGrey = new Color(0.33f, 0.38f, 0.48f);
    private static readonly Color MutedText = new Color(0.8f, 0.84f, 0.9f);

    private static readonly Color[] PowerUpColors =
    {
        new Color(1f, 0.85f, 0.2f),   // Ускорение
        new Color(0.3f, 0.85f, 1f),   // Замедление
        new Color(0.35f, 0.5f, 1f),   // Щит
        new Color(0.95f, 0.3f, 0.8f), // Магнит
        new Color(1f, 0.55f, 0.15f)   // Очки x2
    };

    private static readonly string[] PowerUpNames = { "Ускорение", "Замедление", "Щит", "Магнит", "Очки x2" };

    private static Font _font;
    private static bool _urp;

    /// <summary>Внешний вид одной змеи.</summary>
    private sealed class SnakeLook
    {
        public Material Body;
        public Material Head;
        public Material Eye;
        public Material Pupil;
        public Material Tongue;
        public Material Marker;
        public Color PieceColor;
    }

    [MenuItem("Tools/Build Snake Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Settings");
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _urp = GraphicsSettings.currentRenderPipeline != null;
        int minimapLayer = EnsureLayer(MINIMAP_LAYER);
        DeleteObsoleteAssets();

        GameConfig config = LoadOrCreateAsset<GameConfig>(SETTINGS_FOLDER + "/GameConfig.asset");

        // ================= Материалы =================
        Texture2D checker = SaveTexture(CreateCheckerTexture(new Color(0.78f, 0.84f, 0.68f), new Color(0.66f, 0.75f, 0.57f)), MATERIALS_FOLDER + "/FloorChecker.asset");
        Texture2D skin = SaveTexture(CreateSkinTexture(), MATERIALS_FOLDER + "/SnakeSkin.asset");

        Material floor = CreateLitMaterial("Floor", Color.white, 0.1f);
        floor.mainTexture = checker;
        Material ground = CreateLitMaterial("Ground", new Color(0.24f, 0.33f, 0.2f), 0.05f);
        Material wall = CreateLitMaterial("Wall", new Color(0.36f, 0.3f, 0.42f), 0.2f);
        Material lowWall = CreateLitMaterial("ObstacleLowWall", new Color(0.66f, 0.5f, 0.36f), 0.15f);
        Material pillar = CreateLitMaterial("ObstaclePillar", new Color(0.5f, 0.56f, 0.66f), 0.3f);
        Material block = CreateLitMaterial("ObstacleBlock", new Color(0.42f, 0.36f, 0.5f), 0.2f);

        var playerLook = new SnakeLook
        {
            Body = CreateLitMaterial("SnakeBody", Color.white, 0.55f),
            Head = CreateLitMaterial("SnakeHead", new Color(0.3f, 0.42f, 0.17f), 0.55f),
            Eye = CreateLitMaterial("SnakeEye", new Color(0.95f, 0.8f, 0.25f), 0.9f),
            Pupil = CreateLitMaterial("SnakePupil", new Color(0.02f, 0.02f, 0.02f), 0.9f),
            Tongue = CreateLitMaterial("SnakeTongue", new Color(0.8f, 0.1f, 0.18f), 0.6f),
            Marker = CreateUnlitMaterial("MarkerPlayer", new Color(0.3f, 1f, 0.4f)),
            PieceColor = new Color(0.36f, 0.5f, 0.2f)
        };
        playerLook.Body.mainTexture = skin;

        var rivalLook = new SnakeLook
        {
            Body = CreateLitMaterial("RivalBody", new Color(1f, 0.62f, 0.38f), 0.55f),
            Head = CreateLitMaterial("RivalHead", new Color(0.55f, 0.28f, 0.12f), 0.55f),
            Eye = CreateLitMaterial("RivalEye", new Color(0.9f, 0.35f, 0.15f), 0.9f),
            Pupil = playerLook.Pupil,
            Tongue = playerLook.Tongue,
            Marker = CreateUnlitMaterial("MarkerRival", new Color(1f, 0.55f, 0.15f)),
            PieceColor = new Color(0.65f, 0.35f, 0.15f)
        };
        rivalLook.Body.mainTexture = skin;

        Material appleNormal = CreateLitMaterial("Food", new Color(0.9f, 0.12f, 0.1f), 0.75f);
        SetEmission(appleNormal, new Color(0.35f, 0.03f, 0.02f));
        Material appleGolden = CreateLitMaterial("FoodGolden", new Color(1f, 0.8f, 0.2f), 0.9f);
        SetEmission(appleGolden, new Color(0.8f, 0.55f, 0.05f));
        Material appleRotten = CreateLitMaterial("FoodRotten", new Color(0.35f, 0.3f, 0.12f), 0.2f);
        Material stem = CreateLitMaterial("AppleStem", new Color(0.35f, 0.22f, 0.1f), 0.1f);
        Material leaf = CreateLitMaterial("AppleLeaf", new Color(0.25f, 0.6f, 0.2f), 0.3f);
        Material foodMarker = CreateUnlitMaterial("MarkerFood", new Color(1f, 0.25f, 0.2f));

        var powerUpMaterials = new Material[PowerUpColors.Length];
        for (int i = 0; i < PowerUpColors.Length; i++)
        {
            powerUpMaterials[i] = CreateLitMaterial("PowerUp" + (PowerUpType)i, PowerUpColors[i], 0.9f);
            SetEmission(powerUpMaterials[i], PowerUpColors[i] * 0.6f);
        }

        Material beam = CreateLitMaterial("SpinnerBeam", new Color(0.85f, 0.25f, 0.2f), 0.5f);
        SetEmission(beam, new Color(0.3f, 0.05f, 0.03f));
        Material post = CreateLitMaterial("SpinnerPost", new Color(0.3f, 0.3f, 0.34f), 0.4f);
        Material danger = CreateLitMaterial("DangerWall", new Color(1f, 0.15f, 0.1f, 0.35f), 0.2f);
        SetEmission(danger, new Color(0.6f, 0.05f, 0.02f));
        MakeTransparent(danger);

        Material particles = CreateParticleMaterial("Particles");
        Material pieces = CreatePiecesMaterial("DeathPieces");
        Material trail = CreateParticleMaterial("DashTrail");

        // ================= Сцена и окружение =================
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var skyColor = new Color(0.56f, 0.7f, 0.84f);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.66f, 0.72f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.5f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = 90f;

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        // ================= Поле =================
        var grid = new GameObject("Grid").AddComponent<GridManager>();
        var board = new GameObject("Board").AddComponent<BoardView>();
        Wire(board, ("_grid", grid), ("_floorMaterial", floor), ("_groundMaterial", ground), ("_wallMaterial", wall),
            ("_lowWallMaterial", lowWall), ("_pillarMaterial", pillar), ("_blockMaterial", block));
        board.Build();

        var particleFloor = new GameObject("ParticleFloor").transform;
        particleFloor.position = grid.Center;

        // ================= Еда =================
        var foodSpawner = new GameObject("FoodSpawner").AddComponent<FoodSpawner>();
        var appleRoot = new GameObject("Food").transform;
        appleRoot.SetParent(foodSpawner.transform, false);
        GameObject apple = CreatePrimitive(PrimitiveType.Sphere, "Apple", appleRoot, appleNormal);
        GameObject stemGo = CreatePrimitive(PrimitiveType.Cylinder, "Stem", appleRoot, stem);
        stemGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        stemGo.transform.localScale = new Vector3(0.07f, 0.12f, 0.07f);
        GameObject leafGo = CreatePrimitive(PrimitiveType.Cube, "Leaf", appleRoot, leaf);
        leafGo.transform.localPosition = new Vector3(0.12f, 0.6f, 0f);
        leafGo.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        leafGo.transform.localScale = new Vector3(0.25f, 0.03f, 0.12f);
        CreateMarker(appleRoot, foodMarker, minimapLayer, 1.8f);
        Wire(foodSpawner, ("_config", config), ("_grid", grid), ("_food", appleRoot), ("_foodRenderer", apple.GetComponent<Renderer>()),
            ("_normalMaterial", appleNormal), ("_goldenMaterial", appleGolden), ("_rottenMaterial", appleRotten));

        // ================= Частицы =================
        ParticleSystem eatFx = CreateParticles("EatParticles", particles, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.2f),
            2f, 5f, 0.4f, 0.08f, 0.22f, 0.4f, 0.9f, 0.25f);
        ParticleSystem deathFx = CreateParticles("DeathSparks", particles, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.2f),
            3f, 8f, 1f, 0.08f, 0.2f, 0.4f, 0.9f, 0.3f);
        ParticleSystem dustFx = CreateParticles("DustParticles", particles, new Color(0.7f, 0.65f, 0.55f, 0.7f), new Color(0.55f, 0.5f, 0.42f, 0.5f),
            0.6f, 1.6f, -0.05f, 0.25f, 0.55f, 0.4f, 0.8f, 0.3f);
        ParticleSystem pickupFx = CreateParticles("PickupParticles", particles, Color.white, new Color(0.5f, 0.9f, 1f),
            3f, 6f, 0f, 0.1f, 0.25f, 0.3f, 0.6f, 0.2f);
        ParticleSystem piecesFx = CreatePiecesParticles(pieces, particleFloor);

        // ================= Змеи =================
        SnakeController player = BuildSnake("Snake", playerLook, true, 1f, config, grid, foodSpawner, piecesFx, trail, minimapLayer);
        SnakeController rival = BuildSnake("RivalSnake", rivalLook, false, 0.85f, config, grid, foodSpawner, piecesFx, null, minimapLayer);
        var ai = rival.gameObject.AddComponent<SnakeAI>();
        Wire(ai, ("_snake", rival), ("_player", player), ("_food", foodSpawner), ("_grid", grid));

        // ================= Бонусы =================
        var powerUpSpawner = new GameObject("PowerUpSpawner").AddComponent<PowerUpSpawner>();
        GameObject crystal = CreatePrimitive(PrimitiveType.Cube, "Crystal", powerUpSpawner.transform, powerUpMaterials[0]);
        crystal.SetActive(false);
        Wire(powerUpSpawner, ("_config", config), ("_grid", grid), ("_player", player), ("_item", crystal.transform),
            ("_itemRenderer", crystal.GetComponent<Renderer>()));
        WireArray(powerUpSpawner, "_typeMaterials", powerUpMaterials);

        // ================= Опасности =================
        var hazards = new GameObject("Hazards").AddComponent<HazardManager>();
        var spinners = new Object[4];
        for (int i = 0; i < spinners.Length; i++) spinners[i] = CreateSpinner(hazards.transform, beam, post);
        Wire(hazards, ("_config", config), ("_grid", grid));
        WireArray(hazards, "_spinners", spinners);

        var arena = new GameObject("ShrinkingArena").AddComponent<ShrinkingArena>();
        var dangerWalls = new Object[4];
        string[] wallNames = { "DangerNorth", "DangerSouth", "DangerEast", "DangerWest" };
        for (int i = 0; i < 4; i++)
        {
            GameObject w = CreatePrimitive(PrimitiveType.Cube, wallNames[i], arena.transform, danger);
            w.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            w.SetActive(false);
            dangerWalls[i] = w.transform;
        }
        Wire(arena, ("_config", config), ("_grid", grid));
        WireArray(arena, "_walls", dangerWalls);

        // ================= Сервисы =================
        var services = new GameObject("Services");
        var score = services.AddComponent<ScoreService>();
        var leaderboard = services.AddComponent<LeaderboardService>();
        var settings = services.AddComponent<SettingsService>();
        var powerUps = services.AddComponent<PowerUpService>();
        Wire(powerUps, ("_config", config));
        var sfxSource = services.AddComponent<AudioSource>();
        var musicSource = services.AddComponent<AudioSource>();
        var audio = services.AddComponent<AudioService>();
        Wire(audio, ("_settings", settings), ("_sfxSource", sfxSource), ("_musicSource", musicSource));

        // ================= Камеры =================
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyColor;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        // Слой 0 — Default: если отдельный слой создать не удалось, не прячем им всю сцену.
        cam.cullingMask = minimapLayer > 0 ? ~(1 << minimapLayer) : ~0;
        camGo.AddComponent<AudioListener>();
        var rig = camGo.AddComponent<CameraRig>();
        Wire(rig, ("_snake", player));

        RenderTexture minimapTexture = LoadOrCreateRenderTexture(SETTINGS_FOLDER + "/Minimap.renderTexture", 384);
        var miniGo = new GameObject("Minimap Camera");
        var miniCam = miniGo.AddComponent<Camera>();
        miniCam.orthographic = true;
        miniCam.orthographicSize = Mathf.Max(grid.Width, grid.Height) * grid.CellSize * 0.5f + 1f;
        miniCam.clearFlags = CameraClearFlags.SolidColor;
        miniCam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
        miniCam.nearClipPlane = 0.3f;
        miniCam.farClipPlane = 40f;
        miniCam.depth = -10f;
        miniCam.targetTexture = minimapTexture;
        miniGo.transform.SetPositionAndRotation(grid.Center + Vector3.up * 20f, Quaternion.Euler(90f, 0f, 0f));

        // ================= GameManager =================
        var game = new GameObject("GameManager").AddComponent<GameManager>();
        Wire(game,
            ("_config", config), ("_grid", grid), ("_hazards", hazards), ("_arena", arena),
            ("_snake", player), ("_rival", rival),
            ("_food", foodSpawner), ("_powerUpSpawner", powerUpSpawner), ("_powerUps", powerUps),
            ("_score", score), ("_leaderboard", leaderboard), ("_settings", settings), ("_audio", audio), ("_cameraRig", rig),
            ("_eatParticles", eatFx), ("_deathParticles", deathFx), ("_dustParticles", dustFx), ("_pickupParticles", pickupFx));

        // ================= UI =================
        BuildUI(game, score, player, powerUps, settings, audio, arena, foodSpawner, leaderboard, camGo.transform, miniCam, minimapTexture);

        // ================= Сохранение =================
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AddSceneToBuildSettings(SCENE_PATH);

        Debug.Log("Snake3D: сцена собрана и сохранена в " + SCENE_PATH + (_urp ? " (URP)" : " (Built-in)"));
    }

    // ===================== Змея =====================

    private static SnakeController BuildSnake(string name, SnakeLook look, bool isPlayer, float speedScale, GameConfig config,
        GridManager grid, FoodSpawner food, ParticleSystem pieces, Material trailMaterial, int markerLayer)
    {
        var root = new GameObject(name);
        var snake = root.AddComponent<SnakeController>();

        // Логическая голова (позиция/поворот/сквош) и визуал под ней.
        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        var headVisual = new GameObject("HeadVisual").transform;
        headVisual.SetParent(head, false);

        // Голова — череп и морда из двух эллипсоидов, плавно переходящих друг в друга.
        GameObject skull = CreatePrimitive(PrimitiveType.Sphere, "Skull", headVisual, look.Head);
        skull.transform.localPosition = new Vector3(0f, 0.03f, 0.02f);
        skull.transform.localScale = new Vector3(0.8f, 0.52f, 1.0f);
        GameObject snout = CreatePrimitive(PrimitiveType.Sphere, "Snout", headVisual, look.Head);
        snout.transform.localPosition = new Vector3(0f, -0.01f, 0.38f);
        snout.transform.localScale = new Vector3(0.56f, 0.36f, 0.6f);

        Transform leftEye = CreateEye(headVisual, new Vector3(-0.27f, 0.15f, 0.28f), -30f, look.Eye, look.Pupil);
        Transform rightEye = CreateEye(headVisual, new Vector3(0.27f, 0.15f, 0.28f), 30f, look.Eye, look.Pupil);
        Transform tongue = CreateTongue(headVisual, look.Tongue);
        CreateMarker(head, look.Marker, markerLayer, 2.2f);

        // Тело — процедурный меш в мировых координатах, поэтому объект стоит в нуле.
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        bodyGo.AddComponent<MeshFilter>();
        bodyGo.AddComponent<MeshRenderer>().sharedMaterial = look.Body;
        var bodyMesh = bodyGo.AddComponent<SnakeBodyMesh>();
        Wire(bodyMesh, ("_snake", snake));

        Wire(snake, ("_config", config), ("_grid", grid), ("_food", food), ("_head", head));
        SetBool(snake, "_isPlayer", isPlayer);
        SetFloat(snake, "_speedScale", speedScale);

        var animator = root.AddComponent<SnakeHeadAnimator>();
        Wire(animator, ("_snake", snake), ("_tongue", tongue), ("_leftEye", leftEye), ("_rightEye", rightEye));

        var death = root.AddComponent<SnakeDeathEffect>();
        Wire(death, ("_snake", snake), ("_body", bodyMesh), ("_headVisual", headVisual.gameObject), ("_pieces", pieces));
        SetColor(death, "_color", look.PieceColor);

        if (trailMaterial != null)
        {
            var trailGo = new GameObject("DashTrail");
            trailGo.transform.SetParent(head, false);
            trailGo.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.1f;
            trail.widthMultiplier = 0.7f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 1f, 0.9f), 0f), new GradientColorKey(new Color(0.3f, 0.7f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            var dashTrail = trailGo.AddComponent<DashTrail>();
            Wire(dashTrail, ("_snake", snake));
        }

        return snake;
    }

    /// <summary>Жёлтый глаз с вертикальным зрачком-щелью, как у настоящей змеи.</summary>
    private static Transform CreateEye(Transform parent, Vector3 localPos, float yaw, Material eyeMat, Material pupilMat)
    {
        GameObject eye = CreatePrimitive(PrimitiveType.Sphere, "Eye", parent, eyeMat);
        eye.transform.localPosition = localPos;
        eye.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        eye.transform.localScale = Vector3.one * 0.2f;
        eye.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        GameObject pupil = CreatePrimitive(PrimitiveType.Sphere, "Pupil", eye.transform, pupilMat);
        pupil.transform.localPosition = new Vector3(0f, 0f, 0.4f);
        pupil.transform.localScale = new Vector3(0.22f, 0.85f, 0.35f);
        pupil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return eye.transform;
    }

    /// <summary>Раздвоенный язык: корень масштабируется по Z, поэтому язык «выдвигается» из пасти.</summary>
    private static Transform CreateTongue(Transform parent, Material mat)
    {
        var root = new GameObject("Tongue").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, -0.08f, 0.62f);

        GameObject stemGo = CreatePrimitive(PrimitiveType.Cube, "TongueBase", root, mat);
        stemGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        stemGo.transform.localScale = new Vector3(0.05f, 0.025f, 0.3f);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject fork = CreatePrimitive(PrimitiveType.Cube, side < 0 ? "ForkLeft" : "ForkRight", root, mat);
            fork.transform.localPosition = new Vector3(0.03f * side, 0f, 0.35f);
            fork.transform.localRotation = Quaternion.Euler(0f, 22f * side, 0f);
            fork.transform.localScale = new Vector3(0.03f, 0.02f, 0.14f);
        }

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return root;
    }

    /// <summary>Яркий диск над объектом, видимый только камере мини-карты.</summary>
    private static void CreateMarker(Transform parent, Material mat, int layer, float size)
    {
        GameObject marker = CreatePrimitive(PrimitiveType.Cylinder, "MinimapMarker", parent, mat);
        marker.transform.localPosition = new Vector3(0f, 3f, 0f);
        marker.transform.localScale = new Vector3(size, 0.02f, size);
        marker.layer = layer;
        var r = marker.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private static SpinnerHazard CreateSpinner(Transform parent, Material beamMat, Material postMat)
    {
        var go = new GameObject("Spinner");
        go.transform.SetParent(parent, false);
        var spinner = go.AddComponent<SpinnerHazard>();

        GameObject postGo = CreatePrimitive(PrimitiveType.Cylinder, "Post", go.transform, postMat);
        postGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        postGo.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        GameObject cap = CreatePrimitive(PrimitiveType.Sphere, "Cap", go.transform, beamMat);
        cap.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        cap.transform.localScale = Vector3.one * 0.45f;

        // Длину балки (масштаб по Z корня Beam) задаёт SpinnerHazard.Configure.
        var beamRoot = new GameObject("Beam").transform;
        beamRoot.SetParent(go.transform, false);
        beamRoot.localScale = new Vector3(1f, 1f, 5f);
        GameObject beamGo = CreatePrimitive(PrimitiveType.Cube, "BeamMesh", beamRoot, beamMat);
        beamGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        beamGo.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        Wire(spinner, ("_beam", beamRoot));
        go.SetActive(false);
        return spinner;
    }

    // ===================== UI =====================

    private static void BuildUI(GameManager game, ScoreService score, SnakeController player, PowerUpService powerUps,
        SettingsService settings, AudioService audio, ShrinkingArena arena, FoodSpawner food, LeaderboardService leaderboard,
        Transform cameraTransform, Camera minimapCamera, RenderTexture minimapTexture)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
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

        // ---------- HUD ----------
        RectTransform hud = CreateRect("HUD", root);
        Stretch(hud);

        RectTransform scorePlate = CreatePlate(hud, "ScorePlate", new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(340f, 80f), PlateColor);
        Text scoreText = CreateFillText(scorePlate, "ScoreText", "Счёт: 0", 42, TextAnchor.MiddleLeft, Color.white, 22f);
        RectTransform levelPlate = CreatePlate(hud, "LevelPlate", new Vector2(0f, 1f), new Vector2(32f, -116f), new Vector2(340f, 56f), PlateColor);
        Text levelText = CreateFillText(levelPlate, "LevelText", "Уровень 1", 32, TextAnchor.MiddleLeft, AccentGreen, 22f);

        RectTransform bestPlate = CreatePlate(hud, "BestPlate", new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(340f, 80f), PlateColor);
        Text bestText = CreateFillText(bestPlate, "BestText", "Рекорд: 0", 42, TextAnchor.MiddleRight, AccentGold, 22f);
        RectTransform timerPlate = CreatePlate(hud, "TimerPlate", new Vector2(1f, 1f), new Vector2(-32f, -116f), new Vector2(220f, 70f), PlateColor);
        Text timerText = CreateFillText(timerPlate, "TimerText", "2:00", 48, TextAnchor.MiddleCenter, Color.white, 8f);
        timerPlate.gameObject.SetActive(false);

        // Стрелка на еду.
        RectTransform pointer = CreatePlate(hud, "FoodPointer", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(130f, 130f), PlateColor);
        Image pointerImg = pointer.GetComponent<Image>();
        pointerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        pointerImg.type = Image.Type.Simple;
        RectTransform arrowPivot = CreateRect("ArrowPivot", pointer);
        arrowPivot.anchorMin = new Vector2(0.5f, 0.56f);
        arrowPivot.anchorMax = new Vector2(0.5f, 0.56f);
        arrowPivot.sizeDelta = new Vector2(70f, 70f);
        RectTransform arrow = CreateRect("Arrow", arrowPivot);
        Stretch(arrow);
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // спрайт смотрит вниз, разворачиваем вверх
        var arrowImg = arrow.gameObject.AddComponent<Image>();
        arrowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        arrowImg.color = AccentRed;
        arrowImg.raycastTarget = false;
        Text pointerLabel = CreateText(pointer, "Label", "еда", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 28f));
        pointerLabel.color = MutedText;

        // Активные бонусы — колонка строк под уровнем.
        var rows = new Object[PowerUpColors.Length];
        var fills = new Object[PowerUpColors.Length];
        for (int i = 0; i < PowerUpColors.Length; i++)
        {
            RectTransform row = CreatePlate(hud, "PowerUp_" + (PowerUpType)i, new Vector2(0f, 1f), new Vector2(32f, -188f - i * 52f), new Vector2(300f, 44f), PlateColor);
            Image fill = CreateFillBar(row, "Fill", new Color(PowerUpColors[i].r, PowerUpColors[i].g, PowerUpColors[i].b, 0.55f));
            CreateFillText(row, "Label", PowerUpNames[i], 26, TextAnchor.MiddleLeft, Color.white, 14f);
            row.gameObject.SetActive(false);
            rows[i] = row.gameObject;
            fills[i] = fill;
        }

        RectTransform dashPlate = CreatePlate(hud, "DashPlate", new Vector2(0f, 0f), new Vector2(32f, 24f), new Vector2(300f, 52f), PlateColor);
        Image dashFill = CreateFillBar(dashPlate, "Fill", new Color(0.4f, 0.85f, 1f, 0.55f));
        CreateFillText(dashPlate, "Label", "Рывок  [Shift]", 26, TextAnchor.MiddleLeft, Color.white, 14f);

        RectTransform hintPlate = CreatePlate(hud, "HintPlate", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(980f, 52f), PlateColor);
        CreateFillText(hintPlate, "HintText", "A D / ← → — руль     Space — прыжок     Shift — рывок     Esc — пауза", 26, TextAnchor.MiddleCenter, MutedText, 12f);

        RectTransform minimapFrame = CreatePlate(hud, "Minimap", new Vector2(1f, 0f), new Vector2(-32f, 24f), new Vector2(300f, 300f), PlateColor);
        RectTransform minimapImageRect = CreateRect("MinimapImage", minimapFrame);
        Stretch(minimapImageRect);
        minimapImageRect.offsetMin = new Vector2(8f, 8f);
        minimapImageRect.offsetMax = new Vector2(-8f, -8f);
        var raw = minimapImageRect.gameObject.AddComponent<RawImage>();
        raw.texture = minimapTexture;
        raw.raycastTarget = false;

        // Подсказки обучения и баннеры.
        RectTransform tutorialPlate = CreatePlate(hud, "TutorialPlate", new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(1200f, 64f), new Color(0.1f, 0.3f, 0.15f, 0.85f));
        Text tutorialText = CreateFillText(tutorialPlate, "TutorialText", "", 30, TextAnchor.MiddleCenter, Color.white, 16f);
        tutorialPlate.gameObject.SetActive(false);

        RectTransform banner = CreateRect("Banner", hud);
        banner.anchorMin = new Vector2(0.5f, 0.72f);
        banner.anchorMax = new Vector2(0.5f, 0.72f);
        banner.sizeDelta = new Vector2(1200f, 110f);
        var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;
        Text bannerText = CreateFillText(banner, "BannerText", "", 72, TextAnchor.MiddleCenter, AccentGold, 0f);

        // ---------- Меню ----------
        RectTransform menu = CreateOverlay(root, "MenuPanel", out RectTransform menuCard, new Vector2(860f, 780f));
        CreateCardText(menuCard, "Title", "ЗМЕЙКА 3D", 100, new Vector2(0f, 310f), new Vector2(820f, 120f), AccentGreen);
        CreateCardText(menuCard, "Controls",
            "A D / ← → — руль    Space — прыжок    Shift — рывок\nСобирайте яблоки и бонусы, избегайте стен, колонн и балок",
            26, new Vector2(0f, 200f), new Vector2(820f, 90f), MutedText);
        Button classicButton = CreateButton(menuCard, "ClassicButton", "Классика", new Vector2(0f, 70f), new Vector2(480f, 84f), 40, AccentGreen);
        Button timeButton = CreateButton(menuCard, "TimeAttackButton", "На время (2 мин)", new Vector2(0f, -30f), new Vector2(480f, 84f), 40, AccentBlue);
        Button survivalButton = CreateButton(menuCard, "SurvivalButton", "Выживание", new Vector2(0f, -130f), new Vector2(480f, 84f), 40, AccentRed);
        Button menuLeaderboardButton = CreateButton(menuCard, "LeaderboardButton", "Рекорды", new Vector2(-125f, -245f), new Vector2(230f, 70f), 32, ButtonGrey);
        Button menuSettingsButton = CreateButton(menuCard, "SettingsButton", "Настройки", new Vector2(125f, -245f), new Vector2(230f, 70f), 32, ButtonGrey);
        CreateCardText(menuCard, "StartHint", "Enter — начать последний режим", 24, new Vector2(0f, -340f), new Vector2(820f, 40f), MutedText);

        // ---------- Пауза ----------
        RectTransform pause = CreateOverlay(root, "PausePanel", out RectTransform pauseCard, new Vector2(720f, 640f));
        CreateCardText(pauseCard, "Title", "Пауза", 96, new Vector2(0f, 230f), new Vector2(680f, 120f), Color.white);
        Button resumeButton = CreateButton(pauseCard, "ResumeButton", "Продолжить", new Vector2(0f, 90f), new Vector2(440f, 84f), 40, AccentGreen);
        Button pauseRestartButton = CreateButton(pauseCard, "RestartButton", "Заново", new Vector2(0f, -10f), new Vector2(440f, 84f), 40, ButtonGrey);
        Button pauseSettingsButton = CreateButton(pauseCard, "SettingsButton", "Настройки", new Vector2(0f, -110f), new Vector2(440f, 84f), 40, ButtonGrey);
        Button pauseMenuButton = CreateButton(pauseCard, "MenuButton", "В меню", new Vector2(0f, -210f), new Vector2(440f, 84f), 40, ButtonGrey);

        // ---------- Game Over ----------
        RectTransform over = CreateOverlay(root, "GameOverPanel", out RectTransform overCard, new Vector2(840f, 800f));
        Text overTitle = CreateCardText(overCard, "Title", "Игра окончена", 84, new Vector2(0f, 320f), new Vector2(800f, 110f), AccentRed);
        Text finalScore = CreateCardText(overCard, "FinalScoreText", "Счёт: 0", 60, new Vector2(0f, 220f), new Vector2(800f, 76f), Color.white);
        Text rankText = CreateCardText(overCard, "RankText", "", 38, new Vector2(0f, 150f), new Vector2(800f, 50f), AccentGold);
        Text statsText = CreateCardText(overCard, "StatsText", "", 30, new Vector2(0f, 10f), new Vector2(800f, 210f), MutedText);
        Button restartButton = CreateButton(overCard, "RestartButton", "Заново (R)", new Vector2(0f, -210f), new Vector2(440f, 84f), 40, AccentGreen);
        Button overMenuButton = CreateButton(overCard, "MenuButton", "В меню", new Vector2(0f, -310f), new Vector2(440f, 84f), 40, ButtonGrey);

        // ---------- Настройки ----------
        RectTransform settingsPanel = CreateOverlay(root, "SettingsPanel", out RectTransform settingsCard, new Vector2(920f, 880f));
        CreateCardText(settingsCard, "Title", "Настройки", 72, new Vector2(0f, 360f), new Vector2(880f, 90f), Color.white);
        Slider masterSlider = CreateSettingSlider(settingsCard, "Общая громкость", 260f, 0f, 1f);
        Slider musicSlider = CreateSettingSlider(settingsCard, "Музыка", 180f, 0f, 1f);
        Slider sfxSlider = CreateSettingSlider(settingsCard, "Звуки", 100f, 0f, 1f);
        Slider steerSlider = CreateSettingSlider(settingsCard, "Чувствительность руля", 20f, 0.5f, 1.5f);
        Toggle fullscreenToggle = CreateSettingToggle(settingsCard, "Полный экран (F11)", -70f);
        Toggle minimapToggle = CreateSettingToggle(settingsCard, "Мини-карта", -145f);
        Toggle rivalToggle = CreateSettingToggle(settingsCard, "Змея-соперник (со следующей партии)", -220f);
        Button qualityButton = CreateButton(settingsCard, "QualityButton", "Качество", new Vector2(-210f, -305f), new Vector2(400f, 70f), 30, ButtonGrey);
        Text qualityLabel = qualityButton.GetComponentInChildren<Text>();
        Button tutorialButton = CreateButton(settingsCard, "TutorialButton", "Показать обучение", new Vector2(210f, -305f), new Vector2(400f, 70f), 30, ButtonGrey);
        Button settingsBack = CreateButton(settingsCard, "BackButton", "Назад", new Vector2(0f, -390f), new Vector2(360f, 70f), 34, AccentGreen);

        // ---------- Рекорды ----------
        RectTransform leaderboardPanel = CreateOverlay(root, "LeaderboardPanel", out RectTransform lbCard, new Vector2(1240f, 800f));
        CreateCardText(lbCard, "Title", "Рекорды", 72, new Vector2(0f, 330f), new Vector2(1200f, 90f), AccentGold);
        var columns = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            Text col = CreateText(lbCard, "Column" + i, "", 28, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f),
                new Vector2(-400f + i * 400f, 10f), new Vector2(360f, 520f));
            col.color = MutedText;
            columns[i] = col;
        }
        Button lbBack = CreateButton(lbCard, "BackButton", "Назад", new Vector2(0f, -330f), new Vector2(360f, 70f), 34, AccentGreen);

        pause.gameObject.SetActive(false);
        over.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        leaderboardPanel.gameObject.SetActive(false);

        // ---------- Компоненты ----------
        var tutorial = canvasGo.AddComponent<TutorialController>();
        Wire(tutorial, ("_game", game), ("_snake", player), ("_plate", tutorialPlate.gameObject), ("_text", tutorialText));

        var settingsComp = settingsPanel.gameObject.AddComponent<SettingsPanel>();
        Wire(settingsComp, ("_settings", settings), ("_audio", audio), ("_tutorial", tutorial),
            ("_masterSlider", masterSlider), ("_musicSlider", musicSlider), ("_sfxSlider", sfxSlider), ("_steerSlider", steerSlider),
            ("_fullscreenToggle", fullscreenToggle), ("_minimapToggle", minimapToggle), ("_rivalToggle", rivalToggle),
            ("_qualityButton", qualityButton), ("_qualityLabel", qualityLabel), ("_tutorialButton", tutorialButton), ("_backButton", settingsBack));

        var lbComp = leaderboardPanel.gameObject.AddComponent<LeaderboardPanel>();
        Wire(lbComp, ("_leaderboard", leaderboard), ("_audio", audio), ("_backButton", lbBack));
        WireArray(lbComp, "_columns", columns);

        var ui = canvasGo.AddComponent<UIController>();
        Wire(ui,
            ("_game", game), ("_score", score), ("_snake", player), ("_powerUps", powerUps), ("_settings", settings),
            ("_audio", audio), ("_arena", arena), ("_food", food), ("_cameraTransform", cameraTransform), ("_minimapCamera", minimapCamera),
            ("_scoreText", scoreText), ("_bestText", bestText), ("_levelText", levelText),
            ("_timerPlate", timerPlate.gameObject), ("_timerText", timerText),
            ("_foodPointer", pointer.gameObject), ("_foodArrow", arrowPivot), ("_dashFill", dashFill),
            ("_minimapRoot", minimapFrame.gameObject), ("_bannerGroup", bannerGroup), ("_bannerText", bannerText),
            ("_menuPanel", menu.gameObject), ("_pausePanel", pause.gameObject), ("_gameOverPanel", over.gameObject),
            ("_settingsPanel", settingsPanel.gameObject), ("_leaderboardPanel", leaderboardPanel.gameObject),
            ("_gameOverTitle", overTitle), ("_finalScoreText", finalScore), ("_rankText", rankText), ("_statsText", statsText),
            ("_classicButton", classicButton), ("_timeAttackButton", timeButton), ("_survivalButton", survivalButton),
            ("_menuLeaderboardButton", menuLeaderboardButton), ("_menuSettingsButton", menuSettingsButton),
            ("_resumeButton", resumeButton), ("_pauseRestartButton", pauseRestartButton),
            ("_pauseSettingsButton", pauseSettingsButton), ("_pauseMenuButton", pauseMenuButton),
            ("_restartButton", restartButton), ("_gameOverMenuButton", overMenuButton));
        WireArray(ui, "_powerUpRows", rows);
        WireArray(ui, "_powerUpFills", fills);
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

    /// <summary>Полоса заполнения на всю плашку (Image типа Filled, слева направо).</summary>
    private static Image CreateFillBar(Transform parent, string name, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        Stretch(rt);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        img.color = color;
        img.raycastTarget = false;
        return img;
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
        text.lineSpacing = 1.1f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        // Обводка: текст читается и на светлом поле, и на тёмном фоне.
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        RectTransform rt = CreatePlate(parent, name, new Vector2(0.5f, 0.5f), position, size, color);
        rt.GetComponent<Image>().raycastTarget = true;

        var button = rt.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        button.colors = colors;

        CreateFillText(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white, 8f);
        return button;
    }

    /// <summary>Строка настроек: подпись слева, слайдер справа.</summary>
    private static Slider CreateSettingSlider(Transform card, string label, float y, float min, float max)
    {
        CreateText(card, "Label", label, 30, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-180f, y), new Vector2(460f, 50f))
            .rectTransform.pivot = new Vector2(0.5f, 0.5f);

        RectTransform rt = CreateRect("Slider", card);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(220f, y);
        rt.sizeDelta = new Vector2(360f, 36f);
        var slider = rt.gameObject.AddComponent<Slider>();

        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        RectTransform bg = CreateRect("Background", rt);
        bg.anchorMin = new Vector2(0f, 0.3f);
        bg.anchorMax = new Vector2(1f, 0.7f);
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = uiSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.25f, 0.28f, 0.34f);

        RectTransform fillArea = CreateRect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.3f);
        fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(5f, 0f);
        fillArea.offsetMax = new Vector2(-15f, 0f);
        RectTransform fill = CreateRect("Fill", fillArea);
        fill.sizeDelta = new Vector2(10f, 0f);
        var fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.sprite = uiSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = AccentGreen;

        RectTransform handleArea = CreateRect("Handle Slide Area", rt);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);
        RectTransform handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(36f, 0f);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        handleImg.color = Color.white;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = max;
        return slider;
    }

    /// <summary>Строка настроек: подпись слева, флажок справа.</summary>
    private static Toggle CreateSettingToggle(Transform card, string label, float y)
    {
        CreateText(card, "Label", label, 30, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-110f, y), new Vector2(600f, 50f))
            .rectTransform.pivot = new Vector2(0.5f, 0.5f);

        RectTransform rt = CreateRect("Toggle", card);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(360f, y);
        rt.sizeDelta = new Vector2(52f, 52f);
        var toggle = rt.gameObject.AddComponent<Toggle>();

        RectTransform bg = CreateRect("Background", rt);
        Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.85f, 0.88f, 0.92f);

        RectTransform check = CreateRect("Checkmark", bg);
        Stretch(check);
        check.offsetMin = new Vector2(6f, 6f);
        check.offsetMax = new Vector2(-6f, -6f);
        var checkImg = check.gameObject.AddComponent<Image>();
        checkImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        checkImg.color = new Color(0.15f, 0.55f, 0.25f);

        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = true;
        return toggle;
    }

    // ===================== Объекты =====================

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        // Физика не нужна: столкновения считаются в коде по данным.
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static ParticleSystem CreateParticles(string name, Material mat, Color colorA, Color colorB,
        float minSpeed, float maxSpeed, float gravity, float minSize, float maxSize, float minLife, float maxLife, float shapeRadius)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        // Длительность нельзя менять у играющей системы, поэтому сначала останавливаем.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
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
        shape.radius = shapeRadius;

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

    /// <summary>Кусочки тела при смерти: сферы-меши с гравитацией, отскакивают от пола.</summary>
    private static ParticleSystem CreatePiecesParticles(Material mat, Transform floor)
    {
        var go = new GameObject("DeathPieces");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        main.startSpeed = 0f;
        main.startSize = 0.3f;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, floor);
        collision.bounce = 0.4f;
        collision.dampen = 0.25f;
        collision.lifetimeLoss = 0f;

        // Меш сферы берём у временного примитива: это встроенный ассет, он переживёт удаление объекта.
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphere = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = sphere;
        renderer.sharedMaterial = mat;
        return ps;
    }

    // ===================== Текстуры =====================

    private static Texture2D CreateCheckerTexture(Color a, Color b)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "FloorChecker", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixels(new[] { a, b, b, a });
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Шкура змеи: U — по окружности (0.25 — спина, 0.75 — брюхо), V — вдоль тела.
    /// На спине ромбы с тёмной каймой, по бокам пятна, брюхо светлое с поперечными щитками,
    /// поверх — мелкая чешуя.
    /// </summary>
    private static Texture2D CreateSkinTexture()
    {
        const int w = 128;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "SnakeSkin", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[w * h];

        var baseColor = new Color(0.36f, 0.48f, 0.2f);
        var diamondFill = new Color(0.52f, 0.58f, 0.26f);
        var dark = new Color(0.12f, 0.16f, 0.07f);
        var belly = new Color(0.86f, 0.8f, 0.58f);

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float distTop = CircularDistance(u, 0.25f);
                float distBelly = CircularDistance(u, 0.75f);

                Color back = baseColor;

                // Ромб на спине: |du|·k + |dv| — «манхэттенское» расстояние даёт ромб.
                float diamond = distTop * 2.2f + Mathf.Abs(v - 0.5f);
                if (diamond < 0.24f) back = diamondFill;
                else if (diamond < 0.3f) back = dark;

                // Боковые пятна между ромбами.
                float side = Mathf.Min(CircularDistance(u, 0f), CircularDistance(u, 0.5f));
                float dv0 = Mathf.Min(v, 1f - v);
                if (side * side * 6f + dv0 * dv0 < 0.012f) back = dark;

                // Мелкая чешуя — лёгкая модуляция яркости.
                float scales = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 24f) * Mathf.Sin(v * Mathf.PI * 16f));
                back *= 0.88f + 0.12f * scales;

                // Брюшные щитки — поперечные полосы.
                Color bellyColor = belly * (Mathf.Repeat(v * 8f, 1f) < 0.12f ? 0.82f : 1f);
                float bellyMask = Mathf.InverseLerp(0.2f, 0.12f, distBelly);

                Color c = Color.Lerp(back, bellyColor, bellyMask);
                c.a = 1f;
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    private static float CircularDistance(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        return Mathf.Min(d, 1f - d);
    }

    /// <summary>Пересохраняем текстуру в ассет (материалы потом получают ссылку заново).</summary>
    private static Texture2D SaveTexture(Texture2D tex, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    private static RenderTexture LoadOrCreateRenderTexture(string path, int size)
    {
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (rt != null) return rt;
        rt = new RenderTexture(size, size, 16) { name = "Minimap" };
        AssetDatabase.CreateAsset(rt, path);
        return rt;
    }

    // ===================== Материалы =====================

    private static Material CreateLitMaterial(string name, Color color, float smoothness)
    {
        Shader shader = Shader.Find(_urp ? "Universal Render Pipeline/Lit" : "Standard");
        Material mat = LoadOrCreateMaterial(name, shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateUnlitMaterial(string name, Color color)
    {
        Shader shader = Shader.Find(_urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        Material mat = LoadOrCreateMaterial(name, shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
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

    /// <summary>Полупрозрачный режим для Standard (Fade) или URP Lit (Transparent).</summary>
    private static void MakeTransparent(Material mat)
    {
        if (_urp)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            mat.SetFloat("_Mode", 2f);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent;
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

    /// <summary>Освещённый шейдер частиц с поддержкой цвета частицы (кусочки разного цвета у разных змей).</summary>
    private static Material CreatePiecesMaterial(string name)
    {
        Shader shader = Shader.Find(_urp ? "Universal Render Pipeline/Particles/Lit" : "Particles/Standard Surface");
        if (shader == null) shader = Shader.Find(_urp ? "Universal Render Pipeline/Lit" : "Standard");
        Material mat = LoadOrCreateMaterial(name, shader);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.5f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
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

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void DeleteObsoleteAssets()
    {
        foreach (string name in ObsoleteMaterials)
        {
            string path = MATERIALS_FOLDER + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
        }
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

    private static void WireArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || !prop.isArray)
        {
            Debug.LogError($"SnakeSceneBuilder: массив {field} не найден в {target.GetType().Name}");
            return;
        }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(Object target, string field, Color value)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedObject FindForWrite(Object target, string field, out SerializedProperty prop)
    {
        var so = new SerializedObject(target);
        prop = so.FindProperty(field);
        if (prop == null) Debug.LogError($"SnakeSceneBuilder: поле {field} не найдено в {target.GetType().Name}");
        return so;
    }

    /// <summary>Находит или создаёт пользовательский слой (8..31) в TagManager.</summary>
    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return 0;
        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        Debug.LogWarning("SnakeSceneBuilder: нет свободного слоя для мини-карты, маркеры будут видны и в основной камере");
        return 0;
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
