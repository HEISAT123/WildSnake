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
/// Собирает сцену Snake3D с нуля: текстуры, материалы, конфиг, поле с зарослями, змеи, живность, орёл,
/// смена дня и ночи, нюх, эффекты, звук, камеры, мини-карта и весь UI. Запуск: Tools/Build Snake Scene.
/// Сцену можно пересобирать сколько угодно раз; GameConfig при этом сохраняет ваши значения.
/// </summary>
public static class SnakeSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/Snake3D.unity";
    private const string MATERIALS_FOLDER = "Assets/Materials";
    private const string SETTINGS_FOLDER = "Assets/Settings";
    private const string MINIMAP_LAYER = "Minimap";
    private const int SCENT_WISPS = 16;

    // Материалы прошлых версий сборщика, которые больше не нужны.
    private static readonly string[] ObsoleteMaterials =
    {
        "TileLight", "TileDark", "SnakeStripe", "SnakeEyeWhite", "Ground",
        "Food", "FoodGolden", "FoodRotten", "AppleStem", "AppleLeaf", "MarkerFood",
        "PowerUpSpeed", "PowerUpSlowTime", "PowerUpShield", "PowerUpMagnet", "PowerUpDoubleScore",
        "SpinnerBeam", "SpinnerPost", "DangerWall", "ObstaclePillar", "PickupParticles"
    };

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

    /// <summary>Готовая змея и её части, нужные другим системам.</summary>
    private sealed class SnakeParts
    {
        public SnakeController Controller;
        public SnakeHeadAnimator Animator;
        public SnakeBodyMesh Body;
    }

    [MenuItem("Tools/Build Snake Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Сцену создаём первой: NewScene выгружает неиспользуемые ассеты, и загруженные до неё
        // GameConfig и материалы превратились бы в «фальшивый null» — ссылки в сцене остались бы пустыми.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Settings");
        _urp = GraphicsSettings.currentRenderPipeline != null;
        int minimapLayer = EnsureLayer(MINIMAP_LAYER);
        DeleteObsoleteAssets();

        GameConfig config = LoadOrCreateAsset<GameConfig>(SETTINGS_FOLDER + "/GameConfig.asset");

        // ================= Материалы =================
        Texture2D skin = SaveTexture(CreateSkinTexture(), MATERIALS_FOLDER + "/SnakeSkin.asset");
        EnvironmentBuilder.ArenaMaterials arena = EnvironmentBuilder.CreateArenaMaterials();

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

        Material particles = CreateParticleMaterial("Particles");
        Material pieces = CreatePiecesMaterial("DeathPieces");
        Material trail = CreateParticleMaterial("DashTrail");

        // ================= Свет =================
        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        // Луна — второй направленный свет; DayNightCycle включает её только ночью.
        var moonGo = new GameObject("Moon");
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.66f, 1f);
        moon.intensity = 0f;
        moon.shadows = LightShadows.None;
        moon.shadowStrength = 0.5f;
        moon.enabled = false;

        // ================= Поле =================
        var grid = new GameObject("Grid").AddComponent<GridManager>();
        var board = new GameObject("Board").AddComponent<BoardView>();
        Wire(board, ("_grid", grid), ("_floorMaterial", arena.Floor), ("_wallMaterial", arena.Wall),
            ("_lowWallMaterial", arena.LowWall), ("_barkMaterial", arena.Bark), ("_stumpTopMaterial", arena.StumpTop),
            ("_blockMaterial", arena.Block), ("_tallGrassMaterial", arena.TallGrass),
            ("_rockMesh", arena.RockMesh), ("_grassTuftMesh", arena.TuftMesh));
        board.Build();
        EnvironmentBuilder.Build(grid, particles, out ParticleSystem pollen, out Renderer stars, out Transform clouds);

        var particleFloor = new GameObject("ParticleFloor").transform;
        particleFloor.position = grid.Center;

        // ================= Частицы =================
        ParticleSystem eatFx = CreateParticles("EatParticles", particles, new Color(1f, 0.85f, 0.4f), new Color(0.75f, 0.2f, 0.15f),
            2f, 5f, 0.4f, 0.08f, 0.22f, 0.4f, 0.9f, 0.25f);
        ParticleSystem deathFx = CreateParticles("DeathSparks", particles, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.2f),
            3f, 8f, 1f, 0.08f, 0.2f, 0.4f, 0.9f, 0.3f);
        ParticleSystem dustFx = CreateParticles("DustParticles", particles, new Color(0.7f, 0.65f, 0.55f, 0.7f), new Color(0.55f, 0.5f, 0.42f, 0.5f),
            0.6f, 1.6f, -0.05f, 0.25f, 0.55f, 0.4f, 0.8f, 0.3f);
        ParticleSystem moltFx = CreateParticles("MoltSparkles", particles, new Color(1f, 0.97f, 0.85f), new Color(0.8f, 1f, 0.4f),
            1.5f, 4f, -0.1f, 0.08f, 0.2f, 0.6f, 1.2f, 0.6f);
        ParticleSystem piecesFx = CreatePiecesParticles(pieces, particleFloor);

        // ================= Змеи =================
        SnakeParts player = BuildSnake("Snake", playerLook, true, 1f, config, grid, piecesFx, trail, minimapLayer);
        SnakeParts rival = BuildSnake("RivalSnake", rivalLook, false, 0.85f, config, grid, piecesFx, null, minimapLayer);

        // ================= Живность =================
        var wildlife = new GameObject("Wildlife").AddComponent<WildlifeManager>();
        PreyAnimal[] animals = BuildPreyPool(wildlife.transform, config, minimapLayer);
        Wire(wildlife, ("_config", config), ("_grid", grid), ("_player", player.Controller), ("_rival", rival.Controller));
        WireArray(wildlife, "_animals", animals);

        var ai = rival.Controller.gameObject.AddComponent<SnakeAI>();
        Wire(ai, ("_snake", rival.Controller), ("_player", player.Controller), ("_wildlife", wildlife), ("_grid", grid));
        // Автопилот змеи игрока — она сама охотится на фоне главного меню; на соперницу не нападает.
        var playerAi = player.Controller.gameObject.AddComponent<SnakeAI>();
        Wire(playerAi, ("_snake", player.Controller), ("_player", rival.Controller), ("_wildlife", wildlife), ("_grid", grid));
        SetFloat(playerAi, "_aggression", 0f);
        SetBool(playerAi, "_canJump", false);
        playerAi.enabled = false;

        // ================= Орёл =================
        EagleHunter eagle = BuildEagle(config, grid, player.Controller, minimapLayer);

        // ================= Нюх и линька =================
        var scent = new GameObject("Scent").AddComponent<ScentTracker>();
        Material wispMat = CreateUnlitMaterial("ScentWisp", new Color(0.82f, 1f, 0.35f));
        var wisps = new Object[SCENT_WISPS];
        for (int i = 0; i < wisps.Length; i++)
        {
            GameObject w = CreatePrimitive(PrimitiveType.Sphere, "Wisp", scent.transform, wispMat);
            w.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            w.SetActive(false);
            wisps[i] = w.transform;
        }
        Wire(scent, ("_config", config), ("_snake", player.Controller), ("_wildlife", wildlife));
        WireArray(scent, "_wisps", wisps);

        Material shedMat = CreateLitMaterial("ShedSkin", new Color(0.92f, 0.88f, 0.74f, 0.7f), 0.3f);
        shedMat.mainTexture = skin;
        MakeTransparent(shedMat);
        var shedGo = new GameObject("ShedSkin");
        shedGo.AddComponent<MeshFilter>();
        var shedRenderer = shedGo.AddComponent<MeshRenderer>();
        shedRenderer.sharedMaterial = shedMat;
        shedRenderer.shadowCastingMode = ShadowCastingMode.Off;
        var shed = shedGo.AddComponent<SkinShedEffect>();
        Wire(shed, ("_body", player.Body));

        // ================= Сервисы =================
        var services = new GameObject("Services");
        var leaderboard = services.AddComponent<LeaderboardService>();
        var settings = services.AddComponent<SettingsService>();
        var sfxSource = services.AddComponent<AudioSource>();
        var musicSource = services.AddComponent<AudioSource>();
        var audio = services.AddComponent<AudioService>();
        Wire(audio, ("_settings", settings), ("_sfxSource", sfxSource), ("_musicSource", musicSource));

        // ================= Камеры =================
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        // Слой 0 — Default: если отдельный слой создать не удалось, не прячем им всю сцену.
        cam.cullingMask = minimapLayer > 0 ? ~(1 << minimapLayer) : ~0;
        camGo.AddComponent<AudioListener>();
        EnvironmentBuilder.SetupSky(light, cam);
        var rig = camGo.AddComponent<CameraRig>();
        Wire(rig, ("_snake", player.Controller), ("_grid", grid));

        // Ввод игрока — на самой змее; в меню выключен, змею ведёт автопилот через то же «намерение».
        var playerInput = player.Controller.gameObject.AddComponent<PlayerSnakeInput>();
        Wire(playerInput, ("_snake", player.Controller), ("_camera", rig));
        playerInput.enabled = false;

        var dayNight = new GameObject("DayNight").AddComponent<DayNightCycle>();
        Wire(dayNight, ("_config", config), ("_sun", light), ("_moon", moon), ("_stars", stars), ("_fireflies", pollen), ("_clouds", clouds));

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
            ("_config", config), ("_grid", grid),
            ("_snake", player.Controller), ("_headAnimator", player.Animator), ("_rival", rival.Controller), ("_playerAutopilot", playerAi), ("_playerInput", playerInput),
            ("_wildlife", wildlife), ("_eagle", eagle), ("_dayNight", dayNight), ("_scent", scent), ("_skinShed", shed),
            ("_leaderboard", leaderboard), ("_settings", settings), ("_audio", audio), ("_cameraRig", rig),
            ("_eatParticles", eatFx), ("_deathParticles", deathFx), ("_dustParticles", dustFx), ("_moltParticles", moltFx));

        // ================= UI =================
        UIBuilder.Build(new UIBuilder.Refs
        {
            Game = game, Config = config, Player = player.Controller, Settings = settings, Audio = audio, Leaderboard = leaderboard,
            Wildlife = wildlife, Eagle = eagle, DayNight = dayNight, Scent = scent,
            CameraTransform = camGo.transform, MinimapCamera = miniCam, MinimapTexture = minimapTexture
        });

        // ================= Сохранение =================
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AddSceneToBuildSettings(SCENE_PATH);

        Debug.Log("Snake3D: сцена собрана и сохранена в " + SCENE_PATH + (_urp ? " (URP)" : " (Built-in)"));
    }

    // ===================== Живность =====================

    /// <summary>Пул животных: каждого вида столько, сколько нужно днём или ночью, плюс запас на улетающих птиц.</summary>
    private static PreyAnimal[] BuildPreyPool(Transform parent, GameConfig config, int markerLayer)
    {
        Material beetleShell = CreateLitMaterial("PreyBeetle", new Color(0.08f, 0.32f, 0.28f), 0.92f);
        SetEmission(beetleShell, new Color(0.02f, 0.08f, 0.06f));
        Material dark = CreateLitMaterial("PreyDark", new Color(0.05f, 0.05f, 0.05f), 0.8f);
        Material mouseFur = CreateLitMaterial("PreyMouse", new Color(0.52f, 0.45f, 0.4f), 0.15f);
        Material pink = CreateLitMaterial("PreyPink", new Color(0.9f, 0.6f, 0.6f), 0.3f);
        Material frogSkin = CreateLitMaterial("PreyFrog", new Color(0.32f, 0.62f, 0.18f), 0.75f);
        Material frogBelly = CreateLitMaterial("PreyFrogBelly", new Color(0.85f, 0.85f, 0.5f), 0.5f);
        Material birdFeather = CreateLitMaterial("PreyBird", new Color(0.56f, 0.38f, 0.22f), 0.2f);
        Material birdChest = CreateLitMaterial("PreyBirdChest", new Color(0.86f, 0.74f, 0.58f), 0.2f);
        Material beak = CreateLitMaterial("PreyBeak", new Color(1f, 0.7f, 0.15f), 0.5f);
        Material scent = CreateLitMaterial("ScentMark", new Color(0.8f, 1f, 0.3f, 0.35f), 0f);
        SetEmission(scent, new Color(0.6f, 0.9f, 0.15f));
        MakeTransparent(scent);
        Material marker = CreateUnlitMaterial("MarkerPrey", new Color(1f, 0.92f, 0.55f));

        var list = new List<Object>();
        for (int k = 0; k < 4; k++)
        {
            var kind = (PreyKind)k;
            PreyStats stats = config.GetPrey(kind);
            int count = Mathf.Max(stats.DayCount, stats.NightCount) + (kind == PreyKind.Bird ? 2 : 1);
            for (int i = 0; i < count; i++)
            {
                var root = new GameObject(kind.ToString());
                root.transform.SetParent(parent, false);
                var body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                Transform leftWing = null, rightWing = null;
                switch (kind)
                {
                    case PreyKind.Beetle: BuildBeetle(body, beetleShell, dark); break;
                    case PreyKind.Mouse: BuildMouse(body, mouseFur, pink, dark); break;
                    case PreyKind.Frog: BuildFrog(body, frogSkin, frogBelly, dark); break;
                    default: BuildBird(body, birdFeather, birdChest, beak, dark, out leftWing, out rightWing); break;
                }

                // Метка запаха — светящийся столбик над добычей: виден поверх травы и издалека.
                GameObject mark = CreatePrimitive(PrimitiveType.Cylinder, "ScentMark", root.transform, scent);
                mark.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                mark.transform.localScale = new Vector3(0.1f, 1.3f, 0.1f);
                mark.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                mark.SetActive(false);
                CreateMarker(root.transform, marker, markerLayer, kind == PreyKind.Beetle ? 0.7f : 1.1f);

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject != mark) r.shadowCastingMode = kind == PreyKind.Beetle ? ShadowCastingMode.Off : ShadowCastingMode.On;

                var animal = root.AddComponent<PreyAnimal>();
                SetEnum(animal, "_kind", k);
                Wire(animal, ("_body", body), ("_scentMark", mark));
                if (leftWing != null) Wire(animal, ("_leftWing", leftWing), ("_rightWing", rightWing));
                if (kind == PreyKind.Frog) SetFloat(animal, "_reactionTime", 0.25f);
                if (kind == PreyKind.Bird) SetFloat(animal, "_reactionTime", 0.2f);
                root.SetActive(false);
                list.Add(animal);
            }
        }

        var result = new PreyAnimal[list.Count];
        for (int i = 0; i < result.Length; i++) result[i] = (PreyAnimal)list[i];
        return result;
    }

    private static void BuildBeetle(Transform body, Material shell, Material dark)
    {
        Part(PrimitiveType.Sphere, "Shell", body, shell, new Vector3(0f, 0.13f, 0f), new Vector3(0.3f, 0.17f, 0.4f));
        Part(PrimitiveType.Sphere, "Head", body, dark, new Vector3(0f, 0.1f, 0.21f), new Vector3(0.15f, 0.12f, 0.12f));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int l = 0; l < 3; l++)
            {
                GameObject leg = Part(PrimitiveType.Cube, "Leg", body, dark, new Vector3(0.16f * side, 0.05f, -0.1f + l * 0.1f), new Vector3(0.14f, 0.02f, 0.02f));
                leg.transform.localRotation = Quaternion.Euler(0f, (l - 1) * 25f * side, -20f * side);
            }
        }
    }

    private static void BuildMouse(Transform body, Material fur, Material pink, Material dark)
    {
        Part(PrimitiveType.Sphere, "Torso", body, fur, new Vector3(0f, 0.17f, 0f), new Vector3(0.32f, 0.28f, 0.52f));
        Part(PrimitiveType.Sphere, "Head", body, fur, new Vector3(0f, 0.2f, 0.3f), new Vector3(0.22f, 0.2f, 0.28f));
        Part(PrimitiveType.Sphere, "Nose", body, pink, new Vector3(0f, 0.19f, 0.45f), Vector3.one * 0.05f);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, "Ear", body, pink, new Vector3(0.09f * side, 0.32f, 0.24f), new Vector3(0.12f, 0.12f, 0.03f));
            Part(PrimitiveType.Sphere, "Eye", body, dark, new Vector3(0.075f * side, 0.24f, 0.38f), Vector3.one * 0.045f);
        }
        GameObject tail = Part(PrimitiveType.Cylinder, "Tail", body, pink, new Vector3(0f, 0.09f, -0.45f), new Vector3(0.025f, 0.2f, 0.025f));
        tail.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
    }

    private static void BuildFrog(Transform body, Material skin, Material belly, Material dark)
    {
        Part(PrimitiveType.Sphere, "Torso", body, skin, new Vector3(0f, 0.15f, 0f), new Vector3(0.46f, 0.28f, 0.44f));
        Part(PrimitiveType.Sphere, "Belly", body, belly, new Vector3(0f, 0.1f, 0.04f), new Vector3(0.38f, 0.16f, 0.36f));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, "Eye", body, skin, new Vector3(0.12f * side, 0.29f, 0.12f), Vector3.one * 0.13f);
            Part(PrimitiveType.Sphere, "Pupil", body, dark, new Vector3(0.13f * side, 0.31f, 0.17f), Vector3.one * 0.06f);
            GameObject leg = Part(PrimitiveType.Sphere, "Leg", body, skin, new Vector3(0.22f * side, 0.08f, -0.12f), new Vector3(0.16f, 0.12f, 0.32f));
            leg.transform.localRotation = Quaternion.Euler(0f, 20f * side, 0f);
        }
    }

    private static void BuildBird(Transform body, Material feather, Material chest, Material beak, Material dark,
        out Transform leftWing, out Transform rightWing)
    {
        Part(PrimitiveType.Sphere, "Torso", body, feather, new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.3f, 0.48f));
        Part(PrimitiveType.Sphere, "Chest", body, chest, new Vector3(0f, 0.27f, 0.1f), new Vector3(0.25f, 0.24f, 0.3f));
        Part(PrimitiveType.Sphere, "Head", body, feather, new Vector3(0f, 0.47f, 0.2f), Vector3.one * 0.22f);
        Part(PrimitiveType.Cube, "Beak", body, beak, new Vector3(0f, 0.45f, 0.34f), new Vector3(0.05f, 0.05f, 0.12f));
        GameObject tail = Part(PrimitiveType.Cube, "Tail", body, feather, new Vector3(0f, 0.34f, -0.32f), new Vector3(0.16f, 0.025f, 0.24f));
        tail.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, "Eye", body, dark, new Vector3(0.08f * side, 0.5f, 0.27f), Vector3.one * 0.04f);
            Part(PrimitiveType.Cylinder, "Leg", body, beak, new Vector3(0.06f * side, 0.08f, 0.02f), new Vector3(0.025f, 0.08f, 0.025f));
        }
        leftWing = WingPivot(body, "LeftWing", -1, feather);
        rightWing = WingPivot(body, "RightWing", 1, feather);
    }

    private static Transform WingPivot(Transform body, string name, int side, Material mat)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(body, false);
        pivot.localPosition = new Vector3(0.13f * side, 0.36f, 0f);
        Part(PrimitiveType.Sphere, "Feathers", pivot, mat, new Vector3(0.17f * side, 0f, -0.04f), new Vector3(0.36f, 0.05f, 0.26f));
        return pivot;
    }

    /// <summary>Орёл: модель (тело, белая голова, клюв, хвост, крылья на шарнирах), тень на земле и маркер мини-карты.</summary>
    private static EagleHunter BuildEagle(GameConfig config, GridManager grid, SnakeController target, int markerLayer)
    {
        Material feather = CreateLitMaterial("EagleFeather", new Color(0.3f, 0.2f, 0.12f), 0.15f);
        Material tip = CreateLitMaterial("EagleWingTip", new Color(0.12f, 0.09f, 0.07f), 0.15f);
        Material white = CreateLitMaterial("EagleWhite", new Color(0.95f, 0.94f, 0.9f), 0.2f);
        Material beak = CreateLitMaterial("EagleBeak", new Color(1f, 0.78f, 0.15f), 0.5f);
        Material eye = CreateLitMaterial("SnakePupil", new Color(0.02f, 0.02f, 0.02f), 0.9f);

        var root = new GameObject("Eagle");
        var model = new GameObject("EagleModel").transform;
        model.SetParent(root.transform, false);
        Part(PrimitiveType.Sphere, "Body", model, feather, Vector3.zero, new Vector3(0.6f, 0.55f, 1.4f));
        Part(PrimitiveType.Sphere, "Head", model, white, new Vector3(0f, 0.16f, 0.78f), Vector3.one * 0.44f);
        Part(PrimitiveType.Sphere, "Beak", model, beak, new Vector3(0f, 0.08f, 1.04f), new Vector3(0.14f, 0.14f, 0.28f));
        for (int side = -1; side <= 1; side += 2)
            Part(PrimitiveType.Sphere, "Eye", model, eye, new Vector3(0.15f * side, 0.24f, 0.92f), Vector3.one * 0.07f);
        Part(PrimitiveType.Cube, "Tail", model, white, new Vector3(0f, 0f, -0.92f), new Vector3(0.7f, 0.05f, 0.62f));

        Transform[] wings = new Transform[2];
        for (int w = 0; w < 2; w++)
        {
            int side = w == 0 ? -1 : 1;
            var pivot = new GameObject(side < 0 ? "LeftWing" : "RightWing").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0.24f * side, 0.1f, 0.1f);
            Part(PrimitiveType.Cube, "Wing", pivot, feather, new Vector3(1.05f * side, 0f, -0.05f), new Vector3(2.1f, 0.07f, 0.8f));
            GameObject end = Part(PrimitiveType.Cube, "WingTip", pivot, tip, new Vector3(2.35f * side, 0f, -0.18f), new Vector3(0.7f, 0.05f, 0.55f));
            end.transform.localRotation = Quaternion.Euler(0f, -12f * side, 0f);
            wings[w] = pivot;
        }
        Material redMarker = CreateUnlitMaterial("MarkerEagle", new Color(1f, 0.25f, 0.2f));
        CreateMarker(model, redMarker, markerLayer, 3f);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.layer != markerLayer) r.shadowCastingMode = ShadowCastingMode.Off;

        // Тень — мягкое пятно на земле: при высоком полёте настоящая тень почти не видна.
        Material shadowMat = CreateLitMaterial("EagleShadow", Color.black, 0f);
        shadowMat.mainTexture = SaveTexture(CreateBlobTexture(), MATERIALS_FOLDER + "/SoftBlob.asset");
        MakeTransparent(shadowMat);
        GameObject shadow = CreatePrimitive(PrimitiveType.Quad, "EagleShadow", root.transform, shadowMat);
        var shadowRenderer = shadow.GetComponent<Renderer>();
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;

        var eagle = root.AddComponent<EagleHunter>();
        Wire(eagle, ("_config", config), ("_grid", grid), ("_target", target), ("_model", model),
            ("_leftWing", wings[0]), ("_rightWing", wings[1]), ("_shadow", shadowRenderer));
        return eagle;
    }

    /// <summary>Мягкое белое пятно с плавным краем (тень орла).</summary>
    private static Texture2D CreateBlobTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftBlob", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        }
        tex.Apply();
        return tex;
    }

    private static GameObject Part(PrimitiveType type, string name, Transform parent, Material mat, Vector3 localPosition, Vector3 localScale)
    {
        GameObject go = CreatePrimitive(type, name, parent, mat);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        return go;
    }

    // ===================== Змея =====================

    private static SnakeParts BuildSnake(string name, SnakeLook look, bool isPlayer, float speedScale, GameConfig config,
        GridManager grid, ParticleSystem pieces, Material trailMaterial, int markerLayer)
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

        Wire(snake, ("_config", config), ("_grid", grid), ("_head", head));
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

        return new SnakeParts { Controller = snake, Animator = animator, Body = bodyMesh };
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

    // ===================== Объекты =====================

    internal static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat)
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
    internal static Texture2D SaveTexture(Texture2D tex, string path)
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

    internal static Material CreateLitMaterial(string name, Color color, float smoothness)
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

    internal static void SetEmission(Material mat, Color emission)
    {
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Полупрозрачный режим для Standard (Fade) или URP Lit (Transparent).</summary>
    internal static void MakeTransparent(Material mat)
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
    internal static Material LoadOrCreateMaterial(string name, Shader shader)
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

        // Текстуры неонового меню и старый мрамор колонн больше не используются.
        foreach (string name in new[] { "MenuSky.asset", "MenuSun.asset", "MenuMountains.asset", "MenuGrid.asset",
                     "Rounded.png", "Pill.png", "Circle.png", "SoftShadow.png" })
        {
            string path = "Assets/Materials/UI/" + name;
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        }

        // Шахматная текстура пола заменена травой из EnvironmentBuilder.
        const string checkerPath = MATERIALS_FOLDER + "/FloorChecker.asset";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(checkerPath) != null) AssetDatabase.DeleteAsset(checkerPath);
    }

    // ===================== Утилиты =====================

    /// <summary>Записываем приватные [SerializeField] поля через SerializedObject.</summary>
    internal static void Wire(Object target, params (string field, Object value)[] links)
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

    internal static void WireArray(Object target, string field, Object[] values)
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

    internal static void SetInt(Object target, string field, int value)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string field, int index)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = FindForWrite(target, field, out SerializedProperty prop);
        if (prop == null) return;
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetColor(Object target, string field, Color value)
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
