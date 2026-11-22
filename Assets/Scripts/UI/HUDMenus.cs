using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Menus: main menu with mission setup (mode, difficulty, arena, mixed reality), settings with tabs
    /// (gameplay, graphics, audio, controls with key rebinding, VR comfort, language), leaderboard + lifetime stats,
    /// controls reference and the pause menu.
    /// </summary>
    public partial class HUD
    {
        enum Panel { None, Settings, HowTo, Leaderboard, Modes }
        Panel panel = Panel.None;
        int settingsTab;
        int waitingBind = -1;
        bool confirmReset;

        static readonly string[] TabNames = { "GAMEPLAY", "GRAPHICS", "AUDIO", "CONTROLS", "VR", "LANGUAGE" };

        // ============================================================================ key rebinding capture

        void HandleRebindEvent()
        {
            if (waitingBind < 0) return;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                if (e.keyCode != KeyCode.Escape) KeyBindings.Set((GameAction)waitingBind, e.keyCode);
                waitingBind = -1;
                Sfx.Play2D(SfxId.Click, 0.7f);
                e.Use();
            }
            else if (e.type == EventType.MouseDown)
            {
                KeyBindings.Set((GameAction)waitingBind, KeyCode.Mouse0 + Mathf.Clamp(e.button, 0, 6));
                waitingBind = -1;
                Sfx.Play2D(SfxId.Click, 0.7f);
                e.Use();
            }
        }

        // ============================================================================ main menu

        void DrawMenu(GameManager gm)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.05f, 0.6f));
            float cx = W * 0.5f;
            Text(R(cx - 800f + 7f, 77f, 1600f, 150f), "NEON BREACH", 140, new Color(Magenta.r, Magenta.g, Magenta.b, 0.75f), TextAnchor.MiddleCenter);
            Text(R(cx - 800f, 70f, 1600f, 150f), "NEON BREACH", 140, Cyan, TextAnchor.MiddleCenter);
            Text(R(cx - 800f, 215f, 1600f, 40f), Loc.T("3D ARENA SHOOTER   |   AR / VR END-SEMESTER PROJECT"), 26, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, false);
            if (gm.HighScore > 0)
                Text(R(cx - 800f, 255f, 1600f, 36f), Loc.T("HIGH SCORE") + "  " + gm.HighScore.ToString("N0"), 24, Yellow, TextAnchor.MiddleCenter);

            if (panel != Panel.None) { DrawPanel(gm); return; }

            // left: the three game modes, then the other actions
            float bx = cx - 640f;
            string start = !GameSettings.TutorialDone && !GameSettings.CoOp ? Loc.T("SHOOTER (TUTORIAL FIRST)") : Loc.T("SHOOTER - START MISSION");
            if (Button(R(bx, 316f, 500f, 70f), start, true, 30)) gm.StartFromMenu();
            if (Button(R(bx, 394f, 500f, 60f), Loc.T("ARCHERY") + "   " + Loc.T("best") + " " + ModeManager.Best(GameMode.Archery).ToString("N0"), true, 28))
                gm.Modes.SwitchTo(GameMode.Archery);
            if (Button(R(bx, 462f, 500f, 60f), Loc.T("PARACHUTE") + "   " + Loc.T("best") + " " + ModeManager.Best(GameMode.Parachute).ToString("N0"), true, 28))
                gm.Modes.SwitchTo(GameMode.Parachute);
            if (RatShooterButton(R(bx, 530f, 500f, 60f), Loc.T("RAT SHOOTER") + "   " + Loc.T("best") + " " + RatShooter.RatShooterGame.BestScore.ToString("N0"), 28))
                LaunchRatShooter();
            if (Button(R(bx, 608f, 500f, 54f), Loc.T("TUTORIAL"), true, 26)) gm.StartTutorial();
            if (Button(R(bx, 670f, 500f, 54f), Loc.T("LEADERBOARD & STATS"), true, 26)) { panel = Panel.Leaderboard; confirmReset = false; }
            if (Button(R(bx, 732f, 500f, 54f), Loc.T("SETTINGS"), true, 26)) { panel = Panel.Settings; settingsTab = 0; }
            if (Button(R(bx, 794f, 500f, 54f), Loc.T("HOW TO PLAY"), true, 26)) panel = Panel.HowTo;
            if (Button(R(bx, 856f, 500f, 54f), Loc.T("QUIT"), true, 26)) gm.QuitGame();
            if (Time.unscaledTime - ratMissingTime < 5f)
                Text(R(bx - 100f, 914f, 700f, 30f), Loc.T("Rat Shooter scene missing - run Neon Breach > Rat Shooter > Build"), 18, Yellow, TextAnchor.MiddleCenter, false);
            else
                Text(R(bx, 914f, 500f, 30f), Loc.T("F5 shooter   F6 archery   F7 parachute"), 18, Dim, TextAnchor.MiddleCenter, false);

            // right: mission setup
            float px = cx - 90f;
            Rect pr = R(px, 330f, 730f, 470f);
            Fill(pr, new Color(0.01f, 0.03f, 0.07f, 0.85f));
            Border(pr, new Color(0.15f, 0.6f, 0.7f, 0.9f), 2f);
            Text(R(px, 340f, 730f, 44f), Loc.T("SHOOTER - MISSION SETUP"), 30, Cyan, TextAnchor.MiddleCenter);

            float sx = px + 25f, sw = 680f;
            int m = Selector(sx, 395f, sw, Loc.T("Mode"), GameSettings.CoOp ? Loc.T("CO-OP (split screen)") : Loc.T("SOLO"));
            if (m != 0) { GameSettings.CoOp = !GameSettings.CoOp; GameSettings.Save(); }
            int d = Selector(sx, 455f, sw, Loc.T("Difficulty"), DifficultyInfo.Name(GameSettings.Difficulty));
            if (d != 0) { GameSettings.Difficulty = (Difficulty)(((int)GameSettings.Difficulty + d + 3) % 3); GameSettings.Save(); }
            int a = Selector(sx, 515f, sw, Loc.T("Arena"), ArenaCatalog.Name(GameSettings.Arena));
            if (a != 0)
            {
                GameSettings.Arena = (GameSettings.Arena + a + ArenaCatalog.Count) % ArenaCatalog.Count;
                GameSettings.Save();
                ArenaManager.Prepare(GameSettings.Arena, false);   // show the arena behind the menu
            }
            Text(R(sx, 565f, sw, 30f), ArenaCatalog.Description(GameSettings.Arena), 20, Dim, TextAnchor.MiddleCenter, false);

            string diffInfo = GameSettings.Difficulty == Difficulty.Easy ? Loc.T("Weaker, fewer enemies - score x0.75")
                            : GameSettings.Difficulty == Difficulty.Hard ? Loc.T("Tougher, more accurate enemies - score x1.5")
                            : Loc.T("The intended experience - score x1");
            Text(R(sx, 598f, sw, 30f), diffInfo, 20, Dim, TextAnchor.MiddleCenter, false);

            if (gm.VrActive)
            {
                GameSettings.MixedReality = Toggle(R(sx, 640f, sw, 50f), Loc.T("MIXED REALITY PASSTHROUGH"), GameSettings.MixedReality);
            }
            else if (GameSettings.CoOp)
            {
                int pads = GamepadInput.Count;
                string info = pads > 0 ? string.Format(Loc.T("P1: keyboard + mouse   P2: {0}"), GamepadInput.DeviceName(0))
                                       : Loc.T("Player 2 needs a gamepad - connect one now");
                Text(R(sx, 640f, sw, 50f), info, 22, pads > 0 ? Green : Yellow, TextAnchor.MiddleCenter);
            }
            else
            {
                Text(R(sx, 640f, sw, 50f), GamepadInput.Count > 0 ? Loc.T("Gamepad detected - you can play with it too") : Loc.T("Keyboard + mouse  (gamepads supported)"), 22, Dim, TextAnchor.MiddleCenter, false);
            }

            Text(R(sx, 700f, 200f, 44f), Loc.T("Callsign"), 24, Color.white, TextAnchor.MiddleLeft, false);
            field.fontSize = Mathf.RoundToInt(24 * S);
            string nn = GUI.TextField(R(sx + 210f, 702f, 300f, 42f), GameSettings.PlayerName, 14, field);
            if (nn != GameSettings.PlayerName) { GameSettings.PlayerName = nn.ToUpperInvariant(); GameSettings.Save(); }
            Text(R(sx + 520f, 700f, 160f, 44f), "$ " + Loc.T("shop between waves"), 16, Dim, TextAnchor.MiddleRight, false);

            Text(R(cx - 800f, 1000f, 1600f, 30f), Loc.T("Survive endless waves.  Headshots score more.  Chain kills for a combo multiplier.  Spend credits on upgrades."), 22, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, false);
            if (VRRig.HeadsetPresent)
                Text(R(cx - 800f, 1035f, 1600f, 30f), Loc.T("VR headset detected - use the menu inside the headset"), 20, Green, TextAnchor.MiddleCenter, false);
        }

        // ============================================================================ rat shooter (separate scene)

        float ratMissingTime = -10f;

        /// <summary>Main-menu entry for the separate Rat Shooter scene: same neon button, warm gold accent with a pulsing glow and a NEW tag.</summary>
        bool RatShooterButton(Rect r, string text, int size)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            Color gold = new Color(1f, 0.78f, 0.16f, 1f);
            for (int i = 3; i >= 1; i--)
            {
                float g = i * 3f * S;
                Border(new Rect(r.x - g, r.y - g, r.width + g * 2f, r.height + g * 2f), new Color(gold.r, gold.g, gold.b, (0.07f + 0.1f * pulse) / i), 2f);
            }
            Fill(r, hover ? new Color(0.42f, 0.28f, 0.04f, 0.9f) : new Color(0.09f, 0.06f, 0.02f, 0.88f));
            Fill(new Rect(r.x, r.y, 7f * S, r.height), gold);
            Border(r, hover ? Color.white : new Color(gold.r, gold.g, gold.b, 0.75f + 0.25f * pulse), 2f);
            Text(r, text, size, hover ? Color.white : new Color(1f, 0.92f, 0.68f, 1f), TextAnchor.MiddleCenter);
            Rect badge = new Rect(r.xMax - 30f * S, r.y - 10f * S, 50f * S, 20f * S);
            Fill(badge, new Color(Magenta.r, Magenta.g, Magenta.b, 0.85f + 0.15f * pulse));
            Text(badge, Loc.T("NEW"), 14, Color.white, TextAnchor.MiddleCenter);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                Sfx.Play2D(SfxId.Click, 0.7f);
                return true;
            }
            return false;
        }

        void LaunchRatShooter()
        {
            if (RatShooter.SceneFader.Busy) return;
            if (!Application.CanStreamedLevelBeLoaded(RatShooter.RatShooterGame.SceneName))
            {
                ratMissingTime = Time.unscaledTime;
                Sfx.Play2D(SfxId.Deny, 0.7f);
                return;
            }
            GameSettings.Save();
            Haptics.StopAll();
            Time.timeScale = 1f;
            RatShooter.SceneFader.Load(RatShooter.RatShooterGame.SceneName);
        }

        // ============================================================================ pause

        void DrawPause(GameManager gm)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.02f, 0.05f, 0.7f));
            float cx = W * 0.5f;
            Text(R(cx - 600f, 110f, 1200f, 120f), Loc.T("PAUSED"), 100, Cyan, TextAnchor.MiddleCenter);
            if (panel != Panel.None) { DrawPanel(gm); return; }
            float bx = cx - 240f;
            MiniGame mini = gm.Modes != null ? gm.Modes.Active : null;
            if (Button(R(bx, 260f, 480f, 62f), Loc.T("RESUME"))) gm.Resume();
            if (Button(R(bx, 332f, 480f, 62f), Loc.T("HOW TO PLAY"))) panel = Panel.HowTo;
            if (Button(R(bx, 404f, 480f, 62f), Loc.T("SETTINGS"))) { panel = Panel.Settings; settingsTab = 0; }
            if (gm.InTutorial)
            {
                if (Button(R(bx, 476f, 480f, 62f), Loc.T("SKIP TUTORIAL"))) { gm.Resume(); gm.FinishTutorial(true); }
            }
            else if (Button(R(bx, 476f, 480f, 62f), Loc.T("RESTART"))) { gm.Resume(); if (mini != null) gm.Modes.Restart(); else gm.StartGame(); }
            if (Button(R(bx, 548f, 480f, 62f), Loc.T("SWITCH MODE"))) panel = Panel.Modes;
            if (Button(R(bx, 620f, 480f, 62f), Loc.T("MAIN MENU"))) { Time.timeScale = 1f; gm.EnterMenu(); }
            if (Button(R(bx, 692f, 480f, 62f), Loc.T("QUIT"))) gm.QuitGame();
            if (mini != null)
                Text(R(cx - 600f, 780f, 1200f, 40f), mini.Title + "   " + mini.StatusLine, 24, Dim, TextAnchor.MiddleCenter, false);
            else if (!gm.InTutorial)
                Text(R(cx - 600f, 780f, 1200f, 40f), Loc.T("Wave") + " " + Mathf.Max(1, gm.Waves.Wave) + "   " + Loc.T("Score") + " " + gm.Score.ToString("N0") +
                     "   $" + gm.Credits + "   " + DifficultyInfo.Name(GameSettings.Difficulty) + "   " + ArenaCatalog.Name(ArenaManager.CurrentId), 24, Dim, TextAnchor.MiddleCenter, false);
        }

        // ============================================================================ panels

        Rect PanelRect(float top, float height)
        {
            float cx = W * 0.5f;
            return R(cx - 680f, top, 1360f, height);
        }

        void DrawPanel(GameManager gm)
        {
            float top = gm.State == GameState.Paused ? 230f : 300f;
            Rect pr = PanelRect(top, 1060f - top);
            Fill(pr, new Color(0.01f, 0.03f, 0.07f, 0.94f));
            Border(pr, Cyan, 2f);
            float px = pr.x / S, py = pr.y / S;

            switch (panel)
            {
                case Panel.Settings: DrawSettings(gm, px, py, pr.height / S); break;
                case Panel.HowTo: DrawHowTo(px, py); break;
                case Panel.Leaderboard: DrawLeaderboard(gm, px, py); break;
                case Panel.Modes: DrawModePicker(gm, px, py); break;
            }
            if (Button(R(px + 560f, py + pr.height / S - 76f, 240f, 60f), Loc.T("BACK"))) CloseOverlays();
        }

        // ---------------------------------------------------------------------------- settings

        void DrawSettings(GameManager gm, float px, float py, float ph)
        {
            for (int i = 0; i < TabNames.Length; i++)
            {
                if (Button(R(px + 20f + i * 222f, py + 16f, 210f, 50f), Loc.T(TabNames[i]), true, 22, settingsTab == i))
                {
                    settingsTab = i;
                    waitingBind = -1;
                }
            }
            float x = px + 120f, y = py + 100f;
            switch (settingsTab)
            {
                case 0:
                    GameSettings.Sensitivity = Slider(R(x, y, 900f, 44f), Loc.T("Mouse sensitivity"), GameSettings.Sensitivity, 0.2f, 3f, "0.00");
                    GameSettings.Fov = Slider(R(x, y + 60f, 900f, 44f), Loc.T("Field of view"), GameSettings.Fov, 60f, 110f, "0");
                    GameSettings.ShakeIntensity = Slider(R(x, y + 120f, 900f, 44f), Loc.T("Screen shake"), GameSettings.ShakeIntensity, 0f, 1.5f, "0.00");
                    GameSettings.InvertY = Toggle(R(x, y + 190f, 540f, 54f), Loc.T("INVERT Y"), GameSettings.InvertY);
                    if (Button(R(x + 580f, y + 190f, 540f, 54f), GameSettings.TutorialDone ? Loc.T("REPLAY TUTORIAL ON NEXT START") : Loc.T("TUTORIAL WILL PLAY ON START"), true, 22, !GameSettings.TutorialDone))
                        GameSettings.TutorialDone = !GameSettings.TutorialDone;
                    Text(R(x, y + 270f, 1100f, 60f), Loc.T("Screen shake only affects the desktop camera - it is always off in VR."), 20, Dim, TextAnchor.MiddleLeft, false);
                    break;

                case 1:
                {
                    string[] q = QualitySettings.names;
                    int cur = QualitySettings.GetQualityLevel();
                    int d = Selector(x, y, 1000f, Loc.T("Graphics quality"), q.Length > 0 ? q[Mathf.Clamp(cur, 0, q.Length - 1)] : "-");
                    if (d != 0 && q.Length > 0) { GameSettings.Quality = (cur + d + q.Length) % q.Length; }
                    GameSettings.PostFx = Toggle(R(x, y + 70f, 480f, 54f), Loc.T("POST-PROCESSING"), GameSettings.PostFx);
                    GameSettings.VSync = Toggle(R(x + 520f, y + 70f, 480f, 54f), "VSYNC", GameSettings.VSync);
                    GameSettings.Bloom = Slider(R(x, y + 150f, 900f, 44f), Loc.T("Bloom intensity"), GameSettings.Bloom, 0f, 2f, "0.00");
                    GameSettings.ShowFps = Toggle(R(x, y + 215f, 480f, 54f), Loc.T("SHOW FPS"), GameSettings.ShowFps);
                    bool fs = Toggle(R(x + 520f, y + 215f, 480f, 54f), Loc.T("FULLSCREEN"), Screen.fullScreen);
                    if (fs != Screen.fullScreen) Screen.fullScreen = fs;
                    Text(R(x, y + 290f, 1100f, 60f), Loc.T("Bloom, colour grading, vignette and chromatic aberration run in the Built-in pipeline with a custom shader."), 20, Dim, TextAnchor.MiddleLeft, false);
                    break;
                }

                case 2:
                    GameSettings.MasterVolume = Slider(R(x, y, 900f, 44f), Loc.T("Master volume"), GameSettings.MasterVolume, 0f, 1f, "0.00");
                    GameSettings.MusicVolume = Slider(R(x, y + 60f, 900f, 44f), Loc.T("Music volume"), GameSettings.MusicVolume, 0f, 1f, "0.00");
                    bool h = Toggle(R(x, y + 130f, 700f, 54f), Loc.T("SPATIAL AUDIO (HRTF)"), GameSettings.Hrtf);
                    if (h != GameSettings.Hrtf) { GameSettings.Hrtf = h; Sfx.RefreshSpatial(); }
                    Text(R(x, y + 200f, 1100f, 90f), BinauralFilter.HasPlugin
                        ? Loc.T("A spatializer plugin is selected in the project - it is used for HRTF.")
                        : Loc.T("Built-in binaural renderer: interaural time + level difference and head-shadow filtering. Use headphones."),
                        20, Dim, TextAnchor.UpperLeft, false, true);
                    break;

                case 3:
                    DrawRebinding(x - 60f, y);
                    break;

                case 4:
                {
                    GameSettings.ComfortVignette = Toggle(R(x, y, 520f, 50f), Loc.T("COMFORT VIGNETTE"), GameSettings.ComfortVignette);
                    GameSettings.VignetteStrength = Slider(R(x + 560f, y + 4f, 600f, 44f), Loc.T("Strength"), GameSettings.VignetteStrength, 0.2f, 1f, "0.00");
                    if (Button(R(x, y + 64f, 520f, 50f), Loc.T("TURNING") + ":  " + (GameSettings.Turn == TurnMode.Snap ? Loc.T("SNAP") : Loc.T("SMOOTH")), true, 24))
                        GameSettings.Turn = GameSettings.Turn == TurnMode.Snap ? TurnMode.Smooth : TurnMode.Snap;
                    if (GameSettings.Turn == TurnMode.Snap)
                        GameSettings.SnapAngle = Mathf.Round(Slider(R(x + 560f, y + 68f, 600f, 44f), Loc.T("Snap angle"), GameSettings.SnapAngle, 15f, 90f, "0") / 15f) * 15f;
                    else
                        GameSettings.SmoothTurnSpeed = Slider(R(x + 560f, y + 68f, 600f, 44f), Loc.T("Turn speed"), GameSettings.SmoothTurnSpeed, 30f, 240f, "0");
                    GameSettings.Seated = Toggle(R(x, y + 128f, 520f, 50f), Loc.T("SEATED MODE"), GameSettings.Seated);
                    GameSettings.HandTracking = Toggle(R(x + 560f, y + 128f, 520f, 50f), Loc.T("HAND TRACKING"), GameSettings.HandTracking);
                    GameSettings.Haptics = Toggle(R(x, y + 192f, 520f, 50f), Loc.T("HAPTICS"), GameSettings.Haptics);
                    GameSettings.MixedReality = Toggle(R(x + 560f, y + 192f, 520f, 50f), Loc.T("MIXED REALITY"), GameSettings.MixedReality);
                    if (Button(R(x, y + 256f, 520f, 50f), Loc.T("RESET VR OPTIONS"), true, 22)) GameSettings.ResetVrComfort();
                    Text(R(x, y + 320f, 1100f, 120f),
                        Loc.T("Vignette narrows your view while moving to reduce motion sickness. Snap turning (with a short blink) is the most comfortable; smooth turning is more immersive. Seated mode raises the view to standing height and lets you crouch by pulling the right stick back."),
                        19, Dim, TextAnchor.UpperLeft, false, true);
                    break;
                }

                default:
                    for (int i = 0; i < Loc.LanguageCount; i++)
                    {
                        if (Button(R(x + 260f, y + i * 74f, 600f, 62f), Loc.LanguageName(i), true, 28, GameSettings.Language == i))
                            GameSettings.Language = i;
                    }
                    Text(R(x, y + 320f, 1100f, 40f), Loc.T("Menus, HUD and tutorial are translated."), 20, Dim, TextAnchor.MiddleCenter, false);
                    break;
            }
            GameSettings.Apply();
        }

        void DrawRebinding(float x, float y)
        {
            GameAction[] all = KeyBindings.All;
            int perCol = 8;
            for (int i = 0; i < all.Length; i++)
            {
                int col = i / perCol, row = i % perCol;
                float bx = x + col * 600f, by = y + row * 50f;
                Text(R(bx, by, 300f, 44f), KeyBindings.ActionName(all[i]), 22, Color.white, TextAnchor.MiddleLeft, false);
                bool waiting = waitingBind == (int)all[i];
                string key = waiting ? Loc.T("press a key...") : KeyBindings.KeyFor(all[i]);
                if (Button(R(bx + 300f, by + 2f, 240f, 42f), key, true, 22, waiting)) waitingBind = (int)all[i];
            }
            if (Button(R(x + 600f, y + 7 * 50f + 2f, 240f, 42f), Loc.T("RESET DEFAULTS"), true, 20)) { KeyBindings.ResetDefaults(); waitingBind = -1; }
            Text(R(x, y + 410f, 1200f, 30f), Loc.T("Click a key, then press the new key or mouse button (ESC cancels). Gamepad: see HOW TO PLAY."), 19, Dim, TextAnchor.MiddleLeft, false);
        }

        // ---------------------------------------------------------------------------- how to play

        void DrawHowTo(float px, float py)
        {
            Text(R(px, py + 10f, 1360f, 46f), Loc.T("HOW TO PLAY"), 34, Cyan, TextAnchor.MiddleCenter);
            float x = px + 40f, y = py + 64f;
            Text(R(x, y, 420f, 30f), Loc.T("KEYBOARD + MOUSE"), 22, Yellow, TextAnchor.MiddleLeft);
            for (int i = 0; i < KeyBindings.All.Length; i++)
            {
                GameAction a = KeyBindings.All[i];
                Text(R(x, y + 32f + i * 27f, 150f, 26f), KeyBindings.KeyFor(a), 18, Cyan, TextAnchor.MiddleLeft);
                Text(R(x + 150f, y + 32f + i * 27f, 260f, 26f), KeyBindings.ActionName(a), 18, Color.white, TextAnchor.MiddleLeft, false);
            }
            Text(R(x, y + 32f + 15 * 27f, 420f, 26f), "1/2/3, " + Loc.T("wheel: weapons   ESC pause   F1 help   F3 FPS   F12 screenshot"), 16, Dim, TextAnchor.MiddleLeft, false);
            Text(R(x, y + 560f, 1300f, 26f), Loc.T("F5/F6/F7: shooter / archery / parachute.   ARCHERY: hold fire to draw, release to shoot, RMB zoom, Shift hold breath.   PARACHUTE: Space jump/deploy, WASD steer, mouse lean, hold LMB to flare."), 16, Yellow, TextAnchor.MiddleLeft, false);

            float x2 = px + 470f;
            Text(R(x2, y, 420f, 30f), Loc.T("GAMEPAD"), 22, Yellow, TextAnchor.MiddleLeft);
            string[] pad =
            {
                "L-STICK", Loc.T("Move"), "R-STICK", Loc.T("Look"), "RT / LT", Loc.T("Fire / aim"), "A", Loc.T("Jump"),
                "B", Loc.T("Crouch / slide"), "X", Loc.T("Reload"), "Y, D-PAD", Loc.T("Switch weapon"), "LB", Loc.T("Dash"),
                "RB", Loc.T("Throw grenade"), "L3 / R3", Loc.T("Sprint / melee"), "D-PAD UP", Loc.T("Flashlight"),
                "D-PAD DOWN", Loc.T("Use medkit"), "START", Loc.T("Pause / leave shop")
            };
            for (int i = 0; i < pad.Length / 2; i++)
            {
                Text(R(x2, y + 32f + i * 27f, 160f, 26f), pad[i * 2], 18, Cyan, TextAnchor.MiddleLeft);
                Text(R(x2 + 160f, y + 32f + i * 27f, 260f, 26f), pad[i * 2 + 1], 18, Color.white, TextAnchor.MiddleLeft, false);
            }

            float x3 = px + 900f;
            Text(R(x3, y, 420f, 30f), Loc.T("VR CONTROLLERS"), 22, Yellow, TextAnchor.MiddleLeft);
            string vr = Loc.T("L-stick move, R-stick turn (pull back: crouch)\nR-trigger fire, R-grip reload\nA jump, B next weapon\nL-trigger grenade, L-grip melee\nX flashlight, Y medkit\nL-stick click sprint, R-stick click dash\nMenu button: pause");
            Text(R(x3, y + 34f, 420f, 200f), vr, 17, Color.white, TextAnchor.UpperLeft, false, true);
            Text(R(x3, y + 240f, 420f, 30f), Loc.T("HAND TRACKING"), 22, Yellow, TextAnchor.MiddleLeft);
            string hands = Loc.T("Right pinch: fire\nRight open palm (hold): reload\nLeft pinch: grenade\nLeft open palm (hold): medkit\nPinch also clicks VR menus");
            Text(R(x3, y + 274f, 420f, 160f), hands, 17, Color.white, TextAnchor.UpperLeft, false, true);
            Text(R(x3, y + 420f, 420f, 120f), Loc.T("Enemies: Grunt (melee), Gunner (bursts), Drone (flying), Sniper (green laser = incoming shot), Shield tank (flank it or use grenades), Overlord boss every 5th wave."), 16, Dim, TextAnchor.UpperLeft, false, true);
        }

        // ---------------------------------------------------------------------------- leaderboard + stats

        void DrawLeaderboard(GameManager gm, float px, float py)
        {
            Text(R(px, py + 10f, 1360f, 46f), Loc.T("LEADERBOARD - TOP 10"), 34, Cyan, TextAnchor.MiddleCenter);
            DrawBoardTable(px + 30f, py + 64f, 860f, -1);

            // lifetime stats
            LifetimeData d = LifetimeStats.Data;
            float x = px + 930f, y = py + 64f;
            Text(R(x, y, 400f, 34f), Loc.T("LIFETIME STATS"), 24, Yellow, TextAnchor.MiddleLeft);
            string[] rows =
            {
                Loc.T("Games played"), d.games.ToString(),
                Loc.T("Total kills"), d.kills.ToString("N0"),
                Loc.T("Headshots"), d.headshots.ToString("N0"),
                Loc.T("Accuracy"), Mathf.RoundToInt(LifetimeStats.Accuracy * 100f) + "%",
                Loc.T("Best wave"), d.bestWave.ToString(),
                Loc.T("Best score"), d.bestScore.ToString("N0"),
                Loc.T("Bosses defeated"), d.bossKills.ToString(),
                Loc.T("Credits spent"), d.creditsSpent.ToString("N0"),
                Loc.T("Time played"), Mathf.FloorToInt(d.timePlayed / 3600f) + "h " + Mathf.FloorToInt(d.timePlayed % 3600f / 60f) + "m",
            };
            for (int i = 0; i < rows.Length / 2; i++)
            {
                Text(R(x, y + 44f + i * 36f, 250f, 34f), rows[i * 2], 20, Color.white, TextAnchor.MiddleLeft, false);
                Text(R(x + 250f, y + 44f + i * 36f, 150f, 34f), rows[i * 2 + 1], 22, Cyan, TextAnchor.MiddleRight);
            }
            string label = confirmReset ? Loc.T("CLICK AGAIN TO ERASE") : Loc.T("RESET ALL STATS");
            if (Button(R(x, y + 390f, 400f, 50f), label, true, 20, confirmReset))
            {
                if (confirmReset)
                {
                    Leaderboard.Clear();
                    LifetimeStats.Reset();
                    for (int i = 0; i < ArenaCatalog.Count; i++) DeathMap.Clear(i);
                    DeathMap.Clear(100);
                    PlayerPrefs.DeleteKey("nb_high");
                    confirmReset = false;
                }
                else confirmReset = true;
            }
        }

        void DrawBoardTable(float x, float y, float w, int highlightRank)
        {
            List<RunRecord> runs = Leaderboard.Runs;
            string[] heads = { "#", Loc.T("NAME"), Loc.T("SCORE"), Loc.T("WAVE"), Loc.T("ACC"), Loc.T("HS"), Loc.T("DIFF"), Loc.T("ARENA") };
            float[] cols = { 0f, 50f, 290f, 430f, 510f, 590f, 660f, 770f };
            for (int c = 0; c < heads.Length; c++) Text(R(x + cols[c], y, 140f, 30f), heads[c], 18, Yellow, TextAnchor.MiddleLeft);
            if (runs.Count == 0)
                Text(R(x, y + 60f, w, 40f), Loc.T("No runs yet - be the first!"), 24, Dim, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < runs.Count && i < Leaderboard.Size; i++)
            {
                RunRecord r = runs[i];
                float ry = y + 36f + i * 38f;
                bool hl = i + 1 == highlightRank;
                Fill(R(x - 6f, ry, w, 34f), hl ? new Color(0.5f, 0.4f, 0f, 0.45f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0.02f));
                Color c = hl ? Yellow : Color.white;
                Text(R(x + cols[0], ry, 50f, 34f), (i + 1).ToString(), 20, c, TextAnchor.MiddleLeft);
                Text(R(x + cols[1], ry, 240f, 34f), r.name, 20, c, TextAnchor.MiddleLeft, false);
                Text(R(x + cols[2], ry, 140f, 34f), r.score.ToString("N0"), 20, Cyan, TextAnchor.MiddleLeft);
                Text(R(x + cols[3], ry, 80f, 34f), r.wave.ToString(), 20, c, TextAnchor.MiddleLeft, false);
                Text(R(x + cols[4], ry, 80f, 34f), Mathf.RoundToInt(r.accuracy * 100f) + "%", 20, c, TextAnchor.MiddleLeft, false);
                Text(R(x + cols[5], ry, 70f, 34f), r.headshots.ToString(), 20, c, TextAnchor.MiddleLeft, false);
                Text(R(x + cols[6], ry, 110f, 34f), DifficultyInfo.Name((Difficulty)Mathf.Clamp(r.difficulty, 0, 2)), 18, c, TextAnchor.MiddleLeft, false);
                Text(R(x + cols[7], ry, 140f, 34f), ArenaCatalog.Name(Mathf.Clamp(r.arena, 0, ArenaCatalog.Count - 1)) + (r.coop ? " (2P)" : ""), 18, c, TextAnchor.MiddleLeft, false);
            }
        }

        void DrawHelpOverlay()
        {
            Fill(R(W - 560f, 260f, 520f, 150f), new Color(0f, 0f, 0f, 0.6f));
            Text(R(W - 540f, 268f, 480f, 134f),
                 Loc.T("F1 hide help") + "\nF3 FPS    F4 stats    F12 " + Loc.T("screenshot") + "\nESC " + Loc.T("pause") + "    " + KeyBindings.KeyFor(GameAction.Grenade) + " " + Loc.T("grenade") + "    " +
                 KeyBindings.KeyFor(GameAction.Melee) + " " + Loc.T("melee") + "\n" + KeyBindings.KeyFor(GameAction.Medkit) + " " + Loc.T("medkit") + "    " +
                 KeyBindings.KeyFor(GameAction.Flashlight) + " " + Loc.T("flashlight") + "    " + KeyBindings.KeyFor(GameAction.Dash) + " " + Loc.T("dash") +
                 "\nF5 " + Loc.T("shooter") + "   F6 " + Loc.T("archery") + "   F7 " + Loc.T("parachute"),
                 20, Color.white, TextAnchor.UpperLeft, false);
        }
    }
}
