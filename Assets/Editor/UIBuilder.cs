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
    private static Sprite _ring;
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
        _ring = UISpriteFactory.Ring;
        _frame = UISpriteFactory.Frame;
        _solid = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Texture2D menuShade = UISpriteFactory.Vignette("ShadeMenu", 0.94f, 0.78f, 0f);
        Texture2D pauseShade = UISpriteFactory.Vignette("ShadePause", 0.94f, 0.82f, 0.4f);
        // Настройки, рекорды и справочник открываются поверх меню/паузы — слева фон непрозрачный, иначе просвечивают их надписи.
        Texture2D deepShade = UISpriteFactory.Vignette("ShadeDeep", 1f, 1f, 0.72f);
        Texture2D topFade = UISpriteFactory.TopFade();
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

        GameObject hud = BuildHud(root, r, topFade, edge, out HudParts hp);
        GameObject menu = BuildMainMenu(root, menuShade, out Row[] menuRows, out CanvasGroup menuGroup);
        GameObject pause = BuildPause(root, pauseShade, out Row[] pauseRows, out CanvasGroup pauseGroup);
        GameObject molt = BuildMolt(root, pauseShade, out Row[] moltRows, out CanvasGroup moltGroup);
        GameObject over = BuildGameOver(root, pauseShade, out Row[] overRows, out CanvasGroup overGroup, out GameOverParts op);
        GameObject settings = BuildSettings(root, deepShade, out SettingsParts sp);
        GameObject board = BuildLeaderboard(root, deepShade, out Object[] columns, out Text empty, out Button boardBack);
        GameObject guide = BuildGuide(root, deepShade, out Button guideBack);

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

        var tutorial = canvasGo.AddComponent<TutorialController>();
        SnakeSceneBuilder.Wire(tutorial, ("_game", r.Game), ("_snake", r.Player), ("_scent", r.Scent),
            ("_plate", hp.TutorialPlate), ("_text", hp.TutorialText));

        var settingsComp = settings.AddComponent<SettingsPanel>();
        SnakeSceneBuilder.Wire(settingsComp, ("_settings", r.Settings), ("_audio", r.Audio), ("_tutorial", tutorial),
            ("_masterSlider", sp.Master), ("_musicSlider", sp.Music), ("_sfxSlider", sp.Sfx), ("_mouseSlider", sp.Mouse), ("_invertYToggle", sp.InvertY),
            ("_fullscreenToggle", sp.Fullscreen), ("_minimapToggle", sp.Minimap), ("_rivalToggle", sp.Rival),
            ("_qualityButton", sp.Quality), ("_qualityLabel", sp.QualityLabel), ("_tutorialButton", sp.Tutorial), ("_backButton", sp.Back));

        var boardComp = board.AddComponent<LeaderboardPanel>();
        SnakeSceneBuilder.Wire(boardComp, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio), ("_config", r.Config),
            ("_emptyText", empty), ("_backButton", boardBack));
        SnakeSceneBuilder.WireArray(boardComp, "_columns", columns);

        var guideComp = guide.AddComponent<GuidePanel>();
        SnakeSceneBuilder.Wire(guideComp, ("_audio", r.Audio), ("_backButton", guideBack));

        var ui = canvasGo.AddComponent<UIController>();
        SnakeSceneBuilder.Wire(ui,
            ("_game", r.Game), ("_snake", r.Player), ("_settings", r.Settings), ("_audio", r.Audio), ("_leaderboard", r.Leaderboard),
            ("_wildlife", r.Wildlife), ("_eagle", r.Eagle), ("_dayNight", r.DayNight), ("_scent", r.Scent),
            ("_cameraTransform", r.CameraTransform), ("_minimapCamera", r.MinimapCamera),
            ("_hudRoot", hud), ("_dayText", hp.Day), ("_clockText", hp.Clock), ("_periodFill", hp.PeriodFill),
            ("_healthFill", hp.HealthFill), ("_healthValue", hp.HealthValue),
            ("_satietyFill", hp.SatietyFill), ("_satietyValue", hp.SatietyValue), ("_satietyLabel", hp.SatietyLabel),
            ("_moltFill", hp.MoltFill), ("_scentPointer", hp.Pointer), ("_scentArrow", hp.Arrow),
            ("_dashFill", hp.DashFill), ("_smellFill", hp.SmellFill), ("_hiddenBadge", hp.Hidden),
            ("_threatGroup", hp.ThreatGroup), ("_threatTitle", hp.ThreatTitle), ("_threatHint", hp.ThreatHint),
            ("_damageFlash", hp.Flash), ("_minimapRoot", hp.Minimap), ("_bannerGroup", hp.BannerGroup), ("_bannerText", hp.BannerText),
            ("_menuPanel", menu), ("_pausePanel", pause), ("_moltPanel", molt), ("_gameOverPanel", over),
            ("_settingsPanel", settings), ("_leaderboardPanel", board), ("_guidePanel", guide),
            ("_gameOverTitle", op.Title), ("_daysText", op.Days), ("_rankText", op.Rank),
            ("_playButton", menuRows[0].Button), ("_guideButton", menuRows[1].Button),
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
        string[] statNames = { "ДОБЫЧА", "ДЛИНА", "ЛИНЬКИ", "ОРЛЫ", "ВРЕМЯ" };
        for (int i = 0; i < statNames.Length; i++)
        {
            float x = LEFT + i * 176f;
            TopLeft(Label(screen, "StatLabel_" + i, Spaced(statNames[i]), 17, Smoke, TextAnchor.MiddleLeft, true), x, -506f, 170f, 24f);
            Text value = Label(screen, "StatValue_" + i, "0", 44, Bone, TextAnchor.MiddleLeft, true);
            TopLeft(value, x - 2f, -532f, 170f, 56f);
            p.Stats[i] = value;
        }
        TopLeft(Label(screen, "EagleNote", "«Орлы» — сколько атак орла змея пережила без удара", 18, Smoke, TextAnchor.MiddleLeft, false),
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
        public Toggle Fullscreen, Minimap, Rival, InvertY;
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
        p.Minimap = SwitchAt(screen, SettingRow(screen, "Мини-карта", -768f), right);
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

        string[] titles = { "ОХОТА", "УКРЫТИЯ", "ОПАСНОСТИ", "РОСТ" };
        Color[] accents = { Venom, Venom, Ember, Sky };
        string[] bodies =
        {
            "Сытость всё время тает, ночью — быстрее.\n\nЖука достаточно коснуться.\n\nМышь, лягушку и птицу ловят только броском: SHIFT, когда добыча рядом. Касание её лишь спугнёт.\n\nУставшая после бегства добыча на миг замирает.",
            "Высокая трава прячет змею.\n\nДобыча замечает вас в два раза позже, а бросок, наоборот, выдаёт.\n\nОрёл не видит змею в траве и улетает ни с чем.\n\nВ прыжке укрытия нет.",
            "Орёл охотится днём: сначала тень и крик, потом пике. Спасают трава или бросок в последний миг.\n\nУдары о камни, деревья и тела ранят.\n\nКогда сытость на нуле, тело худеет, а затем змея теряет здоровье.",
            "E — нюх: язык покажет след к ближайшей добыче и подсветит всех вокруг.\n\nНабрав рост, змея линяет: раны затягиваются, а вы выбираете одно из трёх улучшений.\n\nПока змея сыта, здоровье восстанавливается."
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

    private sealed class HudParts
    {
        public Text Day, Clock, HealthValue, SatietyValue, SatietyLabel, TutorialText, BannerText, ThreatTitle, ThreatHint;
        public Image PeriodFill, HealthFill, SatietyFill, MoltFill, DashFill, SmellFill;
        public GameObject Pointer, Minimap, TutorialPlate, Hidden;
        public RectTransform Arrow;
        public CanvasGroup BannerGroup, ThreatGroup, Flash;
    }

    private static GameObject BuildHud(Transform root, Refs r, Texture2D topFade, Texture2D edge, out HudParts p)
    {
        p = new HudParts();
        RectTransform hud = Rect("HUD", root);
        Stretch(hud);

        // Вспышка урона — под всем остальным HUD, красная виньетка по краям.
        RectTransform flash = Rect("DamageFlash", hud);
        Stretch(flash);
        Raw(flash, edge).color = Ember;
        p.Flash = flash.gameObject.AddComponent<CanvasGroup>();
        p.Flash.alpha = 0f;
        p.Flash.blocksRaycasts = false;
        p.Flash.interactable = false;

        // Мягкие затемнения у верхнего и нижнего края: текст читается над любой частью сцены.
        RectTransform top = Rect("TopFade", hud);
        Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 330f));
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        Raw(top, topFade);
        RectTransform bottom = Rect("BottomFade", hud);
        Anchor(bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 200f));
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        Raw(bottom, topFade).uvRect = new Rect(0f, 1f, 1f, -1f);

        // Сутки и часы — слева сверху.
        TopLeft(Label(hud, "DayCaption", Spaced("СУТКИ"), 17, HudCaption, TextAnchor.MiddleLeft, true), 44f, -34f, 300f, 24f);
        p.Day = Label(hud, "Day", "1", 78, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Day, 38f, -52f, 140f, 92f);
        p.Clock = Label(hud, "Clock", "07:10   УТРО", 22, Bone, TextAnchor.MiddleLeft, false);
        TopLeft(p.Clock, 150f, -70f, 260f, 30f);
        p.PeriodFill = Bar(hud, 150f, -108f, 210f, 3f, Venom);

        // Здоровье, сытость и прогресс линьки.
        p.HealthValue = Vital(hud, "ЗДОРОВЬЕ", -160f, out Text _);
        p.HealthFill = Bar(hud, 44f, -192f, 316f, 4f, Bone);
        p.SatietyValue = Vital(hud, "СЫТОСТЬ", -210f, out p.SatietyLabel);
        p.SatietyFill = Bar(hud, 44f, -242f, 316f, 4f, Venom);
        Vital(hud, "ЛИНЬКА", -260f, out Text _).text = "";
        p.MoltFill = Bar(hud, 44f, -292f, 316f, 2f, Sky);

        // Компас нюха — тонкое кольцо сверху по центру, только пока виден след.
        RectTransform pointer = Rect("ScentPointer", hud);
        Anchor(pointer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(84f, 84f));
        Img(pointer, new Color(Bone.r, Bone.g, Bone.b, 0.45f), _ring);
        RectTransform pivot = Rect("ArrowPivot", pointer);
        Anchor(pivot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        RectTransform arrow = Rect("Arrow", pivot);
        Stretch(arrow);
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // спрайт смотрит вниз, разворачиваем вверх
        Image arrowImg = Img(arrow, Venom, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"));
        arrowImg.type = Image.Type.Simple;
        Text pointerLabel = Label(pointer, "Label", Spaced("СЛЕД"), 15, HudCaption, TextAnchor.MiddleCenter, true);
        Anchor(pointerLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(120f, 20f));
        pointer.gameObject.SetActive(false);
        p.Pointer = pointer.gameObject;
        p.Arrow = pivot;

        // Тревога об орле — под компасом, крупная надпись и совет.
        RectTransform threat = Rect("Threat", hud);
        Anchor(threat, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 100f));
        p.ThreatGroup = threat.gameObject.AddComponent<CanvasGroup>();
        p.ThreatGroup.alpha = 0f;
        p.ThreatGroup.blocksRaycasts = false;
        p.ThreatGroup.interactable = false;
        p.ThreatTitle = Label(threat, "Title", "ОРЁЛ В НЕБЕ", 44, Ember, TextAnchor.UpperCenter, true);
        Anchor(p.ThreatTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 56f));
        p.ThreatHint = Label(threat, "Hint", "Уходите в высокую траву", 22, Bone, TextAnchor.UpperCenter, false);
        Anchor(p.ThreatHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(1000f, 30f));

        // Бросок и нюх — слева снизу: подпись, клавиша и полоса готовности.
        p.DashFill = Ability(hud, "Dash", "СИЛЫ", "SHIFT", 44f);
        p.SmellFill = Ability(hud, "Smell", "НЮХ", "E", 324f);

        // Метка укрытия — лаймовая плашка над способностями.
        RectTransform hidden = Rect("HiddenBadge", hud);
        Anchor(hidden, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 126f), new Vector2(200f, 32f));
        Img(hidden, Venom, _chamfer);
        Text hiddenText = Label(hidden, "Text", "СКРЫТА В ТРАВЕ", 17, Ink, TextAnchor.MiddleCenter, true);
        Stretch(hiddenText.rectTransform);
        hidden.gameObject.SetActive(false);
        p.Hidden = hidden.gameObject;

        HintRow(hud, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(0.5f, 0.5f),
            ("WASD", "ползти"), ("CTRL", "красться"), ("ЛКМ", "укус"), ("ПКМ", "замах"), ("E", "нюх"), ("ESC", "пауза"));
        // Подсказки поверх светлой травы: подписи светлее, чем в меню.
        foreach (Text t in hud.Find("Hints").GetComponentsInChildren<Text>(true)) t.color = HudCaption;

        // Мини-карта — справа снизу, тонкая рамка и подпись.
        RectTransform minimap = Rect("Minimap", hud);
        Anchor(minimap, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 44f), new Vector2(250f, 250f));
        RectTransform mapImage = Rect("Map", minimap);
        Stretch(mapImage);
        mapImage.offsetMin = new Vector2(5f, 5f);
        mapImage.offsetMax = new Vector2(-5f, -5f);
        Raw(mapImage, r.MinimapTexture);
        RectTransform frame = Rect("Frame", minimap);
        Stretch(frame);
        Img(frame, new Color(Bone.r, Bone.g, Bone.b, 0.4f), _frame);
        Text mapLabel = Label(minimap, "Label", Spaced("КАРТА"), 15, HudCaption, TextAnchor.LowerLeft, true);
        Anchor(mapLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(200f, 20f));
        p.Minimap = minimap.gameObject;

        // Подсказка обучения — тёмная полоса с лаймовой меткой.
        RectTransform tutorial = Rect("Tutorial", hud);
        Anchor(tutorial, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 126f), new Vector2(1100f, 54f));
        Img(tutorial, new Color(Ink.r, Ink.g, Ink.b, 0.85f), null);
        RectTransform tutorialTag = Rect("Tag", tutorial);
        Anchor(tutorialTag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(150f, 30f));
        Img(tutorialTag, Venom, _chamfer);
        Text tagText = Label(tutorialTag, "Text", "ПОДСКАЗКА", 17, Ink, TextAnchor.MiddleCenter, true);
        Stretch(tagText.rectTransform);
        p.TutorialText = Label(tutorial, "Text", "", 23, Bone, TextAnchor.MiddleLeft, false);
        Stretch(p.TutorialText.rectTransform);
        p.TutorialText.rectTransform.offsetMin = new Vector2(184f, 0f);
        tutorial.gameObject.SetActive(false);
        p.TutorialPlate = tutorial.gameObject;

        // Баннер событий — крупная надпись с лаймовой чертой.
        RectTransform banner = Rect("Banner", hud);
        Anchor(banner, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 130f));
        p.BannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        p.BannerGroup.alpha = 0f;
        p.BannerGroup.blocksRaycasts = false;
        p.BannerGroup.interactable = false;
        p.BannerText = Label(banner, "Text", "", 72, Bone, TextAnchor.MiddleCenter, true);
        Stretch(p.BannerText.rectTransform);
        RectTransform bannerLine = Line(banner, Venom);
        Anchor(bannerLine, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(90f, 5f));

        // Тексты висят над 3D-сценой — лёгкая тень для читаемости.
        foreach (Text t in hud.GetComponentsInChildren<Text>(true))
            if (t.color != Ink) AddShadow(t);
        return hud.gameObject;
    }

    /// <summary>Строка показателя: разреженная подпись слева и число справа. Возвращает текст числа.</summary>
    private static Text Vital(RectTransform hud, string caption, float y, out Text label)
    {
        label = Label(hud, "Caption_" + caption, Spaced(caption), 17, HudCaption, TextAnchor.MiddleLeft, true);
        TopLeft(label, 44f, y, 220f, 24f);
        Text value = Label(hud, "Value_" + caption, "100", 22, Bone, TextAnchor.MiddleRight, false);
        TopLeft(value, 160f, y, 200f, 24f);
        return value;
    }

    /// <summary>Тонкая полоса: волосяная дорожка и заливка (Image типа Filled). Возвращает заливку.</summary>
    private static Image Bar(RectTransform parent, float x, float y, float width, float height, Color color)
    {
        RectTransform track = Rect("Track", parent);
        TopLeft(track, x, y, width, Mathf.Max(1f, height - 2f));
        track.anchoredPosition = new Vector2(x, y - 1f);
        Img(track, Hair, null);
        RectTransform fillRt = Rect("Fill", parent);
        TopLeft(fillRt, x, y, width, height);
        Image fill = Img(fillRt, color, _solid);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        return fill;
    }

    /// <summary>Способность внизу слева: подпись, клавиша и полоса готовности.</summary>
    private static Image Ability(RectTransform hud, string name, string caption, string key, float x)
    {
        RectTransform root = Rect(name, hud);
        Anchor(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 44f), new Vector2(250f, 60f));
        Text label = Label(root, "Label", Spaced(caption), 17, HudCaption, TextAnchor.MiddleLeft, true);
        Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(150f, 28f));
        RectTransform keycap = Keycap(root, key);
        Anchor(keycap, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, keycap.sizeDelta);
        RectTransform track = Rect("Track", root);
        Anchor(track, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(250f, 3f));
        Img(track, Hair, null);
        RectTransform fillRt = Rect("Fill", root);
        Anchor(fillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f), new Vector2(250f, 5f));
        Image fill = Img(fillRt, Venom, _solid);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        return fill;
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
