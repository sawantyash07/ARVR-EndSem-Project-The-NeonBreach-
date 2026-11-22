using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Desktop HUD and menus for Farm Sniper (IMGUI, like the rest of Neon Breach's UI): title screen with difficulty
    /// select, mission briefing, gameplay HUD (objective + progress, timer, score, accuracy, crop health, ammo, bolt /
    /// reload status, scope magnification, breath meter, compass, crosshair sized to the real shot spread, hit marker,
    /// kill feed, interaction prompt), sniper scope overlay, pause menu with settings, victory and game-over screens.
    /// In VR the same information is shown by FarmVRPanel in world space instead.
    /// </summary>
    public class FarmHUD : MonoBehaviour
    {
        public FarmGameManager game;

        [Header("Look")]
        public Color accent = new Color(1f, 0.80f, 0.32f, 1f);
        public Color panel = new Color(0.07f, 0.055f, 0.04f, 0.78f);
        public Color text = new Color(0.98f, 0.95f, 0.88f, 1f);
        public Color good = new Color(0.45f, 0.85f, 0.35f, 1f);
        public Color bad = new Color(0.95f, 0.30f, 0.22f, 1f);
        [Range(0.6f, 1.6f)] public float uiScale = 1f;
        public bool showCompass = true;
        public float hitMarkerTime = 0.28f;

        Texture2D white, scopeTex;
        GUIStyle sTitle, sBig, sLabel, sSmall, sButton, sCenter, sTiny, sRight, sHuge;
        bool showControls;
        float W, H;
        readonly PlayerInputState pad = new PlayerInputState();

        void Awake()
        {
            if (game == null) game = GetComponent<FarmGameManager>();
            white = FarmTextures.White();
            scopeTex = FarmTextures.ScopeOverlay(1024);
        }

        void OnDestroy()
        {
            if (white != null) Destroy(white);
            if (scopeTex != null) Destroy(scopeTex);
        }

        void Update()
        {
            if (game == null) return;
            // keyboard / gamepad shortcuts for the menus
            pad.Clear();
            GamepadInput.Read(0, pad);
            bool confirm = DesktopInput.Pressed(KeyCode.Return) || DesktopInput.Pressed(KeyCode.Space) || pad.jumpPressed;
            switch (game.State)
            {
                case FarmState.Title:
                    if (confirm) game.ShowBriefing();
                    if (DesktopInput.Pressed(KeyCode.Alpha1)) Pick(FarmDifficulty.Easy);
                    if (DesktopInput.Pressed(KeyCode.Alpha2)) Pick(FarmDifficulty.Normal);
                    if (DesktopInput.Pressed(KeyCode.Alpha3)) Pick(FarmDifficulty.Hard);
                    if (pad.weaponCycle != 0) Pick((FarmDifficulty)Mathf.Clamp((int)game.difficulty + pad.weaponCycle, 0, 2));
                    break;
                case FarmState.Briefing:
                    if (confirm) game.BeginMission();
                    break;
                case FarmState.Victory:
                case FarmState.GameOver:
                    if (confirm) game.Restart();
                    break;
            }
        }

        void Pick(FarmDifficulty d)
        {
            if (game.difficulty != d) FarmAudio.PlayUI(FarmSound.UiClick);
            game.SetDifficulty(d);
        }

        // ------------------------------------------------------------------ styles

        void Styles()
        {
            if (sLabel != null) return;
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 22, font = f, wordWrap = true, richText = true };
            sLabel.normal.textColor = text;
            sSmall = new GUIStyle(sLabel) { fontSize = 17 };
            sTiny = new GUIStyle(sLabel) { fontSize = 14 };
            sBig = new GUIStyle(sLabel) { fontSize = 34, fontStyle = FontStyle.Bold };
            sHuge = new GUIStyle(sLabel) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sTitle = new GUIStyle(sLabel) { fontSize = 78, fontStyle = FontStyle.Bold };
            sTitle.normal.textColor = accent;
            sCenter = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter };
            sRight = new GUIStyle(sLabel) { alignment = TextAnchor.UpperRight };
            sButton = new GUIStyle(GUI.skin.button) { fontSize = 22, font = f, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sButton.normal.background = MakeTex(new Color(0.20f, 0.15f, 0.09f, 0.92f));
            sButton.hover.background = MakeTex(new Color(0.42f, 0.30f, 0.12f, 0.95f));
            sButton.active.background = MakeTex(new Color(0.60f, 0.45f, 0.15f, 1f));
            sButton.normal.textColor = text;
            sButton.hover.textColor = Color.white;
            sButton.active.textColor = Color.white;
        }

        Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void Rect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Panel(Rect r) { Rect(r, panel); Rect(new Rect(r.x, r.y, r.width, 3f), new Color(accent.r, accent.g, accent.b, 0.85f)); }

        void Bar(Rect r, float k, Color fill)
        {
            Rect(r, new Color(0f, 0f, 0f, 0.55f));
            Rect(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(k), r.height - 4f), fill);
        }

        bool Button(Rect r, string label)
        {
            bool b = GUI.Button(r, label, sButton);
            if (b) FarmAudio.PlayUI(FarmSound.UiClick);
            return b;
        }

        void Shadowed(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = st.normal.textColor;
            st.normal.textColor = new Color(0f, 0f, 0f, 0.7f * c.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            st.normal.textColor = old;
        }

        static string Clock(float t)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, t));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ------------------------------------------------------------------ OnGUI

        void OnGUI()
        {
            if (game == null || game.VrActive) return;
            Styles();
            float s = Screen.height / 1080f * uiScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            W = Screen.width / s;
            H = Screen.height / s;

            switch (game.State)
            {
                case FarmState.Title: DrawTitle(); break;
                case FarmState.Briefing: DrawBriefing(); break;
                case FarmState.Playing: DrawGameplay(); break;
                case FarmState.Paused: DrawGameplay(); DrawPause(); break;
                case FarmState.Victory: DrawResults(true); break;
                case FarmState.GameOver: DrawResults(false); break;
            }
        }

        // ------------------------------------------------------------------ title

        void DrawTitle()
        {
            Rect(new Rect(0, 0, W * 0.52f, H), new Color(0.05f, 0.04f, 0.03f, 0.62f));
            float x = 80f, y = 90f;
            Shadowed(new Rect(x, y, 900, 100), "FARM SNIPER", sTitle, accent);
            Shadowed(new Rect(x + 4, y + 92, 900, 40), "Pest control at Willow Creek Farm", sBig, text);
            GUI.Label(new Rect(x + 4, y + 142, W * 0.42f, 80),
                "Rats are eating the harvest. Take the farmer's scoped rifle, find them in the corn, wheat and cabbage rows, and clear the farm before time runs out.", sSmall);

            y = 340f;
            GUI.Label(new Rect(x, y, 400, 30), "<b>DIFFICULTY</b>  <size=15>(1 / 2 / 3 or D-pad)</size>", sLabel);
            y += 40f;
            FarmDifficulty[] all = { FarmDifficulty.Easy, FarmDifficulty.Normal, FarmDifficulty.Hard };
            for (int i = 0; i < all.Length; i++)
            {
                DifficultyProfile p = game.ProfileFor(all[i]);
                bool sel = game.difficulty == all[i];
                Rect r = new Rect(x, y, 640, 84);
                Rect(r, sel ? new Color(accent.r * 0.45f, accent.g * 0.35f, 0.05f, 0.9f) : new Color(0.12f, 0.09f, 0.06f, 0.75f));
                if (sel) Rect(new Rect(r.x, r.y, 6, r.height), accent);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Pick(all[i]);
                GUI.Label(new Rect(r.x + 20, r.y + 6, 300, 32), "<b>" + p.displayName + "</b>", sLabel);
                GUI.Label(new Rect(r.x + 330, r.y + 8, 300, 30), p.ratsToEliminate + " rats · " + Clock(p.timeLimit) + " · best " + FarmGameManager.Best(all[i]), sTiny);
                GUI.Label(new Rect(r.x + 20, r.y + 38, 610, 46), p.description, sTiny);
                y += 94f;
            }

            y += 16f;
            if (Button(new Rect(x, y, 310, 60), "START MISSION")) game.ShowBriefing();
            if (Button(new Rect(x + 330, y, 310, 60), showControls ? "HIDE CONTROLS" : "CONTROLS")) showControls = !showControls;
            y += 72f;
            if (game.CanLoadMainGame && Button(new Rect(x, y, 310, 50), "BACK TO NEON BREACH")) game.LoadMainGame();
            if (Button(new Rect(x + 330, y, 310, 50), "QUIT")) game.Quit();
            GUI.Label(new Rect(x, H - 50, 800, 30), "Enter / Space / Gamepad A: start", sTiny);

            if (showControls) DrawControls(new Rect(W - 640, 90, 560, 720));
        }

        void DrawControls(Rect r)
        {
            Panel(r);
            string k = "<b>KEYBOARD + MOUSE</b>\n" +
                "Move  " + KeyBindings.KeyFor(GameAction.MoveForward) + KeyBindings.KeyFor(GameAction.MoveLeft) + KeyBindings.KeyFor(GameAction.MoveBack) + KeyBindings.KeyFor(GameAction.MoveRight) +
                "   ·   Look  mouse (360°)\n" +
                "Fire  " + KeyBindings.KeyFor(GameAction.Fire) + "   ·   Scope  hold " + KeyBindings.KeyFor(GameAction.Aim) + "   ·   Zoom  mouse wheel\n" +
                "Hold breath (scoped)  " + KeyBindings.KeyFor(GameAction.Sprint) + "   ·   Reload  " + KeyBindings.KeyFor(GameAction.Reload) + "\n" +
                "Sprint  " + KeyBindings.KeyFor(GameAction.Sprint) + "   ·   Crouch  " + KeyBindings.KeyFor(GameAction.Crouch) + "   ·   Jump  " + KeyBindings.KeyFor(GameAction.Jump) + "\n" +
                "Ammo crate  E   ·   Camera view  " + KeyBindings.KeyFor(GameAction.Melee) + "   ·   Pause  Esc\n\n" +
                "<b>GAMEPAD</b>\n" +
                "L-stick move · R-stick look · RT fire · LT scope · D-pad / Y zoom · X reload · L3 sprint / hold breath · B crouch · A jump · D-pad down ammo crate · R3 camera · Start pause\n\n" +
                "<b>VR CONTROLLERS</b>\n" +
                "Aim with the rifle in your right hand - the scope shows a live magnified view.\n" +
                "R-trigger fire · R-grip reload · B or L-trigger magnification · L-stick move · R-stick turn (snap / smooth from Neon Breach settings) · X / Y ammo crate · A jump · Menu pause\n\n" +
                "<b>TIPS</b>\n" +
                "Rats panic at gunshots and nearby impacts: they rear up, zig-zag and hide behind hay bales, crates and in the tall corn - or dive into a burrow. Crouch to sneak closer. Bullets drop and take time to arrive: lead running rats at long range.";
            GUI.Label(new Rect(r.x + 24, r.y + 20, r.width - 48, r.height - 40), k, sSmall);
        }

        // ------------------------------------------------------------------ briefing

        void DrawBriefing()
        {
            Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            Rect r = new Rect(W * 0.5f - 560, H * 0.5f - 380, 1120, 760);
            Panel(r);
            DifficultyProfile p = game.Profile;
            Shadowed(new Rect(r.x + 40, r.y + 28, 800, 60), "MISSION BRIEFING", sBig, accent);
            GUI.Label(new Rect(r.x + 820, r.y + 40, 260, 40), "<b>" + p.displayName + "</b>", sRight);
            string obj =
                "<b>OBJECTIVE</b>\n" +
                "•  Eliminate <b>" + p.ratsToEliminate + " rats</b> before the timer runs out (<b>" + Clock(p.timeLimit) + "</b>).\n" +
                (game.cropLossEndsMission ? "•  Keep the farm's crop health above <b>" + Mathf.RoundToInt(game.cropFailThreshold * 100f) + " %</b> - feeding rats destroy crops.\n" : "") +
                "•  You start with a 5-round magazine and " + (game.player != null && game.player.rifle != null ? game.player.rifle.startReserve.ToString() : "20") + " spare rounds. Restock at the ammo crates (watchtower, barn).\n\n" +
                "<b>INTEL</b>\n" +
                "•  Rats come out of burrows " + (p.maxSpawnTier == 0 ? "close to the farmhouse yard" : (p.maxSpawnTier == 1 ? "across the fields" : "everywhere - fields, barn, orchard, pond and the far fences")) + ".\n" +
                "•  They scurry in short bursts, feed in the crop rows, and bolt at any gunshot - zig-zagging to cover or underground.\n" +
                "•  Climb the watchtower for a view over the fields. Crouch to stay unnoticed.\n\n" +
                "<b>SCORING</b>\n" +
                "•  " + game.killScore + " per rat · +" + game.headshotBonus + " headshot · +" + game.movingTargetBonus + " moving target · +" + game.distanceBonusPerMetre.ToString("0.#") + " per metre beyond " + game.distanceBonusFrom.ToString("0") + " m\n" +
                "•  Hit streak multiplier up to +" + Mathf.RoundToInt(game.streakMax * 100f) + " % · end bonus for time left, crops saved and accuracy.";
            GUI.Label(new Rect(r.x + 40, r.y + 100, r.width - 80, 480), obj, sLabel);
            if (Button(new Rect(r.x + 40, r.yMax - 100, 340, 64), "BEGIN MISSION")) game.BeginMission();
            if (Button(new Rect(r.x + 400, r.yMax - 100, 240, 64), "BACK")) game.GoTitle();
            if (Button(new Rect(r.x + 660, r.yMax - 100, 240, 64), showControls ? "HIDE CONTROLS" : "CONTROLS")) showControls = !showControls;
            GUI.Label(new Rect(r.x + 40, r.yMax - 30, 900, 26), "Enter / Space / Gamepad A: begin  ·  Esc: back", sTiny);
            if (showControls) DrawControls(new Rect(W * 0.5f - 300, H * 0.5f - 360, 600, 720));
        }

        // ------------------------------------------------------------------ gameplay

        void DrawGameplay()
        {
            FarmPlayer p = game.player;
            SniperRifle rifle = p != null ? p.rifle : null;

            if (rifle != null) DrawScope(rifle);
            if (rifle != null && !rifle.Scoped && p.cam != null) DrawCrosshair(rifle, p.cam);
            DrawHitMarker(rifle != null && rifle.Scoped);

            // --- objective (top left)
            Rect o = new Rect(28, 26, 430, 178);
            Panel(o);
            GUI.Label(new Rect(o.x + 18, o.y + 12, 400, 30), "<b>ELIMINATE THE RATS</b>", sLabel);
            GUI.Label(new Rect(o.x + 18, o.y + 40, 120, 44), "<size=34><b>" + game.Kills + "</b></size><size=22> / " + game.Target + "</size>", sLabel);
            GUI.Label(new Rect(o.x + 190, o.y + 50, 220, 30), "remaining  <b>" + game.RemainingRats + "</b>", sSmall);
            Bar(new Rect(o.x + 18, o.y + 92, 394, 16), game.Target > 0 ? game.Kills / (float)game.Target : 0f, accent);
            float crop = game.CropHealth;
            Color cc = Color.Lerp(bad, good, Mathf.InverseLerp(game.cropFailThreshold, 1f, crop));
            GUI.Label(new Rect(o.x + 18, o.y + 116, 400, 26), "CROP HEALTH  <b>" + Mathf.RoundToInt(crop * 100f) + " %</b>" + (game.spawner != null ? "      rats in the open " + game.spawner.Alive : ""), sSmall);
            Bar(new Rect(o.x + 18, o.y + 148, 394, 14), crop, cc);
            if (game.cropLossEndsMission) Rect(new Rect(o.x + 20 + 390 * game.cropFailThreshold, o.y + 144, 2, 22), bad);

            // --- timer (top centre)
            float t = game.TimeLeft;
            bool warn = t <= game.warnTimeLeft;
            Color tc = warn ? Color.Lerp(bad, text, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : text;
            Rect tr = new Rect(W * 0.5f - 110, 18, 220, 70);
            Panel(tr);
            Shadowed(tr, Clock(t), sHuge, tc);
            if (showCompass && p != null) DrawCompass(new Rect(W * 0.5f - 260, 96, 520, 30), p.Yaw);
            if (game.StartCountdown > 0f)
                Shadowed(new Rect(0, H * 0.32f, W, 80), "GET READY  " + Mathf.CeilToInt(game.StartCountdown), sHuge, accent);

            // --- score (top right)
            Rect sr = new Rect(W - 330, 26, 302, 140);
            Panel(sr);
            GUI.Label(new Rect(sr.x + 18, sr.y + 10, 270, 30), "SCORE", sSmall);
            GUI.Label(new Rect(sr.x + 18, sr.y + 32, 270, 50), "<size=40><b>" + game.Score + "</b></size>", sLabel);
            GUI.Label(new Rect(sr.x + 18, sr.y + 84, 280, 26), "ACCURACY  <b>" + Mathf.RoundToInt(game.Accuracy * 100f) + " %</b>   (" + game.ShotsHit + "/" + game.ShotsFired + ")", sSmall);
            GUI.Label(new Rect(sr.x + 18, sr.y + 108, 280, 26), "STREAK  <b>x" + game.Streak + "</b>     HEADSHOTS  <b>" + game.Headshots + "</b>", sSmall);

            // --- kill feed (right)
            float fy = 190f;
            for (int i = game.Feed.Count - 1; i >= 0; i--)
            {
                FeedEntry e = game.Feed[i];
                float age = Time.unscaledTime - e.time;
                float a = Mathf.Clamp01(3.2f - age);
                Color c = e.color;
                c.a = a;
                Shadowed(new Rect(W - 640, fy, 610, 30), e.text, sRight, c);
                fy += 32f;
            }

            // --- ammo (bottom right)
            if (rifle != null) DrawAmmo(rifle, new Rect(W - 380, H - 168, 352, 140));

            // --- scope info (bottom centre)
            if (rifle != null && (rifle.Scoped || rifle.AdsAmount > 0.5f))
            {
                Rect zr = new Rect(W * 0.5f - 170, H - 120, 340, 92);
                Rect(zr, new Color(0f, 0f, 0f, 0.45f));
                string zooms = "";
                for (int i = 0; i < rifle.zoomLevels.Length; i++)
                    zooms += (Mathf.Approximately(rifle.zoomLevels[i], rifle.Zoom) ? "<color=#FFD050><b>" + rifle.zoomLevels[i] + "x</b></color>" : rifle.zoomLevels[i] + "x") + "   ";
                GUI.Label(new Rect(zr.x, zr.y + 6, zr.width, 30), zooms, sCenter);
                string breath = rifle.Exhausted ? "<color=#FF6050>OUT OF BREATH</color>" : (rifle.HoldingBreath ? "HOLDING BREATH" : "HOLD " + KeyBindings.KeyFor(GameAction.Sprint) + " TO STEADY");
                GUI.Label(new Rect(zr.x, zr.y + 36, zr.width, 26), "<size=16>" + breath + "</size>", sCenter);
                Bar(new Rect(zr.x + 40, zr.y + 66, zr.width - 80, 12), rifle.Breath, rifle.Exhausted ? bad : new Color(0.6f, 0.85f, 1f));
            }

            // --- prompt
            if (p != null && !string.IsNullOrEmpty(p.InteractPrompt))
                Shadowed(new Rect(0, H * 0.62f, W, 40), p.InteractPrompt, sCenter, accent);
            if (rifle != null && rifle.Mag == 0 && !rifle.Reloading)
                Shadowed(new Rect(0, H * 0.66f, W, 40), rifle.Reserve > 0 ? "PRESS " + KeyBindings.KeyFor(GameAction.Reload) + " TO RELOAD" : "OUT OF AMMO - FIND AN AMMO CRATE", sCenter, bad);

            GUI.Label(new Rect(28, H - 40, 600, 30), "Esc pause  ·  " + KeyBindings.KeyFor(GameAction.Melee) + " camera view  ·  " + game.Profile.displayName, sTiny);
        }

        void DrawAmmo(SniperRifle rifle, Rect r)
        {
            Panel(r);
            GUI.Label(new Rect(r.x + 18, r.y + 10, 300, 28), "<b>BOLT-ACTION .308</b>", sSmall);
            // magazine pips
            float px = r.x + 18;
            for (int i = 0; i < rifle.magazineSize; i++)
            {
                bool full = i < rifle.Mag;
                Rect cart = new Rect(px, r.y + 44, 14, 40);
                Rect(cart, full ? new Color(0.85f, 0.65f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.15f));
                Rect(new Rect(px + 3, r.y + 38, 8, 8), full ? new Color(0.75f, 0.55f, 0.35f, 1f) : new Color(1f, 1f, 1f, 0.1f));
                px += 22f;
            }
            GUI.Label(new Rect(r.x + 150, r.y + 38, 200, 50), "<size=38><b>" + rifle.Mag + "</b></size><size=20> / " + rifle.Reserve + "</size>", sLabel);
            string status;
            float k = 0f;
            if (rifle.Reloading) { status = "RELOADING"; k = rifle.ReloadProgress; }
            else if (rifle.Cycling) { status = "WORKING THE BOLT"; k = rifle.CycleProgress; }
            else status = rifle.Mag > 0 ? "READY" : (rifle.Reserve > 0 ? "EMPTY" : "NO AMMO");
            GUI.Label(new Rect(r.x + 18, r.y + 92, 320, 26), status, sTiny);
            if (k > 0f) Bar(new Rect(r.x + 18, r.y + 118, r.width - 36, 10), k, accent);
        }

        void DrawCompass(Rect r, float yaw)
        {
            Rect(r, new Color(0f, 0f, 0f, 0.35f));
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            float pxPerDeg = r.width / 180f;
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                float d = Mathf.DeltaAngle(yaw, a);
                if (Mathf.Abs(d) > 88f) continue;
                float x = r.center.x + d * pxPerDeg;
                bool major = i % 3 == 0;
                Rect(new Rect(x - 1, r.y + (major ? 2 : 14), 2, major ? 10 : 6), new Color(1f, 1f, 1f, 0.7f));
                if (major) GUI.Label(new Rect(x - 20, r.y + 8, 40, 24), "<size=15>" + names[i / 3] + "</size>", sCenter);
            }
            Rect(new Rect(r.center.x - 1, r.y - 4, 2, r.height + 4), accent);
        }

        void DrawCrosshair(SniperRifle rifle, Camera cam)
        {
            float fov = cam.fieldOfView;
            float px = Mathf.Tan(rifle.CurrentSpread * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * H * 0.5f;
            float gap = Mathf.Clamp(px, 4f, 220f);
            Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
            Color col = new Color(1f, 1f, 1f, 0.85f);
            float len = 12f;
            Rect(new Rect(c.x - 1, c.y - gap - len, 2, len), col);
            Rect(new Rect(c.x - 1, c.y + gap, 2, len), col);
            Rect(new Rect(c.x - gap - len, c.y - 1, len, 2), col);
            Rect(new Rect(c.x + gap, c.y - 1, len, 2), col);
            Rect(new Rect(c.x - 1.5f, c.y - 1.5f, 3, 3), new Color(1f, 0.9f, 0.6f, 0.9f));
        }

        void DrawHitMarker(bool scoped)
        {
            float age = Time.unscaledTime - game.LastHitTime;
            if (game.LastHitTime <= 0f || age > hitMarkerTime) return;
            float a = 1f - age / hitMarkerTime;
            Color c = game.LastHitHead ? new Color(1f, 0.8f, 0.25f, a) : new Color(1f, 1f, 1f, a);
            Vector2 ctr = new Vector2(W * 0.5f, H * 0.5f);
            float r0 = scoped ? 18f : 10f, r1 = r0 + 14f;
            Matrix4x4 m = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + 90f * i, ctr * (Screen.height / 1080f * uiScale));
                Rect(new Rect(ctr.x - 1.5f, ctr.y - r1, 3f, r1 - r0), c);
                GUI.matrix = m;
            }
        }

        void DrawScope(SniperRifle rifle)
        {
            float ads = rifle.AdsAmount;
            if (ads < 0.6f) return;
            float a = Mathf.InverseLerp(0.6f, 0.92f, ads);
            if (!rifle.Scoped)
            {
                // fade through dark while the eye moves to the scope
                Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, a * 0.85f));
                return;
            }
            float size = H * 1.02f;
            Rect sr = new Rect(W * 0.5f - size * 0.5f, H * 0.5f - size * 0.5f, size, size);
            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(sr, scopeTex, ScaleMode.StretchToFill, true);
            GUI.color = old;
            Rect(new Rect(0, 0, sr.x + 1, H), Color.black);
            Rect(new Rect(sr.xMax - 1, 0, W - sr.xMax + 1, H), Color.black);
            // range hint: distance to whatever is under the crosshair
            FarmPlayer p = game.player;
            if (p != null && p.cam != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(p.cam.transform.position, p.cam.transform.forward, out hit, rifle.maxRange, rifle.hitMask, QueryTriggerInteraction.Ignore))
                    GUI.Label(new Rect(W * 0.5f + size * 0.12f, H * 0.5f + 14, 200, 30), "<size=18>" + Mathf.RoundToInt(hit.distance) + " m</size>", sLabel);
            }
        }

        // ------------------------------------------------------------------ pause

        void DrawPause()
        {
            Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
            Rect r = new Rect(W * 0.5f - 300, H * 0.5f - 400, 600, 800);
            Panel(r);
            Shadowed(new Rect(r.x, r.y + 24, r.width, 60), "PAUSED", sHuge, accent);
            float y = r.y + 110;
            if (Button(new Rect(r.x + 80, y, 440, 58), "RESUME")) game.Resume();
            y += 70;
            if (Button(new Rect(r.x + 80, y, 440, 58), "RESTART MISSION")) game.Restart();
            y += 70;
            if (Button(new Rect(r.x + 80, y, 440, 58), showControls ? "HIDE CONTROLS" : "HOW TO PLAY")) showControls = !showControls;
            y += 84;

            GUI.Label(new Rect(r.x + 80, y, 440, 28), "Mouse sensitivity  " + GameSettings.Sensitivity.ToString("0.00"), sSmall);
            y += 30;
            float sens = GUI.HorizontalSlider(new Rect(r.x + 80, y, 440, 20), GameSettings.Sensitivity, 0.2f, 3f);
            if (!Mathf.Approximately(sens, GameSettings.Sensitivity)) { GameSettings.Sensitivity = sens; GameSettings.Save(); }
            y += 34;
            GUI.Label(new Rect(r.x + 80, y, 440, 28), "Master volume  " + Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + " %", sSmall);
            y += 30;
            float vol = GUI.HorizontalSlider(new Rect(r.x + 80, y, 440, 20), GameSettings.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(vol, GameSettings.MasterVolume)) { GameSettings.MasterVolume = vol; AudioListener.volume = vol; GameSettings.Save(); }
            y += 34;
            if (Button(new Rect(r.x + 80, y, 440, 40), "INVERT LOOK Y:  " + (GameSettings.InvertY ? "ON" : "OFF")))
            {
                GameSettings.InvertY = !GameSettings.InvertY;
                GameSettings.Save();
            }
            y += 56;

            if (Button(new Rect(r.x + 80, y, 440, 54), "MAIN MENU")) game.GoTitle();
            y += 64;
            if (game.CanLoadMainGame && Button(new Rect(r.x + 80, y, 440, 54), "BACK TO NEON BREACH")) game.LoadMainGame();
            y += 64;
            if (Button(new Rect(r.x + 80, y, 440, 54), "QUIT")) game.Quit();
            GUI.Label(new Rect(r.x, r.yMax - 34, r.width, 26), "<size=14>Esc / Start: resume</size>", sCenter);
            if (showControls) DrawControls(new Rect(r.xMax + 20, r.y, Mathf.Min(560, W - r.xMax - 40), 720));
        }

        // ------------------------------------------------------------------ results

        void DrawResults(bool won)
        {
            Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            Rect r = new Rect(W * 0.5f - 480, H * 0.5f - 420, 960, 840);
            Panel(r);
            Shadowed(new Rect(r.x, r.y + 26, r.width, 60), won ? "MISSION COMPLETE" : "MISSION FAILED", sHuge, won ? accent : bad);
            string sub;
            if (won) sub = "The farm is safe - " + game.Kills + " rats cleared on " + game.Profile.displayName + ".";
            else if (game.Reason == FailReason.CropsDestroyed) sub = "The rats destroyed too much of the harvest.";
            else if (game.Reason == FailReason.OutOfAmmo) sub = "Out of ammunition.";
            else sub = "Time ran out - " + game.RemainingRats + " rats still loose on the farm.";
            GUI.Label(new Rect(r.x, r.y + 92, r.width, 32), sub, sCenter);

            if (won)
            {
                // three diamond "stars"
                float scale = Screen.height / 1080f * uiScale;
                Matrix4x4 m = GUI.matrix;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 c = new Vector2(r.center.x + (i - 1) * 70f, r.y + 156f);
                    GUIUtility.RotateAroundPivot(45f, c * scale);
                    Rect(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), i < game.Stars ? accent : new Color(1f, 1f, 1f, 0.15f));
                    if (i >= game.Stars) Rect(new Rect(c.x - 16f, c.y - 16f, 32f, 32f), panel);
                    GUI.matrix = m;
                }
            }

            float y = r.y + 200, x0 = r.x + 120, x1 = r.x + 560;
            Row(ref y, x0, x1, "Rats eliminated", game.Kills + " / " + game.Target);
            Row(ref y, x0, x1, "Headshots", game.Headshots.ToString());
            Row(ref y, x0, x1, "Accuracy", Mathf.RoundToInt(game.Accuracy * 100f) + " %  (" + game.ShotsHit + " of " + game.ShotsFired + ")");
            Row(ref y, x0, x1, "Longest shot", Mathf.RoundToInt(game.LongestShot) + " m");
            Row(ref y, x0, x1, "Best streak", "x" + game.BestStreak);
            Row(ref y, x0, x1, "Crop health", Mathf.RoundToInt(game.CropHealth * 100f) + " %");
            Row(ref y, x0, x1, "Time", won ? Clock(game.TimeLeft) + " left" : Clock(game.Elapsed) + " played");
            if (won)
            {
                y += 6;
                Row(ref y, x0, x1, "Time bonus", "+" + game.TimeBonus);
                Row(ref y, x0, x1, "Crops saved bonus", "+" + game.CropBonus);
                Row(ref y, x0, x1, "Accuracy bonus", "+" + game.AccuracyBonus);
            }
            y += 10;
            Rect(new Rect(x0, y, x1 - x0 + 260, 2), new Color(accent.r, accent.g, accent.b, 0.6f));
            y += 12;
            GUI.Label(new Rect(x0, y, 400, 50), "<size=34><b>FINAL SCORE</b></size>", sLabel);
            GUI.Label(new Rect(x1, y, 300, 50), "<size=34><b>" + game.Score + "</b></size>", sLabel);
            y += 50;
            GUI.Label(new Rect(x0, y, 700, 30), game.NewBest ? "<color=#FFD050><b>NEW BEST SCORE!</b></color>" : "Best on " + game.Profile.displayName + ": " + FarmGameManager.Best(game.difficulty), sLabel);

            float by = r.yMax - 96;
            if (Button(new Rect(r.x + 60, by, 270, 62), won ? "PLAY AGAIN" : "RETRY")) game.Restart();
            if (won && game.difficulty != FarmDifficulty.Hard)
            {
                if (Button(new Rect(r.x + 345, by, 270, 62), "NEXT DIFFICULTY")) { game.SetDifficulty(game.difficulty + 1); game.ShowBriefing(); }
            }
            else if (Button(new Rect(r.x + 345, by, 270, 62), "CHANGE DIFFICULTY")) game.GoTitle();
            if (Button(new Rect(r.x + 630, by, 270, 62), "MAIN MENU")) game.GoTitle();
        }

        void Row(ref float y, float x0, float x1, string label, string value)
        {
            GUI.Label(new Rect(x0, y, 420, 30), label, sLabel);
            GUI.Label(new Rect(x1, y, 360, 30), "<b>" + value + "</b>", sLabel);
            y += 34;
        }
    }
}
