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
/// и клавиш-подсказок. HUD в той же гамме. Вызывается из SnakeSceneBuilder.
/// </summary>
internal static class UIBuilder
{
    /// <summary>Ссылки на игровые системы, с которыми связывается UI.</summary>
    public sealed class Refs
    {
        public GameManager Game;
        public ScoreService Score;
        public SnakeController Player;
        public PowerUpService PowerUps;
        public SettingsService Settings;
        public AudioService Audio;
        public ShrinkingArena Arena;
        public FoodSpawner Food;
        public LeaderboardService Leaderboard;
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

    private static readonly Color[] PowerUpColors = { new Color(1f, 0.85f, 0.2f), Sky, new Color(0.55f, 0.6f, 1f), new Color(0.95f, 0.4f, 0.85f), new Color(1f, 0.6f, 0.2f) };
    private static readonly string[] PowerUpNames = { "УСКОРЕНИЕ", "ЗАМЕДЛЕНИЕ", "ЩИТ", "МАГНИТ", "ОЧКИ ×2" };

    private static readonly string[] ModeNames = { "КЛАССИКА", "НА ВРЕМЯ", "ВЫЖИВАНИЕ" };
    private static readonly string[] ModeDescriptions = { "Бесконечная партия с уровнями", "Две минуты на рекорд", "Арена сужается каждые 15 секунд" };
    private static readonly Color[] ModeColors = { Venom, Sky, Ember };

    private const float LEFT = 96f;

    private static Font _font;
    private static Sprite _chamfer;
    private static Sprite _keycap;
    private static Sprite _ring;
    private static Sprite _frame;

    /// <summary>Пункт списка, собранный MakeRow.</summary>
    private struct Row
    {
        public Button Button;
        public RectTransform Content;
        public GameObject Accent;
        public Text Label;
        public Text Index;
        public Text Record;
    }

    public static void Build(Refs r)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _chamfer = UISpriteFactory.Chamfer;
        _keycap = UISpriteFactory.ChamferOutline;
        _ring = UISpriteFactory.Ring;
        _frame = UISpriteFactory.Frame;
        Texture2D menuShade = UISpriteFactory.Vignette("ShadeMenu", 0.94f, 0.78f, 0f);
        Texture2D pauseShade = UISpriteFactory.Vignette("ShadePause", 0.94f, 0.82f, 0.4f);
        // Настройки и рекорды открываются поверх меню/паузы — слева фон непрозрачный, иначе просвечивают их надписи.
        Texture2D deepShade = UISpriteFactory.Vignette("ShadeDeep", 1f, 1f, 0.72f);
        Texture2D topFade = UISpriteFactory.TopFade();

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

        GameObject hud = BuildHud(root, r, topFade, out HudParts hp);
        GameObject menu = BuildMainMenu(root, menuShade, out Row[] menuRows, out CanvasGroup menuGroup);
        GameObject pause = BuildPause(root, pauseShade, out Row[] pauseRows, out CanvasGroup pauseGroup);
        GameObject over = BuildGameOver(root, pauseShade, out Row[] overRows, out CanvasGroup overGroup, out GameOverParts op);
        GameObject settings = BuildSettings(root, deepShade, out SettingsParts sp);
        GameObject board = BuildLeaderboard(root, deepShade, out Object[] columns, out Button boardBack);

        pause.SetActive(false);
        over.SetActive(false);
        settings.SetActive(false);
        board.SetActive(false);

        // ---------- Компоненты ----------
        canvasGo.AddComponent<UIFontApplier>();
        var overlays = new Object[] { settings, board };

        WireList(menu, menuRows, menuGroup, r, overlays, 3);
        WireList(pause, pauseRows, pauseGroup, r, overlays, 0);
        WireList(over, overRows, overGroup, r, overlays, 0);

        var tutorial = canvasGo.AddComponent<TutorialController>();
        SnakeSceneBuilder.Wire(tutorial, ("_game", r.Game), ("_snake", r.Player), ("_plate", hp.TutorialPlate), ("_text", hp.TutorialText));

        var settingsComp = settings.AddComponent<SettingsPanel>();
        SnakeSceneBuilder.Wire(settingsComp, ("_settings", r.Settings), ("_audio", r.Audio), ("_tutorial", tutorial),
            ("_masterSlider", sp.Master), ("_musicSlider", sp.Music), ("_sfxSlider", sp.Sfx), ("_steerSlider", sp.Steer),
            ("_fullscreenToggle", sp.Fullscreen), ("_minimapToggle", sp.Minimap), ("_rivalToggle", sp.Rival),
            ("_qualityButton", sp.Quality), ("_qualityLabel", sp.QualityLabel), ("_tutorialButton", sp.Tutorial), ("_backButton", sp.Back));

        var boardComp = board.AddComponent<LeaderboardPanel>();
        SnakeSceneBuilder.Wire(boardComp, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio), ("_backButton", boardBack));
        SnakeSceneBuilder.WireArray(boardComp, "_columns", columns);

        var ui = canvasGo.AddComponent<UIController>();
        SnakeSceneBuilder.Wire(ui,
            ("_game", r.Game), ("_score", r.Score), ("_snake", r.Player), ("_powerUps", r.PowerUps), ("_settings", r.Settings),
            ("_audio", r.Audio), ("_arena", r.Arena), ("_food", r.Food), ("_cameraTransform", r.CameraTransform), ("_minimapCamera", r.MinimapCamera),
            ("_hudRoot", hud), ("_scoreText", hp.Score), ("_bestText", hp.Best), ("_levelText", hp.Level),
            ("_timerPlate", hp.TimerPlate), ("_timerText", hp.Timer),
            ("_foodPointer", hp.Pointer), ("_foodArrow", hp.Arrow), ("_dashFill", hp.DashFill),
            ("_minimapRoot", hp.Minimap), ("_bannerGroup", hp.BannerGroup), ("_bannerText", hp.BannerText),
            ("_menuPanel", menu), ("_pausePanel", pause), ("_gameOverPanel", over),
            ("_settingsPanel", settings), ("_leaderboardPanel", board),
            ("_gameOverTitle", op.Title), ("_finalScoreText", op.Score), ("_rankText", op.Rank),
            ("_classicButton", menuRows[0].Button), ("_timeAttackButton", menuRows[1].Button), ("_survivalButton", menuRows[2].Button),
            ("_menuLeaderboardButton", menuRows[3].Button), ("_menuSettingsButton", menuRows[4].Button), ("_quitButton", menuRows[5].Button),
            ("_resumeButton", pauseRows[0].Button), ("_pauseRestartButton", pauseRows[1].Button),
            ("_pauseSettingsButton", pauseRows[2].Button), ("_pauseMenuButton", pauseRows[3].Button),
            ("_restartButton", overRows[0].Button), ("_gameOverMenuButton", overRows[1].Button));
        SnakeSceneBuilder.WireArray(ui, "_statValues", op.Stats);
        SnakeSceneBuilder.WireArray(ui, "_powerUpRows", hp.PowerUpRows);
        SnakeSceneBuilder.WireArray(ui, "_powerUpFills", hp.PowerUpFills);
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

        SnakeSceneBuilder.Wire(list, ("_audio", r.Audio), ("_leaderboard", r.Leaderboard), ("_group", group));
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
        TopLeft(Label(screen, "Kicker", Spaced("АРКАДА"), 20, Ash, TextAnchor.MiddleLeft, true), LEFT + 72f, -92f, 400f, 30f);

        TopLeft(Label(screen, "Title", "ЗМЕЙКА", 164, Bone, TextAnchor.MiddleLeft, true), LEFT - 10f, -122f, 1000f, 200f);
        TopLeft(Line(screen, Venom), LEFT, -330f, 72f, 5f);
        TopLeft(Label(screen, "Tagline", "Свободное движение. Прыжки. Рывки.", 24, Ash, TextAnchor.MiddleLeft, false), LEFT, -352f, 800f, 34f);

        rows = new Row[6];
        for (int i = 0; i < 3; i++)
            rows[i] = MakeRow(screen, i, ModeNames[i], ModeDescriptions[i], LEFT, -430f - i * 104f, 620f, 92f, 50, ModeColors[i], true);

        TopLeft(Line(screen, Hair), LEFT, -756f, 620f, 1f);
        string[] links = { "РЕКОРДЫ", "НАСТРОЙКИ", "ВЫХОД" };
        float[] linkX = { LEFT, LEFT + 214f, LEFT + 452f };
        for (int i = 0; i < 3; i++)
            rows[3 + i] = MakeRow(screen, 3 + i, links[i], null, linkX[i], -774f, 200f, 60f, 26, i == 2 ? Ember : Venom, false);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("↑ ↓", "выбор"), ("ENTER", "играть"));
        Text version = Label(screen, "Version", "v1.3", 18, Smoke, TextAnchor.MiddleRight, true);
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
    // Итоги партии
    // =====================================================================

    private sealed class GameOverParts
    {
        public Text Title, Score, Rank;
        public Object[] Stats;
    }

    private static GameObject BuildGameOver(Transform root, Texture2D shade, out Row[] rows, out CanvasGroup group, out GameOverParts p)
    {
        p = new GameOverParts { Stats = new Object[5] };
        RectTransform screen = Screen(root, "GameOverPanel", shade, out group);

        p.Rank = Label(screen, "Rank", "", 20, Venom, TextAnchor.MiddleLeft, true);
        TopLeft(p.Rank, LEFT, -92f, 800f, 30f);
        p.Title = Label(screen, "Title", "ИГРА ОКОНЧЕНА", 60, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Title, LEFT - 4f, -126f, 1000f, 76f);
        p.Score = Label(screen, "Score", "0", 220, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Score, LEFT - 14f, -186f, 1000f, 250f);
        TopLeft(Label(screen, "ScoreCaption", Spaced("ОЧКОВ"), 20, Smoke, TextAnchor.MiddleLeft, true), LEFT, -436f, 400f, 28f);

        TopLeft(Line(screen, Hair), LEFT, -486f, 880f, 1f);
        string[] statNames = { "ДЛИНА", "ЯБЛОКИ", "БОНУСЫ", "УРОВЕНЬ", "ВРЕМЯ" };
        for (int i = 0; i < statNames.Length; i++)
        {
            float x = LEFT + i * 176f;
            TopLeft(Label(screen, "StatLabel_" + i, Spaced(statNames[i]), 17, Smoke, TextAnchor.MiddleLeft, true), x, -506f, 170f, 24f);
            Text value = Label(screen, "StatValue_" + i, "0", 44, Bone, TextAnchor.MiddleLeft, true);
            TopLeft(value, x - 2f, -532f, 170f, 56f);
            p.Stats[i] = value;
        }

        rows = new Row[2];
        rows[0] = MakeRow(screen, 0, "ЕЩЁ РАЗ", null, LEFT, -640f, 620f, 76f, 44, Venom, false);
        rows[1] = MakeRow(screen, 1, "В ГЛАВНОЕ МЕНЮ", null, LEFT, -728f, 620f, 76f, 44, Venom, false);

        HintRow(screen, new Vector2(0f, 0f), new Vector2(LEFT, 64f), new Vector2(0f, 0.5f),
            ("R", "ещё раз"), ("ENTER", "выбрать"));
        return screen.gameObject;
    }

    // =====================================================================
    // Настройки
    // =====================================================================

    private sealed class SettingsParts
    {
        public Slider Master, Music, Sfx, Steer;
        public Toggle Fullscreen, Minimap, Rival;
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
        p.Steer = SliderAt(screen, SettingRow(screen, "Чувствительность руля", -546f), right, 0.5f, 1.5f);

        Section(screen, "ЭКРАН И ИГРА", -628f);
        p.Fullscreen = SwitchAt(screen, SettingRow(screen, "Полный экран   <size=18><color=#757A7A>F11</color></size>", -652f), right);
        p.Minimap = SwitchAt(screen, SettingRow(screen, "Мини-карта", -710f), right);
        p.Rival = SwitchAt(screen, SettingRow(screen, "Змея-соперник   <size=18><color=#757A7A>со следующей партии</color></size>", -768f), right);

        float qualityY = SettingRow(screen, "Качество графики", -826f);
        p.Quality = LinkButton(screen, "Quality", "ULTRA   ›", 24, out p.QualityLabel);
        Anchor((RectTransform)p.Quality.transform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(right, qualityY), new Vector2(300f, 44f));
        p.QualityLabel.alignment = TextAnchor.MiddleRight;

        p.Back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(p.Back, LEFT, -918f, 220f, 60f);
        p.Tutorial = LinkButton(screen, "Tutorial", "ПОКАЗАТЬ ОБУЧЕНИЕ", 22, out Text tutorialLabel);
        TopLeft(p.Tutorial, LEFT + 252f, -918f, 360f, 60f);
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

    private static GameObject BuildLeaderboard(Transform root, Texture2D shade, out Object[] columns, out Button back)
    {
        RectTransform screen = Screen(root, "LeaderboardPanel", shade, out _);
        Heading(screen, "ЛУЧШИЕ РЕЗУЛЬТАТЫ", "РЕКОРДЫ", 110);

        columns = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            float x = LEFT + i * 480f;
            TopLeft(Label(screen, "Index", (i + 1).ToString("00"), 20, ModeColors[i], TextAnchor.MiddleLeft, true), x, -300f, 80f, 26f);
            TopLeft(Label(screen, "Mode", ModeNames[i], 40, Bone, TextAnchor.MiddleLeft, true), x - 2f, -326f, 420f, 52f);
            TopLeft(Line(screen, ModeColors[i]), x, -390f, 420f, 2f);
            Text rows = Label(screen, "Rows", "", 23, Ash, TextAnchor.UpperLeft, false);
            rows.lineSpacing = 1.45f;
            TopLeft(rows, x, -414f, 420f, 460f);
            columns[i] = rows;
        }

        back = PrimaryButton(screen, "Done", "ГОТОВО");
        TopLeft(back, LEFT, -918f, 220f, 60f);
        return screen.gameObject;
    }

    // =====================================================================
    // HUD
    // =====================================================================

    private sealed class HudParts
    {
        public Text Score, Best, Level, Timer, TutorialText, BannerText;
        public GameObject TimerPlate, Pointer, Minimap, TutorialPlate;
        public RectTransform Arrow;
        public Image DashFill;
        public CanvasGroup BannerGroup;
        public Object[] PowerUpRows, PowerUpFills;
    }

    private static GameObject BuildHud(Transform root, Refs r, Texture2D topFade, out HudParts p)
    {
        p = new HudParts();
        RectTransform hud = Rect("HUD", root);
        Stretch(hud);

        // Мягкие затемнения у верхнего и нижнего края: текст читается над любой частью сцены.
        RectTransform top = Rect("TopFade", hud);
        Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 250f));
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        Raw(top, topFade);
        RectTransform bottom = Rect("BottomFade", hud);
        Anchor(bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 200f));
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        Raw(bottom, topFade).uvRect = new Rect(0f, 1f, 1f, -1f);

        // Счёт — слева сверху.
        TopLeft(Label(hud, "ScoreCaption", Spaced("СЧЁТ"), 17, HudCaption, TextAnchor.MiddleLeft, true), 44f, -34f, 300f, 24f);
        p.Score = Label(hud, "Score", "0", 78, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Score, 38f, -52f, 500f, 92f);
        p.Best = Label(hud, "Best", "РЕКОРД  0", 20, Bone, TextAnchor.MiddleLeft, true);
        TopLeft(p.Best, 44f, -146f, 400f, 26f);
        RectTransform chip = Rect("LevelChip", hud);
        TopLeft(chip, 44f, -182f, 84f, 28f);
        Img(chip, Venom, _chamfer);
        p.Level = Label(chip, "Level", "УР 1", 19, Ink, TextAnchor.MiddleCenter, true);
        Stretch(p.Level.rectTransform);

        // Таймер — справа сверху (только в режиме «На время»).
        RectTransform timer = Rect("TimerPlate", hud);
        Anchor(timer, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -34f), new Vector2(300f, 120f));
        Text timerCaption = Label(timer, "Caption", Spaced("ВРЕМЯ"), 17, HudCaption, TextAnchor.UpperRight, true);
        Anchor(timerCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(300f, 24f));
        p.Timer = Label(timer, "Timer", "2:00", 72, Bone, TextAnchor.UpperRight, true);
        Anchor(p.Timer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(4f, -18f), new Vector2(300f, 90f));
        timer.gameObject.SetActive(false);
        p.TimerPlate = timer.gameObject;

        // Компас на еду — тонкое кольцо сверху по центру.
        RectTransform pointer = Rect("FoodPointer", hud);
        Anchor(pointer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(84f, 84f));
        Img(pointer, new Color(Bone.r, Bone.g, Bone.b, 0.45f), _ring);
        RectTransform pivot = Rect("ArrowPivot", pointer);
        Anchor(pivot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        RectTransform arrow = Rect("Arrow", pivot);
        Stretch(arrow);
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // спрайт смотрит вниз, разворачиваем вверх
        Image arrowImg = Img(arrow, Venom, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"));
        arrowImg.type = Image.Type.Simple;
        Text pointerLabel = Label(pointer, "Label", Spaced("ЕДА"), 15, HudCaption, TextAnchor.MiddleCenter, true);
        Anchor(pointerLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(120f, 20f));
        p.Pointer = pointer.gameObject;
        p.Arrow = pivot;

        // Активные бонусы — подпись и тонкая полоса оставшегося времени.
        p.PowerUpRows = new Object[PowerUpColors.Length];
        p.PowerUpFills = new Object[PowerUpColors.Length];
        for (int i = 0; i < PowerUpColors.Length; i++)
        {
            RectTransform row = Rect("PowerUp_" + (PowerUpType)i, hud);
            TopLeft(row, 44f, -236f - i * 44f, 220f, 36f);
            Text label = Label(row, "Label", PowerUpNames[i], 16, Bone, TextAnchor.UpperLeft, true);
            Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(220f, 22f));
            RectTransform track = Rect("Track", row);
            Anchor(track, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(220f, 2f));
            Img(track, Hair, null);
            RectTransform fillRt = Rect("Fill", row);
            Anchor(fillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f), new Vector2(220f, 4f));
            Image fill = Img(fillRt, PowerUpColors[i], null);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            row.gameObject.SetActive(false);
            p.PowerUpRows[i] = row.gameObject;
            p.PowerUpFills[i] = fill;
        }

        // Рывок — слева снизу: подпись, клавиша и полоса готовности.
        RectTransform dash = Rect("Dash", hud);
        Anchor(dash, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 44f), new Vector2(260f, 60f));
        Text dashLabel = Label(dash, "Label", Spaced("РЫВОК"), 17, HudCaption, TextAnchor.MiddleLeft, true);
        Anchor(dashLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(140f, 28f));
        RectTransform shiftKey = Keycap(dash, "SHIFT");
        Anchor(shiftKey, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, shiftKey.sizeDelta);
        RectTransform dashTrack = Rect("Track", dash);
        Anchor(dashTrack, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(260f, 3f));
        Img(dashTrack, Hair, null);
        RectTransform dashFillRt = Rect("Fill", dash);
        Anchor(dashFillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f), new Vector2(260f, 5f));
        p.DashFill = Img(dashFillRt, Venom, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));
        p.DashFill.type = Image.Type.Filled;
        p.DashFill.fillMethod = Image.FillMethod.Horizontal;
        p.DashFill.fillOrigin = 0;

        HintRow(hud, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(0.5f, 0.5f),
            ("A D", "руль"), ("SPACE", "прыжок"), ("SHIFT", "рывок"), ("ESC", "пауза"));
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
        Anchor(banner, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 130f));
        p.BannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        p.BannerGroup.alpha = 0f;
        p.BannerGroup.blocksRaycasts = false;
        p.BannerGroup.interactable = false;
        p.BannerText = Label(banner, "Text", "", 88, Bone, TextAnchor.MiddleCenter, true);
        Stretch(p.BannerText.rectTransform);
        RectTransform bannerLine = Line(banner, Venom);
        Anchor(bannerLine, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(90f, 5f));

        // Тексты висят над 3D-сценой — лёгкая тень для читаемости.
        foreach (Text t in hud.GetComponentsInChildren<Text>(true))
            if (t.color != Ink) AddShadow(t);
        return hud.gameObject;
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
            Text d = Label(content, "Description", description, 20, Smoke, TextAnchor.MiddleLeft, false);
            Anchor(d.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelX + 2f, labelY - labelSize * 0.66f), new Vector2(width - labelX, 26f));
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
