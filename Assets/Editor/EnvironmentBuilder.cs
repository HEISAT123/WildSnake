using System.Collections.Generic;
using WildSnake;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Окружение вокруг арены: процедурное небо, звёзды, рельеф с холмами, горами и озером, лес, камни, кусты,
/// трава, цветы, фонари, облака и пыльца (ночью — светлячки). Плюс материалы и меши самой арены:
/// брёвна, деревья, валуны и заросли высокой травы.
/// Всё строится кодом из примитивов и сгенерированных мешей/текстур, вызывается SnakeSceneBuilder.
/// </summary>
public static class EnvironmentBuilder
{
    private const string TEXTURE_FOLDER = "Assets/Materials/Environment";
    private const string MESH_FOLDER = "Assets/Meshes";

    private const float TERRAIN_SIZE = 360f;
    // Шаг вершин — ровно клетка: внутри поля меш совпадает с картой высот логики.
    private const int TERRAIN_RESOLUTION = 361;

    /// <summary>Материалы и меши арены для BoardView.</summary>
    public sealed class ArenaMaterials
    {
        public Material Floor;
        public Material Wall;
        public Material LowWall;
        public Material Bark;
        public Material StumpTop;
        public Material Block;
        public Material TallGrass;
        public Material PondWater;
        public Material PondBed;
        public Material SunStone;
        public Material Dirt;
        public Material Hole;
        public Material Leaves;
        public Material LeavesFade;
        public Material BarkFade;
        public Material StumpTopFade;
        public Material BlockFade;
        public Material Pine;
        public Material PineFade;
        public Material Litter;
        public Material Egg;
        public Material Beacon;
        public Material RiverWater;
        public Material SwampWater;
        public Material BirchBark;
        public Material BirchBarkFade;
        public Material BirchLeaves;
        public Material BirchLeavesFade;
        public Material DeadBark;
        public Material DeadBarkFade;
        public Material Reed;
        public Material Cattail;
        public Material Fern;
        public Material MushroomCap;
        public Material MushroomStem;
        public Material LilyPad;
        public Material BirdEgg;
        public Mesh CrownMesh;
        public Mesh PineMesh;
        public Mesh RockMesh;
        public Mesh TuftMesh;
    }

    private static Vector3 _center;
    private static GridManager _grid;

    // ===================== Материалы арены =====================

    public static ArenaMaterials CreateArenaMaterials()
    {
        EnsureFolders();

        ProceduralTextures.GrassFloor(256, out Texture2D grass, out Texture2D grassN);
        ProceduralTextures.StoneBricks(256, out Texture2D bricks, out Texture2D bricksN);
        ProceduralTextures.Rock(256, out Texture2D rock, out Texture2D rockN);
        var m = new ArenaMaterials
        {
            Floor = Textured("Floor", Color.white, 0.12f, Save(grass), Save(grassN), 0.8f),
            Wall = Textured("Wall", Color.white, 0.2f, Save(bricks), Save(bricksN), 1.2f),
            LowWall = Textured("ObstacleLowWall", new Color(1f, 0.95f, 0.88f), 0.15f, Save(rock), Save(rockN), 1.2f),
            Block = Textured("ObstacleBlock", new Color(0.78f, 0.78f, 0.74f), 0.15f, rock, rockN, 1.2f),
            Bark = SnakeSceneBuilder.CreateLitMaterial("Bark", Color.white, 0.05f),
            StumpTop = SnakeSceneBuilder.CreateLitMaterial("StumpTop", new Color(0.78f, 0.62f, 0.42f), 0.1f),
            TallGrass = SnakeSceneBuilder.CreateLitMaterial("TallGrass", new Color(0.6f, 0.78f, 0.4f), 0.05f),
            // Свои имена мешей и текстур: окружение ниже пересохраняет Boulder*, GrassBlades и т. п.,
            // а пересохранение удаляет старый ассет — ссылки поля потерялись бы.
            RockMesh = SaveMesh(ProceduralMeshes.Blob("ArenaRock", 2, 0.45f, 211), "ArenaRock"),
            TuftMesh = SaveMesh(ProceduralMeshes.GrassTuft("TallGrassTuft"), "TallGrassTuft")
        };
        m.Bark.mainTexture = Save(ProceduralTextures.Bark(128));
        Texture2D blades = ProceduralTextures.GrassBlades(128);
        blades.name = "TallGrassBlades";
        m.TallGrass.mainTexture = Save(blades);
        MakeCutout(m.TallGrass);
        // Стены — длинные кубы: повторяем кладку вдоль, иначе кирпичи растянутся на всю длину.
        m.Wall.mainTextureScale = new Vector2(24f, 0.6f);

        // Пруд на арене: полупрозрачная рябь над тёмным илистым дном.
        m.PondWater = SnakeSceneBuilder.CreateLitMaterial("PondWater", new Color(0.18f, 0.42f, 0.52f, 0.72f), 0.95f);
        // Своё имя текстуры: окружение пересохраняет Water_N, и ссылка пруда потерялась бы.
        Texture2D pondNormal = ProceduralTextures.WaterNormal(256);
        pondNormal.name = "PondWater_N";
        SetNormalMap(m.PondWater, Save(pondNormal), 0.5f);
        m.PondWater.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
        // В URP все карты Lit берут тайлинг основной текстуры (_BaseMap).
        m.PondWater.mainTextureScale = new Vector2(2f, 2f);
        SnakeSceneBuilder.MakeTransparent(m.PondWater);
        m.PondBed = SnakeSceneBuilder.CreateLitMaterial("PondBed", new Color(0.24f, 0.27f, 0.17f), 0.35f);
        m.SunStone = Textured("SunStone", new Color(0.95f, 0.86f, 0.7f), 0.25f, rock, rockN, 0.9f);
        m.Dirt = SnakeSceneBuilder.CreateLitMaterial("BurrowDirt", new Color(0.36f, 0.25f, 0.16f), 0.05f);
        m.Hole = SnakeSceneBuilder.CreateLitMaterial("BurrowHole", new Color(0.03f, 0.02f, 0.015f), 0f);

        // Роща на арене: свои имена текстур и мешей (окружение пересохраняет LeafFoliage, CrownA и т. п.).
        m.Leaves = FoliageMaterial("ArenaLeaves", new Color(0.14f, 0.34f, 0.1f), new Color(0.4f, 0.62f, 0.2f));
        m.Pine = FoliageMaterial("ArenaPine", new Color(0.06f, 0.2f, 0.1f), new Color(0.18f, 0.38f, 0.18f));
        // Полупрозрачные копии крон: на них CanopyFader меняет крону между камерой и змеёй.
        m.LeavesFade = FadeCopy("ArenaLeavesFade", m.Leaves);
        m.PineFade = FadeCopy("ArenaPineFade", m.Pine);
        m.BarkFade = FadeCopy("BarkFade", m.Bark);
        m.StumpTopFade = FadeCopy("StumpTopFade", m.StumpTop);
        m.BlockFade = FadeCopy("ObstacleBlockFade", m.Block);
        m.Litter = SnakeSceneBuilder.CreateLitMaterial("LeafLitter", Color.white, 0.05f);
        m.Litter.mainTexture = Save(ProceduralTextures.LeafLitter(256));
        SnakeSceneBuilder.MakeTransparent(m.Litter);
        m.Egg = SnakeSceneBuilder.CreateLitMaterial("SnakeEgg", new Color(0.95f, 0.92f, 0.8f), 0.35f);
        m.Beacon = SnakeSceneBuilder.CreateLitMaterial("NestBeacon", new Color(0.8f, 1f, 0.3f, 0.22f), 0f);
        SnakeSceneBuilder.SetEmission(m.Beacon, new Color(0.55f, 0.85f, 0.12f));
        SnakeSceneBuilder.MakeTransparent(m.Beacon);
        // Река — светлее и прозрачнее пруда; рябь бежит вдоль русла (UV по течению, сдвиг — WaterFlow).
        m.RiverWater = SnakeSceneBuilder.CreateLitMaterial("RiverWater", new Color(0.24f, 0.5f, 0.58f, 0.68f), 0.95f);
        Texture2D riverNormal = ProceduralTextures.WaterNormal(256);
        riverNormal.name = "RiverWater_N";
        SetNormalMap(m.RiverWater, Save(riverNormal), 0.7f);
        SnakeSceneBuilder.MakeTransparent(m.RiverWater);
        // Болотная вода — мутная, зеленовато-бурая, почти непрозрачная.
        m.SwampWater = SnakeSceneBuilder.CreateLitMaterial("SwampWater", new Color(0.19f, 0.25f, 0.13f, 0.86f), 0.85f);
        SetNormalMap(m.SwampWater, m.RiverWater.GetTexture("_BumpMap") as Texture2D, 0.25f);
        SnakeSceneBuilder.MakeTransparent(m.SwampWater);

        m.BirchBark = SnakeSceneBuilder.CreateLitMaterial("BirchBark", Color.white, 0.1f);
        m.BirchBark.mainTexture = Save(ProceduralTextures.BirchBark(128));
        m.BirchLeaves = FoliageMaterial("ArenaBirchLeaves", new Color(0.3f, 0.5f, 0.15f), new Color(0.68f, 0.82f, 0.32f));
        m.DeadBark = SnakeSceneBuilder.CreateLitMaterial("DeadBark", new Color(0.82f, 0.8f, 0.76f), 0.05f);
        m.DeadBark.mainTexture = m.Bark.mainTexture;
        m.BirchBarkFade = FadeCopy("BirchBarkFade", m.BirchBark);
        m.BirchLeavesFade = FadeCopy("ArenaBirchLeavesFade", m.BirchLeaves);
        m.DeadBarkFade = FadeCopy("DeadBarkFade", m.DeadBark);

        // Камыш и папоротник — те же вырезанные по альфе стебли, что у высокой травы, другого цвета.
        m.Reed = SnakeSceneBuilder.CreateLitMaterial("Reed", new Color(0.66f, 0.7f, 0.38f), 0.05f);
        m.Reed.mainTexture = m.TallGrass.mainTexture;
        MakeCutout(m.Reed);
        m.Fern = SnakeSceneBuilder.CreateLitMaterial("Fern", new Color(0.3f, 0.56f, 0.2f), 0.05f);
        m.Fern.mainTexture = m.TallGrass.mainTexture;
        MakeCutout(m.Fern);
        m.Cattail = SnakeSceneBuilder.CreateLitMaterial("Cattail", new Color(0.33f, 0.2f, 0.11f), 0.1f);
        m.MushroomCap = SnakeSceneBuilder.CreateLitMaterial("MushroomCap", new Color(0.78f, 0.2f, 0.12f), 0.45f);
        m.MushroomStem = SnakeSceneBuilder.CreateLitMaterial("MushroomStem", new Color(0.93f, 0.9f, 0.82f), 0.2f);
        m.LilyPad = SnakeSceneBuilder.CreateLitMaterial("LilyPad", new Color(0.22f, 0.46f, 0.18f), 0.55f);
        m.BirdEgg = SnakeSceneBuilder.CreateLitMaterial("BirdEgg", new Color(0.72f, 0.86f, 0.9f), 0.4f);

        m.CrownMesh = SaveMesh(ProceduralMeshes.Blob("ArenaCrown", 2, 0.42f, 57), "ArenaCrown");
        m.PineMesh = SaveMesh(ProceduralMeshes.Cone("ArenaPineTier", 9), "ArenaPineTier");
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
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 330f;

        sun.color = new Color(1f, 0.93f, 0.8f);
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.farClipPlane = 700f;
    }

    // ===================== Окружение =====================

    /// <summary>Строит окружение; pollen — пыльца (ночью DayNightCycle делает её светлячками), stars — звёздное небо.</summary>
    public static void Build(GridManager grid, Material particleMaterial, out ParticleSystem pollen, out Renderer stars, out Transform clouds)
    {
        EnsureFolders();
        _center = grid.Center;
        _grid = grid;

        var root = new GameObject("Environment").transform;
        var rng = new System.Random(4242);

        BuildTerrain(root);
        BuildForest(root, rng);
        BuildRocks(root, rng);
        BuildBushes(root, rng);
        BuildGrassAndFlowers(root, rng);
        clouds = BuildClouds(root, rng);
        pollen = BuildPollen(root, particleMaterial);
        stars = BuildStars(root, rng);
    }

    /// <summary>
    /// Звёзды — сотни маленьких квадов на далёкой полусфере в одном меше. Материал без тумана и освещения;
    /// прозрачность меняет DayNightCycle (днём звёзд нет).
    /// </summary>
    private static Renderer BuildStars(Transform root, System.Random rng)
    {
        const int count = 900;
        const float radius = 420f;
        var verts = new List<Vector3>(count * 4);
        var uvs = new List<Vector2>(count * 4);
        var colors = new List<Color>(count * 4);
        var tris = new List<int>(count * 6);
        for (int i = 0; i < count; i++)
        {
            // Равномерно по верхней полусфере, но не у самого горизонта — там их скрыл бы туман.
            float y = Lerp(rng, 0.12f, 1f);
            float a = Lerp(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(1f - y * y);
            var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            Vector3 c = dir * radius;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(dir, right);
            float size = Lerp(rng, 0.9f, 2.6f) * (rng.NextDouble() < 0.06 ? 1.8f : 1f);
            float warmth = Lerp(rng, -1f, 1f);
            var tint = new Color(1f - Mathf.Max(0f, -warmth) * 0.25f, 0.95f, 1f - Mathf.Max(0f, warmth) * 0.3f, Lerp(rng, 0.45f, 1f));

            int b = verts.Count;
            verts.Add(c + (-right - up) * size);
            verts.Add(c + (right - up) * size);
            verts.Add(c + (right + up) * size);
            verts.Add(c + (-right + up) * size);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            for (int k = 0; k < 4; k++) colors.Add(tint);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
        }

        var mesh = new Mesh { name = "Stars" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        SaveMesh(mesh, "Stars");

        Material mat = SnakeSceneBuilder.LoadOrCreateMaterial("Stars", Shader.Find("Sprites/Default"));
        var dot = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (dot != null) mat.mainTexture = dot;
        mat.color = new Color(1f, 1f, 1f, 0f);
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Stars");
        go.transform.SetParent(root, false);
        go.transform.position = _center;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>Высота рельефа: внутри поля — карта высот GridManager (холмы, чаши водоёмов), дальше — горы TerrainShape.</summary>
    private static float TerrainHeight(float x, float z)
    {
        return _grid.GroundHeight(new Vector3(x, 0f, z));
    }

    /// <summary>Окраска земли по местности: болото — бурая трясина, гряда — камень и щебень, у воды — ил и галька.</summary>
    private static Color TerrainTint(float x, float z, Color c)
    {
        var p = new Vector3(x, 0f, z);
        float swamp = _grid.SwampWeight(p);
        float ridge = _grid.RidgeWeight(p);
        float shore = _grid.ShoreDistance(p);
        var bog = new Color(0.25f, 0.29f, 0.13f);
        var stone = new Color(0.46f, 0.44f, 0.4f);
        var gravel = new Color(0.52f, 0.47f, 0.38f);
        var mud = new Color(0.31f, 0.26f, 0.17f);
        c = Color.Lerp(c, bog, swamp * 0.75f);
        float n = ProceduralTextures.Fbm(x * 0.15f + 3f, z * 0.15f + 9f, 3, 2.1f);
        c = Color.Lerp(c, Color.Lerp(stone, gravel, n), ridge * Mathf.Clamp01(0.45f + n * 0.7f));
        if (shore < 2.5f) c = Color.Lerp(c, shore < 0.6f ? gravel : mud, (1f - shore / 2.5f) * 0.7f);
        return c;
    }

    /// <summary>Один рельеф на весь мир: по нему ходит змея, и он же поднимается в горы-границу.</summary>
    private static void BuildTerrain(Transform root)
    {
        Mesh mesh = SaveMesh(ProceduralMeshes.Terrain(TERRAIN_RESOLUTION, TERRAIN_SIZE, _center, TerrainHeight), "Terrain");
        Texture2D color = Save(ProceduralTextures.Terrain(2048, TerrainHeight, TERRAIN_SIZE, _center, TerrainTint));
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
        // Холмы отбрасывают тень друг на друга — рельеф читается лучше.
        r.shadowCastingMode = ShadowCastingMode.On;
        MarkStatic(go);
    }

    private static void BuildForest(Transform root, System.Random rng)
    {
        Mesh cone = SaveMesh(ProceduralMeshes.Cone("PineTier", 9), "PineTier");
        Mesh crownA = SaveMesh(ProceduralMeshes.Blob("CrownA", 2, 0.45f, 11), "CrownA");
        Mesh crownB = SaveMesh(ProceduralMeshes.Blob("CrownB", 2, 0.5f, 23), "CrownB");

        // Кора уже создана для поля (CreateArenaMaterials): пересохранение текстуры оборвало бы ссылку прозрачной копии.
        Material bark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bark.mat");
        Material pine = FoliageMaterial("PineFoliage", new Color(0.08f, 0.24f, 0.12f), new Color(0.2f, 0.42f, 0.2f));
        Material leaves = FoliageMaterial("LeafFoliage", new Color(0.18f, 0.4f, 0.12f), new Color(0.45f, 0.68f, 0.22f));
        Material autumn = FoliageMaterial("AutumnFoliage", new Color(0.55f, 0.22f, 0.06f), new Color(0.95f, 0.62f, 0.15f));

        var forest = new GameObject("Forest").transform;
        forest.SetParent(root, false);

        int placed = 0;
        for (int attempt = 0; attempt < 6000 && placed < 320; attempt++)
        {
            if (!TryScatter(rng, false, 0.4f, 0.12f, 30f, out Vector3 pos)) continue;

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
        for (int attempt = 0; attempt < 4000 && placed < 140; attempt++)
        {
            if (!TryScatter(rng, false, 0f, 1f, 60f, out Vector3 pos)) continue;
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
        for (int attempt = 0; attempt < 5000 && placed < 160; attempt++)
        {
            if (!TryScatter(rng, false, 0.3f, 0.6f, 26f, out Vector3 pos)) continue;
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
        // Низкая трава — по равнине мира: только украшение, змее не мешает и не прячет.
        for (int attempt = 0; attempt < 30000 && grassTransforms.Count < 9000; attempt++)
        {
            if (!TryScatter(rng, true, 0f, 0.7f, 100f, out Vector3 pos)) continue;
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
            for (int attempt = 0; attempt < 6000 && flowers.Count < 480; attempt++)
            {
                if (!TryScatter(rng, true, 0.35f, 0.5f, 100f, out Vector3 pos)) continue;
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

    private static Transform BuildClouds(Transform root, System.Random rng)
    {
        Mesh cloudMesh = SaveMesh(ProceduralMeshes.Blob("CloudPuff", 2, 0.25f, 31), "CloudPuff");
        Material cloud = SnakeSceneBuilder.CreateLitMaterial("Cloud", new Color(0.97f, 0.97f, 1f), 0f);
        // Подсветка снизу, чтобы облака были белыми, а не серыми; ночью её гасит DayNightCycle.
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
        return layer.transform;
    }

    /// <summary>Пыльца и светлячки, медленно плавающие над ареной.</summary>
    private static ParticleSystem BuildPollen(Transform root, Material particleMaterial)
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
        // Пыльца летает вокруг змеи (SnakeSceneBuilder вешает FollowTarget), а не по всему огромному миру.
        shape.scale = new Vector3(44f, 2.5f, 44f);

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
        return ps;
    }

    // ===================== Утилиты =====================

    /// <summary>
    /// Случайная точка для украшений. plains — равнина мира (свободная клетка: не препятствие, не вода,
    /// не высокая трава), иначе — склоны гор за границей мира. threshold > 0 — растёт пятнами по шуму.
    /// </summary>
    private static bool TryScatter(System.Random rng, bool plains, float threshold, float noiseScale, float maxHeight, out Vector3 pos)
    {
        pos = default;
        float x, z;
        if (plains)
        {
            float angle = Lerp(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Lerp(rng, 0f, 1f)) * (TerrainShape.PlayRadius - 2f);
            x = _center.x + Mathf.Cos(angle) * r;
            z = _center.z + Mathf.Sin(angle) * r;
            Vector2Int cell = _grid.WorldToCell(new Vector3(x, 0f, z));
            if (!_grid.IsInside(cell) || _grid.GetObstacle(cell) != ObstacleType.None) return false;
            if (_grid.IsWater(cell) || _grid.IsCover(cell) || _grid.IsBurrowCell(cell) || _grid.IsColonyHole(cell)) return false;
            // Лужица и корка тёплого ключа — без травы.
            for (int i = 0; i < _grid.HotSpringCount; i++)
            {
                Vector3 d = _grid.GetHotSpring(i) - new Vector3(x, 0f, z);
                d.y = 0f;
                if (d.sqrMagnitude < 2.2f * 2.2f) return false;
            }
        }
        else
        {
            x = _center.x + Lerp(rng, -TERRAIN_SIZE * 0.48f, TERRAIN_SIZE * 0.48f);
            z = _center.z + Lerp(rng, -TERRAIN_SIZE * 0.48f, TERRAIN_SIZE * 0.48f);
            if (TerrainShape.MountainFactor(x, z, _center) < 0.3f) return false;
        }
        if (threshold > 0f && ProceduralTextures.Fbm(x * 0.04f * noiseScale + 50f, z * 0.04f * noiseScale + 50f, 3, 1.9f) < threshold) return false;

        float h = TerrainHeight(x, z);
        if (h - _center.y > maxHeight) return false;
        float slope = Mathf.Abs(TerrainHeight(x + 1f, z) - h) + Mathf.Abs(TerrainHeight(x, z + 1f) - h);
        if (slope > (plains ? 0.8f : 1.6f)) return false;

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

    private static Material FadeCopy(string name, Material source)
    {
        // Цвет — как у оригинала, чтобы полупрозрачная копия не меняла оттенок (серая кора, светлый спил).
        Color tint = source.color;
        tint.a = 0.22f;
        Material mat = SnakeSceneBuilder.CreateLitMaterial(name, tint, 0.12f);
        mat.mainTexture = source.mainTexture;
        SnakeSceneBuilder.MakeTransparent(mat);
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
