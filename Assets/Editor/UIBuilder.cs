using WildSnake;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
// В UnityEditor есть свой SettingsService — явно выбираем игровой.
using SettingsService = WildSnake.SettingsService;

/// <summary>
/// Интерфейс игры в едином стиле: крупная типографика (Bahnschrift), чернильный фон с затемнением
/// слева поверх живой сцены, тёплый «костяной» текст и единственный акцент — ядовитый лайм.
/// Никаких «карточек»: экраны — композиции из заголовка, нумерованных строк, тонких линий
/// и клавиш-подсказок. HUD выживания в той же гамме. Вызывается из SnakeSceneBuilder.
/// </summary>
internal static class UIBuilder
{
    /// <summary>Ссылки на игровые системы, с которыми связывается UI.</summary>
    public sealed class Refs
    {
        public GameManager Game;
        public GameConfig Config;
        public SnakeController Player;
        public SettingsService Settings;
        public AudioService Audio;
        public LeaderboardService Leaderboard;
        public WildlifeManager Wildlife;
        public EagleHunter Eagle;
        public FoxHunter Fox;
        public GridManager Grid;
        public WeatherSystem Weather;
        public DayNightCycle DayNight;
        public ScentTracker Scent;
        public BirdNests BirdNests;
        public WaspNests Wasps;
        public Transform CameraTransform;
        public CameraRig Rig;
        public SnakeController Rival;
    }

    // ---------- Палитра ----------
    private static readonly Color Ink = new Color(0.043f, 0.055f, 0.07f);
    private static readonly Color Bone = new Color(0.95f, 0.93f, 0.88f);
    private static readonly Color Ash = new Color(0.62f, 0.63f, 0.6f);
    private static readonly Color Smoke = new Color(0.46f, 0.48f, 0.48f);
    private static readonly Color Venom = new Color(0.78f, 0.96f, 0.2f);
    private static readonly Color Sky = new Color(0.45f, 0.75f, 1f);
    private static readonly Color Ember = new Color(1f, 0.42f, 0.3f);
    private static readonly Color Hair = new Color(0.95f, 0.93f, 0.88f, 0.14f);
    private static readonly Color HudCaption = new Color(0.95f, 0.93f, 0.88f, 0.78f);

    // ---------- Цвета значков карты ----------
    private static readonly Color MapRival = new Color(1f, 0.55f, 0.15f);
    private static readonly Color MapPrey = new Color(1f, 0.9f, 0.55f);
    private static readonly Color MapFish = new Color(0.72f, 0.92f, 1f);
    private static readonly Color MapFox = new Color(1f, 0.5f, 0.12f);
    private static readonly Color MapSun = new Color(1f, 0.78f, 0.3f);
    private static readonly Color MapSpring = new Color(0.5f, 0.92f, 0.9f);
    private static readonly Color MapColony = new Color(0.86f, 0.74f, 0.58f);
    private static readonly Color MapWasp = new Color(1f, 0.82f, 0.15f);
    private static readonly Color MapBramble = new Color(0.52f, 0.36f, 0.42f);

    private const float LEFT = 96f;
    /// <summary>Радиус круга мини-карты, пикселей интерфейса.</summary>
    private const float MINIMAP_RADIUS = 125f;

    private static Font _font;
    private static Sprite _chamfer;
    private static Sprite _keycap;
    private static Sprite _frame;
    private static Sprite _solid;

    /// <summary>Пункт списка, собранный MakeRow.</summary>
    private struct Row
    {
        public Button Button;
        public RectTransform Content;
        public GameObject Accent;
        public Text Label;
        public Text Index;
        public Text Description;
        public Text Record;
    }

    public static void Build(Refs r)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _chamfer = UISpriteFactory.Chamfer;
        _keycap = UISpriteFactory.ChamferOutline;
        _frame = UISpriteFactory.Frame;
        _solid = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Texture2D menuShade = UISpriteFactory.Vignette("ShadeMenu", 0.94f, 0.78f, 0f);
        Texture2D pauseShade = UISpriteFactory.Vignette("ShadePause", 0.94f, 0.82f, 0.4f);
        // Настройки, рекорды и справочник открываются поверх меню/паузы — слева фон непрозрачный, иначе просвечивают их надписи.
        Texture2D deepShade = UISpriteFactory.Vignette("ShadeDeep", 1f, 1f, 0.72f);
        Texture2D edge = UISpriteFactory.EdgeVignette();

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

        // Градиенты краёв создаём один раз: повторное сохранение ассета обнулило бы уже выданные ссылки.
        Texture2D edgeH = UISpriteFactory.EdgeGradient(false);
        Texture2D edgeV = UISpriteFactory.EdgeGradient(true);
        GameObject stateLayer = BuildOverlays(root, r, edge, edgeH, edgeV, deepShade, out OverlayParts op2, out GameObject mapRoot);
        Texture2D cornerShade = UISpriteFactory.CornerShade();
        GameObject hud = BuildHud(root, r, cornerShade, out HudParts hp);
        GameObject menu = BuildMainMenu(root, menuShade, out Row[] menuRows, out CanvasGroup menuGroup);
        GameObject pause = BuildPause(root, pauseShade, out Row[] pauseRows, out CanvasGroup pauseGroup);
        GameObject molt = BuildMolt(root, pauseShade, out Row[] moltRows, out CanvasGroup moltGroup);
        GameObject over = BuildGameOver(root, pauseShade, out Row[] overRows, out CanvasGroup overGroup, out GameOverParts op);
        GameObject settings = BuildSettings(root, deepShade, out SettingsParts sp);
        GameObject board = BuildLeaderboard(root, deepShade, out Object[] columns, out Text empty, out Button boardBack);
        GameObject guide = BuildGuide(root, deepShade, out Button guideBack, out Button guideWatch);
        // Видео-обучение — поверх всего меню: открывается из настроек и из справочника.
        GameObject tutorial = BuildTutorial(root, deepShade, out TutorialParts tp);

        // Карта по Tab — поверх HUD; во время партии остальные экраны скрыты, так что выше неё ничего нет.
        mapRoot.transform.SetAsLastSibling();
        pause.SetActive(false);
        molt.SetActive(false);
        over.SetActive(false);
        settings.SetActive(false);
        board.SetActive(false);
        guide.SetActive(false);
        tutorial.SetActive(false);

        // ---------- Компоненты ----------
        canvasGo.AddComponent<UIFontApplier>();
        var overlays = new Object[] { settings, board, guide, tutorial };

        WireList(menu, menuRows, menuGroup, r, overlays, 0);
        WireList(pause, pauseRows, pauseGroup, r, overlays, 0);
        WireList(molt, moltRows, moltGroup, r, overlays, 0);
        WireList(over, overRows, overGroup, r, overlays, 0);

        // ---------- HUD ----------
        var vitals = hud.AddComponent<HudVitals>();
        SnakeSceneBuilder.Wire(vitals, ("_game", r.Game), ("_snake", r.Player), ("_group", hp.VitalsGroup));
        SnakeSceneBuilder.WireArray(vitals, "_fills", hp.VitalFills);
        SnakeSceneBuilder.WireArray(vitals, "_arcs", hp.VitalArcs);
        SnakeSceneBuilder.WireArray(vitals, "_labels", hp.VitalLabels);
        SnakeSceneBuilder.WireArray(vitals, "_values", hp.VitalValues);

        var objective = hud.AddComponent<HudObjective>();
        SnakeSceneBuilder.Wire(objective, ("_game", r.Game), ("_dayNight", r.DayNight), ("_text", hp.ObjectiveText), ("_progress", hp.ObjectiveFill),
            ("_moltFill", hp.MoltFill), ("_moltValue", hp.MoltValue));

        var status = hud.AddComponent<HudStatus>();
        SnakeSceneBuilder.Wire(status, ("_game", r.Game), ("_snake", r.Player), ("_weather", r.Weather));
        SnakeSceneBuilder.WireArray(status, "_lines", hp.StatusLines);

        var dial = hud.AddComponent<SunDial>();
        SnakeSceneBuilder.Wire(dial, ("_dayNight", r.DayNight), ("_body", hp.SunBody), ("_bodyImage", hp.SunBodyImage), ("_progress", hp.SunProgress),
            ("_label", hp.DayLabel));

        var hints = canvasGo.AddComponent<ContextHints>();
        SnakeSceneBuilder.Wire(hints, ("_game", r.Game), ("_config", r.Config), ("_snake", r.Player), ("_wildlife", r.Wildlife),
            ("_grid", r.Grid), ("_eagle", r.Eagle), ("_fox", r.Fox), ("_scent", r.Scent), ("_birdNests", r.BirdNests), ("_wasps", r.Wasps),
            ("_group", hp.HintGroup), ("_keycap", hp.HintKeycap), ("_keyText", hp.HintKey), ("_label", hp.HintLabel));

        var threat = hud.AddComponent<ThreatIndicator>();
        SnakeSceneBuilder.Wire(threat, ("_game", r.Game), ("_snake", r.Player), ("_eagle", r.Eagle), ("_fox", r.Fox),
            ("_camera", r.CameraTransform), ("_eagleMarker", hp.EagleMarker), ("_foxMarker", hp.FoxMarker),
            ("_alertGroup", hp.AlertGroup), ("_alertText", hp.AlertText));

        var reticle = hud.AddComponent<CenterReticle>();
        SnakeSceneBuilder.Wire(reticle, ("_game", r.Game), ("_snake", r.Player), ("_aimGroup", hp.AimGroup), ("_chargeFill", hp.ChargeFill),
            ("_struggleGroup", hp.StruggleGroup), ("_struggleFill", hp.StruggleFill));

        var vignettes = stateLayer.AddComponent<ScreenVignettes>();
        SnakeSceneBuilder.Wire(vignettes, ("_game", r.Game), ("_snake", r.Player), ("_camera", r.CameraTransform),
            ("_lowHealth", op2.LowHealth), ("_frost", op2.Frost), ("_warmth", op2.Warmth), ("_ambush", op2.Ambush));
        SnakeSceneBuilder.WireArray(vignettes, "_damageEdges", op2.DamageEdges);

        var journal = mapRoot.AddComponent<MapJournal>();
        SnakeSceneBuilder.Wire(journal, ("_game", r.Game), ("_dayNight", r.DayNight), ("_hud", hp.HudGroup),
            ("_group", op2.MapGroup), ("_dayText", op2.MapDay), ("_summaryText", op2.Summary),
            ("_questText", op2.QuestText), ("_questNote", op2.QuestNote));
        SnakeSceneBuilder.WireArray(journal, "_goalLabels", op2.GoalLabels);
        SnakeSceneBuilder.WireArray(journal, "_goalNotes", op2.GoalNotes);
        SnakeSceneBuilder.WireArray(journal, "_goalChecks", op2.GoalChecks);
        SnakeSceneBuilder.WireArray(journal, "_goalIndexes", op2.GoalIndexes);

        var mapIcons = mapRoot.AddComponent<MapIcons>();
        WireMapIcons(mapIcons, r, op2.MapIcons, op2.MapGroup);

        // Мини-карта в HUD: та же подложка (берётся у карты журнала), окно вокруг змеи по камере.
        var minimap = hp.MinimapGroup.gameObject.AddComponent<MapIcons>();
        WireMapIcons(minimap, r, hp.Minimap, hp.MinimapGroup);
        SnakeSceneBuilder.Wire(minimap, ("_textureSource", mapIcons), ("_camera", r.Rig), ("_rotator", hp.MinimapRotator),
            ("_north", hp.MinimapNorth), ("_settings", r.Settings));
        SnakeSceneBuilder.SetBool(minimap, "_follow", true);
        SnakeSceneBuilder.SetFloat(minimap, "_windowRadius", MINIMAP_RADIUS);
        SnakeSceneBuilder.SetFloat(minimap, "_preySize", 14f);
        SnakeSceneBuilder.SetFloat(minimap, "_beetleSize", 10f);

        var settingsComp = settings.AddComponent<SettingsPanel>();
        SnakeSceneBuilder.Wire(settingsComp, ("_settings", r.Settings), ("_audio", r.Audio),
            ("_masterSlider", sp.Master), ("_musicSlider", sp.Music), ("_sfxSlider", sp.Sfx), ("_mouseSlider", sp.Mouse), ("_invertYToggle", sp.InvertY),
            ("_fullscreenToggle", sp.Fullscreen), ("_hudToggle", sp.Hud), ("_rivalToggle", sp.Rival), ("_minimapToggle", sp.Minimap),
            ("_qualityButton", sp.Quality), ("_qualityLabel", sp.QualityLabel), ("_backButton", sp.Back));

        var boardComp = board.AddComponent<LeaderboardPanel>();
        SnakeSceneBuilder.Wire(boardComp, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio), ("_config", r.Config),
            ("_emptyText", empty), ("_backButton", boardBack));
        SnakeSceneBuilder.WireArray(boardComp, "_columns", columns);

        var guideComp = guide.AddComponent<GuidePanel>();
        SnakeSceneBuilder.Wire(guideComp, ("_audio", r.Audio), ("_backButton", guideBack), ("_watchButton", guideWatch), ("_tutorialPanel", tutorial));

        var player = tutorial.AddComponent<TutorialPlayer>();
        SnakeSceneBuilder.Wire(player, ("_audio", r.Audio), ("_canvas", tp.Canvas), ("_chapterTitle", tp.Title), ("_chapterIndex", tp.Index),
            ("_caption", tp.Caption), ("_clock", tp.Clock), ("_playLabel", tp.PlayLabel), ("_screenButton", tp.Screen),
            ("_playButton", tp.Play), ("_prevButton", tp.Prev), ("_nextButton", tp.Next), ("_backButton", tp.Back));
        SnakeSceneBuilder.WireArray(player, "_segmentFills", tp.Fills);
        SnakeSceneBuilder.WireArray(player, "_chapterButtons", tp.ChapterButtons);
        SnakeSceneBuilder.WireArray(player, "_chapterLabels", tp.ChapterLabels);
        SnakeSceneBuilder.WireArray(tp.Canvas, "_labels", tp.Labels);
        SnakeSceneBuilder.WireArray(tp.Canvas, "_keys", tp.Keys);
        SnakeSceneBuilder.WireArray(tp.Canvas, "_keyFrames", tp.KeyFrames);
        SnakeSceneBuilder.WireArray(tp.Canvas, "_keyTexts", tp.KeyTexts);

        var ui = canvasGo.AddComponent<UIController>();
        SnakeSceneBuilder.Wire(ui,
            ("_game", r.Game), ("_settings", r.Settings), ("_audio", r.Audio), ("_leaderboard", r.Leaderboard),
            ("_eagle", r.Eagle), ("_dayNight", r.DayNight), ("_scent", r.Scent),
            ("_hudRoot", hud), ("_vignetteRoot", stateLayer), ("_bannerGroup", hp.BannerGroup), ("_bannerText", hp.BannerText),
            ("_menuPanel", menu), ("_pausePanel", pause), ("_moltPanel", molt), ("_gameOverPanel", over),
            ("_settingsPanel", settings), ("_leaderboardPanel", board), ("_guidePanel", guide), ("_tutorialPanel", tutorial),
            ("_gameOverTitle", op.Title), ("_daysText", op.Days), ("_rankText", op.Rank),
            ("_playButton", menuRows[0].Button), ("_playLabel", menuRows[0].Label), ("_playDescription", menuRows[0].Description),
            ("_guideButton", menuRows[1].Button),
            ("_menuLeaderboardButton", menuRows[2].Button), ("_menuSettingsButton", menuRows[3].Button), ("_quitButton", menuRows[4].Button),
            ("_resumeButton", pauseRows[0].Button), ("_pauseRestartButton", pauseRows[1].Button),
            ("_pauseSettingsButton", pauseRows[2].Button), ("_pauseMenuButton", pauseRows[3].Button),
            ("_restartButton", overRows[0].Button), ("_gameOverMenuButton", overRows[1].Button));
        SnakeSceneBuilder.WireArray(ui, "_statValues", op.Stats);

        var moltButtons = new Object[moltRows.Length];
        var moltLabels = new Object[moltRows.Length];
        var moltDescriptions = new Object[moltRows.Length];
        var moltLevels = new Object[moltRows.Length];
        for (int i = 0; i < moltRows.Length; i++)
        {
            moltButtons[i] = moltRows[i].Button;
            moltLabels[i] = moltRows[i].Label;
            moltDescriptions[i] = moltRows[i].Description;
            moltLevels[i] = moltRows[i].Record;
        }
        SnakeSceneBuilder.WireArray(ui, "_moltButtons", moltButtons);
        SnakeSceneBuilder.WireArray(ui, "_moltLabels", moltLabels);
        SnakeSceneBuilder.WireArray(ui, "_moltDescriptions", moltDescriptions);
        SnakeSceneBuilder.WireArray(ui, "_moltLevels", moltLevels);
    }

    private static void WireList(GameObject screen, Row[] rows, CanvasGroup group, Refs r, Object[] overlays, int recordCount)
    {
        var list = screen.AddComponent<MenuList>();
        var buttons = new Object[rows.Length];
        var contents = new Object[rows.Length];
        var accents = new Object[rows.Length];
        var labels = new Object[rows.Length];
        var indexes = new Object[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            buttons[i] = rows[i].Button;
            contents[i] = rows[i].Content;
            accents[i] = rows[i].Accent;
            labels[i] = rows[i].Label;
            indexes[i] = rows[i].Index;
        }
        var records = new Object[recordCount];
        for (int i = 0; i < recordCount; i++) records[i] = rows[i].Record;

        SnakeSceneBuilder.Wire(list, ("_audio", r.Audio), ("_leaderboard", r.Leaderboard), ("_config", r.Config), ("_group", group));
        SnakeSceneBuilder.WireArray(list, "_items", buttons);
        SnakeSceneBuilder.WireArray(list, "_contents", contents);
        SnakeSceneBuilder.WireArray(list, "_accents", accents);
        SnakeSceneBuilder.WireArray(list, "_labels", labels);
        SnakeSceneBuilder.WireArray(list, "_indexes", indexes);
        SnakeSceneBuilder.WireArray(list, "_records", records);
        SnakeSceneBuilder.WireArray(list, "_overlays", overlays);
    }

    // =====================================================================
    // Главное меню
    // =====================================================================

    private static GameObject BuildMainMenu(Transform root, Texture2D shade, out Row[] rows, out CanvasGroup group)
    {
        RectTransform screen = Screen(root, "MenuPanel", shade, out group);

        // Только название и пять строк — без слоганов, описаний и подсказок.
        RectTransform tag = Rect("Tag", screen);
        TopLeft(tag, LEFT, -112f, 150f, 30f);
        Img(tag, Venom, _chamfer);
        Text tagText = Label(tag, "Text", "ВЫЖИВАНИЕ", 17, Ink, TextAnchor.MiddleCenter, true);
        Stretch(tagText.rectTransform);

        TopLeft(Label(screen, "Title", "WILD SNAKE", 128, Bone, TextAnchor.MiddleLeft, true), LEFT - 10f, -142f, 1000f, 200f);
        TopLeft(Line(screen, Venom), LEFT, -350f, 72f, 5f);

        string[] items = { "ИГРАТЬ", "СПРАВОЧНИК", "РЕКОРДЫ", "НАСТРОЙКИ", "ВЫХОД" };
        rows = new Row[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            // Под «Играть» — место для строки о сохранении, поэтому дальше строки чуть ниже.
            float y = -410f - i * 88f - (i > 0 ? 26f : 0f);
            rows[i] = MakeRow(screen, i, items[i], null, LEFT, y, 620f, 76f, 44, i == items.Length - 1 ? Ember : Venom, false);
        }
        // Есть сохранение — мелко «день N»; без сохранения строка пустая.
        rows[0].Description = Label(rows[0].Content, "Description", "", 20, Smoke, TextAnchor.MiddleLeft, false);
        Anchor(rows[0].Description.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, -46f), new Vector2(560f, 26f));
        return screen.gameObject;
    }

    // =====================================================================
    // Пауза
    // =====================================================================

    private static GameObject BuildPause(Transform root, Texture2D shade, out Row[] rows, out CanvasGroup group)
    {
        RectTransform screen = Screen(root, "PausePanel", shade, out group);
        Heading(screen, "ИГРА ОСТАНОВЛЕНА", "ПАУЗА", 150);

        string[] items = { "ПРОДОЛЖИТЬ", "ЗАНОВО", "НАСТРОЙКИ", "В ГЛАВНОЕ МЕНЮ" };
        rows = new Row[items.Length];
        for (int i = 0; i < items.Length; i++)
            rows[i] = MakeRow(screen, i, items[i], null, LEFT, -420f - i * 88f, 620f, 76f, 44, Venom, false);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("ESC", "продолжить"), ("R", "заново"), ("↑ ↓", "выбор"));
        return screen.gameObject;
    }

    // =====================================================================
    // Линька
    // =====================================================================

    /// <summary>Выбор улучшения: три нумерованные строки, названия и описания подставляет UIController.</summary>
    private static GameObject BuildMolt(Transform root, Texture2D shade, out Row[] rows, out CanvasGroup group)
    {
        RectTransform screen = Screen(root, "MoltPanel", shade, out group);
        Heading(screen, "ЛИНЬКА", "НОВАЯ КОЖА", 130);
        TopLeft(Label(screen, "Subtitle", "Раны затянулись. Выберите, какой станет змея.", 24, Ash, TextAnchor.MiddleLeft, false),
            LEFT, -300f, 900f, 34f);

        rows = new Row[GameManager.MOLT_CHOICES];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = MakeRow(screen, i, "УЛУЧШЕНИЕ", "Описание", LEFT, -380f - i * 108f, 700f, 96f, 46, Venom, true);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("↑ ↓", "выбор"), ("ENTER", "взять"));
        return screen.gameObject;
    }

    // =====================================================================
    // Итоги жизни
    // =====================================================================

    private sealed class GameOverParts
    {
        public Text Title, Days, Rank;
        public Object[] Stats;
    }

    private static GameObject BuildGameOver(Transform root, Texture2D shade, out Row[] rows, out CanvasGroup group, out GameOverParts p)
    {
        p = new GameOverParts { Stats = new Object[5] };
        RectTransform screen = Screen(root, "GameOverPanel", shade, out group);

        p.Rank = Label(screen, "Rank", "", 20, Venom, TextAnchor.MiddleLeft, true);
        TopLeft(p.Rank, LEFT, -92f, 800f, 30f);
        p.Title = Label(screen, "Title", "СМЕРТЕЛЬНАЯ РАНА", 60, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Title, LEFT - 4f, -126f, 1200f, 76f);
        p.Days = Label(screen, "Days", "0,0", 220, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Days, LEFT - 14f, -186f, 1000f, 250f);
        TopLeft(Label(screen, "DaysCaption", Spaced("СУТОК ПРОЖИТО"), 20, Smoke, TextAnchor.MiddleLeft, true), LEFT, -436f, 500f, 28f);

        TopLeft(Line(screen, Hair), LEFT, -486f, 880f, 1f);
        string[] statNames = { "ДОБЫЧА", "ПОКОЛЕНИЕ", "ЛИНЬКИ", "ХИЩНИКИ", "ВРЕМЯ" };
        for (int i = 0; i < statNames.Length; i++)
        {
            float x = LEFT + i * 176f;
            TopLeft(Label(screen, "StatLabel_" + i, Spaced(statNames[i]), 17, Smoke, TextAnchor.MiddleLeft, true), x, -506f, 170f, 24f);
            Text value = Label(screen, "StatValue_" + i, "0", 44, Bone, TextAnchor.MiddleLeft, true);
            TopLeft(value, x - 2f, -532f, 170f, 56f);
            p.Stats[i] = value;
        }
        TopLeft(Label(screen, "EagleNote", "«Хищники» — сколько атак орла пережито и сколько раз лиса отпугнута", 18, Smoke, TextAnchor.MiddleLeft, false),
            LEFT, -596f, 880f, 24f);

        rows = new Row[2];
        rows[0] = MakeRow(screen, 0, "ЕЩЁ РАЗ", null, LEFT, -650f, 620f, 76f, 44, Venom, false);
        rows[1] = MakeRow(screen, 1, "В ГЛАВНОЕ МЕНЮ", null, LEFT, -738f, 620f, 76f, 44, Venom, false);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("R", "ещё раз"), ("ENTER", "выбрать"));
        return screen.gameObject;
    }

    // =====================================================================
    // Настройки
    // =====================================================================

    private sealed class SettingsParts
    {
        public Slider Master, Music, Sfx, Mouse;
        public Toggle Fullscreen, Hud, Rival, InvertY, Minimap;
        public Button Quality, Back;
        public Text QualityLabel;
    }

    private static GameObject BuildSettings(Transform root, Texture2D shade, out SettingsParts p)
    {
        p = new SettingsParts();
        RectTransform screen = Screen(root, "SettingsPanel", shade, out _);
        Heading(screen, "ПАРАМЕТРЫ", "НАСТРОЙКИ", 110);

        const float right = LEFT + 900f;
        Section(screen, "ЗВУК", -300f);
        p.Master = SliderAt(screen, SettingRow(screen, "Общая громкость", -324f), right, 0f, 1f);
        p.Music = SliderAt(screen, SettingRow(screen, "Музыка", -376f), right, 0f, 1f);
        p.Sfx = SliderAt(screen, SettingRow(screen, "Звуки", -428f), right, 0f, 1f);

        Section(screen, "УПРАВЛЕНИЕ", -500f);
        p.Mouse = SliderAt(screen, SettingRow(screen, "Чувствительность мыши", -524f), right, 0.2f, 3f);
        p.InvertY = SwitchAt(screen, SettingRow(screen, "Инверсия оси Y", -576f), right);

        Section(screen, "ЭКРАН И ИГРА", -648f);
        p.Fullscreen = SwitchAt(screen, SettingRow(screen, "Полный экран   <size=18><color=#757A7A>F11</color></size>", -672f), right);
        p.Hud = SwitchAt(screen, SettingRow(screen, "Интерфейс в игре   <size=18><color=#757A7A>H</color></size>", -724f), right);
        p.Minimap = SwitchAt(screen, SettingRow(screen, "Мини-карта", -776f), right);
        p.Rival = SwitchAt(screen, SettingRow(screen, "Змея-соперница   <size=18><color=#757A7A>со следующей жизни</color></size>", -828f), right);

        float qualityY = SettingRow(screen, "Качество графики", -880f);
        p.Quality = LinkButton(screen, "Quality", "ULTRA   ›", 24, out p.QualityLabel);
        Anchor((RectTransform)p.Quality.transform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(right, qualityY), new Vector2(300f, 44f));
        p.QualityLabel.alignment = TextAnchor.MiddleRight;

        p.Back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(p.Back, LEFT, -970f, 220f, 60f);
        return screen.gameObject;
    }

    private static void Section(RectTransform screen, string text, float y)
    {
        TopLeft(Label(screen, "Section", Spaced(text), 17, Venom, TextAnchor.MiddleLeft, true), LEFT, y, 600f, 24f);
    }

    /// <summary>Строка настройки: подпись слева и тонкая линия снизу. Возвращает Y центра строки.</summary>
    private static float SettingRow(RectTransform screen, string label, float top)
    {
        const float height = 58f;
        float center = top - height * 0.5f;
        Text t = Label(screen, "Row", label, 24, Bone, TextAnchor.MiddleLeft, false);
        Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(LEFT, center), new Vector2(620f, 40f));
        TopLeft(Line(screen, Hair), LEFT, top - height, 900f, 1f);
        return center;
    }

    /// <summary>Слайдер: волосяная дорожка, лаймовая заливка и квадратная ручка.</summary>
    private static Slider SliderAt(RectTransform screen, float y, float right, float min, float max)
    {
        RectTransform rt = Rect("Slider", screen);
        Anchor(rt, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(right, y), new Vector2(320f, 24f));
        var slider = rt.gameObject.AddComponent<Slider>();

        // Невидимая область нажатия на всю дорожку с запасом по высоте: клик в любом месте сразу ставит значение,
        // тащить квадратную ручку не обязательно (нажатие «всплывает» к Slider).
        RectTransform hitArea = Rect("HitArea", rt);
        Stretch(hitArea);
        hitArea.offsetMin = new Vector2(-10f, -12f);
        hitArea.offsetMax = new Vector2(10f, 12f);
        Img(hitArea, new Color(0f, 0f, 0f, 0f), null).raycastTarget = true;

        RectTransform track = Rect("Background", rt);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 2f);
        Img(track, new Color(Bone.r, Bone.g, Bone.b, 0.2f), null);

        RectTransform fillArea = Rect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-8f, 2f);
        RectTransform fill = Rect("Fill", fillArea);
        fill.sizeDelta = new Vector2(8f, 0f);
        Img(fill, Venom, null);

        RectTransform handleArea = Rect("Handle Slide Area", rt);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);
        RectTransform handle = Rect("Handle", handleArea);
        // Slider растягивает ручку на высоту родителя (24): «-8» даёт квадрат 16x16.
        handle.sizeDelta = new Vector2(16f, -8f);
        Image handleImg = Img(handle, Venom, null);
        handleImg.raycastTarget = true;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = max;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }

    /// <summary>Прямоугольный переключатель: лаймовая дорожка «вкл», серая «выкл», светлый квадрат.</summary>
    private static Toggle SwitchAt(RectTransform screen, float y, float right)
    {
        RectTransform rt = Rect("Switch", screen);
        Anchor(rt, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(right, y), new Vector2(54f, 26f));
        Image track = Img(rt, Venom, null);
        track.raycastTarget = true;

        RectTransform knob = Rect("Knob", rt);
        Anchor(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        Img(knob, Ink, null);

        var toggle = rt.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.isOn = true;

        var view = rt.gameObject.AddComponent<ToggleSwitch>();
        SnakeSceneBuilder.Wire(view, ("_track", track), ("_knob", knob));
        SnakeSceneBuilder.SetFloat(view, "_knobTravel", 14f);
        SnakeSceneBuilder.SetColor(view, "_onColor", Venom);
        SnakeSceneBuilder.SetColor(view, "_offColor", new Color(Bone.r, Bone.g, Bone.b, 0.3f));
        return toggle;
    }

    // =====================================================================
    // Рекорды
    // =====================================================================

    /// <summary>Одна таблица: колонки одного кегля, чтобы строки совпадали по высоте.</summary>
    private static GameObject BuildLeaderboard(Transform root, Texture2D shade, out Object[] columns, out Text empty, out Button back)
    {
        RectTransform screen = Screen(root, "LeaderboardPanel", shade, out _);
        Heading(screen, "САМЫЕ ДОЛГИЕ ЖИЗНИ", "РЕКОРДЫ", 110);

        string[] headers = { "МЕСТО", "СУТОК", "ДОБЫЧА", "ДЛИНА", "ЛИНЬКИ" };
        float[] x = { 0f, 150f, 400f, 620f, 860f };
        Color[] colors = { Smoke, Bone, Ash, Ash, Ash };
        columns = new Object[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            TopLeft(Label(screen, "Header_" + i, Spaced(headers[i]), 17, i == 1 ? Venom : Smoke, TextAnchor.MiddleLeft, true), LEFT + x[i], -300f, 220f, 24f);
            Text rows = Label(screen, "Column_" + i, "", 26, colors[i], TextAnchor.UpperLeft, i == 1);
            rows.lineSpacing = 1.45f;
            TopLeft(rows, LEFT + x[i], -350f, 240f, 520f);
            columns[i] = rows;
        }
        TopLeft(Line(screen, Hair), LEFT, -334f, 1000f, 1f);

        empty = Label(screen, "Empty", "Пока пусто — проживите первый день", 26, Ash, TextAnchor.MiddleLeft, false);
        TopLeft(empty, LEFT, -350f, 900f, 40f);

        back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(back, LEFT, -918f, 220f, 60f);
        return screen.gameObject;
    }

    // =====================================================================
    // Справочник
    // =====================================================================

    private static GameObject BuildGuide(Transform root, Texture2D shade, out Button back, out Button watch)
    {
        RectTransform screen = Screen(root, "GuidePanel", shade, out _);
        Heading(screen, "КАК ВЫЖИТЬ", "СПРАВОЧНИК", 110);

        string[] titles = { "ОХОТА", "ЗАСАДА", "ОПАСНОСТИ", "РОД" };
        Color[] accents = { Venom, Venom, Ember, Sky };
        string[] bodies =
        {
            "WASD — ползти, мышь — камера. CTRL — красться, SHIFT — быстрее.\n\nЖука хватит коснуться. Мышь, лягушку, птицу и рыбу ловят броском: ЛКМ — укус, ПКМ — замах.\n\nКролика бросок только хватает — держите F, чтобы задушить.\n\nВ птичьих гнёздах — яйца. Птица будет клевать; бросок её отгонит.",
            "Замрите на пару секунд — змея сольётся с местностью, а в траве и камыше её почти не видно. Затаившаяся змея подманивает добычу кончиком хвоста.\n\nВ мышиных городках мыши ныряют в норки — ждите в засаде.\n\nЁж свернулся — толкните шар носом в воду или ждите, пока выглянет.\n\nХолодно — солнечный камень, ночью — тёплый ключ.",
            "Днём — орёл: спасают трава, кроны, полое бревно или бросок в последний миг.\n\nНочью — лиса: идёт по следу, в воду и ежевику не лезет. Бросок в морду отпугнёт её.\n\nЕжевика колет на бегу. Осы гонят шумную змею до воды, норы или бревна — и жалят лису.",
            "Повзрослейте, затем ночью, сытой, отложите яйца в гнезде (E) и охраняйте кладку — лиса придёт за яйцами. Вылупятся змеёныши — играете следующим поколением.\n\nE у норы — сохраниться. TAB — карта и цели. H — скрыть интерфейс."
        };
        for (int i = 0; i < titles.Length; i++)
        {
            float x = LEFT + i * 430f;
            TopLeft(Label(screen, "Index", (i + 1).ToString("00"), 20, accents[i], TextAnchor.MiddleLeft, true), x, -300f, 80f, 26f);
            TopLeft(Label(screen, "Title", titles[i], 40, Bone, TextAnchor.MiddleLeft, true), x - 2f, -326f, 380f, 52f);
            TopLeft(Line(screen, accents[i]), x, -390f, 380f, 2f);
            Text body = Label(screen, "Body", bodies[i], 20, Ash, TextAnchor.UpperLeft, false);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
            TopLeft(body, x, -414f, 370f, 470f);
        }

        back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(back, LEFT, -918f, 220f, 60f);
        watch = LinkButton(screen, "Watch", "СМОТРЕТЬ ВИДЕО-ОБУЧЕНИЕ   ›", 22, out Text watchLabel);
        TopLeft(watch, LEFT + 252f, -918f, 420f, 60f);
        watchLabel.alignment = TextAnchor.MiddleLeft;
        return screen.gameObject;
    }

    // =====================================================================
    // Видео-обучение
    // =====================================================================

    private sealed class TutorialParts
    {
        public TutorialCanvas Canvas;
        public Text Title, Index, Caption, Clock, PlayLabel;
        public Button Screen, Play, Prev, Next, Back;
        public Object[] Fills, ChapterButtons, ChapterLabels;
        public Object[] Labels, Keys, KeyFrames, KeyTexts;
    }

    /// <summary>
    /// Плеер видео-обучения: «экран» 16:9 слева (кадр рисует TutorialCanvas, края обрезает RectMask2D),
    /// под ним таймлайн из отрезков-глав, строка «глава · время» и субтитры; справа — список глав и кнопки.
    /// </summary>
    private static GameObject BuildTutorial(Transform root, Texture2D shade, out TutorialParts p)
    {
        int chapters = TutorialFilm.ChapterCount;
        p = new TutorialParts
        {
            Fills = new Object[chapters], ChapterButtons = new Object[chapters], ChapterLabels = new Object[chapters],
            Labels = new Object[5], Keys = new Object[3], KeyFrames = new Object[3], KeyTexts = new Object[3]
        };
        RectTransform screen = Screen(root, "TutorialPanel", shade, out _);
        // Поверх виньетки — ровная тёмная подложка: ролик смотрится как в кинозале, сцена не отвлекает.
        RectTransform dim = Rect("Dim", screen);
        Stretch(dim);
        Img(dim, new Color(Ink.r, Ink.g, Ink.b, 0.8f), null);
        Heading(screen, "ВИДЕО-ОБУЧЕНИЕ", "КАК ИГРАТЬ", 84);

        const float videoW = 1088f, videoH = 612f, videoTop = -262f;
        RectTransform border = Rect("VideoBorder", screen);
        TopLeft(border, LEFT - 2f, videoTop + 2f, videoW + 4f, videoH + 4f);
        Img(border, Hair, null);
        RectTransform frame = Rect("Video", screen);
        TopLeft(frame, LEFT, videoTop, videoW, videoH);
        Img(frame, Ink, null);
        frame.gameObject.AddComponent<RectMask2D>();

        RectTransform film = Rect("Film", frame);
        Stretch(film);
        p.Canvas = film.gameObject.AddComponent<TutorialCanvas>();
        p.Canvas.raycastTarget = true;
        p.Screen = film.gameObject.AddComponent<Button>();
        p.Screen.transition = Selectable.Transition.None;
        p.Screen.navigation = new Navigation { mode = Navigation.Mode.None };

        for (int i = 0; i < p.Labels.Length; i++)
        {
            Text label = Label(film, "Label", "", 26, Bone, TextAnchor.MiddleCenter, true);
            Anchor(label.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 40f));
            AddShadow(label);
            label.gameObject.SetActive(false);
            p.Labels[i] = label;
        }
        for (int i = 0; i < p.Keys.Length; i++)
        {
            RectTransform key = Rect("Key", film);
            Anchor(key, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 38f));
            p.KeyFrames[i] = Img(key, new Color(Bone.r, Bone.g, Bone.b, 0.55f), _keycap);
            Text keyText = Label(key, "Text", "", 18, Bone, TextAnchor.MiddleCenter, false);
            Stretch(keyText.rectTransform);
            p.KeyTexts[i] = keyText;
            p.Keys[i] = key;
            key.gameObject.SetActive(false);
        }

        // Таймлайн: отрезки по длительности глав, с зазорами.
        const float gap = 6f;
        float total = 0f;
        for (int i = 0; i < chapters; i++) total += TutorialFilm.Durations[i];
        float x = LEFT;
        float usable = videoW - gap * (chapters - 1);
        for (int i = 0; i < chapters; i++)
        {
            float w = usable * TutorialFilm.Durations[i] / total;
            RectTransform seg = Rect("Segment_" + i, screen);
            TopLeft(seg, x, videoTop - videoH - 16f, w, 5f);
            Img(seg, Hair, null);
            RectTransform fill = Rect("Fill", seg);
            Stretch(fill);
            Image fillImg = Img(fill, Venom, _solid);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 0f;
            p.Fills[i] = fillImg;
            x += w + gap;
        }

        float infoY = videoTop - videoH - 32f;
        p.Index = Label(screen, "ChapterIndex", "01 / 09", 20, Venom, TextAnchor.MiddleLeft, true);
        TopLeft(p.Index, LEFT, infoY, 110f, 34f);
        p.Title = Label(screen, "ChapterTitle", "ДВИЖЕНИЕ", 28, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Title, LEFT + 110f, infoY, 600f, 34f);
        p.Clock = Label(screen, "Clock", "0:00 / 2:00", 20, Ash, TextAnchor.MiddleRight, false);
        TopLeft(p.Clock, LEFT + videoW - 300f, infoY, 300f, 34f);
        p.Caption = Label(screen, "Caption", "", 24, Bone, TextAnchor.UpperLeft, false);
        p.Caption.horizontalOverflow = HorizontalWrapMode.Wrap;
        p.Caption.lineSpacing = 1.1f;
        TopLeft(p.Caption, LEFT, infoY - 42f, videoW, 70f);

        // Справа — главы и кнопки.
        float right = LEFT + videoW + 72f;
        TopLeft(Label(screen, "ChaptersCaption", Spaced("ГЛАВЫ"), 17, Venom, TextAnchor.MiddleLeft, true), right, videoTop, 400f, 24f);
        for (int i = 0; i < chapters; i++)
        {
            RectTransform row = Rect("Chapter_" + i, screen);
            TopLeft(row, right, videoTop - 36f - i * 56f, 520f, 52f);
            Img(row, new Color(0f, 0f, 0f, 0f), null).raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text label = Label(row, "Label", "<color=#757A7A>" + (i + 1).ToString("00") + "</color>    " + TutorialFilm.Titles[i], 26, Ash,
                TextAnchor.MiddleLeft, true);
            Stretch(label.rectTransform);
            TopLeft(Line(row, Hair), 0f, -51f, 520f, 1f);
            p.ChapterButtons[i] = button;
            p.ChapterLabels[i] = label;
        }

        float controlsY = videoTop - 36f - chapters * 56f - 24f;
        p.Prev = LinkButton(screen, "Prev", "‹  НАЗАД", 22, out _);
        TopLeft(p.Prev, right, controlsY, 130f, 56f);
        p.Play = PrimaryButton(screen, "Play", "ПАУЗА");
        TopLeft(p.Play, right + 140f, controlsY, 240f, 56f);
        p.PlayLabel = p.Play.GetComponentInChildren<Text>();
        p.Next = LinkButton(screen, "Next", "ВПЕРЁД  ›", 22, out _);
        TopLeft(p.Next, right + 390f, controlsY, 130f, 56f);

        p.Back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(p.Back, right, controlsY - 80f, 220f, 56f);
        TopLeft(Label(screen, "Keys", "SPACE — пауза    A / D — главы    ESC — закрыть", 18, Smoke, TextAnchor.MiddleLeft, false),
            right, controlsY - 150f, 560f, 28f);
        return screen.gameObject;
    }

    // =====================================================================
    // HUD
    // =====================================================================

    /// <summary>Элементы HUD, которые связываются с компонентами в Build.</summary>
    private sealed class HudParts
    {
        public CanvasGroup HudGroup;
        public CanvasGroup VitalsGroup;
        public Object[] VitalFills = new Object[3];
        public Object[] VitalArcs = new Object[3];
        public RectTransform SunBody;
        public Image SunBodyImage, SunProgress;
        public CanvasGroup HintGroup;
        public GameObject HintKeycap;
        public Text HintKey, HintLabel;
        public CanvasGroup AimGroup, StruggleGroup;
        public Image ChargeFill, StruggleFill;
        public RectTransform EagleMarker, FoxMarker;
        public MapIconParts Minimap;
        public CanvasGroup MinimapGroup;
        public RectTransform MinimapRotator, MinimapNorth;
        public CanvasGroup AlertGroup;
        public Text AlertText;
        public CanvasGroup BannerGroup;
        public Text BannerText;
        public Text ObjectiveText, DayLabel;
        public Image ObjectiveFill, MoltFill;
        public Text MoltValue;
        public Object[] StatusLines = new Object[3];
        public Object[] VitalLabels = new Object[3];
        public Object[] VitalValues = new Object[3];
    }

    /// <summary>Виньетки состояния и оверлей карты: живут вне HUD, чтобы работать и в режиме «без интерфейса».</summary>
    private sealed class OverlayParts
    {
        public Object[] DamageEdges = new Object[4];
        public CanvasGroup LowHealth, Frost, Warmth, Ambush;
        public CanvasGroup MapGroup;
        public MapIconParts MapIcons;
        public Text MapDay, Summary, QuestText, QuestNote;
        public Object[] GoalLabels = new Object[DailyGoals.MAX_GOALS];
        public Object[] GoalNotes = new Object[DailyGoals.MAX_GOALS];
        public Object[] GoalChecks = new Object[DailyGoals.MAX_GOALS];
        public Object[] GoalIndexes = new Object[DailyGoals.MAX_GOALS];
    }

    /// <summary>
    /// «Тихий» HUD: дуги показателей вокруг значка змеи (слева снизу), дуга солнца (справа сверху),
    /// контекстная подсказка у центра, прицел и кольцо борьбы, метки угроз у краёв, баннер.
    /// Всё, кроме дуги солнца, появляется только когда нужно.
    /// </summary>
    private static GameObject BuildHud(Transform root, Refs r, Texture2D cornerShade, out HudParts p)
    {
        p = new HudParts();
        RectTransform hud = Rect("HUD", root);
        Stretch(hud);
        p.HudGroup = Group(hud);

        // Мягкие чернильные тени из углов: текст читается и над светлыми кронами, а краёв у тени не видно —
        // она гаснет и вбок, и вниз (раньше прямоугольные полосы давали заметную светлую «щель» между ними).
        RectTransform objShade = Rect("ObjectiveShade", hud);
        TopLeft(objShade, 0f, 0f, 1150f, 330f);
        Raw(objShade, cornerShade).color = new Color(Ink.r, Ink.g, Ink.b, 0.82f);
        RectTransform dayShade = Rect("DayShade", hud);
        Anchor(dayShade, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(640f, 300f));
        RawImage dayShadeImg = Raw(dayShade, cornerShade);
        dayShadeImg.color = new Color(Ink.r, Ink.g, Ink.b, 0.75f);
        dayShadeImg.uvRect = new Rect(1f, 0f, -1f, 1f);
        RectTransform vitalShade = Rect("VitalsShade", hud);
        Anchor(vitalShade, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(980f, 470f));
        RawImage vitalShadeImg = Raw(vitalShade, cornerShade);
        vitalShadeImg.color = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
        vitalShadeImg.uvRect = new Rect(0f, 1f, 1f, -1f);
        RectTransform mapShade = Rect("MinimapShade", hud);
        Anchor(mapShade, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(620f, 470f));
        RawImage mapShadeImg = Raw(mapShade, cornerShade);
        mapShadeImg.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
        mapShadeImg.uvRect = new Rect(1f, 1f, -1f, -1f);

        BuildMinimap(hud, r, p);

        // ---------- Показатели: три тонкие дуги вокруг значка змеи ----------
        RectTransform vitals = Rect("Vitals", hud);
        Anchor(vitals, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(124f, 124f), new Vector2(190f, 190f));
        p.VitalsGroup = Group(vitals);
        RectTransform glyph = Rect("SnakeGlyph", vitals);
        Anchor(glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        Img(glyph, new Color(Bone.r, Bone.g, Bone.b, 0.9f), UISpriteFactory.SnakeGlyph);
        float[] diameters = { 182f, 150f, 118f };
        string[] names = { "Health", "Satiety", "Stamina" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform arc = Rect("Arc_" + names[i], vitals);
            Anchor(arc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameters[i], diameters[i]));
            // Разрыв дуги — внизу справа; поворачиваем, чтобы он смотрел вниз.
            arc.localRotation = Quaternion.Euler(0f, 0f, -45f);
            CanvasGroup arcGroup = Group(arc);
            Image track = Radial(arc, "Track", Hair, 0.75f);
            track.rectTransform.sizeDelta = Vector2.zero;
            Image fill = Radial(arc, "Fill", Bone, 0.75f);
            p.VitalFills[i] = fill;
            p.VitalArcs[i] = arcGroup;
        }

        // Подписи дуг справа от блока — видны всегда.
        string[] vitalNames = { "ЗДОРОВЬЕ", "СЫТОСТЬ", "ВЫНОСЛИВОСТЬ" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform row = Rect("VitalLabel_" + names[i], hud);
            Anchor(row, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(232f, 168f - i * 34f), new Vector2(340f, 30f));
            CanvasGroup rowGroup = Group(row);
            Text caption = Label(row, "Caption", Spaced(vitalNames[i]), 17, Bone, TextAnchor.MiddleLeft, false);
            Anchor(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(250f, 30f));
            Text value = Label(row, "Value", "100", 22, Bone, TextAnchor.MiddleLeft, false);
            Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(80f, 30f));
            p.VitalLabels[i] = rowGroup;
            p.VitalValues[i] = value;
        }

        // Состояния над показателями: «В ТРАВЕ», «ХОЛОДНО»…
        for (int i = 0; i < 3; i++)
        {
            Text line = Label(hud, "Status_" + i, "", 17, Venom, TextAnchor.MiddleLeft, true);
            Anchor(line.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(46f, 244f + i * 30f), new Vector2(520f, 28f));
            line.gameObject.SetActive(false);
            p.StatusLines[i] = line;
        }

        // ---------- Главная цель: строка слева сверху ----------
        TopLeft(Label(hud, "ObjectiveKicker", Spaced("ЦЕЛЬ"), 17, Venom, TextAnchor.MiddleLeft, true), 46f, -34f, 300f, 24f);
        p.ObjectiveText = Label(hud, "Objective", "Повзрослеть", 22, Bone, TextAnchor.MiddleLeft, false);
        TopLeft(p.ObjectiveText, 46f, -60f, 1100f, 30f);
        RectTransform objTrack = Rect("ObjectiveTrack", hud);
        TopLeft(objTrack, 46f, -98f, 380f, 2f);
        Img(objTrack, Hair, null);
        RectTransform objFill = Rect("ObjectiveFill", hud);
        TopLeft(objFill, 46f, -97f, 380f, 4f);
        p.ObjectiveFill = Img(objFill, Venom, _solid);
        p.ObjectiveFill.type = Image.Type.Filled;
        p.ObjectiveFill.fillMethod = Image.FillMethod.Horizontal;
        // До следующей линьки — сколько ещё съесть.
        TopLeft(Label(hud, "MoltCaption", Spaced("ДО ЛИНЬКИ"), 15, HudCaption, TextAnchor.MiddleLeft, true), 46f, -116f, 200f, 22f);
        RectTransform moltTrack = Rect("MoltTrack", hud);
        TopLeft(moltTrack, 196f, -127f, 230f, 2f);
        Img(moltTrack, Hair, null);
        RectTransform moltFillRt = Rect("MoltFill", hud);
        TopLeft(moltFillRt, 196f, -126f, 230f, 4f);
        p.MoltFill = Img(moltFillRt, Sky, _solid);
        p.MoltFill.type = Image.Type.Filled;
        p.MoltFill.fillMethod = Image.FillMethod.Horizontal;
        p.MoltValue = Label(hud, "MoltValue", "0%", 16, Bone, TextAnchor.MiddleLeft, false);
        TopLeft(p.MoltValue, 436f, -116f, 80f, 22f);
        TopLeft(Label(hud, "MapHint", "TAB — карта и журнал", 16, HudCaption, TextAnchor.MiddleLeft, false), 46f, -144f, 400f, 24f);

        // ---------- Время суток: полудуга, по которой идёт солнце или луна ----------
        RectTransform dial = Rect("SunDial", hud);
        Anchor(dial, new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(-104f, -92f), new Vector2(92f, 92f));
        RectTransform dialPivot = Rect("Pivot", dial);
        Anchor(dialPivot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
        Image dialTrack = Img(dialPivot, Hair, UISpriteFactory.ArcRing);
        // Полукруг: заливка Radial360 от левого края по часовой стрелке на половину круга.
        dialTrack.type = Image.Type.Filled;
        dialTrack.fillMethod = Image.FillMethod.Radial360;
        dialTrack.fillOrigin = (int)Image.Origin360.Left;
        dialTrack.fillClockwise = true;
        dialTrack.fillAmount = 0.5f;
        RectTransform progress = Rect("Progress", dialPivot);
        Stretch(progress);
        p.SunProgress = Img(progress, Venom, UISpriteFactory.ArcRing);
        p.SunProgress.type = Image.Type.Filled;
        p.SunProgress.fillMethod = Image.FillMethod.Radial360;
        p.SunProgress.fillOrigin = (int)Image.Origin360.Left;
        p.SunProgress.fillClockwise = true;
        RectTransform horizon = Line(dial, Hair);
        Anchor(horizon, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 1f));
        p.DayLabel = Label(hud, "DayLabel", "ДЕНЬ 1  ·  УТРО", 18, Bone, TextAnchor.UpperCenter, false);
        Anchor(p.DayLabel.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-104f, -100f), new Vector2(240f, 24f));
        p.SunBody = Rect("Body", dialPivot);
        Anchor(p.SunBody, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13f, 13f));
        p.SunBodyImage = Img(p.SunBody, Venom, UISpriteFactory.Dot);

        // ---------- Контекстная подсказка: клавиша и короткая подпись под центром экрана ----------
        RectTransform hint = Rect("ContextHint", hud);
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
        hint.pivot = new Vector2(0.5f, 0.5f);
        // Ниже головы змеи (при обычном ракурсе она чуть ниже центра экрана).
        hint.anchoredPosition = new Vector2(0f, -270f);
        var layout = hint.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(16, 22, 9, 9);
        var fitter = hint.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // Тёмная плашка под подсказкой: читается поверх светлой травы и неба.
        Img(hint, new Color(Ink.r, Ink.g, Ink.b, 0.72f), _chamfer);
        p.HintGroup = Group(hint);
        p.HintGroup.alpha = 0f;
        RectTransform keycap = Keycap(hint, "ЛКМ");
        keycap.sizeDelta = new Vector2(74f, 32f);
        var keyElement = keycap.gameObject.AddComponent<LayoutElement>();
        keyElement.preferredWidth = 74f;
        keyElement.preferredHeight = 32f;
        p.HintKeycap = keycap.gameObject;
        p.HintKey = keycap.GetComponentInChildren<Text>();
        p.HintLabel = Label(hint, "Label", "укус", 22, Bone, TextAnchor.MiddleLeft, false);

        // ---------- Прицел (только при замахе) и кольцо борьбы (только при обвивании) ----------
        RectTransform aim = Rect("Aim", hud);
        Anchor(aim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        p.AimGroup = Group(aim);
        p.AimGroup.alpha = 0f;
        RectTransform reticle = Rect("Reticle", aim);
        Anchor(reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
        Img(reticle, new Color(Bone.r, Bone.g, Bone.b, 0.85f), UISpriteFactory.ArcRing);
        RectTransform pip = Rect("Dot", aim);
        Anchor(pip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f));
        Img(pip, Bone, UISpriteFactory.Dot);
        p.ChargeFill = Radial(aim, "Charge", Venom, 0f);

        RectTransform struggle = Rect("Struggle", hud);
        Anchor(struggle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(132f, 132f));
        p.StruggleGroup = Group(struggle);
        p.StruggleGroup.alpha = 0f;
        Radial(struggle, "Track", Hair, 1f);
        p.StruggleFill = Radial(struggle, "Fill", Venom, 0f);
        RectTransform fKey = Keycap(struggle, "F");
        Anchor(fKey, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(40f, 34f));
        Text hold = Label(struggle, "Hold", Spaced("ДЕРЖИТЕ"), 15, HudCaption, TextAnchor.MiddleCenter, true);
        Anchor(hold.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(160f, 20f));

        // ---------- Угрозы: метки у краёв и одно крупное предупреждение о пике ----------
        p.EagleMarker = ThreatMarker(hud, "EagleMarker", UISpriteFactory.MapEagle, Ember);
        p.FoxMarker = ThreatMarker(hud, "FoxMarker", UISpriteFactory.MapFox, MapFox);
        RectTransform alert = Rect("Alert", hud);
        Anchor(alert, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 90f));
        p.AlertGroup = Group(alert);
        p.AlertGroup.alpha = 0f;
        p.AlertText = Label(alert, "Text", "ОРЁЛ ПИКИРУЕТ", 64, Ember, TextAnchor.MiddleCenter, true);
        Stretch(p.AlertText.rectTransform);

        // ---------- Баннер событий: один за раз ----------
        RectTransform banner = Rect("Banner", hud);
        Anchor(banner, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 110f));
        p.BannerGroup = Group(banner);
        p.BannerGroup.alpha = 0f;
        p.BannerText = Label(banner, "Text", "", 64, Bone, TextAnchor.MiddleCenter, true);
        Stretch(p.BannerText.rectTransform);
        p.BannerText.resizeTextForBestFit = true;
        p.BannerText.resizeTextMinSize = 30;
        p.BannerText.resizeTextMaxSize = 64;
        p.BannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform bannerLine = Line(banner, Venom);
        Anchor(bannerLine, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(90f, 5f));

        // Тексты висят над 3D-сценой — лёгкая тень для читаемости.
        foreach (Text t in hud.GetComponentsInChildren<Text>(true))
            if (t.color != Ink) AddShadow(t);
        return hud.gameObject;
    }

    /// <summary>Метка угрозы: стрелка (поворачивается к угрозе) и подпись под ней.</summary>
    /// <summary>Метка угрозы: значок зверя, стрелка (ThreatIndicator водит её вокруг значка) и подпись с расстоянием.</summary>
    private static RectTransform ThreatMarker(RectTransform hud, string name, Sprite icon, Color color)
    {
        RectTransform marker = Rect(name, hud);
        Anchor(marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        Group(marker).alpha = 0f;
        RectTransform backing = Rect("Backing", marker);
        Anchor(backing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
        Img(backing, new Color(Ink.r, Ink.g, Ink.b, 0.55f), UISpriteFactory.MapDisc);
        RectTransform ring = Rect("Ring", marker);
        Anchor(ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
        Img(ring, color, UISpriteFactory.ArcRing);
        RectTransform iconRt = Rect("Icon", marker);
        Anchor(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
        Img(iconRt, color, icon).preserveAspect = true;
        RectTransform arrow = Rect("Arrow", marker);
        Anchor(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(30f, 30f));
        Img(arrow, color, UISpriteFactory.ThreatArrow);
        Text label = Label(marker, "Label", "", 19, Bone, TextAnchor.MiddleCenter, false);
        Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(220f, 24f));
        // Тёмный контур: подпись читается и на светлой траве, и на небе.
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return marker;
    }

    /// <summary>
    /// Мини-карта справа снизу: круглое окно (маска) с поворотным узлом; внутри — подложка и значки (их ведёт MapIcons),
    /// поверх — тонкий обод и метка севера.
    /// </summary>
    private static void BuildMinimap(RectTransform hud, Refs r, HudParts p)
    {
        float size = MINIMAP_RADIUS * 2f;
        RectTransform root = Rect("Minimap", hud);
        Anchor(root, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-60f - MINIMAP_RADIUS, 60f + MINIMAP_RADIUS), new Vector2(size, size));
        p.MinimapGroup = Group(root);

        RectTransform back = Rect("Back", root);
        Anchor(back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 10f, size + 10f));
        Img(back, new Color(Ink.r, Ink.g, Ink.b, 0.85f), UISpriteFactory.MapDisc);

        RectTransform mask = Rect("Mask", root);
        Anchor(mask, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        Img(mask, Color.white, UISpriteFactory.MapDisc);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        RectTransform rotator = Rect("Rotator", mask);
        Anchor(rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        p.MinimapRotator = rotator;

        RectTransform mapImage = Rect("Map", rotator);
        Anchor(mapImage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        RawImage raw = Raw(mapImage, null);
        raw.color = new Color(0.1f, 0.12f, 0.14f);
        p.Minimap = BuildMapIcons(mapImage, r, 0.75f);
        p.Minimap.Map = raw;

        RectTransform rim = Rect("Rim", root);
        Anchor(rim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 4f, size + 4f));
        Img(rim, new Color(Bone.r, Bone.g, Bone.b, 0.55f), UISpriteFactory.ArcRing);

        Text north = Label(root, "North", "С", 18, Venom, TextAnchor.MiddleCenter, false);
        Anchor(north.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, MINIMAP_RADIUS - 14f), new Vector2(26f, 24f));
        AddShadow(north);
        p.MinimapNorth = north.rectTransform;
    }

    /// <summary>
    /// Виньетки состояния (урон с нужной стороны, малое здоровье, иней, тепло, засада) и карта с журналом по Tab.
    /// Виньетки видны во время партии всегда — и без интерфейса; карта открывается только по Tab.
    /// </summary>
    private static GameObject BuildOverlays(Transform root, Refs r, Texture2D edge, Texture2D edgeH, Texture2D edgeV, Texture2D shade,
        out OverlayParts p, out GameObject mapRoot)
    {
        p = new OverlayParts();
        RectTransform layer = Rect("Overlays", root);
        Stretch(layer);

        p.Ambush = FullLayer(layer, "Ambush", edge, new Color(Ink.r, Ink.g, Ink.b, 1f));
        p.Warmth = FullLayer(layer, "Warmth", edge, new Color(1f, 0.62f, 0.25f, 1f));
        p.Frost = FullLayer(layer, "Frost", UISpriteFactory.FrostEdge(), new Color(0.78f, 0.9f, 1f, 1f));
        p.LowHealth = FullLayer(layer, "LowHealth", edge, Ember);

        // Края урона: слева, справа, сверху, снизу.
        p.DamageEdges[0] = EdgeLayer(layer, "DamageLeft", edgeH, new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Rect(0f, 0f, 1f, 1f));
        p.DamageEdges[1] = EdgeLayer(layer, "DamageRight", edgeH, new Vector2(0.7f, 0f), new Vector2(1f, 1f), new Rect(1f, 0f, -1f, 1f));
        p.DamageEdges[2] = EdgeLayer(layer, "DamageTop", edgeV, new Vector2(0f, 0.7f), new Vector2(1f, 1f), new Rect(0f, 0f, 1f, 1f));
        p.DamageEdges[3] = EdgeLayer(layer, "DamageBottom", edgeV, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Rect(0f, 1f, 1f, -1f));

        // ---------- Карта и журнал (Tab) ----------
        // Карта — отдельный слой поверх HUD (его порядок выставляет Build), виньетки — под HUD.
        RectTransform map = Rect("MapJournal", root);
        mapRoot = map.gameObject;
        Stretch(map);
        RawImage bg = Raw(map, shade);
        bg.color = new Color(1f, 1f, 1f, 0.96f);
        p.MapGroup = Group(map);
        p.MapGroup.alpha = 0f;

        const float mapSize = 840f;
        RectTransform mapImage = Rect("Map", map);
        Anchor(mapImage, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LEFT, 0f), new Vector2(mapSize, mapSize));
        // Подложку рисует MapIcons при старте; до этого — тёмный квадрат.
        RawImage mapRaw = Raw(mapImage, null);
        mapRaw.color = new Color(0.1f, 0.12f, 0.14f);
        RegionLabels(mapImage, r.Grid, mapSize);
        p.MapIcons = BuildMapIcons(mapImage, r, 1f);
        p.MapIcons.KnownRing = MapIcon(mapImage, "KnownRing", UISpriteFactory.ArcRing, new Color(Bone.r, Bone.g, Bone.b, 0.35f), 100f);
        p.MapIcons.KnownRing.SetSiblingIndex(p.MapIcons.KnownRing.parent.childCount - 2);
        p.MapIcons.Map = mapRaw;
        RectTransform frame = Rect("Frame", mapImage);
        Stretch(frame);
        Img(frame, new Color(Bone.r, Bone.g, Bone.b, 0.35f), _frame);

        // Колонка журнала привязана к середине экрана, как и карта: верх колонки — верх карты, низ — не ниже её низа.
        // Раньше она шла от верха экрана и на экранах ниже 16:9 легенда наезжала на подсказку внизу.
        float x = LEFT + mapSize + 90f;
        MidLeft(Label(map, "Kicker", Spaced("ЖУРНАЛ"), 20, Venom, TextAnchor.MiddleLeft, true), x, -128f, 600f, 30f);
        p.MapDay = Label(map, "Day", "ДЕНЬ 1", 96, Bone, TextAnchor.MiddleLeft, true);
        MidLeft(p.MapDay, x - 6f, -158f, 900f, 120f);
        MidLeft(Line(map, Venom), x, -290f, 72f, 5f);
        MidLeft(Label(map, "QuestCaption", Spaced("ГЛАВНАЯ ЦЕЛЬ · ПРОДОЛЖИТЬ РОД"), 17, Venom, TextAnchor.MiddleLeft, true), x, -318f, 700f, 24f);
        p.QuestText = Label(map, "Quest", "1 из 2  ·  Повзрослеть", 30, Bone, TextAnchor.MiddleLeft, true);
        MidLeft(p.QuestText, x, -346f, 800f, 40f);
        p.QuestNote = Label(map, "QuestNote", "", 19, Ash, TextAnchor.UpperLeft, false);
        p.QuestNote.horizontalOverflow = HorizontalWrapMode.Wrap;
        MidLeft(p.QuestNote, x, -392f, 760f, 54f);
        MidLeft(Label(map, "GoalsCaption", Spaced("ЦЕЛИ ДНЯ"), 17, Venom, TextAnchor.MiddleLeft, true), x, -470f, 400f, 24f);
        for (int i = 0; i < DailyGoals.MAX_GOALS; i++)
        {
            float y = -506f - i * 96f;
            Text index = Label(map, "Index", (i + 1).ToString("00"), 21, Smoke, TextAnchor.MiddleLeft, true);
            MidLeft(index, x, y - 4f, 60f, 30f);
            p.GoalIndexes[i] = index;
            Text label = Label(map, "Goal_" + i, "Цель", 34, Bone, TextAnchor.MiddleLeft, true);
            MidLeft(label, x + 60f, y - 4f, 760f, 44f);
            Text note = Label(map, "Note_" + i, "награда", 19, Ash, TextAnchor.MiddleLeft, false);
            MidLeft(note, x + 62f, y - 52f, 760f, 26f);
            RectTransform check = Line(map, Venom);
            MidLeft(check, x - 22f, y - 6f, 4f, 72f);
            check.gameObject.SetActive(false);
            p.GoalLabels[i] = label;
            p.GoalNotes[i] = note;
            p.GoalChecks[i] = check.gameObject;
        }
        MidLeft(Line(map, Hair), x, -712f, 820f, 1f);
        p.Summary = Label(map, "Summary", "", 20, Ash, TextAnchor.MiddleLeft, false);
        MidLeft(p.Summary, x, -730f, 760f, 28f);
        Legend(map, x, -786f);
        // Подсказка — в правом верхнем углу: внизу она спорила с легендой.
        HintRow(map, new Vector2(1f, 1f), new Vector2(-LEFT, -60f), new Vector2(1f, 0.5f), ("TAB", "держать — карта и журнал"));
        return layer.gameObject;
    }

    /// <summary>Подложка и значки одной карты (журнала или мини-карты).</summary>
    private sealed class MapIconParts
    {
        public RawImage Map;
        public RectTransform PlayerIcon, PlayerRing, RivalIcon, EagleIcon, FoxIcon, NestIcon, NestRing;
        public Object[] PreyIcons, BurrowIcons, BirdNestIcons, SunStoneIcons, SpringIcons, ColonyIcons, WaspIcons;
        public RectTransform KnownRing;
    }

    /// <summary>
    /// Значки карты поверх подложки. Порядок слоёв: неподвижное (камни, гнёзда, норы), добыча, змеи и хищники, вы — сверху.
    /// Неподвижных значков столько, сколько объектов на поле этого сида; MapIcons расставит их и скроет лишние.
    /// scale — размер значков относительно карты журнала (на мини-карте они мельче).
    /// </summary>
    private static MapIconParts BuildMapIcons(RectTransform mapImage, Refs r, float scale)
    {
        var p = new MapIconParts();
        RectTransform layer = Rect("Icons", mapImage);
        Stretch(layer);
        GridManager grid = r.Grid;

        p.SunStoneIcons = new Object[grid.SunStoneCount];
        for (int i = 0; i < p.SunStoneIcons.Length; i++) p.SunStoneIcons[i] = MapIcon(layer, "SunStone", UISpriteFactory.MapSun, MapSun, 16f * scale);
        p.SpringIcons = new Object[grid.HotSpringCount];
        for (int i = 0; i < p.SpringIcons.Length; i++) p.SpringIcons[i] = MapIcon(layer, "HotSpring", UISpriteFactory.MapSpring, MapSpring, 24f * scale);
        p.ColonyIcons = new Object[grid.MouseColonyCount];
        for (int i = 0; i < p.ColonyIcons.Length; i++) p.ColonyIcons[i] = MapIcon(layer, "MouseColony", UISpriteFactory.MapMouse, MapColony, 24f * scale);
        p.WaspIcons = new Object[grid.WaspNestCount];
        for (int i = 0; i < p.WaspIcons.Length; i++) p.WaspIcons[i] = MapIcon(layer, "WaspNest", UISpriteFactory.MapWasp, MapWasp, 26f * scale);
        p.BirdNestIcons = new Object[grid.BirdNestCount];
        for (int i = 0; i < p.BirdNestIcons.Length; i++) p.BirdNestIcons[i] = MapIcon(layer, "BirdNest", UISpriteFactory.MapBirdNest, Sky, 22f * scale);
        p.BurrowIcons = new Object[grid.BurrowCount];
        for (int i = 0; i < p.BurrowIcons.Length; i++) p.BurrowIcons[i] = MapIcon(layer, "Burrow", UISpriteFactory.MapBurrow, Venom, 26f * scale);
        p.NestRing = MapIcon(layer, "NestRing", UISpriteFactory.MapRing, Venom, 62f * scale);
        p.NestIcon = MapIcon(layer, "Nest", UISpriteFactory.MapNest, Bone, 38f * scale);

        // Значки добычи — по одному на животное пула; вид (лапа, рыба, жук) MapIcons выставит по животному.
        p.PreyIcons = new Object[r.Wildlife.AnimalCount];
        for (int i = 0; i < p.PreyIcons.Length; i++)
            p.PreyIcons[i] = MapIcon(layer, "Prey", UISpriteFactory.MapPaw, MapPrey, 18f * scale).GetComponent<Image>();

        p.RivalIcon = MapIcon(layer, "Rival", UISpriteFactory.MapArrow, MapRival, 30f * scale);
        p.FoxIcon = MapIcon(layer, "Fox", UISpriteFactory.MapFox, MapFox, 32f * scale);
        p.EagleIcon = MapIcon(layer, "Eagle", UISpriteFactory.MapEagle, Ember, 38f * scale);
        p.PlayerRing = MapIcon(layer, "PlayerRing", UISpriteFactory.MapRing, Venom, 56f * scale);
        p.PlayerIcon = MapIcon(layer, "Player", UISpriteFactory.MapArrow, Venom, 36f * scale);
        return p;
    }

    private static void WireMapIcons(MapIcons component, Refs r, MapIconParts p, CanvasGroup group)
    {
        SnakeSceneBuilder.Wire(component, ("_game", r.Game), ("_grid", r.Grid), ("_player", r.Player), ("_rival", r.Rival),
            ("_wildlife", r.Wildlife), ("_eagle", r.Eagle), ("_fox", r.Fox), ("_map", p.Map), ("_group", group),
            ("_playerIcon", p.PlayerIcon), ("_playerRing", p.PlayerRing), ("_rivalIcon", p.RivalIcon),
            ("_eagleIcon", p.EagleIcon), ("_foxIcon", p.FoxIcon), ("_nestIcon", p.NestIcon), ("_nestRing", p.NestRing),
            ("_pawSprite", UISpriteFactory.MapPaw), ("_fishSprite", UISpriteFactory.MapFish), ("_beetleSprite", UISpriteFactory.MapBeetle));
        SnakeSceneBuilder.WireArray(component, "_preyIcons", p.PreyIcons);
        SnakeSceneBuilder.WireArray(component, "_burrowIcons", p.BurrowIcons);
        SnakeSceneBuilder.WireArray(component, "_birdNestIcons", p.BirdNestIcons);
        SnakeSceneBuilder.WireArray(component, "_sunStoneIcons", p.SunStoneIcons);
        SnakeSceneBuilder.WireArray(component, "_springIcons", p.SpringIcons);
        SnakeSceneBuilder.WireArray(component, "_colonyIcons", p.ColonyIcons);
        SnakeSceneBuilder.WireArray(component, "_waspIcons", p.WaspIcons);
        if (p.KnownRing != null) SnakeSceneBuilder.Wire(component, ("_knownRing", p.KnownRing));
    }

    private static RectTransform MapIcon(RectTransform parent, string name, Sprite sprite, Color color, float size)
    {
        RectTransform icon = Rect(name, parent);
        Anchor(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        Img(icon, color, sprite).preserveAspect = true;
        return icon;
    }

    /// <summary>Обозначения карты: те же значки и цвета, четыре колонки под журналом (звери, добыча, места).</summary>
    private static void Legend(RectTransform map, float x, float y)
    {
        (Sprite sprite, Color color, string text)[] items =
        {
            (UISpriteFactory.MapArrow, Venom, "вы"), (UISpriteFactory.MapArrow, MapRival, "соперница"),
            (UISpriteFactory.MapEagle, Ember, "орёл"), (UISpriteFactory.MapFox, MapFox, "лиса"),
            (UISpriteFactory.MapPaw, MapPrey, "добыча рядом"), (UISpriteFactory.MapFish, MapFish, "рыба"),
            (UISpriteFactory.MapBeetle, MapPrey, "жуки"), (UISpriteFactory.MapBirdNest, Sky, "птичье гнездо"),
            (UISpriteFactory.MapBurrow, Venom, "нора"), (UISpriteFactory.MapNest, Bone, "гнездо для кладки"),
            (UISpriteFactory.MapSun, MapSun, "солнечный камень"), (UISpriteFactory.MapSpring, MapSpring, "тёплый ключ"),
            (UISpriteFactory.MapMouse, MapColony, "мышиный городок"), (UISpriteFactory.MapWasp, MapWasp, "осиное гнездо"),
            (UISpriteFactory.Dot, MapBramble, "ежевика")
        };
        MidLeft(Label(map, "LegendCaption", Spaced("ОБОЗНАЧЕНИЯ"), 17, Venom, TextAnchor.MiddleLeft, true), x, y, 400f, 24f);
        const int columns = 4;
        for (int i = 0; i < items.Length; i++)
        {
            float ix = x + (i % columns) * 205f;
            float iy = y - 36f - (i / columns) * 34f;
            RectTransform icon = Rect("LegendIcon", map);
            MidLeft(icon, ix, iy, 26f, 26f);
            Img(icon, items[i].color, items[i].sprite).preserveAspect = true;
            MidLeft(Label(map, "LegendText", items[i].text, 18, Ash, TextAnchor.MiddleLeft, false), ix + 34f, iy, 170f, 26f);
        }
    }

    /// <summary>
    /// Подписи регионов на карте журнала: позиции — из плана мира (клетки от центра), шрифт мелкий, с тенью —
    /// читается и на траве, и на воде, но не спорит со значками (они выше).
    /// </summary>
    private static void RegionLabels(RectTransform mapImage, GridManager grid, float mapSize)
    {
        RectTransform layer = Rect("Regions", mapImage);
        Stretch(layer);
        float pixelsPerCell = mapSize / grid.Width;
        for (int i = 0; i < WorldBlueprint.Regions.Length; i++)
        {
            Text label = Label(layer, "Region", Spaced(WorldBlueprint.Regions[i].name), 15, new Color(Bone.r, Bone.g, Bone.b, 0.85f), TextAnchor.MiddleCenter, false);
            Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), WorldBlueprint.Regions[i].at * pixelsPerCell, new Vector2(320f, 22f));
            AddShadow(label);
        }
    }

    /// <summary>Привязка к левому краю и середине экрана по высоте; y — как от верха экрана 1080 (отрицательный).</summary>
    private static void MidLeft(Component c, float x, float y, float w, float h)
    {
        Anchor((RectTransform)c.transform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(x, y + 540f), new Vector2(w, h));
    }

    private static CanvasGroup FullLayer(RectTransform parent, string name, Texture2D texture, Color color)
    {
        RectTransform rt = Rect(name, parent);
        Stretch(rt);
        Raw(rt, texture).color = color;
        CanvasGroup g = Group(rt);
        g.alpha = 0f;
        return g;
    }

    private static CanvasGroup EdgeLayer(RectTransform parent, string name, Texture2D texture, Vector2 min, Vector2 max, Rect uv)
    {
        RectTransform rt = Rect(name, parent);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        RawImage raw = Raw(rt, texture);
        raw.color = Ember;
        raw.uvRect = uv;
        CanvasGroup g = Group(rt);
        g.alpha = 0f;
        return g;
    }

    /// <summary>Кольцо-заливка (Image Filled Radial360) на всю площадь родителя.</summary>
    private static Image Radial(RectTransform parent, string name, Color color, float amount)
    {
        RectTransform rt = Rect(name, parent);
        Stretch(rt);
        Image img = Img(rt, color, UISpriteFactory.ArcRing);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Bottom;
        img.fillClockwise = true;
        img.fillAmount = amount;
        return img;
    }

    /// <summary>CanvasGroup, не перехватывающий клики.</summary>
    private static CanvasGroup Group(RectTransform rt)
    {
        var g = rt.gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.interactable = false;
        return g;
    }

    // =====================================================================
    // Строительные блоки
    // =====================================================================

    /// <summary>Полноэкранный слой с затемнением (блокирует клики под собой) и CanvasGroup для появления.</summary>
    private static RectTransform Screen(Transform root, string name, Texture2D shade, out CanvasGroup group)
    {
        RectTransform screen = Rect(name, root);
        Stretch(screen);
        RawImage bg = Raw(screen, shade);
        bg.raycastTarget = true;
        group = screen.gameObject.AddComponent<CanvasGroup>();
        return screen;
    }

    /// <summary>Заголовок экрана: лаймовая надпись-кикер, крупный заголовок и короткая черта.</summary>
    private static void Heading(RectTransform screen, string kicker, string title, int size)
    {
        TopLeft(Label(screen, "Kicker", Spaced(kicker), 20, Venom, TextAnchor.MiddleLeft, true), LEFT, -92f, 900f, 30f);
        float height = size * 1.18f;
        TopLeft(Label(screen, "Title", title, size, Bone, TextAnchor.MiddleLeft, true), LEFT - size * 0.06f, -122f, 1400f, height);
        TopLeft(Line(screen, Venom), LEFT, -122f - height - 8f, 72f, 5f);
    }

    /// <summary>
    /// Пункт списка: номер «01», крупная подпись, необязательное описание и рекорд.
    /// Акцентная полоса стоит снаружи содержимого, поэтому не уезжает вместе с ним.
    /// </summary>
    private static Row MakeRow(RectTransform parent, int index, string label, string description, float x, float y, float width, float height,
        int labelSize, Color accentColor, bool withRecord)
    {
        var row = new Row();
        RectTransform root = Rect("Row_" + label, parent);
        TopLeft(root, x, y, width, height);
        Image hit = Img(root, new Color(0f, 0f, 0f, 0f), null);
        hit.raycastTarget = true;
        row.Button = root.gameObject.AddComponent<Button>();
        row.Button.transition = Selectable.Transition.None;
        row.Button.navigation = new Navigation { mode = Navigation.Mode.None };
        root.gameObject.AddComponent<MenuItemHover>().Index = index;

        RectTransform accent = Rect("Accent", root);
        Anchor(accent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-22f, 0f), new Vector2(4f, height * 0.6f));
        Img(accent, accentColor, null);
        accent.gameObject.SetActive(false);
        row.Accent = accent.gameObject;

        RectTransform content = Rect("Content", root);
        Stretch(content);
        row.Content = content;

        bool sub = description != null;
        float labelY = sub ? height * 0.14f : 0f;
        bool small = labelSize < 34;
        float labelX = small ? 0f : 64f;

        if (!small)
        {
            row.Index = Label(content, "Index", (index + 1).ToString("00"), 21, Smoke, TextAnchor.MiddleLeft, true);
            Anchor(row.Index.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, labelY + labelSize * 0.12f), new Vector2(56f, 28f));
        }

        row.Label = Label(content, "Label", label, labelSize, Ash, TextAnchor.MiddleLeft, true);
        Anchor(row.Label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelX, labelY), new Vector2(width - labelX, labelSize * 1.3f));

        if (sub)
        {
            row.Description = Label(content, "Description", description, 20, Smoke, TextAnchor.MiddleLeft, false);
            Anchor(row.Description.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelX + 2f, labelY - labelSize * 0.66f), new Vector2(width - labelX, 26f));
        }

        if (withRecord)
        {
            row.Record = Label(content, "Record", "", 18, Smoke, TextAnchor.MiddleRight, true);
            Anchor(row.Record.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, labelY), new Vector2(220f, 24f));
        }
        return row;
    }

    /// <summary>Ряд подсказок «клавиша — действие»; раскладку делает HorizontalLayoutGroup.</summary>
    private static void HintRow(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 pivot, params (string key, string action)[] hints)
    {
        RectTransform row = Rect("Hints", parent);
        row.anchorMin = row.anchorMax = anchor;
        row.pivot = pivot;
        row.anchoredPosition = position;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < hints.Length; i++)
        {
            RectTransform key = Keycap(row, hints[i].key);
            var element = key.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = key.sizeDelta.x;
            element.preferredHeight = key.sizeDelta.y;
            Text action = Label(row, "Action", hints[i].action, 19, Ash, TextAnchor.MiddleLeft, false);
            if (i < hints.Length - 1) action.text += "      ";
        }
    }

    /// <summary>Клавиша-подсказка: контур со скошенными углами и надпись.</summary>
    private static RectTransform Keycap(Transform parent, string key)
    {
        RectTransform rt = Rect("Key_" + key, parent);
        rt.sizeDelta = new Vector2(Mathf.Max(34f, 20f + key.Length * 11f), 32f);
        Img(rt, new Color(Bone.r, Bone.g, Bone.b, 0.55f), _keycap);
        Text t = Label(rt, "Text", key, 16, Bone, TextAnchor.MiddleCenter, true);
        Stretch(t.rectTransform);
        return rt;
    }

    /// <summary>Главная кнопка: лаймовая плашка со скошенными углами и тёмный текст.</summary>
    private static Button PrimaryButton(RectTransform parent, string name, string text)
    {
        RectTransform rt = Rect(name, parent);
        Image img = Img(rt, Venom, _chamfer);
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        c.highlightedColor = Color.white;
        c.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        c.selectedColor = c.normalColor;
        c.colorMultiplier = 1.1f;
        b.colors = c;
        Text label = Label(rt, "Label", Spaced(text), 20, Ink, TextAnchor.MiddleCenter, true);
        Stretch(label.rectTransform);
        return b;
    }

    /// <summary>Текстовая кнопка: подпись светлеет при наведении.</summary>
    private static Button LinkButton(RectTransform parent, string name, string text, int size, out Text label)
    {
        RectTransform rt = Rect(name, parent);
        Image hit = Img(rt, new Color(0f, 0f, 0f, 0f), null);
        hit.raycastTarget = true;
        label = Label(rt, "Label", text, size, Color.white, TextAnchor.MiddleCenter, true);
        Stretch(label.rectTransform);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = label;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock c = b.colors;
        c.normalColor = Ash;
        c.highlightedColor = Bone;
        c.pressedColor = Venom;
        c.selectedColor = Ash;
        c.colorMultiplier = 1f;
        b.colors = c;
        return b;
    }

    private static RectTransform Line(Transform parent, Color color)
    {
        RectTransform rt = Rect("Line", parent);
        Img(rt, color, null);
        return rt;
    }

    private static Image Img(RectTransform rt, Color color, Sprite sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    private static RawImage Raw(RectTransform rt, Texture texture)
    {
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        return raw;
    }

    private static Text Label(Transform parent, string name, string value, int size, Color color, TextAnchor align, bool bold)
    {
        RectTransform rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _font;
        t.text = value;
        t.fontSize = size;
        // Жирный у системного шрифта синтезируется — на мелких кеглях он «мылит» буквы,
        // поэтому мелкие подписи всегда обычного начертания.
        t.fontStyle = bold && size > 22 ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = align;
        t.lineSpacing = 1f;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Разрядка для мелких заголовков: тонкий пробел между буквами (у Text нет межбуквенного интервала).</summary>
    private static string Spaced(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static void AddShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -2f);
    }

    private static RectTransform Rect(string name, Transform parent)
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

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Привязка к левому верхнему углу родителя: x вправо, y вниз (отрицательный).</summary>
    private static void TopLeft(Component c, float x, float y, float w, float h)
    {
        Anchor((RectTransform)c.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
    }
}
