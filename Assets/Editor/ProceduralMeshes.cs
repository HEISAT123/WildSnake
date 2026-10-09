using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Генерация мешей окружения кодом: рельеф, «низкополигональные» камни и кроны, конусы елей,
/// пучки травы и объединение множества копий в один меш. Вызывается только из редактора.
/// </summary>
public static class ProceduralMeshes
{
    /// <summary>Рельеф: сетка resolution x resolution вершин, высоты из heightAt, UV на всю площадь.</summary>
    public static Mesh Terrain(int resolution, float worldSize, Vector3 center, System.Func<float, float, float> heightAt)
    {
        var vertices = new Vector3[resolution * resolution];
        var uvs = new Vector2[vertices.Length];
        float step = worldSize / (resolution - 1);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float wx = center.x - worldSize * 0.5f + x * step;
                float wz = center.z - worldSize * 0.5f + y * step;
                int i = y * resolution + x;
                vertices[i] = new Vector3(wx, heightAt(wx, wz), wz);
                uvs[i] = new Vector2((float)x / (resolution - 1), (float)y / (resolution - 1));
            }
        }

        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int t = 0;
        for (int y = 0; y < resolution - 1; y++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = y * resolution + x;
                triangles[t++] = i;
                triangles[t++] = i + resolution;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + resolution;
                triangles[t++] = i + resolution + 1;
            }
        }

        var mesh = new Mesh { name = "Terrain" };
        if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Неровная «капля»: икосфера с шумовым смещением вершин и плоскими гранями (вершины не общие),
    /// поэтому выглядит как стилизованный камень или крона.
    /// </summary>
    public static Mesh Blob(string name, int subdivisions, float roughness, int seed)
    {
        BuildIcosphere(subdivisions, out List<Vector3> verts, out List<int> tris);

        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f, oz = (float)rng.NextDouble() * 100f;
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            float n = Mathf.PerlinNoise(v.x * 1.7f + ox, v.y * 1.7f + oy) * 0.6f + Mathf.PerlinNoise(v.z * 1.7f + oz, v.x * 1.7f + oy) * 0.4f;
            verts[i] = v * (0.5f * (1f + (n - 0.5f) * 2f * roughness));
        }

        return Faceted(name, verts, tris, true);
    }

    /// <summary>Конус (ярус ели) с плоскими гранями и дном.</summary>
    public static Mesh Cone(string name, int segments)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var apex = new Vector3(0f, 1f, 0f);
        var bottom = Vector3.zero;
        for (int s = 0; s < segments; s++)
        {
            float a0 = s * Mathf.PI * 2f / segments;
            float a1 = (s + 1) * Mathf.PI * 2f / segments;
            var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            AddTriangle(verts, tris, p1, p0, apex);
            AddTriangle(verts, tris, p0, p1, bottom);
        }
        return Faceted(name, verts, tris, false);
    }

    /// <summary>Пучок травы: три перекрещённые двусторонние плоскости. UV на всю текстуру травинок.</summary>
    public static Mesh GrassTuft(string name)
    {
        var mesh = new Mesh { name = name };
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int q = 0; q < 3; q++)
        {
            float a = q * Mathf.PI / 3f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
            for (int side = 0; side < 2; side++)
            {
                int b = verts.Count;
                verts.Add(-dir); verts.Add(dir); verts.Add(dir + Vector3.up); verts.Add(-dir + Vector3.up);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                // Нормали вверх: так пучки освещаются ровно с обеих сторон, как трава.
                for (int k = 0; k < 4; k++) normals.Add(Vector3.up);
                if (side == 0)
                {
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }
                else
                {
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Объединить много копий меша (разные позиции/повороты/масштабы) в один — один вызов отрисовки.</summary>
    public static Mesh Combine(string name, Mesh source, List<Matrix4x4> transforms)
    {
        var combine = new CombineInstance[transforms.Count];
        for (int i = 0; i < transforms.Count; i++)
            combine[i] = new CombineInstance { mesh = source, transform = transforms[i] };

        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.CombineMeshes(combine, true, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ===================== Внутреннее =====================

    private static Mesh Faceted(string name, List<Vector3> verts, List<int> tris, bool sphericalUv)
    {
        var outVerts = new Vector3[tris.Count];
        var outUv = new Vector2[tris.Count];
        var outTris = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            Vector3 v = verts[tris[i]];
            outVerts[i] = v;
            outTris[i] = i;
            outUv[i] = sphericalUv
                ? new Vector2(Mathf.Atan2(v.z, v.x) / (Mathf.PI * 2f) + 0.5f, v.y + 0.5f) * 2f
                : new Vector2(v.x + 0.5f, v.y + v.z + 0.5f);
        }

        var mesh = new Mesh { name = name };
        mesh.vertices = outVerts;
        mesh.uv = outUv;
        mesh.triangles = outTris;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTriangle(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    /// <summary>Икосфера единичного радиуса: 20 граней, каждая делится на 4 subdivisions раз.</summary>
    private static void BuildIcosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        verts = new List<Vector3>
        {
            new Vector3(-1, t, 0).normalized, new Vector3(1, t, 0).normalized, new Vector3(-1, -t, 0).normalized, new Vector3(1, -t, 0).normalized,
            new Vector3(0, -1, t).normalized, new Vector3(0, 1, t).normalized, new Vector3(0, -1, -t).normalized, new Vector3(0, 1, -t).normalized,
            new Vector3(t, 0, -1).normalized, new Vector3(t, 0, 1).normalized, new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized
        };
        tris = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        var cache = new Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++)
        {
            var next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Midpoint(verts, cache, a, b);
                int bc = Midpoint(verts, cache, b, c);
                int ca = Midpoint(verts, cache, c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = next;
        }
        // Порядок вершин уже даёт нормали наружу в соглашении Unity: cross(B-A, C-A) смотрит от центра.
    }

    private static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
        if (cache.TryGetValue(key, out int index)) return index;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        index = verts.Count - 1;
        cache[key] = index;
        return index;
    }
}
