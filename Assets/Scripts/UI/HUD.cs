using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Complete 2D interface drawn with Unity's immediate-mode GUI (no Canvas / TextMeshPro assets needed).
    /// This file: shared drawing helpers + the in-game HUD (drawn once per player viewport for split-screen co-op):
    /// crosshair + hit markers, health / shield / stamina, ammo, wave info, radar, damage indicators, floating damage
    /// numbers, power-up timers, tutorial panel, banners. Menus, shop and game-over screens live in the other HUD*.cs
    /// partial files. Layout is authored for 1080p height and scaled to any resolution.
    /// </summary>
    public partial class HUD : MonoBehaviour
    {
        public bool showHelp;
        public bool newHighScore;

        class FloatText { public Vector3 pos; public string text; public Color color; public float born; public bool head; }
        class ToastMsg { public string text; public float born; }
        class DamageInd { public Vector3 from; public float born; }

        /// <summary>Per-player HUD state (hit markers, damage indicators).</summary>
        class PState
        {
            public float hitTime = -10f, blockedTime = -10f, lastHurtTime = -10f;
            public bool hitHead, hitKill;
            public readonly List<DamageInd> indicators = new List<DamageInd>();
        }

        readonly List<FloatText> floats = new List<FloatText>();
        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        readonly PState[] pstate = { new PState(), new PState() };

        string bannerTitle = "", bannerSub = "";
        float bannerBorn = -10f;
        float gameOverTime;
        float fps = 60f;
        /// <summary>F4: live performance panel (FPS, frame time, batches, triangles, memory) for demos and profiling.</summary>
        public bool showPerf;
        readonly List<KeyValuePair<string, string>> perfLines = new List<KeyValuePair<string, string>>();

        GUIStyle label;
        GUIStyle field;
        Texture2D vignette;
        float S = 1f;       // pixels per layout unit
        float W = 1920f;    // layout width of the current area (height is always 1080)
        float VW, VH;       // pixel size of the current area

        static readonly Color Cyan = new Color(0.15f, 0.92f, 1f, 1f);
        static readonly Color Magenta = new Color(1f, 0.2f, 0.7f, 1f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.1f, 1f);
        static readonly Color Red = new Color(1f, 0.15f, 0.15f, 1f);
        static readonly Color Yellow = new Color(1f, 0.9f, 0.2f, 1f);
        static readonly Color Green = new Color(0.3f, 1f, 0.45f, 1f);
        static readonly Color Blue = new Color(0.35f, 0.65f, 1f, 1f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);

        // ============================================================================ events from the game

        public void Toast(string text)
        {
            toasts.Add(new ToastMsg { text = text, born = Time.unscaledTime });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        public void Banner(string title, string sub)
        {
            bannerTitle = title;
            bannerSub = sub;
            bannerBorn = Time.unscaledTime;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Player != null && gm.Player.vr != null)
                gm.Player.vr.ShowBanner(string.IsNullOrEmpty(sub) ? title : title + "\n" + sub);
        }

        static PState Safe(PState[] arr, int i) { return arr[Mathf.Clamp(i, 0, arr.Length - 1)]; }

        public void AddHitMarker(int player, bool head, bool kill)
        {
            PState s = Safe(pstate, player);
            s.hitTime = Time.unscaledTime;
            s.hitHead = head;
            s.hitKill = kill;
        }

        public void AddBlockedMarker(int player) { Safe(pstate, player).blockedTime = Time.unscaledTime; }

        public void AddDamageNumber(Vector3 pos, float amount, bool head)
        {
            FloatText f = new FloatText();
            f.pos = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0.2f, Random.Range(-0.3f, 0.3f));
            f.text = Mathf.RoundToInt(amount).ToString();
            f.color = head ? Yellow : (PowerUps.DoubleDamage ? Orange : Color.white);
            f.head = head;
            f.born = Time.unscaledTime;
            floats.Add(f);
            if (floats.Count > 40) floats.RemoveAt(0);
        }

        public void OnPlayerHit(PlayerController p, Vector3 from, float amount)
        {
            PState s = Safe(pstate, p != null ? p.index : 0);
            s.lastHurtTime = Time.unscaledTime;
            DamageInd d = new DamageInd();
            d.from = from;
            d.born = Time.unscaledTime;
            s.indicators.Add(d);
            if (s.indicators.Count > 8) s.indicators.RemoveAt(0);
        }

        public void OnGameOver()
        {
            gameOverTime = Time.unscaledTime;
            panel = Panel.None;
            overTab = 0;
            if (heatmap != null) Destroy(heatmap);
            if (chart != null) Destroy(chart);
            heatmap = null;
            chart = null;
        }

        float fadeStart = -10f, fadeDur = 0.6f;
        bool fadePending;

        /// <summary>Fade the screen in from black (mode start, restart, return to menu) for a consistent transition.</summary>
        public void FadeIn(float duration = 0.6f)
        {
            fadePending = true;   // the clock starts on the first drawn frame, so a long mode build cannot use up the fade
            fadeDur = Mathf.Max(0.05f, duration);
        }

        void DrawFade()
        {
            if (fadePending)
            {
                if (Event.current.type != EventType.Repaint) { Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.01f, 0.025f, 1f)); return; }
                fadePending = false;
                fadeStart = Time.unscaledTime;
            }
            float t = (Time.unscaledTime - fadeStart) / fadeDur;
            if (t >= 1f || t < 0f) return;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.01f, 0.025f, 1f - Mathf.SmoothStep(0f, 1f, t)));
        }

        public void ResetUi()
        {
            floats.Clear(); toasts.Clear();
            for (int i = 0; i < pstate.Length; i++)
            {
                pstate[i].indicators.Clear();
                pstate[i].hitTime = -10f; pstate[i].lastHurtTime = -10f; pstate[i].blockedTime = -10f;
            }
            bannerBorn = -10f;
            newHighScore = false;
            panel = Panel.None;
            waitingBind = -1;
        }

        /// <summary>Automated smoke test: open a menu panel / settings tab / game-over tab.</summary>
        public void DebugShow(int panelIndex, int tab)
        {
            panel = (Panel)Mathf.Clamp(panelIndex, 0, 3);
            settingsTab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            overTab = Mathf.Clamp(tab, 0, 2);
        }

        public void CloseOverlays()
        {
            panel = Panel.None;
            waitingBind = -1;
            GameSettings.Save();
        }

        // ============================================================================ drawing helpers

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label);
            label.wordWrap = false;
            label.clipping = TextClipping.Overflow;
            label.richText = false;
            field = new GUIStyle(GUI.skin.textField);
            vignette = ProcTex.Vignette(128);
        }

        /// <summary>Use the whole screen as the layout area.</summary>
        void FullScreenArea()
        {
            VW = Screen.width;
            VH = Screen.height;
            S = VH / 1080f;
            W = VW / S;
        }

        Rect R(float x, float y, float w, float h) { return new Rect(x * S, y * S, w * S, h * S); }

        static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);   // respects a global fade set through GUI.color
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Border(Rect r, Color c, float t)
        {
            float th = Mathf.Max(1f, t * S);
            Fill(new Rect(r.x, r.y, r.width, th), c);
            Fill(new Rect(r.x, r.yMax - th, r.width, th), c);
            Fill(new Rect(r.x, r.y, th, r.height), c);
            Fill(new Rect(r.xMax - th, r.y, th, r.height), c);
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor anchor, bool bold = true, bool wrap = false)
        {
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S));
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.wordWrap = wrap;
            label.clipping = wrap ? TextClipping.Clip : TextClipping.Overflow;
            label.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.75f);
            GUI.Label(new Rect(r.x + 2f * S, r.y + 2f * S, r.width, r.height), s, label);
            label.normal.textColor = c;
            GUI.Label(r, s, label);
        }

        bool Button(Rect r, string text, bool enabled = true, int size = 30, bool selected = false)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Color bg = !enabled ? new Color(0.03f, 0.05f, 0.08f, 0.6f) : (hover ? new Color(0.1f, 0.55f, 0.65f, 0.85f) : (selected ? new Color(0.06f, 0.3f, 0.38f, 0.9f) : new Color(0.03f, 0.07f, 0.12f, 0.85f)));
            if (hover)
            {
                // soft pulsing outer glow + left accent bar: the same hover language as the Rat Shooter button
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                for (int i = 2; i >= 1; i--)
                {
                    float g = i * 3f * S;
                    Border(new Rect(r.x - g, r.y - g, r.width + g * 2f, r.height + g * 2f), new Color(Cyan.r, Cyan.g, Cyan.b, (0.1f + 0.12f * pulse) / i), 2f);
                }
            }
            Fill(r, bg);
            if (hover || selected) Fill(new Rect(r.x, r.y, 6f * S, r.height), hover ? Color.white : Cyan);
            Border(r, hover || selected ? Cyan : new Color(0.15f, 0.5f, 0.6f, enabled ? 0.9f : 0.4f), 2f);
            Text(r, text, size, !enabled ? new Color(1f, 1f, 1f, 0.35f) : (hover ? Color.white : new Color(0.8f, 0.95f, 1f, 1f)), TextAnchor.MiddleCenter);
            if (enabled && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                Sfx.Play2D(SfxId.Click, 0.7f);
                return true;
            }
            return false;
        }

        float Slider(Rect r, string name, float value, float min, float max, string fmt)
        {
            Text(new Rect(r.x, r.y - 6f * S, 360f * S, r.height), name, 26, Color.white, TextAnchor.MiddleLeft, false);
            Rect sr = new Rect(r.x + 380f * S, r.y + r.height * 0.3f, 340f * S, r.height * 0.5f);
            float v = GUI.HorizontalSlider(sr, value, min, max);
            Text(new Rect(sr.xMax + 20f * S, r.y - 6f * S, 140f * S, r.height), v.ToString(fmt), 26, Cyan, TextAnchor.MiddleLeft);
            return v;
        }

        bool Toggle(Rect r, string name, bool value)
        {
            if (Button(r, name + ":  " + (value ? Loc.T("ON") : Loc.T("OFF")), true, 24, value)) return !value;
            return value;
        }

        /// <summary>"&lt; value &gt;" selector; returns -1 / 0 / +1.</summary>
        int Selector(float x, float y, float w, string name, string value)
        {
            Text(R(x, y, 360f, 50f), name, 26, Color.white, TextAnchor.MiddleLeft, false);
            int r = 0;
            if (Button(R(x + 380f, y + 3f, 54f, 44f), "<", true, 26)) r = -1;
            Fill(R(x + 440f, y + 3f, w - 500f, 44f), new Color(0f, 0f, 0f, 0.45f));
            Text(R(x + 440f, y + 3f, w - 500f, 44f), value, 24, Cyan, TextAnchor.MiddleCenter);
            if (Button(R(x + w - 54f, y + 3f, 54f, 44f), ">", true, 26)) r = 1;
            return r;
        }

        void DrawVignette(float alpha)
        {
            if (alpha <= 0.01f) return;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(0f, 0f, VW, VH), vignette);
            GUI.color = old;
        }

        // ============================================================================ main entry

        void OnGUI()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.Player.cam == null) return;
            EnsureStyles();
            HandleRebindEvent();
            FullScreenArea();

            float dtu = Time.unscaledDeltaTime;
            if (dtu > 0f) fps = Mathf.Lerp(fps, 1f / dtu, 0.05f);

            switch (gm.State)
            {
                case GameState.Playing:
                    if (!DrawModeHud(gm)) DrawPlayerViews(gm);
                    break;
                case GameState.Paused:
                    if (!DrawModeHud(gm)) DrawPlayerViews(gm);
                    FullScreenArea();
                    DrawPause(gm);
                    break;
                case GameState.Shop:
                    DrawPlayerViews(gm);
                    FullScreenArea();
                    DrawShop(gm);
                    break;
                case GameState.GameOver:
                    if (DrawModeResults(gm)) break;
                    DrawVignette(1f);
                    DrawGameOver(gm);
                    break;
                default:
                    DrawMenu(gm);
                    break;
            }

            FullScreenArea();
            DrawFade();
            if (GameSettings.ShowFps)
                Text(R(W - 200f, 1040f, 180f, 30f), Mathf.RoundToInt(fps) + " FPS", 22, Yellow, TextAnchor.MiddleRight);
            if (showPerf) DrawPerfOverlay();
            if (showHelp) DrawHelpOverlay();
        }

        /// <summary>Draw one HUD per player viewport (split-screen co-op), then shared banners and toasts.</summary>
        void DrawPlayerViews(GameManager gm)
        {
            bool split = gm.Players.Count > 1;
            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController p = gm.Players[i];
                if (p == null || p.cam == null || !p.gameObject.activeInHierarchy) continue;
                if (split && gm.VrActive && p.index == 0) continue;   // asymmetric co-op: the monitor shows P2
                Rect pr = p.cam.pixelRect;
                Rect gr = new Rect(pr.x, Screen.height - pr.y - pr.height, pr.width, pr.height);
                GUI.BeginGroup(gr);
                VW = gr.width;
                VH = gr.height;
                S = VH / 1080f * (split && !gm.VrActive ? 0.8f : 1f);
                W = VW / S;
                DrawGameHud(gm, p, gr, split);
                GUI.EndGroup();
            }
            if (split && !gm.VrActive) Fill(new Rect(Screen.width * 0.5f - 2f, 0f, 4f, Screen.height), new Color(0f, 0f, 0f, 0.9f));
            FullScreenArea();
            DrawToastsAndBanner(Time.unscaledTime, gm);
        }

        // ============================================================================ in-game HUD

        void DrawGameHud(GameManager gm, PlayerController p, Rect area, bool split)
        {
            WeaponSystem w = p.weapons;
            Camera cam = p.cam;
            PState ps = Safe(pstate, p.index);
            Vector2 c = new Vector2(VW * 0.5f, VH * 0.5f);
            float now = Time.unscaledTime;

            // --- damage vignette (recent hit flash + low health pulse)
            float low = 1f - p.Health / p.maxHealth;
            float flash = Mathf.Clamp01(1f - (now - ps.lastHurtTime) / 0.6f);
            float pulse = low > 0.6f ? (low - 0.6f) * 1.8f * (0.65f + 0.35f * Mathf.Sin(now * 6f)) : 0f;
            DrawVignette(Mathf.Max(flash * 0.9f, pulse));

            if (p.IsDead)
            {
                Fill(new Rect(0f, 0f, VW, VH), new Color(0.15f, 0f, 0.02f, 0.5f));
                Text(R(0f, 420f, W, 100f), Loc.T("DOWN"), 90, Red, TextAnchor.MiddleCenter);
                Text(R(0f, 520f, W, 40f), Loc.T("You come back when your partner clears the wave"), 26, Color.white, TextAnchor.MiddleCenter, false);
                DrawWaveInfo(gm, p);
                return;
            }

            // --- directional damage indicators
            Matrix4x4 m = GUI.matrix;
            Vector3 camFlat = cam.transform.forward;
            camFlat.y = 0f;
            for (int i = ps.indicators.Count - 1; i >= 0; i--)
            {
                DamageInd d = ps.indicators[i];
                float age = now - d.born;
                if (age > 1.6f) { ps.indicators.RemoveAt(i); continue; }
                Vector3 to = d.from - p.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f || camFlat.sqrMagnitude < 0.0001f) continue;
                float ang = Vector3.SignedAngle(camFlat, to, Vector3.up);
                GUI.matrix = m;
                GUIUtility.RotateAroundPivot(ang, c);   // pivot is in group space (Unity unclips it)
                Fill(new Rect(c.x - 70f * S, c.y - 240f * S, 140f * S, 12f * S), new Color(1f, 0.1f, 0.1f, 1f - age / 1.6f));
            }
            GUI.matrix = m;

            // --- crosshair (not in VR: the laser sight replaces it)
            if (!p.VrOn)
            {
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float px = Mathf.Tan(w.CurrentSpreadDeg * Mathf.Deg2Rad) / Mathf.Tan(fovRad * 0.5f) * VH * 0.5f;
                float gap = Mathf.Max(7f * S, px);
                float len = 11f * S, th = Mathf.Max(1.5f, 2.6f * S);
                Color cc = w.TargetingEnemy ? Red : new Color(1f, 1f, 1f, 0.92f);
                if (PowerUps.DoubleDamage) cc = w.TargetingEnemy ? Red : Orange;
                if (!w.IsReloading || w.Current.shellReload)
                {
                    Fill(new Rect(c.x - th / 2f, c.y - gap - len, th, len), cc);
                    Fill(new Rect(c.x - th / 2f, c.y + gap, th, len), cc);
                    Fill(new Rect(c.x - gap - len, c.y - th / 2f, len, th), cc);
                    Fill(new Rect(c.x + gap, c.y - th / 2f, len, th), cc);
                }
                Fill(new Rect(c.x - 1.5f * S, c.y - 1.5f * S, 3f * S, 3f * S), cc);
            }

            // --- hit marker
            float since = now - ps.hitTime;
            if (since < 0.22f)
            {
                Color hc = ps.hitKill ? Red : (ps.hitHead ? Yellow : Color.white);
                hc.a = 1f - since / 0.22f;
                float off = (ps.hitKill ? 12f : 9f) * S;
                for (int k = 0; k < 4; k++)
                {
                    GUI.matrix = m;
                    GUIUtility.RotateAroundPivot(45f + 90f * k, c);
                    Fill(new Rect(c.x + off, c.y - 1.6f * S, 12f * S, 3.2f * S), hc);
                }
                GUI.matrix = m;
            }
            float blocked = now - ps.blockedTime;
            if (blocked < 0.5f)
                Text(new Rect(c.x - 120f * S, c.y - 70f * S, 240f * S, 30f * S), Loc.T("SHIELD - FLANK IT!"), 20, new Color(0.3f, 0.9f, 1f, 1f - blocked / 0.5f), TextAnchor.MiddleCenter);

            // --- reload indicator
            if (w.IsReloading)
            {
                Rect br = new Rect(c.x - 70f * S, c.y + 60f * S, 140f * S, 8f * S);
                Fill(br, new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(br.x, br.y, br.width * w.ReloadProgress, br.height), Cyan);
                Text(new Rect(c.x - 100f * S, c.y + 70f * S, 200f * S, 30f * S), Loc.T("RELOADING"), 20, Cyan, TextAnchor.MiddleCenter);
            }
            else if (w.Mag == 0 && w.Reserve == 0)
            {
                Text(new Rect(c.x - 150f * S, c.y + 60f * S, 300f * S, 34f * S), Loc.T("OUT OF AMMO"), 26, Red, TextAnchor.MiddleCenter);
            }
            else if (w.Mag <= Mathf.Max(1, w.CurrentMagSize / 4))
            {
                float a = 0.5f + 0.5f * Mathf.Sin(now * 8f);
                string key = p.useKeyboardMouse ? KeyBindings.KeyFor(GameAction.Reload) : "X";
                Text(new Rect(c.x - 150f * S, c.y + 60f * S, 300f * S, 34f * S), Loc.T("RELOAD") + " [" + key + "]", 24, new Color(1f, 0.8f, 0.2f, a), TextAnchor.MiddleCenter);
            }
            if (p.UsingMedkit)
                Text(new Rect(c.x - 150f * S, c.y + 100f * S, 300f * S, 34f * S), Loc.T("HEALING..."), 24, Green, TextAnchor.MiddleCenter);

            DrawVitals(p);
            DrawAmmo(p, w);
            if (gm.InTutorial && p.index == 0) DrawTutorial(gm);
            else DrawWaveInfo(gm, p);
            DrawRadar(gm, p);
            DrawPowerUps();
            DrawFloatingText(cam, now, area);

            if (gm.PlayTime < 14f && gm.Playing && !gm.InTutorial && !p.VrOn)
            {
                float a = Mathf.Clamp01((14f - gm.PlayTime) / 2f);
                string hint = p.useKeyboardMouse
                    ? string.Format(Loc.T("{0} move   {1} sprint   {2} slide   {3} jump   {4} dash   {5} fire   {6} aim   {7} reload   {8} grenade   {9} melee   {10} medkit"),
                        KeyBindings.KeyFor(GameAction.MoveForward) + KeyBindings.KeyFor(GameAction.MoveLeft) + KeyBindings.KeyFor(GameAction.MoveBack) + KeyBindings.KeyFor(GameAction.MoveRight),
                        KeyBindings.KeyFor(GameAction.Sprint), KeyBindings.KeyFor(GameAction.Crouch), KeyBindings.KeyFor(GameAction.Jump),
                        KeyBindings.KeyFor(GameAction.Dash), KeyBindings.KeyFor(GameAction.Fire), KeyBindings.KeyFor(GameAction.Aim),
                        KeyBindings.KeyFor(GameAction.Reload), KeyBindings.KeyFor(GameAction.Grenade), KeyBindings.KeyFor(GameAction.Melee),
                        KeyBindings.KeyFor(GameAction.Medkit))
                    : Loc.T("L-stick move   R-stick look   RT fire   LT aim   A jump   B crouch   X reload   Y switch   LB dash   RB grenade   R3 melee");
                Text(R(W * 0.5f - 700f, 858f, 1400f, 30f), hint, split ? 18 : 20, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter, false);
            }
            if (split)
                Text(R(20f, 1040f, 300f, 30f), "P" + (p.index + 1) + (p.gamepadIndex >= 0 ? "  [" + Loc.T("PAD") + "]" : "") + (p.useKeyboardMouse ? "  [" + Loc.T("KB+MOUSE") + "]" : ""),
                     18, p.index == 0 ? Cyan : Green, TextAnchor.MiddleLeft);
            if (split && p.index == 1 && p.gamepadIndex < 0)
                Text(R(0f, 600f, W, 40f), Loc.T("Connect a gamepad for player 2"), 30, Yellow, TextAnchor.MiddleCenter);
        }

        void DrawVitals(PlayerController p)
        {
            float x = 50f, y = 920f;
            Fill(R(x - 14f, y - 14f, 440f, 150f), new Color(0.01f, 0.03f, 0.06f, 0.45f));

            float hp = p.Health / p.maxHealth;
            Color hpCol = Color.Lerp(Red, Green, Mathf.Clamp01(hp * 1.3f));
            Fill(R(x, y, 412f, 24f), new Color(0f, 0f, 0f, 0.6f));
            Fill(R(x, y, 412f * hp, 24f), hpCol);
            Border(R(x, y, 412f, 24f), new Color(1f, 1f, 1f, 0.35f), 1.5f);
            Text(R(x + 8f, y - 2f, 300f, 28f), Loc.T("HEALTH") + "  " + Mathf.CeilToInt(p.Health), 20, Color.white, TextAnchor.MiddleLeft);

            float sh = p.Shield / Mathf.Max(1f, p.maxShield);
            Fill(R(x, y + 32f, 412f, 16f), new Color(0f, 0f, 0f, 0.6f));
            Fill(R(x, y + 32f, 412f * sh, 16f), Cyan);
            Border(R(x, y + 32f, 412f, 16f), new Color(1f, 1f, 1f, 0.3f), 1.5f);
            Text(R(x + 8f, y + 28f, 300f, 24f), Loc.T("SHIELD") + (Upgrades.Level(UpgradeId.Shield) > 0 ? "  +" + Mathf.RoundToInt(Upgrades.ShieldBonus) : ""), 16, new Color(0f, 0.1f, 0.15f, 1f), TextAnchor.MiddleLeft);

            float st = p.Stamina / p.maxStamina;
            Fill(R(x, y + 56f, 412f, 8f), new Color(0f, 0f, 0f, 0.6f));
            Fill(R(x, y + 56f, 412f * st, 8f), Yellow);

            string kits = Loc.T("MEDKITS") + " ";
            for (int i = 0; i < 3; i++) kits += i < p.Medkits ? "[+] " : "[ ] ";
            string mk = p.useKeyboardMouse ? KeyBindings.KeyFor(GameAction.Medkit) : Loc.T("D-PAD DOWN");
            Text(R(x, y + 70f, 400f, 28f), kits + " (" + mk + ")", 20, p.Medkits > 0 ? Green : new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleLeft);

            Fill(R(x, y + 108f, 412f, 6f), new Color(0f, 0f, 0f, 0.6f));
            Fill(R(x, y + 108f, 412f * p.DashReady, 6f), p.DashReady >= 1f ? Magenta : new Color(0.5f, 0.2f, 0.4f, 1f));
            Text(R(x + 220f, y + 88f, 192f, 20f), Loc.T("DASH") + " (" + (p.useKeyboardMouse ? KeyBindings.KeyFor(GameAction.Dash) : "LB") + ")", 14, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleRight);
        }

        void DrawAmmo(PlayerController p, WeaponSystem w)
        {
            float rx = W - 60f;
            Fill(R(rx - 460f, 900f, 480f, 170f), new Color(0.01f, 0.03f, 0.06f, 0.45f));

            Text(R(rx - 440f, 910f, 430f, 30f), w.Current.name, 22, Cyan, TextAnchor.MiddleRight);
            Text(R(rx - 440f, 935f, 300f, 90f), w.Mag.ToString(), 82, w.Mag <= w.CurrentMagSize / 4 ? Red : Color.white, TextAnchor.MiddleRight);
            Text(R(rx - 130f, 960f, 120f, 50f), "/ " + w.Reserve, 34, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleLeft);

            for (int i = 0; i < w.SlotCount; i++)
            {
                Rect sr = R(rx - 440f + i * 62f, 1030f, 54f, 30f);
                bool cur = i == w.CurrentIndex;
                Fill(sr, cur ? new Color(0.1f, 0.6f, 0.7f, 0.9f) : new Color(0f, 0f, 0f, 0.5f));
                Border(sr, cur ? Cyan : new Color(1f, 1f, 1f, 0.25f), 1.5f);
                Text(sr, (i + 1).ToString(), 20, Color.white, TextAnchor.MiddleCenter);
            }
            string g = Loc.T("GRENADES") + "  ";
            for (int i = 0; i < WeaponSystem.MaxGrenades; i++) g += i < w.Grenades ? "o " : ". ";
            string gk = p.useKeyboardMouse ? KeyBindings.KeyFor(GameAction.Grenade) : "RB";
            Text(R(rx - 250f, 1030f, 240f, 30f), g + "(" + gk + ")", 18, w.Grenades > 0 ? Orange : new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleRight);
        }

        void DrawPowerUps()
        {
            float y = 280f;
            if (PowerUps.DoubleDamage)
            {
                float k = PowerUps.DoubleDamageLeft / PowerUps.DoubleDamageDuration;
                Fill(R(40f, y, 220f, 34f), new Color(0.3f, 0.05f, 0f, 0.6f));
                Fill(R(40f, y + 28f, 220f * Mathf.Clamp01(k), 6f), Orange);
                Text(R(48f, y, 210f, 30f), "x2 " + Loc.T("DAMAGE") + "  " + Mathf.CeilToInt(PowerUps.DoubleDamageLeft) + "s", 20, Orange, TextAnchor.MiddleLeft);
                y += 44f;
            }
            if (PowerUps.SlowMo)
            {
                float k = PowerUps.SlowMoLeft / PowerUps.SlowMoDuration;
                Fill(R(40f, y, 220f, 34f), new Color(0f, 0.1f, 0.3f, 0.6f));
                Fill(R(40f, y + 28f, 220f * Mathf.Clamp01(k), 6f), Blue);
                Text(R(48f, y, 210f, 30f), Loc.T("SLOW-MO") + "  " + Mathf.CeilToInt(PowerUps.SlowMoLeft) + "s", 20, Blue, TextAnchor.MiddleLeft);
            }
        }

        void DrawTutorial(GameManager gm)
        {
            Tutorial t = gm.Tutorial;
            if (t == null || !t.Running) return;
            float cx = W * 0.5f;
            Rect pr = R(cx - 520f, 20f, 1040f, 200f);
            Fill(pr, new Color(0.01f, 0.03f, 0.07f, 0.8f));
            Border(pr, Orange, 2f);
            Text(R(cx - 500f, 28f, 1000f, 40f), Loc.T("TRAINING") + "  " + Mathf.Min(t.StepIndex + 1, t.StepCount) + " / " + t.StepCount + "   -   " + t.Title, 30, Orange, TextAnchor.MiddleCenter);
            Text(R(cx - 490f, 75f, 980f, 80f), t.Instruction, 24, Color.white, TextAnchor.UpperCenter, false, true);
            Text(R(cx - 500f, 160f, 1000f, 30f), t.Progress, 24, Cyan, TextAnchor.MiddleCenter);
            Text(R(cx - 500f, 192f, 1000f, 24f), Loc.T("ESC: pause / skip tutorial"), 16, Dim, TextAnchor.MiddleCenter, false);
        }

        void DrawWaveInfo(GameManager gm, PlayerController p)
        {
            WaveManager wm = gm.Waves;
            float cx = W * 0.5f;
            Text(R(cx - 300f, 20f, 600f, 44f), Loc.T("WAVE") + " " + Mathf.Max(1, wm.Wave), 38, Cyan, TextAnchor.MiddleCenter);
            if (wm.Intermission)
                Text(R(cx - 300f, 62f, 600f, 30f), Loc.T("NEXT WAVE IN") + " " + Mathf.CeilToInt(wm.IntermissionLeft), 22, Yellow, TextAnchor.MiddleCenter);
            else
                Text(R(cx - 300f, 62f, 600f, 30f), Loc.T("HOSTILES") + "  " + wm.Remaining, 22, Color.white, TextAnchor.MiddleCenter);

            float rx = W - 50f;
            Text(R(rx - 460f, 20f, 460f, 50f), Loc.T("SCORE") + "  " + gm.Score.ToString("N0"), 40, Color.white, TextAnchor.MiddleRight);
            Text(R(rx - 460f, 68f, 460f, 26f), "$" + gm.Credits.ToString("N0") + "   " + Loc.T("BEST") + "  " + Mathf.Max(gm.HighScore, gm.Score).ToString("N0"), 20, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight);
            if (gm.Combo > 1)
            {
                Text(R(rx - 460f, 94f, 460f, 36f), "x" + gm.Multiplier + "   " + Loc.T("COMBO") + " " + gm.Combo, 28, Orange, TextAnchor.MiddleRight);
                Fill(R(rx - 260f, 130f, 260f, 5f), new Color(0f, 0f, 0f, 0.5f));
                Fill(R(rx - 260f * Mathf.Clamp01(gm.ComboTimeLeft / 4.5f), 130f, 260f * Mathf.Clamp01(gm.ComboTimeLeft / 4.5f), 5f), Orange);
            }

            Enemy boss = wm.Boss;
            if (boss != null)
            {
                Fill(R(cx - 320f, 108f, 640f, 20f), new Color(0f, 0f, 0f, 0.65f));
                Fill(R(cx - 320f, 108f, 640f * boss.HealthFraction, 20f), Orange);
                Border(R(cx - 320f, 108f, 640f, 20f), Color.white, 1.5f);
                Text(R(cx - 320f, 128f, 640f, 26f), Loc.T("OVERLORD"), 18, Orange, TextAnchor.MiddleCenter);
            }
        }

        void DrawRadar(GameManager gm, PlayerController p)
        {
            float size = 220f, x = 40f, y = 40f;
            Rect rr = R(x, y, size, size);
            Fill(rr, new Color(0f, 0.05f, 0.1f, 0.55f));
            Border(rr, new Color(0.15f, 0.8f, 0.9f, 0.8f), 2f);
            Fill(R(x + size / 2f - 0.5f, y, 1f, size), new Color(0.15f, 0.8f, 0.9f, 0.25f));
            Fill(R(x, y + size / 2f - 0.5f, size, 1f), new Color(0.15f, 0.8f, 0.9f, 0.25f));

            float yawRef = p.VrOn ? Quaternion.LookRotation(p.vr.HeadForwardFlat).eulerAngles.y : p.transform.eulerAngles.y;
            Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0f, yawRef, 0f));
            float range = MixedReality.Active ? 6f : 40f;
            float half = size / 2f - 6f;
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                Enemy e = Enemy.All[i];
                if (e == null) continue;
                Color col = Color.white;
                switch (e.type)
                {
                    case EnemyType.Boss: col = Orange; break;
                    case EnemyType.Drone: col = Yellow; break;
                    case EnemyType.Gunner: col = Magenta; break;
                    case EnemyType.Sniper: col = Green; break;
                    case EnemyType.Tank: col = Cyan; break;
                    default: col = Red; break;
                }
                Dot(p, inv, e.transform.position, range, half, x, y, size, col, e.type == EnemyType.Boss || e.type == EnemyType.Tank ? 13f : 8f);
            }
            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController o = gm.Players[i];
                if (o == null || o == p || !o.gameObject.activeInHierarchy) continue;
                Dot(p, inv, o.transform.position, range, half, x, y, size, o.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Color.white, 11f);
            }
            Fill(R(x + size / 2f - 4f, y + size / 2f - 4f, 8f, 8f), Cyan);
            Fill(R(x + size / 2f - 1f, y + size / 2f - 14f, 2f, 10f), Cyan);
        }

        void Dot(PlayerController p, Quaternion inv, Vector3 world, float range, float half, float x, float y, float size, Color col, float ds)
        {
            Vector3 rel = inv * (world - p.transform.position);
            Vector2 v = new Vector2(rel.x, rel.z) / range;
            bool edge = v.magnitude > 1f;
            if (edge) { v = v.normalized; col.a = 0.55f; }
            float dx = x + size / 2f + v.x * half - ds / 2f;
            float dy = y + size / 2f - v.y * half - ds / 2f;
            Fill(R(dx, dy, ds, ds), col);
        }

        void DrawFloatingText(Camera cam, float now, Rect area)
        {
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                FloatText f = floats[i];
                float age = now - f.born;
                if (age > 0.9f) { floats.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(f.pos + Vector3.up * (age * 1.2f));
                if (sp.z <= 0f) continue;
                float gx = sp.x - area.x;
                float gy = Screen.height - sp.y - area.y;
                if (gx < 0f || gx > VW) continue;
                Color col = f.color;
                col.a = 1f - age / 0.9f;
                float sz = f.head ? 34f : 26f;
                Text(new Rect(gx - 60f * S, gy - 20f * S, 120f * S, 40f * S), f.text, Mathf.RoundToInt(sz), col, TextAnchor.MiddleCenter);
            }
        }

        void DrawToastsAndBanner(float now, GameManager gm)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (now - toasts[i].born > 3f) toasts.RemoveAt(i);
            for (int i = 0; i < toasts.Count; i++)
            {
                float age = now - toasts[i].born;
                float a = age > 2.2f ? 1f - (age - 2.2f) / 0.8f : 1f;
                Text(R(W - 560f, 420f + i * 34f, 510f, 32f), toasts[i].text, 24, new Color(1f, 0.95f, 0.6f, a), TextAnchor.MiddleRight);
            }

            float bAge = now - bannerBorn;
            if (bAge < 3.6f)
            {
                float a = bAge < 0.3f ? bAge / 0.3f : (bAge > 2.9f ? 1f - (bAge - 2.9f) / 0.7f : 1f);
                float slide = (1f - Mathf.Clamp01(bAge / 0.3f)) * 40f;
                float yb = gm.InTutorial ? 300f : 250f;
                Text(R(0f, yb + slide, W, 120f), bannerTitle, gm.Players.Count > 1 ? 80 : 100, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.MiddleCenter);
                if (!string.IsNullOrEmpty(bannerSub))
                    Text(R(0f, yb + 110f, W, 50f), bannerSub, 30, new Color(1f, 1f, 1f, a * 0.9f), TextAnchor.MiddleCenter, false);
            }
        }
    
        /// <summary>Performance panel (F4): numbers come from Unity's ProfilerRecorder counters, see <see cref="Perf"/>.</summary>
        void DrawPerfOverlay()
        {
            if (Event.current.type == EventType.Repaint) Perf.Tick(Time.unscaledDeltaTime);
            Perf.Lines(perfLines);
            float x = 20f, y = 250f, w = 470f, lh = 27f;
            Rect box = R(x, y, w, 44f + perfLines.Count * lh);
            Fill(box, new Color(0.01f, 0.03f, 0.06f, 0.82f));
            Border(box, new Color(0.25f, 0.9f, 1f, 0.8f), 2f);
            Text(R(x + 14f, y + 6f, w - 28f, 30f), "PERFORMANCE  (F4)", 20, new Color(0.3f, 0.95f, 1f), TextAnchor.MiddleLeft);
            for (int i = 0; i < perfLines.Count; i++)
            {
                float ly = y + 40f + i * lh;
                Text(R(x + 14f, ly, 250f, lh), perfLines[i].Key, 17, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleLeft, false);
                Text(R(x + 230f, ly, w - 244f, lh), perfLines[i].Value, 17, Color.white, TextAnchor.MiddleRight);
            }
        }
    }
}
