using System.Collections.Generic;
using Snake3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Окружение вокруг арены: процедурное небо, рельеф с холмами, горами и озером, лес, камни, кусты,
/// трава, цветы, фонари, облака и пыльца в воздухе. Плюс текстурированные материалы самой арены.
/// Всё строится кодом из примитивов и сгенерированных мешей/текстур, вызывается SnakeSceneBuilder.
/// </summary>
public static class EnvironmentBuilder
{
    private const string TEXTURE_FOLDER = "Assets/Materials/Environment";
    private const string MESH_FOLDER = "Assets/Meshes";

    private const float TERRAIN_SIZE = 360f;
    private const int TERRAIN_RESOLUTION = 181;
    private const float FLAT_HEIGHT = -0.22f;
    private const float WATER_HEIGHT = -0.75f;

    /// <summary>Текстурированные материалы арены для BoardView.</summary>
    public sealed class ArenaMaterials
    {
        public Material Floor;
        public Material Wall;
        public Material LowWall;
        public Material Pillar;
        public Material Block;
    }

    private static Vector3 _center;
    private static float _arenaHalf;
    private static Vector3 _lakeCenter;
    private static float _lakeRadius;

    // ===================== Материалы арены =====================

    public static ArenaMaterials CreateArenaMaterials()
    {
        EnsureFolders();

        ProceduralTextures.GrassFloor(256, out Texture2D grass, out Texture2D grassN);
        ProceduralTextures.StoneBricks(256, out Texture2D bricks, out Texture2D bricksN);
        ProceduralTextures.Rock(256, out Texture2D rock, out Texture2D rockN);
        ProceduralTextures.Marble(256, out Texture2D marble, out Texture2D marbleN);

        var m = new ArenaMaterials
        {
            Floor = Textured("Floor", Color.white, 0.12f, Save(grass), Save(grassN), 0.8f),
            Wall = Textured("Wall", Color.white, 0.2f, Save(bricks), Save(bricksN), 1.2f),
            LowWall = Textured("ObstacleLowWall", new Color(1f, 0.95f, 0.88f), 0.15f, Save(rock), Save(rockN), 1.2f),
            Pillar = Textured("ObstaclePillar", Color.white, 0.55f, Save(marble), Save(marbleN), 0.6f),
            Block = Textured("ObstacleBlock", new Color(0.85f, 0.82f, 0.9f), 0.15f, rock, rockN, 1.2f)
        };
        // Стены — длинные кубы: повторяем кладку вдоль, иначе кирпичи растянутся на всю длину.
        m.Wall.mainTextureScale = new Vector2(24f, 0.6f);
        return m;
    }

    // ===================== Небо и свет =====================

    /// <summary>Процедурное небо Unity (шейдер Skybox/Procedural), солнце, туман в цвет горизонта.</summary>
    public static void SetupSky(Light sun, Camera camera)
    {
        Material sky = SnakeSceneBuilder.LoadOrCreateMaterial("Sky", Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.045f);
        sky.SetFloat("_SunSizeConvergence", 6f);
        sky.SetFloat("_AtmosphereThickness", 0.85f);
        sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.8f));
        sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.4f));
        sky.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.76f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.88f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 260f;

        sun.color = new Color(1f, 0.93f, 0.8f);
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.farClipPlane = 500f;
    }

    // ===================== Окружение =====================

    public static void Build(GridManager grid, Material particleMaterial)
    {
        EnsureFolders();
        _center = grid.Center;
        _arenaHalf = Mathf.Max(grid.Width, grid.Height) * grid.CellSize * 0.5f;
        _lakeCenter = _center + new Vector3(-(_arenaHalf + 17f), 0f, _arenaHalf * 0.6f);
        _lakeRadius = 13f;

        var root = new GameObject("Environment").transform;
        var rng = new System.Random(4242);

        BuildTerrain(root);
        BuildWater(root);
        BuildForest(root, rng);
        BuildRocks(root, rng);
        BuildBushes(root, rng);
        BuildGrassAndFlowers(root, rng);
        BuildLanterns(root, grid);
        BuildClouds(root, rng);
        BuildPollen(root, particleMaterial);
    }

    /// <summary>
    /// Высота рельефа: ровная площадка вокруг арены, дальше холмы, ещё дальше хребты гор, сбоку озеро.
    /// Расстояние считается от скруглённого квадрата арены, чтобы углы не торчали.
    /// </summary>
    private static float TerrainHeight(float x, float z)
    {
        float dx = Mathf.Max(Mathf.Abs(x - _center.x) - _arenaHalf, 0f);
        float dz = Mathf.Max(Mathf.Abs(z - _center.z) - _arenaHalf, 0f);
        float d = Mathf.Sqrt(dx * dx + dz * dz) - 1.5f;
        if (d <= 0f) return FLAT_HEIGHT;

        float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 32f, d));
        float hills = Mathf.Max(0f, ProceduralTextures.Fbm(x * 0.022f, z * 0.022f, 4, 3.3f) - 0.3f) * 16f * ramp;
        float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(45f, 110f, d));
        float mountains = ProceduralTextures.RidgedFbm(x * 0.011f, z * 0.011f, 5, 9.1f) * 55f * mountainMask;
        float h = FLAT_HEIGHT + ramp * 0.6f + hills + mountains;

        float lake = new Vector2(x - _lakeCenter.x, z - _lakeCenter.z).magnitude;
        if (lake < _lakeRadius * 1.6f)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_lakeRadius * 1.6f, _lakeRadius * 0.5f, lake));
            h = Mathf.Lerp(h, -2.6f, k);
        }
        return h;
    }

    private static void BuildTerrain(Transform root)
    {
        Mesh mesh = SaveMesh(ProceduralMeshes.Terrain(TERRAIN_RESOLUTION, TERRAIN_SIZE, _center, TerrainHeight), "Terrain");
        Texture2D color = Save(ProceduralTextures.Terrain(1024, TerrainHeight, TERRAIN_SIZE, _center));
        Texture2D detail = Save(ProceduralTextures.Detail(256));

        Material mat = SnakeSceneBuilder.CreateLitMaterial("Terrain", Color.white, 0.08f);
        mat.mainTexture = color;
        if (mat.HasProperty("_DetailAlbedoMap"))
        {
            mat.SetTexture("_DetailAlbedoMap", detail);
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(140f, 140f));
            mat.EnableKeyword("_DETAIL_MULX2");
        }
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Terrain");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        MarkStatic(go);
    }

    private static void BuildWater(Transform root)
    {
        Material water = SnakeSceneBuilder.CreateLitMaterial("Water", new Color(0.16f, 0.38f, 0.5f, 0.78f), 0.95f);
        Texture2D normal = Save(ProceduralTextures.WaterNormal(256));
        SetNormalMap(water, normal, 0.6f);
        water.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
        SnakeSceneBuilder.MakeTransparent(water);

        GameObject plane = SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Plane, "Lake", root, water);
        float size = _lakeRadius * 3.4f / 10f; // Plane — 10x10 при единичном масштабе
        plane.transform.position = new Vector3(_lakeCenter.x, WATER_HEIGHT, _lakeCenter.z);
        plane.transform.localScale = new Vector3(size, 1f, size);
        plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void BuildForest(Transform root, System.Random rng)
    {
        Mesh cone = SaveMesh(ProceduralMeshes.Cone("PineTier", 9), "PineTier");
        Mesh crownA = SaveMesh(ProceduralMeshes.Blob("CrownA", 2, 0.45f, 11), "CrownA");
        Mesh crownB = SaveMesh(ProceduralMeshes.Blob("CrownB", 2, 0.5f, 23), "CrownB");

        Material bark = SnakeSceneBuilder.CreateLitMaterial("Bark", Color.white, 0.05f);
        bark.mainTexture = Save(ProceduralTextures.Bark(128));
        Material pine = FoliageMaterial("PineFoliage", new Color(0.08f, 0.24f, 0.12f), new Color(0.2f, 0.42f, 0.2f));
        Material leaves = FoliageMaterial("LeafFoliage", new Color(0.18f, 0.4f, 0.12f), new Color(0.45f, 0.68f, 0.22f));
        Material autumn = FoliageMaterial("AutumnFoliage", new Color(0.55f, 0.22f, 0.06f), new Color(0.95f, 0.62f, 0.15f));

        var forest = new GameObject("Forest").transform;
        forest.SetParent(root, false);

        int placed = 0;
        for (int attempt = 0; attempt < 1400 && placed < 230; attempt++)
        {
            if (!TryScatter(rng, 5f, 150f, 0.45f, 0.12f, out Vector3 pos)) continue;
            if (pos.y > 20f) continue;

            float scale = Lerp(rng, 0.8f, 1.6f);
            float yaw = Lerp(rng, 0f, 360f);
            bool isPine = rng.NextDouble() < 0.58;
            var tree = new GameObject(isPine ? "Pine" : "Tree").transform;
            tree.SetParent(forest, false);
            tree.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            tree.localScale = Vector3.one * scale;

            if (isPine)
            {
                AddPart(tree, SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Cylinder, "Trunk", tree, bark), new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.6f, 0.32f));
                AddMesh(tree, "Tier1", cone, pine, new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 2.3f, 2.8f), 0f, true);
                AddMesh(tree, "Tier2", cone, pine, new Vector3(0f, 2.0f, 0f), new Vector3(2.15f, 2.0f, 2.15f), 20f, true);
                AddMesh(tree, "Tier3", cone, pine, new Vector3(0f, 3.0f, 0f), new Vector3(1.4f, 1.8f, 1.4f), 40f, true);
            }
            else
            {
                Material crownMat = rng.NextDouble() < 0.14 ? autumn : leaves;
                AddPart(tree, SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Cylinder, "Trunk", tree, bark), new Vector3(0f, 1.2f, 0f), new Vector3(0.38f, 1.2f, 0.38f));
                AddMesh(tree, "Crown", crownA, crownMat, new Vector3(0f, 3.0f, 0f), new Vector3(3.2f, 2.6f, 3.2f), 0f, true);
                AddMesh(tree, "Crown2", crownB, crownMat, new Vector3(0.7f, 3.6f, 0.3f), new Vector3(2.2f, 1.9f, 2.2f), 60f, true);
                AddMesh(tree, "Crown3", crownA, crownMat, new Vector3(-0.6f, 3.4f, -0.4f), new Vector3(2.0f, 1.7f, 2.0f), 120f, true);
            }

            MarkStatic(tree.gameObject);
            placed++;
        }
    }

    private static void BuildRocks(Transform root, System.Random rng)
    {
        Material rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ObstacleLowWall.mat");
        var meshes = new Mesh[4];
        for (int i = 0; i < meshes.Length; i++)
            meshes[i] = SaveMesh(ProceduralMeshes.Blob("Boulder" + i, 2, 0.6f, 100 + i * 7), "Boulder" + i);

        var parent = new GameObject("Rocks").transform;
        parent.SetParent(root, false);
        int placed = 0;
        for (int attempt = 0; attempt < 800 && placed < 90; attempt++)
        {
            if (!TryScatter(rng, 3f, 140f, 0f, 1f, out Vector3 pos)) continue;
            float s = Lerp(rng, 0.6f, 3.2f);
            if (rng.NextDouble() < 0.15) s *= 2f; // изредка — большие глыбы
            var scale = new Vector3(s * Lerp(rng, 0.8f, 1.3f), s * Lerp(rng, 0.55f, 0.9f), s * Lerp(rng, 0.8f, 1.3f));
            pos.y -= scale.y * 0.2f; // немного утапливаем в землю
            AddMesh(parent, "Boulder", meshes[rng.Next(meshes.Length)], rock, pos, scale, Lerp(rng, 0f, 360f));
            placed++;
        }
    }

    private static void BuildBushes(Transform root, System.Random rng)
    {
        Mesh bush = SaveMesh(ProceduralMeshes.Blob("Bush", 1, 0.35f, 77), "Bush");
        Material bushMat = FoliageMaterial("BushFoliage", new Color(0.12f, 0.32f, 0.1f), new Color(0.35f, 0.58f, 0.2f));
        Material berry = SnakeSceneBuilder.CreateLitMaterial("Berries", new Color(0.8f, 0.1f, 0.25f), 0.7f);

        var parent = new GameObject("Bushes").transform;
        parent.SetParent(root, false);
        int placed = 0;
        for (int attempt = 0; attempt < 900 && placed < 140; attempt++)
        {
            if (!TryScatter(rng, 1.5f, 60f, 0.3f, 0.6f, out Vector3 pos)) continue;
            float s = Lerp(rng, 0.7f, 1.5f);
            var cluster = new GameObject("BushCluster").transform;
            cluster.SetParent(parent, false);
            cluster.position = pos;
            int parts = 2 + rng.Next(3);
            for (int k = 0; k < parts; k++)
            {
                var offset = new Vector3(Lerp(rng, -0.6f, 0.6f), 0.3f, Lerp(rng, -0.6f, 0.6f)) * s;
                AddMesh(cluster, "Bush", bush, bushMat, pos + offset, Vector3.one * s * Lerp(rng, 0.8f, 1.2f), Lerp(rng, 0f, 360f));
            }
            if (rng.NextDouble() < 0.3)
            {
                for (int k = 0; k < 5; k++)
                {
                    GameObject b = SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Sphere, "Berry", cluster, berry);
                    b.transform.position = pos + new Vector3(Lerp(rng, -0.6f, 0.6f), Lerp(rng, 0.4f, 0.8f), Lerp(rng, -0.6f, 0.6f)) * s;
                    b.transform.localScale = Vector3.one * 0.12f;
                }
            }
            MarkStatic(cluster.gameObject);
            placed++;
        }
    }

    /// <summary>Трава и цветы — тысячи копий, поэтому объединены в несколько больших мешей.</summary>
    private static void BuildGrassAndFlowers(Transform root, System.Random rng)
    {
        Mesh tuft = ProceduralMeshes.GrassTuft("GrassTuft");
        var grassTransforms = new List<Matrix4x4>();
        for (int attempt = 0; attempt < 6000 && grassTransforms.Count < 2600; attempt++)
        {
            if (!TryScatter(rng, 1.2f, 45f, 0f, 0.7f, out Vector3 pos)) continue;
            var scale = new Vector3(Lerp(rng, 0.5f, 0.95f), Lerp(rng, 0.3f, 0.65f), Lerp(rng, 0.5f, 0.95f));
            grassTransforms.Add(Matrix4x4.TRS(pos + Vector3.down * 0.03f, Quaternion.Euler(0f, Lerp(rng, 0f, 360f), 0f), scale));
        }

        Material grassMat = SnakeSceneBuilder.CreateLitMaterial("GrassTuft", Color.white, 0.05f);
        grassMat.mainTexture = Save(ProceduralTextures.GrassBlades(128));
        MakeCutout(grassMat);
        CreateCombined(root, "Grass", SaveMesh(ProceduralMeshes.Combine("GrassField", tuft, grassTransforms), "GrassField"), grassMat, false);

        Mesh head = SaveMesh(ProceduralMeshes.Blob("FlowerHead", 0, 0.2f, 5), "FlowerHead");
        Color[] colors = { new Color(1f, 0.92f, 0.25f), new Color(0.95f, 0.35f, 0.5f), new Color(0.6f, 0.45f, 1f), Color.white };
        string[] names = { "FlowerYellow", "FlowerPink", "FlowerViolet", "FlowerWhite" };
        for (int c = 0; c < colors.Length; c++)
        {
            var flowers = new List<Matrix4x4>();
            for (int attempt = 0; attempt < 1500 && flowers.Count < 160; attempt++)
            {
                if (!TryScatter(rng, 1.5f, 40f, 0.35f, 0.5f, out Vector3 pos)) continue;
                // Цветы растут полянками: группа из нескольких головок.
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = pos + new Vector3(Lerp(rng, -0.7f, 0.7f), Lerp(rng, 0.18f, 0.32f), Lerp(rng, -0.7f, 0.7f));
                    flowers.Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, Lerp(rng, 0f, 360f), 0f), Vector3.one * Lerp(rng, 0.12f, 0.2f)));
                }
            }
            Material mat = SnakeSceneBuilder.CreateLitMaterial(names[c], colors[c], 0.4f);
            SnakeSceneBuilder.SetEmission(mat, colors[c] * 0.15f);
            CreateCombined(root, names[c], SaveMesh(ProceduralMeshes.Combine(names[c] + "Field", head, flowers), names[c] + "Field"), mat, false);
        }
    }

    /// <summary>Фонари вдоль стен арены: столбик и светящийся плафон (без источников света — дёшево).</summary>
    private static void BuildLanterns(Transform root, GridManager grid)
    {
        Material post = SnakeSceneBuilder.CreateLitMaterial("LanternPost", new Color(0.18f, 0.16f, 0.15f), 0.5f);
        Material lamp = SnakeSceneBuilder.CreateLitMaterial("LanternLamp", new Color(1f, 0.85f, 0.55f), 0.9f);
        SnakeSceneBuilder.SetEmission(lamp, new Color(1.6f, 1.1f, 0.5f));

        var parent = new GameObject("Lanterns").transform;
        parent.SetParent(root, false);
        float half = _arenaHalf + 1.6f;
        const float spacing = 11f;
        for (float t = -half; t <= half + 0.01f; t += spacing)
        {
            AddLantern(parent, post, lamp, new Vector3(t, 0f, half));
            AddLantern(parent, post, lamp, new Vector3(t, 0f, -half));
            if (Mathf.Abs(t) < half - 0.5f)
            {
                AddLantern(parent, post, lamp, new Vector3(half, 0f, t));
                AddLantern(parent, post, lamp, new Vector3(-half, 0f, t));
            }
        }
        MarkStatic(parent.gameObject);
    }

    private static void AddLantern(Transform parent, Material post, Material lamp, Vector3 local)
    {
        var lantern = new GameObject("Lantern").transform;
        lantern.SetParent(parent, false);
        lantern.position = _center + local + Vector3.up * FLAT_HEIGHT;
        AddPart(lantern, SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Cylinder, "Post", lantern, post), new Vector3(0f, 0.9f, 0f), new Vector3(0.14f, 0.9f, 0.14f));
        AddPart(lantern, SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Cube, "Cap", lantern, post), new Vector3(0f, 2.05f, 0f), new Vector3(0.45f, 0.08f, 0.45f));
        GameObject glow = SnakeSceneBuilder.CreatePrimitive(PrimitiveType.Sphere, "Lamp", lantern, lamp);
        AddPart(lantern, glow, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.38f);
        glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void BuildClouds(Transform root, System.Random rng)
    {
        Mesh cloudMesh = SaveMesh(ProceduralMeshes.Blob("CloudPuff", 2, 0.25f, 31), "CloudPuff");
        Material cloud = SnakeSceneBuilder.CreateLitMaterial("Cloud", new Color(0.97f, 0.97f, 1f), 0f);
        SnakeSceneBuilder.SetEmission(cloud, new Color(0.45f, 0.46f, 0.5f));

        var layer = new GameObject("Clouds");
        layer.transform.SetParent(root, false);
        layer.transform.position = _center;
        layer.AddComponent<CloudDrift>();

        for (int c = 0; c < 18; c++)
        {
            float angle = Lerp(rng, 0f, Mathf.PI * 2f);
            float dist = Lerp(rng, 40f, 170f);
            var pos = _center + new Vector3(Mathf.Cos(angle) * dist, Lerp(rng, 55f, 80f), Mathf.Sin(angle) * dist);
            int puffs = 3 + rng.Next(4);
            for (int p = 0; p < puffs; p++)
            {
                var offset = new Vector3(Lerp(rng, -10f, 10f), Lerp(rng, -1.5f, 2f), Lerp(rng, -5f, 5f));
                var scale = new Vector3(Lerp(rng, 9f, 18f), Lerp(rng, 4f, 7f), Lerp(rng, 7f, 12f));
                GameObject puff = AddMesh(layer.transform, "Cloud", cloudMesh, cloud, pos + offset, scale, Lerp(rng, 0f, 360f));
                puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    /// <summary>Пыльца и светлячки, медленно плавающие над ареной.</summary>
    private static void BuildPollen(Transform root, Material particleMaterial)
    {
        var go = new GameObject("Pollen");
        go.transform.SetParent(root, false);
        go.transform.position = _center + Vector3.up * 1.6f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 10f;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 0.8f), new Color(0.85f, 1f, 0.6f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 25f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_arenaHalf * 2.6f, 2.5f, _arenaHalf * 2.6f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.25f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ===================== Утилиты =====================

    /// <summary>
    /// Случайная точка на рельефе вне арены: на расстоянии [minDist, maxDist] от её края, не в озере,
    /// не на крутом склоне. density/threshold — «полянки»: точка принимается, если шум выше порога.
    /// </summary>
    private static bool TryScatter(System.Random rng, float minDist, float maxDist, float threshold, float noiseScale, out Vector3 pos)
    {
        float angle = Lerp(rng, 0f, Mathf.PI * 2f);
        // Квадрат расстояния делает распределение равномерным по площади, а не густым у арены.
        float d = Mathf.Sqrt(Lerp(rng, 0f, 1f)) * (maxDist - minDist) + minDist;
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        // Луч от центра до края арены (квадрат) плюс d.
        float toEdge = _arenaHalf / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
        float r = toEdge + 1.5f + d;
        float x = _center.x + dir.x * r;
        float z = _center.z + dir.y * r;

        pos = default;
        if (Mathf.Abs(x - _center.x) > TERRAIN_SIZE * 0.48f || Mathf.Abs(z - _center.z) > TERRAIN_SIZE * 0.48f) return false;
        if (new Vector2(x - _lakeCenter.x, z - _lakeCenter.z).magnitude < _lakeRadius * 1.3f) return false;
        if (threshold > 0f && ProceduralTextures.Fbm(x * 0.04f * noiseScale + 50f, z * 0.04f * noiseScale + 50f, 3, 1.9f) < threshold) return false;

        float h = TerrainHeight(x, z);
        float slope = Mathf.Abs(TerrainHeight(x + 1f, z) - h) + Mathf.Abs(TerrainHeight(x, z + 1f) - h);
        if (slope > 1.1f) return false;

        pos = new Vector3(x, h, z);
        return true;
    }

    /// <summary>Объект с мешем; local = true — позиция в координатах родителя (части дерева), иначе мировая.</summary>
    private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 position, Vector3 scale, float yaw, bool local = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (local)
        {
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else
        {
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        MarkStatic(go);
        return go;
    }

    private static void AddPart(Transform parent, GameObject part, Vector3 localPosition, Vector3 localScale)
    {
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        MarkStatic(part);
    }

    private static void CreateCombined(Transform root, string name, Mesh mesh, Material mat, bool castShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        MarkStatic(go);
    }

    private static Material FoliageMaterial(string name, Color dark, Color bright)
    {
        Material mat = SnakeSceneBuilder.CreateLitMaterial(name, Color.white, 0.12f);
        mat.mainTexture = Save(ProceduralTextures.Foliage(128, dark, bright, name));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material Textured(string name, Color tint, float smoothness, Texture2D albedo, Texture2D normal, float bump)
    {
        Material mat = SnakeSceneBuilder.CreateLitMaterial(name, tint, smoothness);
        mat.mainTexture = albedo;
        SetNormalMap(mat, normal, bump);
        return mat;
    }

    private static void SetNormalMap(Material mat, Texture2D normal, float strength)
    {
        if (!mat.HasProperty("_BumpMap")) return;
        mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", strength);
        mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Режим «вырез по альфе» для травы (Standard Cutout или URP Alpha Clipping).</summary>
    private static void MakeCutout(Material mat)
    {
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
    }

    private static void MarkStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    private static float Lerp(System.Random rng, float a, float b)
    {
        return a + (float)rng.NextDouble() * (b - a);
    }

    private static Texture2D Save(Texture2D tex)
    {
        return SnakeSceneBuilder.SaveTexture(tex, TEXTURE_FOLDER + "/" + tex.name + ".asset");
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = MESH_FOLDER + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(TEXTURE_FOLDER)) AssetDatabase.CreateFolder("Assets/Materials", "Environment");
        if (!AssetDatabase.IsValidFolder(MESH_FOLDER)) AssetDatabase.CreateFolder("Assets", "Meshes");
    }
}
