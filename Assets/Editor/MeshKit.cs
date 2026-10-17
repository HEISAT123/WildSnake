using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Черновик меша для моделей из кода: эллипсоиды, «лофты» (трубки переменного сечения вдоль кривой), пластины, конусы.
/// Каждой части задаётся подмеш (= материал), поэтому существо — один меш и один рендерер на подвижную часть.
/// Нормали считаются в конце со сваркой по позиции — швы UV и сходящиеся в точку концы не дают видимых рёбер.
/// Только для редактора: готовые меши сохраняются ассетами в Assets/Meshes.
/// </summary>
public sealed class MeshDraft
{
    /// <summary>Сечение лофта: центр, полуоси (x — вбок, y — вверх относительно кадра) и поворот сечения вокруг оси.</summary>
    public struct Ring
    {
        public Vector3 Center;
        public Vector2 Radius;
        public Ring(Vector3 center, float rx, float ry) { Center = center; Radius = new Vector2(rx, ry); }
        public Ring(Vector3 center, float r) { Center = center; Radius = new Vector2(r, r); }
    }

    private readonly List<Vector3> _v = new List<Vector3>();
    private readonly List<Vector2> _uv = new List<Vector2>();
    private readonly List<List<int>> _sub = new List<List<int>>();

    /// <summary>Подмеш, в который пишутся следующие части.</summary>
    public int Sub;

    /// <summary>Преобразование, применяемое к следующим частям (локальные координаты части → координаты меша).</summary>
    public Matrix4x4 Xf = Matrix4x4.identity;

    /// <summary>Развернуть лицевую сторону следующих частей (внутренняя поверхность дупла, низ шляпки гриба).</summary>
    public bool FlipFaces;

    public MeshDraft(int submeshes = 1)
    {
        for (int i = 0; i < submeshes; i++) _sub.Add(new List<int>());
    }

    public int VertexCount => _v.Count;

    private int Vert(Vector3 p, Vector2 uv)
    {
        _v.Add(Xf.MultiplyPoint3x4(p));
        _uv.Add(uv);
        return _v.Count - 1;
    }

    private void Tri(int a, int b, int c)
    {
        while (_sub.Count <= Sub) _sub.Add(new List<int>());
        List<int> t = _sub[Sub];
        t.Add(a);
        if (FlipFaces) { t.Add(c); t.Add(b); }
        else { t.Add(b); t.Add(c); }
    }

    // ---------------- Части ----------------

    /// <summary>Эллипсоид; warp (необязательно) деформирует точку единичной сферы до масштабирования.</summary>
    public void Ellipsoid(Vector3 center, Vector3 radii, Quaternion rotation, int lon = 12, int lat = 8,
        System.Func<Vector3, Vector3> warp = null)
    {
        int start = _v.Count;
        for (int y = 0; y <= lat; y++)
        {
            float v = (float)y / lat;
            float phi = Mathf.PI * (v - 0.5f);
            for (int x = 0; x <= lon; x++)
            {
                float u = (float)x / lon;
                float th = u * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(phi) * Mathf.Sin(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(th));
                if (warp != null) p = warp(p);
                Vert(center + rotation * Vector3.Scale(p, radii), new Vector2(u, v));
            }
        }
        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                int a = start + y * (lon + 1) + x, b = a + 1, c = a + lon + 1, d = c + 1;
                Tri(a, b, c);
                Tri(b, d, c);
            }
        }
    }

    public void Sphere(Vector3 center, float r, int lon = 10, int lat = 6) => Ellipsoid(center, Vector3.one * r, Quaternion.identity, lon, lat);

    /// <summary>
    /// Лофт по сечениям: кадр переносится вдоль кривой без закручивания, начиная с up.
    /// shape (необязательно) — множитель радиуса по углу сечения и доле длины (корни, рёбра, неровности).
    /// Концы закрываются «крышками», если радиус там не нулевой.
    /// </summary>
    public void Loft(IList<Ring> rings, int sides, Vector3 up, bool capStart = true, bool capEnd = true,
        System.Func<float, float, float> shape = null, float uvScale = 1f, int capSub = -1)
    {
        int n = rings.Count;
        if (n < 2) return;
        var tangents = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 a = rings[Mathf.Max(0, i - 1)].Center, b = rings[Mathf.Min(n - 1, i + 1)].Center;
            tangents[i] = (b - a).sqrMagnitude > 1e-10f ? (b - a).normalized : Vector3.forward;
        }
        Vector3 normal = Vector3.ProjectOnPlane(up, tangents[0]);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.ProjectOnPlane(Vector3.right, tangents[0]);
        normal.Normalize();

        int start = _v.Count;
        float length = 0f;
        float circumference = Mathf.PI * (rings[0].Radius.x + rings[0].Radius.y);
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                normal = Quaternion.FromToRotation(tangents[i - 1], tangents[i]) * normal;
                normal = Vector3.ProjectOnPlane(normal, tangents[i]).normalized;
                length += Vector3.Distance(rings[i - 1].Center, rings[i].Center);
            }
            Vector3 side = Vector3.Cross(normal, tangents[i]);
            float t = (float)i / (n - 1);
            for (int s = 0; s <= sides; s++)
            {
                float u = (float)s / sides;
                float a = u * Mathf.PI * 2f;
                float k = shape != null ? shape(a, t) : 1f;
                Vector3 off = side * (Mathf.Cos(a) * rings[i].Radius.x * k) + normal * (Mathf.Sin(a) * rings[i].Radius.y * k);
                Vert(rings[i].Center + off, new Vector2(u * Mathf.Max(1f, Mathf.Round(circumference * uvScale)), length * uvScale));
            }
        }
        for (int i = 0; i < n - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = start + i * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                Tri(a, b, c);
                Tri(b, d, c);
            }
        }

        int keep = Sub;
        if (capSub >= 0) Sub = capSub;
        if (capStart && rings[0].Radius.sqrMagnitude > 1e-8f) Cap(start, sides, rings[0].Center, false);
        if (capEnd && rings[n - 1].Radius.sqrMagnitude > 1e-8f) Cap(start + (n - 1) * (sides + 1), sides, rings[n - 1].Center, true);
        Sub = keep;
    }

    /// <summary>Крышка: веер из отдельных вершин (своя нормаль, а не сглаженная с боком).</summary>
    private void Cap(int ringStart, int sides, Vector3 center, bool end)
    {
        Matrix4x4 keep = Xf;
        Xf = Matrix4x4.identity; // вершины кольца уже преобразованы
        int c = Vert(keep.MultiplyPoint3x4(center), new Vector2(0.5f, 0.5f));
        int first = _v.Count;
        for (int s = 0; s <= sides; s++)
        {
            float a = (float)s / sides * Mathf.PI * 2f;
            Vert(_v[ringStart + s], new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        Xf = keep;
        for (int s = 0; s < sides; s++)
        {
            if (end) Tri(c, first + s, first + s + 1);
            else Tri(c, first + s + 1, first + s);
        }
        _flat.Add((c, _v.Count));
    }

    // Диапазоны вершин, которые не свариваются с соседями (крышки, плоские части).
    private readonly List<(int from, int to)> _flat = new List<(int, int)>();

    /// <summary>Лофт по сглаженной кривой через опорные точки (Catmull-Rom) с радиусом по доле длины.</summary>
    public void Tube(Vector3[] points, int segments, int sides, System.Func<float, Vector2> radius, Vector3 up,
        bool capStart = true, bool capEnd = true, System.Func<float, float, float> shape = null, float uvScale = 1f, int capSub = -1)
    {
        var rings = new List<Ring>();
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 r = radius(t);
            rings.Add(new Ring(Spline(points, t), r.x, r.y));
        }
        Loft(rings, sides, up, capStart, capEnd, shape, uvScale, capSub);
    }

    /// <summary>Точка сглаженной кривой Catmull-Rom через опорные точки, t от 0 до 1.</summary>
    public static Vector3 Spline(Vector3[] p, float t)
    {
        if (p.Length == 1) return p[0];
        float f = Mathf.Clamp01(t) * (p.Length - 1);
        int i = Mathf.Min((int)f, p.Length - 2);
        float u = f - i;
        Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(p.Length - 1, i + 2)];
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
    }

    /// <summary>
    /// Пластина по контуру в плоскости XY (звёздчатому относительно центра масс) толщиной thickness по Z:
    /// уши, крылья, плавники, листья. Обе стороны и кромка — отдельные плоские грани.
    /// bend (необязательно) прогибает пластину: смещение по Z от точки контура.
    /// </summary>
    public void Plate(Vector2[] outline, float thickness, Matrix4x4 local, System.Func<Vector2, float> bend = null)
    {
        Matrix4x4 keep = Xf;
        Xf = keep * local;
        int from = _v.Count;
        Vector2 c = Vector2.zero;
        foreach (Vector2 p in outline) c += p;
        c /= outline.Length;
        int n = outline.Length;
        float h = thickness * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            float bc = bend != null ? bend(c) : 0f;
            int ci = Vert(new Vector3(c.x, c.y, bc + side * h * 1.6f), c);
            int first = _v.Count;
            for (int i = 0; i < n; i++)
            {
                float b = bend != null ? bend(outline[i]) : 0f;
                Vert(new Vector3(outline[i].x, outline[i].y, b + side * h * 0.3f), outline[i]);
            }
            for (int i = 0; i < n; i++)
            {
                int a = first + i, bnext = first + (i + 1) % n;
                if (side > 0) Tri(ci, a, bnext);
                else Tri(ci, bnext, a);
            }
        }
        Xf = keep;
        _flat.Add((from, _v.Count));
    }

    /// <summary>Конус от основания к вершине (иголки, когти, шипы, клюв).</summary>
    public void Cone(Vector3 from, Vector3 to, float radius, int sides = 6, float flatten = 1f)
    {
        Vector3 dir = to - from;
        Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Loft(new[] { new Ring(from, radius, radius * flatten), new Ring(to, 0f) }, sides, up, true, false);
    }

    /// <summary>Гранёная «капля» (икосфера с шумом) — камни, комья листвы, кусты. Вершины не свариваются.</summary>
    public void Blob(Vector3 center, Vector3 radii, int subdivisions, float roughness, int seed, Quaternion rotation)
    {
        Icosphere(subdivisions, out List<Vector3> verts, out List<int> tris);
        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f, oz = (float)rng.NextDouble() * 100f;
        int from = _v.Count;
        for (int i = 0; i < tris.Count; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = verts[tris[i + k]];
                float noise = Mathf.PerlinNoise(p.x * 1.9f + ox, p.y * 1.9f + oy) * 0.6f + Mathf.PerlinNoise(p.z * 1.9f + oz, p.x * 1.9f + oy) * 0.4f;
                p *= 1f + (noise - 0.5f) * 2f * roughness;
                Vert(center + rotation * Vector3.Scale(p, radii), new Vector2(p.x + p.z, p.y));
            }
            Tri(_v.Count - 3, _v.Count - 2, _v.Count - 1);
        }
        _flat.Add((from, _v.Count));
    }

    /// <summary>Отдельный плоский треугольник (лицом туда, откуда обход a→b→c виден по часовой).</summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA = default, Vector2 uvB = default, Vector2 uvC = default)
    {
        int from = _v.Count;
        int i = Vert(a, uvA), j = Vert(b, uvB), k = Vert(c, uvC);
        Tri(i, j, k);
        _flat.Add((from, _v.Count));
    }

    /// <summary>Плоское кольцо (или диск при rIn = 0) в плоскости, перпендикулярной normal; UV — радиальные (годовые кольца спила).</summary>
    public void Annulus(Vector3 center, Vector3 normal, float rIn, float rOut, int segments, float jitter = 0f, int seed = 0)
    {
        normal.Normalize();
        Vector3 a = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 b = Vector3.Cross(normal, a);
        var rng = new System.Random(seed);
        int from = _v.Count;
        int c = rIn <= 0f ? Vert(center, new Vector2(0.5f, 0.5f)) : -1;
        int ring = _v.Count;
        for (int s = 0; s <= segments; s++)
        {
            float ang = (float)s / segments * Mathf.PI * 2f;
            float k = s == segments ? 1f : 1f + ((float)rng.NextDouble() - 0.5f) * jitter;
            Vector3 dir = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
            Vert(center + dir * rOut * k, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            if (rIn > 0f) Vert(center + dir * rIn, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f * rIn / rOut, 0.5f + Mathf.Sin(ang) * 0.5f * rIn / rOut));
        }
        for (int s = 0; s < segments; s++)
        {
            if (rIn <= 0f)
            {
                Tri(c, ring + s, ring + s + 1);
            }
            else
            {
                int o0 = ring + s * 2, i0 = o0 + 1, o1 = o0 + 2, i1 = o0 + 3;
                Tri(i0, o0, o1);
                Tri(i0, o1, i1);
            }
        }
        _flat.Add((from, _v.Count));
    }

    /// <summary>Добавить готовый меш (все его подмеши — в текущий) с преобразованием.</summary>
    public void Append(MeshDraft other, Matrix4x4 m)
    {
        int offset = _v.Count;
        for (int i = 0; i < other._v.Count; i++)
        {
            _v.Add((Xf * m).MultiplyPoint3x4(other._v[i]));
            _uv.Add(other._uv[i]);
        }
        for (int s = 0; s < other._sub.Count; s++)
        {
            while (_sub.Count <= s) _sub.Add(new List<int>());
            foreach (int i in other._sub[s]) _sub[s].Add(i + offset);
        }
        foreach (var f in other._flat) _flat.Add((f.from + offset, f.to + offset));
    }

    // ---------------- Сборка ----------------

    /// <summary>Собрать меш. flat — все грани плоские (стилизованная листва, камни); иначе сглаживание со сваркой по позиции.</summary>
    public Mesh ToMesh(string name, bool flat = false, bool mirrorX = false)
    {
        if (mirrorX) return Mirrored().ToMesh(name, flat);
        var mesh = new Mesh { name = name };
        if (flat)
        {
            var fv = new List<Vector3>();
            var fuv = new List<Vector2>();
            var subs = new List<int[]>();
            foreach (List<int> s in _sub)
            {
                var idx = new int[s.Count];
                for (int i = 0; i < s.Count; i++)
                {
                    idx[i] = fv.Count;
                    fv.Add(_v[s[i]]);
                    fuv.Add(_uv[s[i]]);
                }
                subs.Add(idx);
            }
            if (fv.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(fv);
            mesh.SetUVs(0, fuv);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateNormals();
        }
        else
        {
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _uv);
            mesh.subMeshCount = _sub.Count;
            for (int i = 0; i < _sub.Count; i++) mesh.SetTriangles(_sub[i], i);
            mesh.SetNormals(SmoothNormals());
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Зеркальная по X копия (левое крыло из правого): вершины отражены, обход треугольников развёрнут.</summary>
    private MeshDraft Mirrored()
    {
        var m = new MeshDraft(_sub.Count);
        foreach (Vector3 p in _v) m._v.Add(new Vector3(-p.x, p.y, p.z));
        m._uv.AddRange(_uv);
        for (int s = 0; s < _sub.Count; s++)
            for (int i = 0; i < _sub[s].Count; i += 3)
            {
                m._sub[s].Add(_sub[s][i]);
                m._sub[s].Add(_sub[s][i + 2]);
                m._sub[s].Add(_sub[s][i + 1]);
            }
        m._flat.AddRange(_flat);
        return m;
    }

    private Vector3[] SmoothNormals()
    {
        var acc = new Vector3[_v.Count];
        foreach (List<int> s in _sub)
        {
            for (int i = 0; i < s.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(_v[s[i + 1]] - _v[s[i]], _v[s[i + 2]] - _v[s[i]]);
                acc[s[i]] += n; acc[s[i + 1]] += n; acc[s[i + 2]] += n;
            }
        }
        var flat = new bool[_v.Count];
        foreach (var f in _flat) for (int i = f.from; i < f.to; i++) flat[i] = true;

        // Сварка по позиции (с допуском) только для сглаживаемых вершин.
        var groups = new Dictionary<Vector3Int, Vector3>();
        var keys = new Vector3Int[_v.Count];
        for (int i = 0; i < _v.Count; i++)
        {
            if (flat[i]) continue;
            Vector3 p = _v[i] * 20000f;
            keys[i] = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            groups.TryGetValue(keys[i], out Vector3 sum);
            groups[keys[i]] = sum + acc[i];
        }
        var normals = new Vector3[_v.Count];
        for (int i = 0; i < _v.Count; i++)
        {
            Vector3 n = flat[i] ? acc[i] : groups[keys[i]];
            normals[i] = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
        }
        return normals;
    }

    private static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        verts = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        tris = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = next;
        }
    }

    private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int i)) return i;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        cache[key] = verts.Count - 1;
        return verts.Count - 1;
    }
}

/// <summary>Сохранение сгенерированных мешей ассетами (с сохранением GUID при пересборке).</summary>
public static class MeshKit
{
    public const string FOLDER = "Assets/Meshes/Models";

    public static Mesh Save(Mesh mesh, string name)
    {
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets/Meshes", "Models");
        string path = FOLDER + "/" + name + ".asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    /// <summary>Объект с мешем и материалами по подмешам; без коллайдера.</summary>
    public static GameObject Part(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return go;
    }

    public static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 s) => Matrix4x4.TRS(p, Quaternion.Euler(euler), s);
    public static Matrix4x4 TRS(Vector3 p, Vector3 euler, float s = 1f) => Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one * s);
}
