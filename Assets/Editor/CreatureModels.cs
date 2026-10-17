using System.Collections.Generic;
using UnityEngine;
using Ring = MeshDraft.Ring;

/// <summary>
/// Меши существ из кода: гладкие лофты вместо наборов сфер и кубов. Одна подвижная часть — один меш с подмешами
/// по материалам (тело лисы, каждая лапа, хвост; крыло птицы). Меши кэшируются на сборку и сохраняются ассетами,
/// поэтому все копии из пулов делят один меш. Вперёд — +Z, вверх — +Y; размеры прежние, чтобы не трогать логику.
/// </summary>
public static class CreatureModels
{
    private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

    /// <summary>Сбросить кэш в начале сборки сцены.</summary>
    public static void Reset() => _cache.Clear();

    private static Mesh Cached(string name, System.Func<Mesh> make)
    {
        if (_cache.TryGetValue(name, out Mesh m) && m != null) return m;
        m = MeshKit.Save(make(), name);
        _cache[name] = m;
        return m;
    }

    // ---------------- Помощники ----------------

    /// <summary>Гладкая кривая профиля по парам (t, значение).</summary>
    private static System.Func<float, float> Curve(params float[] kv)
    {
        var keys = new Keyframe[kv.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(kv[i * 2], kv[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
        return c.Evaluate;
    }

    /// <summary>Лофт вдоль сглаженной оси с отдельными профилями ширины и высоты; можно взять только часть [t0, t1].</summary>
    private static void Body(MeshDraft d, Vector3[] spine, System.Func<float, float> rx, System.Func<float, float> ry,
        int rings, int sides, Vector3 up, float t0 = 0f, float t1 = 1f, bool capStart = true, bool capEnd = true,
        System.Func<float, float, float> shape = null)
    {
        var list = new List<Ring>(rings + 1);
        for (int i = 0; i <= rings; i++)
        {
            float t = Mathf.Lerp(t0, t1, (float)i / rings);
            list.Add(new Ring(MeshDraft.Spline(spine, t), Mathf.Max(0f, rx(t)), Mathf.Max(0f, ry(t))));
        }
        d.Loft(list, sides, up, capStart, capEnd, shape != null ? (a, u) => shape(a, Mathf.Lerp(t0, t1, u)) : (System.Func<float, float, float>)null);
    }

    /// <summary>Простая трубка: радиус линейно от r0 до r1 (r1 = 0 — острый конец).</summary>
    private static void Limb(MeshDraft d, Vector3[] pts, float r0, float r1, int sides, Vector3 up, int rings = 6, float flatten = 1f)
    {
        d.Tube(pts, rings, sides, t => new Vector2(Mathf.Lerp(r0, r1, t), Mathf.Lerp(r0, r1, t) * flatten), up);
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private static Vector2[] Oval(float rx, float ry, int n, float pinch = 0f)
    {
        var o = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float y = Mathf.Sin(a) * ry;
            o[i] = new Vector2(Mathf.Cos(a) * rx * (1f - pinch * Mathf.Max(0f, Mathf.Sin(a))), y);
        }
        return o;
    }

    // ===================== Лиса =====================
    // Подмеши тела: 0 мех, 1 белый, 2 тёмный, 3 глаз; лапы: 0 мех, 1 тёмный; хвоста: 0 мех, 1 белый.

    public static Mesh FoxBody() => Cached("FoxBody", () =>
    {
        var d = new MeshDraft(4);
        Vector3[] spine = { V(0, 0.5f, -0.5f), V(0, 0.54f, -0.15f), V(0, 0.53f, 0.15f), V(0, 0.55f, 0.38f) };
        Body(d, spine, Curve(0, 0.02f, 0.1f, 0.12f, 0.28f, 0.155f, 0.55f, 0.135f, 0.82f, 0.155f, 1f, 0.12f),
            Curve(0, 0.02f, 0.1f, 0.13f, 0.28f, 0.17f, 0.55f, 0.15f, 0.82f, 0.185f, 1f, 0.13f), 16, 14, Vector3.up, capEnd: false);
        // Шея к голове.
        Body(d, new[] { V(0, 0.55f, 0.34f), V(0, 0.63f, 0.5f), V(0, 0.73f, 0.6f) }, Curve(0, 0.12f, 1, 0.1f), Curve(0, 0.13f, 1, 0.1f), 6, 12, Vector3.up, capStart: false);
        d.Ellipsoid(V(0, 0.755f, 0.625f), V(0.14f, 0.12f, 0.14f), Quaternion.identity, 14, 10);
        // Морда: рыжая спинка носа, белая нижняя челюсть и щёки.
        Body(d, new[] { V(0, 0.75f, 0.67f), V(0, 0.72f, 0.8f), V(0, 0.695f, 0.915f) }, Curve(0, 0.08f, 1, 0.026f), Curve(0, 0.07f, 1, 0.022f), 6, 10, Vector3.up);
        d.Sub = 1;
        Body(d, new[] { V(0, 0.69f, 0.66f), V(0, 0.67f, 0.78f), V(0, 0.665f, 0.87f) }, Curve(0, 0.07f, 1, 0.02f), Curve(0, 0.045f, 1, 0.015f), 6, 10, Vector3.up);
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.075f * s, 0.7f, 0.68f), V(0.075f, 0.065f, 0.085f), Quaternion.Euler(0, 20 * s, 0), 10, 8);
        // Белая грудь и брюхо.
        d.Ellipsoid(V(0, 0.47f, 0.32f), V(0.115f, 0.15f, 0.13f), Quaternion.Euler(-20, 0, 0), 12, 8);
        d.Ellipsoid(V(0, 0.425f, 0.02f), V(0.1f, 0.07f, 0.3f), Quaternion.identity, 12, 6);
        // Уши: рыжее основание, тёмные кончики; светлая изнанка.
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3[] ear = { V(0.07f * s, 0.83f, 0.6f), V(0.095f * s, 0.93f, 0.59f), V(0.115f * s, 1.02f, 0.575f) };
            System.Func<float, float> w = Curve(0, 0.058f, 0.5f, 0.045f, 1, 0f);
            System.Func<float, float> th = Curve(0, 0.024f, 1, 0f);
            d.Sub = 0;
            Body(d, ear, w, th, 4, 8, Vector3.forward, 0f, 0.55f, true, false);
            d.Sub = 2;
            Body(d, ear, w, th, 4, 8, Vector3.forward, 0.55f, 1f, false, false);
            d.Sub = 1;
            d.Ellipsoid(V(0.085f * s, 0.875f, 0.615f), V(0.028f, 0.045f, 0.008f), Quaternion.Euler(0, 0, -12 * s), 8, 6);
        }
        d.Sub = 2;
        d.Ellipsoid(V(0, 0.695f, 0.92f), V(0.028f, 0.022f, 0.022f), Quaternion.identity, 8, 6);
        d.Sub = 3;
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.072f * s, 0.785f, 0.735f), V(0.022f, 0.016f, 0.02f), Quaternion.Euler(0, 35 * s, 0), 8, 6);
        return d.ToMesh("FoxBody");
    });

    /// <summary>Лапа лисы от плеча (шарнир в нуле) до земли (-0.36).</summary>
    public static Mesh FoxLeg(bool hind) => Cached(hind ? "FoxLegHind" : "FoxLegFront", () =>
    {
        var d = new MeshDraft(2);
        Vector3[] upper = hind ? new[] { V(0, 0.09f, 0), V(0, -0.05f, -0.045f), V(0, -0.14f, -0.07f) } : new[] { V(0, 0.07f, 0), V(0, -0.02f, 0.01f), V(0, -0.11f, 0.012f) };
        Vector3[] lower = hind ? new[] { V(0, -0.14f, -0.07f), V(0, -0.25f, -0.04f), V(0, -0.335f, 0) } : new[] { V(0, -0.11f, 0.012f), V(0, -0.23f, 0.004f), V(0, -0.335f, 0.012f) };
        Body(d, upper, Curve(0, hind ? 0.07f : 0.055f, 1, 0.042f), Curve(0, hind ? 0.09f : 0.065f, 1, 0.045f), 5, 10, Vector3.forward);
        d.Sub = 1;
        Body(d, lower, Curve(0, 0.04f, 1, 0.03f), Curve(0, 0.042f, 1, 0.032f), 5, 8, Vector3.forward);
        d.Ellipsoid(V(0, -0.343f, 0.03f), V(0.038f, 0.022f, 0.055f), Quaternion.identity, 8, 6);
        return d.ToMesh(hind ? "FoxLegHind" : "FoxLegFront");
    });

    /// <summary>Пушистый хвост от корня вдоль -Z, белый кончик.</summary>
    public static Mesh FoxTail() => Cached("FoxTail", () =>
    {
        var d = new MeshDraft(2);
        Vector3[] spine = { V(0, 0, 0.04f), V(0, -0.04f, -0.28f), V(0, -0.05f, -0.58f), V(0, -0.02f, -0.86f) };
        System.Func<float, float> r = Curve(0, 0.045f, 0.4f, 0.125f, 0.7f, 0.115f, 0.9f, 0.07f, 1f, 0f);
        // Неровный край меха: чуть рваный контур сечения.
        System.Func<float, float, float> fluff = (a, t) => 1f + 0.07f * Mathf.Sin(a * 5f + t * 11f);
        Body(d, spine, r, r, 14, 12, Vector3.up, 0f, 0.72f, true, false, fluff);
        d.Sub = 1;
        Body(d, spine, r, r, 6, 12, Vector3.up, 0.72f, 1f, false, false, fluff);
        return d.ToMesh("FoxTail");
    });

    // ===================== Орёл =====================
    // Подмеши тела: 0 перо, 1 тёмный (когти), 2 белый, 3 клюв, 4 глаз; крыла: 0 перо, 1 кончики.

    public static Mesh EagleBody() => Cached("EagleBody", () =>
    {
        var d = new MeshDraft(5);
        Vector3[] spine = { V(0, 0, -0.75f), V(0, 0.02f, -0.2f), V(0, 0.03f, 0.3f), V(0, 0.06f, 0.62f) };
        Body(d, spine, Curve(0, 0.09f, 0.2f, 0.23f, 0.55f, 0.3f, 0.85f, 0.24f, 1, 0.17f),
            Curve(0, 0.06f, 0.2f, 0.19f, 0.55f, 0.27f, 0.85f, 0.22f, 1, 0.16f), 14, 14, Vector3.up, capEnd: false);
        // Жёлтые лапы с когтями, поджатые к хвосту.
        d.Sub = 3;
        for (int s = -1; s <= 1; s += 2)
        {
            Limb(d, new[] { V(0.12f * s, -0.18f, 0.05f), V(0.12f * s, -0.27f, -0.12f), V(0.1f * s, -0.27f, -0.3f) }, 0.05f, 0.035f, 8, Vector3.up);
            d.Sub = 1;
            for (int k = -1; k <= 1; k++)
                d.Cone(V(0.1f * s + k * 0.03f, -0.27f, -0.3f), V(0.1f * s + k * 0.05f, -0.31f, -0.38f), 0.014f, 5);
            d.Sub = 3;
        }
        d.Sub = 2;
        Body(d, new[] { V(0, 0.06f, 0.55f), V(0, 0.12f, 0.72f), V(0, 0.17f, 0.8f) }, Curve(0, 0.17f, 1, 0.17f), Curve(0, 0.17f, 1, 0.16f), 5, 14, Vector3.up, capStart: false);
        d.Ellipsoid(V(0, 0.18f, 0.83f), V(0.18f, 0.17f, 0.23f), Quaternion.Euler(-8, 0, 0), 14, 10);
        // Белый веер хвоста.
        Vector2[] tail = { V2(-0.13f, 0.05f), V2(0.13f, 0.05f), V2(0.36f, -0.52f), V2(0.18f, -0.6f), V2(0, -0.63f), V2(-0.18f, -0.6f), V2(-0.36f, -0.52f) };
        d.Plate(tail, 0.05f, MeshKit.TRS(V(0, 0.01f, -0.62f), V(90, 0, 0)));
        // Крючковатый клюв и восковица.
        d.Sub = 3;
        d.Ellipsoid(V(0, 0.16f, 1.0f), V(0.075f, 0.075f, 0.07f), Quaternion.identity, 10, 8);
        Body(d, new[] { V(0, 0.17f, 1.0f), V(0, 0.15f, 1.12f), V(0, 0.09f, 1.18f), V(0, 0.04f, 1.17f) },
            Curve(0, 0.065f, 0.6f, 0.035f, 1, 0.005f), Curve(0, 0.075f, 0.6f, 0.045f, 1, 0.005f), 8, 10, Vector3.up);
        d.Sub = 4;
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.125f * s, 0.23f, 0.93f), V(0.035f, 0.03f, 0.035f), Quaternion.identity, 8, 6);
        // Надбровья — суровый «орлиный» взгляд.
        d.Sub = 2;
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.1f * s, 0.27f, 0.93f), V(0.06f, 0.03f, 0.08f), Quaternion.Euler(0, 0, 15 * s), 8, 6);
        return d.ToMesh("EagleBody");
    });

    /// <summary>Крыло орла (правое, вдоль +X от шарнира); левое — зеркальное. Пальцы маховых перьев тёмные.</summary>
    public static Mesh EagleWing(int side) => Cached(side < 0 ? "EagleWingL" : "EagleWingR", () =>
    {
        var d = new MeshDraft(2);
        System.Func<Vector2, float> dihedral = p => -0.035f * p.x;
        Vector2[] inner = { V2(0, 0.3f), V2(0.6f, 0.4f), V2(1.3f, 0.38f), V2(2.0f, 0.3f), V2(2.05f, -0.4f), V2(1.5f, -0.5f), V2(0.8f, -0.55f), V2(0.25f, -0.42f), V2(0, -0.28f) };
        d.Plate(inner, 0.06f, MeshKit.TRS(V(0, 0, -0.05f), V(90, 0, 0)), dihedral);
        // Утолщённая передняя кромка крыла.
        Limb(d, new[] { V(0, 0, 0.22f), V(1.0f, -0.035f, 0.32f), V(2.0f, -0.07f, 0.24f) }, 0.08f, 0.03f, 8, Vector3.up, 8, 0.6f);
        // Кроющие перья светлее не делаем — орёл виден снизу и издалека; главное — силуэт «пальцев».
        d.Sub = 1;
        for (int k = 0; k < 6; k++)
        {
            float z = 0.22f - k * 0.12f;
            float len = 0.75f - Mathf.Abs(k - 1.5f) * 0.08f;
            Vector2[] finger = { V2(-0.1f, 0.065f), V2(len * 0.6f, 0.055f), V2(len, 0f), V2(len * 0.6f, -0.055f), V2(-0.1f, -0.065f) };
            d.Plate(finger, 0.03f, Matrix4x4.TRS(V(1.95f, -0.07f - k * 0.004f, z - 0.05f), Quaternion.Euler(0, k * 9f - 6f, 0) * Quaternion.Euler(90, 0, 0), Vector3.one),
                p => -0.06f * Mathf.Max(0f, p.x));
        }
        return d.ToMesh(side < 0 ? "EagleWingL" : "EagleWingR", false, side < 0);
    });

    private static Vector2 V2(float x, float y) => new Vector2(x, y);

    // ===================== Добыча =====================

    /// <summary>Мышь. Подмеши: 0 мех, 1 розовый, 2 тёмный.</summary>
    public static Mesh Mouse() => Cached("PreyMouse", () =>
    {
        var d = new MeshDraft(3);
        Vector3[] spine = { V(0, 0.15f, -0.27f), V(0, 0.18f, -0.05f), V(0, 0.19f, 0.2f), V(0, 0.175f, 0.47f) };
        Body(d, spine, Curve(0, 0.03f, 0.15f, 0.14f, 0.45f, 0.155f, 0.68f, 0.11f, 0.85f, 0.07f, 1, 0.012f),
            Curve(0, 0.03f, 0.15f, 0.12f, 0.45f, 0.14f, 0.68f, 0.1f, 0.85f, 0.065f, 1, 0.012f), 16, 12, Vector3.up);
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 0;
            d.Ellipsoid(V(0.09f * s, 0.3f, 0.25f), V(0.075f, 0.075f, 0.014f), Quaternion.Euler(0, 25 * s, 15 * s), 10, 6);
            d.Sub = 1;
            d.Ellipsoid(V(0.093f * s, 0.302f, 0.262f), V(0.058f, 0.058f, 0.008f), Quaternion.Euler(0, 25 * s, 15 * s), 10, 6);
            d.Ellipsoid(V(0.08f * s, 0.025f, 0.12f), V(0.028f, 0.018f, 0.045f), Quaternion.identity, 6, 4);
            d.Ellipsoid(V(0.09f * s, 0.025f, -0.13f), V(0.032f, 0.02f, 0.06f), Quaternion.identity, 6, 4);
            d.Sub = 2;
            d.Ellipsoid(V(0.062f * s, 0.235f, 0.35f), V(0.024f, 0.024f, 0.024f), Quaternion.identity, 8, 6);
        }
        d.Sub = 1;
        d.Sphere(V(0, 0.178f, 0.475f), 0.022f, 8, 6);
        Limb(d, new[] { V(0, 0.13f, -0.24f), V(0, 0.06f, -0.4f), V(0.04f, 0.03f, -0.58f), V(0.1f, 0.03f, -0.72f) }, 0.022f, 0.005f, 6, Vector3.up, 10);
        return d.ToMesh("PreyMouse");
    });

    /// <summary>Кролик. Подмеши: 0 мех, 1 белый, 2 розовый, 3 тёмный.</summary>
    public static Mesh Rabbit() => Cached("PreyRabbit", () =>
    {
        var d = new MeshDraft(4);
        Vector3[] spine = { V(0, 0.27f, -0.4f), V(0, 0.31f, -0.15f), V(0, 0.36f, 0.08f), V(0, 0.42f, 0.22f) };
        Body(d, spine, Curve(0, 0.05f, 0.15f, 0.21f, 0.45f, 0.235f, 0.8f, 0.18f, 1, 0.12f),
            Curve(0, 0.05f, 0.15f, 0.21f, 0.45f, 0.225f, 0.8f, 0.19f, 1, 0.13f), 14, 14, Vector3.up, capEnd: false);
        d.Ellipsoid(V(0, 0.5f, 0.31f), V(0.15f, 0.14f, 0.17f), Quaternion.Euler(-10, 0, 0), 14, 10);
        d.Ellipsoid(V(0, 0.465f, 0.43f), V(0.085f, 0.07f, 0.07f), Quaternion.identity, 10, 6);
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 0;
            Vector3[] ear = { V(0.055f * s, 0.6f, 0.27f), V(0.075f * s, 0.77f, 0.24f), V(0.095f * s, 0.93f, 0.2f) };
            Body(d, ear, Curve(0, 0.04f, 0.45f, 0.06f, 0.85f, 0.045f, 1, 0f), Curve(0, 0.02f, 0.5f, 0.022f, 1, 0f), 8, 8, Vector3.forward);
            d.Sub = 2;
            d.Ellipsoid(V(0.077f * s, 0.775f, 0.258f), V(0.034f, 0.13f, 0.008f), Quaternion.Euler(-12, 0, -7 * s), 8, 8);
            d.Sub = 0;
            d.Ellipsoid(V(0.15f * s, 0.06f, -0.12f), V(0.065f, 0.055f, 0.18f), Quaternion.identity, 10, 6);
            d.Ellipsoid(V(0.17f * s, 0.2f, -0.2f), V(0.09f, 0.14f, 0.16f), Quaternion.Euler(20, 0, 0), 10, 8);
            Limb(d, new[] { V(0.075f * s, 0.24f, 0.18f), V(0.08f * s, 0.12f, 0.21f), V(0.08f * s, 0.03f, 0.25f) }, 0.04f, 0.03f, 8, Vector3.forward);
            d.Sub = 3;
            d.Ellipsoid(V(0.112f * s, 0.54f, 0.39f), V(0.03f, 0.034f, 0.03f), Quaternion.identity, 8, 6);
        }
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.3f, 0.18f), V(0.14f, 0.16f, 0.11f), Quaternion.Euler(-15, 0, 0), 12, 8);
        d.Ellipsoid(V(0, 0.37f, -0.4f), V(0.08f, 0.08f, 0.07f), Quaternion.identity, 10, 8, p => p * (1f + 0.12f * Mathf.Sin(p.x * 9f) * Mathf.Sin(p.y * 7f)));
        d.Sub = 2;
        d.Sphere(V(0, 0.48f, 0.495f), 0.024f, 8, 6);
        return d.ToMesh("PreyRabbit");
    });

    /// <summary>Птица (тело без крыльев). Подмеши: 0 перо, 1 грудка, 2 клюв и лапки, 3 глаз.</summary>
    public static Mesh Bird() => Cached("PreyBirdBody", () =>
    {
        var d = new MeshDraft(4);
        Vector3[] spine = { V(0, 0.29f, -0.2f), V(0, 0.3f, 0f), V(0, 0.34f, 0.15f) };
        Body(d, spine, Curve(0, 0.04f, 0.25f, 0.13f, 0.6f, 0.15f, 1, 0.1f), Curve(0, 0.04f, 0.25f, 0.13f, 0.6f, 0.15f, 1, 0.11f), 10, 12, Vector3.up, capEnd: false);
        Body(d, new[] { V(0, 0.33f, 0.12f), V(0, 0.42f, 0.17f), V(0, 0.46f, 0.2f) }, Curve(0, 0.1f, 1, 0.085f), Curve(0, 0.11f, 1, 0.085f), 4, 12, Vector3.up, capStart: false);
        d.Ellipsoid(V(0, 0.47f, 0.2f), V(0.095f, 0.09f, 0.105f), Quaternion.identity, 12, 8);
        d.Plate(new[] { V2(-0.05f, 0.02f), V2(0.05f, 0.02f), V2(0.095f, -0.22f), V2(0.03f, -0.25f), V2(0, -0.22f), V2(-0.03f, -0.25f), V2(-0.095f, -0.22f) },
            0.025f, MeshKit.TRS(V(0, 0.32f, -0.17f), V(110, 0, 0)));
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.28f, 0.07f), V(0.115f, 0.125f, 0.13f), Quaternion.Euler(-20, 0, 0), 12, 8);
        d.Sub = 2;
        d.Cone(V(0, 0.46f, 0.285f), V(0, 0.445f, 0.37f), 0.03f, 8, 0.8f);
        for (int s = -1; s <= 1; s += 2)
        {
            Limb(d, new[] { V(0.05f * s, 0.2f, 0.02f), V(0.05f * s, 0.1f, 0.03f), V(0.05f * s, 0.01f, 0.03f) }, 0.013f, 0.01f, 5, Vector3.forward, 3);
            for (int k = -1; k <= 1; k++) d.Cone(V(0.05f * s, 0.01f, 0.03f), V(0.05f * s + k * 0.03f, 0.005f, 0.09f - Mathf.Abs(k) * 0.015f), 0.008f, 4);
            d.Cone(V(0.05f * s, 0.01f, 0.03f), V(0.05f * s, 0.005f, -0.02f), 0.007f, 4);
            d.Sub = 3;
            d.Ellipsoid(V(0.07f * s, 0.49f, 0.25f), V(0.018f, 0.02f, 0.018f), Quaternion.identity, 8, 6);
            d.Sub = 2;
        }
        return d.ToMesh("PreyBirdBody");
    });

    /// <summary>Крыло птицы (правое, от шарнира вдоль +X); левое — зеркальное.</summary>
    public static Mesh BirdWing(int side) => Cached(side < 0 ? "PreyBirdWingL" : "PreyBirdWingR", () =>
    {
        var d = new MeshDraft(1);
        Vector2[] wing = { V2(0, 0.07f), V2(0.15f, 0.07f), V2(0.3f, 0.03f), V2(0.4f, -0.04f), V2(0.32f, -0.08f), V2(0.27f, -0.14f), V2(0.18f, -0.13f), V2(0.13f, -0.19f), V2(0.04f, -0.16f), V2(0, -0.1f) };
        d.Plate(wing, 0.035f, MeshKit.TRS(V(0, 0, 0), V(90, 0, 0)), p => -0.04f * p.x);
        Limb(d, new[] { V(0, 0, 0.05f), V(0.2f, -0.008f, 0.06f), V(0.36f, -0.016f, 0.0f) }, 0.035f, 0.012f, 6, Vector3.up, 5, 0.6f);
        return d.ToMesh(side < 0 ? "PreyBirdWingL" : "PreyBirdWingR", false, side < 0);
    });

    /// <summary>Лягушка. Подмеши: 0 кожа, 1 брюшко, 2 тёмный.</summary>
    public static Mesh Frog() => Cached("PreyFrog", () =>
    {
        var d = new MeshDraft(3);
        Vector3[] spine = { V(0, 0.14f, -0.21f), V(0, 0.17f, 0f), V(0, 0.16f, 0.23f) };
        Body(d, spine, Curve(0, 0.06f, 0.2f, 0.2f, 0.55f, 0.225f, 0.85f, 0.19f, 1, 0.09f), Curve(0, 0.05f, 0.25f, 0.13f, 0.6f, 0.13f, 0.88f, 0.09f, 1, 0.04f), 12, 14, Vector3.up,
            shape: (a, t) => Mathf.Sin(a) < 0f ? 1f - 0.25f * -Mathf.Sin(a) : 1f);
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 0;
            d.Ellipsoid(V(0.11f * s, 0.255f, 0.12f), V(0.065f, 0.06f, 0.065f), Quaternion.identity, 10, 8);
            // Задние лапы сложены «зигзагом», перепончатые стопы.
            Body(d, new[] { V(0.13f * s, 0.14f, -0.1f), V(0.26f * s, 0.1f, 0.04f), V(0.22f * s, 0.045f, -0.12f), V(0.3f * s, 0.025f, -0.03f) },
                Curve(0, 0.075f, 0.35f, 0.055f, 1, 0.025f), Curve(0, 0.07f, 0.35f, 0.05f, 1, 0.02f), 12, 10, Vector3.up);
            d.Plate(new[] { V2(0, 0.02f), V2(0.07f, 0.06f), V2(0.1f, 0.01f), V2(0.08f, -0.05f), V2(0, -0.02f) }, 0.012f, MeshKit.TRS(V(0.29f * s, 0.012f, -0.04f), V(90, -40 * s, 0)));
            Limb(d, new[] { V(0.12f * s, 0.12f, 0.14f), V(0.16f * s, 0.06f, 0.19f), V(0.17f * s, 0.01f, 0.22f) }, 0.03f, 0.02f, 8, Vector3.forward);
            d.Ellipsoid(V(0.18f * s, 0.008f, 0.24f), V(0.035f, 0.008f, 0.03f), Quaternion.identity, 8, 4);
            d.Sub = 2;
            d.Ellipsoid(V(0.13f * s, 0.27f, 0.16f), V(0.035f, 0.03f, 0.03f), Quaternion.Euler(0, 25 * s, 0), 8, 6);
        }
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.09f, 0.03f), V(0.18f, 0.06f, 0.19f), Quaternion.identity, 12, 6);
        return d.ToMesh("PreyFrog");
    });

    /// <summary>Тело ежа с иголками. Подмеши: 0 мех, 1 иголки.</summary>
    public static Mesh HedgehogBody() => Cached("PreyHedgehogBody", () =>
    {
        var d = new MeshDraft(2);
        d.Ellipsoid(V(0, 0.19f, 0), V(0.22f, 0.15f, 0.27f), Quaternion.identity, 14, 10);
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.215f, -0.03f), V(0.235f, 0.165f, 0.27f), Quaternion.identity, 14, 10, p => p.y < -0.25f ? new Vector3(p.x, -0.25f, p.z) * 0.98f : p);
        const int count = 90;
        for (int i = 0; i < count; i++)
        {
            float a = i * 2.39996f;
            float h = -0.2f + 1.15f * ((i + 0.5f) / count);
            if (h > 1f) continue;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - h * h));
            var dir = new Vector3(Mathf.Cos(a) * ring, h, Mathf.Sin(a) * ring);
            if (dir.z > 0.72f) continue; // мордочку не закрываем
            Vector3 basePos = V(dir.x * 0.22f, 0.215f + dir.y * 0.155f, -0.03f + dir.z * 0.26f);
            Vector3 tipDir = (dir + V(0, 0.15f, -0.45f)).normalized;
            float len = 0.12f + 0.04f * Mathf.Sin(i * 1.7f);
            d.Cone(basePos - tipDir * 0.01f, basePos + tipDir * len, 0.022f, 4);
        }
        return d.ToMesh("PreyHedgehogBody");
    });

    /// <summary>Мордочка и лапки ежа (прячутся, когда он свёрнут). Подмеши: 0 светлый, 1 тёмный.</summary>
    public static Mesh HedgehogFace() => Cached("PreyHedgehogFace", () =>
    {
        var d = new MeshDraft(2);
        Body(d, new[] { V(0, 0.17f, 0.18f), V(0, 0.16f, 0.3f), V(0, 0.145f, 0.41f) }, Curve(0, 0.11f, 1, 0.025f), Curve(0, 0.095f, 1, 0.022f), 8, 12, Vector3.up);
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 0;
            d.Ellipsoid(V(0.09f * s, 0.25f, 0.24f), V(0.032f, 0.03f, 0.015f), Quaternion.Euler(0, 30 * s, 0), 8, 6);
            d.Ellipsoid(V(0.13f * s, 0.035f, 0.15f), V(0.045f, 0.035f, 0.06f), Quaternion.identity, 8, 6);
            d.Ellipsoid(V(0.14f * s, 0.035f, -0.16f), V(0.05f, 0.035f, 0.065f), Quaternion.identity, 8, 6);
            d.Sub = 1;
            d.Ellipsoid(V(0.062f * s, 0.205f, 0.31f), V(0.02f, 0.02f, 0.018f), Quaternion.identity, 8, 6);
        }
        d.Sub = 1;
        d.Sphere(V(0, 0.147f, 0.418f), 0.026f, 8, 6);
        return d.ToMesh("PreyHedgehogFace");
    });

    /// <summary>Жук. Подмеши: 0 панцирь, 1 тёмный.</summary>
    public static Mesh Beetle() => Cached("PreyBeetle", () =>
    {
        var d = new MeshDraft(2);
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.072f * s, 0.13f, -0.035f), V(0.085f, 0.085f, 0.19f), Quaternion.Euler(0, 0, -6 * s), 12, 8,
                p => p.y < -0.2f ? new Vector3(p.x, -0.2f, p.z) : p);
        d.Ellipsoid(V(0, 0.125f, 0.15f), V(0.11f, 0.07f, 0.075f), Quaternion.identity, 12, 8);
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.1f, -0.02f), V(0.13f, 0.05f, 0.18f), Quaternion.identity, 10, 6);
        d.Ellipsoid(V(0, 0.095f, 0.235f), V(0.065f, 0.05f, 0.055f), Quaternion.identity, 10, 6);
        for (int s = -1; s <= 1; s += 2)
        {
            for (int l = 0; l < 3; l++)
            {
                float z = 0.1f - l * 0.1f, dz = (l - 1) * 0.05f;
                Limb(d, new[] { V(0.07f * s, 0.09f, z), V(0.17f * s, 0.13f, z + dz), V(0.24f * s, 0.0f, z + dz * 2f) }, 0.016f, 0.01f, 5, Vector3.up, 5);
            }
            Limb(d, new[] { V(0.025f * s, 0.11f, 0.28f), V(0.07f * s, 0.16f, 0.36f), V(0.11f * s, 0.18f, 0.42f) }, 0.009f, 0.006f, 4, Vector3.up, 4);
            d.Cone(V(0.025f * s, 0.085f, 0.28f), V(0.01f * s, 0.075f, 0.33f), 0.015f, 4);
        }
        return d.ToMesh("PreyBeetle");
    });

    /// <summary>Рыба. Подмеши: 0 чешуя, 1 плавники, 2 тёмный.</summary>
    public static Mesh Fish() => Cached("PreyFish", () =>
    {
        var d = new MeshDraft(3);
        Body(d, new[] { V(0, 0, -0.24f), V(0, 0.01f, 0f), V(0, 0, 0.26f) }, Curve(0, 0.015f, 0.3f, 0.065f, 0.65f, 0.085f, 0.9f, 0.065f, 1, 0.012f),
            Curve(0, 0.035f, 0.3f, 0.08f, 0.65f, 0.1f, 0.9f, 0.075f, 1, 0.02f), 14, 12, Vector3.up);
        d.Sub = 1;
        d.Plate(new[] { V2(0, 0.025f), V2(0.13f, 0.12f), V2(0.095f, 0f), V2(0.13f, -0.12f), V2(0, -0.025f) }, 0.012f, MeshKit.TRS(V(0, 0, -0.22f), V(0, 90, 0)));
        d.Plate(new[] { V2(-0.08f, 0f), V2(0.09f, 0f), V2(0.07f, 0.045f), V2(-0.05f, 0.08f) }, 0.01f, MeshKit.TRS(V(0, 0.085f, 0f), V(0, 90, 0)));
        d.Plate(new[] { V2(-0.04f, 0f), V2(0.05f, 0f), V2(0.04f, -0.04f) }, 0.008f, MeshKit.TRS(V(0, -0.08f, -0.1f), V(0, 90, 0)));
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 1;
            d.Plate(new[] { V2(0, 0.012f), V2(0.07f, 0.02f), V2(0.08f, -0.02f), V2(0, -0.012f) }, 0.008f, MeshKit.TRS(V(0.06f * s, -0.04f, 0.1f), V(0, 90 + 140 * s, 25 * s)));
            d.Sub = 2;
            d.Ellipsoid(V(0.052f * s, 0.025f, 0.17f), V(0.02f, 0.022f, 0.02f), Quaternion.identity, 8, 6);
        }
        return d.ToMesh("PreyFish");
    });

    // ===================== Осы и змеи =====================

    /// <summary>Оса. Подмеши: 0 жёлтый, 1 чёрный, 2 крылья.</summary>
    public static Mesh Wasp() => Cached("Wasp", () =>
    {
        var d = new MeshDraft(3);
        Vector3[] abd = { V(0, 0, -0.005f), V(0, -0.006f, -0.07f), V(0, -0.015f, -0.15f) };
        System.Func<float, float> r = Curve(0, 0.008f, 0.12f, 0.012f, 0.45f, 0.04f, 0.8f, 0.03f, 1, 0f);
        float[] cuts = { 0f, 0.15f, 0.36f, 0.46f, 0.62f, 0.72f, 0.86f, 1f };
        for (int i = 0; i < cuts.Length - 1; i++)
        {
            d.Sub = i % 2 == 0 ? 1 : 0;
            Body(d, abd, r, r, 3, 10, Vector3.up, cuts[i], cuts[i + 1], i == 0, false);
        }
        d.Sub = 1;
        d.Ellipsoid(V(0, 0.002f, 0.03f), V(0.03f, 0.03f, 0.036f), Quaternion.identity, 10, 8);
        d.Ellipsoid(V(0, 0.002f, 0.075f), V(0.027f, 0.025f, 0.02f), Quaternion.identity, 10, 8);
        for (int s = -1; s <= 1; s += 2)
        {
            d.Sub = 0;
            d.Ellipsoid(V(0.012f * s, 0.0f, 0.092f), V(0.012f, 0.014f, 0.006f), Quaternion.identity, 6, 4);
            d.Sub = 1;
            Limb(d, new[] { V(0.012f * s, 0.015f, 0.085f), V(0.03f * s, 0.045f, 0.11f), V(0.04f * s, 0.05f, 0.135f) }, 0.003f, 0.002f, 4, Vector3.up, 3);
            for (int l = 0; l < 3; l++)
            {
                float z = 0.045f - l * 0.018f;
                Limb(d, new[] { V(0.02f * s, -0.015f, z), V(0.045f * s, -0.03f, z - 0.01f), V(0.05f * s, -0.07f, z - 0.03f) }, 0.004f, 0.003f, 4, Vector3.up, 3);
            }
            d.Sub = 2;
            d.Plate(new[] { V2(0, 0.01f), V2(0.05f, 0.016f), V2(0.095f, 0.004f), V2(0.07f, -0.014f), V2(0, -0.008f) }, 0.002f,
                Matrix4x4.TRS(V(0.015f * s, 0.028f, 0.03f), Quaternion.Euler(0, s < 0 ? 180 + 25 : -25, 0) * Quaternion.Euler(90, 0, 0), Vector3.one));
        }
        return d.ToMesh("Wasp");
    });

    /// <summary>Голова змеи: приплюснутый череп с надбровьями, сужающийся к морде. Глаза и язык — отдельные (их анимируют).</summary>
    public static Mesh SnakeHead() => Cached("SnakeHead", () =>
    {
        var d = new MeshDraft(1);
        Vector3[] spine = { V(0, 0.02f, -0.46f), V(0, 0.04f, -0.05f), V(0, 0.02f, 0.35f), V(0, -0.02f, 0.68f) };
        Body(d, spine, Curve(0, 0.27f, 0.25f, 0.4f, 0.5f, 0.38f, 0.78f, 0.27f, 0.93f, 0.17f, 1, 0.04f),
            Curve(0, 0.2f, 0.25f, 0.26f, 0.5f, 0.23f, 0.78f, 0.17f, 0.93f, 0.11f, 1, 0.03f), 16, 16, Vector3.up,
            shape: (a, t) => Mathf.Sin(a) < 0f ? 1f - 0.22f * -Mathf.Sin(a) : 1f);
        for (int s = -1; s <= 1; s += 2)
            d.Ellipsoid(V(0.22f * s, 0.17f, 0.22f), V(0.12f, 0.06f, 0.2f), Quaternion.Euler(0, -10 * s, 8 * s), 10, 6);
        return d.ToMesh("SnakeHead");
    });

    /// <summary>Лёгкая сфера радиуса 0.5 (вместо встроенной на 515 вершин): глаза, зрачки, сегменты змеёнышей.</summary>
    public static Mesh LowSphere() => Cached("LowSphere", () =>
    {
        var d = new MeshDraft(1);
        d.Sphere(Vector3.zero, 0.5f, 14, 10);
        return d.ToMesh("LowSphere");
    });

    /// <summary>Голова змеёныша (диаметр 1, масштабируется как прежний шарик): вытянутая, с глазками. Подмеши: 0 тело, 1 глаз.</summary>
    public static Mesh HatchlingHead() => Cached("HatchlingHead", () =>
    {
        var d = new MeshDraft(2);
        Body(d, new[] { V(0, 0, -0.45f), V(0, 0.02f, 0f), V(0, -0.02f, 0.55f) }, Curve(0, 0.36f, 0.4f, 0.5f, 0.85f, 0.35f, 1, 0.08f),
            Curve(0, 0.32f, 0.4f, 0.38f, 0.85f, 0.25f, 1, 0.06f), 10, 12, Vector3.up);
        d.Sub = 1;
        for (int s = -1; s <= 1; s += 2) d.Sphere(V(0.34f * s, 0.18f, 0.2f), 0.13f, 8, 6);
        return d.ToMesh("HatchlingHead");
    });
}
