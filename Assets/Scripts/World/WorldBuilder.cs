using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach
{
    /// <summary>
    /// Builds the whole playable world from code: neon arena, cover, skyline, lights, spawn points, the player rig
    /// and the manager objects. Called from the editor menu (to bake it into a real scene) and, as a safety net,
    /// at runtime if the open scene contains no game.
    /// </summary>
    public static class WorldBuilder
    {
        public const float ArenaHalf = 30f;

        public static void BuildAll()
        {
            ApplyEnvironment();
            ArenaInfo info = BuildArenaById(0);
            PlayerController player = PlayerFactory.Create(info.playerSpawn, 0);
            BuildManagers(player, info.spawnPoints);
        }

        /// <summary>Build one arena (geometry, lights, spawn points) under a single root that carries an ArenaInfo.</summary>
        public static ArenaInfo BuildArenaById(int id)
        {
            ArenaInfo info;
            if (id == 1) info = FoundryArena.Build();
            else
            {
                GameObject root = BuildArena();
                info = root.AddComponent<ArenaInfo>();
                info.id = 0;
                info.halfX = ArenaHalf;
                info.halfZ = ArenaHalf;
                info.playerSpawn = new Vector3(0f, 0.05f, 0f);
                BuildLights().transform.SetParent(root.transform, true);
                Transform[] spawns = BuildSpawnPoints();
                spawns[0].parent.SetParent(root.transform, true);
                info.spawnPoints = spawns;
            }
            return info;
        }

        // ------------------------------------------------------------------------------------ environment

        public static void ApplyEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.07f, 1f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.20f, 0.32f, 1f);
            RenderSettings.skybox = null;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, string mat, bool collider, float yRot)
        {
            GameObject go = Util.Part(parent, PrimitiveType.Cube, name, pos, new Vector3(0f, yRot, 0f), scale, mat, collider);
            if (collider) go.layer = Layers.Environment;
            return go;
        }

        public static void Trim(Transform crate, float topY, string mat)
        {
            // thin glowing frame on top of a crate (child, so it inherits the crate's scale and rotation)
            GameObject t = Util.Part(crate, PrimitiveType.Cube, "Trim", new Vector3(0f, 0.5f + 0.03f / topY, 0f),
                                     Vector3.zero, new Vector3(1.03f, 0.06f / topY, 1.03f), mat, false);
            t.layer = 0;
        }

        static GameObject BuildArena()
        {
            GameObject arena = new GameObject("Arena");
            Transform a = arena.transform;
            float h = ArenaHalf;

            // floor with neon grid + decorative rings
            Box(a, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(64f, 1f, 64f), "Floor", true, 0f);
            Util.Part(a, PrimitiveType.Cylinder, "RingOuter", new Vector3(0f, 0.012f, 0f), Vector3.zero, new Vector3(18f, 0.01f, 18f), "NeonCyan");
            Util.Part(a, PrimitiveType.Cylinder, "RingOuterMask", new Vector3(0f, 0.016f, 0f), Vector3.zero, new Vector3(17.5f, 0.01f, 17.5f), "Building");
            Util.Part(a, PrimitiveType.Cylinder, "RingInner", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(7.4f, 0.01f, 7.4f), "NeonMagenta");
            Util.Part(a, PrimitiveType.Cylinder, "RingInnerMask", new Vector3(0f, 0.024f, 0f), Vector3.zero, new Vector3(7f, 0.01f, 7f), "Building");

            // perimeter walls
            Box(a, "Wall_N", new Vector3(0f, 4f, h + 1f), new Vector3(64f, 8f, 2f), "Wall", true, 0f);
            Box(a, "Wall_S", new Vector3(0f, 4f, -h - 1f), new Vector3(64f, 8f, 2f), "Wall", true, 0f);
            Box(a, "Wall_E", new Vector3(h + 1f, 4f, 0f), new Vector3(2f, 8f, 60f), "Wall", true, 0f);
            Box(a, "Wall_W", new Vector3(-h - 1f, 4f, 0f), new Vector3(2f, 8f, 60f), "Wall", true, 0f);

            // neon strips on the inner faces
            float f = h - 0.04f;
            Box(a, "StripN_low", new Vector3(0f, 0.35f, f), new Vector3(60f, 0.12f, 0.06f), "NeonCyan", false, 0f);
            Box(a, "StripN_high", new Vector3(0f, 7.4f, f), new Vector3(60f, 0.12f, 0.06f), "NeonMagenta", false, 0f);
            Box(a, "StripS_low", new Vector3(0f, 0.35f, -f), new Vector3(60f, 0.12f, 0.06f), "NeonCyan", false, 0f);
            Box(a, "StripS_high", new Vector3(0f, 7.4f, -f), new Vector3(60f, 0.12f, 0.06f), "NeonMagenta", false, 0f);
            Box(a, "StripE_low", new Vector3(f, 0.35f, 0f), new Vector3(0.06f, 0.12f, 60f), "NeonCyan", false, 0f);
            Box(a, "StripE_high", new Vector3(f, 7.4f, 0f), new Vector3(0.06f, 0.12f, 60f), "NeonMagenta", false, 0f);
            Box(a, "StripW_low", new Vector3(-f, 0.35f, 0f), new Vector3(0.06f, 0.12f, 60f), "NeonCyan", false, 0f);
            Box(a, "StripW_high", new Vector3(-f, 7.4f, 0f), new Vector3(0.06f, 0.12f, 60f), "NeonMagenta", false, 0f);
            for (int i = -2; i <= 2; i++)
            {
                float o = i * 10f;
                Box(a, "PanelN" + i, new Vector3(o, 4f, f), new Vector3(0.25f, 7f, 0.1f), "NeonMagenta", false, 0f);
                Box(a, "PanelS" + i, new Vector3(o, 4f, -f), new Vector3(0.25f, 7f, 0.1f), "NeonMagenta", false, 0f);
                Box(a, "PanelE" + i, new Vector3(f, 4f, o), new Vector3(0.1f, 7f, 0.25f), "NeonMagenta", false, 0f);
                Box(a, "PanelW" + i, new Vector3(-f, 4f, o), new Vector3(0.1f, 7f, 0.25f), "NeonMagenta", false, 0f);
            }

            // central pillars
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * 12f, 0f, sz * 12f);
                    GameObject col = Util.Part(a, PrimitiveType.Cylinder, "Pillar", p + new Vector3(0f, 2.2f, 0f), Vector3.zero, new Vector3(2.4f, 2.2f, 2.4f), "Cover", true);
                    col.layer = Layers.Environment;
                    Util.Part(a, PrimitiveType.Cylinder, "PillarBandHigh", p + new Vector3(0f, 3.6f, 0f), Vector3.zero, new Vector3(2.5f, 0.05f, 2.5f), "NeonCyan");
                    Util.Part(a, PrimitiveType.Cylinder, "PillarBandLow", p + new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(2.5f, 0.05f, 2.5f), "NeonMagenta");
                }
            }

            // crates and low walls: x, z, sizeX, sizeY, sizeZ, rotationY
            float[,] cover =
            {
                {  0f,  8f, 6f, 1.3f, 0.9f,  0f }, {  0f, -8f, 6f, 1.3f, 0.9f,  0f },
                {  8f,  0f, 0.9f, 1.3f, 6f,  0f }, { -8f,  0f, 0.9f, 1.3f, 6f,  0f },
                { 18f,  6f, 2.2f, 1.2f, 2.2f, 20f }, { 18f, -6f, 2.2f, 1.2f, 2.2f, -15f },
                {-18f,  6f, 2.2f, 1.2f, 2.2f, -20f }, {-18f, -6f, 2.2f, 1.2f, 2.2f, 15f },
                {  6f, 18f, 2.2f, 1.2f, 2.2f, 10f }, { -6f, 18f, 2.2f, 1.2f, 2.2f, -10f },
                {  6f,-18f, 2.2f, 1.2f, 2.2f, -10f }, { -6f,-18f, 2.2f, 1.2f, 2.2f, 10f },
                { 21f, 21f, 4f, 2.4f, 4f,  0f }, { -21f, 21f, 4f, 2.4f, 4f,  0f },
                { 21f,-21f, 4f, 2.4f, 4f,  0f }, { -21f,-21f, 4f, 2.4f, 4f,  0f },
                { 24f,  0f, 1.5f, 1.8f, 6f, 0f }, { -24f, 0f, 1.5f, 1.8f, 6f, 0f },
                {  0f, 24f, 6f, 1.8f, 1.5f, 0f }, {  0f,-24f, 6f, 1.8f, 1.5f, 0f },
            };
            for (int i = 0; i < cover.GetLength(0); i++)
            {
                float sy = cover[i, 3];
                GameObject c = Box(a, "Cover" + i, new Vector3(cover[i, 0], sy * 0.5f, cover[i, 1]),
                                   new Vector3(cover[i, 2], sy, cover[i, 4]), "Cover", true, cover[i, 5]);
                Trim(c.transform, sy, i % 2 == 0 ? "NeonCyan" : "NeonMagenta");
            }

            // corner towers
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * 27f, 0f, sz * 27f);
                    GameObject t = Util.Part(a, PrimitiveType.Cylinder, "Tower", p + new Vector3(0f, 5f, 0f), Vector3.zero, new Vector3(3f, 5f, 3f), "Wall", true);
                    t.layer = Layers.Environment;
                    Util.Part(a, PrimitiveType.Cylinder, "TowerGlow", p + new Vector3(0f, 9.5f, 0f), Vector3.zero, new Vector3(3.1f, 0.08f, 3.1f), "NeonCyan");
                    Util.Part(a, PrimitiveType.Cylinder, "TowerGlowLow", p + new Vector3(0f, 7.5f, 0f), Vector3.zero, new Vector3(3.1f, 0.05f, 3.1f), "NeonMagenta");
                }
            }

            BuildSkyline(a);
            return arena;
        }

        public static void BuildSkyline(Transform arena)
        {
            GameObject sky = new GameObject("Skyline");
            sky.transform.SetParent(arena, false);
            System.Random rng = new System.Random(7);
            int count = 28;
            for (int i = 0; i < count; i++)
            {
                double ang = i / (double)count * Mathf.PI * 2.0 + rng.NextDouble() * 0.15;
                float radius = 46f + (float)rng.NextDouble() * 30f;
                float width = 6f + (float)rng.NextDouble() * 9f;
                float height = 14f + (float)rng.NextDouble() * 34f;
                Vector3 pos = new Vector3((float)System.Math.Cos(ang) * radius, height * 0.5f, (float)System.Math.Sin(ang) * radius);
                GameObject b = Box(sky.transform, "Building" + i, pos, new Vector3(width, height, width * (0.7f + (float)rng.NextDouble() * 0.6f)), "Building", false, (float)rng.NextDouble() * 90f);
                int belts = 1 + rng.Next(3);
                for (int k = 0; k < belts; k++)
                {
                    float ly = -0.45f + (float)rng.NextDouble() * 0.9f;
                    string m = rng.Next(2) == 0 ? "NeonCyan" : "NeonMagenta";
                    Util.Part(b.transform, PrimitiveType.Cube, "Belt", new Vector3(0f, ly, 0f), Vector3.zero, new Vector3(1.01f, 0.012f, 1.01f), m);
                }
                Util.Part(b.transform, PrimitiveType.Cube, "Roof", new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(1.02f, 0.01f, 1.02f), "NeonRed");
            }
        }

        // ------------------------------------------------------------------------------------ lights + spawns

        static GameObject BuildLights()
        {
            GameObject lights = new GameObject("Lights");

            GameObject sun = new GameObject("Moonlight");
            sun.transform.SetParent(lights.transform, false);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light d = sun.AddComponent<Light>();
            d.type = LightType.Directional;
            d.color = new Color(0.55f, 0.65f, 1f, 1f);
            d.intensity = 0.55f;
            d.shadows = LightShadows.Soft;
            d.shadowStrength = 0.8f;

            int n = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Color c = (n % 2 == 0) ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.2f, 0.7f);
                    Light l = Util.AddLight(lights.transform, LightType.Point, c, 2.6f, 17f, new Vector3(sx * 12f, 4.8f, sz * 12f));
                    l.name = "PillarLight" + n;
                    n++;
                }
            }
            Light mid = Util.AddLight(lights.transform, LightType.Point, new Color(0.2f, 0.9f, 1f), 1.6f, 22f, new Vector3(0f, 3.5f, 0f));
            mid.name = "CenterLight";
            return lights;
        }

        static Transform[] BuildSpawnPoints()
        {
            GameObject root = new GameObject("SpawnPoints");
            int count = 16;
            Transform[] pts = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float ang = (i + 0.5f) * (360f / count) * Mathf.Deg2Rad;
                GameObject p = new GameObject("Spawn_" + i);
                p.transform.SetParent(root.transform, false);
                p.transform.position = new Vector3(Mathf.Cos(ang) * 25f, 0.05f, Mathf.Sin(ang) * 25f);
                pts[i] = p.transform;
            }
            return pts;
        }

        static void BuildManagers(PlayerController player, Transform[] spawns)
        {
            GameObject go = new GameObject("GameManager");
            GameManager gm = go.AddComponent<GameManager>();
            WaveManager waves = go.AddComponent<WaveManager>();
            HUD hud = go.AddComponent<HUD>();
            go.AddComponent<Tutorial>();
            go.AddComponent<VRMenuDriver>();
            // mini-game modes (Archery, Parachute) and the mode switcher; they build their worlds on demand
            // (at runtime GameManager.Awake has already added them, so only add what is missing)
            if (go.GetComponent<WindSystem>() == null) go.AddComponent<WindSystem>();
            if (go.GetComponent<ArcheryMode>() == null) go.AddComponent<ArcheryMode>();
            if (go.GetComponent<ParachuteMode>() == null) go.AddComponent<ParachuteMode>();
            if (go.GetComponent<ModeManager>() == null) go.AddComponent<ModeManager>();
            gm.Player = player;
            gm.Waves = waves;
            gm.Hud = hud;
            waves.spawnPoints = spawns;
        }
    }

    /// <summary>Creates the first-person player rig: capsule controller, camera, weapon holder, flashlight, VR rig.</summary>
    public static class PlayerFactory
    {
        public static PlayerController Create(Vector3 position, int index = 0)
        {
            GameObject root = new GameObject(index == 0 ? "Player" : "Player" + (index + 1));
            root.layer = Layers.Player;
            root.transform.position = position;

            PlayerController pc = root.AddComponent<PlayerController>();   // also adds the CharacterController
            CharacterController cc = root.GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.03f;
            cc.slopeLimit = 50f;

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            GameObject camGo = new GameObject(index == 0 ? "Main Camera" : "Camera P" + (index + 1));
            if (index == 0) camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 78f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f, 1f);
            cam.allowHDR = true;
            if (index == 0) camGo.AddComponent<AudioListener>();
            // each player has a visible body for the other player's camera, hidden from its own camera
            int bodyLayer = Layers.PlayerBody(index);
            cam.cullingMask = ~(1 << bodyLayer);
            BuildBody(root.transform, index, bodyLayer);

            GameObject holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(camGo.transform, false);
            holder.transform.localPosition = new Vector3(0.23f, -0.21f, 0.45f);

            WeaponSystem ws = camGo.AddComponent<WeaponSystem>();
            ws.player = pc;
            ws.cam = cam;
            ws.holder = holder.transform;

            Light fl = Util.AddLight(camGo.transform, LightType.Spot, new Color(0.85f, 0.95f, 1f), 3.5f, 35f, Vector3.zero);
            fl.name = "Flashlight";
            fl.spotAngle = 55f;
            fl.enabled = false;

            VRRig vr = index == 0 ? root.AddComponent<VRRig>() : null;

            pc.index = index;
            pc.cameraPivot = pivot.transform;
            pc.cam = cam;
            pc.weapons = ws;
            pc.flashlight = fl;
            pc.vr = vr;
            return pc;
        }

        static void BuildBody(Transform root, int index, int layer)
        {
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root, false);
            string glow = index == 0 ? "NeonCyan" : "NeonGreen";
            Util.Part(body.transform, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.6f, 0.62f, 0.45f), "GunMetal");
            Util.Part(body.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0f), Vector3.zero, Vector3.one * 0.32f, "GunGrip");
            Util.Part(body.transform, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.64f, 0.14f), Vector3.zero, new Vector3(0.26f, 0.07f, 0.06f), glow);
            Util.Part(body.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 1.15f, 0.23f), Vector3.zero, new Vector3(0.3f, 0.05f, 0.02f), glow);
            Util.SetLayerRecursive(body, layer);
            Util.NoShadows(body);
        }
    }
}
