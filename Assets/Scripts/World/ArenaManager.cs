using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach
{
    public static class ArenaCatalog
    {
        public const int Count = 2;

        public static string Name(int id) { return id == 1 ? Loc.T("FOUNDRY") : Loc.T("NEON GRID"); }

        public static string Description(int id)
        {
            return id == 1 ? Loc.T("Long industrial hall: raised platform, ramps and narrow lanes")
                           : Loc.T("Open square arena with pillars, crates and corner towers");
        }
    }

    /// <summary>
    /// Switches arenas at runtime and (re)builds the navigation mesh with the AI Navigation package
    /// (NavMeshSurface), so enemies can path around cover in any arena - including the player's real room in MR.
    /// </summary>
    public static class ArenaManager
    {
        public static int CurrentId { get; private set; }
        public static bool NavReady { get; private set; }
        public static ArenaInfo Current { get; private set; }

        static NavMeshSurface surface;
        static bool builtForMr;

        public static void Prepare(int id, bool mixedReality)
        {
            GameManager gm = GameManager.Instance;
            ArenaInfo info = Object.FindAnyObjectByType<ArenaInfo>();
            bool rebuild = info == null || info.id != id;

            if (rebuild)
            {
                if (MixedReality.Active) MixedReality.Disable();
                if (info != null) Kill(info.gameObject);
                // scenes baked before arenas existed have loose roots
                Kill(GameObject.Find("Arena"));
                Kill(GameObject.Find("Lights"));
                Kill(GameObject.Find("SpawnPoints"));
                info = WorldBuilder.BuildArenaById(id);
                NavReady = false;
            }
            Current = info;
            CurrentId = id;
            if (rebuild || !info.batched)
            {
                Perf.NeonNoShadows(info.gameObject);
                // the arena never moves: merge its meshes into static batches (spawn points carry no renderers)
                Perf.BatchStatic(info.gameObject);
                info.batched = true;
            }

            if (mixedReality != MixedReality.Active)
            {
                if (mixedReality) MixedReality.Enable(info);
                else MixedReality.Disable();
                NavReady = false;
            }
            if (!NavReady || builtForMr != mixedReality) BuildNavMesh();
            builtForMr = mixedReality;

            if (gm != null)
            {
                gm.playerSpawn = mixedReality ? new Vector3(0f, 0.05f, 0f) : info.playerSpawn;
                if (gm.Waves != null) gm.Waves.spawnPoints = mixedReality ? MixedReality.SpawnPoints : info.spawnPoints;
            }
        }

        static void Kill(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);   // so GameObject.Find / FindAnyObjectByType ignore it this frame
            Object.Destroy(go);
        }

        public static void BuildNavMesh()
        {
            if (surface == null)
            {
                GameObject go = new GameObject("Navigation");
                surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = 1 << Layers.Environment;
            }
            surface.RemoveData();
            surface.BuildNavMesh();
            NavMeshHit hit;
            NavReady = NavMesh.SamplePosition(GameManager.Instance != null ? GameManager.Instance.playerSpawn : Vector3.zero, out hit, 50f, NavMesh.AllAreas)
                       || surface.navMeshData != null;
            if (!NavReady) Debug.LogWarning("[Neon Breach] NavMesh build produced no data - enemies fall back to raycast steering.");
        }
    }

    /// <summary>Arena 2: a long industrial hall with a raised central platform, ramps, lanes and furnaces.</summary>
    public static class FoundryArena
    {
        const float HX = 36f, HZ = 24f;

        public static ArenaInfo Build()
        {
            GameObject arena = new GameObject("Arena");
            Transform a = arena.transform;

            WorldBuilder.Box(a, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(HX * 2f + 4f, 1f, HZ * 2f + 4f), "Floor", true, 0f);

            // perimeter
            WorldBuilder.Box(a, "Wall_N", new Vector3(0f, 4f, HZ + 1f), new Vector3(HX * 2f + 4f, 8f, 2f), "Wall", true, 0f);
            WorldBuilder.Box(a, "Wall_S", new Vector3(0f, 4f, -HZ - 1f), new Vector3(HX * 2f + 4f, 8f, 2f), "Wall", true, 0f);
            WorldBuilder.Box(a, "Wall_E", new Vector3(HX + 1f, 4f, 0f), new Vector3(2f, 8f, HZ * 2f), "Wall", true, 0f);
            WorldBuilder.Box(a, "Wall_W", new Vector3(-HX - 1f, 4f, 0f), new Vector3(2f, 8f, HZ * 2f), "Wall", true, 0f);
            float fz = HZ - 0.04f, fx = HX - 0.04f;
            WorldBuilder.Box(a, "StripN", new Vector3(0f, 0.35f, fz), new Vector3(HX * 2f, 0.12f, 0.06f), "NeonOrange", false, 0f);
            WorldBuilder.Box(a, "StripS", new Vector3(0f, 0.35f, -fz), new Vector3(HX * 2f, 0.12f, 0.06f), "NeonOrange", false, 0f);
            WorldBuilder.Box(a, "StripE", new Vector3(fx, 0.35f, 0f), new Vector3(0.06f, 0.12f, HZ * 2f), "NeonOrange", false, 0f);
            WorldBuilder.Box(a, "StripW", new Vector3(-fx, 0.35f, 0f), new Vector3(0.06f, 0.12f, HZ * 2f), "NeonOrange", false, 0f);
            WorldBuilder.Box(a, "StripN_high", new Vector3(0f, 7.4f, fz), new Vector3(HX * 2f, 0.12f, 0.06f), "NeonCyan", false, 0f);
            WorldBuilder.Box(a, "StripS_high", new Vector3(0f, 7.4f, -fz), new Vector3(HX * 2f, 0.12f, 0.06f), "NeonCyan", false, 0f);
            for (int i = -3; i <= 3; i++)
            {
                WorldBuilder.Box(a, "RibN" + i, new Vector3(i * 10f, 4f, fz), new Vector3(0.6f, 8f, 0.4f), "Cover", false, 0f);
                WorldBuilder.Box(a, "RibS" + i, new Vector3(i * 10f, 4f, -fz), new Vector3(0.6f, 8f, 0.4f), "Cover", false, 0f);
            }

            // central raised platform (2 m) with ramps north and south
            GameObject plat = WorldBuilder.Box(a, "Platform", new Vector3(0f, 1f, 0f), new Vector3(12f, 2f, 12f), "Cover", true, 0f);
            WorldBuilder.Trim(plat.transform, 2f, "NeonOrange");
            float ang = Mathf.Atan2(2f, 6f) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(36f + 4f);
            float t = 0.5f;
            float c = Mathf.Cos(ang * Mathf.Deg2Rad), sn = Mathf.Sin(ang * Mathf.Deg2Rad);
            Ramp(a, "RampN", new Vector3(0f, 1f - c * t * 0.5f, 9f - sn * t * 0.5f), ang, len, t);
            Ramp(a, "RampS", new Vector3(0f, 1f - c * t * 0.5f, -9f + sn * t * 0.5f), -ang, len, t);
            // waist-high walls on the platform edges (cover while standing on it)
            WorldBuilder.Box(a, "PlatWallE", new Vector3(5.7f, 2.55f, 0f), new Vector3(0.4f, 1.1f, 8f), "Cover", true, 0f);
            WorldBuilder.Box(a, "PlatWallW", new Vector3(-5.7f, 2.55f, 0f), new Vector3(0.4f, 1.1f, 8f), "Cover", true, 0f);
            WorldBuilder.Box(a, "PlatWallNE", new Vector3(4f, 2.55f, 5.7f), new Vector3(3.5f, 1.1f, 0.4f), "Cover", true, 0f);
            WorldBuilder.Box(a, "PlatWallNW", new Vector3(-4f, 2.55f, 5.7f), new Vector3(3.5f, 1.1f, 0.4f), "Cover", true, 0f);
            WorldBuilder.Box(a, "PlatWallSE", new Vector3(4f, 2.55f, -5.7f), new Vector3(3.5f, 1.1f, 0.4f), "Cover", true, 0f);
            WorldBuilder.Box(a, "PlatWallSW", new Vector3(-4f, 2.55f, -5.7f), new Vector3(3.5f, 1.1f, 0.4f), "Cover", true, 0f);

            // conveyor walls forming lanes (with gaps to cut through)
            float[] lanesX = { -26f, -14f, 14f, 26f };
            for (int i = 0; i < lanesX.Length; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    GameObject w = WorldBuilder.Box(a, "Lane" + i + (s > 0 ? "N" : "S"), new Vector3(lanesX[i], 1.5f, s * 11f), new Vector3(8f, 3f, 1f), "Wall", true, 0f);
                    WorldBuilder.Trim(w.transform, 3f, s > 0 ? "NeonCyan" : "NeonOrange");
                }
            }

            // furnaces (big glowing cylinders)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * 30f, 0f, sz * 18f);
                    GameObject f = Util.Part(a, PrimitiveType.Cylinder, "Furnace", p + new Vector3(0f, 3.5f, 0f), Vector3.zero, new Vector3(4f, 3.5f, 4f), "Wall", true);
                    f.layer = Layers.Environment;
                    Util.Part(a, PrimitiveType.Cylinder, "FurnaceMouth", p + new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(4.05f, 0.5f, 4.05f), "NeonOrange");
                    Util.Part(a, PrimitiveType.Cylinder, "FurnaceBand", p + new Vector3(0f, 6.2f, 0f), Vector3.zero, new Vector3(4.1f, 0.06f, 4.1f), "NeonCyan");
                }
            }

            // crates: x, z, sx, sy, sz, rotY
            float[,] crates =
            {
                { -20f, 0f, 2.2f, 1.2f, 2.2f, 15f }, { 20f, 0f, 2.2f, 1.2f, 2.2f, -15f },
                { -8f, 15f, 3f, 1.3f, 1f, 0f }, { 8f, -15f, 3f, 1.3f, 1f, 0f },
                { -8f, -17f, 2f, 1.2f, 2f, 30f }, { 8f, 17f, 2f, 1.2f, 2f, -30f },
                { -32f, 0f, 1.5f, 1.8f, 5f, 0f }, { 32f, 0f, 1.5f, 1.8f, 5f, 0f },
                { -19f, -19f, 2.4f, 2.4f, 2.4f, 0f }, { 19f, 19f, 2.4f, 2.4f, 2.4f, 0f },
                { 0f, 20f, 5f, 1.4f, 1f, 0f }, { 0f, -20f, 5f, 1.4f, 1f, 0f },
            };
            for (int i = 0; i < crates.GetLength(0); i++)
            {
                float sy = crates[i, 3];
                GameObject cr = WorldBuilder.Box(a, "Crate" + i, new Vector3(crates[i, 0], sy * 0.5f, crates[i, 1]),
                                                 new Vector3(crates[i, 2], sy, crates[i, 4]), "Cover", true, crates[i, 5]);
                WorldBuilder.Trim(cr.transform, sy, i % 2 == 0 ? "NeonOrange" : "NeonCyan");
            }

            // overhead gantry beams (decoration, no collision)
            for (int i = -2; i <= 2; i++)
                Util.Part(a, PrimitiveType.Cube, "Gantry" + i, new Vector3(i * 14f, 9f, 0f), Vector3.zero, new Vector3(0.6f, 0.6f, HZ * 2f), "Cover");
            Util.Part(a, PrimitiveType.Cube, "GantryGlow", new Vector3(0f, 8.65f, 0f), Vector3.zero, new Vector3(HX * 2f, 0.06f, 0.2f), "NeonOrange");

            WorldBuilder.BuildSkyline(a);

            // lights
            GameObject lights = new GameObject("Lights");
            lights.transform.SetParent(a, false);
            GameObject sun = new GameObject("Moonlight");
            sun.transform.SetParent(lights.transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, 25f, 0f);
            Light d = sun.AddComponent<Light>();
            d.type = LightType.Directional;
            d.color = new Color(0.6f, 0.6f, 0.9f, 1f);
            d.intensity = 0.45f;
            d.shadows = LightShadows.Soft;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Util.AddLight(lights.transform, LightType.Point, new Color(1f, 0.5f, 0.15f), 3f, 16f, new Vector3(sx * 30f, 3f, sz * 13f)).name = "FurnaceLight";
            Util.AddLight(lights.transform, LightType.Point, new Color(1f, 0.55f, 0.2f), 2.2f, 18f, new Vector3(0f, 6f, 0f)).name = "PlatformLight";
            Util.AddLight(lights.transform, LightType.Point, new Color(0.2f, 0.9f, 1f), 1.8f, 20f, new Vector3(-20f, 5f, 0f)).name = "LaneLightW";
            Util.AddLight(lights.transform, LightType.Point, new Color(0.2f, 0.9f, 1f), 1.8f, 20f, new Vector3(20f, 5f, 0f)).name = "LaneLightE";

            // spawn points around the edges
            GameObject sp = new GameObject("SpawnPoints");
            sp.transform.SetParent(a, false);
            Vector3[] pts =
            {
                new Vector3(-33f, 0.05f, -20f), new Vector3(-33f, 0.05f, -6f), new Vector3(-33f, 0.05f, 6f), new Vector3(-33f, 0.05f, 20f),
                new Vector3(33f, 0.05f, -20f), new Vector3(33f, 0.05f, -6f), new Vector3(33f, 0.05f, 6f), new Vector3(33f, 0.05f, 20f),
                new Vector3(-22f, 0.05f, 21f), new Vector3(-10f, 0.05f, 21f), new Vector3(10f, 0.05f, 21f), new Vector3(22f, 0.05f, 21f),
                new Vector3(-22f, 0.05f, -21f), new Vector3(-10f, 0.05f, -21f), new Vector3(10f, 0.05f, -21f), new Vector3(22f, 0.05f, -21f),
            };
            Transform[] spawns = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                GameObject g = new GameObject("Spawn_" + i);
                g.transform.SetParent(sp.transform, false);
                g.transform.position = pts[i];
                spawns[i] = g.transform;
            }

            ArenaInfo info = arena.AddComponent<ArenaInfo>();
            info.id = 1;
            info.halfX = HX;
            info.halfZ = HZ;
            info.playerSpawn = new Vector3(0f, 0.05f, -15f);
            info.spawnPoints = spawns;
            return info;
        }

        static void Ramp(Transform a, string name, Vector3 center, float angleX, float length, float thickness)
        {
            GameObject r = Util.Part(a, PrimitiveType.Cube, name, center, new Vector3(angleX, 0f, 0f), new Vector3(4f, thickness, length), "Cover", true);
            r.layer = Layers.Environment;
            Util.Part(r.transform, PrimitiveType.Cube, "Edge", new Vector3(0.49f, 0.55f, 0f), Vector3.zero, new Vector3(0.03f, 0.2f, 1f), "NeonOrange");
            Util.Part(r.transform, PrimitiveType.Cube, "Edge", new Vector3(-0.49f, 0.55f, 0f), Vector3.zero, new Vector3(0.03f, 0.2f, 1f), "NeonOrange");
        }
    }
}
