using Snake3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
// В UnityEditor есть свой SettingsService — явно выбираем игровой.
using SettingsService = Snake3D.SettingsService;

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
        public DayNightCycle DayNight;
        public ScentTracker Scent;
        public Transform CameraTransform;
        public Camera MinimapCamera;
        public RenderTexture MinimapTexture;
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

    private const float LEFT = 96f;

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

        GameObject stateLayer = BuildOverlays(root, r, edge, deepShade, out OverlayParts op2, out GameObject mapRoot);
        GameObject hud = BuildHud(root, out HudParts hp);
        GameObject menu = BuildMainMenu(root, menuShade, out Row[] menuRows, out CanvasGroup menuGroup);
        GameObject pause = BuildPause(root, pauseShade, out Row[] pauseRows, out CanvasGroup pauseGroup);
        GameObject molt = BuildMolt(root, pauseShade, out Row[] moltRows, out CanvasGroup moltGroup);
        GameObject over = BuildGameOver(root, pauseShade, out Row[] overRows, out CanvasGroup overGroup, out GameOverParts op);
        GameObject settings = BuildSettings(root, deepShade, out SettingsParts sp);
        GameObject board = BuildLeaderboard(root, deepShade, out Object[] columns, out Text empty, out Button boardBack);
        GameObject guide = BuildGuide(root, deepShade, out Button guideBack);

        // Карта по Tab — поверх HUD; во время партии остальные экраны скрыты, так что выше неё ничего нет.
        mapRoot.transform.SetAsLastSibling();
        pause.SetActive(false);
        molt.SetActive(false);
        over.SetActive(false);
        settings.SetActive(false);
        board.SetActive(false);
        guide.SetActive(false);

        // ---------- Компоненты ----------
        canvasGo.AddComponent<UIFontApplier>();
        var overlays = new Object[] { settings, board, guide };

        WireList(menu, menuRows, menuGroup, r, overlays, 1);
        WireList(pause, pauseRows, pauseGroup, r, overlays, 0);
        WireList(molt, moltRows, moltGroup, r, overlays, 0);
        WireList(over, overRows, overGroup, r, overlays, 0);

        // ---------- HUD ----------
        var vitals = hud.AddComponent<HudVitals>();
        SnakeSceneBuilder.Wire(vitals, ("_game", r.Game), ("_snake", r.Player), ("_group", hp.VitalsGroup));
        SnakeSceneBuilder.WireArray(vitals, "_fills", hp.VitalFills);
        SnakeSceneBuilder.WireArray(vitals, "_arcs", hp.VitalArcs);

        var dial = hud.AddComponent<SunDial>();
        SnakeSceneBuilder.Wire(dial, ("_dayNight", r.DayNight), ("_body", hp.SunBody), ("_bodyImage", hp.SunBodyImage), ("_progress", hp.SunProgress));

        var hints = canvasGo.AddComponent<ContextHints>();
        SnakeSceneBuilder.Wire(hints, ("_game", r.Game), ("_config", r.Config), ("_snake", r.Player), ("_wildlife", r.Wildlife),
            ("_grid", r.Grid), ("_eagle", r.Eagle), ("_fox", r.Fox), ("_scent", r.Scent),
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
        SnakeSceneBuilder.Wire(journal, ("_game", r.Game), ("_dayNight", r.DayNight), ("_mapCamera", r.MinimapCamera),
            ("_group", op2.MapGroup), ("_dayText", op2.MapDay), ("_summaryText", op2.Summary));
        SnakeSceneBuilder.WireArray(journal, "_goalLabels", op2.GoalLabels);
        SnakeSceneBuilder.WireArray(journal, "_goalNotes", op2.GoalNotes);
        SnakeSceneBuilder.WireArray(journal, "_goalChecks", op2.GoalChecks);

        var settingsComp = settings.AddComponent<SettingsPanel>();
        SnakeSceneBuilder.Wire(settingsComp, ("_settings", r.Settings), ("_audio", r.Audio), ("_hints", hints),
            ("_masterSlider", sp.Master), ("_musicSlider", sp.Music), ("_sfxSlider", sp.Sfx), ("_mouseSlider", sp.Mouse), ("_invertYToggle", sp.InvertY),
            ("_fullscreenToggle", sp.Fullscreen), ("_hudToggle", sp.Hud), ("_rivalToggle", sp.Rival),
            ("_qualityButton", sp.Quality), ("_qualityLabel", sp.QualityLabel), ("_tutorialButton", sp.Tutorial), ("_backButton", sp.Back));

        var boardComp = board.AddComponent<LeaderboardPanel>();
        SnakeSceneBuilder.Wire(boardComp, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio), ("_config", r.Config),
            ("_emptyText", empty), ("_backButton", boardBack));
        SnakeSceneBuilder.WireArray(boardComp, "_columns", columns);

        var guideComp = guide.AddComponent<GuidePanel>();
        SnakeSceneBuilder.Wire(guideComp, ("_audio", r.Audio), ("_backButton", guideBack));

        var ui = canvasGo.AddComponent<UIController>();
        SnakeSceneBuilder.Wire(ui,
            ("_game", r.Game), ("_settings", r.Settings), ("_audio", r.Audio), ("_leaderboard", r.Leaderboard),
            ("_eagle", r.Eagle), ("_dayNight", r.DayNight), ("_scent", r.Scent),
            ("_hudRoot", hud), ("_vignetteRoot", stateLayer), ("_bannerGroup", hp.BannerGroup), ("_bannerText", hp.BannerText),
            ("_menuPanel", menu), ("_pausePanel", pause), ("_moltPanel", molt), ("_gameOverPanel", over),
            ("_settingsPanel", settings), ("_leaderboardPanel", board), ("_guidePanel", guide),
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

        // Марка: лаймовая плашка «3D» и разреженная подпись.
        RectTransform tag = Rect("Tag", screen);
        TopLeft(tag, LEFT, -92f, 56f, 30f);
        Img(tag, Venom, _chamfer);
        Text tagText = Label(tag, "Text", "3D", 19, Ink, TextAnchor.MiddleCenter, true);
        Stretch(tagText.rectTransform);
        TopLeft(Label(screen, "Kicker", Spaced("ДИКАЯ ПРИРОДА"), 20, Ash, TextAnchor.MiddleLeft, true), LEFT + 72f, -92f, 500f, 30f);

        TopLeft(Label(screen, "Title", "ЗМЕЙКА", 164, Bone, TextAnchor.MiddleLeft, true), LEFT - 10f, -122f, 1000f, 200f);
        TopLeft(Line(screen, Venom), LEFT, -330f, 72f, 5f);
        TopLeft(Label(screen, "Tagline", "Охоться. Прячься. Линяй. Доживи до рассвета.", 24, Ash, TextAnchor.MiddleLeft, false), LEFT, -352f, 900f, 34f);

        rows = new Row[5];
        rows[0] = MakeRow(screen, 0, "ВЫЖИВАНИЕ", "Голод, хищники, смена дня и ночи", LEFT, -430f, 640f, 92f, 50, Venom, true);
        rows[1] = MakeRow(screen, 1, "СПРАВОЧНИК", "Как охотиться, прятаться и линять", LEFT, -534f, 640f, 92f, 50, Sky, false);

        TopLeft(Line(screen, Hair), LEFT, -652f, 640f, 1f);
        string[] links = { "РЕКОРДЫ", "НАСТРОЙКИ", "ВЫХОД" };
        float[] linkX = { LEFT, LEFT + 214f, LEFT + 452f };
        for (int i = 0; i < 3; i++)
            rows[2 + i] = MakeRow(screen, 2 + i, links[i], null, linkX[i], -670f, 200f, 60f, 26, i == 2 ? Ember : Venom, false);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("↑ ↓", "выбор"), ("ENTER", "играть"));
        Text version = Label(screen, "Version", "v2.0", 18, Smoke, TextAnchor.MiddleRight, true);
        Anchor(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-48f, 64f), new Vector2(200f, 24f));
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
        string[] statNames = { "ДОБЫЧА", "ДЛИНА", "ЛИНЬКИ", "ХИЩНИКИ", "ВРЕМЯ" };
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
        public Toggle Fullscreen, Hud, Rival, InvertY;
        public Button Quality, Tutorial, Back;
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
        p.Music = SliderAt(screen, SettingRow(screen, "Музыка", -382f), right, 0f, 1f);
        p.Sfx = SliderAt(screen, SettingRow(screen, "Звуки", -440f), right, 0f, 1f);

        Section(screen, "УПРАВЛЕНИЕ", -522f);
        p.Mouse = SliderAt(screen, SettingRow(screen, "Чувствительность мыши", -546f), right, 0.2f, 3f);
        p.InvertY = SwitchAt(screen, SettingRow(screen, "Инверсия оси Y", -604f), right);

        Section(screen, "ЭКРАН И ИГРА", -686f);
        p.Fullscreen = SwitchAt(screen, SettingRow(screen, "Полный экран   <size=18><color=#757A7A>F11</color></size>", -710f), right);
        p.Hud = SwitchAt(screen, SettingRow(screen, "Интерфейс в игре   <size=18><color=#757A7A>H</color></size>", -768f), right);
        p.Rival = SwitchAt(screen, SettingRow(screen, "Змея-соперница   <size=18><color=#757A7A>со следующей жизни</color></size>", -826f), right);

        float qualityY = SettingRow(screen, "Качество графики", -884f);
        p.Quality = LinkButton(screen, "Quality", "ULTRA   ›", 24, out p.QualityLabel);
        Anchor((RectTransform)p.Quality.transform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(right, qualityY), new Vector2(300f, 44f));
        p.QualityLabel.alignment = TextAnchor.MiddleRight;

        p.Back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(p.Back, LEFT, -970f, 220f, 60f);
        p.Tutorial = LinkButton(screen, "Tutorial", "ПОКАЗАТЬ ОБУЧЕНИЕ", 22, out Text tutorialLabel);
        TopLeft(p.Tutorial, LEFT + 252f, -970f, 360f, 60f);
        tutorialLabel.alignment = TextAnchor.MiddleLeft;
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

    private static GameObject BuildGuide(Transform root, Texture2D shade, out Button back)
    {
        RectTransform screen = Screen(root, "GuidePanel", shade, out _);
        Heading(screen, "КАК ВЫЖИТЬ", "СПРАВОЧНИК", 110);

        string[] titles = { "ОХОТА", "ЗАСАДА", "ОПАСНОСТИ", "ВЫЖИВАНИЕ" };
        Color[] accents = { Venom, Venom, Ember, Sky };
        string[] bodies =
        {
            "WASD — ползти, мышь — камера. CTRL — красться, SHIFT — быстрее (тратит силы).\n\nЖука достаточно коснуться. Мышь, лягушку и птицу ловят броском: ЛКМ — укус, ПКМ — замах, и бросок летит дальше.\n\nКролика бросок только хватает — держите F, чтобы задушить, пока хватает сил.",
            "Замрите на пару секунд — змея сольётся с местностью, а в высокой траве её почти не видно. Затаившаяся змея подманивает добычу кончиком хвоста: так ловят птиц.\n\nПромах броском на секунду выдаёт змею.",
            "Днём — орёл: метка у края экрана и тень. Спасают трава или бросок в последний миг.\n\nНочью приходит лиса: идёт по следу, в пруд не заходит. Бросок в морду отпугнёт её, пока здоровья больше половины.",
            "Змея холоднокровна: на солнечном камне греется, ночью и в воде мёрзнет — иней по краям экрана.\n\nE у норы — войти: прогресс сохраняется, ночь проходит быстрее.\n\nTAB — карта и цели дня. H — скрыть интерфейс."
        };
        for (int i = 0; i < titles.Length; i++)
        {
            float x = LEFT + i * 430f;
            TopLeft(Label(screen, "Index", (i + 1).ToString("00"), 20, accents[i], TextAnchor.MiddleLeft, true), x, -300f, 80f, 26f);
            TopLeft(Label(screen, "Title", titles[i], 40, Bone, TextAnchor.MiddleLeft, true), x - 2f, -326f, 380f, 52f);
            TopLeft(Line(screen, accents[i]), x, -390f, 380f, 2f);
            Text body = Label(screen, "Body", bodies[i], 22, Ash, TextAnchor.UpperLeft, false);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
            TopLeft(body, x, -414f, 370f, 470f);
        }

        back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(back, LEFT, -918f, 220f, 60f);
        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT + 260f, 108f), new Vector2(0f, 0.5f), ("ESC", "закрыть"));
        return screen.gameObject;
    }

    // =====================================================================
    // HUD
    // =====================================================================

    /// <summary>Элементы HUD, которые связываются с компонентами в Build.</summary>
    private sealed class HudParts
    {
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
        public CanvasGroup AlertGroup;
        public Text AlertText;
        public CanvasGroup BannerGroup;
        public Text BannerText;
    }

    /// <summary>Виньетки состояния и оверлей карты: живут вне HUD, чтобы работать и в режиме «без интерфейса».</summary>
    private sealed class OverlayParts
    {
        public Object[] DamageEdges = new Object[4];
        public CanvasGroup LowHealth, Frost, Warmth, Ambush;
        public CanvasGroup MapGroup;
        public Text MapDay, Summary;
        public Object[] GoalLabels = new Object[DailyGoals.MAX_GOALS];
        public Object[] GoalNotes = new Object[DailyGoals.MAX_GOALS];
        public Object[] GoalChecks = new Object[DailyGoals.MAX_GOALS];
    }

    /// <summary>
    /// «Тихий» HUD: дуги показателей вокруг значка змеи (слева снизу), дуга солнца (справа сверху),
    /// контекстная подсказка у центра, прицел и кольцо борьбы, метки угроз у краёв, баннер.
    /// Всё, кроме дуги солнца, появляется только когда нужно.
    /// </summary>
    private static GameObject BuildHud(Transform root, out HudParts p)
    {
        p = new HudParts();
        RectTransform hud = Rect("HUD", root);
        Stretch(hud);

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
            arcGroup.alpha = 0f;
            Image track = Radial(arc, "Track", Hair, 0.75f);
            track.rectTransform.sizeDelta = Vector2.zero;
            Image fill = Radial(arc, "Fill", Bone, 0.75f);
            p.VitalFills[i] = fill;
            p.VitalArcs[i] = arcGroup;
        }

        // ---------- Время суток: полудуга, по которой идёт солнце или луна ----------
        RectTransform dial = Rect("SunDial", hud);
        Anchor(dial, new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(-104f, -92f), new Vector2(92f, 92f));
        RectTransform dialPivot = Rect("Pivot", dial);
        Anchor(dialPivot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
        Image dialTrack = Img(dialPivot, Hair, UISpriteFactory.ArcRing);
        dialTrack.type = Image.Type.Filled;
        dialTrack.fillMethod = Image.FillMethod.Radial180;
        dialTrack.fillOrigin = (int)Image.Origin180.Bottom;
        dialTrack.fillAmount = 1f;
        RectTransform progress = Rect("Progress", dialPivot);
        Stretch(progress);
        p.SunProgress = Img(progress, Venom, UISpriteFactory.ArcRing);
        p.SunProgress.type = Image.Type.Filled;
        p.SunProgress.fillMethod = Image.FillMethod.Radial180;
        p.SunProgress.fillOrigin = (int)Image.Origin180.Bottom;
        p.SunProgress.fillClockwise = true;
        RectTransform horizon = Line(dial, Hair);
        Anchor(horizon, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 1f));
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
        var fitter = hint.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        p.HintGroup = Group(hint);
        p.HintGroup.alpha = 0f;
        RectTransform keycap = Keycap(hint, "ЛКМ");
        keycap.sizeDelta = new Vector2(74f, 32f);
        var keyElement = keycap.gameObject.AddComponent<LayoutElement>();
        keyElement.preferredWidth = 74f;
        keyElement.preferredHeight = 32f;
        p.HintKeycap = keycap.gameObject;
        p.HintKey = keycap.GetComponentInChildren<Text>();
        p.HintLabel = Label(hint, "Label", "укус", 21, Bone, TextAnchor.MiddleLeft, false);

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
        p.EagleMarker = ThreatMarker(hud, "EagleMarker", "ОРЁЛ");
        p.FoxMarker = ThreatMarker(hud, "FoxMarker", "ЛИСА");
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
    private static RectTransform ThreatMarker(RectTransform hud, string name, string caption)
    {
        RectTransform marker = Rect(name, hud);
        Anchor(marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        Group(marker).alpha = 0f;
        RectTransform arrow = Rect("Arrow", marker);
        Anchor(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        Img(arrow, Ember, UISpriteFactory.Chevron);
        Text label = Label(marker, "Label", Spaced(caption), 15, Ember, TextAnchor.MiddleCenter, true);
        Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(120f, 20f));
        AddShadow(label);
        return marker;
    }

    /// <summary>
    /// Виньетки состояния (урон с нужной стороны, малое здоровье, иней, тепло, засада) и карта с журналом по Tab.
    /// Виньетки видны во время партии всегда — и без интерфейса; карта открывается только по Tab.
    /// </summary>
    private static GameObject BuildOverlays(Transform root, Refs r, Texture2D edge, Texture2D shade, out OverlayParts p, out GameObject mapRoot)
    {
        p = new OverlayParts();
        RectTransform layer = Rect("Overlays", root);
        Stretch(layer);

        Texture2D edgeH = UISpriteFactory.EdgeGradient(false);
        Texture2D edgeV = UISpriteFactory.EdgeGradient(true);
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

        const float mapSize = 820f;
        RectTransform mapImage = Rect("Map", map);
        Anchor(mapImage, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LEFT, 10f), new Vector2(mapSize, mapSize));
        Raw(mapImage, r.MinimapTexture);
        RectTransform frame = Rect("Frame", mapImage);
        Stretch(frame);
        Img(frame, new Color(Bone.r, Bone.g, Bone.b, 0.35f), _frame);
        Legend(map, LEFT, -488f);

        float x = LEFT + mapSize + 90f;
        TopLeft(Label(map, "Kicker", Spaced("ЖУРНАЛ"), 20, Venom, TextAnchor.MiddleLeft, true), x, -128f, 600f, 30f);
        p.MapDay = Label(map, "Day", "ДЕНЬ 1", 96, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.MapDay, x - 6f, -158f, 900f, 120f);
        TopLeft(Line(map, Venom), x, -290f, 72f, 5f);
        TopLeft(Label(map, "GoalsCaption", Spaced("ЦЕЛИ ДНЯ"), 17, Venom, TextAnchor.MiddleLeft, true), x, -330f, 400f, 24f);
        for (int i = 0; i < DailyGoals.MAX_GOALS; i++)
        {
            float y = -372f - i * 112f;
            TopLeft(Label(map, "Index", (i + 1).ToString("00"), 21, Smoke, TextAnchor.MiddleLeft, true), x, y - 4f, 60f, 30f);
            Text label = Label(map, "Goal_" + i, "Цель", 34, Bone, TextAnchor.MiddleLeft, true);
            TopLeft(label, x + 60f, y - 4f, 760f, 44f);
            Text note = Label(map, "Note_" + i, "награда", 19, Ash, TextAnchor.MiddleLeft, false);
            TopLeft(note, x + 62f, y - 52f, 760f, 26f);
            RectTransform check = Line(map, Venom);
            TopLeft(check, x - 22f, y - 6f, 4f, 72f);
            check.gameObject.SetActive(false);
            p.GoalLabels[i] = label;
            p.GoalNotes[i] = note;
            p.GoalChecks[i] = check.gameObject;
        }
        TopLeft(Line(map, Hair), x, -620f, 760f, 1f);
        p.Summary = Label(map, "Summary", "", 20, Ash, TextAnchor.MiddleLeft, false);
        TopLeft(p.Summary, x, -640f, 760f, 28f);
        HintRow(map, new Vector2(0f, 0f), new Vector2(x, 64f), new Vector2(0f, 0.5f), ("TAB", "держать — карта и журнал"));
        return layer.gameObject;
    }

    /// <summary>Подписи к меткам карты: точки тех же цветов, что и маркеры камеры карты.</summary>
    private static void Legend(RectTransform map, float x, float y)
    {
        (Color color, string text)[] items =
        {
            (new Color(0.3f, 1f, 0.4f), "вы"), (new Color(1f, 0.92f, 0.55f), "добыча"), (Venom, "норы"),
            (new Color(1f, 0.5f, 0.1f), "хищники"), (new Color(1f, 0.55f, 0.15f), "соперница")
        };
        for (int i = 0; i < items.Length; i++)
        {
            float ix = x + i * 150f;
            RectTransform dot = Rect("LegendDot", map);
            Anchor(dot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(ix, y), new Vector2(12f, 12f));
            Img(dot, items[i].color, UISpriteFactory.Dot);
            Text t = Label(map, "LegendText", items[i].text, 18, Ash, TextAnchor.MiddleLeft, false);
            Anchor(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(ix + 20f, y), new Vector2(130f, 24f));
        }
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
