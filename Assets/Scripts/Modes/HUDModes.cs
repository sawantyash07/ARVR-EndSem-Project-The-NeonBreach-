using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// IMGUI for the mini-game modes (partial HUD class, same drawing helpers as the shooter HUD):
    /// Archery (crosshair with holdover dots, draw meter, breath bar, wind arrow, arrows/rounds, round summary),
    /// Parachute (altimeter, vertical speed, wind, mini-map arrow to the pad, ring counter, timer, prompts),
    /// the mode picker used by the main and pause menus, and the per-mode results screen.
    /// </summary>
    public partial class HUD
    {
        readonly List<string> modeRows = new List<string>();

        /// <summary>Draws the HUD of the running mini-game. Returns false in shooter mode (normal HUD is drawn).</summary>
        bool DrawModeHud(GameManager gm)
        {
            ModeManager mm = ModeManager.Instance;
            if (mm == null || mm.IsShooter || mm.Active == null || gm.Player == null) return false;
            FullScreenArea();
            if (mm.Current == GameMode.Archery) DrawArcheryHud(mm.Archery, gm.Player);
            else DrawParachuteHud(mm.Parachute, gm.Player);
            FullScreenArea();
            DrawToastsAndBanner(Time.unscaledTime, gm);
            return true;
        }

        // ============================================================================ shared pieces

        /// <summary>An arrow pointing at angleDeg (0 = up, clockwise) centred on c.</summary>
        void DrawPointer(Vector2 c, float angleDeg, float len, float width, Color col)
        {
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angleDeg, c);
            Fill(new Rect(c.x - width * 0.5f, c.y - len * 0.5f, width, len), col);
            GUI.matrix = m;
            GUIUtility.RotateAroundPivot(angleDeg - 35f, c);
            Fill(new Rect(c.x - width * 0.5f, c.y - len * 0.5f, width, len * 0.4f), col);
            GUI.matrix = m;
            GUIUtility.RotateAroundPivot(angleDeg + 35f, c);
            Fill(new Rect(c.x - width * 0.5f, c.y - len * 0.5f, width, len * 0.4f), col);
            GUI.matrix = m;
        }

        void DrawWindPanel(float x, float y, Vector3 viewForward)
        {
            WindSystem w = WindSystem.Instance;
            if (w == null) return;
            Rect pr = R(x, y, 230f, 150f);
            Fill(pr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(pr, new Color(0.15f, 0.8f, 0.9f, 0.7f), 2f);
            Text(R(x + 12f, y + 6f, 200f, 26f), Loc.T("WIND"), 20, Cyan, TextAnchor.MiddleLeft);
            float rel = w.RelativeTo(viewForward);
            Vector2 c = new Vector2((x + 70f) * S, (y + 88f) * S);
            Fill(new Rect(c.x - 44f * S, c.y - 44f * S, 88f * S, 88f * S), new Color(0f, 0f, 0f, 0.35f));
            float strength = Mathf.Clamp01(w.Speed / 8f);
            Color col = Color.Lerp(Green, Red, strength);
            if (w.Speed > 0.15f) DrawPointer(c, rel, (40f + 34f * strength) * S, 5f * S, col);
            else Text(new Rect(c.x - 40f * S, c.y - 14f * S, 80f * S, 28f * S), Loc.T("CALM"), 18, Dim, TextAnchor.MiddleCenter);
            Text(R(x + 120f, y + 50f, 105f, 44f), w.Speed.ToString("0.0"), 36, col, TextAnchor.MiddleLeft);
            Text(R(x + 120f, y + 92f, 105f, 26f), "m/s", 18, Dim, TextAnchor.MiddleLeft, false);
            if (w.Speed > 0.15f) Text(R(x + 120f, y + 116f, 105f, 26f), WindWords(rel), 16, Dim, TextAnchor.MiddleLeft, false);
        }

        static string WindWords(float rel)
        {
            float a = Mathf.Abs(rel);
            if (a < 30f) return Loc.T("tailwind");
            if (a > 150f) return Loc.T("headwind");
            return rel > 0f ? Loc.T("to the right") : Loc.T("to the left");
        }

        Vector3 ViewForward(PlayerController p)
        {
            return p.VrOn && p.vr != null ? p.vr.HeadForwardFlat : p.cam.transform.forward;
        }

        // ============================================================================ archery

        void DrawArcheryHud(ArcheryMode a, PlayerController p)
        {
            Camera cam = p.cam;
            Vector2 c = new Vector2(VW * 0.5f, VH * 0.5f);
            float now = Time.time;

            // --- crosshair / scope reticle (desktop: the camera is the aim; VR aims with the bow itself)
            if (!p.VrOn && a.State == ArcheryMode.Phase.Shooting)
            {
                float z = a.Zoom;
                Color cc = new Color(1f, 1f, 1f, 0.9f);
                float th = Mathf.Max(1.5f, 2.2f * S);
                if (z > 0.05f)
                {
                    Color line = new Color(0f, 0f, 0f, 0.55f * z);
                    Fill(new Rect(0f, c.y - th * 0.5f, VW, th), line);
                    Fill(new Rect(c.x - th * 0.5f, 0f, th, VH), line);
                    // holdover dots every degree below the centre (arrows drop!)
                    float half = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    for (int i = 1; i <= 4; i++)
                    {
                        float py = Mathf.Tan(i * Mathf.Deg2Rad) / half * VH * 0.5f;
                        float ds = (i == 2 ? 7f : 5f) * S;
                        Fill(new Rect(c.x - ds * 0.5f, c.y + py - ds * 0.5f, ds, ds), new Color(1f, 0.85f, 0.2f, z));
                        Text(new Rect(c.x + 10f * S, c.y + py - 12f * S, 60f * S, 24f * S), i.ToString(), 14, new Color(1f, 0.85f, 0.2f, z * 0.8f), TextAnchor.MiddleLeft);
                    }
                }
                float gap = Mathf.Lerp(9f, 4f, a.Draw) * S, len = 9f * S;
                Fill(new Rect(c.x - th / 2f, c.y - gap - len, th, len), cc);
                Fill(new Rect(c.x - th / 2f, c.y + gap, th, len), cc);
                Fill(new Rect(c.x - gap - len, c.y - th / 2f, len, th), cc);
                Fill(new Rect(c.x + gap, c.y - th / 2f, len, th), cc);
                Fill(new Rect(c.x - 1.5f * S, c.y - 1.5f * S, 3f * S, 3f * S), new Color(1f, 0.3f, 0.3f, 1f));
            }

            // --- draw meter
            if (a.State == ArcheryMode.Phase.Shooting && a.Draw > 0f)
            {
                Rect br = new Rect(c.x - 90f * S, c.y + 70f * S, 180f * S, 10f * S);
                Fill(br, new Color(0f, 0f, 0f, 0.6f));
                Color dc = a.Draw >= 1f ? Green : Color.Lerp(Yellow, Orange, a.Draw);
                Fill(new Rect(br.x, br.y, br.width * a.Draw, br.height), dc);
                Border(br, new Color(1f, 1f, 1f, 0.4f), 1f);
                string dl = a.Draw >= 1f ? Loc.T("FULL DRAW") : Loc.T("DRAW") + " " + Mathf.RoundToInt(a.Draw * 100f) + "%";
                Text(new Rect(c.x - 120f * S, c.y + 82f * S, 240f * S, 28f * S), dl, 18, dc, TextAnchor.MiddleCenter);
            }

            // --- breath (only meaningful while zoomed)
            if (!p.VrOn && (a.Zoom > 0.05f || a.Breath < 0.999f))
            {
                Rect bb = new Rect(c.x - 90f * S, c.y + 118f * S, 180f * S, 6f * S);
                Fill(bb, new Color(0f, 0f, 0f, 0.6f));
                bool flash = a.Exhausted && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
                Fill(new Rect(bb.x, bb.y, bb.width * a.Breath, bb.height), a.Exhausted ? (flash ? Red : Orange) : Cyan);
                string bl = a.HoldingBreath ? Loc.T("HOLDING BREATH") : (a.Exhausted ? Loc.T("OUT OF BREATH") : Loc.T("hold") + " " + KeyBindings.KeyFor(GameAction.Sprint) + " " + Loc.T("to steady"));
                Text(new Rect(c.x - 160f * S, c.y + 126f * S, 320f * S, 24f * S), bl, 16, a.Exhausted ? Orange : Dim, TextAnchor.MiddleCenter, false);
            }

            // --- last hit
            float age = now - a.LastHitTime;
            if (age < 1.8f && !string.IsNullOrEmpty(a.LastHit))
            {
                Color lc = a.LastHitColor;
                lc.a *= 1f - Mathf.Clamp01((age - 1.2f) / 0.6f);
                float rise = Mathf.Clamp01(age / 0.25f);
                Text(new Rect(c.x - 400f * S, c.y - (90f + rise * 20f) * S, 800f * S, 50f * S), a.LastHit, 36, lc, TextAnchor.MiddleCenter);
            }

            // --- top centre: round + arrows
            float cx = W * 0.5f;
            Text(R(cx - 300f, 16f, 600f, 44f), Loc.T("ROUND") + " " + (a.Round + 1) + " / " + ArcheryMode.Rounds, 36, Cyan, TextAnchor.MiddleCenter);
            float ax = cx - ArcheryMode.ArrowsPerRound * 15f;
            for (int i = 0; i < ArcheryMode.ArrowsPerRound; i++)
            {
                int sc = a.ArrowScores[a.Round, i];
                Color col = sc < 0 ? new Color(0.2f, 0.95f, 1f, 0.9f) : (sc == 0 ? new Color(1f, 1f, 1f, 0.2f) : (sc >= 50 ? Yellow : Orange));
                Rect ar = R(ax + i * 30f, 66f, 18f, 30f);
                Fill(ar, col);
                if (sc > 0) Text(R(ax + i * 30f - 6f, 96f, 30f, 20f), sc.ToString(), 13, col, TextAnchor.MiddleCenter);
            }
            Text(R(cx - 300f, 118f, 600f, 26f), a.ArrowsLeft + " " + Loc.T("arrows left"), 18, Dim, TextAnchor.MiddleCenter, false);

            // --- top right: score
            float rx = W - 50f;
            Text(R(rx - 460f, 20f, 460f, 50f), Loc.T("SCORE") + "  " + a.Score.ToString("N0"), 40, Color.white, TextAnchor.MiddleRight);
            Text(R(rx - 460f, 68f, 460f, 26f), Loc.T("BEST") + "  " + Mathf.Max(ModeManager.Best(GameMode.Archery), a.Score).ToString("N0"), 20, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight);
            if (a.Streak > 1)
                Text(R(rx - 460f, 96f, 460f, 34f), Loc.T("STREAK") + " " + a.Streak + "   +" + Mathf.RoundToInt((a.StreakMultiplier - 1f) * 100f) + "%", 26, Orange, TextAnchor.MiddleRight);

            // --- top left: wind
            DrawWindPanel(40f, 40f, ViewForward(p));

            // --- key hints for the first round
            if (a.Round == 0 && a.Shots < 3 && !p.VrOn)
            {
                string hint = string.Format(Loc.T("Hold {0} to draw, release to shoot   {1} zoom   {2} hold breath   ESC pause   F5/F6/F7 switch mode"),
                                            KeyBindings.KeyFor(GameAction.Fire), KeyBindings.KeyFor(GameAction.Aim), KeyBindings.KeyFor(GameAction.Sprint));
                Text(R(cx - 800f, 1000f, 1600f, 30f), hint, 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, false);
            }
            else if (p.VrOn && a.Round == 0 && a.Shots < 3)
                Text(R(cx - 800f, 1000f, 1600f, 30f), Loc.T("VR: hold the right trigger to draw, release to shoot - aim with the bow"), 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, false);

            if (a.State == ArcheryMode.Phase.RoundSummary) DrawRoundSummary(a);
        }

        void DrawRoundSummary(ArcheryMode a)
        {
            float cx = W * 0.5f;
            Rect pr = R(cx - 420f, 300f, 840f, 400f);
            Fill(pr, new Color(0.01f, 0.03f, 0.07f, 0.9f));
            Border(pr, Cyan, 2f);
            Text(R(cx - 420f, 316f, 840f, 50f), Loc.T("ROUND") + " " + (a.Round + 1) + " " + Loc.T("COMPLETE"), 40, Cyan, TextAnchor.MiddleCenter);
            for (int i = 0; i < ArcheryMode.ArrowsPerRound; i++)
            {
                int sc = Mathf.Max(0, a.ArrowScores[a.Round, i]);
                Rect cell = R(cx - 380f + i * 76f, 390f, 68f, 68f);
                Fill(cell, sc >= 50 ? new Color(0.5f, 0.4f, 0f, 0.8f) : (sc > 0 ? new Color(0.06f, 0.3f, 0.38f, 0.9f) : new Color(0f, 0f, 0f, 0.5f)));
                Border(cell, new Color(1f, 1f, 1f, 0.25f), 1.5f);
                Text(cell, sc > 0 ? sc.ToString() : "-", 26, sc >= 50 ? Yellow : Color.white, TextAnchor.MiddleCenter);
                Text(R(cx - 380f + i * 76f, 460f, 68f, 22f), (i + 1).ToString(), 14, Dim, TextAnchor.MiddleCenter, false);
            }
            Text(R(cx - 400f, 500f, 800f, 50f), Loc.T("Round score") + "  " + a.RoundScores[a.Round] + "      " + Loc.T("Total") + "  " + a.Score, 32, Color.white, TextAnchor.MiddleCenter);
            Text(R(cx - 400f, 550f, 800f, 34f), Loc.T("Best streak") + " " + a.BestStreak + "     " + Loc.T("Bullseyes") + " " + a.Bullseyes, 22, Dim, TextAnchor.MiddleCenter, false);
            bool last = a.Round + 1 >= ArcheryMode.Rounds;
            string next = (last ? Loc.T("FIRE / SPACE: final results") : Loc.T("FIRE / SPACE: next round") + "  (" + Loc.T("wind changes") + ")");
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
            if (a.SummaryAge > 1.2f) Text(R(cx - 400f, 620f, 800f, 40f), next, 26, new Color(1f, 0.9f, 0.3f, blink), TextAnchor.MiddleCenter);
        }

        // ============================================================================ parachute

        void DrawParachuteHud(ParachuteMode m, PlayerController p)
        {
            ParachuteController ctrl = m.Controller;
            if (ctrl == null) return;
            SkydiveSettings st = m.Settings;
            Camera cam = p.cam;
            float cx = W * 0.5f;
            float alt = p.transform.position.y;
            Vector3 v = ctrl.Velocity;
            ParachuteController.Phase ph = ctrl.State;
            bool falling = ph == ParachuteController.Phase.FreeFall;
            bool blink = Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.2f;

            // --- world markers: pad and next ring
            Vector3 padTop = m.PadCenter + Vector3.up * 1f;
            Vector3 padFlat = padTop - p.transform.position;
            padFlat.y = 0f;
            DrawWorldMarker(cam, padTop, Loc.T("PAD") + " " + Mathf.RoundToInt(padFlat.magnitude) + " m", Green);
            Ring next = m.NextRing;
            if (next != null && ph != ParachuteController.Phase.Plane && ctrl.State != ParachuteController.Phase.Landed)
                DrawWorldMarker(cam, next.transform.position, Loc.T("RING") + " " + (next.index + 1), Yellow);

            // --- altimeter (left)
            float ax = 40f, ay = 230f;
            Rect pr = R(ax, ay, 260f, 520f);
            Fill(pr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(pr, new Color(0.15f, 0.8f, 0.9f, 0.7f), 2f);
            Text(R(ax + 12f, ay + 6f, 240f, 26f), Loc.T("ALTIMETER"), 20, Cyan, TextAnchor.MiddleLeft);
            Color altCol = falling && ctrl.Agl < st.warningAltitude ? (blink ? Red : Orange) : (falling && ctrl.CanDeploy ? Yellow : Color.white);
            Text(R(ax + 12f, ay + 32f, 240f, 60f), Mathf.RoundToInt(alt).ToString("N0"), 52, altCol, TextAnchor.MiddleLeft);
            Text(R(ax + 190f, ay + 50f, 60f, 40f), "m", 22, Dim, TextAnchor.MiddleLeft, false);
            Text(R(ax + 12f, ay + 92f, 240f, 26f), Loc.T("AGL") + "  " + Mathf.RoundToInt(ctrl.Agl).ToString("N0") + " m", 20, Dim, TextAnchor.MiddleLeft, false);
            // tape: 0..exit altitude with the deploy window, the warning band and the auto-deploy line
            float top = Mathf.Max(1f, st.jumpAltitude);
            float tx = ax + 30f, ty = ay + 130f, th = 300f;
            Fill(R(tx, ty, 16f, th), new Color(0f, 0f, 0f, 0.6f));
            float yDeploy = ty + th * (1f - Mathf.Clamp01(st.deployAltitude / top));
            float yWarn = ty + th * (1f - Mathf.Clamp01(st.warningAltitude / top));
            float yAuto = ty + th * (1f - Mathf.Clamp01(st.autoDeployAltitude / top));
            Fill(R(tx, yDeploy, 16f, yWarn - yDeploy), new Color(0.2f, 1f, 0.3f, 0.35f));
            Fill(R(tx, yWarn, 16f, yAuto - yWarn), new Color(1f, 0.85f, 0.2f, 0.4f));
            Fill(R(tx, yAuto, 16f, ty + th - yAuto), new Color(1f, 0.2f, 0.2f, 0.45f));
            Text(R(tx + 22f, yDeploy - 12f, 170f, 24f), Mathf.RoundToInt(st.deployAltitude) + "  " + Loc.T("open"), 14, Green, TextAnchor.MiddleLeft, false);
            Text(R(tx + 22f, yWarn - 12f, 170f, 24f), Mathf.RoundToInt(st.warningAltitude) + "  " + Loc.T("pull now"), 14, Yellow, TextAnchor.MiddleLeft, false);
            if (st.autoDeploy && yAuto - yWarn > 14f)
                Text(R(tx + 22f, yAuto - 12f, 170f, 24f), Mathf.RoundToInt(st.autoDeployAltitude) + "  " + Loc.T("auto"), 14, Red, TextAnchor.MiddleLeft, false);
            float yMe = ty + th * (1f - Mathf.Clamp01(ctrl.Agl / top));
            Fill(R(tx - 8f, yMe - 3f, 32f, 6f), Color.white);
            // vertical / horizontal speed
            float vs = -v.y;
            Text(R(ax + 12f, ay + 440f, 240f, 34f), Loc.T("V/S") + "  " + (vs >= 0.5f ? "-" : (vs <= -0.5f ? "+" : "")) + Mathf.Abs(vs).ToString("0") + " m/s", 24, vs > 30f ? Orange : Color.white, TextAnchor.MiddleLeft);
            Text(R(ax + 12f, ay + 474f, 240f, 30f), Loc.T("GND") + "  " + new Vector3(v.x, 0f, v.z).magnitude.ToString("0") + " m/s", 20, Dim, TextAnchor.MiddleLeft, false);

            // --- wind (top left)
            DrawWindPanel(40f, 40f, ViewForward(p));

            // --- top centre: phase + timer
            string phase = ph == ParachuteController.Phase.Plane ? Loc.T("IN THE PLANE") : falling ? Loc.T("FREE FALL")
                         : ph == ParachuteController.Phase.Deploying ? Loc.T("DEPLOYING") : ph == ParachuteController.Phase.Canopy ? Loc.T("CANOPY") : Loc.T("LANDED");
            Text(R(cx - 300f, 16f, 600f, 44f), phase, 36, Cyan, TextAnchor.MiddleCenter);
            float t = m.TotalTime;
            string time = Mathf.FloorToInt(t / 60f).ToString("00") + ":" + (t % 60f).ToString("00.0");
            Text(R(cx - 300f, 60f, 600f, 36f), time + (m.PenaltyTime > 0f ? "   (+" + Mathf.RoundToInt(m.PenaltyTime) + "s)" : ""), 28, m.PenaltyTime > 0f ? Orange : Color.white, TextAnchor.MiddleCenter);

            // --- top right: score, rings, coins
            float rx = W - 50f;
            Text(R(rx - 460f, 20f, 460f, 50f), Loc.T("SCORE") + "  " + m.Score.ToString("N0"), 40, Color.white, TextAnchor.MiddleRight);
            Text(R(rx - 460f, 68f, 460f, 26f), Loc.T("BEST") + "  " + ModeManager.Best(GameMode.Parachute).ToString("N0"), 20, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight);
            Text(R(rx - 460f, 98f, 460f, 34f), Loc.T("RINGS") + "  " + m.RingsPassed + " / " + ParachuteMode.RingCount + (m.RingStreak > 1 ? "   x" + m.RingStreak : ""), 26, Yellow, TextAnchor.MiddleRight);
            Text(R(rx - 460f, 132f, 460f, 30f), Loc.T("COINS") + "  " + m.CoinsCollected + " / " + m.CoinsTotal, 22, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight);

            // --- right: speed indicator + speed control, parachute status
            DrawSpeedPanel(W - 300f, 230f, ctrl, st, p);
            DrawChutePanel(W - 300f, 545f, m, ctrl, st, blink);

            // --- mini-map compass (bottom right): arrow to the pad, dot for the next ring
            DrawPadCompass(m, p, ctrl);

            // --- centre prompt (the deploy window shows the OPEN PARACHUTE button instead)
            string prompt = ctrl.CanDeploy ? "" : m.DeployPrompt;
            if (!string.IsNullOrEmpty(prompt))
            {
                Color pc = Color.white;
                string key = SkydiveSettings.KeyName(st.jumpInput, p.VrOn);
                if (ph == ParachuteController.Phase.Plane) { pc = m.GreenLight ? Green : Yellow; if (m.GreenLight || m.PlaneToJump >= 0f) prompt += "   [" + key + "]"; }
                else if (falling) pc = Dim;
                else if (ph == ParachuteController.Phase.Canopy)
                {
                    pc = blink ? Yellow : Orange;
                    prompt += "   " + Loc.T("hold") + " [" + SkydiveSettings.KeyName(st.flareInput, p.VrOn) + "]";
                }
                Text(R(cx - 700f, 700f, 1400f, 60f), prompt, 42, pc, TextAnchor.MiddleCenter);
            }
            if (ctrl.CanDeploy) DrawOpenParachuteButton(cx, ctrl, st, p.VrOn, blink);

            // --- canopy: flare energy
            if (ph == ParachuteController.Phase.Canopy)
            {
                Rect fb = R(cx - 120f, 770f, 240f, 10f);
                Fill(fb, new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(fb.x, fb.y, fb.width * ctrl.FlareEnergy, fb.height), ctrl.Flaring ? Green : Cyan);
                Text(R(cx - 120f, 782f, 240f, 24f), ctrl.Flaring ? Loc.T("FLARING") : Loc.T("FLARE"), 16, Dim, TextAnchor.MiddleCenter, false);
            }

            // --- key hints
            string up = SkydiveSettings.KeyName(st.speedUpInput, p.VrOn), down = SkydiveSettings.KeyName(st.slowDownInput, p.VrOn);
            string camKey = SkydiveSettings.KeyName(st.cameraToggleInput, false);
            if (!p.VrOn)
            {
                string hint = "";
                string fire = SkydiveSettings.KeyName(st.flareInput, false), jump = SkydiveSettings.KeyName(st.jumpInput, false);
                string wasd = KeyBindings.KeyFor(GameAction.MoveForward) + KeyBindings.KeyFor(GameAction.MoveLeft) + KeyBindings.KeyFor(GameAction.MoveBack) + KeyBindings.KeyFor(GameAction.MoveRight);
                if (ph == ParachuteController.Phase.Plane) hint = string.Format(Loc.T("Mouse: look around   {0}: jump   {1}: camera"), jump, camKey);
                else if (falling) hint = string.Format(Loc.T("{0}: move   mouse: turn + lean   hold {1}: dive faster   hold {2}: slow down   {3}: open parachute   {4}: camera"),
                                                       wasd, up, down, SkydiveSettings.KeyName(st.deployInput, false), camKey);
                else if (ph == ParachuteController.Phase.Canopy) hint = string.Format(Loc.T("A/D: steer   W or {0}: faster   S or {1}: brakes   hold {2}: flare   mouse: look   {3}: camera"), up, down, fire, camKey);
                else if (ph == ParachuteController.Phase.Landed) hint = string.Format(Loc.T("mouse: look around   {0}: camera"), camKey);
                if (hint.Length > 0) Text(R(cx - 900f, 1030f, 1800f, 30f), hint, 20, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, false);
            }
            else
            {
                string vh = ph == ParachuteController.Phase.Plane ? Loc.T("A: jump")
                          : falling ? string.Format(Loc.T("L-stick or head tilt: steer   look down: dive   {0}: faster   {1}: slower   A or R-grip: open parachute"), up, down)
                          : ph == ParachuteController.Phase.Canopy ? string.Format(Loc.T("L-stick or head tilt: steer   {0}: faster   {1}: brakes   R-trigger: flare"), up, down) : "";
                if (vh.Length > 0) Text(R(cx - 900f, 1030f, 1800f, 30f), vh, 20, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, false);
            }
        }

        /// <summary>Total speed (km/h), fall rate against terminal velocity, and the SLOW .. FAST speed control.</summary>
        void DrawSpeedPanel(float x, float y, ParachuteController ctrl, SkydiveSettings st, PlayerController p)
        {
            bool vr = p.VrOn;
            Rect pr = R(x, y, 260f, 300f);
            Fill(pr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(pr, new Color(0.15f, 0.8f, 0.9f, 0.7f), 2f);
            Text(R(x + 12f, y + 6f, 240f, 26f), Loc.T("SPEED"), 20, Cyan, TextAnchor.MiddleLeft);
            Vector3 v = ctrl.Velocity;
            float kmh = v.magnitude * 3.6f;
            ParachuteController.Phase ph = ctrl.State;
            bool falling = ph == ParachuteController.Phase.FreeFall;
            Color sc = kmh > 250f ? Orange : Color.white;
            Text(R(x + 12f, y + 30f, 170f, 60f), Mathf.RoundToInt(kmh).ToString(), 52, sc, TextAnchor.MiddleLeft);
            Text(R(x + 170f, y + 48f, 80f, 40f), "km/h", 20, Dim, TextAnchor.MiddleLeft, false);

            // fall-rate bar: 0 .. the fastest terminal velocity; tick = terminal velocity of the current position
            float fall = Mathf.Max(0f, -v.y);
            float maxFall = Mathf.Max(10f, st.diveTerminalVelocity * 1.05f);
            Rect bar = R(x + 14f, y + 100f, 232f, 14f);
            Fill(bar, new Color(0f, 0f, 0f, 0.6f));
            bool under = ph == ParachuteController.Phase.Canopy || ph == ParachuteController.Phase.Deploying;
            if (under) Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(st.safeVerticalSpeed / maxFall), bar.height), new Color(0.2f, 1f, 0.3f, 0.25f));
            Color fc = under ? (fall > st.safeVerticalSpeed ? Orange : Green) : Color.Lerp(Cyan, Orange, Mathf.InverseLerp(st.spreadTerminalVelocity, st.diveTerminalVelocity, fall));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fall / maxFall), bar.height), fc);
            if (falling)
            {
                float tv = Mathf.Clamp01(ctrl.TerminalVelocity / maxFall);
                Fill(new Rect(bar.x + bar.width * tv - 1.5f * S, bar.y - 4f * S, 3f * S, bar.height + 8f * S), Color.white);
            }
            Border(bar, new Color(1f, 1f, 1f, 0.3f), 1f);
            string fallLine = Loc.T("FALL") + "  " + fall.ToString("0") + " m/s";
            if (falling) fallLine += "   " + Loc.T("max") + " " + ctrl.TerminalVelocity.ToString("0");
            Text(R(x + 12f, y + 118f, 240f, 26f), fallLine, 18, Dim, TextAnchor.MiddleLeft, false);

            // speed control: -1 slow .. +1 fast (under canopy W / S count too)
            bool active = falling || ph == ParachuteController.Phase.Canopy;
            Text(R(x + 12f, y + 152f, 240f, 24f), Loc.T("SPEED CONTROL"), 16, active ? Cyan : Dim, TextAnchor.MiddleLeft);
            Rect sb = R(x + 14f, y + 182f, 232f, 16f);
            Fill(sb, new Color(0f, 0f, 0f, 0.6f));
            float ctl = ctrl.SpeedControl;
            if (ph == ParachuteController.Phase.Canopy) ctl = Mathf.Clamp(ctl + p.Inp.move.y, -1f, 1f);
            float mid = sb.x + sb.width * 0.5f;
            Color cc = ctl > 0f ? Orange : Cyan;
            if (ctl >= 0f) Fill(new Rect(mid, sb.y, sb.width * 0.5f * ctl, sb.height), cc);
            else Fill(new Rect(mid + sb.width * 0.5f * ctl, sb.y, -sb.width * 0.5f * ctl, sb.height), cc);
            Fill(new Rect(mid - 1f * S, sb.y - 3f * S, 2f * S, sb.height + 6f * S), Color.white);
            Border(sb, new Color(1f, 1f, 1f, 0.3f), 1f);
            Text(R(x + 12f, y + 200f, 120f, 22f), Loc.T("SLOW") + " [" + SkydiveSettings.KeyName(st.slowDownInput, vr) + "]", 13, Dim, TextAnchor.MiddleLeft, false);
            Text(R(x + 128f, y + 200f, 120f, 22f), "[" + SkydiveSettings.KeyName(st.speedUpInput, vr) + "] " + Loc.T("FAST"), 13, Dim, TextAnchor.MiddleRight, false);
            Text(R(x + 12f, y + 236f, 240f, 50f), ctrl.BodyPosition, 22, active ? Color.white : Dim, TextAnchor.MiddleCenter, true, true);
        }

        /// <summary>Parachute status: PACKED -> READY / PULL! -> DEPLOYING % -> OPEN (flare energy) -> LANDED.</summary>
        void DrawChutePanel(float x, float y, ParachuteMode m, ParachuteController ctrl, SkydiveSettings st, bool blink)
        {
            Rect pr = R(x, y, 260f, 170f);
            Fill(pr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(pr, new Color(0.15f, 0.8f, 0.9f, 0.7f), 2f);
            Text(R(x + 12f, y + 6f, 240f, 26f), Loc.T("PARACHUTE"), 20, Cyan, TextAnchor.MiddleLeft);
            string status, sub = "";
            Color col;
            float bar = -1f;
            switch (ctrl.State)
            {
                case ParachuteController.Phase.Plane:
                    status = Loc.T("PACKED"); col = Dim;
                    sub = Loc.T("opens below") + " " + Mathf.RoundToInt(st.deployAltitude) + " m";
                    break;
                case ParachuteController.Phase.FreeFall:
                    if (!ctrl.CanDeploy)
                    {
                        status = Loc.T("PACKED"); col = Dim;
                        sub = Loc.T("ready in") + " " + Mathf.RoundToInt(ctrl.Agl - st.deployAltitude) + " m";
                    }
                    else if (ctrl.Agl <= st.warningAltitude)
                    {
                        status = Loc.T("PULL!"); col = blink ? Red : Orange;
                        sub = st.autoDeploy ? Loc.T("auto-deploy at") + " " + Mathf.RoundToInt(st.autoDeployAltitude) + " m" : Loc.T("no automatic opening!");
                    }
                    else
                    {
                        status = Loc.T("READY"); col = Green;
                        sub = Loc.T("pull before") + " " + Mathf.RoundToInt(st.warningAltitude) + " m";
                    }
                    break;
                case ParachuteController.Phase.Deploying:
                    status = Loc.T("DEPLOYING") + " " + Mathf.RoundToInt(ctrl.DeployProgress * 100f) + "%"; col = Yellow;
                    bar = ctrl.DeployProgress;
                    break;
                case ParachuteController.Phase.Canopy:
                    status = ctrl.Flaring ? Loc.T("FLARING") : Loc.T("OPEN"); col = ctrl.Flaring ? Green : Cyan;
                    sub = Loc.T("flare") + " " + Mathf.RoundToInt(ctrl.FlareEnergy * 100f) + "%";
                    bar = ctrl.FlareEnergy;
                    break;
                default:
                    status = m.Crashed ? Loc.T("FAILED") : Loc.T("LANDED"); col = m.Crashed ? Red : Green;
                    sub = m.Crashed ? "" : Loc.T("canopy collapsed");
                    break;
            }
            Text(R(x + 12f, y + 38f, 240f, 50f), status, 34, col, TextAnchor.MiddleCenter);
            if (bar >= 0f)
            {
                Rect b = R(x + 20f, y + 96f, 220f, 12f);
                Fill(b, new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(b.x, b.y, b.width * Mathf.Clamp01(bar), b.height), col);
                Border(b, new Color(1f, 1f, 1f, 0.3f), 1f);
            }
            if (sub.Length > 0) Text(R(x + 12f, y + 120f, 240f, 32f), sub, 17, Dim, TextAnchor.MiddleCenter, false);
        }

        /// <summary>The interactive OPEN PARACHUTE option: shown inside the deploy window, clickable, with its keys.</summary>
        void DrawOpenParachuteButton(float cx, ParachuteController ctrl, SkydiveSettings st, bool vr, bool blink)
        {
            bool urgent = ctrl.Agl <= st.warningAltitude;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (urgent ? 12f : 5f));
            Rect r = R(cx - 280f, 790f, 560f, 100f);
            bool hover = Cursor.lockState != CursorLockMode.Locked && r.Contains(Event.current.mousePosition);
            Color edge = urgent ? Color.Lerp(Red, Yellow, pulse) : Color.Lerp(Green, Color.white, pulse * 0.5f);
            Fill(r, urgent ? new Color(0.35f, 0.02f, 0.02f, 0.85f) : (hover ? new Color(0.05f, 0.45f, 0.2f, 0.9f) : new Color(0.02f, 0.2f, 0.08f, 0.85f)));
            Border(r, edge, 3f + pulse * 2f);
            Text(new Rect(r.x, r.y + 6f * S, r.width, 58f * S), Loc.T("OPEN PARACHUTE"), 46, urgent && blink ? Yellow : Color.white, TextAnchor.MiddleCenter);
            string keys = "[" + SkydiveSettings.KeyName(st.deployInput, vr) + "]   " + Loc.T("or") + "   [" + SkydiveSettings.KeyName(st.deployAltInput, vr) + "]   " +
                          Loc.T("or") + "   [" + SkydiveSettings.KeyName(st.deployClickInput, vr) + "]";
            Text(new Rect(r.x, r.y + 62f * S, r.width, 30f * S), keys + "     " + Mathf.RoundToInt(ctrl.Agl) + " m", 18, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, false);
            // clickable when the cursor is free (menus / touch); with a locked cursor the click itself deploys
            if (Cursor.lockState != CursorLockMode.Locked && GUI.Button(r, GUIContent.none, GUIStyle.none)) ctrl.RequestDeploy();
        }

        void DrawWorldMarker(Camera cam, Vector3 world, string label, Color col)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            float x = sp.x, y = Screen.height - sp.y;
            if (x < 0f || x > Screen.width || y < 0f || y > Screen.height) return;
            float s = 14f * S;
            Border(new Rect(x - s, y - s, s * 2f, s * 2f), col, 2f);
            Text(new Rect(x - 150f * S, y + s + 2f * S, 300f * S, 26f * S), label, 18, col, TextAnchor.MiddleCenter);
        }

        void DrawPadCompass(ParachuteMode m, PlayerController p, ParachuteController ctrl)
        {
            float size = 240f;
            float x = W - 40f - size, y = 1080f - 60f - size;
            Rect rr = R(x, y, size, size);
            Fill(rr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(rr, new Color(0.15f, 0.8f, 0.9f, 0.7f), 2f);
            Vector2 c = new Vector2((x + size * 0.5f) * S, (y + size * 0.5f) * S);
            Vector3 fwd = ViewForward(p);
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = ctrl.HeadingForward;
            float yawRef = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            // north tick
            DrawPointer(c + Rotate(new Vector2(0f, -(size * 0.5f - 14f) * S), -yawRef), -yawRef, 14f * S, 3f * S, new Color(1f, 1f, 1f, 0.5f));
            Vector2 north = c + Rotate(new Vector2(0f, -(size * 0.5f - 30f) * S), -yawRef);
            Text(new Rect(north.x - 15f * S, north.y - 12f * S, 30f * S, 24f * S), "N", 16, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);

            Vector3 to = m.PadCenter - p.transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
            // the pad dot moves towards the centre as you get close (1.5 km range)
            float rN = Mathf.Clamp01(dist / 1500f) * (size * 0.5f - 22f) * S;
            Vector2 padDot = c + Rotate(new Vector2(0f, -rN), ang);
            DrawPointer(c, ang, 70f * S, 6f * S, Green);
            Fill(new Rect(padDot.x - 7f * S, padDot.y - 7f * S, 14f * S, 14f * S), Green);

            Ring next = m.NextRing;
            if (next != null && ctrl.State != ParachuteController.Phase.Plane)
            {
                Vector3 tr = next.transform.position - p.transform.position;
                tr.y = 0f;
                float ra = Vector3.SignedAngle(fwd, tr, Vector3.up);
                float rn = Mathf.Clamp01(tr.magnitude / 400f) * (size * 0.5f - 22f) * S;
                Vector2 rd = c + Rotate(new Vector2(0f, -rn), ra);
                Fill(new Rect(rd.x - 5f * S, rd.y - 5f * S, 10f * S, 10f * S), Yellow);
            }
            Fill(new Rect(c.x - 4f * S, c.y - 4f * S, 8f * S, 8f * S), Cyan);
            Text(R(x, y + size + 4f, size, 26f), Loc.T("PAD") + "  " + Mathf.RoundToInt(dist) + " m", 20, Green, TextAnchor.MiddleCenter);
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        // ============================================================================ mode picker + results

        /// <summary>Three mode cards in a row. Returns true if one was clicked (the switch is already done).</summary>
        bool DrawModeCards(float x, float y, float w, float h)
        {
            ModeManager mm = ModeManager.Instance;
            if (mm == null) return false;
            float gap = 24f;
            float cw = (w - gap * 2f) / 3f;
            for (int i = 0; i < ModeManager.All.Length; i++)
            {
                GameMode gmode = ModeManager.All[i];
                float bx = x + i * (cw + gap);
                bool current = mm.Current == gmode && GameManager.Instance.State != GameState.Menu;
                if (Button(R(bx, y, cw, 70f), ModeManager.Name(gmode) + "  (" + ModeManager.HotKey(gmode) + ")", true, 28, current))
                {
                    mm.SwitchTo(gmode);
                    return true;
                }
                Text(R(bx + 10f, y + 80f, cw - 20f, h - 130f), ModeManager.Description(gmode), 19, Dim, TextAnchor.UpperCenter, false, true);
                int best = ModeManager.Best(gmode);
                Text(R(bx, y + h - 44f, cw, 34f), Loc.T("BEST") + "  " + best.ToString("N0"), 22, best > 0 ? Yellow : Dim, TextAnchor.MiddleCenter);
            }
            return false;
        }

        void DrawModePicker(GameManager gm, float px, float py)
        {
            Text(R(px, py + 10f, 1360f, 46f), Loc.T("SWITCH MODE"), 34, Cyan, TextAnchor.MiddleCenter);
            Text(R(px, py + 56f, 1360f, 30f), Loc.T("The current run ends. Each mode keeps its own high score."), 20, Dim, TextAnchor.MiddleCenter, false);
            if (DrawModeCards(px + 60f, py + 110f, 1240f, 260f)) CloseOverlays();
        }

        /// <summary>Results screen of a finished mini-game. Returns false in shooter mode.</summary>
        bool DrawModeResults(GameManager gm)
        {
            ModeManager mm = ModeManager.Instance;
            if (mm == null || mm.IsShooter || mm.Active == null) return false;
            MiniGame g = mm.Active;
            FullScreenArea();
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.05f, 0.72f));
            float cx = W * 0.5f;
            Text(R(cx - 800f, 70f, 1600f, 90f), g.ResultTitle, 70, Cyan, TextAnchor.MiddleCenter);
            Text(R(cx - 800f, 160f, 1600f, 80f), Loc.T("SCORE") + "  " + g.Score.ToString("N0"), 60, Color.white, TextAnchor.MiddleCenter);
            if (g.NewBest)
            {
                float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
                Text(R(cx - 800f, 238f, 1600f, 40f), Loc.T("NEW HIGH SCORE!"), 32, new Color(1f, 0.9f, 0.2f, a), TextAnchor.MiddleCenter);
            }

            modeRows.Clear();
            g.ResultRows(modeRows);
            Rect pr = R(cx - 420f, 290f, 840f, 60f + modeRows.Count / 2 * 44f);
            Fill(pr, new Color(0.01f, 0.03f, 0.07f, 0.85f));
            Border(pr, new Color(0.15f, 0.6f, 0.7f, 0.9f), 2f);
            for (int i = 0; i + 1 < modeRows.Count; i += 2)
            {
                float ry = 315f + i / 2 * 44f;
                Text(R(cx - 390f, ry, 560f, 40f), modeRows[i], 24, Color.white, TextAnchor.MiddleLeft, false);
                Text(R(cx + 170f, ry, 220f, 40f), modeRows[i + 1], 26, Cyan, TextAnchor.MiddleRight);
            }

            float by = 330f + modeRows.Count / 2 * 44f + 40f;
            if (Button(R(cx - 420f, by, 400f, 66f), Loc.T("PLAY AGAIN"), true, 30)) { mm.Restart(); return true; }
            if (Button(R(cx + 20f, by, 400f, 66f), Loc.T("MAIN MENU"), true, 30)) { gm.EnterMenu(); return true; }
            DrawModeCards(cx - 620f, by + 90f, 1240f, 190f);
            return true;
        }
    }
}
