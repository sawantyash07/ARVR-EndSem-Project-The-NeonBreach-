using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach
{
    /// <summary>
    /// PARACHUTE: jump from a plane at 3000 m above a procedurally generated island (height-field mesh with a
    /// generated colour map, beaches, a mountain, trees, ocean and a landing pad). Fall through floating rings for
    /// bonus points, grab optional coins, avoid drifting balloons and bird flocks (each hit costs 5 seconds), open the
    /// canopy, flare near the ground and land on the pad. Score = rings + coins + landing accuracy + landing softness
    /// + time bonus (- auto-deploy penalty). Water or crash landings end the run without landing points.
    /// This class owns the world, collectibles and scoring; <see cref="ParachuteController"/> flies the player.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class ParachuteMode : MiniGame
    {
        [Tooltip("Skydive tuning: aircraft, free-fall physics, deployment altitudes, canopy, landing, camera and controls.")]
        [SerializeField] SkydiveSettings settings = new SkydiveSettings();
        public SkydiveSettings Settings { get { if (settings == null) settings = new SkydiveSettings(); return settings; } }

        public float JumpAltitude { get { return Settings.jumpAltitude; } }
        public float AutoDeployAgl { get { return Settings.autoDeployAltitude; } }
        public float MaxDeployAgl { get { return Settings.deployAltitude; } }
        public float WarningAgl { get { return Settings.warningAltitude; } }
        float PlaneSpeed { get { return Settings.planeSpeed; } }

        public const float WaterLevel = 0f;
        public const float PadRadius = 15f;
        public const int RingCount = 12;
        public const float ObstaclePenalty = 5f;
        const float IslandRadius = 950f;
        const float TerrainHalf = 1400f;
        const int Grid = 200;
        const float SkyRadius = 4500f;

        public override GameMode Kind { get { return GameMode.Parachute; } }
        public override string Title { get { return Loc.T("PARACHUTE"); } }

        // ---- scoring / state read by the HUD
        public int RingsPassed { get; private set; }
        public int RingsMissed { get; private set; }
        public int RingStreak { get; private set; }
        public int CoinsCollected { get; private set; }
        public int CoinsTotal { get { return coins.Count; } }
        public int ObstacleHits { get; private set; }
        public float Timer { get; private set; }
        public float PenaltyTime { get; private set; }
        public float TotalTime { get { return Timer + PenaltyTime; } }
        public bool AutoDeployed { get; private set; }
        public int RingPoints { get; private set; }
        public int CoinPoints { get; private set; }
        public int LandingPoints { get; private set; }
        public int SoftPoints { get; private set; }
        public int TimeBonus { get; private set; }
        public int Penalty { get; private set; }
        public string Outcome { get; private set; }
        public string SoftLabel { get; private set; }
        public bool Crashed { get; private set; }
        public bool HasLanded { get; private set; }
        public float LandDistance { get; private set; }
        public float LandSpeed { get; private set; }
        public Vector3 PadCenter { get; private set; }
        public ParachuteController Controller { get { return ctrl; } }
        public List<Ring> Rings { get { return rings; } }
        public Ring NextRing
        {
            get
            {
                for (int i = 0; i < rings.Count; i++)
                    if (rings[i].State == Ring.RingState.Next || rings[i].State == Ring.RingState.Waiting) return rings[i];
                return null;
            }
        }

        // ---- plane
        public Vector3 PlaneVelocity { get { return planeDir * PlaneSpeed; } }
        public float PlaneYaw { get { return Mathf.Atan2(planeDir.x, planeDir.z) * Mathf.Rad2Deg; } }
        /// <summary>Along-track distance of the plane to the jump point (negative = before it).</summary>
        public float PlaneToJump { get { return plane != null ? Vector3.Dot(plane.position - jumpPoint, planeDir) : 0f; } }
        public bool GreenLight { get { return Mathf.Abs(PlaneToJump) < Settings.greenLightHalfWidth; } }
        public bool PastJumpPoint { get { return PlaneToJump > Settings.autoJumpDistance; } }
        public Transform PlaneTransform { get { return plane; } }

        PlayerController player;
        ParachuteController ctrl;
        GameObject world;
        Transform plane, treesRoot, skyDome, props;
        Vector3 planeDir, jumpPoint;
        bool planeReleased;
        float planeLife;
        Mesh skyMesh;
        Vector3[] skyDirs;
        Color[] skyColors;
        AudioSource windSrc, planeSrc, canopySrc;
        Material cloudMat, beaconMat;
        float finishTimer = -1f;
        float altSmoothed;
        float prevAgl = -1f, beepTimer;
        int beepsQueued;

        void OnValidate() { Settings.Validate(); }

        readonly List<Ring> rings = new List<Ring>();
        readonly List<Transform> coins = new List<Transform>();

        class Obstacle
        {
            public Transform t;
            public float radius;
            public bool bird;
            public Vector3 center;
            public float angle, orbit, speed, cooldown;
            public Transform wingL, wingR;
            public string label;
        }
        readonly List<Obstacle> obstacles = new List<Obstacle>();

        // cached procedural assets (kept across runs so restarting is instant)
        static Mesh terrainMesh;
        static Texture2D terrainTex;
        static float padHeight = float.NaN;
        static readonly Vector2 PadXZ = new Vector2(170f, -150f);

        // ===================================================================================== height field

        /// <summary>GLSL-style smoothstep: 0 below e0, 1 above e1 (Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        public static float Step(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float z)
        {
            float a = 0f, amp = 0.5f, f = 0.0045f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                a += Mathf.PerlinNoise(x * f + 13.1f * o + 100f, z * f + 7.7f * o + 100f) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2.1f;
            }
            return a / norm;
        }

        static float RawHeight(float x, float z)
        {
            float ang = Mathf.Atan2(z, x);
            float d = Mathf.Sqrt(x * x + z * z) / IslandRadius;
            d *= 1f + 0.12f * Mathf.Sin(ang * 3f + 1.3f) + 0.07f * Mathf.Sin(ang * 5f + 0.4f)
                 + (Mathf.PerlinNoise(x * 0.003f + 5f, z * 0.003f + 9f) - 0.5f) * 0.3f;
            float land = 1f - Step(0.6f, 1.0f, d);
            float n = Fbm(x, z);
            float hills = 16f + 60f * n * n;
            float mx = x + 220f, mz = z - 160f;
            float mountain = 150f * Mathf.Exp(-(mx * mx + mz * mz) / (2f * 170f * 170f));
            return Mathf.Lerp(-35f, hills + mountain, land) - 4f;
        }

        static float PadHeight
        {
            get
            {
                if (float.IsNaN(padHeight)) padHeight = Mathf.Max(RawHeight(PadXZ.x, PadXZ.y), 12f);
                return padHeight;
            }
        }

        /// <summary>Terrain height (metres above sea level) at a world position; flattened around the landing pad.</summary>
        public static float Height(float x, float z)
        {
            float raw = RawHeight(x, z);
            float dp = Vector2.Distance(new Vector2(x, z), PadXZ);
            float k = 1f - Step(PadRadius + 8f, PadRadius + 70f, dp);
            return Mathf.Lerp(raw, PadHeight, k);
        }

        // ===================================================================================== lifecycle

        public override void Begin(PlayerController p)
        {
            player = p;
            Running = true;
            Score = 0; NewBest = false;
            RingsPassed = 0; RingsMissed = 0; RingStreak = 0; CoinsCollected = 0; ObstacleHits = 0;
            Timer = 0f; PenaltyTime = 0f; AutoDeployed = false;
            RingPoints = 0; CoinPoints = 0; LandingPoints = 0; SoftPoints = 0; TimeBonus = 0; Penalty = 0;
            Outcome = ""; SoftLabel = ""; Crashed = false; HasLanded = false; LandDistance = 0f; LandSpeed = 0f;
            finishTimer = -1f;
            planeReleased = false;
            planeLife = 0f;
            prevAgl = -1f; beepTimer = 0f; beepsQueued = 0;
            Settings.Validate();

            if (WindSystem.Instance != null) WindSystem.Instance.Randomize(1.5f, 5f, 0.3f);
            BuildWorld();
            ApplyEnvironment(p.cam);

            GameManager.Instance.playerSpawn = PadCenter + Vector3.up * 0.5f;
            ModeManager.TakePlayer(p, DoorPoint(), PlaneYaw + 90f);
            ctrl = p.gameObject.AddComponent<ParachuteController>();
            ctrl.Init(this, p);
            p.Driver = ctrl;

            windSrc = MakeLoopSource("WindAudio", SfxId.WindLoop);
            planeSrc = MakeLoopSource("PlaneAudio", SfxId.PlaneLoop);
            planeSrc.volume = 0.45f;
            canopySrc = MakeLoopSource("CanopyAudio", SfxId.CanopyFlutter);

            GameManager gm = GameManager.Instance;
            if (gm.Hud != null) gm.Hud.Banner(Loc.T("SKYDIVE"), Loc.T("Wait for the green light, then JUMP"));
        }

        public override void Teardown()
        {
            Running = false;
            if (player != null)
            {
                player.Driver = null;
                if (player.vr != null) player.vr.WristOverride = null;
            }
            if (ctrl != null) { ctrl.Shutdown(); Destroy(ctrl); }
            ctrl = null;
            if (world != null) Destroy(world);
            world = null;
            if (windSrc != null) Destroy(windSrc.gameObject);
            if (planeSrc != null) Destroy(planeSrc.gameObject);
            if (canopySrc != null) Destroy(canopySrc.gameObject);
            if (cloudMat != null) Destroy(cloudMat);
            if (beaconMat != null) Destroy(beaconMat);
            if (skyMesh != null) Destroy(skyMesh);
            rings.Clear();
            coins.Clear();
            obstacles.Clear();
            plane = null;
            player = null;
            ComfortVignette.ExternalMotion = -1f;
        }

        AudioSource MakeLoopSource(string name, SfxId id)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(world.transform, false);
            AudioSource s = g.AddComponent<AudioSource>();
            s.clip = AudioSynth.Get(id);
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        void ApplyEnvironment(Camera cam)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.22f);
            RenderSettings.skybox = null;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.farClipPlane = 6000f;
                cam.nearClipPlane = 0.08f;
            }
            altSmoothed = JumpAltitude;
            UpdateSky(JumpAltitude, cam);
        }

        // ===================================================================================== world

        void BuildWorld()
        {
            if (world != null) Destroy(world);
            rings.Clear();
            coins.Clear();
            obstacles.Clear();
            world = new GameObject("ParachuteWorld");
            Transform w = world.transform;
            PadCenter = new Vector3(PadXZ.x, PadHeight, PadXZ.y);

            BuildTerrain(w);
            BuildWater(w);
            BuildPad(w);
            BuildTrees(w);
            BuildSkyDome(w);
            BuildSun(w);
            BuildClouds(w);
            PlanCourse(w);
            BuildPlane(w);
        }

        void BuildTerrain(Transform w)
        {
            if (terrainMesh == null)
            {
                int n = Grid + 1;
                Vector3[] v = new Vector3[n * n];
                Vector2[] uv = new Vector2[n * n];
                float cell = TerrainHalf * 2f / Grid;
                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float wx = -TerrainHalf + x * cell, wz = -TerrainHalf + z * cell;
                        v[z * n + x] = new Vector3(wx, Height(wx, wz), wz);
                        uv[z * n + x] = new Vector2(x / (float)Grid, z / (float)Grid);
                    }
                }
                int[] tri = new int[Grid * Grid * 6];
                int t = 0;
                for (int z = 0; z < Grid; z++)
                {
                    for (int x = 0; x < Grid; x++)
                    {
                        int i = z * n + x;
                        tri[t++] = i; tri[t++] = i + n; tri[t++] = i + 1;
                        tri[t++] = i + 1; tri[t++] = i + n; tri[t++] = i + n + 1;
                    }
                }
                terrainMesh = new Mesh();
                terrainMesh.name = "NB_Island";
                terrainMesh.vertices = v;
                terrainMesh.uv = uv;
                terrainMesh.triangles = tri;
                terrainMesh.RecalculateNormals();
                terrainMesh.RecalculateBounds();
            }
            if (terrainTex == null)
                terrainTex = ProcTex.HeightColorMap(512, Height, -TerrainHalf, TerrainHalf * 2f, WaterLevel);

            GameObject go = new GameObject("Island");
            go.transform.SetParent(w, false);
            go.layer = Layers.Environment;
            go.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Textured("Island", terrainTex, Color.white, 0f, 0.08f, Vector2.one);
            go.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
        }

        void BuildWater(Transform w)
        {
            GameObject water = Util.Part(w, PrimitiveType.Plane, "Ocean", new Vector3(0f, WaterLevel, 0f), Vector3.zero, new Vector3(4000f, 1f, 4000f), "Water");
            Renderer r = water.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildPad(Transform w)
        {
            Transform pad = new GameObject("LandingPad").transform;
            pad.SetParent(w, false);
            pad.position = PadCenter;
            GameObject b = Util.Part(pad, PrimitiveType.Cylinder, "PadBase", Vector3.zero, Vector3.zero, new Vector3(PadRadius * 2f, 0.3f, PadRadius * 2f), "Cover");
            b.AddComponent<MeshCollider>();   // a scaled cylinder's capsule collider would be a 15 m sphere
            b.layer = Layers.Environment;
            float top = 0.305f;
            string[] mats = { "NeonYellow", "Building", "NeonYellow", "Building", "NeonYellow", "Building", "NeonRed" };
            float[] radii = { 15f, 14.4f, 10f, 9.4f, 5f, 4.4f, 1.6f };
            for (int i = 0; i < radii.Length; i++)
                Util.Part(pad, PrimitiveType.Cylinder, "PadRing" + i, new Vector3(0f, top + i * 0.004f, 0f), Vector3.zero, new Vector3(radii[i] * 2f, 0.003f, radii[i] * 2f), mats[i]);
            Util.Part(pad, PrimitiveType.Cube, "PadCrossA", new Vector3(0f, top + 0.03f, 0f), Vector3.zero, new Vector3(8f, 0.003f, 0.6f), "NeonWhite");
            Util.Part(pad, PrimitiveType.Cube, "PadCrossB", new Vector3(0f, top + 0.03f, 0f), Vector3.zero, new Vector3(0.6f, 0.003f, 8f), "NeonWhite");
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (PadRadius + 2.5f);
                Util.Part(pad, PrimitiveType.Cylinder, "BeaconPost", p + Vector3.up * 1f, Vector3.zero, new Vector3(0.25f, 1f, 0.25f), "GunMetal");
                Util.Part(pad, PrimitiveType.Sphere, "BeaconLight", p + Vector3.up * 2.2f, Vector3.zero, Vector3.one * 0.7f, "NeonOrange");
            }
            // a tall translucent light column so the pad can be found from 3 km up
            beaconMat = Mats.FxColor(new Color(0.3f, 1f, 0.45f, 0.22f));
            GameObject col = Util.Part(pad, PrimitiveType.Cylinder, "Beacon", new Vector3(0f, 160f, 0f), Vector3.zero, new Vector3(2.5f, 160f, 2.5f), "NeonGreen");
            col.GetComponent<Renderer>().sharedMaterial = beaconMat;
            Util.NoShadows(col);
            // windsock
            Util.Part(pad, PrimitiveType.Cylinder, "SockPole", new Vector3(PadRadius + 6f, 3f, 0f), Vector3.zero, new Vector3(0.15f, 3f, 0.15f), "GunMetal");
            Light l = Util.AddLight(pad, LightType.Point, new Color(1f, 0.6f, 0.2f), 2f, 40f, new Vector3(0f, 6f, 0f));
            l.name = "PadLight";
            Perf.BatchStatic(pad.gameObject);
        }

        void BuildTrees(Transform w)
        {
            treesRoot = new GameObject("Trees").transform;
            treesRoot.SetParent(w, false);
            System.Random rng = new System.Random(4242);
            int placed = 0;
            for (int i = 0; i < 2600 && placed < 230; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * IslandRadius;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * IslandRadius;
                float h = Height(x, z);
                if (h < 6f || h > 72f) continue;
                if (Vector2.Distance(new Vector2(x, z), PadXZ) < PadRadius + 40f) continue;
                float slope = Mathf.Abs(Height(x + 4f, z) - h) + Mathf.Abs(Height(x, z + 4f) - h);
                if (slope > 3f) continue;
                if (Mathf.PerlinNoise(x * 0.01f + 3f, z * 0.01f + 3f) < 0.42f) continue;   // forests, not a carpet
                float th = 5f + (float)rng.NextDouble() * 6f;
                float crown = 3.5f + (float)rng.NextDouble() * 3f;
                Transform tree = new GameObject("Tree").transform;
                tree.SetParent(treesRoot, false);
                tree.position = new Vector3(x, h - 0.8f, z);
                GameObject trunk = Util.Part(tree, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, th * 0.5f, 0f), Vector3.zero, new Vector3(0.7f, th * 0.5f, 0.7f), "Bark", true);
                trunk.layer = Layers.Environment;
                string leaf = rng.Next(2) == 0 ? "Leaf" : "LeafDark";
                GameObject c1 = Util.Part(tree, PrimitiveType.Sphere, "Crown", new Vector3(0f, th + crown * 0.3f, 0f), Vector3.zero, new Vector3(crown * 2f, crown * 1.7f, crown * 2f), leaf, true);
                c1.layer = Layers.Environment;
                Util.Part(tree, PrimitiveType.Sphere, "CrownTop", new Vector3(0.3f, th + crown * 1.2f, -0.2f), Vector3.zero, new Vector3(crown * 1.3f, crown * 1.2f, crown * 1.3f), leaf);
                placed++;
            }
            // ~690 tree parts share 3 materials: static batching merges them into a handful of draw calls
            Perf.BatchStatic(treesRoot.gameObject);
        }

        public bool IsTree(Collider c)
        {
            return c != null && treesRoot != null && c.transform.IsChildOf(treesRoot);
        }

        void BuildSun(Transform w)
        {
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(w, false);
            sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            Light d = sun.AddComponent<Light>();
            d.type = LightType.Directional;
            d.color = new Color(1f, 0.95f, 0.86f, 1f);
            d.intensity = 1.15f;
            d.shadows = LightShadows.Soft;
            d.shadowStrength = 0.7f;
        }

        void BuildClouds(Transform w)
        {
            cloudMat = Mats.FxColor(new Color(1f, 1f, 1f, 0.33f));
            System.Random rng = new System.Random(77);
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 200f + (float)rng.NextDouble() * 1300f;
                float y = (1100f + (float)rng.NextDouble() * 1200f) * JumpAltitude / 3000f;   // cloud deck scales with the exit altitude
                Transform c = new GameObject("Cloud").transform;
                c.SetParent(w, false);
                c.position = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                for (int k = 0; k < 3; k++)
                {
                    float sx = 90f + (float)rng.NextDouble() * 120f;
                    GameObject puff = Util.Part(c, PrimitiveType.Sphere, "Puff", new Vector3((k - 1) * sx * 0.45f, (float)rng.NextDouble() * 8f, (float)rng.NextDouble() * 30f),
                                                Vector3.zero, new Vector3(sx, 14f + (float)rng.NextDouble() * 12f, sx * 0.7f), "NeonWhite");
                    puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
                    Util.NoShadows(puff);
                }
            }
        }

        /// <summary>Lay out the ring line, the coins, the obstacles and the plane's flight path for this run.</summary>
        void PlanCourse(Transform w)
        {
            props = new GameObject("Course").transform;
            props.SetParent(w, false);
            float baseAng = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(baseAng), 0f, Mathf.Cos(baseAng));
            Vector3 pad = new Vector3(PadCenter.x, 0f, PadCenter.z);
            Vector3 r0 = pad + dirA * 900f;
            Vector3 r1 = pad + Quaternion.Euler(0f, 25f, 0f) * dirA * 150f;
            Vector3 perp = Vector3.Cross(Vector3.up, (r1 - r0).normalized);
            float phase = Random.Range(0f, 6f);

            Vector3[] ringPos = new Vector3[RingCount];
            for (int i = 0; i < RingCount; i++)
            {
                float f = i / (float)(RingCount - 1);
                float wig = Mathf.Sin(f * Mathf.PI * 2.3f + phase) * 55f * Step(0f, 0.15f, f);
                Vector3 hp = Vector3.Lerp(r0, r1, f) + perp * wig;
                hp.y = Mathf.Max(260f, Mathf.Lerp(2750f, 650f, f) * JumpAltitude / 3000f);   // the ring line scales with the exit altitude
                ringPos[i] = hp;
                rings.Add(Ring.Create(props, hp, Mathf.Lerp(14f, 10f, f), i));
            }
            rings[0].SetState(Ring.RingState.Next);

            // free-fall coins between every second pair of rings
            for (int i = 0; i + 1 < RingCount; i += 2)
                AddCoin((ringPos[i] + ringPos[i + 1]) * 0.5f + perp * (i % 4 == 0 ? 14f : -14f));
            // canopy coins on a descending spiral into the pad
            Vector3 fromPad = r1 - pad;
            float th0 = Mathf.Atan2(fromPad.z, fromPad.x);
            for (int i = 0; i < 14; i++)
            {
                float f = i / 13f;
                float rr = Mathf.Lerp(320f, 50f, f);
                float th = th0 + f * Mathf.PI * 1.6f;
                AddCoin(new Vector3(pad.x + Mathf.Cos(th) * rr, PadCenter.y + Mathf.Lerp(560f, 35f, f), pad.z + Mathf.Sin(th) * rr));
            }

            // drifting balloons near the ring line
            for (int i = 0; i < 8; i++)
            {
                float f = Random.Range(0.05f, 0.95f);
                Vector3 hp = Vector3.Lerp(r0, r1, f) + perp * (Random.Range(25f, 70f) * (Random.value < 0.5f ? -1f : 1f));
                hp.y = Mathf.Lerp(2700f, 700f, f) * JumpAltitude / 3000f + Random.Range(-60f, 60f);
                AddBalloon(hp);
            }
            // bird flocks circling along the canopy approach
            for (int i = 0; i < 5; i++)
            {
                float f = (i + 0.5f) / 5f;
                float rr = Mathf.Lerp(300f, 80f, f);
                float th = th0 + f * Mathf.PI * 1.6f + 0.25f;
                Vector3 c = new Vector3(pad.x + Mathf.Cos(th) * rr, PadCenter.y + Mathf.Lerp(520f, 90f, f), pad.z + Mathf.Sin(th) * rr);
                for (int b = 0; b < 5; b++) AddBird(c, b);
            }

            // plane: flies across the ring line so the first ring is right below the green-light zone
            planeDir = Quaternion.Euler(0f, Random.value < 0.5f ? 75f : -75f, 0f) * (-dirA);
            jumpPoint = r0 - planeDir * 80f;
            jumpPoint.y = JumpAltitude;
        }

        void AddCoin(Vector3 p)
        {
            GameObject c = Util.Part(props, PrimitiveType.Cylinder, "Coin", p, new Vector3(90f, 0f, 0f), new Vector3(2.4f, 0.12f, 2.4f), "NeonYellow");
            Util.NoShadows(c);
            coins.Add(c.transform);
        }

        void AddBalloon(Vector3 p)
        {
            Transform b = new GameObject("Balloon").transform;
            b.SetParent(props, false);
            b.position = p;
            Util.Part(b, PrimitiveType.Sphere, "Envelope", Vector3.zero, Vector3.zero, new Vector3(16f, 19f, 16f), "Balloon");
            Util.Part(b, PrimitiveType.Cylinder, "Band", new Vector3(0f, -2f, 0f), Vector3.zero, new Vector3(16.2f, 0.6f, 16.2f), "NeonYellow");
            Util.Part(b, PrimitiveType.Cube, "Basket", new Vector3(0f, -15f, 0f), Vector3.zero, new Vector3(2.4f, 1.6f, 2.4f), "Wood");
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 top = new Vector3(Mathf.Cos(a) * 5f, -7.5f, Mathf.Sin(a) * 5f);
                Vector3 bot = new Vector3(Mathf.Cos(a) * 1.1f, -14.2f, Mathf.Sin(a) * 1.1f);
                Util.Part(b, PrimitiveType.Cube, "Rope", (top + bot) * 0.5f, Quaternion.LookRotation(top - bot).eulerAngles,
                          new Vector3(0.08f, 0.08f, (top - bot).magnitude), "GunGrip");
            }
            obstacles.Add(new Obstacle { t = b, radius = 10.5f, bird = false, label = Loc.T("BALLOON") });
        }

        void AddBird(Vector3 center, int k)
        {
            Transform b = new GameObject("Bird").transform;
            b.SetParent(props, false);
            Util.Part(b, PrimitiveType.Cube, "Body", Vector3.zero, Vector3.zero, new Vector3(0.35f, 0.28f, 1.0f), "EnemyDark");
            Util.Part(b, PrimitiveType.Cube, "Beak", new Vector3(0f, 0f, 0.58f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.2f), "NeonOrange");
            GameObject wl = new GameObject("WingL"); wl.transform.SetParent(b, false);
            GameObject wr = new GameObject("WingR"); wr.transform.SetParent(b, false);
            Util.Part(wl.transform, PrimitiveType.Cube, "W", new Vector3(-0.8f, 0f, 0f), Vector3.zero, new Vector3(1.6f, 0.05f, 0.55f), "EnemyGrey");
            Util.Part(wr.transform, PrimitiveType.Cube, "W", new Vector3(0.8f, 0f, 0f), Vector3.zero, new Vector3(1.6f, 0.05f, 0.55f), "EnemyGrey");
            Util.NoShadows(b.gameObject);
            Obstacle o = new Obstacle
            {
                t = b, radius = 2.6f, bird = true, center = center + Vector3.up * (k * 2.5f - 5f),
                angle = k * 0.6f, orbit = 18f + k * 3f, speed = 11f + k, wingL = wl.transform, wingR = wr.transform,
                label = Loc.T("BIRD STRIKE")
            };
            obstacles.Add(o);
        }

        void BuildPlane(Transform w)
        {
            plane = new GameObject("Plane").transform;
            plane.SetParent(w, false);
            plane.position = jumpPoint - planeDir * PlaneSpeed * Settings.planeLeadTime;
            plane.rotation = Quaternion.LookRotation(planeDir);
            Transform p = plane;
            Util.Part(p, PrimitiveType.Capsule, "Fuselage", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(3.2f, 9f, 3.2f), "PlaneBody");
            Util.Part(p, PrimitiveType.Cube, "Wing", new Vector3(0f, 0.9f, 1.5f), Vector3.zero, new Vector3(28f, 0.35f, 3.6f), "PlaneBody");
            Util.Part(p, PrimitiveType.Cube, "WingStripe", new Vector3(0f, 1.09f, 0.4f), Vector3.zero, new Vector3(27f, 0.02f, 0.3f), "NeonCyan");
            Util.Part(p, PrimitiveType.Cube, "Fin", new Vector3(0f, 2.4f, -7.6f), new Vector3(-15f, 0f, 0f), new Vector3(0.3f, 3.6f, 2.6f), "PlaneBody");
            Util.Part(p, PrimitiveType.Cube, "Stab", new Vector3(0f, 0.8f, -8f), Vector3.zero, new Vector3(9f, 0.25f, 2f), "PlaneBody");
            Util.Part(p, PrimitiveType.Cube, "Windows", new Vector3(0f, 0.55f, 1f), Vector3.zero, new Vector3(3.25f, 0.5f, 9f), "GunGrip");
            Util.Part(p, PrimitiveType.Cube, "Stripe", new Vector3(0f, -0.2f, 0f), Vector3.zero, new Vector3(3.26f, 0.2f, 14f), "NeonMagenta");
            for (int s = -1; s <= 1; s += 2)
            {
                Util.Part(p, PrimitiveType.Cylinder, "Engine", new Vector3(s * 5.5f, 0.3f, 3.2f), new Vector3(90f, 0f, 0f), new Vector3(1.3f, 1.6f, 1.3f), "GunMetal");
                Util.Part(p, PrimitiveType.Cube, "Prop", new Vector3(s * 5.5f, 0.3f, 4.9f), Vector3.zero, new Vector3(3.4f, 0.25f, 0.06f), "GunGrip");
            }
            // the open door on the right side, with a step to stand on
            Util.Part(p, PrimitiveType.Cube, "DoorFrame", new Vector3(1.62f, 0f, 0f), Vector3.zero, new Vector3(0.1f, 2.1f, 1.5f), "GunGrip");
            Util.Part(p, PrimitiveType.Cube, "DoorGlow", new Vector3(1.66f, 1.05f, 0f), Vector3.zero, new Vector3(0.05f, 0.05f, 1.5f), "NeonGreen");
            Util.Part(p, PrimitiveType.Cube, "Step", new Vector3(2.1f, -1.42f, 0f), Vector3.zero, new Vector3(1.1f, 0.08f, 1.3f), "GunMetal");
            Util.NoShadows(plane.gameObject);
        }

        /// <summary>Where the player's feet go while standing in the door.</summary>
        public Vector3 DoorPoint()
        {
            if (plane == null) return jumpPoint;
            return plane.TransformPoint(new Vector3(2.1f, -1.38f, 0f));
        }

        /// <summary>The controller advances the plane while the player rides it (no one-frame lag at the door).</summary>
        public void StepPlane(float dt)
        {
            if (plane == null) return;
            plane.position += planeDir * PlaneSpeed * dt;
            SpinProps(dt);
        }

        void SpinProps(float dt)
        {
            for (int i = 0; i < plane.childCount; i++)
            {
                Transform c = plane.GetChild(i);
                if (c.name == "Prop") c.Rotate(0f, 0f, 1800f * dt, Space.Self);
            }
        }

        // ===================================================================================== sky

        void BuildSkyDome(Transform w)
        {
            const int lon = 32, lat = 16;
            int n = (lon + 1) * (lat + 1);
            Vector3[] v = new Vector3[n];
            skyDirs = new Vector3[n];
            skyColors = new Color[n];
            int k = 0;
            for (int y = 0; y <= lat; y++)
            {
                float el = Mathf.Lerp(-90f, 90f, y / (float)lat) * Mathf.Deg2Rad;
                for (int x = 0; x <= lon; x++)
                {
                    float az = x / (float)lon * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
                    skyDirs[k] = d;
                    v[k] = d * SkyRadius;
                    k++;
                }
            }
            int[] tri = new int[lon * lat * 6];
            int t = 0;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    // wound to face inwards (seen from the centre)
                    tri[t++] = i; tri[t++] = i + 1; tri[t++] = i + lon + 1;
                    tri[t++] = i + 1; tri[t++] = i + lon + 2; tri[t++] = i + lon + 1;
                }
            }
            skyMesh = new Mesh();
            skyMesh.name = "NB_SkyDome";
            skyMesh.vertices = v;
            skyMesh.triangles = tri;
            skyMesh.colors = skyColors;
            skyMesh.bounds = new Bounds(Vector3.zero, Vector3.one * SkyRadius * 2f);
            GameObject go = new GameObject("SkyDome");
            go.transform.SetParent(w, false);
            go.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.FxSky;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            skyDome = go.transform;
        }

        /// <summary>Dynamic sky gradient + fog: deep blue and clear up high, paler and hazier near the sea.</summary>
        void UpdateSky(float altitude, Camera cam)
        {
            float k = Mathf.Clamp01(altitude / JumpAltitude);
            Color zenith = Color.Lerp(new Color(0.30f, 0.55f, 0.90f), new Color(0.07f, 0.18f, 0.52f), k);
            Color horizon = Color.Lerp(new Color(0.80f, 0.87f, 0.93f), new Color(0.62f, 0.76f, 0.93f), k);
            Color below = horizon * 0.97f;   // matches the fully fogged sea at the far plane, so there is no visible edge
            if (skyMesh != null)
            {
                for (int i = 0; i < skyDirs.Length; i++)
                {
                    float e = skyDirs[i].y;
                    Color c = e >= 0f ? Color.Lerp(horizon, zenith, Mathf.Pow(e, 0.55f)) : Color.Lerp(horizon, below, Mathf.Clamp01(-e * 4f));
                    c.a = 1f;
                    skyColors[i] = c;
                }
                skyMesh.colors = skyColors;
            }
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = Mathf.Lerp(150f, 900f, k);
            RenderSettings.fogEndDistance = Mathf.Lerp(2600f, 5600f, k);   // < far plane (6000): the sea fades fully into the sky
            if (cam != null) cam.backgroundColor = horizon;
        }

        // ===================================================================================== per frame

        void Update()
        {
            if (world == null) return;
            GameManager gm = GameManager.Instance;
            bool playing = gm != null && gm.Playing;
            float dt = Time.deltaTime;
            Camera cam = player != null ? player.cam : null;

            if (cam != null)
            {
                altSmoothed = Mathf.Lerp(altSmoothed, cam.transform.position.y, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                UpdateSky(altSmoothed, cam);
            }
            UpdateAudio(playing);
            if (!playing) return;

            if (planeReleased && plane != null)
            {
                StepPlane(dt);
                planeLife += dt;
                if (planeLife > 45f) { Destroy(plane.gameObject); plane = null; }
            }

            if (ctrl != null && ctrl.Airborne) Timer += dt;

            for (int i = 0; i < coins.Count; i++)
                if (coins[i] != null && coins[i].gameObject.activeSelf) coins[i].Rotate(0f, 120f * dt, 0f, Space.World);
            UpdateObstacles(dt);

            if (finishTimer >= 0f)
            {
                finishTimer += dt;
                if (finishTimer > Settings.landingSequenceDuration && Running) FinishRun();
            }
            UpdateAltimeter(dt);
            UpdateWrist();
        }

        void LateUpdate()
        {
            if (skyDome != null && player != null && player.cam != null) skyDome.position = player.cam.transform.position;
        }

        void UpdateAudio(bool playing)
        {
            float speed = ctrl != null ? ctrl.Velocity.magnitude : 0f;
            if (ctrl != null && ctrl.State == ParachuteController.Phase.Plane) speed = PlaneSpeed * 0.6f;
            if (windSrc != null)
            {
                float want = playing ? Mathf.Clamp01(speed / 60f) * 0.9f : 0f;
                windSrc.volume = Mathf.MoveTowards(windSrc.volume, want, Time.unscaledDeltaTime * 1.5f);
                windSrc.pitch = 0.6f + Mathf.Clamp01(speed / 70f) * 0.9f;
            }
            if (planeSrc != null)
            {
                float d = plane != null && player != null ? Vector3.Distance(plane.position, player.transform.position) : 9999f;
                float want = playing ? Mathf.Clamp01(1f - d / 600f) * 0.5f : 0f;
                planeSrc.volume = Mathf.MoveTowards(planeSrc.volume, want, Time.unscaledDeltaTime);
            }
            if (canopySrc != null)
            {
                // fabric flutter of the open canopy: louder and higher when flying fast, quiet when flaring
                bool open = ctrl != null && (ctrl.State == ParachuteController.Phase.Canopy || ctrl.State == ParachuteController.Phase.Deploying);
                float want = playing && open ? 0.12f + Mathf.Clamp01(speed / 16f) * 0.38f : 0f;
                canopySrc.volume = Mathf.MoveTowards(canopySrc.volume, want, Time.unscaledDeltaTime * 0.8f);
                canopySrc.pitch = 0.8f + Mathf.Clamp01(speed / 18f) * 0.45f;
            }
        }

        /// <summary>
        /// Audible altimeter (like the one real skydivers wear in the helmet): two beeps when you enter the deploy
        /// window, three at the warning altitude, then a continuous fast beep until the canopy is out.
        /// </summary>
        void UpdateAltimeter(float dt)
        {
            if (ctrl == null) return;
            SkydiveSettings s = Settings;
            bool falling = ctrl.State == ParachuteController.Phase.FreeFall;
            float agl = ctrl.Agl;
            if (falling && s.audibleAltimeter && prevAgl >= 0f)
            {
                if (prevAgl > s.deployAltitude && agl <= s.deployAltitude) { beepsQueued = 2; beepTimer = 0f; }
                if (prevAgl > s.warningAltitude && agl <= s.warningAltitude) { beepsQueued = 3; beepTimer = 0f; }
            }
            prevAgl = falling ? agl : -1f;
            if (!falling) { beepsQueued = 0; return; }
            if (!s.audibleAltimeter) return;
            beepTimer -= dt;
            if (beepTimer > 0f) return;
            if (beepsQueued > 0)
            {
                beepsQueued--;
                Sfx.Play2D(SfxId.AltimeterBeep, 0.55f, agl <= s.warningAltitude ? 1.25f : 1f);
                beepTimer = 0.22f;
            }
            else if (agl <= s.warningAltitude)
            {
                Sfx.Play2D(SfxId.AltimeterBeep, 0.6f, 1.25f);
                beepTimer = Mathf.Lerp(0.18f, 0.45f, Mathf.InverseLerp(s.autoDeployAltitude, s.warningAltitude, agl));
            }
        }

        void UpdateObstacles(float dt)
        {
            Vector3 wind = WindSystem.Instance != null ? WindSystem.Instance.Current : Vector3.zero;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle o = obstacles[i];
                if (o.t == null) continue;
                o.cooldown -= dt;
                if (o.bird)
                {
                    o.angle += o.speed / o.orbit * dt;
                    Vector3 p = o.center + new Vector3(Mathf.Cos(o.angle), Mathf.Sin(o.angle * 2f) * 0.08f, Mathf.Sin(o.angle)) * o.orbit;
                    Vector3 tangent = new Vector3(-Mathf.Sin(o.angle), 0f, Mathf.Cos(o.angle));
                    o.t.position = p;
                    o.t.rotation = Quaternion.LookRotation(tangent);
                    float flap = Mathf.Sin(Time.time * 9f + i) * 40f;
                    o.wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                    o.wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
                }
                else
                {
                    o.t.position += wind * 0.6f * dt + Vector3.up * Mathf.Sin(Time.time * 0.3f + i) * 0.3f * dt;
                }
            }
        }

        void UpdateWrist()
        {
            if (player == null || player.vr == null || !player.VrOn || ctrl == null) return;
            Vector3 to = PadCenter - player.transform.position;
            to.y = 0f;
            player.vr.WristOverride = Loc.T("ALT") + " " + Mathf.RoundToInt(ctrl.Agl) + " m   " + Mathf.RoundToInt(-ctrl.Velocity.y) + " m/s" +
                                      "\n" + Loc.T("PAD") + " " + Mathf.RoundToInt(to.magnitude) + " m   " + Loc.T("RINGS") + " " + RingsPassed + "/" + RingCount +
                                      "\n" + DeployPrompt;
        }

        // ===================================================================================== gameplay events

        /// <summary>Rings, coins and obstacles along the path the player moved this frame.</summary>
        public void CheckPath(Vector3 a, Vector3 b)
        {
            if (!Running || HasLanded) return;
            GameManager gm = GameManager.Instance;
            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                if (r.State != Ring.RingState.Waiting && r.State != Ring.RingState.Next) continue;
                if (r.Crossed(a, b))
                {
                    RingsPassed++;
                    RingStreak++;
                    int pts = Mathf.Min(200, 100 + 25 * (RingStreak - 1));
                    RingPoints += pts;
                    r.SetState(Ring.RingState.Passed);
                    Sfx.Play2D(SfxId.RingPass, 0.8f, 1f + RingStreak * 0.04f);
                    if (gm.Hud != null) gm.Hud.Toast("+" + pts + "  " + Loc.T("RING") + (RingStreak > 1 ? "  " + Loc.T("STREAK") + " " + RingStreak : ""));
                    if (player != null && player.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.LeftHand, 0.4f, 0.08f);
                }
                else if (b.y < r.transform.position.y - 25f)
                {
                    RingsMissed++;
                    RingStreak = 0;
                    r.SetState(Ring.RingState.Missed);
                }
            }
            Ring next = NextRing;
            if (next != null && next.State == Ring.RingState.Waiting) next.SetState(Ring.RingState.Next);

            for (int i = 0; i < coins.Count; i++)
            {
                Transform c = coins[i];
                if (c == null || !c.gameObject.activeSelf) continue;
                if (SegmentDistance(a, b, c.position) < 4f)
                {
                    c.gameObject.SetActive(false);
                    CoinsCollected++;
                    CoinPoints += 25;
                    Sfx.Play2D(SfxId.Coin, 0.7f);
                    FX.Burst(c.position, new Color(1f, 0.9f, 0.2f), 14, 4f, 0.25f, 0.6f);
                }
            }

            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle o = obstacles[i];
                if (o.t == null || o.cooldown > 0f) continue;
                Vector3 c = o.bird ? o.t.position : o.t.position + Vector3.up * 0.5f;
                if (SegmentDistance(a, b, c) < o.radius)
                {
                    o.cooldown = 3f;
                    ObstacleHits++;
                    PenaltyTime += ObstaclePenalty;
                    Sfx.Play2D(SfxId.Bonk, 0.9f);
                    if (gm.Hud != null) gm.Hud.Toast(o.label + "  +" + Mathf.RoundToInt(ObstaclePenalty) + "s");
                    if (ctrl != null) ctrl.Bump((b - c).normalized);
                    if (player != null) player.Shake(0.25f);
                    if (player != null && player.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.RightHand, 0.8f, 0.15f);
                    if (o.bird) o.t.gameObject.SetActive(false);
                }
            }
            RecomputeScore();
        }

        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return Vector3.Distance(a + ab * t, p);
        }

        public void OnJumped()
        {
            planeReleased = true;
            Sfx.Play2D(SfxId.Dash, 0.9f, 0.7f);
            GameManager gm = GameManager.Instance;
            if (gm.Hud != null) gm.Hud.Banner(Loc.T("FREE FALL"), Loc.T("Steer through the rings"));
        }

        public void OnDeployed(bool automatic)
        {
            AutoDeployed = automatic;
            Penalty = automatic ? Settings.autoDeployPenalty : 0;
            Sfx.Play2D(SfxId.Ripcord, 0.8f);
            Sfx.Play2D(SfxId.ChuteOpen, 1f);
            GameManager gm = GameManager.Instance;
            if (gm.Hud != null)
                gm.Hud.Banner(automatic ? Loc.T("AUTO-DEPLOY") : Loc.T("CANOPY OPEN"),
                              automatic ? "-" + Settings.autoDeployPenalty + " " + Loc.T("points. Steer to the pad, flare before touchdown") : Loc.T("Steer to the pad, flare before touchdown"));
            RecomputeScore();
        }

        /// <summary>Touchdown: evaluate the landing and end the run a few seconds later.</summary>
        public void OnLanded(Vector3 pos, Vector3 impact, Collider hit, bool water, bool canopyOpen)
        {
            if (HasLanded) return;
            HasLanded = true;
            Vector3 flat = pos - PadCenter;
            flat.y = 0f;
            LandDistance = flat.magnitude;
            float vert = Mathf.Max(0f, -impact.y);
            float horiz = new Vector3(impact.x, 0f, impact.z).magnitude;
            LandSpeed = impact.magnitude;
            Crashed = false;
            LandingPoints = 0; SoftPoints = 0; TimeBonus = 0; SoftLabel = "";

            if (water) { Outcome = Loc.T("WATER LANDING"); Crashed = true; Sfx.Play2D(SfxId.Splash, 1f); FX.Burst(pos, new Color(0.8f, 0.9f, 1f), 40, 6f, 0.4f, 1.2f); }
            else if (IsTree(hit)) { Outcome = Loc.T("TREE LANDING"); Crashed = true; Sfx.Play2D(SfxId.Slam, 0.9f); }
            else if (!canopyOpen) { Outcome = Loc.T("NO CANOPY - CRASH"); Crashed = true; Sfx.Play2D(SfxId.Explosion, 0.9f); }
            else if (vert > Settings.safeVerticalSpeed || horiz > Settings.safeHorizontalSpeed) { Outcome = Loc.T("CRASH LANDING"); Crashed = true; Sfx.Play2D(SfxId.Slam, 1f); }
            else
            {
                float soft = Mathf.Sqrt(vert * vert + horiz * horiz * 0.25f);
                if (soft < 2.5f) { SoftPoints = 400; SoftLabel = Loc.T("PERFECT FLARE"); }
                else if (soft < 4.5f) { SoftPoints = 250; SoftLabel = Loc.T("SOFT"); }
                else if (soft < 7f) { SoftPoints = 100; SoftLabel = Loc.T("FIRM"); }
                else SoftLabel = Loc.T("HARD");
                if (LandDistance < 50f)
                {
                    LandingPoints = LandDistance <= 2f ? 1000 : Mathf.Max(0, Mathf.RoundToInt(1000f * (1f - (LandDistance - 2f) / 48f)));
                    TimeBonus = Mathf.Max(0, Mathf.RoundToInt(600f - 3f * TotalTime));
                }
                Outcome = LandDistance <= PadRadius ? Loc.T("ON THE PAD!") : (LandDistance < 50f ? Loc.T("NEAR THE PAD") : Loc.T("OFF TARGET"));
                Sfx.Play2D(SfxId.Land, 1f);
                if (LandDistance <= PadRadius) Sfx.Play2D(SfxId.Bullseye, 0.7f);
            }
            if (Crashed && player != null) player.Shake(0.5f);
            RecomputeScore();
            finishTimer = 0f;
            GameManager gm = GameManager.Instance;
            if (gm.Hud != null)
                gm.Hud.Banner(Outcome, Crashed ? Loc.T("Run over") : LandDistance.ToString("0.0") + " m  -  " + SoftLabel + "  -  " + Score + " " + Loc.T("points"));
        }

        void RecomputeScore()
        {
            Score = RingPoints + CoinPoints + LandingPoints + SoftPoints + TimeBonus - Penalty;
        }

        /// <summary>Altitude prompt for the HUD / VR wrist.</summary>
        public string DeployPrompt
        {
            get
            {
                if (ctrl == null) return "";
                switch (ctrl.State)
                {
                    case ParachuteController.Phase.Plane:
                        return GreenLight ? Loc.T("GREEN LIGHT - JUMP!") : (PlaneToJump < 0f ? Loc.T("Jump zone in") + " " + Mathf.CeilToInt(-PlaneToJump / PlaneSpeed) + " s" : Loc.T("JUMP NOW!"));
                    case ParachuteController.Phase.FreeFall:
                        if (ctrl.Agl > MaxDeployAgl) return Loc.T("Too high to deploy") + " (" + Mathf.RoundToInt(ctrl.Agl - MaxDeployAgl) + " m)";
                        if (ctrl.Agl > Mathf.Lerp(WarningAgl, MaxDeployAgl, 0.3f)) return Loc.T("OPEN PARACHUTE when ready");
                        if (ctrl.Agl > WarningAgl) return Loc.T("OPEN PARACHUTE NOW");
                        return Loc.T("PULL! PULL!");
                    case ParachuteController.Phase.Canopy:
                        return ctrl.Agl < Settings.flarePromptAltitude ? Loc.T("FLARE!") : "";
                    default: return "";
                }
            }
        }

        // ===================================================================================== results

        public override string ResultTitle { get { return Crashed ? Outcome : Loc.T("LANDED") + " - " + Outcome; } }

        public override string StatusLine
        {
            get
            {
                return Loc.T("Rings") + " " + RingsPassed + "/" + RingCount + "   " + Loc.T("Coins") + " " + CoinsCollected + "   " +
                       Loc.T("Time") + " " + TotalTime.ToString("0.0") + "s   " + Loc.T("Best") + " " + ModeManager.Best(GameMode.Parachute);
            }
        }

        public override void ResultRows(List<string> rows)
        {
            rows.Add(Loc.T("Rings") + "  " + RingsPassed + " / " + RingCount); rows.Add("+" + RingPoints);
            rows.Add(Loc.T("Coins") + "  " + CoinsCollected + " / " + CoinsTotal); rows.Add("+" + CoinPoints);
            rows.Add(Loc.T("Landing accuracy") + "  " + LandDistance.ToString("0.0") + " m"); rows.Add("+" + LandingPoints);
            rows.Add(Loc.T("Landing") + "  " + LandSpeed.ToString("0.0") + " m/s  " + SoftLabel); rows.Add("+" + SoftPoints);
            rows.Add(Loc.T("Time") + "  " + TotalTime.ToString("0.0") + " s  (" + ObstacleHits + " " + Loc.T("hits") + ")"); rows.Add("+" + TimeBonus);
            rows.Add(Loc.T("Auto-deploy penalty")); rows.Add(Penalty > 0 ? "-" + Penalty : "0");
            rows.Add(Loc.T("Best (all time)")); rows.Add(ModeManager.Best(GameMode.Parachute).ToString("N0"));
        }
    }
}
