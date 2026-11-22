using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Generates the whole Farm Sniper level: lighting + sky, terrain, crop fields (corn, wheat, cabbage, pumpkins,
    /// vegetable garden), farmhouse, barn, silo, watchtower, chicken coop, well, windmill, fences, trees, hay bales,
    /// tractor and other props, rat burrows and cover, the farmer (player rig with rifle and VR hands) and the game
    /// systems. Called by the editor builder (Neon Breach > Farm Sniper > Build Farm Scene), which saves the result.
    /// </summary>
    public static class FarmWorldBuilder
    {
        public const int EnvLayer = 10;     // Layers.Environment
        public const int PlayerLayer = 8;   // Layers.Player

        [System.Serializable]
        public class Settings
        {
            public int groundSegments = 160;
            [Range(0.2f, 2f)] public float cropDensity = 1f;
            public int grassTufts = 3200;
            public int perimeterTrees = 85;
            public int seed = 20251120;
        }

        public class Result
        {
            public GameObject root;
            public Light sun;
            public Material skybox;
            public FarmGameManager game;
            public RatSpawner spawner;
            public FarmPlayer player;
        }

        static System.Random rng;
        static readonly List<HideSpot> hides = new List<HideSpot>();
        static readonly List<Transform> perches = new List<Transform>();
        static readonly List<AmmoCrate> crates = new List<AmmoCrate>();

        static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static Material M(string key) { return FarmMats.Get(key); }
        static float H(float x, float z) { return FarmLayout.Height(x, z); }
        static Vector3 G(float x, float z, float y = 0f) { return FarmLayout.OnGround(x, z, y); }

        public static Result Build(Settings s)
        {
            if (s == null) s = new Settings();
            rng = new System.Random(s.seed);
            hides.Clear(); perches.Clear(); crates.Clear();
            FarmMats.ClearCache();
            FarmMeshes.ClearCache();
            FarmBuild.ClearCache();

            Result res = new Result();
            res.root = new GameObject("Farm World");
            Transform root = res.root.transform;

            BuildEnvironment(root, res);
            Transform env = FarmBuild.Node(root, "Environment", Vector3.zero);
            BuildGround(env, s);
            List<CropField> fields = BuildFields(env, s);
            BuildFarmhouse(env);
            BuildBarn(env);
            BuildSilo(env);
            BuildTower(env);
            BuildSmallBuildings(env);
            BuildProps(env);
            BuildFences(env);
            BuildTrees(env, s);
            BuildGrass(env, s);
            BuildPond(env);
            FarmBuild.SetLayer(env.gameObject, EnvLayer);
            // pond water stays on Default so it is not walkable for the rats' NavMesh but still stops bullets
            Transform water = env.Find("Pond/Water");
            if (water != null) water.gameObject.layer = 0;

            List<RatSpawnPoint> burrows = BuildBurrows(root);
            FarmPlayer player = BuildPlayer(root);
            BuildSystems(root, res, player, fields, burrows);
            return res;
        }

        // ------------------------------------------------------------------ lighting

        static void BuildEnvironment(Transform root, Result res)
        {
            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(43f, 32f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            res.sun = sun;

            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                Material m = new Material(sky);
                m.name = "Farm_Skybox";
                m.SetFloat("_SunDisk", 2f);
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_SunSizeConvergence", 6f);
                m.SetFloat("_AtmosphereThickness", 0.85f);
                m.SetColor("_SkyTint", new Color(0.48f, 0.55f, 0.66f));
                m.SetColor("_GroundColor", new Color(0.42f, 0.40f, 0.36f));
                m.SetFloat("_Exposure", 1.2f);
                res.skybox = m;
            }
            ApplyRenderSettings(res.skybox, sun);
        }

        public static void ApplyRenderSettings(Material skybox, Light sun)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.68f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.53f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.20f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0034f;
            RenderSettings.fogColor = new Color(0.72f, 0.77f, 0.82f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.55f;
        }

        // ------------------------------------------------------------------ ground

        static void BuildGround(Transform env, Settings s)
        {
            Mesh mesh = FarmMeshes.Ground(s.groundSegments, FarmLayout.GroundSize);
            GameObject g = FarmBuild.MeshObj(env, "Ground", mesh, FarmMats.Ground, Vector3.zero, Vector3.zero, Vector3.one, ShadowCastingMode.Off);
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
            FarmBuild.Surface(g, SurfaceKind.Dirt);
            g.isStatic = true;
        }

        // ------------------------------------------------------------------ crops

        static List<CropField> BuildFields(Transform env, Settings s)
        {
            List<CropField> list = new List<CropField>();
            Transform parent = FarmBuild.Node(env, "Fields", Vector3.zero);
            for (int i = 0; i < FarmLayout.Fields.Length; i++)
            {
                FarmLayout.Field f = FarmLayout.Fields[i];
                Vector2 c = f.rect.center;
                GameObject go = new GameObject(f.name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(c.x, H(c.x, c.y), c.y);
                CropField cf = go.AddComponent<CropField>();
                cf.cropName = f.name;
                cf.size = f.rect.size;
                int seed = s.seed + i * 7919;
                float d = s.cropDensity;
                switch (f.kind)
                {
                    case FarmLayout.CropKind.Corn:
                        cf.tallCrop = true;
                        cf.weight = 1.2f;
                        cf.witheredTint = new Color(0.55f, 0.48f, 0.28f);
                        Planter(go, cf, VegetationKind.Corn, f.rect, seed, d, 1.0f, 0.9f, 0.12f, new Vector2(0.9f, 1.1f), 0.03f, 12f, true, "Corn");
                        break;
                    case FarmLayout.CropKind.Wheat:
                        cf.weight = 1.2f;
                        cf.witheredTint = new Color(0.5f, 0.42f, 0.30f);
                        Planter(go, cf, VegetationKind.Wheat, f.rect, seed, d, 0.75f, 0.85f, 0.3f, new Vector2(0.85f, 1.15f), 0f, 14f, false, "Wheat");
                        break;
                    case FarmLayout.CropKind.Cabbage:
                        Planter(go, cf, VegetationKind.Cabbage, f.rect, seed, d, 1.2f, 0.9f, 0.08f, new Vector2(0.8f, 1.2f), 0.02f, 12f, true, "Cabbage");
                        break;
                    case FarmLayout.CropKind.Pumpkin:
                        Planter(go, cf, VegetationKind.Pumpkin, f.rect, seed, d, 2.2f, 2.0f, 0.6f, new Vector2(0.75f, 1.3f), 0.15f, 17f, true, "Pumpkin");
                        Planter(go, cf, VegetationKind.PumpkinVines, f.rect, seed + 1, d, 1.1f, 1.0f, 0.4f, new Vector2(0.8f, 1.3f), 0.2f, 17f, false, "Vine");
                        break;
                    case FarmLayout.CropKind.Garden:
                        cf.weight = 0.5f;
                        Planter(go, cf, VegetationKind.Lettuce, f.rect, seed, d, 0.9f, 0.6f, 0.05f, new Vector2(0.8f, 1.1f), 0.05f, 16f, true, "Leaves");
                        GardenStakes(go.transform, f.rect);
                        break;
                }
                list.Add(cf);
                if (f.kind == FarmLayout.CropKind.Corn)
                {
                    // the tall corn is cover: rats can vanish anywhere inside it
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 p = G(R(f.rect.xMin + 4f, f.rect.xMax - 4f), R(f.rect.yMin + 4f, f.rect.yMax - 4f));
                        HideSpot h = Hide(go.transform, "CornCover", p, 4f);
                        h.insideCover = true;
                    }
                }
            }
            return list;
        }

        /// <summary>Adds a VegetationPlanter: the crop meshes are grown from the seed whenever the scene loads (not saved).</summary>
        static void Planter(GameObject field, CropField cf, VegetationKind kind, Rect r, int seed, float density, float row, float spacing, float jitter,
                            Vector2 scale, float skip, float chunk, bool shadows, string mat)
        {
            GameObject go = new GameObject(kind + " Planter");
            go.transform.SetParent(field.transform, false);
            go.transform.position = Vector3.zero;
            VegetationPlanter p = go.AddComponent<VegetationPlanter>();
            p.kind = kind;
            p.area = r;
            p.seed = seed;
            p.density = density;
            p.rowSpacing = kind == VegetationKind.Wheat ? row / Mathf.Sqrt(density) : row;
            p.plantSpacing = kind == VegetationKind.Wheat ? spacing * Mathf.Sqrt(density) : spacing;
            p.jitter = jitter;
            p.scaleRange = scale;
            p.skipChance = skip;
            p.chunkSize = chunk;
            p.castShadows = shadows;
            p.field = cf;
            p.material = M(mat);
            p.Regenerate();
        }

        static void GardenStakes(Transform field, Rect r)
        {
            // tomato stakes + a scarecrow-ish sign
            for (float x = r.xMin + 1.5f; x < r.xMax - 1f; x += 1.8f)
                for (float z = r.yMin + 1.2f; z < r.yMin + 3.6f; z += 1.2f)
                    FarmBuild.Block(field, "Stake", field.InverseTransformPoint(G(x, z, 0.55f)), new Vector3(0.04f, 1.1f, 0.04f), Vector3.zero, M("FenceWood"), 1f, false);
        }

        // ------------------------------------------------------------------ farmhouse

        static void BuildFarmhouse(Transform env)
        {
            Vector3 p = FarmLayout.HousePos;
            Transform h = FarmBuild.Node(env, "Farmhouse", new Vector3(p.x, H(p.x, p.z), p.z));
            const float w = 12f, d = 9f, wall = 5.4f, baseH = 0.6f;
            FarmBuild.Block(h, "Foundation", new Vector3(0f, baseH * 0.5f - 0.3f, 0f), new Vector3(w + 0.4f, baseH + 0.6f, d + 0.4f), Vector3.zero, M("Stone"), 1f, true, SurfaceKind.Stone);
            FarmBuild.Block(h, "Walls", new Vector3(0f, baseH + wall * 0.5f, 0f), new Vector3(w, wall, d), Vector3.zero, M("HouseSiding"), 1f, true, SurfaceKind.Wood);
            float roofY = baseH + wall;
            GameObject roof = FarmBuild.MeshObj(h, "Roof", FarmMeshes.GableRoof(d + 1.4f, w + 1.2f, 3.1f, 0.18f), M("RoofShingle"), new Vector3(0f, roofY - 0.05f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            roof.AddComponent<MeshCollider>().sharedMesh = roof.GetComponent<MeshFilter>().sharedMesh;
            FarmBuild.Surface(roof, SurfaceKind.Wood);
            float riseAtWall = 3.1f * d / (d + 1.4f);
            FarmBuild.MeshObj(h, "GableE", FarmMeshes.GableEnd(d, riseAtWall, false), M("HouseSiding"), new Vector3(w * 0.5f, roofY, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            FarmBuild.MeshObj(h, "GableW", FarmMeshes.GableEnd(d, riseAtWall, false), M("HouseSiding"), new Vector3(-w * 0.5f, roofY, 0f), new Vector3(0f, -90f, 0f), Vector3.one);
            FarmBuild.Block(h, "Chimney", new Vector3(3.6f, roofY + 2.2f, -1.5f), new Vector3(0.9f, 4.4f, 0.9f), Vector3.zero, M("Brick"), 1f, true, SurfaceKind.Stone);

            // windows + door (front = +Z)
            float fz = d * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float x = -4.2f + i * 2.8f;
                if (i == 1) continue;   // door goes here
                Window(h, new Vector3(x, baseH + 1.6f, fz), 0f);
                Window(h, new Vector3(x, baseH + 4.1f, fz), 0f);
            }
            Window(h, new Vector3(-1.4f, baseH + 4.1f, fz), 0f);
            for (int i = 0; i < 3; i++)
            {
                Window(h, new Vector3(-3.6f + i * 3.6f, baseH + 1.6f, -fz), 180f);
                Window(h, new Vector3(-3.6f + i * 3.6f, baseH + 4.1f, -fz), 180f);
            }
            Window(h, new Vector3(w * 0.5f, baseH + 1.6f, 1.8f), 90f);
            Window(h, new Vector3(w * 0.5f, baseH + 1.6f, -1.8f), 90f);
            Window(h, new Vector3(-w * 0.5f, baseH + 1.6f, 1.8f), -90f);
            Window(h, new Vector3(-w * 0.5f, baseH + 1.6f, -1.8f), -90f);
            FarmBuild.Block(h, "Door", new Vector3(-1.4f, baseH + 1.1f, fz + 0.04f), new Vector3(1.1f, 2.2f, 0.08f), Vector3.zero, M("DarkWood"), 1f, false);
            FarmBuild.Block(h, "DoorFrame", new Vector3(-1.4f, baseH + 2.28f, fz + 0.06f), new Vector3(1.4f, 0.15f, 0.1f), Vector3.zero, M("WhiteTrim"), 1f, false);
            FarmBuild.Part(h, PrimitiveType.Sphere, "Knob", new Vector3(-1.0f, baseH + 1.05f, fz + 0.1f), Vector3.zero, Vector3.one * 0.07f, M("Metal"));
            // corner trims
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                FarmBuild.Block(h, "CornerTrim", new Vector3(sx * (w * 0.5f + 0.02f), baseH + wall * 0.5f, sz * (d * 0.5f + 0.02f)), new Vector3(0.18f, wall, 0.18f), Vector3.zero, M("WhiteTrim"), 1f, false);
            }

            // porch
            Transform porch = FarmBuild.Node(h, "Porch", new Vector3(0f, 0f, fz + 1.4f));
            FarmBuild.Block(porch, "Deck", new Vector3(0f, 0.42f, 0f), new Vector3(w, 0.22f, 2.8f), Vector3.zero, M("Wood"), 1f, true);
            for (int i = 0; i < 5; i++)
                FarmBuild.Part(porch, PrimitiveType.Cylinder, "Post", new Vector3(-5.6f + i * 2.8f, 1.85f, 1.2f), Vector3.zero, new Vector3(0.16f, 1.35f, 0.16f), M("WhiteTrim"));
            FarmBuild.Block(porch, "PorchRoof", new Vector3(0f, 3.25f, 0.15f), new Vector3(w + 0.4f, 0.12f, 3.2f), new Vector3(-9f, 0f, 0f), M("RoofMetal"), 0.5f, true, SurfaceKind.Metal);
            for (int i = 0; i < 2; i++)
                FarmBuild.Block(porch, "Step", new Vector3(-1.4f, 0.1f + i * 0.14f, 1.9f - i * 0.3f), new Vector3(2f, 0.2f + i * 0.08f, 0.32f), Vector3.zero, M("Wood"), 1f, true);
            // porch rail
            FarmBuild.Block(porch, "Rail", new Vector3(3f, 1.25f, 1.25f), new Vector3(5.6f, 0.08f, 0.08f), Vector3.zero, M("WhiteTrim"), 1f, false);
            FarmBuild.Block(porch, "Rail", new Vector3(-4.2f, 1.25f, 1.25f), new Vector3(3.2f, 0.08f, 0.08f), Vector3.zero, M("WhiteTrim"), 1f, false);
            // rocking chair-ish bench + crates by the porch
            FarmBuild.Block(porch, "Bench", new Vector3(3.5f, 0.85f, -0.6f), new Vector3(1.8f, 0.1f, 0.5f), Vector3.zero, M("DarkWood"), 1f, false);
            FarmBuild.Block(porch, "BenchBack", new Vector3(3.5f, 1.2f, -0.85f), new Vector3(1.8f, 0.6f, 0.06f), Vector3.zero, M("DarkWood"), 1f, false);
        }

        static void Window(Transform parent, Vector3 pos, float yaw)
        {
            Transform w = FarmBuild.Node(parent, "Window", pos, new Vector3(0f, yaw, 0f));
            FarmBuild.Block(w, "Glass", new Vector3(0f, 0f, 0.02f), new Vector3(1.0f, 1.25f, 0.04f), Vector3.zero, M("Glass"), 1f, false);
            Material t = M("WhiteTrim");
            FarmBuild.Block(w, "Top", new Vector3(0f, 0.68f, 0.05f), new Vector3(1.25f, 0.12f, 0.08f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "Sill", new Vector3(0f, -0.68f, 0.07f), new Vector3(1.3f, 0.1f, 0.14f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "L", new Vector3(-0.56f, 0f, 0.05f), new Vector3(0.1f, 1.3f, 0.08f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "R", new Vector3(0.56f, 0f, 0.05f), new Vector3(0.1f, 1.3f, 0.08f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "MullionV", new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 1.25f, 0.05f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "MullionH", new Vector3(0f, 0f, 0.05f), new Vector3(1.0f, 0.05f, 0.05f), Vector3.zero, t, 1f, false);
            FarmBuild.Block(w, "ShutterL", new Vector3(-0.85f, 0f, 0.04f), new Vector3(0.45f, 1.3f, 0.05f), Vector3.zero, M("BluePaint"), 1f, false);
            FarmBuild.Block(w, "ShutterR", new Vector3(0.85f, 0f, 0.04f), new Vector3(0.45f, 1.3f, 0.05f), Vector3.zero, M("BluePaint"), 1f, false);
        }

        // ------------------------------------------------------------------ barn + silo

        static void BuildBarn(Transform env)
        {
            Vector3 p = FarmLayout.BarnPos;
            Transform b = FarmBuild.Node(env, "Barn", new Vector3(p.x, H(p.x, p.z), p.z));
            const float w = 14f, len = 22f, wall = 6f, rise = 5f;
            FarmBuild.Block(b, "Foundation", new Vector3(0f, -0.1f, 0f), new Vector3(w + 0.4f, 0.8f, len + 0.4f), Vector3.zero, M("Stone"), 1f, true, SurfaceKind.Stone);
            FarmBuild.Block(b, "Walls", new Vector3(0f, 0.3f + wall * 0.5f, 0f), new Vector3(w, wall, len), Vector3.zero, M("BarnRed"), 0.5f, true, SurfaceKind.Wood);
            float roofY = 0.3f + wall;
            GameObject roof = FarmBuild.MeshObj(b, "Roof", FarmMeshes.GambrelRoof(w, len + 1.2f, rise, 0.5f, 0.18f), M("RoofMetal"), new Vector3(0f, roofY, 0f), Vector3.zero, Vector3.one);
            roof.AddComponent<MeshCollider>().sharedMesh = roof.GetComponent<MeshFilter>().sharedMesh;
            FarmBuild.Surface(roof, SurfaceKind.Metal);
            Mesh gable = FarmMeshes.GableEnd(w, rise, true);
            FarmBuild.MeshObj(b, "GableFront", gable, M("BarnRed"), new Vector3(0f, roofY, len * 0.5f), Vector3.zero, Vector3.one);
            FarmBuild.MeshObj(b, "GableBack", gable, M("BarnRed"), new Vector3(0f, roofY, -len * 0.5f), new Vector3(0f, 180f, 0f), Vector3.one);

            // big sliding doors with white X bracing (front = +Z, facing the fields)
            float fz = len * 0.5f + 0.06f;
            Material trim = M("WhiteTrim");
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.75f : 1.75f;
                Transform door = FarmBuild.Node(b, "Door" + i, new Vector3(x, 0.3f + 2.3f, fz));
                FarmBuild.Block(door, "Panel", Vector3.zero, new Vector3(3.4f, 4.6f, 0.1f), Vector3.zero, M("BarnRed"), 0.5f, false);
                FarmBuild.Block(door, "FrameT", new Vector3(0f, 2.25f, 0.06f), new Vector3(3.4f, 0.16f, 0.06f), Vector3.zero, trim, 1f, false);
                FarmBuild.Block(door, "FrameB", new Vector3(0f, -2.25f, 0.06f), new Vector3(3.4f, 0.16f, 0.06f), Vector3.zero, trim, 1f, false);
                FarmBuild.Block(door, "FrameL", new Vector3(-1.62f, 0f, 0.06f), new Vector3(0.16f, 4.6f, 0.06f), Vector3.zero, trim, 1f, false);
                FarmBuild.Block(door, "FrameR", new Vector3(1.62f, 0f, 0.06f), new Vector3(0.16f, 4.6f, 0.06f), Vector3.zero, trim, 1f, false);
                float diag = Mathf.Atan2(4.3f, 3.1f) * Mathf.Rad2Deg;
                FarmBuild.Block(door, "X1", new Vector3(0f, 0f, 0.07f), new Vector3(5.3f, 0.16f, 0.05f), new Vector3(0f, 0f, diag), trim, 1f, false);
                FarmBuild.Block(door, "X2", new Vector3(0f, 0f, 0.07f), new Vector3(5.3f, 0.16f, 0.05f), new Vector3(0f, 0f, -diag), trim, 1f, false);
            }
            FarmBuild.Block(b, "DoorTrack", new Vector3(0f, 5.05f, fz + 0.05f), new Vector3(8f, 0.14f, 0.1f), Vector3.zero, M("Metal"), 1f, false);
            // hay loft door + hoist beam
            FarmBuild.Block(b, "LoftDoor", new Vector3(0f, roofY + 1.7f, fz), new Vector3(2.4f, 2.2f, 0.1f), Vector3.zero, M("DarkWood"), 1f, false);
            FarmBuild.Block(b, "LoftTrim", new Vector3(0f, roofY + 1.7f, fz + 0.05f), new Vector3(2.7f, 2.5f, 0.04f), Vector3.zero, trim, 1f, false);
            FarmBuild.Block(b, "HoistBeam", new Vector3(0f, roofY + 3.3f, fz + 0.9f), new Vector3(0.25f, 0.25f, 2f), Vector3.zero, M("DarkWood"), 1f, false);
            // corner trims, side windows
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                FarmBuild.Block(b, "Corner", new Vector3(sx * (w * 0.5f + 0.03f), 0.3f + wall * 0.5f, sz * (len * 0.5f + 0.03f)), new Vector3(0.22f, wall, 0.22f), Vector3.zero, trim, 1f, false);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = -6f + i * 6f;
                FarmBuild.Block(b, "SideWin", new Vector3(w * 0.5f + 0.04f, 3.8f, z), new Vector3(0.06f, 1.2f, 1.4f), Vector3.zero, M("DarkWood"), 1f, false);
                FarmBuild.Block(b, "SideWinTrim", new Vector3(w * 0.5f + 0.06f, 3.8f, z), new Vector3(0.04f, 1.45f, 1.65f), Vector3.zero, trim, 1f, false);
                FarmBuild.Block(b, "SideWinW", new Vector3(-w * 0.5f - 0.04f, 3.8f, z), new Vector3(0.06f, 1.2f, 1.4f), Vector3.zero, M("DarkWood"), 1f, false);
            }
            // cupola + weather vane
            Transform cup = FarmBuild.Node(b, "Cupola", new Vector3(0f, roofY + rise - 0.1f, 0f));
            FarmBuild.Block(cup, "Box", new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 1.4f, 1.4f), Vector3.zero, M("WhiteTrim"), 1f, false);
            FarmBuild.MeshObj(cup, "Cap", FarmMeshes.GableRoof(1.9f, 1.9f, 0.8f, 0.08f), M("RoofMetal"), new Vector3(0f, 1.4f, 0f), Vector3.zero, Vector3.one);
            Transform vane = FarmBuild.Node(cup, "Vane", new Vector3(0f, 2.3f, 0f));
            FarmBuild.Part(cup, PrimitiveType.Cylinder, "Rod", new Vector3(0f, 2.1f, 0f), Vector3.zero, new Vector3(0.04f, 0.5f, 0.04f), M("Metal"));
            FarmBuild.Block(vane, "Arrow", new Vector3(0f, 0.1f, 0f), new Vector3(0.04f, 0.18f, 1.1f), Vector3.zero, M("Metal"), 1f, false);
            FarmBuild.Block(vane, "Tail", new Vector3(0f, 0.1f, -0.5f), new Vector3(0.03f, 0.4f, 0.3f), Vector3.zero, M("Metal"), 1f, false);
            Spinner sp = vane.gameObject.AddComponent<Spinner>();
            sp.axis = Vector3.up;
            sp.degreesPerSecond = 6f;
            sp.gustiness = 1f;

            // hay inside the open loft, bales by the door
            for (int i = 0; i < 3; i++) RoundBale(env, G(p.x + 10f + i * 1.7f, p.z + 8f + (i % 2) * 0.4f), R(-10f, 10f));
            BaleStack(env, G(p.x - 5.5f, p.z + 13.5f), 15f);
            BaleStack(env, G(p.x + 6.5f, p.z + 13.2f), -8f);
            AmmoBox(env, G(p.x - 3f, p.z + 13.2f), 0f);
        }

        static void BuildSilo(Transform env)
        {
            Vector3 p = FarmLayout.SiloPos;
            Transform s = FarmBuild.Node(env, "Silo", new Vector3(p.x, H(p.x, p.z), p.z));
            GameObject tube = FarmBuild.MeshObj(s, "Tube", FarmMeshes.Cylinder(20, 3.4f, 3.4f, 15f, false), M("SiloMetal"), new Vector3(0f, -0.3f, 0f), Vector3.zero, Vector3.one);
            CapsuleCollider cc = tube.AddComponent<CapsuleCollider>();
            cc.radius = 3.4f;
            cc.height = 16f;
            cc.center = new Vector3(0f, 7.5f, 0f);
            FarmBuild.Surface(tube, SurfaceKind.Metal);
            FarmBuild.Part(s, PrimitiveType.Sphere, "Dome", new Vector3(0f, 14.7f, 0f), Vector3.zero, new Vector3(6.9f, 3.6f, 6.9f), M("Metal"));
            for (int i = 0; i < 5; i++)
                FarmBuild.Part(s, PrimitiveType.Cylinder, "Hoop", new Vector3(0f, 1.5f + i * 3f, 0f), Vector3.zero, new Vector3(6.95f, 0.06f, 6.95f), M("RustyMetal"));
            // ladder
            for (int i = 0; i < 2; i++)
                FarmBuild.Block(s, "LadderRail", new Vector3(-0.3f + i * 0.6f, 7.5f, 3.5f), new Vector3(0.06f, 15f, 0.06f), Vector3.zero, M("Metal"), 1f, false);
            for (int i = 0; i < 36; i++)
                FarmBuild.Block(s, "Rung", new Vector3(0f, 0.5f + i * 0.4f, 3.5f), new Vector3(0.6f, 0.04f, 0.04f), Vector3.zero, M("Metal"), 1f, false);
            FarmBuild.Block(s, "Chute", new Vector3(-3.6f, 3f, 0f), new Vector3(0.8f, 6f, 0.8f), Vector3.zero, M("RustyMetal"), 1f, true, SurfaceKind.Metal);
        }

        // ------------------------------------------------------------------ watchtower

        static void BuildTower(Transform env)
        {
            Vector3 p = FarmLayout.TowerPos;
            Transform t = FarmBuild.Node(env, "Watchtower", new Vector3(p.x, H(p.x, p.z), p.z));
            const float deck = 4.2f, half = 1.8f;
            Material wood = M("FenceWood");
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                FarmBuild.Block(t, "Post", new Vector3(sx * half, 3.6f, sz * half), new Vector3(0.24f, 7.2f, 0.24f), Vector3.zero, wood, 1f, true);
            }
            // cross bracing
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 c = dir * half;
                FarmBuild.Block(t, "Brace", c + Vector3.up * 2.1f, new Vector3(5.0f, 0.12f, 0.08f), new Vector3(0f, yaw, i % 2 == 0 ? 52f : -52f), wood, 1f, false);
            }
            FarmBuild.Block(t, "Deck", new Vector3(0f, deck, 0f), new Vector3(half * 2f + 0.5f, 0.2f, half * 2f + 0.5f), Vector3.zero, M("Wood"), 1f, true);
            // railings on three sides (south side is the stairs)
            FarmBuild.Block(t, "RailN", new Vector3(0f, deck + 1.0f, half + 0.15f), new Vector3(half * 2f + 0.4f, 0.1f, 0.08f), Vector3.zero, wood, 1f, true);
            FarmBuild.Block(t, "RailE", new Vector3(half + 0.15f, deck + 1.0f, 0f), new Vector3(0.08f, 0.1f, half * 2f + 0.4f), Vector3.zero, wood, 1f, true);
            FarmBuild.Block(t, "RailW", new Vector3(-half - 0.15f, deck + 1.0f, 0f), new Vector3(0.08f, 0.1f, half * 2f + 0.4f), Vector3.zero, wood, 1f, true);
            FarmBuild.Block(t, "RailS", new Vector3(0.65f, deck + 1.0f, -half - 0.15f), new Vector3(2.5f, 0.1f, 0.08f), Vector3.zero, wood, 1f, true);
            FarmBuild.Block(t, "KickN", new Vector3(0f, deck + 0.45f, half + 0.15f), new Vector3(half * 2f + 0.4f, 0.1f, 0.08f), Vector3.zero, wood, 1f, false);
            FarmBuild.Block(t, "KickE", new Vector3(half + 0.15f, deck + 0.45f, 0f), new Vector3(0.08f, 0.1f, half * 2f + 0.4f), Vector3.zero, wood, 1f, false);
            FarmBuild.Block(t, "KickW", new Vector3(-half - 0.15f, deck + 0.45f, 0f), new Vector3(0.08f, 0.1f, half * 2f + 0.4f), Vector3.zero, wood, 1f, false);
            FarmBuild.MeshObj(t, "Roof", FarmMeshes.GableRoof(half * 2f + 1.2f, half * 2f + 1.2f, 1.1f, 0.12f), M("RoofMetal"), new Vector3(0f, 7.2f, 0f), Vector3.zero, Vector3.one);

            // stairs up the south side (0.25 m risers - the CharacterController steps them, rats cannot)
            int steps = 17;
            float rise = deck / steps, run = 0.34f;
            for (int i = 0; i < steps; i++)
            {
                float y = rise * (i + 0.5f);
                float z = -half - 0.3f - (steps - 1 - i) * run;
                FarmBuild.Block(t, "Step", new Vector3(-1.25f, y, z), new Vector3(1.1f, rise, run + 0.02f), Vector3.zero, M("Wood"), 1f, true);
            }
            FarmBuild.Block(t, "StairRail", new Vector3(-1.85f, deck * 0.5f + 0.9f, -half - 0.3f - (steps - 1) * run * 0.5f), new Vector3(0.08f, 0.08f, Mathf.Sqrt(deck * deck + (steps * run) * (steps * run))),
                new Vector3(-Mathf.Atan2(deck, steps * run) * Mathf.Rad2Deg, 0f, 0f), wood, 1f, false);   // rises towards +Z

            AmmoBox(t, t.position + new Vector3(1.2f, deck + 0.1f, 1.2f), 20f, true);
            BaleStack(env, G(p.x + 3.5f, p.z - 4f), 30f);
        }

        // ------------------------------------------------------------------ small buildings

        static void BuildSmallBuildings(Transform env)
        {
            // chicken coop
            Vector3 cp = G(-16f, -64f);
            Transform coop = FarmBuild.Node(env, "Chicken Coop", cp, new Vector3(0f, 15f, 0f));
            FarmBuild.Block(coop, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(3f, 1.6f, 2.4f), Vector3.zero, M("Wood"), 1f, true);
            FarmBuild.MeshObj(coop, "Roof", FarmMeshes.GableRoof(3.0f, 3.5f, 0.9f, 0.08f), M("RoofMetal"), new Vector3(0f, 1.8f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            FarmBuild.Block(coop, "Ramp", new Vector3(0f, 0.35f, 1.7f), new Vector3(0.5f, 0.05f, 1.2f), new Vector3(30f, 0f, 0f), M("Wood"), 1f, false);
            FarmBuild.MeshObj(coop, "GableA", FarmMeshes.GableEnd(2.4f, 0.75f, false), M("Wood"), new Vector3(1.5f, 1.8f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            FarmBuild.MeshObj(coop, "GableB", FarmMeshes.GableEnd(2.4f, 0.75f, false), M("Wood"), new Vector3(-1.5f, 1.8f, 0f), new Vector3(0f, -90f, 0f), Vector3.one);
            Hide(coop, "CoopCover", cp + new Vector3(0f, 0f, 0f), 2f);

            // well
            Vector3 wp = G(-24f, -50f);
            Transform well = FarmBuild.Node(env, "Well", wp);
            GameObject ring = FarmBuild.MeshObj(well, "Ring", FarmMeshes.Cylinder(16, 0.9f, 0.9f, 0.9f, true), M("Stone"), Vector3.zero, Vector3.zero, Vector3.one);
            CapsuleCollider rc = ring.AddComponent<CapsuleCollider>();
            rc.radius = 0.9f; rc.height = 1.8f; rc.center = new Vector3(0f, 0.45f, 0f);
            FarmBuild.Surface(ring, SurfaceKind.Stone);
            FarmBuild.Part(well, PrimitiveType.Cylinder, "Dark", new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(1.5f, 0.02f, 1.5f), M("BurrowHole"));
            FarmBuild.Block(well, "PostA", new Vector3(-0.8f, 1.4f, 0f), new Vector3(0.12f, 1.6f, 0.12f), Vector3.zero, M("DarkWood"), 1f, false);
            FarmBuild.Block(well, "PostB", new Vector3(0.8f, 1.4f, 0f), new Vector3(0.12f, 1.6f, 0.12f), Vector3.zero, M("DarkWood"), 1f, false);
            FarmBuild.MeshObj(well, "Roof", FarmMeshes.GableRoof(1.5f, 2f, 0.6f, 0.06f), M("RoofShingle"), new Vector3(0f, 2.2f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            FarmBuild.RodZ(well, "Crank", new Vector3(0f, 1.8f, 0f), 0.06f, 1.6f, M("DarkWood")).transform.localEulerAngles = new Vector3(0f, 0f, 90f);
            FarmBuild.Part(well, PrimitiveType.Cylinder, "Bucket", new Vector3(0.95f, 0.98f, 0.2f), Vector3.zero, new Vector3(0.3f, 0.15f, 0.3f), M("Wood"));
            Hide(well, "WellCover", wp, 1f);

            // windmill water pump on the north edge
            Vector3 mp = G(-30f, 76f);
            Transform mill = FarmBuild.Node(env, "Windmill", mp, new Vector3(0f, 180f, 0f));
            const float mh = 9f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                Vector3 bottom = new Vector3(sx * 1.4f, 0f, sz * 1.4f), top = new Vector3(sx * 0.35f, mh, sz * 0.35f);
                Vector3 mid = (bottom + top) * 0.5f;
                Vector3 dir = top - bottom;
                GameObject leg = FarmBuild.Block(mill, "Leg", mid, new Vector3(0.12f, dir.magnitude, 0.12f), Vector3.zero, M("Metal"), 1f, false);
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            }
            for (int k = 1; k < 4; k++)
            {
                float y = k * 2.2f, half = Mathf.Lerp(1.4f, 0.35f, y / mh);
                for (int i = 0; i < 4; i++)
                {
                    float yaw = i * 90f;
                    Vector3 c = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, y, half);
                    FarmBuild.Block(mill, "Brace", c, new Vector3(half * 2f, 0.06f, 0.06f), new Vector3(0f, yaw, 0f), M("Metal"), 1f, false);
                }
            }
            Transform hub = FarmBuild.Node(mill, "Rotor", new Vector3(0f, mh + 0.3f, 0.6f));
            FarmBuild.RodZ(hub, "Hub", Vector3.zero, 0.15f, 0.3f, M("Metal"));
            for (int i = 0; i < 14; i++)
            {
                Transform blade = FarmBuild.Node(hub, "Blade", Vector3.zero, new Vector3(0f, 0f, i * (360f / 14f)));
                FarmBuild.Block(blade, "Vane", new Vector3(0f, 1.1f, 0f), new Vector3(0.32f, 1.7f, 0.02f), new Vector3(0f, 25f, 0f), M("Metal"), 1f, false);
            }
            FarmBuild.Part(hub, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(3.9f, 0.02f, 3.9f), M("RustyMetal"));
            Spinner spin = hub.gameObject.AddComponent<Spinner>();
            spin.axis = Vector3.forward;
            spin.degreesPerSecond = 70f;
            FarmBuild.Block(mill, "TailBoom", new Vector3(0f, mh + 0.3f, -1.2f), new Vector3(0.08f, 0.08f, 2.4f), Vector3.zero, M("Metal"), 1f, false);
            FarmBuild.Block(mill, "TailVane", new Vector3(0f, mh + 0.5f, -2.4f), new Vector3(0.03f, 1.0f, 1.2f), Vector3.zero, M("RedPaint"), 1f, false);
            FarmBuild.Block(mill, "Platform", new Vector3(0f, mh - 0.2f, 0f), new Vector3(1.2f, 0.08f, 1.2f), Vector3.zero, M("Wood"), 1f, false);
            // tank + trough next to it
            GameObject tank = FarmBuild.MeshObj(env, "Water Tank", FarmMeshes.Cylinder(16, 1.6f, 1.6f, 1.8f, true), M("RustyMetal"), G(-25f, 74f, -0.1f), Vector3.zero, Vector3.one);
            CapsuleCollider tc = tank.AddComponent<CapsuleCollider>();
            tc.radius = 1.6f; tc.height = 3.2f; tc.center = new Vector3(0f, 0.9f, 0f);
            FarmBuild.Surface(tank, SurfaceKind.Metal);
            Hide(tank.transform, "TankCover", tank.transform.position, 1.7f);
        }

        // ------------------------------------------------------------------ props

        static void BuildProps(Transform env)
        {
            Transform props = FarmBuild.Node(env, "Props", Vector3.zero);
            // hay bales scattered in and around the fields (cover for rats)
            Vector2[] round =
            {
                new Vector2(42f, -48f), new Vector2(44f, -46.5f), new Vector2(26f, 66f), new Vector2(64f, 6f), new Vector2(78f, 44f),
                new Vector2(36f, 36f), new Vector2(-22f, 12f), new Vector2(8f, 46f), new Vector2(-50f, 62f), new Vector2(-84f, 30f),
                new Vector2(58f, -44f), new Vector2(84f, -24f), new Vector2(-8f, 70f), new Vector2(18f, -12f)
            };
            for (int i = 0; i < round.Length; i++) RoundBale(props, G(round[i].x, round[i].y), R(0f, 180f));
            BaleStack(props, G(-14f, 4f), 12f);
            BaleStack(props, G(54f, 64f), -20f);
            BaleStack(props, G(44f, -8f), 40f);

            // crates + barrels
            CrateStack(props, G(-31f, -45f), 10f);
            CrateStack(props, G(38f, -47.5f), -15f);
            CrateStack(props, G(24f, -73f), 30f);
            CrateStack(props, G(-6f, -36f), 0f);
            BarrelGroup(props, G(22f, -50f));
            BarrelGroup(props, G(-46f, -44f));
            BarrelGroup(props, G(46f, -72f));
            BarrelGroup(props, G(6f, -60f));

            Tractor(props, G(18f, -31f), 35f);
            Trough(props, G(14f, -52f), 80f);
            Woodpile(props, G(-50f, -58f), 0f);
            Wheelbarrow(props, G(-47f, -37f), -30f);
            Scarecrow(props, G(-55f, 33f), 160f);
            Scarecrow(props, G(48f, 36f), 200f);
            Mailbox(props, G(6f, -97f));

            // power poles along the entry road
            Vector3 prevTop = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                float z = -100f + i * 20f;
                Vector3 pp = G(5.5f, z);
                Transform pole = FarmBuild.Node(props, "Power Pole", pp);
                GameObject shaft = FarmBuild.Part(pole, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 4f, 0f), Vector3.zero, new Vector3(0.28f, 4f, 0.28f), M("DarkWood"), true);
                FarmBuild.Surface(shaft, SurfaceKind.Wood);
                FarmBuild.Block(pole, "Arm", new Vector3(0f, 7.4f, 0f), new Vector3(1.8f, 0.12f, 0.12f), Vector3.zero, M("DarkWood"), 1f, false);
                for (int k = -1; k <= 1; k += 2) FarmBuild.Part(pole, PrimitiveType.Cylinder, "Insulator", new Vector3(k * 0.75f, 7.55f, 0f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), M("Glass"));
                Vector3 top = pp + Vector3.up * 7.6f;
                if (i > 0) Wire(props, prevTop, top);
                prevTop = top;
            }

            // a few rocks
            for (int i = 0; i < 16; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(20f, 90f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                if (FarmLayout.FieldAt(x, z, 2f) >= 0 || FarmLayout.RoadDistance(x, z) < 4f || FarmLayout.InYard(x, z)) continue;
                float s = R(0.4f, 1.1f);
                GameObject rock = FarmBuild.MeshObj(props, "Rock", FarmMeshes.Blob(i % 4, 2, 0.5f), M("Stone"), G(x, z, s * 0.2f), new Vector3(0f, R(0f, 360f), 0f), new Vector3(s * 1.3f, s * 0.7f, s));
                SphereCollider sc = rock.AddComponent<SphereCollider>();
                sc.radius = 0.85f;
                FarmBuild.Surface(rock, SurfaceKind.Stone);
                if (s > 0.8f) Hide(props, "RockCover", rock.transform.position, s * 1.2f);
            }
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                GameObject w = new GameObject("Wire");
                w.transform.SetParent(parent, false);
                LineRenderer lr = w.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 9;
                Vector3 off = new Vector3(k * 0.75f, 0f, 0f);
                for (int i = 0; i < 9; i++)
                {
                    float t = i / 8f;
                    Vector3 p = Vector3.Lerp(a, b, t) + off + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.6f;
                    lr.SetPosition(i, p);
                }
                lr.startWidth = lr.endWidth = 0.025f;
                lr.sharedMaterial = FarmMats.Unlit("Wire", new Color(0.08f, 0.08f, 0.08f));
                lr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static HideSpot Hide(Transform parent, string name, Vector3 worldPos, float radius)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            HideSpot h = go.AddComponent<HideSpot>();
            h.radius = radius;
            hides.Add(h);
            return h;
        }

        static void RoundBale(Transform parent, Vector3 pos, float yaw)
        {
            Transform b = FarmBuild.Node(parent, "Round Bale", pos, new Vector3(0f, yaw, 0f));
            GameObject body = FarmBuild.MeshObj(b, "Hay", FarmMeshes.Cylinder(18, 0.78f, 0.78f, 1.25f, true), M("Hay"), new Vector3(-0.62f, 0.76f, 0f), new Vector3(0f, 0f, -90f), Vector3.one);
            CapsuleCollider c = body.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = 0.78f;
            c.height = 1.6f;
            c.center = new Vector3(0f, 0.625f, 0f);
            FarmBuild.Surface(body, SurfaceKind.Hay);
            Hide(parent, "BaleCover", pos, 0.9f);
        }

        static void BaleStack(Transform parent, Vector3 pos, float yaw)
        {
            Transform s = FarmBuild.Node(parent, "Bale Stack", pos, new Vector3(0f, yaw, 0f));
            Vector3 size = new Vector3(1.1f, 0.45f, 0.55f);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3 - row; i++)
                {
                    GameObject b = FarmBuild.Block(s, "Bale", new Vector3((i - (2 - row) * 0.5f) * 1.12f, 0.22f + row * 0.45f, 0f), size, new Vector3(0f, R(-4f, 4f), 0f), M("Hay"), 1f, true, SurfaceKind.Hay);
                    FarmBuild.Block(b.transform, "Twine", new Vector3(0.28f, 0f, 0f), new Vector3(0.02f, 0.46f, 0.56f), Vector3.zero, M("Rope"), 1f, false);
                    FarmBuild.Block(b.transform, "Twine", new Vector3(-0.28f, 0f, 0f), new Vector3(0.02f, 0.46f, 0.56f), Vector3.zero, M("Rope"), 1f, false);
                }
            Hide(parent, "StackCover", pos, 1.8f);
        }

        static void CrateStack(Transform parent, Vector3 pos, float yaw)
        {
            Transform s = FarmBuild.Node(parent, "Crates", pos, new Vector3(0f, yaw, 0f));
            Crate(s, new Vector3(0f, 0.4f, 0f), 0.8f, 5f);
            Crate(s, new Vector3(0.85f, 0.35f, 0.1f), 0.7f, -8f);
            Crate(s, new Vector3(0.3f, 1.1f, 0f), 0.6f, 20f);
            Hide(parent, "CrateCover", pos, 1.3f);
        }

        static void Crate(Transform parent, Vector3 pos, float s, float yaw)
        {
            Transform c = FarmBuild.Node(parent, "Crate", pos, new Vector3(0f, yaw, 0f));
            FarmBuild.Block(c, "Box", Vector3.zero, new Vector3(s, s, s), Vector3.zero, M("Wood"), 1f, true);
            Material dark = M("DarkWood");
            float e = s * 0.5f + 0.01f;
            for (int i = 0; i < 4; i++)
            {
                float yawE = i * 90f;
                Transform side = FarmBuild.Node(c, "Edge", Vector3.zero, new Vector3(0f, yawE, 0f));
                FarmBuild.Block(side, "Top", new Vector3(0f, s * 0.45f, e), new Vector3(s, s * 0.1f, 0.03f), Vector3.zero, dark, 1f, false);
                FarmBuild.Block(side, "Bottom", new Vector3(0f, -s * 0.45f, e), new Vector3(s, s * 0.1f, 0.03f), Vector3.zero, dark, 1f, false);
                FarmBuild.Block(side, "Diag", new Vector3(0f, 0f, e), new Vector3(s * 1.25f, s * 0.09f, 0.03f), new Vector3(0f, 0f, 45f), dark, 1f, false);
            }
        }

        static void BarrelGroup(Transform parent, Vector3 pos)
        {
            Transform g = FarmBuild.Node(parent, "Barrels", pos);
            Vector3[] offs = { Vector3.zero, new Vector3(0.75f, 0f, 0.2f), new Vector3(0.3f, 0f, 0.75f) };
            for (int i = 0; i < offs.Length; i++)
            {
                bool metal = i == 1;
                GameObject b = FarmBuild.MeshObj(g, "Barrel", FarmMeshes.Cylinder(14, 0.3f, 0.3f, 0.95f, true), M(metal ? "RustyMetal" : "Wood"), offs[i], new Vector3(0f, R(0f, 360f), 0f), Vector3.one);
                CapsuleCollider c = b.AddComponent<CapsuleCollider>();
                c.radius = 0.3f; c.height = 0.95f; c.center = new Vector3(0f, 0.475f, 0f);
                FarmBuild.Surface(b, metal ? SurfaceKind.Metal : SurfaceKind.Wood);
                for (int k = 0; k < 2; k++)
                    FarmBuild.Part(b.transform, PrimitiveType.Cylinder, "Hoop", new Vector3(0f, 0.2f + k * 0.55f, 0f), Vector3.zero, new Vector3(0.62f, 0.02f, 0.62f), M("Metal"));
            }
            Hide(parent, "BarrelCover", pos + new Vector3(0.35f, 0f, 0.3f), 1f);
        }

        static void Tractor(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = FarmBuild.Node(parent, "Tractor", pos, new Vector3(0f, yaw, 0f));
            Material green = M("TractorGreen"), yellow = M("TractorYellow"), rubber = M("Rubber");
            FarmBuild.Block(t, "Chassis", new Vector3(0f, 0.9f, 0.3f), new Vector3(0.9f, 0.5f, 3.0f), Vector3.zero, green, 1f, true, SurfaceKind.Metal);
            FarmBuild.Block(t, "Hood", new Vector3(0f, 1.35f, 0.95f), new Vector3(0.85f, 0.7f, 1.7f), Vector3.zero, green, 1f, true, SurfaceKind.Metal);
            FarmBuild.Block(t, "Grille", new Vector3(0f, 1.3f, 1.81f), new Vector3(0.75f, 0.6f, 0.04f), Vector3.zero, M("Metal"), 1f, false);
            FarmBuild.Block(t, "Seat", new Vector3(0f, 1.45f, -0.75f), new Vector3(0.55f, 0.12f, 0.5f), Vector3.zero, M("Rubber"), 1f, false);
            FarmBuild.Block(t, "SeatBack", new Vector3(0f, 1.75f, -1.0f), new Vector3(0.55f, 0.5f, 0.1f), new Vector3(-10f, 0f, 0f), M("Rubber"), 1f, false);
            FarmBuild.Block(t, "Fender", new Vector3(0.75f, 1.65f, -0.75f), new Vector3(0.6f, 0.08f, 1.5f), Vector3.zero, green, 1f, false);
            FarmBuild.Block(t, "Fender", new Vector3(-0.75f, 1.65f, -0.75f), new Vector3(0.6f, 0.08f, 1.5f), Vector3.zero, green, 1f, false);
            FarmBuild.Part(t, PrimitiveType.Cylinder, "Exhaust", new Vector3(0.3f, 2.0f, 1.3f), Vector3.zero, new Vector3(0.08f, 0.4f, 0.08f), M("Metal"));
            Wheel(t, new Vector3(0.82f, 0.8f, -0.75f), 0.8f, 0.42f, rubber, yellow);
            Wheel(t, new Vector3(-0.82f, 0.8f, -0.75f), 0.8f, 0.42f, rubber, yellow);
            Wheel(t, new Vector3(0.62f, 0.45f, 1.35f), 0.45f, 0.25f, rubber, yellow);
            Wheel(t, new Vector3(-0.62f, 0.45f, 1.35f), 0.45f, 0.25f, rubber, yellow);
            FarmBuild.Block(t, "Steering", new Vector3(0f, 1.85f, -0.25f), new Vector3(0.04f, 0.5f, 0.04f), new Vector3(-30f, 0f, 0f), M("Metal"), 1f, false);
            FarmBuild.Part(t, PrimitiveType.Cylinder, "SteeringWheel", new Vector3(0f, 2.05f, -0.38f), new Vector3(-30f, 0f, 0f), new Vector3(0.4f, 0.01f, 0.4f), rubber);
            Hide(parent, "TractorCover", pos, 2f);
        }

        static void Wheel(Transform parent, Vector3 pos, float r, float width, Material tire, Material rim)
        {
            Transform w = FarmBuild.Node(parent, "Wheel", pos, new Vector3(0f, 0f, 90f));
            GameObject tg = FarmBuild.MeshObj(w, "Tire", FarmMeshes.Cylinder(22, r, r, width, true), tire, new Vector3(0f, -width * 0.5f, 0f), Vector3.zero, Vector3.one);
            CapsuleCollider c = tg.AddComponent<CapsuleCollider>();
            c.radius = r; c.height = Mathf.Max(width, r * 2f); c.center = new Vector3(0f, width * 0.5f, 0f);
            c.direction = 1;
            FarmBuild.Surface(tg, SurfaceKind.Metal);
            FarmBuild.MeshObj(w, "Rim", FarmMeshes.Cylinder(16, r * 0.55f, r * 0.55f, width + 0.02f, true), rim, new Vector3(0f, -width * 0.5f - 0.01f, 0f), Vector3.zero, Vector3.one);
        }

        static void Trough(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = FarmBuild.Node(parent, "Water Trough", pos, new Vector3(0f, yaw, 0f));
            FarmBuild.Block(t, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(2.4f, 0.6f, 0.8f), Vector3.zero, M("RustyMetal"), 1f, true, SurfaceKind.Metal);
            FarmBuild.Block(t, "Water", new Vector3(0f, 0.58f, 0f), new Vector3(2.3f, 0.02f, 0.7f), Vector3.zero, M("Water"), 1f, false);
            Hide(parent, "TroughCover", pos, 1.3f);
        }

        static void Woodpile(Transform parent, Vector3 pos, float yaw)
        {
            Transform w = FarmBuild.Node(parent, "Woodpile", pos, new Vector3(0f, yaw, 0f));
            Mesh log = FarmMeshes.Cylinder(8, 0.13f, 0.13f, 1.2f, true);
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 9 - row; i++)
                    FarmBuild.MeshObj(w, "Log", log, M("Bark"), new Vector3((i - (8 - row) * 0.5f) * 0.27f, 0.13f + row * 0.24f, -0.6f), new Vector3(90f, 0f, R(0f, 360f)), Vector3.one);
            BoxCollider bc = w.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.5f, 0f);
            bc.size = new Vector3(2.5f, 1f, 1.2f);
            FarmBuild.Surface(w.gameObject, SurfaceKind.Wood);
            FarmBuild.Block(w, "ChoppingBlock", new Vector3(1.8f, 0.3f, 0.6f), new Vector3(0.5f, 0.6f, 0.5f), Vector3.zero, M("Bark"), 1f, true);
            Hide(parent, "WoodCover", pos, 1.5f);
        }

        static void Wheelbarrow(Transform parent, Vector3 pos, float yaw)
        {
            Transform w = FarmBuild.Node(parent, "Wheelbarrow", pos, new Vector3(0f, yaw, 0f));
            FarmBuild.Block(w, "Tray", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.3f, 0.9f), new Vector3(-5f, 0f, 0f), M("RedPaint"), 1f, true, SurfaceKind.Metal);
            FarmBuild.Block(w, "HandleL", new Vector3(-0.28f, 0.45f, -0.75f), new Vector3(0.05f, 0.05f, 0.9f), new Vector3(-10f, 0f, 0f), M("Wood"), 1f, false);
            FarmBuild.Block(w, "HandleR", new Vector3(0.28f, 0.45f, -0.75f), new Vector3(0.05f, 0.05f, 0.9f), new Vector3(-10f, 0f, 0f), M("Wood"), 1f, false);
            Wheel(w, new Vector3(0f, 0.2f, 0.55f), 0.2f, 0.08f, M("Rubber"), M("Metal"));
        }

        static void Scarecrow(Transform parent, Vector3 pos, float yaw)
        {
            Transform s = FarmBuild.Node(parent, "Scarecrow", pos, new Vector3(0f, yaw, 0f));
            GameObject post = FarmBuild.Block(s, "Post", new Vector3(0f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), Vector3.zero, M("FenceWood"), 1f, true);
            FarmBuild.Surface(post, SurfaceKind.Wood);
            FarmBuild.Block(s, "Bar", new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 0.08f, 0.08f), Vector3.zero, M("FenceWood"), 1f, false);
            FarmBuild.Part(s, PrimitiveType.Cube, "Shirt", new Vector3(0f, 1.45f, 0f), Vector3.zero, new Vector3(0.5f, 0.6f, 0.25f), M("Flannel"));
            FarmBuild.Part(s, PrimitiveType.Cube, "Sleeves", new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(1.4f, 0.18f, 0.2f), M("Flannel"));
            FarmBuild.Part(s, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.05f, 0f), Vector3.zero, new Vector3(0.32f, 0.36f, 0.32f), M("Cloth"));
            FarmBuild.Part(s, PrimitiveType.Cylinder, "Brim", new Vector3(0f, 2.22f, 0f), Vector3.zero, new Vector3(0.6f, 0.01f, 0.6f), M("Straw"));
            FarmBuild.Part(s, PrimitiveType.Cylinder, "Crown", new Vector3(0f, 2.3f, 0f), Vector3.zero, new Vector3(0.3f, 0.08f, 0.3f), M("Straw"));
            FarmBuild.Part(s, PrimitiveType.Cube, "Straw", new Vector3(0.72f, 1.55f, 0f), new Vector3(0f, 0f, -30f), new Vector3(0.12f, 0.12f, 0.14f), M("Hay"));
            FarmBuild.Part(s, PrimitiveType.Cube, "Straw", new Vector3(-0.72f, 1.55f, 0f), new Vector3(0f, 0f, 30f), new Vector3(0.12f, 0.12f, 0.14f), M("Hay"));
        }

        static void Mailbox(Transform parent, Vector3 pos)
        {
            Transform m = FarmBuild.Node(parent, "Mailbox", pos);
            FarmBuild.Block(m, "Post", new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.1f), Vector3.zero, M("FenceWood"), 1f, true);
            FarmBuild.Block(m, "Box", new Vector3(0f, 1.2f, 0.05f), new Vector3(0.25f, 0.25f, 0.5f), Vector3.zero, M("Metal"), 1f, true, SurfaceKind.Metal);
            FarmBuild.Block(m, "Flag", new Vector3(0.14f, 1.32f, 0.1f), new Vector3(0.02f, 0.2f, 0.08f), Vector3.zero, M("RedPaint"), 1f, false);
        }

        static void AmmoBox(Transform parent, Vector3 worldPos, float yaw, bool small = false)
        {
            Transform a = FarmBuild.Node(parent, "Ammo Crate", Vector3.zero);
            a.position = worldPos;
            a.rotation = Quaternion.Euler(0f, yaw, 0f);
            float s = small ? 0.8f : 1f;
            FarmBuild.Block(a, "Box", new Vector3(0f, 0.22f * s, 0f), new Vector3(0.9f, 0.44f, 0.5f) * s, Vector3.zero, M("AmmoGreen"), 1f, true, SurfaceKind.Wood);
            FarmBuild.Block(a, "Lid", new Vector3(0f, 0.46f * s, -0.25f * s), new Vector3(0.92f, 0.04f, 0.5f) * s, new Vector3(-70f, 0f, 0f), M("AmmoGreen"), 1f, false);
            Transform full = FarmBuild.Node(a, "Rounds", Vector3.zero);
            for (int i = 0; i < 8; i++)
                FarmBuild.Part(full, PrimitiveType.Cylinder, "Cartridge", new Vector3(-0.3f * s + i * 0.085f * s, 0.47f * s, 0.05f), Vector3.zero, new Vector3(0.03f, 0.06f, 0.03f) * s, M("TractorYellow"));
            FarmBuild.Block(a, "Stripe", new Vector3(0f, 0.3f * s, 0.255f * s), new Vector3(0.6f, 0.06f, 0.01f) * s, Vector3.zero, M("TractorYellow"), 1f, false);
            AmmoCrate c = a.gameObject.AddComponent<AmmoCrate>();
            c.fullVisual = full.gameObject;
            crates.Add(c);
        }

        // ------------------------------------------------------------------ fences

        static void BuildFences(Transform env)
        {
            Transform f = FarmBuild.Node(env, "Fences", Vector3.zero);
            float h = FarmLayout.FenceHalf, gate = FarmLayout.GateHalfWidth;
            FenceLine(f, "North", new Vector2(-h, h), new Vector2(h, h));
            FenceLine(f, "East", new Vector2(h, h), new Vector2(h, -h));
            FenceLine(f, "West", new Vector2(-h, -h), new Vector2(-h, h));
            FenceLine(f, "SouthW", new Vector2(-h, -h), new Vector2(-gate, -h));
            FenceLine(f, "SouthE", new Vector2(gate, -h), new Vector2(h, -h));
            for (int i = -1; i <= 1; i += 2)
                FarmBuild.Block(f, "GatePost", G(i * gate, -h, 1.1f), new Vector3(0.3f, 2.2f, 0.3f), Vector3.zero, M("DarkWood"), 1f, true);
            Transform gateT = FarmBuild.Node(f, "Gate", G(gate, -h), new Vector3(0f, -110f, 0f));
            for (int k = 0; k < 4; k++)
                FarmBuild.Block(gateT, "Bar", new Vector3(-gate * 0.5f, 0.35f + k * 0.28f, 0f), new Vector3(gate, 0.1f, 0.05f), Vector3.zero, M("FenceWood"), 1f, false);

            // field fences (with openings for the lanes)
            FarmLayout.Field corn = FarmLayout.Fields[0];
            Rect r = corn.rect;
            FenceLine(f, "CornS", new Vector2(r.xMin - 1f, r.yMin - 1f), new Vector2(r.xMax - 8f, r.yMin - 1f));
            FenceLine(f, "CornE", new Vector2(r.xMax + 1f, r.yMin + 6f), new Vector2(r.xMax + 1f, r.yMax + 1f));
            FarmLayout.Field pump = FarmLayout.Fields[3];
            r = pump.rect;
            FenceLine(f, "PumpkinS", new Vector2(r.xMin - 1f, r.yMin - 1f), new Vector2(r.xMax + 1f, r.yMin - 1f));
            FenceLine(f, "PumpkinE", new Vector2(r.xMax + 1f, r.yMin - 1f), new Vector2(r.xMax + 1f, r.yMax + 1f));
            FarmLayout.Field garden = FarmLayout.Fields[4];
            r = garden.rect;
            FenceLine(f, "GardenN", new Vector2(r.xMin - 0.6f, r.yMax + 0.6f), new Vector2(r.xMax + 0.6f, r.yMax + 0.6f), 0.9f);
            FenceLine(f, "GardenW", new Vector2(r.xMin - 0.6f, r.yMin - 0.6f), new Vector2(r.xMin - 0.6f, r.yMax + 0.6f), 0.9f);
            FenceLine(f, "GardenE", new Vector2(r.xMax + 0.6f, r.yMin - 0.6f), new Vector2(r.xMax + 0.6f, r.yMax + 0.6f), 0.9f);
        }

        /// <summary>Post-and-rail fence from a to b: one combined mesh + one long collider per rail band (rats run under the bottom rail).</summary>
        static void FenceLine(Transform parent, string name, Vector2 a, Vector2 b, float height = 1.15f)
        {
            Vector3 A = G(a.x, a.y), B = G(b.x, b.y);
            Vector3 flat = new Vector3(B.x - A.x, 0f, B.z - A.z);
            float len = flat.magnitude;
            if (len < 0.5f) return;
            Quaternion rot = Quaternion.LookRotation(flat / len);
            Transform t = FarmBuild.Node(parent, "Fence " + name, A);
            t.rotation = rot;
            MeshBuilder mb = new MeshBuilder();
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.6f));
            float step = len / posts;
            for (int i = 0; i <= posts; i++)
            {
                float z = i * step;
                Vector3 world = A + flat / len * z;
                float gy = H(world.x, world.z) - A.y;
                FarmMeshes.Box(mb, new Vector3(0f, gy + height * 0.5f - 0.1f, z), new Vector3(0.13f, height + 0.2f, 0.13f), Quaternion.Euler(0f, R(-3f, 3f), R(-2f, 2f)), 1f);
                if (i < posts)
                {
                    Vector3 w2 = A + flat / len * (z + step);
                    float gy2 = H(w2.x, w2.z) - A.y;
                    float slope = Mathf.Atan2(gy2 - gy, step) * Mathf.Rad2Deg;
                    for (int k = 0; k < 2; k++)
                    {
                        float ry = (k == 0 ? 0.5f : 0.95f) * height / 1.15f;
                        FarmMeshes.Box(mb, new Vector3(0.07f, (gy + gy2) * 0.5f + ry, z + step * 0.5f), new Vector3(0.05f, 0.13f, step + 0.1f), Quaternion.Euler(-slope, 0f, 0f), 1f);
                    }
                }
            }
            Mesh m = mb.ToMesh("Farm_Fence_" + name);
            GameObject go = FarmBuild.MeshObj(t, "Rails", m, M("FenceWood"), Vector3.zero, Vector3.zero, Vector3.one);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            float bottom = 0.42f * height / 1.15f;
            bc.center = new Vector3(0.03f, bottom + (height - bottom) * 0.5f + (B.y - A.y) * 0.5f, len * 0.5f);
            bc.size = new Vector3(0.2f, height - bottom + Mathf.Abs(B.y - A.y), len);
            FarmBuild.Surface(go, SurfaceKind.Wood);
        }

        // ------------------------------------------------------------------ trees

        static void BuildTrees(Transform env, Settings s)
        {
            Transform trees = FarmBuild.Node(env, "Trees", Vector3.zero);
            // ring of woodland outside the fence
            int placed = 0, guard = 0;
            List<Vector2> used = new List<Vector2>();
            while (placed < s.perimeterTrees && guard++ < 4000)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(103f, 165f);
                Vector2 p = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
                if (Mathf.Abs(p.x) < 14f && p.y < -100f) continue;   // keep the entry road open
                bool close = false;
                for (int i = 0; i < used.Count; i++) if ((used[i] - p).sqrMagnitude < 36f) { close = true; break; }
                if (close) continue;
                used.Add(p);
                if (rng.NextDouble() < 0.45) Pine(trees, G(p.x, p.y, -0.1f), R(0.8f, 1.35f));
                else Oak(trees, G(p.x, p.y, -0.1f), R(0.9f, 1.4f), false);
                placed++;
            }
            // shade trees around the house and along the lanes
            Vector2[] inside = { new Vector2(-56f, -40f), new Vector2(-26f, -64f), new Vector2(-58f, -66f), new Vector2(20f, 78f), new Vector2(-12f, 80f), new Vector2(88f, 70f), new Vector2(86f, -60f), new Vector2(-88f, -10f), new Vector2(64f, -86f) };
            for (int i = 0; i < inside.Length; i++) Oak(trees, G(inside[i].x, inside[i].y, -0.1f), R(1.1f, 1.45f), false);
            // apple orchard
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 4; z++)
                    Oak(trees, G(-88f + x * 6f + R(-0.6f, 0.6f), -86f + z * 6.5f + R(-0.6f, 0.6f), -0.05f), R(0.55f, 0.7f), true);
        }

        static void Oak(Transform parent, Vector3 pos, float s, bool apples)
        {
            Transform t = FarmBuild.Node(parent, apples ? "Apple Tree" : "Oak", pos, new Vector3(0f, R(0f, 360f), 0f));
            t.localScale = Vector3.one * s;
            float trunkH = R(3.2f, 4.2f);
            GameObject trunk = FarmBuild.MeshObj(t, "Trunk", FarmMeshes.Cylinder(9, 0.38f, 0.22f, 1f, false, "trunk"), M("Bark"), Vector3.zero, Vector3.zero, new Vector3(1f, trunkH + 0.8f, 1f));
            CapsuleCollider cc = trunk.AddComponent<CapsuleCollider>();
            cc.radius = 0.35f; cc.height = 1f; cc.center = new Vector3(0f, 0.5f, 0f);   // the trunk object is scaled to its height
            FarmBuild.Surface(trunk, SurfaceKind.Wood);
            for (int i = 0; i < 3; i++)
            {
                float yaw = i * 120f + R(-20f, 20f);
                FarmBuild.MeshObj(t, "Branch", FarmMeshes.Cylinder(6, 0.14f, 0.06f, 2.2f, false, "branch"), M("Bark"), new Vector3(0f, trunkH * 0.75f, 0f), new Vector3(R(35f, 55f), yaw, 0f), Vector3.one);
            }
            int blobs = 6 + rng.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * Mathf.PI * 2f + R(-0.3f, 0.3f);
                float rad = i == 0 ? 0f : R(1.2f, 2.0f);
                Vector3 c = new Vector3(Mathf.Cos(a) * rad, trunkH + R(0.6f, 2.2f) + (i == 0 ? 1.2f : 0f), Mathf.Sin(a) * rad);
                float bs = R(1.6f, 2.4f);
                FarmBuild.MeshObj(t, "Foliage", FarmMeshes.Blob(rng.Next(6), 2, 0.4f), M(rng.NextDouble() < 0.5 ? "Leaves" : "LeavesDark"), c, new Vector3(0f, R(0f, 360f), 0f), new Vector3(bs, bs * 0.8f, bs));
                if (apples)
                    for (int k = 0; k < 3; k++)
                        FarmBuild.Part(t, PrimitiveType.Sphere, "Apple", c + Random3(bs * 0.85f), Vector3.zero, Vector3.one * 0.14f, M("RedPaint"));
            }
            perches.Add(FarmBuild.Node(t, "Perch", new Vector3(0f, trunkH + 2.5f, 0f)));
        }

        static Vector3 Random3(float r)
        {
            Vector3 v = new Vector3(R(-1f, 1f), R(-0.6f, 0.4f), R(-1f, 1f));
            return v.normalized * r * 0.5f;
        }

        static void Pine(Transform parent, Vector3 pos, float s)
        {
            Transform t = FarmBuild.Node(parent, "Pine", pos, new Vector3(0f, R(0f, 360f), 0f));
            t.localScale = Vector3.one * s;
            GameObject trunk = FarmBuild.MeshObj(t, "Trunk", FarmMeshes.Cylinder(7, 0.3f, 0.12f, 9f, false, "pinetrunk"), M("Bark"), Vector3.zero, Vector3.zero, Vector3.one);
            CapsuleCollider cc = trunk.AddComponent<CapsuleCollider>();
            cc.radius = 0.3f; cc.height = 9f; cc.center = new Vector3(0f, 4.5f, 0f);
            FarmBuild.Surface(trunk, SurfaceKind.Wood);
            for (int i = 0; i < 5; i++)
            {
                float y = 2.2f + i * 1.7f;
                float r = Mathf.Lerp(2.6f, 0.9f, i / 4f);
                FarmBuild.MeshObj(t, "Needles", FarmMeshes.Cylinder(10, r, 0.05f, 2.6f, true, "pinecone" + i), M("PineNeedles"), new Vector3(0f, y, 0f), new Vector3(0f, R(0f, 360f), 0f), Vector3.one);
            }
            perches.Add(FarmBuild.Node(t, "Perch", new Vector3(0f, 9f, 0f)));
        }

        // ------------------------------------------------------------------ grass + pond

        static void BuildGrass(Transform env, Settings s)
        {
            GameObject g = new GameObject("Grass");
            g.transform.SetParent(env, false);
            VegetationPlanter p = g.AddComponent<VegetationPlanter>();
            p.kind = VegetationKind.Grass;
            p.seed = s.seed + 99;
            p.grassCount = s.grassTufts;
            p.chunkSize = 30f;
            p.castShadows = false;
            p.dryMaterial = M("GrassDry");
            p.material = M("Grass");   // assigned last: setting it lets the planter grow the grass
            p.Regenerate();
        }

        static void BuildPond(Transform env)
        {
            Vector2 c = FarmLayout.PondCenter;
            Transform pond = FarmBuild.Node(env, "Pond", new Vector3(c.x, -0.42f, c.y));
            GameObject water = FarmBuild.MeshObj(pond, "Water", FarmMeshes.Disc(40, FarmLayout.PondRadius * 0.98f, "pondDisc"), M("Water"), Vector3.zero, Vector3.zero, Vector3.one, ShadowCastingMode.Off);
            BoxCollider bc = water.AddComponent<BoxCollider>();
            bc.size = new Vector3(FarmLayout.PondRadius * 1.9f, 0.05f, FarmLayout.PondRadius * 1.9f);
            FarmBuild.Surface(water, SurfaceKind.Water);
            // small jetty
            Transform jetty = FarmBuild.Node(env, "Jetty", G(c.x - FarmLayout.PondRadius - 1.5f, c.y, 0f), new Vector3(0f, 90f, 0f));
            FarmBuild.Block(jetty, "Deck", new Vector3(0f, 0.25f, 2.5f), new Vector3(1.4f, 0.12f, 5f), Vector3.zero, M("Wood"), 1f, true);
            for (int i = 0; i < 3; i++)
                for (int k = -1; k <= 1; k += 2)
                    FarmBuild.Part(jetty, PrimitiveType.Cylinder, "Pile", new Vector3(k * 0.6f, -0.2f, 0.6f + i * 2f), Vector3.zero, new Vector3(0.14f, 0.55f, 0.14f), M("DarkWood"));
        }

        // ------------------------------------------------------------------ burrows

        static List<RatSpawnPoint> BuildBurrows(Transform root)
        {
            Transform parent = FarmBuild.Node(root, "Rat Burrows", Vector3.zero);
            List<RatSpawnPoint> list = new List<RatSpawnPoint>();
            // tier 0: open ground in view of the yard; tier 1: inside the fields; tier 2: far edges / buildings / corners
            Vector3[] pts =
            {
                new Vector3(-8f, 0, 22f), new Vector3(2f, 0, 34f), new Vector3(-12f, 0, 50f), new Vector3(22f, 0, 18f), new Vector3(30f, 0, 28f),
                new Vector3(-30f, 0, 12f), new Vector3(-20f, 0, -4f), new Vector3(14f, 0, 4f),
                new Vector3(60f, 1, 46f), new Vector3(72f, 1, 24f), new Vector3(42f, 1, 56f), new Vector3(-70f, 1, 20f), new Vector3(-48f, 1, 50f),
                new Vector3(-62f, 1, 36f), new Vector3(62f, 1, -30f), new Vector3(76f, 1, -18f), new Vector3(0f, 1, 62f), new Vector3(-56f, 1, -34f),
                new Vector3(22f, 2, -68f), new Vector3(40f, 2, -52f), new Vector3(-48f, 2, -60f), new Vector3(-30f, 2, -42f), new Vector3(-80f, 2, -76f),
                new Vector3(54f, 2, 72f), new Vector3(-86f, 2, 86f), new Vector3(86f, 2, 86f), new Vector3(88f, 2, -86f), new Vector3(-88f, 2, 0f),
                new Vector3(-22f, 2, 84f), new Vector3(10f, 2, -84f)
            };
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 p = G(pts[i].x, pts[i].z);
                GameObject go = new GameObject("Burrow T" + (int)pts[i].y);
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                RatSpawnPoint sp = go.AddComponent<RatSpawnPoint>();
                sp.tier = (int)pts[i].y;
                FarmBuild.MeshObj(go.transform, "Mound", FarmMeshes.Blob(7, 1, 0.5f), M("Soil"), new Vector3(0.15f, 0.02f, -0.2f), new Vector3(0f, R(0f, 360f), 0f), new Vector3(0.7f, 0.18f, 0.6f));
                FarmBuild.MeshObj(go.transform, "Hole", FarmMeshes.Disc(16, 0.17f, "burrowHole"), M("BurrowHole"), new Vector3(-0.1f, 0.07f, 0.15f), new Vector3(-12f, 0f, 0f), Vector3.one, ShadowCastingMode.Off);
                list.Add(sp);
            }
            return list;
        }

        // ------------------------------------------------------------------ player

        static FarmPlayer BuildPlayer(Transform root)
        {
            Vector3 spawn = G(FarmLayout.PlayerSpawn.x, FarmLayout.PlayerSpawn.z, 0.05f);
            GameObject pl = new GameObject("Farmer (Player)");
            pl.transform.SetParent(root, false);
            pl.transform.position = spawn;
            pl.transform.rotation = Quaternion.identity;
            CharacterController cc = pl.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            Transform ts = FarmBuild.Node(pl.transform, "Tracking Space", Vector3.zero);
            Transform head = FarmBuild.Node(ts, "Head", new Vector3(0f, 1.68f, 0f));
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(head, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 70f;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();

            SniperRifle rifle = FarmModels.BuildRifle(head);
            rifle.transform.localPosition = rifle.hipPosition;

            Transform rh = FarmBuild.Node(ts, "Right Hand", new Vector3(0.25f, 1.2f, 0.3f));
            Transform lh = FarmBuild.Node(ts, "Left Hand", new Vector3(-0.25f, 1.2f, 0.3f));
            FarmModels.BuildController(rh, false);
            FarmModels.BuildController(lh, true);
            rh.gameObject.SetActive(false);
            lh.gameObject.SetActive(false);

            FarmerAnimator farmer = FarmModels.BuildFarmer(pl.transform);

            FarmVRRig vr = pl.AddComponent<FarmVRRig>();
            vr.trackingSpace = ts;
            vr.rightHand = rh;
            vr.leftHand = lh;

            FarmPlayer fp = pl.AddComponent<FarmPlayer>();
            fp.cam = cam;
            fp.trackingSpace = ts;
            fp.head = head;
            fp.rifle = rifle;
            fp.farmer = farmer;
            fp.vr = vr;
            fp.ammoCrates = crates.ToArray();
            rifle.owner = fp;

            FarmBuild.SetLayer(pl, PlayerLayer);
            return fp;
        }

        // ------------------------------------------------------------------ systems

        static void BuildSystems(Transform root, Result res, FarmPlayer player, List<CropField> fields, List<RatSpawnPoint> burrows)
        {
            GameObject sys = new GameObject("Farm Sniper Game");
            sys.transform.SetParent(root, false);
            StandaloneSceneMarker marker = sys.AddComponent<StandaloneSceneMarker>();
            marker.levelName = "Farm Sniper";
            FarmAudio audio = sys.AddComponent<FarmAudio>();
            audio.birdPerches = perches.ToArray();
            FarmGameManager gm = sys.AddComponent<FarmGameManager>();
            FarmHUD hud = sys.AddComponent<FarmHUD>();
            hud.game = gm;
            FarmVRPanel vp = sys.AddComponent<FarmVRPanel>();
            vp.game = gm;

            GameObject ratsGo = new GameObject("Rats");
            ratsGo.transform.SetParent(root, false);
            RatSpawner sp = ratsGo.AddComponent<RatSpawner>();
            sp.ratTemplate = FarmModels.BuildRat(ratsGo.transform);
            sp.burrows = burrows.ToArray();
            sp.hideSpots = hides.ToArray();
            sp.fields = fields.ToArray();
            sp.farmHalfSize = FarmLayout.FenceHalf - 3f;

            gm.player = player;
            gm.spawner = sp;
            gm.hud = hud;
            res.game = gm;
            res.spawner = sp;
            res.player = player;
        }
    }
}
