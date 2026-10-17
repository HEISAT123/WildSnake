using System.Collections.Generic;
using UnityEngine;
using Ring = MeshDraft.Ring;

/// <summary>
/// Меши природы из кода: деревья (ствол с корнями и ветвями + крона одним мешем), пни, брёвна, дупла, ежевика,
/// грибы, рогоз, кувшинки, гнёзда, яйца, осиное гнездо. У каждого вида несколько вариантов — поле не выглядит штампованным.
/// Ствол и крона — отдельные меши с одним материалом: CanopyFader подменяет у рендерера единственный материал.
/// Размеры — в метрах при клетке 1 м; BoardView масштабирует по клетке и разбросу.
/// </summary>
public static class NatureModels
{
    public const int TREE_VARIANTS = 3;
    public const int MAX_LOG = 12;

    private const float BARK_UV = 0.35f;

    private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

    public static void Reset() => _cache.Clear();

    private static Mesh Cached(string name, System.Func<Mesh> make)
    {
        if (_cache.TryGetValue(name, out Mesh m) && m != null) return m;
        m = MeshKit.Save(make(), name);
        _cache[name] = m;
        return m;
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    private static System.Func<float, float> Curve(params float[] kv)
    {
        var keys = new Keyframe[kv.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(kv[i * 2], kv[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
        return c.Evaluate;
    }

    private static void Tube(MeshDraft d, Vector3[] spine, System.Func<float, float> r, int rings, int sides,
        System.Func<float, float, float> shape = null, bool capStart = true, bool capEnd = true, float uv = BARK_UV)
    {
        var list = new List<Ring>(rings + 1);
        for (int i = 0; i <= rings; i++)
        {
            float t = (float)i / rings;
            list.Add(new Ring(MeshDraft.Spline(spine, t), Mathf.Max(0f, r(t))));
        }
        Vector3 axis = spine[spine.Length - 1] - spine[0];
        Vector3 up = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > 0.8f ? Vector3.forward : Vector3.up;
        d.Loft(list, sides, up, capStart, capEnd, shape, uv);
    }

    /// <summary>Корни-контрфорсы у комля: вздутия сечения, сходящие на нет к высоте flare (доля длины).</summary>
    private static System.Func<float, float, float> Roots(int count, float strength, float flare, float phase, float bark = 0.04f)
    {
        return (a, t) =>
        {
            float root = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * count + phase)), 3f) * Mathf.Pow(Mathf.Max(0f, 1f - t / flare), 2f);
            return 1f + strength * root + bark * Mathf.Sin(a * 7f + t * 23f);
        };
    }

    /// <summary>Ветка: изогнутая кверху трубка из точки from в направлении dir.</summary>
    private static Vector3 Branch(MeshDraft d, Vector3 from, Vector3 dir, float length, float r0, float r1, float rise, int sides = 7)
    {
        dir.Normalize();
        Vector3 mid = from + dir * (length * 0.5f) + Vector3.up * (rise * 0.25f);
        Vector3 end = from + dir * length + Vector3.up * rise;
        Tube(d, new[] { from - dir * r0, mid, end }, t => Mathf.Lerp(r0, r1, t), 3, sides);
        return end;
    }

    // ===================== Лиственное дерево =====================

    /// <summary>Скелет дерева: точка верхушки ствола и концы ветвей — общие для ствола и кроны одного варианта.</summary>
    private static void TreeSkeleton(int v, out Vector3[] spine, out List<(Vector3 from, Vector3 dir, float len, float rise)> branches)
    {
        var rng = new System.Random(101 + v * 37);
        float lean = R(rng, 0.12f, 0.3f), leanYaw = R(rng, 0f, 6.28f);
        var l = new Vector3(Mathf.Cos(leanYaw), 0f, Mathf.Sin(leanYaw)) * lean;
        spine = new[] { V(0, -0.3f, 0), l * 0.15f + V(0, 1.2f, 0), l * 0.55f + V(0, 2.4f, 0), l + V(0, 3.5f, 0) };
        branches = new List<(Vector3, Vector3, float, float)>();
        int count = 4 + v % 2;
        float yaw0 = R(rng, 0f, 360f);
        for (int b = 0; b < count; b++)
        {
            float t = 0.62f + 0.3f * b / (count - 1);
            Vector3 from = MeshDraft.Spline(spine, t);
            float yaw = (yaw0 + b * (360f / count) + R(rng, -25f, 25f)) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(yaw), R(rng, 0.25f, 0.55f), Mathf.Sin(yaw));
            branches.Add((from, dir, R(rng, 1.0f, 1.6f), R(rng, 0.4f, 0.8f)));
        }
    }

    public static Mesh TreeTrunk(int v) => Cached("TreeTrunk" + v, () =>
    {
        TreeSkeleton(v, out Vector3[] spine, out var branches);
        var d = new MeshDraft(1);
        // Комель — частые кольца (корни), выше ствол почти прямой: кольца реже. ~350 треугольников вместе с ветвями.
        Tube(d, spine, Curve(0, 0.5f, 0.1f, 0.38f, 0.25f, 0.33f, 0.7f, 0.26f, 1, 0.15f), 9, 10, Roots(5, 0.4f, 0.16f, v * 1.3f), false, true);
        foreach (var b in branches) Branch(d, b.from, b.dir, b.len, 0.13f, 0.04f, b.rise, 5);
        return d.ToMesh("TreeTrunk" + v);
    });

    public static Mesh TreeCrown(int v) => Cached("TreeCrown" + v, () =>
    {
        TreeSkeleton(v, out Vector3[] spine, out var branches);
        var rng = new System.Random(7 + v * 13);
        var d = new MeshDraft(1);
        Vector3 top = spine[spine.Length - 1];
        // Верхний большой ком и комья на концах ветвей; между ними — заполняющие, чтобы крона не распадалась.
        d.Blob(top + V(0, 1.2f, 0), V(1.95f, 1.45f, 1.95f), 2, 0.3f, rng.Next(), Quaternion.Euler(0, R(rng, 0, 360), 0));
        foreach (var b in branches)
        {
            Vector3 end = b.from + b.dir.normalized * b.len + Vector3.up * b.rise;
            float s = R(rng, 1.2f, 1.5f);
            d.Blob(end + V(0, 0.4f, 0), V(s, s * 0.82f, s), 1, 0.36f, rng.Next(), Quaternion.Euler(0, R(rng, 0, 360), 0));
        }
        for (int i = 0; i < 2; i++)
        {
            float a = R(rng, 0, 6.28f);
            d.Blob(top + V(Mathf.Cos(a) * 1.0f, R(rng, 0.4f, 1.0f), Mathf.Sin(a) * 1.0f), V(1.3f, 1.05f, 1.3f), 1, 0.35f, rng.Next(), Quaternion.identity);
        }
        return d.ToMesh("TreeCrown" + v, true);
    });

    // ===================== Ель =====================

    public static Mesh PineTrunk(int v) => Cached("PineTrunk" + v, () =>
    {
        var d = new MeshDraft(1);
        var rng = new System.Random(300 + v);
        Vector3 lean = V(R(rng, -0.1f, 0.1f), 0, R(rng, -0.1f, 0.1f));
        Tube(d, new[] { V(0, -0.3f, 0), lean * 0.4f + V(0, 3f, 0), lean + V(0, 6.6f, 0) }, Curve(0, 0.4f, 0.08f, 0.28f, 0.5f, 0.18f, 1, 0.02f), 12, 10,
            Roots(4, 0.35f, 0.12f, v * 2.1f), false, false);
        return d.ToMesh("PineTrunk" + v);
    });

    /// <summary>Ярусы ели: «юбки» с зубчатым свисающим краем, гранёные.</summary>
    public static Mesh PineCrown(int v) => Cached("PineCrown" + v, () =>
    {
        var d = new MeshDraft(1);
        var rng = new System.Random(400 + v);
        const int tiers = 6;
        for (int k = 0; k < tiers; k++)
        {
            float y = 2.8f + k * 0.62f;
            float r = (2.05f - k * 0.3f) * R(rng, 0.92f, 1.08f);
            float h = 1.5f - k * 0.05f;
            float rot = R(rng, 0f, 6.28f);
            int n = 9 - k / 2;
            var rim = new Vector3[n * 2];
            for (int i = 0; i < rim.Length; i++)
            {
                float a = rot + i * Mathf.PI / n;
                bool tip = i % 2 == 0;
                float rr = r * (tip ? R(rng, 0.95f, 1.08f) : 0.72f);
                rim[i] = V(Mathf.Cos(a) * rr, y + (tip ? -0.22f : 0.02f) + R(rng, -0.05f, 0.05f), Mathf.Sin(a) * rr);
            }
            Vector3 apex = V(0, y + h, 0);
            Vector3 innerCenter = V(0, y + h * 0.25f, 0);
            for (int i = 0; i < rim.Length; i++)
            {
                Vector3 a = rim[i], b = rim[(i + 1) % rim.Length];
                Face(d, apex, a, b, (a + b) * 0.5f - V(0, y, 0));
                Face(d, innerCenter, b, a, Vector3.down);
            }
        }
        d.Cone(V(0, 2.8f + tiers * 0.62f + 0.6f, 0), V(0, 2.8f + tiers * 0.62f + 1.4f, 0), 0.22f, 6);
        return d.ToMesh("PineCrown" + v, true);
    });

    /// <summary>Треугольник, развёрнутый лицом в сторону outward.</summary>
    private static void Face(MeshDraft d, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, outward) < 0f) d.Triangle(a, c, b, V2(a), V2(c), V2(b));
        else d.Triangle(a, b, c, V2(a), V2(b), V2(c));
    }

    private static Vector2 V2(Vector3 p) => new Vector2(p.x + p.z, p.y);

    // ===================== Берёза =====================

    private static void BirchSkeleton(int v, out Vector3[] spine, out List<(Vector3 from, Vector3 dir, float len, float rise)> branches)
    {
        var rng = new System.Random(500 + v * 11);
        float yaw = R(rng, 0, 6.28f);
        var l = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * R(rng, 0.2f, 0.45f);
        Vector3 side = Vector3.Cross(l.normalized, Vector3.up) * 0.12f;
        spine = new[] { V(0, -0.3f, 0), side + l * 0.2f + V(0, 1.8f, 0), -side + l * 0.6f + V(0, 3.6f, 0), l + V(0, 5.6f, 0) };
        branches = new List<(Vector3, Vector3, float, float)>();
        for (int b = 0; b < 4; b++)
        {
            float t = 0.6f + b * 0.1f;
            float a = yaw + b * 2.2f + R(rng, -0.4f, 0.4f);
            branches.Add((MeshDraft.Spline(spine, t), new Vector3(Mathf.Cos(a), R(rng, 0.5f, 0.9f), Mathf.Sin(a)), R(rng, 0.8f, 1.2f), R(rng, 0.2f, 0.5f)));
        }
    }

    public static Mesh BirchTrunk(int v) => Cached("BirchTrunk" + v, () =>
    {
        BirchSkeleton(v, out Vector3[] spine, out var branches);
        var d = new MeshDraft(1);
        Tube(d, spine, Curve(0, 0.3f, 0.08f, 0.21f, 0.4f, 0.17f, 1, 0.06f), 9, 8, Roots(4, 0.25f, 0.1f, v, 0.02f), false, true);
        foreach (var b in branches) Branch(d, b.from, b.dir, b.len, 0.06f, 0.02f, b.rise, 4);
        return d.ToMesh("BirchTrunk" + v);
    });

    public static Mesh BirchCrown(int v) => Cached("BirchCrown" + v, () =>
    {
        BirchSkeleton(v, out Vector3[] spine, out var branches);
        var rng = new System.Random(600 + v);
        var d = new MeshDraft(1);
        Vector3 top = spine[spine.Length - 1];
        d.Blob(top + V(0, 0.1f, 0), V(1.25f, 1.5f, 1.25f), 2, 0.3f, rng.Next(), Quaternion.identity);
        foreach (var b in branches)
        {
            Vector3 end = b.from + b.dir.normalized * b.len + Vector3.up * b.rise;
            float s = R(rng, 0.95f, 1.15f);
            Vector3 c = Vector3.Lerp(end, top, 0.3f);
            d.Blob(c + V(0, 0.1f, 0), V(s, s * 1.2f, s), 1, 0.4f, rng.Next(), Quaternion.Euler(0, R(rng, 0, 360), 0));
        }
        // Плакучие пряди: вытянутые вниз комочки по краю кроны.
        for (int i = 0; i < 4; i++)
        {
            float a = R(rng, 0, 6.28f);
            Vector3 p = MeshDraft.Spline(spine, R(rng, 0.72f, 0.85f)) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * R(rng, 0.8f, 1.1f);
            d.Blob(p, V(0.55f, 0.95f, 0.55f), 1, 0.3f, rng.Next(), Quaternion.identity);
        }
        return d.ToMesh("BirchCrown" + v, true);
    });

    // ===================== Сухое дерево болота =====================

    public static Mesh DeadTree(int v) => Cached("DeadTree" + v, () =>
    {
        var rng = new System.Random(700 + v * 5);
        var d = new MeshDraft(1);
        Vector3 k1 = V(R(rng, -0.2f, 0.2f), 1.2f, R(rng, -0.2f, 0.2f)), k2 = k1 + V(R(rng, -0.25f, 0.25f), 1.3f, R(rng, -0.25f, 0.25f));
        Vector3[] spine = { V(0, -0.2f, 0), k1, k2 };
        Tube(d, spine, Curve(0, 0.36f, 0.12f, 0.24f, 1, 0.13f), 8, 8, Roots(4, 0.45f, 0.18f, v * 0.7f, 0.06f), false, false);
        // Расщеплённая обломанная верхушка.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 1.57f + R(rng, -0.3f, 0.3f);
            Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.08f;
            d.Cone(k2 + o - V(0, 0.1f, 0), k2 + o * 1.6f + V(0, R(rng, 0.2f, 0.55f), 0), 0.085f, 4);
        }
        for (int b = 0; b < 4; b++)
        {
            float t = 0.35f + b * 0.17f;
            float a = R(rng, 0, 6.28f);
            Vector3 dir = new Vector3(Mathf.Cos(a), R(rng, 0.3f, 0.9f), Mathf.Sin(a));
            Vector3 from = MeshDraft.Spline(spine, t);
            float len = R(rng, 0.7f, 1.2f);
            Vector3 end = Branch(d, from, dir, len, 0.07f, 0.015f, R(rng, -0.1f, 0.3f), 5);
            // Сучок от ветки.
            Vector3 mid = Vector3.Lerp(from, end, 0.55f);
            Branch(d, mid, dir + new Vector3(Mathf.Sin(a), 0.6f, -Mathf.Cos(a)), len * 0.45f, 0.035f, 0.01f, 0.1f, 4);
        }
        return d.ToMesh("DeadTree" + v);
    });

    // ===================== Пень =====================

    /// <summary>Пень (диаметр 1, высота спила 1): корни, неровный край коры и острый обломок с +X.</summary>
    public static Mesh Stump(int v) => Cached("Stump" + v, () =>
    {
        var rng = new System.Random(800 + v);
        var d = new MeshDraft(1);
        Tube(d, new[] { V(0, -0.2f, 0), V(0, 0.5f, 0), V(0, 1.0f, 0) }, Curve(0, 0.52f, 0.2f, 0.47f, 1, 0.45f), 8, 14, Roots(5, 0.5f, 0.32f, v * 1.1f, 0.03f), false, false);
        // Зубчатый край коры вокруг спила.
        int teeth = 11;
        for (int i = 0; i < teeth; i++)
        {
            float a0 = (i + R(rng, -0.2f, 0.2f)) / teeth * Mathf.PI * 2f;
            float a1 = a0 + Mathf.PI * 2f / teeth * 0.9f;
            float am = (a0 + a1) * 0.5f;
            Vector3 p0 = V(Mathf.Cos(a0) * 0.452f, 0.99f, Mathf.Sin(a0) * 0.452f);
            Vector3 p1 = V(Mathf.Cos(a1) * 0.452f, 0.99f, Mathf.Sin(a1) * 0.452f);
            Vector3 tip = V(Mathf.Cos(am) * 0.43f, 1f + R(rng, 0.03f, 0.14f), Mathf.Sin(am) * 0.43f);
            Vector3 outward = V(Mathf.Cos(am), 0, Mathf.Sin(am));
            Face(d, p0, p1, tip, outward);
            Face(d, p0, p1, tip, -outward);
        }
        // Обломок ствола: высокая щепа с одной стороны.
        Vector3[] shard = { V(0.2f, 0.9f, 0), V(0.25f, 1.15f, 0.02f), V(0.32f, 1.38f, 0.05f) };
        var rings = new List<Ring>();
        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            rings.Add(new Ring(MeshDraft.Spline(shard, t), Mathf.Lerp(0.27f, 0.03f, t), Mathf.Lerp(0.17f, 0.02f, t)));
        }
        d.Loft(rings, 6, Vector3.right, false, false, null, BARK_UV);
        return d.ToMesh("Stump" + v);
    });

    /// <summary>Светлый спил пня с годовыми кольцами (радиальные UV).</summary>
    public static Mesh StumpTop() => Cached("StumpTop", () =>
    {
        var d = new MeshDraft(1);
        d.Annulus(V(0, 0.985f, 0), Vector3.up, 0f, 0.445f, 16, 0.06f, 3);
        return d.ToMesh("StumpTop");
    });

    // ===================== Брёвна =====================

    /// <summary>Бревно вдоль X длиной length (м), диаметр 1: лёгкий изгиб, неровная кора, обломки сучков.</summary>
    public static Mesh Log(int length) => Cached("Log" + length, () =>
    {
        var rng = new System.Random(900 + length);
        var d = new MeshDraft(1);
        float half = length * 0.5f;
        int rings = Mathf.Max(3, length);
        var pts = new Vector3[rings + 1];
        for (int i = 0; i <= rings; i++)
        {
            float t = (float)i / rings;
            pts[i] = V(Mathf.Lerp(-half, half, t), Mathf.Sin(t * 3.1f + length) * 0.025f, Mathf.Sin(t * 2.3f) * 0.03f);
        }
        var list = new List<Ring>();
        for (int i = 0; i <= rings; i++)
        {
            float t = (float)i / rings;
            list.Add(new Ring(pts[i], 0.5f * (1f - 0.08f * t)));
        }
        d.Loft(list, 9, Vector3.up, false, false, (a, t) => 1f + 0.035f * Mathf.Sin(a * 5f + t * 9f) + 0.02f * Mathf.Sin(a * 11f), BARK_UV);
        int knots = Mathf.Max(1, length / 2);
        for (int k = 0; k < knots; k++)
        {
            float x = Mathf.Lerp(-half * 0.7f, half * 0.7f, (k + R(rng, 0.2f, 0.8f)) / knots);
            float a = R(rng, -0.3f, 3.4f);
            var dir = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
            Vector3 from = V(x, 0, 0) + dir * 0.42f;
            Tube(d, new[] { from, from + dir * 0.18f + V(R(rng, -0.1f, 0.1f), 0, 0), from + dir * R(rng, 0.25f, 0.4f) },
                t => Mathf.Lerp(0.1f, 0.05f, t), 3, 6, null, false, true);
        }
        return d.ToMesh("Log" + length);
    });

    /// <summary>Торцы бревна (спилы) — отдельный меш со своим материалом.</summary>
    public static Mesh LogEnds(int length) => Cached("LogEnds" + length, () =>
    {
        var d = new MeshDraft(1);
        float half = length * 0.5f;
        d.Annulus(V(half + 0.005f, Mathf.Sin(3.1f + length) * 0.025f, Mathf.Sin(2.3f) * 0.03f), Vector3.right, 0f, 0.44f, 12, 0.05f, length);
        d.Annulus(V(-half - 0.005f, Mathf.Sin(length) * 0.025f, 0f), Vector3.left, 0f, 0.48f, 12, 0.05f, length + 1);
        return d.ToMesh("LogEnds" + length);
    });

    /// <summary>Полое бревно вдоль X, диаметр 1: кора снаружи.</summary>
    public static Mesh HollowOuter(int length) => Cached("HollowOuter" + length, () =>
    {
        var d = new MeshDraft(1);
        float half = length * 0.5f;
        d.Loft(new[] { new Ring(V(-half, 0, 0), 0.5f), new Ring(V(-half * 0.3f, 0.02f, 0), 0.49f), new Ring(V(half * 0.3f, 0.01f, 0), 0.47f), new Ring(V(half, 0, 0), 0.48f) },
            14, Vector3.up, false, false, (a, t) => 1f + 0.04f * Mathf.Sin(a * 6f + t * 8f), BARK_UV);
        return d.ToMesh("HollowOuter" + length);
    });

    /// <summary>Внутренность дупла (тёмный материал): лицом внутрь.</summary>
    public static Mesh HollowInner(int length) => Cached("HollowInner" + length, () =>
    {
        var d = new MeshDraft(1) { FlipFaces = true };
        float half = length * 0.5f;
        d.Loft(new[] { new Ring(V(-half, 0, 0), 0.36f), new Ring(V(0, 0.01f, 0), 0.34f), new Ring(V(half, 0, 0), 0.35f) }, 12, Vector3.up, false, false, null, BARK_UV);
        return d.ToMesh("HollowInner" + length);
    });

    /// <summary>Кольца спила на торцах полого бревна.</summary>
    public static Mesh HollowRims(int length) => Cached("HollowRims" + length, () =>
    {
        var d = new MeshDraft(1);
        float half = length * 0.5f;
        d.Annulus(V(half + 0.003f, 0, 0), Vector3.right, 0.35f, 0.48f, 14, 0f);
        d.Annulus(V(-half - 0.003f, 0, 0), Vector3.left, 0.36f, 0.5f, 14, 0f);
        return d.ToMesh("HollowRims" + length);
    });

    // ===================== Мелочи =====================

    /// <summary>
    /// Куст ежевики (радиус ~0.5): гранёные комья и дуги плетей. Лёгкий (~200 вершин): кустов сотни,
    /// а объединённый меш хранится прямо в сцене.
    /// </summary>
    public static Mesh BrambleBush(int v) => Cached("BrambleBush" + v, () =>
    {
        var rng = new System.Random(1000 + v);
        var d = new MeshDraft(1);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f + R(rng, -0.4f, 0.4f);
            d.Blob(V(Mathf.Cos(a) * 0.17f, R(rng, -0.05f, 0.08f), Mathf.Sin(a) * 0.17f), V(0.38f, 0.44f, 0.38f) * R(rng, 0.85f, 1.1f), 0, 0.3f, rng.Next(),
                Quaternion.Euler(R(rng, 0, 360), R(rng, 0, 360), 0));
        }
        for (int i = 0; i < 2; i++)
        {
            float a = i * 3.1f + R(rng, -0.5f, 0.5f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3[] cane = { V(0, 0.2f, 0), dir * 0.32f + V(0, 0.55f, 0), dir * 0.62f + V(0, 0.15f, 0) };
            Tube(d, cane, t => Mathf.Lerp(0.04f, 0.018f, t), 3, 3, null, false, false, 1f);
        }
        return d.ToMesh("BrambleBush" + v);
    });

    /// <summary>Гроздь ягод (радиус ~1, масштабируется до размера ягоды).</summary>
    public static Mesh BerryCluster() => Cached("BerryCluster", () =>
    {
        var d = new MeshDraft(1);
        // Ягоды крошечные: по 16 треугольников на ягоду хватает, а кустов — сотни.
        d.Ellipsoid(V(0, 0, 0), Vector3.one * 0.5f, Quaternion.identity, 4, 2);
        d.Ellipsoid(V(0.55f, -0.25f, 0.2f), Vector3.one * 0.42f, Quaternion.identity, 4, 2);
        d.Ellipsoid(V(-0.3f, -0.35f, 0.45f), Vector3.one * 0.38f, Quaternion.identity, 4, 2);
        return d.ToMesh("BerryCluster");
    });

    /// <summary>Шляпка гриба (от земли, высота ~0.17): купол и плоский низ.</summary>
    public static Mesh MushroomCap() => Cached("MushroomCap", () =>
    {
        var d = new MeshDraft(1);
        d.Loft(new[] { new Ring(V(0, 0.112f, 0), 0.088f), new Ring(V(0, 0.13f, 0), 0.094f), new Ring(V(0, 0.152f, 0), 0.078f), new Ring(V(0, 0.168f, 0), 0.045f), new Ring(V(0, 0.174f, 0), 0.001f) },
            12, Vector3.forward, false, false, null, 4f);
        d.FlipFaces = true;
        d.Loft(new[] { new Ring(V(0, 0.112f, 0), 0.088f), new Ring(V(0, 0.122f, 0), 0.02f) }, 12, Vector3.forward, false, false, null, 4f);
        d.FlipFaces = false;
        return d.ToMesh("MushroomCap");
    });

    public static Mesh MushroomStem() => Cached("MushroomStem", () =>
    {
        var d = new MeshDraft(1);
        Tube(d, new[] { V(0, -0.01f, 0), V(0.006f, 0.06f, 0), V(0, 0.125f, 0) }, Curve(0, 0.026f, 0.5f, 0.019f, 1, 0.017f), 4, 8, null, false, false, 4f);
        return d.ToMesh("MushroomStem");
    });

    /// <summary>Початок рогоза: бархатный цилиндр с острым кончиком, центр в нуле.</summary>
    public static Mesh CattailHead() => Cached("CattailHead", () =>
    {
        var d = new MeshDraft(1);
        Tube(d, new[] { V(0, -0.13f, 0), V(0, 0f, 0), V(0, 0.13f, 0) }, Curve(0, 0.02f, 0.12f, 0.034f, 0.88f, 0.034f, 1, 0.02f), 6, 8, null, true, true, 4f);
        d.Cone(V(0, 0.12f, 0), V(0, 0.24f, 0), 0.008f, 4);
        return d.ToMesh("CattailHead");
    });

    /// <summary>Стебель рогоза от земли до высоты 1 (масштаб по Y — высота).</summary>
    public static Mesh ReedStalk() => Cached("ReedStalk", () =>
    {
        var d = new MeshDraft(1);
        Tube(d, new[] { V(0, -0.02f, 0), V(0.01f, 0.5f, 0), V(0, 0.9f, 0) }, t => Mathf.Lerp(0.012f, 0.008f, t), 3, 4, null, false, false, 1f);
        return d.ToMesh("ReedStalk");
    });

    /// <summary>Лист кувшинки радиуса 0.5 с вырезом и слегка поднятым краем.</summary>
    public static Mesh LilyPad() => Cached("LilyPad", () =>
    {
        var d = new MeshDraft(1);
        const int n = 16;
        float gap = 0.35f;
        Vector3 c = V(0, 0.01f, 0);
        for (int i = 0; i < n; i++)
        {
            float a0 = gap * 0.5f + (Mathf.PI * 2f - gap) * i / n;
            float a1 = gap * 0.5f + (Mathf.PI * 2f - gap) * (i + 1) / n;
            Vector3 p0 = V(Mathf.Cos(a0) * 0.5f, 0.02f, Mathf.Sin(a0) * 0.5f), p1 = V(Mathf.Cos(a1) * 0.5f, 0.02f, Mathf.Sin(a1) * 0.5f);
            Face(d, c, p0, p1, Vector3.up);
            Face(d, c - V(0, 0.01f, 0), p0 - V(0, 0.01f, 0), p1 - V(0, 0.01f, 0), Vector3.down);
        }
        return d.ToMesh("LilyPad", true);
    });

    /// <summary>Кольцо гнезда из переплетённых прутьев (радиус 1, высота ~0.3).</summary>
    public static Mesh NestRing() => Cached("NestRing", () =>
    {
        var rng = new System.Random(1200);
        var d = new MeshDraft(1);
        for (int i = 0; i < 46; i++)
        {
            float a = i * 0.41f + R(rng, -0.1f, 0.1f);
            float y = R(rng, 0.02f, 0.28f);
            float r = R(rng, 0.82f, 1.05f) - y * 0.15f;
            float span = R(rng, 0.5f, 0.9f);
            var pts = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                float aa = a + span * (k - 1) * 0.5f;
                pts[k] = V(Mathf.Cos(aa) * r, y + R(rng, -0.06f, 0.06f), Mathf.Sin(aa) * r);
            }
            Tube(d, pts, t => 0.045f, 3, 4, null, true, true, 4f);
        }
        return d.ToMesh("NestRing");
    });

    /// <summary>Яйцо диаметром 1 (масштабируется как прежняя сфера): острее сверху.</summary>
    public static Mesh Egg() => Cached("Egg", () =>
    {
        var d = new MeshDraft(1);
        d.Ellipsoid(Vector3.zero, Vector3.one * 0.5f, Quaternion.identity, 12, 8, p => new Vector3(p.x * (1f - 0.18f * p.y), p.y, p.z * (1f - 0.18f * p.y)));
        return d.ToMesh("Egg");
    });

    /// <summary>Бумажное осиное гнездо (высота ~0.6): слоистые «юбки» и черешок сверху; лаз — отдельно.</summary>
    public static Mesh WaspNest() => Cached("WaspNestBody", () =>
    {
        var d = new MeshDraft(1);
        var rings = new List<Ring>();
        System.Func<float, float> r = Curve(0, 0.02f, 0.1f, 0.16f, 0.45f, 0.235f, 0.8f, 0.17f, 0.95f, 0.06f, 1, 0.02f);
        for (int i = 0; i <= 22; i++)
        {
            float t = i / 22f;
            // Слои бумаги: край каждого слоя чуть выступает.
            float layer = 1f - 0.07f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4.5f));
            rings.Add(new Ring(V(Mathf.Sin(t * 3f) * 0.015f, Mathf.Lerp(-0.29f, 0.31f, t), 0), r(t) * layer));
        }
        d.Loft(rings, 14, Vector3.forward, true, false, (a, t) => 1f + 0.03f * Mathf.Sin(a * 3f + t * 17f), 2f);
        d.Loft(new[] { new Ring(V(0, 0.3f, 0), 0.02f), new Ring(V(0.02f, 0.45f, 0), 0.015f) }, 5, Vector3.forward, false, true, null, 2f);
        return d.ToMesh("WaspNestBody");
    });

    /// <summary>Невысокий валун-камешек для обода тёплого ключа (радиус 0.5).</summary>
    public static Mesh Pebble() => Cached("Pebble", () =>
    {
        var d = new MeshDraft(1);
        d.Blob(Vector3.zero, Vector3.one * 0.5f, 1, 0.3f, 1300, Quaternion.identity);
        return d.ToMesh("Pebble", true);
    });

    /// <summary>Холмик земли с кратером (радиус 0.5, высота ~0.3): норы и мышиные норки.</summary>
    public static Mesh Mound() => Cached("Mound", () =>
    {
        var d = new MeshDraft(1);
        // Сплюснутая полусфера с ямкой-кратером на макушке.
        d.Ellipsoid(V(0, -0.04f, 0), V(0.5f, 0.3f, 0.5f), Quaternion.identity, 12, 8, p =>
        {
            float rho2 = p.x * p.x + p.z * p.z;
            float y = p.y > 0f ? p.y * (1f - 1.25f * Mathf.Exp(-rho2 / 0.07f)) : p.y;
            float k = 1f + 0.08f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 3f + 1f);
            return new Vector3(p.x * k, y, p.z * k);
        });
        return d.ToMesh("Mound", true);
    });

    /// <summary>Цветок радиуса 0.5: пять лепестков одной плоской «звездой», чашечкой кверху. Лёгкий — их тысячи.</summary>
    public static Mesh Flower() => Cached("Flower", () =>
    {
        var d = new MeshDraft(1);
        var outline = new List<Vector2>();
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 0.4f;
            outline.Add(new Vector2(Mathf.Cos(a - 0.62f), Mathf.Sin(a - 0.62f)) * 0.14f);
            outline.Add(new Vector2(Mathf.Cos(a - 0.32f), Mathf.Sin(a - 0.32f)) * 0.42f);
            outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f);
            outline.Add(new Vector2(Mathf.Cos(a + 0.32f), Mathf.Sin(a + 0.32f)) * 0.42f);
        }
        d.Plate(outline.ToArray(), 0.04f, MeshKit.TRS(Vector3.zero, V(90, 0, 0)), p => -0.5f * p.sqrMagnitude);
        return d.ToMesh("Flower");
    });

    /// <summary>Неровная плоская лужица/корка радиуса 0.5 лицом вверх (тёплые ключи).</summary>
    public static Mesh Puddle(int seed) => Cached("Puddle" + seed, () =>
    {
        var d = new MeshDraft(1);
        d.Annulus(Vector3.zero, Vector3.up, 0f, 0.5f, 22, 0.16f, 1400 + seed);
        Mesh m = d.ToMesh("Puddle" + seed);
        // Глубина для шейдера воды (UV2.x): лужица мелкая, но не «нулевая» — иначе её край растворился бы.
        var depth = new Vector2[m.vertexCount];
        for (int i = 0; i < depth.Length; i++) depth[i] = new Vector2(0.06f, 0f);
        m.uv2 = depth;
        return m;
    });

    // ===================== Нора и камни =====================

    /// <summary>
    /// Нора (клетка 1, вход смотрит в +Z): земляной холм и вал-арка над лазом. Сам лаз — тёмный овал (BurrowPit)
    /// на склоне холма, поэтому читается как дыра с любой стороны, откуда виден вход.
    /// </summary>
    public static Mesh BurrowMound() => Cached("BurrowMound", () =>
    {
        var d = new MeshDraft(1);
        d.Ellipsoid(V(0, -0.12f, -0.25f), V(0.85f, 0.5f, 0.75f), Quaternion.identity, 16, 10,
            p => p * (1f + 0.06f * Mathf.Sin(p.x * 6f + p.z * 4f) + 0.04f * Mathf.Sin(p.y * 9f)));
        // Арка из выброшенной земли над входом.
        var arch = new Vector3[9];
        for (int i = 0; i < arch.Length; i++)
        {
            float a = Mathf.PI * i / (arch.Length - 1);
            arch[i] = V(Mathf.Cos(a) * 0.32f, -0.06f + Mathf.Sin(a) * 0.3f, 0.43f + Mathf.Sin(a) * 0.02f);
        }
        Tube(d, arch, t => 0.095f * (1f + 0.25f * Mathf.Sin(t * 19f)), 12, 8, null, true, true, 2f);
        return d.ToMesh("BurrowMound");
    });

    /// <summary>Тёмный лаз норы: овал, вдавленный в склон холма под аркой.</summary>
    public static Mesh BurrowPit() => Cached("BurrowPit", () =>
    {
        var d = new MeshDraft(1);
        d.Ellipsoid(V(0, 0.07f, 0.42f), V(0.27f, 0.23f, 0.07f), Quaternion.Euler(-18, 0, 0), 14, 8);
        return d.ToMesh("BurrowPit");
    });

    /// <summary>Валун радиуса ~0.5: гранёный, с плоским низом и слоистостью породы.</summary>
    public static Mesh Boulder(int v) => Cached("Boulder" + v, () =>
    {
        var d = new MeshDraft(1);
        d.Blob(Vector3.zero, Vector3.one * 0.5f, 2, 0.32f, 1500 + v * 17, Quaternion.Euler(0, v * 70, 0));
        return Strata(d.ToMesh("Boulder" + v, true), 0.22f, -0.32f, 0f);
    });

    /// <summary>Менгир: вытянутый вверх камень, сужающийся к макушке (высота 1, основание ~0.5).</summary>
    public static Mesh StandingStone(int v) => Cached("StandingStone" + v, () =>
    {
        var d = new MeshDraft(1);
        d.Blob(V(0, 0.45f, 0), V(0.58f, 0.9f, 0.5f), 2, 0.3f, 1600 + v * 23, Quaternion.Euler(0, v * 50, 0));
        return Strata(d.ToMesh("StandingStone" + v, true), 0.15f, 0.02f, 0.18f);
    });

    /// <summary>Скала для гор: высокий зубчатый выход породы с ярусами (высота ~1, ширина ~0.6).</summary>
    public static Mesh Crag(int v) => Cached("Crag" + v, () =>
    {
        var rng = new System.Random(1700 + v);
        var d = new MeshDraft(1);
        d.Blob(V(0, 0.35f, 0), V(0.55f, 0.7f, 0.45f), 2, 0.38f, rng.Next(), Quaternion.identity);
        d.Blob(V(R(rng, -0.25f, 0.25f), 0.7f, R(rng, -0.15f, 0.15f)), V(0.28f, 0.5f, 0.25f), 1, 0.4f, rng.Next(), Quaternion.Euler(0, R(rng, 0, 360), R(rng, -15, 15)));
        d.Blob(V(R(rng, -0.4f, 0.4f), 0.15f, R(rng, -0.3f, 0.3f)), V(0.35f, 0.35f, 0.3f), 1, 0.35f, rng.Next(), Quaternion.identity);
        return Strata(d.ToMesh("Crag" + v, true), 0.3f, -0.2f, 0.25f);
    });

    /// <summary>
    /// Слоистость камня: высоты вершин частично «ступенькой» (strata — доля), низ срезан на minY, макушка сужается (taper).
    /// Вершины плоско-гранёного меша меняются вместе с нормалями граней.
    /// </summary>
    private static Mesh Strata(Mesh m, float strata, float minY, float taper)
    {
        Vector3[] v = m.vertices;
        Bounds b = m.bounds;
        for (int i = 0; i < v.Length; i++)
        {
            float y = v[i].y;
            float step = Mathf.Round(y * 6f) / 6f;
            y = Mathf.Lerp(y, step, strata);
            y = Mathf.Max(y, minY);
            float k = 1f - taper * Mathf.InverseLerp(b.min.y, b.max.y, y);
            v[i] = new Vector3(v[i].x * k, y, v[i].z * k);
        }
        m.vertices = v;
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }
}
