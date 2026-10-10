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
/// Весь интерфейс в едином современном стиле: тёмные полупрозрачные панели со скруглёнными углами
/// и мягкими тенями поверх живой сцены. Главное меню — панель слева с карточками режимов,
/// пауза, итоги, настройки и рекорды — модальные карточки по центру; HUD — компактные плашки.
/// Вызывается из SnakeSceneBuilder.
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
    private static readonly Color PanelColor = new Color(0.05f, 0.065f, 0.095f, 0.86f);
    private static readonly Color HudPlate = new Color(0.05f, 0.065f, 0.095f, 0.72f);
    private static readonly Color CardColor = new Color(0.075f, 0.09f, 0.13f, 0.97f);
    private static readonly Color Surface = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color Dim = new Color(0.02f, 0.03f, 0.05f, 0.55f);
    private static readonly Color Text1 = new Color(0.96f, 0.97f, 0.98f);
    private static readonly Color Text2 = new Color(0.72f, 0.76f, 0.82f);
    private static readonly Color Text3 = new Color(0.52f, 0.57f, 0.64f);
    private static readonly Color Green = new Color(0.36f, 0.84f, 0.5f);
    private static readonly Color GreenDark = new Color(0.03f, 0.15f, 0.07f);
    private static readonly Color Blue = new Color(0.42f, 0.62f, 1f);
    private static readonly Color Red = new Color(1f, 0.45f, 0.45f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.36f);

    private static readonly Color[] PowerUpColors =
    {
        new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.85f, 1f), new Color(0.45f, 0.6f, 1f),
        new Color(0.95f, 0.35f, 0.8f), new Color(1f, 0.6f, 0.2f)
    };
    private static readonly string[] PowerUpNames = { "Ускорение", "Замедление", "Щит", "Магнит", "Очки x2" };

    private static readonly string[] ModeNames = { "Классика", "На время", "Выживание" };
    private static readonly string[] ModeDescriptions = { "Бесконечно, с уровнями", "Две минуты на рекорд", "Арена сужается" };
    private static readonly Color[] ModeColors = { Green, Blue, Red };

    private static Font _font;
    private static Sprite _rounded;
    private static Sprite _pill;
    private static Sprite _circle;
    private static Sprite _shadow;

    public static void Build(Refs r)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _rounded = UISpriteFactory.Rounded;
        _pill = UISpriteFactory.Pill;
        _circle = UISpriteFactory.Circle;
        _shadow = UISpriteFactory.SoftShadow;

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

        var hud = BuildHud(root, r, out HudParts hudParts);
        var menu = BuildMainMenu(root, out MenuParts menuParts);
        var pause = BuildPause(root, out PauseParts pauseParts);
        var over = BuildGameOver(root, out GameOverParts overParts);
        var settings = BuildSettings(root, out SettingsParts settingsParts);
        var board = BuildLeaderboard(root, out Object[] columns, out Button boardBack);

        pause.SetActive(false);
        over.SetActive(false);
        settings.SetActive(false);
        board.SetActive(false);

        // ---------- Компоненты ----------
        canvasGo.AddComponent<UIFontApplier>();

        var tutorial = canvasGo.AddComponent<TutorialController>();
        SnakeSceneBuilder.Wire(tutorial, ("_game", r.Game), ("_snake", r.Player), ("_plate", hudParts.TutorialPlate), ("_text", hudParts.TutorialText));

        var settingsComp = settings.AddComponent<SettingsPanel>();
        SnakeSceneBuilder.Wire(settingsComp, ("_settings", r.Settings), ("_audio", r.Audio), ("_tutorial", tutorial),
            ("_masterSlider", settingsParts.Master), ("_musicSlider", settingsParts.Music), ("_sfxSlider", settingsParts.Sfx),
            ("_steerSlider", settingsParts.Steer), ("_fullscreenToggle", settingsParts.Fullscreen), ("_minimapToggle", settingsParts.Minimap),
            ("_rivalToggle", settingsParts.Rival), ("_qualityButton", settingsParts.Quality), ("_qualityLabel", settingsParts.QualityLabel),
            ("_tutorialButton", settingsParts.Tutorial), ("_backButton", settingsParts.Back));

        var boardComp = board.AddComponent<LeaderboardPanel>();
        SnakeSceneBuilder.Wire(boardComp, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio), ("_backButton", boardBack));
        SnakeSceneBuilder.WireArray(boardComp, "_columns", columns);

        var mainMenu = menu.AddComponent<MainMenu>();
        SnakeSceneBuilder.Wire(mainMenu, ("_leaderboard", r.Leaderboard), ("_audio", r.Audio));
        SnakeSceneBuilder.WireArray(mainMenu, "_items", menuParts.Items);
        SnakeSceneBuilder.WireArray(mainMenu, "_backgrounds", menuParts.Backgrounds);
        SnakeSceneBuilder.WireArray(mainMenu, "_accents", menuParts.Accents);
        SnakeSceneBuilder.WireArray(mainMenu, "_recordTexts", menuParts.Records);
        SnakeSceneBuilder.WireArray(mainMenu, "_overlays", new Object[] { settings, board });

        var ui = canvasGo.AddComponent<UIController>();
        SnakeSceneBuilder.Wire(ui,
            ("_game", r.Game), ("_score", r.Score), ("_snake", r.Player), ("_powerUps", r.PowerUps), ("_settings", r.Settings),
            ("_audio", r.Audio), ("_arena", r.Arena), ("_food", r.Food), ("_cameraTransform", r.CameraTransform), ("_minimapCamera", r.MinimapCamera),
            ("_hudRoot", hud), ("_scoreText", hudParts.Score), ("_bestText", hudParts.Best), ("_levelText", hudParts.Level),
            ("_timerPlate", hudParts.TimerPlate), ("_timerText", hudParts.Timer),
            ("_foodPointer", hudParts.Pointer), ("_foodArrow", hudParts.Arrow), ("_dashFill", hudParts.DashFill),
            ("_minimapRoot", hudParts.Minimap), ("_bannerGroup", hudParts.BannerGroup), ("_bannerText", hudParts.BannerText),
            ("_menuPanel", menu), ("_pausePanel", pause), ("_gameOverPanel", over),
            ("_settingsPanel", settings), ("_leaderboardPanel", board),
            ("_gameOverTitle", overParts.Title), ("_finalScoreText", overParts.Score), ("_rankText", overParts.Rank), ("_statsText", overParts.Stats),
            ("_classicButton", menuParts.Items[0]), ("_timeAttackButton", menuParts.Items[1]), ("_survivalButton", menuParts.Items[2]),
            ("_menuLeaderboardButton", menuParts.Items[3]), ("_menuSettingsButton", menuParts.Items[4]), ("_quitButton", menuParts.Items[5]),
            ("_resumeButton", pauseParts.Resume), ("_pauseRestartButton", pauseParts.Restart),
            ("_pauseSettingsButton", pauseParts.Settings), ("_pauseMenuButton", pauseParts.Menu),
            ("_restartButton", overParts.Restart), ("_gameOverMenuButton", overParts.Menu));
        SnakeSceneBuilder.WireArray(ui, "_powerUpRows", hudParts.PowerUpRows);
        SnakeSceneBuilder.WireArray(ui, "_powerUpFills", hudParts.PowerUpFills);
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

    private static GameObject BuildHud(Transform root, Refs r, out HudParts p)
    {
        p = new HudParts();
        RectTransform hud = Rect("HUD", root);
        Stretch(hud);

        RectTransform scorePlate = Plate(hud, "ScorePlate", new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(320f, 76f), HudPlate, _rounded);
        p.Score = Label(scorePlate, "Score", "Счёт: 0", 38, Text1, TextAnchor.MiddleLeft, true);
        Fill(p.Score.rectTransform, 24f);
        RectTransform levelPlate = Plate(hud, "LevelPlate", new Vector2(0f, 1f), new Vector2(32f, -114f), new Vector2(200f, 48f), HudPlate, _rounded);
        p.Level = Label(levelPlate, "Level", "Уровень 1", 26, Green, TextAnchor.MiddleLeft, true);
        Fill(p.Level.rectTransform, 24f);

        RectTransform bestPlate = Plate(hud, "BestPlate", new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(320f, 76f), HudPlate, _rounded);
        p.Best = Label(bestPlate, "Best", "Рекорд: 0", 38, Gold, TextAnchor.MiddleRight, true);
        Fill(p.Best.rectTransform, 24f);
        RectTransform timerPlate = Plate(hud, "TimerPlate", new Vector2(1f, 1f), new Vector2(-32f, -114f), new Vector2(200f, 64f), HudPlate, _rounded);
        p.Timer = Label(timerPlate, "Timer", "2:00", 44, Text1, TextAnchor.MiddleCenter, true);
        Fill(p.Timer.rectTransform, 8f);
        timerPlate.gameObject.SetActive(false);
        p.TimerPlate = timerPlate.gameObject;

        // Стрелка на еду — круглый компас сверху по центру.
        RectTransform pointer = Plate(hud, "FoodPointer", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(112f, 112f), HudPlate, _circle);
        RectTransform pivot = Rect("ArrowPivot", pointer);
        pivot.anchorMin = pivot.anchorMax = new Vector2(0.5f, 0.56f);
        pivot.sizeDelta = new Vector2(60f, 60f);
        RectTransform arrow = Rect("Arrow", pivot);
        Stretch(arrow);
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); // спрайт смотрит вниз, разворачиваем вверх
        var arrowImg = arrow.gameObject.AddComponent<Image>();
        arrowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        arrowImg.color = Red;
        arrowImg.raycastTarget = false;
        Text pointerLabel = Label(pointer, "Label", "еда", 18, Text2, TextAnchor.MiddleCenter, false);
        Place(pointerLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(100f, 24f));
        p.Pointer = pointer.gameObject;
        p.Arrow = pivot;

        // Активные бонусы — колонка под уровнем.
        p.PowerUpRows = new Object[PowerUpColors.Length];
        p.PowerUpFills = new Object[PowerUpColors.Length];
        for (int i = 0; i < PowerUpColors.Length; i++)
        {
            RectTransform row = Plate(hud, "PowerUp_" + (PowerUpType)i, new Vector2(0f, 1f), new Vector2(32f, -178f - i * 50f), new Vector2(260f, 42f), HudPlate, _rounded);
            Image fill = FillBar(row, new Color(PowerUpColors[i].r, PowerUpColors[i].g, PowerUpColors[i].b, 0.45f));
            Text label = Label(row, "Label", PowerUpNames[i], 22, Text1, TextAnchor.MiddleLeft, true);
            Fill(label.rectTransform, 16f);
            row.gameObject.SetActive(false);
            p.PowerUpRows[i] = row.gameObject;
            p.PowerUpFills[i] = fill;
        }

        RectTransform dash = Plate(hud, "DashPlate", new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(260f, 48f), HudPlate, _rounded);
        p.DashFill = FillBar(dash, new Color(0.4f, 0.8f, 1f, 0.45f));
        Text dashLabel = Label(dash, "Label", "Рывок  ·  Shift", 22, Text1, TextAnchor.MiddleLeft, true);
        Fill(dashLabel.rectTransform, 16f);

        RectTransform hint = Plate(hud, "HintPlate", new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(860f, 46f), HudPlate, _pill);
        Text hintText = Label(hint, "Hint", "A D / ← →  руль      Space  прыжок      Shift  рывок      Esc  пауза", 21, Text2, TextAnchor.MiddleCenter, false);
        Fill(hintText.rectTransform, 12f);

        RectTransform minimap = Plate(hud, "Minimap", new Vector2(1f, 0f), new Vector2(-32f, 28f), new Vector2(280f, 280f), HudPlate, _rounded);
        RectTransform mapImage = Rect("MinimapImage", minimap);
        Stretch(mapImage);
        mapImage.offsetMin = new Vector2(10f, 10f);
        mapImage.offsetMax = new Vector2(-10f, -10f);
        var raw = mapImage.gameObject.AddComponent<RawImage>();
        raw.texture = r.MinimapTexture;
        raw.raycastTarget = false;
        p.Minimap = minimap.gameObject;

        RectTransform tutorial = Plate(hud, "TutorialPlate", new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1100f, 60f), new Color(0.1f, 0.32f, 0.18f, 0.9f), _pill);
        p.TutorialText = Label(tutorial, "Text", "", 26, Text1, TextAnchor.MiddleCenter, true);
        Fill(p.TutorialText.rectTransform, 24f);
        tutorial.gameObject.SetActive(false);
        p.TutorialPlate = tutorial.gameObject;

        RectTransform banner = Rect("Banner", hud);
        Place(banner, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1200f, 110f));
        p.BannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        p.BannerGroup.alpha = 0f;
        p.BannerGroup.blocksRaycasts = false;
        p.BannerGroup.interactable = false;
        p.BannerText = Label(banner, "Text", "", 64, Gold, TextAnchor.MiddleCenter, true);
        Stretch(p.BannerText.rectTransform);

        // Текст HUD висит над 3D-сценой — лёгкая тень для читаемости на светлом фоне.
        foreach (Text t in hud.GetComponentsInChildren<Text>(true)) AddShadow(t, 0.45f);
        return hud.gameObject;
    }

    // =====================================================================
    // Главное меню: панель слева поверх живой сцены
    // =====================================================================

    private sealed class MenuParts
    {
        public Button[] Items;
        public Object[] Backgrounds, Accents, Records;
    }

    private static GameObject BuildMainMenu(Transform root, out MenuParts p)
    {
        p = new MenuParts { Items = new Button[6], Backgrounds = new Object[6], Accents = new Object[6], Records = new Object[3] };

        RectTransform menu = Rect("MenuPanel", root);
        Stretch(menu);

        RectTransform side = Rect("SidePanel", menu);
        side.anchorMin = new Vector2(0f, 0f);
        side.anchorMax = new Vector2(0f, 1f);
        side.pivot = new Vector2(0f, 0.5f);
        side.sizeDelta = new Vector2(640f, 0f);
        Img(side, PanelColor, null).raycastTarget = true;

        RectTransform edge = Rect("Edge", side);
        edge.anchorMin = new Vector2(1f, 0f);
        edge.anchorMax = new Vector2(1f, 1f);
        edge.pivot = new Vector2(1f, 0.5f);
        edge.sizeDelta = new Vector2(1f, 0f);
        Img(edge, new Color(1f, 1f, 1f, 0.08f), null);

        const float left = 72f;
        TopLeft(Label(side, "Overline", "3D  АРКАДА", 22, Green, TextAnchor.MiddleLeft, true), left, -118f, 480f, 30f);
        TopLeft(Label(side, "Title", "Змейка", 116, Text1, TextAnchor.MiddleLeft, true), left - 6f, -146f, 540f, 140f);
        TopLeft(Label(side, "Tagline", "Свободное движение, прыжки и рывки", 26, Text2, TextAnchor.MiddleLeft, false), left, -290f, 520f, 36f);
        TopLeft(Label(side, "Section", "ВЫБЕРИТЕ РЕЖИМ", 19, Text3, TextAnchor.MiddleLeft, true), left, -372f, 480f, 26f);

        // Карточки режимов.
        for (int i = 0; i < 3; i++)
        {
            RectTransform card = Rect("Mode_" + ModeNames[i], side);
            TopLeft(card, left, -410f - i * 118f, 496f, 104f);
            Image bg = Img(card, new Color(1f, 1f, 1f, 0.06f), _rounded);
            bg.raycastTarget = true;
            Button button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            card.gameObject.AddComponent<MenuItemHover>();

            RectTransform accent = Rect("Accent", card);
            Place(accent, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 56f));
            accent.pivot = new Vector2(0f, 0.5f);
            Img(accent, ModeColors[i], _pill);
            accent.gameObject.SetActive(false);

            RectTransform dot = Rect("Dot", card);
            Place(dot, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(16f, 16f));
            Img(dot, ModeColors[i], _circle);

            Text name = Label(card, "Name", ModeNames[i], 34, Text1, TextAnchor.MiddleLeft, true);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, 16f), new Vector2(300f, 44f));
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text desc = Label(card, "Description", ModeDescriptions[i], 21, Text2, TextAnchor.MiddleLeft, false);
            Place(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, -20f), new Vector2(300f, 30f));
            desc.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text record = Label(card, "Record", "", 20, Text2, TextAnchor.MiddleRight, false);
            Place(record.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(180f, 30f));
            record.rectTransform.pivot = new Vector2(1f, 0.5f);

            p.Items[i] = button;
            p.Backgrounds[i] = bg;
            p.Accents[i] = accent.gameObject;
            p.Records[i] = record;
        }

        // Нижний ряд: рекорды, настройки, выход.
        string[] names = { "Рекорды", "Настройки", "Выйти" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform b = Rect("Button_" + names[i], side);
            TopLeft(b, left + i * 170f, -778f, 156f, 60f);
            Image bg = Img(b, new Color(1f, 1f, 1f, 0.08f), _rounded);
            bg.raycastTarget = true;
            Button button = b.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            b.gameObject.AddComponent<MenuItemHover>();
            Text label = Label(b, "Label", names[i], 24, i == 2 ? Red : Text1, TextAnchor.MiddleCenter, true);
            Stretch(label.rectTransform);

            p.Items[3 + i] = button;
            p.Backgrounds[3 + i] = bg;
        }

        Text footer = Label(side, "Footer", "↑ ↓  выбор        Enter  играть", 21, Text2, TextAnchor.MiddleLeft, false);
        Place(footer.rectTransform, new Vector2(0f, 0f), new Vector2(left, 52f), new Vector2(480f, 30f));
        footer.rectTransform.pivot = new Vector2(0f, 0.5f);

        Text version = Label(menu, "Version", "Змейка 3D  ·  v1.2", 18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.LowerRight, false);
        Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(400f, 28f));
        version.rectTransform.pivot = new Vector2(1f, 0f);
        AddShadow(version, 0.5f);
        return menu.gameObject;
    }

    // =====================================================================
    // Пауза
    // =====================================================================

    private sealed class PauseParts
    {
        public Button Resume, Restart, Settings, Menu;
    }

    private static GameObject BuildPause(Transform root, out PauseParts p)
    {
        p = new PauseParts();
        RectTransform card = Modal(root, "PausePanel", new Vector2(520f, 560f), out GameObject panel);
        TopCenter(Label(card, "Title", "Пауза", 56, Text1, TextAnchor.MiddleCenter, true), -44f, 440f, 70f);
        TopCenter(Label(card, "Subtitle", "Игра остановлена", 24, Text2, TextAnchor.MiddleCenter, false), -116f, 440f, 34f);
        p.Resume = PrimaryButton(card, "Resume", "Продолжить", -184f, 420f);
        p.Restart = SecondaryButton(card, "Restart", "Заново", -266f, 420f, Text1);
        p.Settings = SecondaryButton(card, "Settings", "Настройки", -348f, 420f, Text1);
        p.Menu = SecondaryButton(card, "Menu", "В главное меню", -430f, 420f, Text1);
        return panel;
    }

    // =====================================================================
    // Итоги партии
    // =====================================================================

    private sealed class GameOverParts
    {
        public Text Title, Score, Rank, Stats;
        public Button Restart, Menu;
    }

    private static GameObject BuildGameOver(Transform root, out GameOverParts p)
    {
        p = new GameOverParts();
        RectTransform card = Modal(root, "GameOverPanel", new Vector2(620f, 740f), out GameObject panel);
        p.Rank = Label(card, "Rank", "", 22, Gold, TextAnchor.MiddleCenter, true);
        TopCenter(p.Rank, -40f, 560f, 30f);
        p.Title = Label(card, "Title", "Игра окончена", 46, Text1, TextAnchor.MiddleCenter, true);
        TopCenter(p.Title, -74f, 560f, 60f);
        p.Score = Label(card, "Score", "0", 112, Text1, TextAnchor.MiddleCenter, true);
        TopCenter(p.Score, -138f, 560f, 130f);
        TopCenter(Label(card, "ScoreCaption", "очков", 22, Text3, TextAnchor.MiddleCenter, false), -262f, 560f, 30f);

        RectTransform stats = Rect("Stats", card);
        TopCenterRect(stats, -312f, 520f, 236f);
        Img(stats, Surface, _rounded);
        Text labels = Label(stats, "Labels", "Длина\nЯблоки\nБонусы\nУровень\nВремя", 25, Text2, TextAnchor.MiddleLeft, false);
        labels.lineSpacing = 1.3f;
        Stretch(labels.rectTransform);
        labels.rectTransform.offsetMin = new Vector2(32f, 0f);
        p.Stats = Label(stats, "Values", "", 25, Text1, TextAnchor.MiddleRight, true);
        p.Stats.lineSpacing = 1.3f;
        Stretch(p.Stats.rectTransform);
        p.Stats.rectTransform.offsetMax = new Vector2(-32f, 0f);

        p.Restart = PrimaryButton(card, "Restart", "Ещё раз", -578f, 250f);
        p.Restart.GetComponent<RectTransform>().anchoredPosition = new Vector2(-133f, -578f);
        p.Menu = SecondaryButton(card, "Menu", "В меню", -578f, 250f, Text1);
        p.Menu.GetComponent<RectTransform>().anchoredPosition = new Vector2(133f, -578f);
        TopCenter(Label(card, "Hint", "R или Enter — ещё раз", 19, Text3, TextAnchor.MiddleCenter, false), -672f, 560f, 28f);
        return panel;
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

    private static GameObject BuildSettings(Transform root, out SettingsParts p)
    {
        p = new SettingsParts();
        RectTransform card = Modal(root, "SettingsPanel", new Vector2(760f, 880f), out GameObject panel);
        TopCenter(Label(card, "Title", "Настройки", 48, Text1, TextAnchor.MiddleCenter, true), -36f, 680f, 64f);

        Section(card, "Звук", -128f);
        p.Master = SliderRow(card, "Общая громкость", -168f, 0f, 1f);
        p.Music = SliderRow(card, "Музыка", -228f, 0f, 1f);
        p.Sfx = SliderRow(card, "Звуки", -288f, 0f, 1f);

        Section(card, "Управление", -356f);
        p.Steer = SliderRow(card, "Чувствительность руля", -396f, 0.5f, 1.5f);

        Section(card, "Экран и игра", -464f);
        p.Fullscreen = SwitchRow(card, "Полный экран  (F11)", -504f);
        p.Minimap = SwitchRow(card, "Мини-карта", -564f);
        p.Rival = SwitchRow(card, "Змея-соперник  <size=19><color=#85919F>со следующей партии</color></size>", -624f);

        RowLabel(card, "Качество графики", -700f);
        p.Quality = SecondaryButton(card, "Quality", "Качество", -700f, 240f, Text1);
        RectTransform q = p.Quality.GetComponent<RectTransform>();
        q.anchorMin = q.anchorMax = new Vector2(1f, 1f);
        q.pivot = new Vector2(1f, 0.5f);
        q.anchoredPosition = new Vector2(-56f, -700f);
        q.sizeDelta = new Vector2(240f, 52f);
        p.QualityLabel = p.Quality.GetComponentInChildren<Text>();
        p.QualityLabel.fontSize = 22;

        p.Tutorial = SecondaryButton(card, "Tutorial", "Показать обучение", -784f, 310f, Text1);
        p.Tutorial.GetComponent<RectTransform>().anchoredPosition = new Vector2(-170f, -784f);
        p.Back = PrimaryButton(card, "Done", "Готово", -784f, 310f);
        p.Back.GetComponent<RectTransform>().anchoredPosition = new Vector2(170f, -784f);
        return panel;
    }

    private static void Section(RectTransform card, string text, float y)
    {
        Text t = Label(card, "Section_" + text, text.ToUpperInvariant(), 20, Text3, TextAnchor.MiddleLeft, true);
        Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(56f, y), new Vector2(500f, 26f));
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    private static void RowLabel(RectTransform card, string text, float y)
    {
        Text t = Label(card, "Label", text, 26, Text1, TextAnchor.MiddleLeft, false);
        Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(56f, y), new Vector2(380f, 40f));
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    private static Slider SliderRow(RectTransform card, string label, float y, float min, float max)
    {
        RowLabel(card, label, y);
        RectTransform rt = Rect("Slider", card);
        Place(rt, new Vector2(1f, 1f), new Vector2(-56f, y), new Vector2(280f, 30f));
        rt.pivot = new Vector2(1f, 0.5f);
        var slider = rt.gameObject.AddComponent<Slider>();

        RectTransform track = Rect("Background", rt);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 8f);
        Img(track, new Color(1f, 1f, 1f, 0.12f), _pill);

        RectTransform fillArea = Rect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-14f, 8f);
        RectTransform fill = Rect("Fill", fillArea);
        fill.sizeDelta = new Vector2(14f, 0f);
        Img(fill, Green, _pill);

        RectTransform handleArea = Rect("Handle Slide Area", rt);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(13f, 0f);
        handleArea.offsetMax = new Vector2(-13f, 0f);
        RectTransform handle = Rect("Handle", handleArea);
        // Slider растягивает ручку на всю высоту, поэтому высоту задаём «минус 4» от 30 = 26.
        handle.sizeDelta = new Vector2(26f, -4f);
        Image handleImg = Img(handle, Color.white, _circle);
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

    private static Toggle SwitchRow(RectTransform card, string label, float y)
    {
        RowLabel(card, label, y);
        RectTransform rt = Rect("Switch", card);
        Place(rt, new Vector2(1f, 1f), new Vector2(-56f, y), new Vector2(60f, 32f));
        rt.pivot = new Vector2(1f, 0.5f);
        Image track = Img(rt, Green, _pill);
        track.raycastTarget = true;

        RectTransform knob = Rect("Knob", rt);
        Place(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Img(knob, Color.white, _circle);

        var toggle = rt.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.isOn = true;

        var view = rt.gameObject.AddComponent<ToggleSwitch>();
        SnakeSceneBuilder.Wire(view, ("_track", track), ("_knob", knob));
        return toggle;
    }

    // =====================================================================
    // Рекорды
    // =====================================================================

    private static GameObject BuildLeaderboard(Transform root, out Object[] columns, out Button back)
    {
        RectTransform card = Modal(root, "LeaderboardPanel", new Vector2(1120f, 780f), out GameObject panel);
        TopCenter(Label(card, "Title", "Рекорды", 48, Text1, TextAnchor.MiddleCenter, true), -36f, 1000f, 64f);

        columns = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform col = Rect("Column_" + ModeNames[i], card);
            TopCenterRect(col, -124f, 330f, 540f);
            col.anchoredPosition = new Vector2(-350f + i * 350f, -124f);
            Img(col, Surface, _rounded);

            RectTransform dot = Rect("Dot", col);
            Place(dot, new Vector2(0f, 1f), new Vector2(32f, -40f), new Vector2(14f, 14f));
            Img(dot, ModeColors[i], _circle);
            Text header = Label(col, "Header", ModeNames[i], 28, Text1, TextAnchor.MiddleLeft, true);
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(54f, -40f), new Vector2(250f, 40f));
            header.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text rows = Label(col, "Rows", "", 23, Text2, TextAnchor.UpperLeft, false);
            rows.lineSpacing = 1.25f;
            Stretch(rows.rectTransform);
            rows.rectTransform.offsetMin = new Vector2(32f, 24f);
            rows.rectTransform.offsetMax = new Vector2(-24f, -80f);
            columns[i] = rows;
        }

        back = PrimaryButton(card, "Done", "Готово", -690f, 300f);
        return panel;
    }

    // =====================================================================
    // Строительные блоки
    // =====================================================================

    /// <summary>Затемнение экрана, мягкая тень и скруглённая карточка по центру. Возвращает карточку.</summary>
    private static RectTransform Modal(Transform root, string name, Vector2 size, out GameObject panel)
    {
        RectTransform dim = Rect(name, root);
        Stretch(dim);
        Img(dim, Dim, null).raycastTarget = true;

        RectTransform shadow = Rect("Shadow", dim);
        Place(shadow, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), size + new Vector2(90f, 90f));
        Img(shadow, new Color(0f, 0f, 0f, 0.6f), _shadow);

        RectTransform card = Rect("Card", dim);
        Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Img(card, CardColor, _rounded).raycastTarget = true;

        panel = dim.gameObject;
        return card;
    }

    private static Button PrimaryButton(RectTransform card, string name, string text, float y, float width)
    {
        Button b = MakeButton(card, name, y, width, Green);
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        c.highlightedColor = Color.white;
        c.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        c.selectedColor = c.normalColor;
        c.colorMultiplier = 1.1f;
        b.colors = c;
        Text label = Label(b.transform, "Label", text, 28, GreenDark, TextAnchor.MiddleCenter, true);
        Stretch(label.rectTransform);
        return b;
    }

    /// <summary>Полупрозрачная кнопка: множитель 2 даёт «подсветку» альфы при наведении.</summary>
    private static Button SecondaryButton(RectTransform card, string name, string text, float y, float width, Color textColor)
    {
        Button b = MakeButton(card, name, y, width, new Color(1f, 1f, 1f, 0.08f));
        ColorBlock c = b.colors;
        c.colorMultiplier = 2f;
        c.normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        c.highlightedColor = new Color(1f, 1f, 1f, 1f);
        c.pressedColor = new Color(0.75f, 0.75f, 0.75f, 0.75f);
        c.selectedColor = c.normalColor;
        b.colors = c;
        Text label = Label(b.transform, "Label", text, 26, textColor, TextAnchor.MiddleCenter, true);
        Stretch(label.rectTransform);
        return b;
    }

    private static Button MakeButton(RectTransform card, string name, float y, float width, Color color)
    {
        RectTransform rt = Rect(name, card);
        TopCenterRect(rt, y, width, 66f);
        Image img = Img(rt, color, _rounded);
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        return b;
    }

    private static RectTransform Plate(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
    {
        RectTransform rt = Rect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Img(rt, color, sprite);
        return rt;
    }

    /// <summary>Полоса заполнения внутри плашки (Image типа Filled, слева направо).</summary>
    private static Image FillBar(RectTransform parent, Color color)
    {
        RectTransform rt = Rect("Fill", parent);
        Stretch(rt);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        Image img = Img(rt, color, _rounded);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        return img;
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

    private static Text Label(Transform parent, string name, string value, int size, Color color, TextAnchor align, bool bold)
    {
        RectTransform rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _font;
        t.text = value;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void AddShadow(Text text, float alpha)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
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

    /// <summary>Растянуть по родителю с горизонтальными отступами.</summary>
    private static void Fill(RectTransform rt, float padding)
    {
        Stretch(rt);
        rt.offsetMin = new Vector2(padding, 0f);
        rt.offsetMax = new Vector2(-padding, 0f);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Привязка к левому верхнему углу родителя.</summary>
    private static void TopLeft(Component c, float x, float y, float w, float h)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void TopCenter(Text t, float y, float w, float h)
    {
        TopCenterRect(t.rectTransform, y, w, h);
    }

    /// <summary>Привязка к верхнему краю по центру; y — отступ верхней кромки (отрицательный).</summary>
    private static void TopCenterRect(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
