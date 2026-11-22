using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach
{
    /// <summary>
    /// ARCHERY: a neon shooting range built from primitives with static, sliding (rail) and pop-up targets at
    /// 10 / 20 / 30 / 40 m. 3 rounds of 10 arrows; the wind changes every round. Hold fire to draw the bow
    /// (0 to 1 in about a second), release to shoot - a fuller draw means a faster, flatter, longer arrow.
    /// Zoom (right mouse / VR left trigger) narrows the view but adds breath sway; hold Shift to steady it until
    /// your breath (stamina) runs out. Consecutive hits build a streak bonus.
    /// Input comes from the player's shared PlayerInputState (keyboard+mouse, gamepad or VR) - no extra input path.
    /// </summary>
    [DefaultExecutionOrder(-10)]   // after PlayerController (-50) has filled the input this frame
    public class ArcheryMode : MiniGame
    {
        public const int Rounds = 3;
        public const int ArrowsPerRound = 10;
        public const float DrawTime = 1.0f;
        public const float MinSpeed = 14f;
        public const float MaxSpeed = 78f;
        public const float ZoomFov = 28f;
        const float BreathHoldTime = 4f;
        const float DesktopDrawLength = 0.42f;   // in bow space (the desktop bow is drawn at 0.6 scale)
        const float DesktopBowScale = 0.6f;
        const float VrDrawLength = 0.35f;

        public enum Phase { Shooting, RoundSummary, Done }

        public override GameMode Kind { get { return GameMode.Archery; } }
        public override string Title { get { return Loc.T("ARCHERY"); } }

        public Phase State { get; private set; }
        public int Round { get; private set; }
        public int ArrowsLeft { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Hits { get; private set; }
        public int Bullseyes { get; private set; }
        public int Shots { get; private set; }
        public float Draw { get; private set; }
        public float Zoom { get; private set; }
        public float Breath { get; private set; }
        public bool Exhausted { get; private set; }
        public bool HoldingBreath { get; private set; }
        public float StreakMultiplier { get { return 1f + 0.1f * Mathf.Clamp(Streak - 1, 0, 5); } }
        public readonly int[] RoundScores = new int[Rounds];
        /// <summary>Points per arrow: -1 not shot yet, 0 miss.</summary>
        public readonly int[,] ArrowScores = new int[Rounds, ArrowsPerRound];
        public readonly float[] RoundWindSpeed = new float[Rounds];
        public string LastHit { get; private set; }
        public Color LastHitColor { get; private set; }
        public float LastHitTime { get; private set; }
        public float SummaryAge { get { return Time.time - summaryStart; } }

        PlayerController player;
        GameObject range;
        readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        readonly List<Transform> flags = new List<Transform>();

        // bow viewmodel
        Transform bowRoot, upperTip, lowerTip;
        readonly List<Transform> upperJoints = new List<Transform>();
        readonly List<Transform> lowerJoints = new List<Transform>();
        LineRenderer bowString;
        Transform nocked;
        float bowKick;

        float nockCooldown;
        bool needRelease;
        float roundEndTimer;
        float summaryStart;
        float swayT, swayAmp, fullDrawTime;
        int landedThisRound;

        static readonly Color FletchColor = new Color(0.2f, 0.95f, 1f, 1f);

        // ===================================================================================== lifecycle

        public override void Begin(PlayerController p)
        {
            player = p;
            Running = true;
            Score = 0; NewBest = false;
            Streak = 0; BestStreak = 0; Hits = 0; Bullseyes = 0; Shots = 0;
            Draw = 0f; Zoom = 0f; Breath = 1f; Exhausted = false; HoldingBreath = false;
            LastHit = ""; LastHitTime = -10f;
            for (int r = 0; r < Rounds; r++) { RoundScores[r] = 0; RoundWindSpeed[r] = 0f; for (int i = 0; i < ArrowsPerRound; i++) ArrowScores[r, i] = -1; }

            Arrow.ClearAll();
            BuildRange();
            ApplyEnvironment(p.cam);

            Vector3 spawn = new Vector3(0f, 0.05f, -2f);
            GameManager.Instance.playerSpawn = spawn;
            ModeManager.TakePlayer(p, spawn, 0f);
            BuildBow();
            StartRound(0);
        }

        public override void Teardown()
        {
            Running = false;
            Arrow.ClearAll();
            targets.Clear();
            flags.Clear();
            upperJoints.Clear();
            lowerJoints.Clear();
            if (range != null) Destroy(range);
            if (bowRoot != null) Destroy(bowRoot.gameObject);
            range = null;
            bowRoot = null;
            if (player != null) player.ClearModeHooks();
            player = null;
        }

        void ApplyEnvironment(Camera cam)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0085f;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.08f, 1f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.36f, 1f);
            RenderSettings.skybox = null;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 1f);
                cam.nearClipPlane = player != null && player.VrOn ? 0.05f : 0.03f;
                cam.farClipPlane = 400f;
            }
        }

        // ===================================================================================== rounds

        void StartRound(int r)
        {
            Round = r;
            ArrowsLeft = ArrowsPerRound;
            State = Phase.Shooting;
            roundEndTimer = 0f;
            landedThisRound = 0;
            needRelease = true;
            nockCooldown = 0.4f;
            Draw = 0f;
            // the wind gets stronger and gustier every round
            float[] min = { 0.5f, 2.0f, 4.0f };
            float[] max = { 2.0f, 4.5f, 7.5f };
            float[] gust = { 0.15f, 0.25f, 0.35f };
            WindSystem w = WindSystem.Instance;
            if (w != null)
            {
                w.Randomize(min[r], max[r], gust[r]);
                RoundWindSpeed[r] = w.BaseSpeed;
            }
            GameManager gm = GameManager.Instance;
            if (gm.Hud != null)
                gm.Hud.Banner(Loc.T("ROUND") + " " + (r + 1) + " / " + Rounds,
                              Loc.T("Wind") + " " + RoundWindSpeed[r].ToString("0.0") + " m/s   -   " + ArrowsPerRound + " " + Loc.T("arrows"));
        }

        void EndRound()
        {
            State = Phase.RoundSummary;
            summaryStart = Time.time;
            Draw = 0f;
            needRelease = true;
            Sfx.Play2D(SfxId.WaveStart, 0.55f, 1.15f);
            // the desktop HUD draws a summary panel; in VR the banner floats in front of the headset instead
            GameManager gm = GameManager.Instance;
            if (gm.Hud != null && player != null && player.VrOn)
                gm.Hud.Banner(Loc.T("ROUND") + " " + (Round + 1) + " " + Loc.T("COMPLETE"), RoundScores[Round] + " " + Loc.T("points"));
        }

        void NextRound()
        {
            if (Round + 1 < Rounds) StartRound(Round + 1);
            else Finish();
        }

        void Finish()
        {
            State = Phase.Done;
            Draw = 0f;
            if (player != null) { player.FovOverride = 0f; player.AimOffset = Vector2.zero; }
            Sfx.Play2D(SfxId.PowerUp, 0.8f);
            FinishRun();
        }

        // ===================================================================================== per frame

        void Update()
        {
            if (!Running || player == null) return;
            GameManager gm = GameManager.Instance;
            UpdateFlags();
            if (gm == null || !gm.Playing)
            {
                UpdateBow(Time.unscaledDeltaTime);
                return;
            }
            float dt = Time.deltaTime;
            PlayerInputState inp = player.Inp;
            bool vr = player.VrOn;

            UpdateAim(inp, dt, vr);

            switch (State)
            {
                case Phase.Shooting:
                    UpdateShooting(inp, dt, vr);
                    if (ArrowsLeft == 0 && Arrow.InFlight == 0)
                    {
                        roundEndTimer += dt;
                        if (roundEndTimer > 1.1f) EndRound();
                    }
                    break;
                case Phase.RoundSummary:
                    if (!inp.fireHeld && !inp.jumpPressed) needRelease = false;
                    bool go = !needRelease && (inp.firePressed || inp.jumpPressed) && SummaryAge > 1.2f;
                    if (go || SummaryAge > 12f) { NextRound(); needRelease = true; }
                    break;
            }

            UpdateBow(dt);
            UpdateWrist();
        }

        void UpdateAim(PlayerInputState inp, float dt, bool vr)
        {
            bool wantZoom = State != Phase.Done && (inp.aimHeld || inp.zoomHeld);
            Zoom = Mathf.MoveTowards(Zoom, wantZoom ? 1f : 0f, dt * 5f);
            player.BlockSprint = wantZoom;   // Shift = hold your breath while zoomed

            bool wantHold = wantZoom && inp.sprintHeld;
            HoldingBreath = wantHold && !Exhausted && Breath > 0f;
            if (HoldingBreath)
            {
                Breath -= dt / BreathHoldTime;
                if (Breath <= 0f)
                {
                    Breath = 0f;
                    Exhausted = true;
                    Sfx.Play2D(SfxId.Hurt, 0.25f, 1.4f);
                }
            }
            else
            {
                Breath = Mathf.Min(1f, Breath + dt / 5f * (wantZoom ? 0.5f : 1f));
                if (Exhausted && Breath > 0.4f) Exhausted = false;
            }

            if (Draw >= 1f) fullDrawTime += dt; else fullDrawTime = 0f;
            float fatigue = Mathf.Clamp01((fullDrawTime - 3f) / 3f) * 0.5f;
            float amp = Zoom * (HoldingBreath ? 0.04f : (Exhausted ? 0.75f : 0.32f)) + fatigue;
            swayAmp = Mathf.Lerp(swayAmp, amp, 1f - Mathf.Exp(-6f * dt));
            swayT += dt;
            float bx = Mathf.Sin(swayT * 0.9f) * 0.6f + (Mathf.PerlinNoise(swayT * 0.5f, 3.3f) - 0.5f) * 1.2f;
            float by = Mathf.Sin(swayT * 1.7f) * 0.8f + (Mathf.PerlinNoise(7.1f, swayT * 0.5f) - 0.5f) * 1.0f;

            if (vr)
            {
                // a headset's field of view cannot be changed: the zoom trigger just steadies the shot (no sway in VR)
                player.FovOverride = 0f;
                player.AimOffset = Vector2.zero;
                player.LookScale = 1f;
            }
            else
            {
                float z = Mathf.SmoothStep(0f, 1f, Zoom);
                player.FovOverride = Zoom > 0.001f ? Mathf.Lerp(GameSettings.Fov, ZoomFov, z) : 0f;
                player.AimOffset = new Vector2(bx, by) * swayAmp;
                player.LookScale = Mathf.Lerp(1f, 0.4f, z);
            }
        }

        void UpdateShooting(PlayerInputState inp, float dt, bool vr)
        {
            nockCooldown -= dt;
            if (!inp.fireHeld) needRelease = false;
            bool canDraw = ArrowsLeft > 0 && nockCooldown <= 0f && !needRelease;
            if (canDraw && inp.fireHeld)
            {
                if (Draw <= 0f) Sfx.Play2D(SfxId.BowDraw, 0.55f);
                Draw = Mathf.Min(1f, Draw + dt / DrawTime);
                if (vr && Time.frameCount % 6 == 0) Haptics.PulseXR(XRNode.RightHand, 0.05f + Draw * 0.15f, 0.02f);
            }
            else if (Draw > 0f && !inp.fireHeld)
            {
                if (Draw >= 0.08f) Release(vr);
                else Draw = 0f;
            }
        }

        void Release(bool vr)
        {
            Camera cam = player.cam;
            Vector3 origin, dir;
            if (vr && bowRoot != null)
            {
                dir = bowRoot.forward;
                origin = bowRoot.position + dir * 0.1f;
            }
            else
            {
                dir = cam.transform.forward;
                origin = cam.transform.position + dir * 0.45f;
            }
            float speed = Mathf.Lerp(MinSpeed, MaxSpeed, Mathf.Pow(Draw, 0.85f));
            Arrow a = Arrow.Spawn(origin, dir * speed, this, FletchColor);
            a.Round = Round;
            a.Index = ArrowsPerRound - ArrowsLeft;
            ArrowsLeft--;
            Shots++;
            Sfx.Play2D(SfxId.BowRelease, 0.9f, Random.Range(0.95f, 1.05f));
            if (vr) Haptics.PulseXR(XRNode.RightHand, 0.6f, 0.08f);
            Draw = 0f;
            bowKick = 1f;
            nockCooldown = 0.45f;
        }

        /// <summary>Called by an arrow when it sticks somewhere (target != null) or is lost.</summary>
        public void OnArrowLanded(Arrow a, ArcheryTarget target, Vector3 point, int points, float normalizedRadius, bool stuck)
        {
            if (!Running) return;
            int r = Mathf.Clamp(a.Round, 0, Rounds - 1);
            int i = Mathf.Clamp(a.Index, 0, ArrowsPerRound - 1);
            landedThisRound++;
            LastHitTime = Time.time;
            if (points > 0 && target != null)
            {
                bool bull = points == 50;
                Streak++;
                Hits++;
                if (bull) Bullseyes++;
                BestStreak = Mathf.Max(BestStreak, Streak);
                float mult = StreakMultiplier * target.Bonus;
                int total = Mathf.RoundToInt(points * mult);
                Score += total;
                RoundScores[r] += total;
                ArrowScores[r, i] = total;
                target.Flash();
                string extra = "";
                if (target.Bonus > 1f) extra += "  " + Loc.T("MOVING") + " x" + target.Bonus.ToString("0.##");
                if (Streak > 1) extra += "  " + Loc.T("STREAK") + " " + Streak;
                LastHit = (bull ? Loc.T("BULLSEYE!") + "  " : "") + "+" + total + extra;
                LastHitColor = bull ? new Color(1f, 0.9f, 0.2f) : (points >= 8 ? new Color(1f, 0.4f, 0.3f) : Color.white);
                Sfx.Play2D(bull ? SfxId.Bullseye : SfxId.TargetHit, bull ? 0.8f : 0.55f, bull ? 1f : 0.9f + points * 0.02f);
                Sfx.Play3D(SfxId.ArrowThud, point, 1f);
                if (bull) FX.Burst(point - target.face.forward * 0.1f, new Color(1f, 0.85f, 0.2f), 30, 3f, 0.12f, 0.8f);
                if (player != null && player.VrOn) Haptics.PulseXR(XRNode.RightHand, bull ? 0.7f : 0.3f, 0.06f);
            }
            else
            {
                Streak = 0;
                ArrowScores[r, i] = 0;
                LastHit = target != null ? Loc.T("EDGE - NO SCORE") : Loc.T("MISS");
                LastHitColor = new Color(1f, 1f, 1f, 0.7f);
                if (stuck) Sfx.Play3D(SfxId.ArrowThud, point, 0.9f, 0.8f);
            }
        }

        // ===================================================================================== range

        void BuildRange()
        {
            if (range != null) Destroy(range);
            targets.Clear();
            flags.Clear();
            range = new GameObject("ArcheryRange");
            Transform a = range.transform;

            WorldBuilder.Box(a, "Floor", new Vector3(0f, -0.5f, 21f), new Vector3(36f, 1f, 64f), "Floor", true, 0f);

            // backstop wall with a hay berm, side walls with neon strips
            WorldBuilder.Box(a, "Backstop", new Vector3(0f, 5f, 51.2f), new Vector3(36f, 10f, 1.5f), "Wall", true, 0f);
            WorldBuilder.Box(a, "Berm", new Vector3(0f, 1.2f, 49.8f), new Vector3(34f, 2.4f, 1.2f), "Hay", true, 0f);
            WorldBuilder.Box(a, "StripBackHigh", new Vector3(0f, 9.6f, 50.42f), new Vector3(34f, 0.12f, 0.06f), "NeonMagenta", false, 0f);
            WorldBuilder.Box(a, "StripBackLow", new Vector3(0f, 2.6f, 50.42f), new Vector3(34f, 0.12f, 0.06f), "NeonCyan", false, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                WorldBuilder.Box(a, "SideWall", new Vector3(s * 17.5f, 4f, 21f), new Vector3(1f, 8f, 64f), "Wall", true, 0f);
                WorldBuilder.Box(a, "SideStripLow", new Vector3(s * 16.96f, 0.35f, 21f), new Vector3(0.06f, 0.12f, 64f), "NeonCyan", false, 0f);
                WorldBuilder.Box(a, "SideStripHigh", new Vector3(s * 16.96f, 7.4f, 21f), new Vector3(0.06f, 0.12f, 64f), "NeonMagenta", false, 0f);
            }

            // shooting booth: counter at the line, side walls, back wall and a roof keep the archer behind the line
            GameObject counter = WorldBuilder.Box(a, "Counter", new Vector3(0f, 0.5f, 0.4f), new Vector3(9f, 1f, 0.5f), "Cover", true, 0f);
            WorldBuilder.Trim(counter.transform, 1f, "NeonCyan");
            for (int s = -1; s <= 1; s += 2)
                WorldBuilder.Box(a, "BoothSide", new Vector3(s * 4.75f, 1.5f, -3f), new Vector3(0.5f, 3f, 7.5f), "Cover", true, 0f);
            WorldBuilder.Box(a, "BoothBack", new Vector3(0f, 1.5f, -6.75f), new Vector3(10f, 3f, 0.5f), "Wall", true, 0f);
            WorldBuilder.Box(a, "BoothRoof", new Vector3(0f, 3.15f, -2.8f), new Vector3(10f, 0.3f, 8.4f), "Cover", false, 0f);
            WorldBuilder.Box(a, "BoothRoofGlow", new Vector3(0f, 2.98f, 1.36f), new Vector3(10f, 0.06f, 0.06f), "NeonCyan", false, 0f);
            Util.Part(a, PrimitiveType.Cube, "ShootLine", new Vector3(0f, 0.012f, 0.9f), Vector3.zero, new Vector3(9f, 0.01f, 0.08f), "NeonYellow");
            // a quiver rack on the counter (decoration)
            for (int i = 0; i < 6; i++)
            {
                GameObject q = new GameObject("RackArrow");
                q.transform.SetParent(a, false);
                q.transform.localPosition = new Vector3(3.2f + i * 0.06f, 1.75f, 0.5f);
                q.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                Arrow.BuildMesh(q.transform, null, null, null);
            }

            // distance lines and signs
            float[] dist = { 10f, 20f, 30f, 40f };
            for (int i = 0; i < dist.Length; i++)
            {
                float d = dist[i];
                Util.Part(a, PrimitiveType.Cube, "Line" + d, new Vector3(0f, 0.012f, d), Vector3.zero, new Vector3(33f, 0.01f, 0.06f), "NeonMagenta");
                for (int s = -1; s <= 1; s += 2)
                {
                    WorldBuilder.Box(a, "SignPost", new Vector3(s * 15.6f, 0.9f, d), new Vector3(0.12f, 1.8f, 0.12f), "GunMetal", false, 0f);
                    WorldBuilder.Box(a, "SignBoard", new Vector3(s * 15.6f, 2.1f, d), new Vector3(1.8f, 0.9f, 0.08f), "Building", false, 0f);
                    ModeManager.Label(a, Mathf.RoundToInt(d) + " m", new Vector3(s * 15.6f, 2.1f, d - 0.06f), 0f, 0.09f, new Color(0.3f, 0.95f, 1f));
                }
            }

            // targets: static at each distance, pop-ups in between, two rail targets
            int n = 0;
            targets.Add(ArcheryTarget.Create(a, new Vector3(-6f, 0f, 10f), 0.50f, 10f, TargetMotion.Static, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(3f, 0f, 20f), 0.65f, 20f, TargetMotion.Static, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(-4f, 0f, 30f), 0.80f, 30f, TargetMotion.Static, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(1.5f, 0f, 40f), 1.00f, 40f, TargetMotion.Static, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(7f, 0f, 15f), 0.55f, 15f, TargetMotion.PopUp, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(-9f, 0f, 25f), 0.70f, 25f, TargetMotion.PopUp, n++));
            targets.Add(ArcheryTarget.Create(a, new Vector3(9f, 0f, 35f), 0.85f, 35f, TargetMotion.PopUp, n++));
            AddRail(a, 23f, 9f, 2.4f, 0.60f, n++);
            AddRail(a, 37f, 11f, 3.2f, 0.90f, n++);

            // wind flags along the walls: they point where the wind blows and stand out further when it is strong
            float[] fz = { 8f, 24f, 42f };
            for (int i = 0; i < fz.Length; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 pp = new Vector3(s * 14.2f, 0f, fz[i]);
                    Util.Part(a, PrimitiveType.Cylinder, "FlagPole", pp + new Vector3(0f, 2.5f, 0f), Vector3.zero, new Vector3(0.07f, 2.5f, 0.07f), "GunMetal");
                    GameObject pivot = new GameObject("Flag");
                    pivot.transform.SetParent(a, false);
                    pivot.transform.localPosition = pp + new Vector3(0f, 4.8f, 0f);
                    Util.Part(pivot.transform, PrimitiveType.Cube, "Cloth", new Vector3(0f, -0.25f, 0.55f), Vector3.zero, new Vector3(0.02f, 0.5f, 1.1f), "Canopy");
                    flags.Add(pivot.transform);
                }
            }

            // a city skyline around (and behind) the range, reusing the shooter's builder
            GameObject sky = new GameObject("SkylineAnchor");
            sky.transform.SetParent(a, false);
            sky.transform.localPosition = new Vector3(0f, 0f, 22f);
            WorldBuilder.BuildSkyline(sky.transform);

            // lights
            GameObject moon = new GameObject("Moonlight");
            moon.transform.SetParent(a, false);
            moon.transform.rotation = Quaternion.Euler(55f, 20f, 0f);
            Light dl = moon.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.6f, 0.65f, 1f, 1f);
            dl.intensity = 0.5f;
            dl.shadows = LightShadows.Soft;
            for (int i = 0; i < dist.Length; i++)
            {
                Color c = i % 2 == 0 ? new Color(0.25f, 0.9f, 1f) : new Color(1f, 0.3f, 0.8f);
                Util.AddLight(a, LightType.Point, c, 2.2f, 18f, new Vector3(0f, 6f, dist[i] - 1.5f)).name = "RangeLight" + i;
            }
            Util.AddLight(a, LightType.Point, new Color(1f, 0.8f, 0.55f), 1.3f, 8f, new Vector3(0f, 2.7f, -1.5f)).name = "BoothLight";

            // static batching: everything except the targets (they move, pop up and carry stuck arrows) and the flags
            List<Transform> moving = new List<Transform>();
            for (int i = 0; i < targets.Count; i++) if (targets[i] != null) moving.Add(targets[i].transform);
            moving.AddRange(flags);
            Perf.BatchStatic(range, moving.ToArray());
        }

        void AddRail(Transform a, float z, float half, float speed, float radius, int index)
        {
            WorldBuilder.Box(a, "Rail", new Vector3(0f, 0.06f, z), new Vector3(half * 2f + 2.5f, 0.12f, 0.3f), "GunMetal", false, 0f);
            WorldBuilder.Box(a, "RailGlow", new Vector3(0f, 0.125f, z - 0.16f), new Vector3(half * 2f + 2.5f, 0.03f, 0.03f), "NeonMagenta", false, 0f);
            ArcheryTarget t = ArcheryTarget.Create(a, new Vector3(0f, 0f, z), radius, z, TargetMotion.Rail, index);
            t.railHalf = half;
            t.railSpeed = speed;
            targets.Add(t);
        }

        void UpdateFlags()
        {
            WindSystem w = WindSystem.Instance;
            if (w == null) return;
            float speed = w.Speed;
            float droop = Mathf.Lerp(75f, 5f, Mathf.Clamp01(speed / 7f));
            for (int i = 0; i < flags.Count; i++)
            {
                if (flags[i] == null) continue;
                float flutter = Mathf.Sin(Time.time * (4f + speed) + i) * (4f + speed * 1.5f);
                flags[i].rotation = Quaternion.Euler(droop, w.Direction + flutter, 0f);
            }
        }

        // ===================================================================================== bow viewmodel

        void BuildBow()
        {
            if (bowRoot != null) Destroy(bowRoot.gameObject);
            upperJoints.Clear();
            lowerJoints.Clear();
            GameObject root = new GameObject("Bow");
            bowRoot = root.transform;

            Util.Part(bowRoot, PrimitiveType.Cube, "Riser", Vector3.zero, Vector3.zero, new Vector3(0.03f, 0.22f, 0.045f), "GunMetal");
            Util.Part(bowRoot, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.015f, 0.004f), Vector3.zero, new Vector3(0.038f, 0.085f, 0.05f), "GunGrip");
            Util.Part(bowRoot, PrimitiveType.Cube, "RiserGlow", new Vector3(0f, 0.01f, 0.024f), Vector3.zero, new Vector3(0.008f, 0.17f, 0.004f), "NeonCyan");
            Util.Part(bowRoot, PrimitiveType.Cube, "Rest", new Vector3(-0.02f, 0.035f, 0f), Vector3.zero, new Vector3(0.012f, 0.006f, 0.02f), "NeonWhite");
            upperTip = BuildLimb(true, upperJoints);
            lowerTip = BuildLimb(false, lowerJoints);

            GameObject s = new GameObject("String");
            s.transform.SetParent(bowRoot, false);
            bowString = s.AddComponent<LineRenderer>();
            bowString.useWorldSpace = false;
            bowString.positionCount = 3;
            bowString.startWidth = 0.0025f;
            bowString.endWidth = 0.0025f;
            bowString.sharedMaterial = Mats.FxTracer;
            bowString.startColor = new Color(0.85f, 0.95f, 1f, 0.95f);
            bowString.endColor = new Color(0.85f, 0.95f, 1f, 0.95f);
            bowString.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bowString.receiveShadows = false;

            GameObject arrow = new GameObject("NockedArrow");
            arrow.transform.SetParent(bowRoot, false);
            Arrow.BuildMesh(arrow.transform, null, null, null);
            nocked = arrow.transform;

            Util.NoShadows(root);
            AttachBow(true);
        }

        Transform BuildLimb(bool upper, List<Transform> joints)
        {
            float sign = upper ? 1f : -1f;
            Transform parent = bowRoot;
            Vector3 start = new Vector3(0f, 0.11f * sign, 0f);
            for (int i = 0; i < 3; i++)
            {
                GameObject j = new GameObject(upper ? "UpperJoint" : "LowerJoint");
                j.transform.SetParent(parent, false);
                j.transform.localPosition = i == 0 ? start : new Vector3(0f, 0.125f * sign, 0f);
                float w = Mathf.Lerp(0.026f, 0.014f, i / 2f);
                Util.Part(j.transform, PrimitiveType.Cube, "Limb", new Vector3(0f, 0.0625f * sign, 0f), Vector3.zero, new Vector3(w, 0.13f, 0.012f), "GunMetal");
                if (i == 1) Util.Part(j.transform, PrimitiveType.Cube, "LimbGlow", new Vector3(0f, 0.0625f * sign, 0.007f), Vector3.zero, new Vector3(0.005f, 0.1f, 0.003f), "NeonMagenta");
                joints.Add(j.transform);
                parent = j.transform;
            }
            GameObject tip = new GameObject("Tip");
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = new Vector3(0f, 0.125f * sign, 0f);
            Util.Part(tip.transform, PrimitiveType.Sphere, "TipCap", Vector3.zero, Vector3.zero, Vector3.one * 0.016f, "NeonCyan");
            return tip.transform;
        }

        void AttachBow(bool force)
        {
            if (bowRoot == null || player == null) return;
            bool vr = player.VrOn && player.vr != null && player.vr.RightHand != null;
            Transform want = vr ? player.vr.RightHand : player.cam.transform;
            if (!force && bowRoot.parent == want) return;
            bowRoot.SetParent(want, false);
            if (vr)
            {
                bowRoot.localPosition = new Vector3(0f, 0f, 0.03f);
                bowRoot.localRotation = Quaternion.identity;
                bowRoot.localScale = Vector3.one;
            }
        }

        void UpdateBow(float dt)
        {
            if (bowRoot == null || player == null) return;
            AttachBow(false);
            bool vr = player.VrOn;
            bowKick = Mathf.MoveTowards(bowKick, 0f, dt * 6f);

            if (!vr)
            {
                // desktop: a scaled-down bow held left of centre and canted; while zoomed it drops out of the
                // scope picture (the narrow FOV would otherwise magnify it over the target)
                bowRoot.localScale = Vector3.one * DesktopBowScale;
                Vector3 rest = new Vector3(-0.11f, -0.07f, 0.5f);
                Vector3 aim = new Vector3(-0.16f, -0.2f, 0.5f);
                Vector3 pos = Vector3.Lerp(rest, aim, Mathf.SmoothStep(0f, 1f, Zoom)) + player.BobOffset * 1.5f + new Vector3(0f, 0f, bowKick * 0.03f);
                bowRoot.localPosition = pos;
                bowRoot.localRotation = Quaternion.Euler(-bowKick * 2f, 0f, Mathf.Lerp(18f, 30f, Zoom));
            }

            // limbs bend towards the archer as the string is drawn
            float bend = 5f + Draw * 9f;
            for (int i = 0; i < upperJoints.Count; i++) upperJoints[i].localRotation = Quaternion.Euler(-bend, 0f, 0f);
            for (int i = 0; i < lowerJoints.Count; i++) lowerJoints[i].localRotation = Quaternion.Euler(bend, 0f, 0f);

            Vector3 ut = bowRoot.InverseTransformPoint(upperTip.position);
            Vector3 lt = bowRoot.InverseTransformPoint(lowerTip.position);
            float restZ = (ut.z + lt.z) * 0.5f;
            float pull = Draw * (vr ? VrDrawLength : DesktopDrawLength);
            Vector3 nock = new Vector3(-0.012f, 0.035f, restZ - pull);
            bowString.SetPosition(0, ut);
            bowString.SetPosition(1, nock);
            bowString.SetPosition(2, lt);

            bool showArrow = State == Phase.Shooting && ArrowsLeft > 0 && nockCooldown <= 0f;
            if (nocked.gameObject.activeSelf != showArrow) nocked.gameObject.SetActive(showArrow);
            if (showArrow)
            {
                nocked.localPosition = nock + new Vector3(0f, 0f, Arrow.Length);
                nocked.localRotation = Quaternion.identity;
            }
        }

        void UpdateWrist()
        {
            if (player == null || player.vr == null || !player.VrOn) return;
            WindSystem w = WindSystem.Instance;
            string wind = w != null ? w.Speed.ToString("0.0") + " m/s " + WindArrowText(w.RelativeTo(player.vr.HeadForwardFlat)) : "-";
            player.vr.WristOverride = Loc.T("ROUND") + " " + (Round + 1) + "/" + Rounds + "   " + Loc.T("ARROWS") + " " + ArrowsLeft +
                                      "\n" + Loc.T("SCORE") + " " + Score + "   x" + StreakMultiplier.ToString("0.0") +
                                      "\n" + Loc.T("WIND") + " " + wind +
                                      (State == Phase.RoundSummary ? "\n" + Loc.T("Trigger: next round") : "");
        }

        /// <summary>Text arrow for a relative wind angle (0 = blowing away from you).</summary>
        public static string WindArrowText(float rel)
        {
            float a = Mathf.Repeat(rel + 22.5f, 360f);
            string[] dirs = { "^", "/^", ">", "\\v", "v", "v/", "<", "^\\" };
            return dirs[Mathf.Clamp((int)(a / 45f), 0, 7)];
        }

        // ===================================================================================== results

        public override string ResultTitle { get { return Loc.T("ARCHERY COMPLETE"); } }

        public override string StatusLine
        {
            get
            {
                return Loc.T("Round") + " " + (Round + 1) + "/" + Rounds + "   " + Loc.T("Arrows") + " " + ArrowsLeft +
                       "   " + Loc.T("Score") + " " + Score + "   " + Loc.T("Best") + " " + ModeManager.Best(GameMode.Archery);
            }
        }

        public override void ResultRows(List<string> rows)
        {
            for (int r = 0; r < Rounds; r++)
            {
                rows.Add(Loc.T("Round") + " " + (r + 1) + "  (" + Loc.T("wind") + " " + RoundWindSpeed[r].ToString("0.0") + " m/s)");
                rows.Add(RoundScores[r].ToString());
            }
            rows.Add(Loc.T("Hits")); rows.Add(Hits + " / " + Shots);
            rows.Add(Loc.T("Bullseyes")); rows.Add(Bullseyes.ToString());
            rows.Add(Loc.T("Best streak")); rows.Add(BestStreak.ToString());
            rows.Add(Loc.T("Accuracy")); rows.Add((Shots > 0 ? Mathf.RoundToInt(100f * Hits / Shots) : 0) + "%");
            rows.Add(Loc.T("Best (all time)")); rows.Add(ModeManager.Best(GameMode.Archery).ToString("N0"));
        }
    }
}
