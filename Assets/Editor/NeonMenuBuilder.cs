using Snake3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню «Неоновый закат» в духе synthwave: градиентное небо со звёздами, полосатое солнце,
/// силуэт гор, неоновая сетка-пол с бегущими линиями, розовый заголовок и список пунктов.
/// Текстуры рисуются кодом. Вызывается из SnakeSceneBuilder.
/// </summary>
internal static class NeonMenuBuilder
{
    private const string FOLDER = "Assets/Materials/UI";

    // Высота горизонта от низа экрана в пикселях канваса 1920x1080.
    private const float HORIZON = 400f;
    private const int GRID_LINES = 14;

    private static readonly Color Pink = new Color(0.96f, 0.75f, 0.82f);
    private static readonly Color HotPink = new Color(0.93f, 0.58f, 0.69f);
    private static readonly Color Lavender = new Color(0.81f, 0.8f, 0.96f);
    private static readonly Color Cyan = new Color(0.36f, 0.79f, 0.65f);
    private static readonly Color Floor = new Color(0.09f, 0.06f, 0.24f);

    private static readonly string[] ItemNames = { "Классика", "На время", "Выживание", "Рекорды", "Настройки", "Выйти" };

    public static RectTransform Build(Transform root, out Button classic, out Button timeAttack, out Button survival,
        out Button leaderboard, out Button settings, out Button quit, out NeonMenu menu)
    {
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets/Materials", "UI");
        Texture2D sky = SnakeSceneBuilder.SaveTexture(SkyTexture(), FOLDER + "/MenuSky.asset");
        Texture2D sun = SnakeSceneBuilder.SaveTexture(SunTexture(), FOLDER + "/MenuSun.asset");
        Texture2D mountains = SnakeSceneBuilder.SaveTexture(MountainTexture(), FOLDER + "/MenuMountains.asset");
        Texture2D grid = SnakeSceneBuilder.SaveTexture(GridTexture(), FOLDER + "/MenuGrid.asset");

        RectTransform panel = SnakeSceneBuilder.CreateRect("MenuPanel", root);
        SnakeSceneBuilder.Stretch(panel);

        // Небо и звёзды.
        RectTransform skyRect = AddRaw(panel, "Sky", sky);
        SnakeSceneBuilder.Stretch(skyRect);
        var rng = new System.Random(3);
        for (int i = 0; i < 70; i++)
        {
            RectTransform star = SnakeSceneBuilder.CreateRect("Star", panel);
            var anchor = new Vector2((float)rng.NextDouble(), 0.45f + (float)rng.NextDouble() * 0.55f);
            star.anchorMin = anchor;
            star.anchorMax = anchor;
            float size = 2f + (float)rng.NextDouble() * 3f;
            star.sizeDelta = new Vector2(size, size);
            var img = star.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.95f, 1f, 0.3f + (float)rng.NextDouble() * 0.6f);
            img.raycastTarget = false;
        }

        // Солнце садится за горы: его низ закрывают горы и пол, нарисованные позже.
        RectTransform sunRect = AddRaw(panel, "Sun", sun);
        sunRect.anchorMin = sunRect.anchorMax = new Vector2(0.5f, 0f);
        sunRect.anchoredPosition = new Vector2(0f, HORIZON + 140f);
        sunRect.sizeDelta = new Vector2(560f, 560f);

        RectTransform mountainRect = AddRaw(panel, "Mountains", mountains);
        mountainRect.anchorMin = new Vector2(0f, 0f);
        mountainRect.anchorMax = new Vector2(1f, 0f);
        mountainRect.pivot = new Vector2(0.5f, 0f);
        mountainRect.anchoredPosition = new Vector2(0f, HORIZON - 2f);
        mountainRect.sizeDelta = new Vector2(0f, 230f);

        // Пол: тёмная плоскость, перспективные «рельсы» и бегущие поперечные линии.
        RectTransform floor = SnakeSceneBuilder.CreateRect("Floor", panel);
        floor.anchorMin = new Vector2(0f, 0f);
        floor.anchorMax = new Vector2(1f, 0f);
        floor.pivot = new Vector2(0.5f, 0f);
        floor.sizeDelta = new Vector2(0f, HORIZON);
        var floorImg = floor.gameObject.AddComponent<Image>();
        floorImg.color = Floor;
        floorImg.raycastTarget = false;
        RectTransform rails = AddRaw(floor, "Rails", grid);
        SnakeSceneBuilder.Stretch(rails);

        RectTransform linesRoot = SnakeSceneBuilder.CreateRect("GridLines", floor);
        SnakeSceneBuilder.Stretch(linesRoot);
        var lines = new Object[GRID_LINES];
        for (int i = 0; i < GRID_LINES; i++)
        {
            RectTransform line = SnakeSceneBuilder.CreateRect("Line", linesRoot);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(0f, 3f);
            var img = line.gameObject.AddComponent<Image>();
            img.color = HotPink;
            img.raycastTarget = false;
            lines[i] = line;
        }

        RectTransform horizon = SnakeSceneBuilder.CreateRect("Horizon", panel);
        horizon.anchorMin = new Vector2(0f, 0f);
        horizon.anchorMax = new Vector2(1f, 0f);
        horizon.anchoredPosition = new Vector2(0f, HORIZON);
        horizon.sizeDelta = new Vector2(0f, 3f);
        var horizonImg = horizon.gameObject.AddComponent<Image>();
        horizonImg.color = HotPink;
        horizonImg.raycastTarget = false;

        // Заголовок: розовый текст с бирюзовой «тенью» — сдвинутый второй цвет, как на ретро-постерах.
        Text title = SnakeSceneBuilder.CreateText(panel, "Title", "З М Е Й К А   3 D", 128, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1800f, 170f));
        title.color = Pink;
        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = Cyan;
        titleShadow.effectDistance = new Vector2(6f, -6f);

        Text subtitle = SnakeSceneBuilder.CreateText(panel, "Subtitle", "свободное движение  ·  прыжки  ·  рывки", 30, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -265f), new Vector2(1400f, 50f));
        subtitle.color = new Color(0.98f, 0.95f, 1f);
        SoftOutline(subtitle);

        // Пункты меню списком слева.
        var buttons = new Button[ItemNames.Length];
        var labels = new Object[ItemNames.Length];
        for (int i = 0; i < ItemNames.Length; i++)
        {
            RectTransform item = SnakeSceneBuilder.CreateRect("Item_" + i, panel);
            item.anchorMin = item.anchorMax = new Vector2(0f, 0f);
            item.pivot = new Vector2(0f, 0.5f);
            item.anchoredPosition = new Vector2(150f, 720f - i * 78f);
            item.sizeDelta = new Vector2(720f, 72f);

            // Прозрачная подложка нужна только для попадания мышью.
            var hit = item.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = item.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            item.gameObject.AddComponent<MenuItemHover>();

            Text label = SnakeSceneBuilder.CreateText(item, "Label", ItemNames[i], 54, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(720f, 72f));
            label.color = Lavender;
            var glow = label.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(0.6f, 0.2f, 0.45f, 0.8f);
            glow.effectDistance = new Vector2(3f, -3f);

            buttons[i] = button;
            labels[i] = label;
        }

        Text record = SnakeSceneBuilder.CreateText(panel, "Record", "", 32, TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(-70f, 60f), new Vector2(1000f, 50f));
        record.color = Pink;
        SoftOutline(record);

        Text hint = SnakeSceneBuilder.CreateText(panel, "Hint", "↑ ↓ — выбор      Enter — выбрать      мышь тоже работает", 24, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 40f));
        hint.color = new Color(0.95f, 0.93f, 1f, 0.9f);
        SoftOutline(hint);

        classic = buttons[0];
        timeAttack = buttons[1];
        survival = buttons[2];
        leaderboard = buttons[3];
        settings = buttons[4];
        quit = buttons[5];

        menu = panel.gameObject.AddComponent<NeonMenu>();
        SnakeSceneBuilder.WireArray(menu, "_items", buttons);
        SnakeSceneBuilder.WireArray(menu, "_labels", labels);
        SnakeSceneBuilder.WireArray(menu, "_gridLines", lines);
        SnakeSceneBuilder.Wire(menu, ("_recordText", record));
        SnakeSceneBuilder.SetFloat(menu, "_floorHeight", HORIZON);
        return panel;
    }

    /// <summary>Тонкая фиолетовая обводка вместо толстой чёрной: мелкий текст на неоне не «мылится».</summary>
    private static void SoftOutline(Text text)
    {
        var outline = text.GetComponent<Outline>();
        if (outline == null) return;
        outline.effectColor = new Color(0.08f, 0.04f, 0.2f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static RectTransform AddRaw(Transform parent, string name, Texture texture)
    {
        RectTransform rt = SnakeSceneBuilder.CreateRect(name, parent);
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        return rt;
    }

    // ===================== Текстуры =====================

    /// <summary>Небо: малиновый у горизонта → фиолетовый → почти чёрный вверху.</summary>
    private static Texture2D SkyTexture()
    {
        const int h = 256;
        var tex = NewTexture(4, h, "MenuSky");
        var horizon = new Color(0.6f, 0.21f, 0.34f);
        var mid = new Color(0.24f, 0.2f, 0.54f);
        var top = new Color(0.05f, 0.04f, 0.15f);
        float horizonV = HORIZON / 1080f;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            float k = Mathf.InverseLerp(horizonV, 1f, v);
            Color c = k < 0.35f ? Color.Lerp(horizon, mid, k / 0.35f) : Color.Lerp(mid, top, (k - 0.35f) / 0.65f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Солнце: жёлто-розовый градиент, нижняя половина разрезана полосами, которые к низу шире.</summary>
    private static Texture2D SunTexture()
    {
        const int size = 256;
        var tex = NewTexture(size, size, "MenuSun");
        var topColor = new Color(0.98f, 0.78f, 0.46f);
        var bottomColor = new Color(0.83f, 0.33f, 0.49f);
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                float alpha = Mathf.Clamp01((0.5f - d) * size * 0.5f); // мягкий край
                if (v < 0.48f)
                {
                    float gap = Mathf.Lerp(0.55f, 0.08f, v / 0.48f);
                    if (Mathf.Repeat(v * 13f, 1f) < gap) alpha = 0f;
                }
                Color c = Color.Lerp(bottomColor, topColor, v);
                c.a = alpha;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Силуэт гор с розовой кромкой по гребню.</summary>
    private static Texture2D MountainTexture()
    {
        const int w = 512, h = 128;
        var tex = NewTexture(w, h, "MenuMountains");
        var body = new Color(0.16f, 0.12f, 0.37f);
        var rim = HotPink;
        for (int x = 0; x < w; x++)
        {
            float u = (float)x / w;
            float ridge = 0.25f + 0.55f * ProceduralTextures.Fbm(u * 6f, 0.5f, 4, 1.3f) + 0.25f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)), 3f);
            float top = Mathf.Clamp01(ridge) * h;
            for (int y = 0; y < h; y++)
            {
                Color c;
                if (y > top) c = new Color(0f, 0f, 0f, 0f);
                else if (top - y < 2.5f) c = rim;
                else c = Color.Lerp(body * 0.7f, body, y / top);
                if (c.a > 0f) c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Продольные линии пола, сходящиеся к точке на горизонте (перспектива).</summary>
    private static Texture2D GridTexture()
    {
        const int w = 512, h = 256;
        var tex = NewTexture(w, h, "MenuGrid");
        var lineColor = new Color(0.69f, 0.66f, 0.93f);
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h; // 0 — низ экрана, 1 — горизонт
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float best = 1f;
                for (int k = -18; k <= 18; k++)
                {
                    float bottomX = 0.5f + k * 0.065f;
                    float lineX = Mathf.Lerp(bottomX, 0.5f + k * 0.004f, v);
                    best = Mathf.Min(best, Mathf.Abs(u - lineX) * w);
                }
                float alpha = Mathf.Clamp01(1.4f - best) * Mathf.Lerp(0.9f, 0.25f, v);
                tex.SetPixel(x, y, new Color(lineColor.r, lineColor.g, lineColor.b, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }
}
