using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WildSnake;

/// <summary>
/// Снимки моделей для проверки внешнего вида без игры: существа на «подиуме» над миром и виды поля из собранной сцены.
/// Листы PNG — в папке Screenshots/ рядом с проектом (не в git). Batch: -executeMethod ModelGallery.Capture (без -nographics).
/// Сцену не сохраняет.
/// </summary>
public static class ModelGallery
{
    private const string OUT = "Screenshots";
    private const int TILE_W = 480, TILE_H = 360;

    [MenuItem("Tools/Capture Model Gallery")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WildSnake.unity");
        // Иначе первые кадры рисуются заглушкой, пока шейдер компилируется.
        ShaderUtil.allowAsyncCompilation = false;
        Directory.CreateDirectory(OUT);
        RenderSettings.fog = false;
        CaptureCreatures();
        CaptureNature();
        CaptureWorld();
        LogMeshStats();
        Debug.Log("ModelGallery: снимки в " + Path.GetFullPath(OUT));
    }

    /// <summary>
    /// Сравнение нагрузки двух сцен (Assets/Scenes/OldScene.unity, если есть, и текущей): рендереры, «вызовы»
    /// (рендерер × подмеш, без учёта батчинга), треугольники, тени, материалы — по корневым объектам и на одно существо.
    /// Результат — Screenshots/stats.txt. Batch: -executeMethod ModelGallery.SceneStats.
    /// </summary>
    [MenuItem("Tools/Scene Render Stats")]
    public static void SceneStats()
    {
        Directory.CreateDirectory(OUT);
        var sb = new System.Text.StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/OldScene.unity", "Assets/Scenes/WildSnake.unity" })
        {
            if (!File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path);
            sb.AppendLine("===== " + path + " (" + (new FileInfo(path).Length / 1048576f).ToString("0.0") + " MB)");
            long draws = 0, tris = 0, shadowTris = 0, verts = 0;
            int renderers = 0, shadowCasters = 0;
            var materials = new HashSet<Material>();
            var groups = new SortedDictionary<string, long[]>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var g = new long[3];
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled) continue;
                    var f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    Mesh m = f.sharedMesh;
                    long t = 0;
                    for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
                    renderers++;
                    draws += m.subMeshCount;
                    tris += t;
                    verts += m.vertexCount;
                    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) { shadowCasters++; shadowTris += t; }
                    foreach (Material mat in r.sharedMaterials) if (mat != null) materials.Add(mat);
                    g[0]++; g[1] += m.subMeshCount; g[2] += t;
                    if (root.name == "Environment" || root.name == "Board")
                    {
                        // Подробнее по слоям окружения: второй уровень иерархии или имя объекта.
                        Transform top = r.transform;
                        while (top.parent != null && top.parent != root.transform) top = top.parent;
                        string key = root.name + "/" + (top.childCount > 0 ? top.name : r.name);
                        if (!groups.TryGetValue(key, out long[] sub)) groups[key] = sub = new long[3];
                        sub[0]++; sub[1] += m.subMeshCount; sub[2] += t;
                    }
                }
                if (g[0] > 0) groups[root.name] = g;
            }
            sb.AppendLine("Активные: рендереров " + renderers + ", вызовов " + draws + ", треугольников " + tris + ", вершин " + verts);
            sb.AppendLine("Тени: рендереров " + shadowCasters + ", треугольников " + shadowTris + "; материалов " + materials.Count);
            foreach (var kv in groups) sb.AppendLine("  " + kv.Key + ": рендереров " + kv.Value[0] + ", вызовов " + kv.Value[1] + ", треуг. " + kv.Value[2]);
            sb.AppendLine("На одно существо (с выключенными частями):");
            var seen = new HashSet<string>();
            foreach (PreyAnimal p in Object.FindObjectsByType<PreyAnimal>(FindObjectsInactive.Include))
                if (seen.Add(p.name)) sb.AppendLine("  " + p.name + ": " + Cost(p.gameObject));
            foreach (string n in new[] { "Fox", "Eagle", "WaspNests/GuardWasp", "BirdNests/NestBird", "Snake/Head", "Hatchlings/Hatchling" })
            {
                string[] parts = n.Split('/');
                GameObject go = null;
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == parts[0]) go = parts.Length > 1 ? root.transform.Find(parts[1])?.gameObject : root;
                if (go != null) sb.AppendLine("  " + n + ": " + Cost(go));
            }
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(OUT, "stats.txt"), sb.ToString());
        Debug.Log("ModelGallery: статистика в " + Path.GetFullPath(Path.Combine(OUT, "stats.txt")));
    }

    private static string Cost(GameObject go)
    {
        int r = 0;
        long draws = 0, tris = 0;
        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var f = mr.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null || mr.name == "ScentMark") continue;
            r++;
            draws += f.sharedMesh.subMeshCount;
            for (int i = 0; i < f.sharedMesh.subMeshCount; i++) tris += f.sharedMesh.GetIndexCount(i) / 3;
        }
        return "рендереров " + r + ", вызовов " + draws + ", треуг. " + tris;
    }

    /// <summary>Треугольники сгенерированных мешей — в Screenshots/meshes.txt (бюджет производительности).</summary>
    private static void LogMeshStats()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshKit.FOLDER }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            long tris = 0;
            for (int i = 0; i < m.subMeshCount; i++) tris += m.GetIndexCount(i) / 3;
            sb.AppendLine(m.name + ": " + tris + " tris, " + m.vertexCount + " verts, " + m.subMeshCount + " sub");
        }
        // Меши, которые живут прямо в сцене (не ассеты), — главный вклад в размер файла сцены.
        sb.AppendLine("--- scene meshes ---");
        var sizes = new Dictionary<string, long>();
        foreach (MeshFilter f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            Mesh m = f.sharedMesh;
            if (m == null || AssetDatabase.Contains(m)) continue;
            sizes.TryGetValue(m.name, out long v);
            sizes[m.name] = v + m.vertexCount;
        }
        foreach (var kv in sizes) sb.AppendLine(kv.Key + ": " + kv.Value + " verts");
        File.WriteAllText(Path.Combine(OUT, "meshes.txt"), sb.ToString());
    }

    // ---------------- Существа ----------------

    private static void CaptureCreatures()
    {
        var subjects = new List<(string name, GameObject go)>();
        Add(subjects, "Fox", FindChild("Fox", "FoxModel"));
        Add(subjects, "Eagle", FindChild("Eagle", "EagleModel"));
        var seen = new HashSet<string>();
        foreach (PreyAnimal p in Object.FindObjectsByType<PreyAnimal>(FindObjectsInactive.Include))
            if (seen.Add(p.name)) Add(subjects, p.name, p.gameObject);
        Add(subjects, "NestBird", FindChild("BirdNests", "NestBird"));
        Add(subjects, "Wasp", FindChild("WaspNests", "GuardWasp"));
        Add(subjects, "Hatchling", FindChild("Hatchlings", "Hatchling"));
        foreach (SnakeController s in Object.FindObjectsByType<SnakeController>(FindObjectsInactive.Include))
            Add(subjects, s.name + "Head", s.transform.Find("Head")?.gameObject);

        Vector3 studio = new Vector3(0f, 400f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = studio;
        ground.transform.localScale = Vector3.one * 4f;
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        groundMat.SetColor("_BaseColor", new Color(0.42f, 0.5f, 0.32f));
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        SetupSun(Quaternion.Euler(42f, -35f, 0f));

        var tiles = new List<Texture2D>();
        foreach (var (name, src) in subjects)
        {
            GameObject clone = Object.Instantiate(src);
            clone.name = name;
            clone.SetActive(true);
            foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                if (t.name == "ScentMark") t.gameObject.SetActive(false);
            foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                if (t.name != "ScentMark" && t != clone.transform && !t.gameObject.activeSelf && t.name.EndsWith("Model")) t.gameObject.SetActive(true);
            clone.transform.SetPositionAndRotation(studio, Quaternion.identity);
            clone.transform.localScale = src.transform.lossyScale;
            Bounds b = BoundsOf(clone);
            // Ставим на пол.
            clone.transform.position += Vector3.up * (studio.y - b.min.y);
            b = BoundsOf(clone);
            tiles.Add(Shot(b, new Vector3(1f, 0.55f, 1.2f), name));
            tiles.Add(Shot(b, new Vector3(-1.3f, 0.35f, -0.25f), name + " side"));
            Object.DestroyImmediate(clone);
        }
        Object.DestroyImmediate(ground);
        SaveSheet(tiles, "creatures");
    }

    // ---------------- Природа крупно ----------------

    private static void CaptureNature()
    {
        Vector3 studio = new Vector3(0f, 400f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = studio;
        ground.transform.localScale = Vector3.one * 4f;
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        groundMat.SetColor("_BaseColor", new Color(0.42f, 0.5f, 0.32f));
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        SetupSun(Quaternion.Euler(42f, -35f, 0f));

        var sets = new List<(string, (string mesh, string mat)[], Vector3 scale)>
        {
            ("tree 0", new[] { ("TreeTrunk0", "Bark"), ("TreeCrown0", "ArenaLeaves") }, Vector3.one),
            ("tree 1", new[] { ("TreeTrunk1", "Bark"), ("TreeCrown1", "ArenaLeaves") }, Vector3.one),
            ("tree 1 trunk", new[] { ("TreeTrunk1", "Bark") }, Vector3.one),
            ("pine 0", new[] { ("PineTrunk0", "Bark"), ("PineCrown0", "ArenaPine") }, Vector3.one),
            ("birch 0", new[] { ("BirchTrunk0", "BirchBark"), ("BirchCrown0", "ArenaBirchLeaves") }, Vector3.one),
            ("birch 1 trunk", new[] { ("BirchTrunk1", "BirchBark") }, Vector3.one),
            ("dead 0", new[] { ("DeadTree0", "DeadBark") }, Vector3.one),
            ("stump 0", new[] { ("Stump0", "Bark"), ("StumpTop", "StumpTop") }, new Vector3(0.8f, 1.3f, 0.8f)),
            ("log 4", new[] { ("Log4", "Bark"), ("LogEnds4", "StumpTop") }, new Vector3(1f, 0.5f, 0.5f)),
            ("hollow 3", new[] { ("HollowOuter3", "Bark"), ("HollowInner3", "BurrowHole"), ("HollowRims3", "StumpTop") }, Vector3.one),
            ("bramble", new[] { ("BrambleBush0", "Bramble"), ("BrambleBush1", "Bramble") }, Vector3.one * 0.8f),
            ("mushroom", new[] { ("MushroomStem", "MushroomStem"), ("MushroomCap", "MushroomCap") }, Vector3.one * 2f),
            ("wasp nest", new[] { ("WaspNestBody", "WaspNestPaper") }, Vector3.one),
            ("nest", new[] { ("NestRing", "Bark"), ("Egg", "SnakeEgg") }, Vector3.one * 0.6f),
            ("lily + cattail", new[] { ("LilyPad", "LilyPad"), ("CattailHead", "Cattail") }, Vector3.one),
            ("mound", new[] { ("Mound", "BurrowDirt") }, Vector3.one),
            ("burrow", new[] { ("BurrowMound", "BurrowDirt"), ("BurrowPit", "BurrowHole") }, Vector3.one),
            ("boulder", new[] { ("Boulder0", "ObstacleBlock") }, new Vector3(2.5f, 1.4f, 2.5f)),
            ("standing stone", new[] { ("StandingStone0", "ObstacleBlock") }, new Vector3(1f, 1.8f, 0.9f)),
            ("crag", new[] { ("Crag1", "ObstacleLowWall") }, Vector3.one * 5f),
        };
        var tiles = new List<Texture2D>();
        foreach (var (label, parts, scale) in sets)
        {
            var root = new GameObject(label);
            root.transform.position = studio;
            foreach (var (meshName, matName) in parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshKit.FOLDER + "/" + meshName + ".asset");
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + matName + ".mat");
                if (mesh == null) { Debug.LogWarning("ModelGallery: нет меша " + meshName); continue; }
                GameObject go = MeshKit.Part(meshName, root.transform, mesh, mat);
                go.transform.localScale = scale;
                if (label == "bramble" && meshName.EndsWith("1")) go.transform.localPosition = new Vector3(0.9f, 0f, 0f);
                if (meshName == "Egg") go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                if (meshName == "Egg") go.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
                if (meshName == "CattailHead") go.transform.localPosition = new Vector3(0.6f, 0.4f, 0f);
            }
            Bounds b = BoundsOf(root);
            root.transform.position += Vector3.up * Mathf.Max(0f, studio.y - b.min.y - 0.02f);
            b = BoundsOf(root);
            tiles.Add(Shot(b, new Vector3(1f, 0.45f, 1.2f), label));
            Object.DestroyImmediate(root);
        }
        Object.DestroyImmediate(ground);
        SaveSheet(tiles, "nature");
    }

    private static void Add(List<(string, GameObject)> list, string name, GameObject go)
    {
        if (go != null) list.Add((name, go));
    }

    private static GameObject FindChild(string root, string child)
    {
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name != root) continue;
            Transform t = go.transform.Find(child);
            return t != null ? t.gameObject : null;
        }
        return null;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Texture2D Shot(Bounds b, Vector3 viewDir, string label)
    {
        float radius = Mathf.Max(b.extents.magnitude, 0.05f);
        Vector3 dir = viewDir.normalized;
        Vector3 pos = b.center + dir * (radius / Mathf.Tan(22f * Mathf.Deg2Rad) * 1.05f);
        return Render(pos, Quaternion.LookRotation(b.center - pos), 44f, radius * 0.05f, label);
    }

    // ---------------- Мир ----------------

    private static void CaptureWorld()
    {
        var grid = Object.FindAnyObjectByType<GridManager>();
        if (grid == null) return;
        grid.GenerateLayout();
        SetupSun(Quaternion.Euler(38f, -50f, 0f));
        RenderSettings.fog = true;
        var tiles = new List<Texture2D>();

        Shoot(grid, tiles, "forest", c => grid.IsForest(c) && !grid.IsBirch(c) && grid.GetObstacle(c) == ObstacleType.Pillar, 6f, 1.6f);
        Shoot(grid, tiles, "forest far", c => grid.IsForest(c) && grid.GetObstacle(c) == ObstacleType.Pillar, 14f, 4f, 40);
        Shoot(grid, tiles, "birch", c => grid.IsBirch(c) && grid.GetObstacle(c) == ObstacleType.Pillar, 7f, 1.6f);
        Shoot(grid, tiles, "stump", c => grid.GetObstacle(c) == ObstacleType.Pillar && !grid.IsForest(c) && !grid.IsSwamp(c) && !grid.IsRidge(c), 3.5f, 1.4f);
        Shoot(grid, tiles, "log", c => grid.GetObstacle(c) == ObstacleType.LowWall, 4f, 1.3f);
        Shoot(grid, tiles, "boulder", c => grid.GetObstacle(c) == ObstacleType.Block, 6f, 1.8f);
        Shoot(grid, tiles, "swamp", c => grid.IsSwamp(c) && grid.GetObstacle(c) == ObstacleType.Pillar, 6f, 1.8f);
        Shoot(grid, tiles, "river", c => grid.IsRiver(c), 7f, 2.2f, 5);
        Shoot(grid, tiles, "reeds", c => grid.IsReed(c), 3f, 1.0f);
        Shoot(grid, tiles, "bramble", c => grid.IsBramble(c), 3.5f, 1.2f);
        if (grid.HollowLogCount > 0)
        {
            grid.GetHollowLog(0, out Vector3 a, out Vector3 b);
            ShootAt(tiles, "hollow log", (a + b) * 0.5f, 4.5f, 1.4f, 0.4f);
            Vector3 d = (b - a).normalized;
            Vector3 eye = b + d * 2.2f + Vector3.up * 0.5f;
            tiles.Add(Render(eye, Quaternion.LookRotation(b + Vector3.up * 0.4f - eye), 50f, 0.05f, "hollow end"));
        }
        if (grid.WaspNestCount > 0) ShootAt(tiles, "wasp nest", grid.CellToWorld(grid.GetWaspNestCell(0)), 3.5f, 1.8f, 1.4f);
        if (grid.HotSpringCount > 0) ShootAt(tiles, "hot spring", grid.GetHotSpring(0), 4f, 2.2f, 0f);
        if (grid.PondCount > 0)
        {
            // Пруд с кольцами ряби: проверка шейдера воды (время колец — «полсекунды назад» по часам шейдера).
            Vector3 pond = grid.GetPondCenter(0);
            pond.y = grid.WaterLevelAt(pond);
            var ripples = new Vector4[16];
            const float now = 100f;
            Shader.SetGlobalFloat("_WaterTime", now);
            for (int i = 0; i < 4; i++) ripples[i] = new Vector4(pond.x + i * 1.2f - 1.5f, pond.z + (i % 2) * 1.5f, now - 0.3f - i * 0.35f, 1f);
            Shader.SetGlobalVectorArray("_WaterRipples", ripples);
            Shader.SetGlobalFloat("_WaterRippleCount", 4f);
            ShootAt(tiles, "pond ripples", pond, 5f, 2.5f, 0f);
            ShootAt(tiles, "pond far", pond, 14f, 5f, 0f);
            Shader.SetGlobalFloat("_WaterRippleCount", 0f);
        }
        if (grid.BurrowCount > 0) ShootAt(tiles, "burrow", grid.GetBurrow(0), 3f, 1.2f, 0.2f);
        if (grid.BirdNestCount > 0) ShootAt(tiles, "bird nest", grid.GetBirdNest(0), 1.6f, 1.0f, 0.1f);
        if (grid.HasNest) ShootAt(tiles, "snake nest", grid.NestPosition, 2.6f, 1.6f, 0.1f);
        if (grid.ColonyHoleCount > 0) ShootAt(tiles, "mouse colony", grid.GetColonyHole(0), 2.5f, 1.2f, 0.1f);
        Shoot(grid, tiles, "mushrooms", c => grid.IsForest(c) && grid.GetObstacle(c) == ObstacleType.None && !grid.IsCover(c), 2.5f, 0.9f, 60);
        foreach (Vector3 dir in new[] { Vector3.forward, Vector3.right })
        {
            Vector3 c = grid.Center;
            Vector3 eye = c + dir * (TerrainShape.PlayRadius - 14f);
            eye.y = grid.GroundHeight(eye) + 3f;
            Vector3 look = c + dir * (TerrainShape.PlayRadius + 30f);
            look.y = grid.GroundHeight(look) + 4f;
            tiles.Add(Render(eye, Quaternion.LookRotation(look - eye), 60f, 0.1f, "mountains"));
        }
        SaveSheet(tiles, "world");
    }

    private static void Shoot(GridManager grid, List<Texture2D> tiles, string label, System.Func<Vector2Int, bool> match,
        float distance, float height, int skip = 0)
    {
        Vector2Int center = new Vector2Int(grid.Width / 2, grid.Height / 2);
        // Ищем подходящую клетку по спирали от центра, чтобы снимки были одинаковыми от сборки к сборке.
        int found = 0;
        for (int r = 0; r < grid.Width; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = new Vector2Int(center.x + dx, center.y + dy);
                    if (!grid.IsInside(c) || !match(c)) continue;
                    if (found++ < skip) continue;
                    ShootAt(tiles, label, grid.CellToWorld(c), distance, height, 0.6f);
                    return;
                }
            }
        }
    }

    private static void ShootAt(List<Texture2D> tiles, string label, Vector3 target, float distance, float height, float lookHeight)
    {
        Vector3 look = target + Vector3.up * lookHeight;
        Vector3 dir = new Vector3(0.7f, 0f, -0.7f);
        Vector3 eye = target + dir * distance;
        eye.y = Mathf.Max(eye.y, target.y) + height;
        tiles.Add(Render(eye, Quaternion.LookRotation(look - eye), 55f, 0.1f, label));
    }

    // ---------------- Рендер ----------------

    private static void SetupSun(Quaternion rotation)
    {
        foreach (Light l in Object.FindObjectsByType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            bool sun = l.name == "Sun";
            l.enabled = sun;
            if (!sun) continue;
            l.transform.rotation = rotation;
            l.intensity = 1.3f;
            l.color = new Color(1f, 0.96f, 0.88f);
        }
    }

    private static Texture2D Render(Vector3 pos, Quaternion rot, float fov, float near, string label)
    {
        var go = new GameObject("GalleryCamera");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        cam.nearClipPlane = Mathf.Max(0.01f, near);
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(TILE_W, TILE_H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(TILE_W, TILE_H, TextureFormat.RGB24, false) { name = label };
        tex.ReadPixels(new Rect(0, 0, TILE_W, TILE_H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        return tex;
    }

    private static void SaveSheet(List<Texture2D> tiles, string name)
    {
        if (tiles.Count == 0) return;
        const int cols = 4;
        int rows = (tiles.Count + cols - 1) / cols;
        var sheet = new Texture2D(cols * TILE_W, rows * TILE_H, TextureFormat.RGB24, false);
        var bg = new Color[sheet.width * sheet.height];
        for (int i = 0; i < bg.Length; i++) bg[i] = new Color(0.1f, 0.1f, 0.1f);
        sheet.SetPixels(bg);
        for (int i = 0; i < tiles.Count; i++)
        {
            int cx = i % cols, cy = rows - 1 - i / cols;
            sheet.SetPixels(cx * TILE_W, cy * TILE_H, TILE_W, TILE_H, tiles[i].GetPixels());
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(OUT, name + ".png"), sheet.EncodeToPNG());
        var labels = new System.Text.StringBuilder();
        for (int i = 0; i < tiles.Count; i++) labels.AppendLine((i + 1) + ": " + tiles[i].name);
        File.WriteAllText(Path.Combine(OUT, name + ".txt"), labels.ToString());
        foreach (Texture2D t in tiles) Object.DestroyImmediate(t);
        Object.DestroyImmediate(sheet);
    }
}
