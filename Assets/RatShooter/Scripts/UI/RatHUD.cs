using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Colourful cartoon interface for Rat Shooter, drawn with Unity's immediate-mode GUI like the rest of Neon Breach
    /// (no Canvas or font assets): chunky rounded panels and pill buttons with gloss and outlines, outlined bold text,
    /// procedural icons. Screens: title (with difficulty), how to play, countdown, in-game HUD (score, combo, timer,
    /// level, mice left, ammo with bullet icons, reload bar, pause button, reactive crosshair, hit markers, floating
    /// score popups, "!" bubbles over scared mice, banners), pause, level complete (stars + bonus tally), game over and
    /// victory (trophy + confetti). Layout is authored for 1080p and scales to any resolution.
    /// </summary>
    public class RatHUD : MonoBehaviour
    {
        public RatShooterGame game;
        public Camera cam;

        enum Sub { None, HowTo }

        class Pop { public Vector3 world; public string text; public Color color; public float born; public float size; }
        class Confetti { public Vector2 p, v; public float rot, vr, w, h; public Color c; }

        static readonly Color Dark = new Color(0.27f, 0.14f, 0.06f, 1f);
        static readonly Color Cream = new Color(1f, 0.96f, 0.85f, 1f);
        static readonly Color Yellow = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.15f, 1f);
        static readonly Color Green = new Color(0.42f, 0.8f, 0.28f, 1f);
        static readonly Color Blue = new Color(0.3f, 0.6f, 0.95f, 1f);
        static readonly Color Red = new Color(0.93f, 0.3f, 0.24f, 1f);
        static readonly Color Brown = new Color(0.55f, 0.33f, 0.16f, 1f);

        Font font;
        GUIStyle lbl;
        Texture2D vignette, glossTex, bullet, bulletEmpty, clock, star, starGrey, trophy, genericMouse;
        Texture2D[] mouseIcons = new Texture2D[0];
        float S = 1f, W = 1920f;
        Sub sub;
        readonly List<Pop> pops = new List<Pop>();
        readonly List<Confetti> confetti = new List<Confetti>();
        string bannerTitle = "", bannerSub = "";
        float bannerBorn = -10f;
        float hitTime = -10f;
        bool hitHead;
        float comboTime = -10f;
        int comboN;
        float comboMult;
        float displayScore;
        bool onTarget;
        Rect pauseRect;
        Vector2 lastMouse;
        string hoverNow, hoverLast;
        bool dragSens;
        RatState lastState = (RatState)(-1);
        float shotPulse;
        int dingMask;

        public bool InSubPanel { get { return sub != Sub.None; } }
        public void CloseSubPanel() { sub = Sub.None; RatAudio.Play2D(RatSound.Click, 0.6f); }
        public bool PointerOverPauseButton { get { return pauseRect.Contains(lastMouse); } }

        // ------------------------------------------------------------------ events from the game

        public void OnLevelStart()
        {
            pops.Clear();
            bannerBorn = -10f;
            hitTime = -10f;
            comboTime = -10f;
            sub = Sub.None;
        }

        public void Banner(string title, string subtitle)
        {
            bannerTitle = title;
            bannerSub = subtitle;
            bannerBorn = Time.unscaledTime;
        }

        public void Popup(Vector3 world, string text, Color color, float size)
        {
            pops.Add(new Pop { world = world, text = text, color = color, born = Time.unscaledTime, size = size });
            if (pops.Count > 20) pops.RemoveAt(0);
        }

        public void HitMarker(bool head) { hitTime = Time.unscaledTime; hitHead = head; }

        public void ComboPop(int n, float mult) { comboN = n; comboMult = mult; comboTime = Time.unscaledTime; }

        // ------------------------------------------------------------------ setup

        void Start()
        {
            if (game == null) game = RatShooterGame.Instance;
            font = PickFont();
            vignette = RatUITex.Vignette(256);
            glossTex = new Texture2D(1, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < 16; i++) glossTex.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 0.5f, i / 15f)));
            glossTex.Apply();
            bullet = RatUITex.Bullet(true);
            bulletEmpty = RatUITex.Bullet(false);
            clock = RatUITex.Clock();
            star = RatUITex.StarIcon(new Color(1f, 0.85f, 0.2f), Dark);
            starGrey = RatUITex.StarIcon(new Color(0.55f, 0.5f, 0.45f), new Color(0.3f, 0.25f, 0.22f));
            trophy = RatUITex.Trophy();
            MouseVariant[] vs = game != null && game.spawner != null ? game.spawner.variants : MouseVariant.Defaults();
            mouseIcons = new Texture2D[vs.Length];
            for (int i = 0; i < vs.Length; i++) mouseIcons[i] = RatUITex.MouseIcon(vs[i].furColor, vs[i].bellyColor, vs[i].pinkColor, vs[i].eyeColor);
            genericMouse = mouseIcons.Length > 0 ? mouseIcons[0] : RatUITex.MouseIcon(Color.grey, Color.white, Color.magenta, Color.black);
        }

        /// <summary>Runtime UI art and the OS font are not scene objects: free them when the scene unloads.</summary>
        void OnDestroy()
        {
            lbl = null;
            Texture2D[] own = { vignette, glossTex, bullet, bulletEmpty, clock, star, starGrey, trophy };
            foreach (Texture2D t in own) if (t != null) Destroy(t);
            foreach (Texture2D t in mouseIcons) if (t != null && t != genericMouse) Destroy(t);
            if (genericMouse != null) Destroy(genericMouse);
            if (font != null) Destroy(font);
        }

        /// <summary>A chunky rounded OS font if one is installed (only names the OS actually reports, so no font warnings).</summary>
        static Font PickFont()
        {
            string[] prefs = { "Arial Rounded MT Bold", "Segoe UI Bold", "Bahnschrift", "Trebuchet MS Bold", "Verdana Bold", "Arial Bold", "Arial" };
            string[] installed;
            try { installed = Font.GetOSInstalledFontNames(); }
            catch (System.Exception) { return null; }
            if (installed == null) return null;
            foreach (string p in prefs)
                if (System.Array.IndexOf(installed, p) >= 0)
                {
                    try { return Font.CreateDynamicFontFromOSFont(p, 32); }
                    catch (System.Exception) { }
                }
            return null;
        }

        void Update()
        {
            onTarget = false;
            if (game == null || cam == null) return;
            if (game.State == RatState.Playing || game.State == RatState.Countdown)
            {
                Ray r = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(r, out hit, 160f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    MouseAI m = hit.collider.GetComponentInParent<MouseAI>();
                    onTarget = m != null && m.IsAlive;
                }
            }
            if (game.gun != null && Time.time - game.gun.LastShotTime < 0.05f) shotPulse = 1f;
            shotPulse = Mathf.MoveTowards(shotPulse, 0f, Time.unscaledDeltaTime * 4f);
            displayScore = Mathf.MoveTowards(displayScore, game.Score, Mathf.Max(60f, Mathf.Abs(game.Score - displayScore) * 6f) * Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------------ drawing helpers

        Rect R(float x, float y, float w, float h) { return new Rect(x * S, y * S, w * S, h * S); }

        static bool Repaint { get { return Event.current.type == EventType.Repaint; } }

        static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }
        static Color Light(Color c, float k) { return new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a); }
        static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }

        void RoundFill(Rect r, Color c, float radius)
        {
            if (!Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        void RoundBorder(Rect r, Color c, float width, float radius)
        {
            if (!Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        }

        void Gloss(Rect r, float radius, float strength)
        {
            if (!Repaint) return;
            Rect g = new Rect(r.x + 3f * S, r.y + 3f * S, r.width - 6f * S, r.height * 0.5f);
            float rad = Mathf.Max(0f, radius - 3f * S);
            GUI.DrawTexture(g, glossTex, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, strength), Vector4.zero, new Vector4(rad, rad, 0f, 0f));
        }

        /// <summary>Chunky cartoon panel: drop shadow, fill, gloss, thick outline.</summary>
        void Panel(Rect r, Color fill, Color edge, float radius = 22f, bool gloss = true)
        {
            float rad = radius * S;
            RoundFill(new Rect(r.x, r.y + 7f * S, r.width, r.height), new Color(0f, 0f, 0f, 0.28f * fill.a), rad);
            RoundFill(r, fill, rad);
            if (gloss) Gloss(r, rad, 0.35f * fill.a);
            RoundBorder(r, edge, Mathf.Max(2f, 4f * S), rad);
        }

        void Icon(Rect r, Texture tex, Color tint)
        {
            if (!Repaint || tex == null) return;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
        }

        void EnsureStyle()
        {
            if (lbl != null) return;
            lbl = new GUIStyle(GUI.skin.label);
            if (font != null) lbl.font = font;
            lbl.fontStyle = font != null ? FontStyle.Normal : FontStyle.Bold;
            lbl.wordWrap = false;
            lbl.richText = false;
            lbl.clipping = TextClipping.Overflow;
            lbl.padding = new RectOffset(0, 0, 0, 0);
            lbl.margin = new RectOffset(0, 0, 0, 0);
        }

        static readonly Vector2[] Dirs =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f)
        };

        /// <summary>Outlined cartoon text (thick dark outline + soft drop shadow).</summary>
        void Txt(Rect r, string s, int size, Color fill, TextAnchor anchor, float outline = 3f, bool wrap = false, Color? outlineColor = null)
        {
            if (!Repaint || string.IsNullOrEmpty(s)) return;
            int px = Mathf.Max(6, Mathf.RoundToInt(size * S));
            if (px > 36) px = px / 4 * 4;
            lbl.fontSize = px;
            lbl.alignment = anchor;
            lbl.wordWrap = wrap;
            float w = outline * S;
            Color oc = outlineColor ?? Dark;
            if (outline > 0f)
            {
                lbl.normal.textColor = new Color(0f, 0f, 0f, 0.3f * fill.a);
                GUI.Label(new Rect(r.x, r.y + w + 2f * S, r.width, r.height), s, lbl);
                lbl.normal.textColor = A(oc, fill.a);
                for (int i = 0; i < Dirs.Length; i++) GUI.Label(new Rect(r.x + Dirs[i].x * w, r.y + Dirs[i].y * w, r.width, r.height), s, lbl);
            }
            lbl.normal.textColor = fill;
            GUI.Label(r, s, lbl);
        }

        /// <summary>Glossy pill button with a 3D base, hover grow and click sound.</summary>
        bool Btn(Rect r, string text, Color col, int size = 32, bool enabled = true)
        {
            Vector2 m = Event.current.mousePosition;
            bool hover = enabled && Cursor.lockState != CursorLockMode.Locked && r.Contains(m);
            if (hover && Repaint) hoverNow = text;
            bool down = hover && GUIUtility.hotControl != 0;
            float grow = hover ? 1.04f : 1f;
            Rect rr = new Rect(r.center.x - r.width * grow * 0.5f, r.center.y - r.height * grow * 0.5f + (down ? 3f * S : 0f), r.width * grow, r.height * grow);
            float rad = Mathf.Min(rr.height * 0.5f, 34f * S);
            Color body = enabled ? (hover ? Light(col, 0.18f) : col) : new Color(0.6f, 0.58f, 0.55f, 1f);
            RoundFill(new Rect(rr.x, rr.y + 8f * S, rr.width, rr.height), new Color(0f, 0f, 0f, 0.25f), rad);
            RoundFill(new Rect(rr.x, rr.y + 5f * S, rr.width, rr.height), Shade(body, 0.62f), rad);
            RoundFill(rr, body, rad);
            Gloss(rr, rad, 0.4f);
            RoundBorder(rr, Shade(body, 0.4f), Mathf.Max(2f, 3.5f * S), rad);
            Txt(new Rect(rr.x, rr.y - 1f * S, rr.width, rr.height), text, size, enabled ? Color.white : new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, 3f, false, Shade(body, 0.35f));
            if (enabled && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                RatAudio.Play2D(RatSound.Click, 0.75f);
                return true;
            }
            return false;
        }

        void Fill(Rect r, Color c)
        {
            if (!Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        Texture2D IconFor(string id)
        {
            if (game != null && game.spawner != null)
                for (int i = 0; i < game.spawner.variants.Length && i < mouseIcons.Length; i++)
                    if (game.spawner.variants[i].id == id) return mouseIcons[i];
            return genericMouse;
        }

        // ------------------------------------------------------------------ main

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyle();
            S = Screen.height / 1080f;
            W = Screen.width / S;
            lastMouse = Event.current.mousePosition;
            if (Repaint) hoverNow = null;

            if (game.State != lastState)
            {
                if (game.State == RatState.Victory) SpawnConfetti();
                if (game.State == RatState.Title || game.State == RatState.Countdown) displayScore = game.Score;
                lastState = game.State;
                dingMask = 0;
            }

            switch (game.State)
            {
                case RatState.Title:
                    if (sub == Sub.HowTo) { DrawDim(0.55f); DrawHowTo(true); }
                    else DrawTitle();
                    break;
                case RatState.Countdown:
                    DrawHud();
                    DrawCountdown();
                    break;
                case RatState.Playing:
                    DrawHud();
                    break;
                case RatState.Paused:
                    DrawHud();
                    DrawDim(0.55f);
                    if (sub == Sub.HowTo) DrawHowTo(false); else DrawPause();
                    break;
                case RatState.LevelComplete:
                    DrawDim(0.45f);
                    DrawLevelComplete();
                    break;
                case RatState.GameOver:
                    DrawDim(0.55f);
                    DrawGameOver();
                    break;
                case RatState.Victory:
                    DrawDim(0.45f);
                    DrawVictory();
                    break;
            }

            if (Repaint)
            {
                if (hoverNow != null && hoverNow != hoverLast) RatAudio.Play2D(RatSound.Hover, 0.35f);
                hoverLast = hoverNow;
            }
        }

        void DrawDim(float a)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.05f, 0.03f, 0.02f, a * 0.6f));
            if (Repaint) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0f, new Color(0.1f, 0.05f, 0.02f, a), 0f, 0f);
        }

        // ------------------------------------------------------------------ title

        void DrawTitle()
        {
            float t = Time.unscaledTime;
            if (Repaint) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0f, new Color(0.05f, 0.04f, 0.08f, 0.5f), 0f, 0f);
            float cx = W * 0.5f;
            float intro = Mathf.Clamp01(game.StateTime / 0.6f);
            float pop = 1f + Mathf.Sin(intro * Mathf.PI) * 0.12f;

            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Sin(t * 1.3f) * 2f, new Vector2(cx * S, 240f * S));
            Icon(R(cx - 520f, 95f + Mathf.Sin(t * 3f) * 8f, 190f, 190f), genericMouse, Color.white);
            Txt(R(cx - 600f, 70f - 20f * (pop - 1f), 1200f, 170f), "RAT", Mathf.RoundToInt(150 * pop), Yellow, TextAnchor.MiddleCenter, 8f);
            Txt(R(cx - 700f, 200f, 1400f, 150f), "SHOOTER", Mathf.RoundToInt(118 * pop), Orange, TextAnchor.MiddleCenter, 8f);
            GUI.matrix = old;

            Txt(R(cx - 700f, 345f, 1400f, 50f), "Clear the farm of sneaky cartoon mice!", 34, Color.white, TextAnchor.MiddleCenter, 3f);

            int best = RatShooterGame.BestScore;
            Rect badge = R(cx - 190f, 405f, 380f, 56f);
            Panel(badge, Yellow, Dark, 28f);
            Icon(R(cx - 178f, 405f, 56f, 56f), star, Color.white);
            Txt(R(cx - 120f, 405f, 300f, 56f), "BEST  " + best.ToString("N0"), 30, Dark, TextAnchor.MiddleCenter, 0f);

            float bx = cx - 270f;
            if (Btn(R(bx, 490f, 540f, 92f), "PLAY!", Green, 48)) game.StartGame();
            if (Btn(R(bx, 602f, 540f, 72f), "HOW TO PLAY", Blue, 32)) { sub = Sub.HowTo; }

            // difficulty selector
            if (Btn(R(bx, 694f, 80f, 72f), "<", Orange, 36)) game.SetDifficulty((RatDifficulty)(((int)game.Difficulty + 2) % 3));
            Panel(R(bx + 96f, 694f, 348f, 72f), Cream, Dark, 30f);
            Txt(R(bx + 96f, 694f, 348f, 72f), RatDifficultyInfo.Name(game.Difficulty), 34, Orange, TextAnchor.MiddleCenter, 3f);
            if (Btn(R(bx + 460f, 694f, 80f, 72f), ">", Orange, 36)) game.SetDifficulty((RatDifficulty)(((int)game.Difficulty + 1) % 3));
            Txt(R(cx - 500f, 770f, 1000f, 34f), RatDifficultyInfo.Blurb(game.Difficulty), 22, Color.white, TextAnchor.MiddleCenter, 2f);

            if (Btn(R(bx, 818f, 540f, 72f), "BACK TO MAIN MENU", Red, 30)) game.ReturnToMainMenu();

            Txt(R(cx - 900f, 1018f, 1800f, 36f), "Mouse: aim     Left click: shoot     Right click: aim down sights     R: reload     Esc: pause     Enter: play", 22, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, 2f);
        }

        // ------------------------------------------------------------------ how to play

        void KeyCap(float x, float y, string key, float w = 120f)
        {
            Rect r = R(x, y, w, 46f);
            RoundFill(new Rect(r.x, r.y + 5f * S, r.width, r.height), Shade(Cream, 0.65f), 10f * S);
            RoundFill(r, Color.white, 10f * S);
            RoundBorder(r, Dark, 2.5f * S, 10f * S);
            Txt(r, key, 22, Dark, TextAnchor.MiddleCenter, 0f);
        }

        void DrawHowTo(bool fromTitle)
        {
            float cx = W * 0.5f;
            Rect pr = R(cx - 780f, 120f, 1560f, 850f);
            Panel(pr, Cream, Dark, 30f);
            Txt(R(cx - 780f, 135f, 1560f, 80f), "HOW TO PLAY", 60, Orange, TextAnchor.MiddleCenter, 5f);

            // controls
            float x = cx - 740f, y = 235f;
            Txt(R(x, y, 600f, 44f), "CONTROLS", 34, Blue, TextAnchor.MiddleLeft, 3f);
            string[] keys = { "MOUSE", "LEFT CLICK", "RIGHT CLICK", KeyBindings.KeyFor(GameAction.Reload), "ESC / P", "ENTER" };
            string[] acts = { "Aim (smooth camera)", "Shoot", "Aim down the sights (zoom, no spread)", "Reload (8-round magazine)", "Pause", "Start / continue" };
            for (int i = 0; i < keys.Length; i++)
            {
                KeyCap(x, y + 58f + i * 60f, keys[i], 190f);
                Txt(R(x + 210f, y + 58f + i * 60f, 480f, 46f), acts[i], 24, Dark, TextAnchor.MiddleLeft, 0f);
            }
            Txt(R(x, y + 430f, 700f, 200f),
                "- Catch every mouse before the clock runs out.\n- Missed shots scare mice: they hop, squeak and run to hide behind barrels, crates, hay, bushes or dive into burrows - then peek out again.\n- Hits in a row build a COMBO (up to x3). Head shots +50%.\n- Leftover time, bullets and accuracy give bonus points.\n- Run out of time or bullets and it's game over!",
                21, Dark, TextAnchor.UpperLeft, 0f, true);

            // mice
            float mx = cx + 20f;
            Txt(R(mx, y, 700f, 44f), "THE MICE", 34, Green, TextAnchor.MiddleLeft, 3f);
            MouseVariant[] vs = game.spawner != null ? game.spawner.variants : MouseVariant.Defaults();
            for (int i = 0; i < vs.Length && i < 6; i++)
            {
                float ry = y + 58f + i * 102f;
                RoundFill(R(mx, ry, 740f, 92f), new Color(1f, 1f, 1f, 0.55f), 18f * S);
                Icon(R(mx + 8f, ry + 4f, 84f, 84f), i < mouseIcons.Length ? mouseIcons[i] : genericMouse, Color.white);
                Txt(R(mx + 104f, ry + 8f, 420f, 40f), vs[i].displayName.ToUpperInvariant(), 28, Dark, TextAnchor.MiddleLeft, 0f);
                Txt(R(mx + 104f, ry + 48f, 520f, 36f), vs[i].trait, 21, Brown, TextAnchor.MiddleLeft, 0f);
                Rect pb = R(mx + 580f, ry + 22f, 140f, 48f);
                Panel(pb, Yellow, Dark, 24f, false);
                Txt(pb, vs[i].points + " PTS", 24, Dark, TextAnchor.MiddleCenter, 0f);
            }

            if (fromTitle)
            {
                if (Btn(R(cx - 420f, 880f, 380f, 72f), "BACK", Red, 32)) CloseSubPanel();
                if (Btn(R(cx + 40f, 880f, 380f, 72f), "PLAY!", Green, 36)) { sub = Sub.None; game.StartGame(); }
            }
            else if (Btn(R(cx - 190f, 880f, 380f, 72f), "BACK", Red, 32)) CloseSubPanel();
        }

        // ------------------------------------------------------------------ countdown

        void DrawCountdown()
        {
            float cx = W * 0.5f;
            RatLevel lvl = game.Level;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(game.StateTime / 0.45f));
            float y0 = Mathf.Lerp(-260f, 190f, slide);
            Rect card = R(cx - 470f, y0, 940f, 200f);
            Panel(card, Cream, Dark, 30f);
            Txt(R(cx - 470f, y0 + 10f, 940f, 40f), "LEVEL " + (game.LevelIndex + 1) + " OF " + game.levels.Length + "   -   " + RatDifficultyInfo.Name(game.Difficulty), 26, Orange, TextAnchor.MiddleCenter, 2f);
            Txt(R(cx - 470f, y0 + 48f, 940f, 70f), lvl.title.ToUpperInvariant(), 56, Dark, TextAnchor.MiddleCenter, 0f);
            Txt(R(cx - 470f, y0 + 118f, 940f, 34f), lvl.subtitle, 24, Brown, TextAnchor.MiddleCenter, 0f);
            int ammo = game.gun != null ? game.gun.TotalAmmo : 0;
            Txt(R(cx - 470f, y0 + 152f, 940f, 38f), "Catch " + lvl.mice + " mice   |   " + Clock(game.TimeLeft) + "   |   " + ammo + " bullets", 26, Dark, TextAnchor.MiddleCenter, 0f);

            float c = game.CountdownLeft;
            if (c > 3f) return;
            int n = Mathf.CeilToInt(c);
            float frac = c - Mathf.Floor(c);
            if (n <= 0) return;
            float scale = 1f + 0.7f * Mathf.Pow(frac, 5f);
            Color col = n == 3 ? Green : (n == 2 ? Yellow : Orange);
            int size = Mathf.RoundToInt(220 * scale);
            Txt(R(cx - 400f, 430f, 800f, 300f), n.ToString(), size, col, TextAnchor.MiddleCenter, 10f);
            Txt(R(cx - 400f, 700f, 800f, 60f), "GET READY!", 40, Color.white, TextAnchor.MiddleCenter, 4f);
        }

        // ------------------------------------------------------------------ in-game HUD

        void DrawHud()
        {
            float t = Time.unscaledTime;
            CartoonGun gun = game.gun;

            // low-time warning glow
            if (game.State == RatState.Playing && game.TimeLeft < 10f && Repaint)
            {
                float a = (Mathf.Sin(t * 8f) * 0.5f + 0.5f) * 0.45f;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.1f, 0.05f, a), 0f, 0f);
            }

            // ---- score
            Rect sp = R(28f, 22f, 340f, 112f);
            Panel(sp, Yellow, Dark, 26f);
            Icon(R(40f, 36f, 84f, 84f), star, Color.white);
            Txt(R(132f, 32f, 230f, 30f), "SCORE", 22, Dark, TextAnchor.MiddleLeft, 0f);
            Txt(R(132f, 58f, 230f, 64f), Mathf.RoundToInt(displayScore).ToString("N0"), 50, Dark, TextAnchor.MiddleLeft, 0f);
            if (game.Combo >= 2)
            {
                float since = t - comboTime;
                float bump = since < 0.35f ? 1f + Mathf.Sin(since / 0.35f * Mathf.PI) * 0.25f : 1f;
                Rect cr = R(38f, 146f, 260f * bump, 54f * bump);
                Panel(cr, Orange, Dark, 27f);
                Txt(cr, "COMBO x" + (1f + Mathf.Min(game.Combo - 1, 4) * 0.5f).ToString("0.0"), Mathf.RoundToInt(28 * bump), Color.white, TextAnchor.MiddleCenter, 3f);
            }

            // ---- timer + level
            float cx = W * 0.5f;
            bool low = game.TimeLeft < 10f && game.State == RatState.Playing;
            float tb = low ? 1f + Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f)) * 0.08f : 1f;
            Rect tr = R(cx - 160f * tb, 20f, 320f * tb, 100f * tb);
            Panel(tr, low ? new Color(1f, 0.78f, 0.72f) : Cream, Dark, 28f);
            Icon(R(cx - 150f * tb, 30f, 76f * tb, 76f * tb), clock, Color.white);
            Txt(R(cx - 70f * tb, 20f, 220f * tb, 100f * tb), Clock(game.TimeLeft), Mathf.RoundToInt(60 * tb), low ? Red : Dark, TextAnchor.MiddleCenter, low ? 3f : 0f, false, Color.white);
            Rect lr = R(cx - 230f, 128f, 460f, 46f);
            Panel(lr, new Color(0.45f, 0.27f, 0.13f, 0.95f), Dark, 23f, false);
            Txt(lr, "LEVEL " + (game.LevelIndex + 1) + "  -  " + game.Level.title.ToUpperInvariant(), 22, Color.white, TextAnchor.MiddleCenter, 2f);

            // ---- mice left + pause button
            Rect mp = R(W - 28f - 120f - 330f, 22f, 330f, 112f);
            Panel(mp, Green, Dark, 26f);
            Icon(R(W - 28f - 120f - 322f, 28f, 100f, 100f), genericMouse, Color.white);
            Txt(R(W - 28f - 120f - 215f, 32f, 210f, 30f), "MICE LEFT", 22, Dark, TextAnchor.MiddleLeft, 0f);
            Txt(R(W - 28f - 120f - 215f, 58f, 210f, 64f), game.MiceRemaining + " / " + game.Level.mice, 48, Color.white, TextAnchor.MiddleLeft, 3f);

            Rect pb = R(W - 28f - 104f, 26f, 104f, 104f);
            pauseRect = pb;
            bool hover = Cursor.lockState != CursorLockMode.Locked && pb.Contains(Event.current.mousePosition);
            Color pc = hover ? Light(Orange, 0.2f) : Orange;
            RoundFill(new Rect(pb.x, pb.y + 6f * S, pb.width, pb.height), Shade(pc, 0.6f), pb.width * 0.5f);
            RoundFill(pb, pc, pb.width * 0.5f);
            Gloss(pb, pb.width * 0.5f, 0.4f);
            RoundBorder(pb, Dark, 4f * S, pb.width * 0.5f);
            RoundFill(R(W - 28f - 104f + 30f, 50f, 15f, 46f), Color.white, 5f * S);
            RoundFill(R(W - 28f - 104f + 59f, 50f, 15f, 46f), Color.white, 5f * S);
            Txt(R(W - 28f - 104f, 132f, 104f, 26f), "ESC", 18, Color.white, TextAnchor.MiddleCenter, 2f);
            if (game.State == RatState.Playing || game.State == RatState.Countdown)
                if (GUI.Button(pb, GUIContent.none, GUIStyle.none)) game.Pause();

            // ---- ammo
            if (gun != null)
            {
                int mag = gun.magazineSize;
                float aw = 54f + mag * 34f + 190f;
                Rect ap = R(W - 28f - aw, 1080f - 28f - 128f, aw, 128f);
                Panel(ap, Cream, Dark, 26f);
                for (int i = 0; i < mag; i++)
                {
                    bool full = i < gun.Ammo;
                    float bounce = full && i == gun.Ammo - 1 && gun.Reloading ? 0f : 0f;
                    Icon(R(W - 28f - aw + 26f + i * 34f, 1080f - 28f - 128f + 20f + bounce, 30f, 76f), full ? bullet : bulletEmpty, Color.white);
                }
                float tx = W - 28f - 190f;
                Txt(R(tx, 1080f - 28f - 122f, 170f, 30f), "AMMO", 20, Brown, TextAnchor.MiddleCenter, 0f);
                Txt(R(tx, 1080f - 28f - 96f, 170f, 60f), gun.Ammo.ToString(), 52, gun.Ammo == 0 ? Red : Dark, TextAnchor.MiddleCenter, 0f);
                Txt(R(tx, 1080f - 28f - 42f, 170f, 30f), "+ " + gun.Reserve + " spare", 20, Brown, TextAnchor.MiddleCenter, 0f);

                if (gun.Reloading)
                {
                    Rect bar = R(W - 28f - aw + 20f, 1080f - 28f - 128f - 44f, aw - 40f, 30f);
                    Panel(bar, new Color(0.3f, 0.18f, 0.09f, 0.95f), Dark, 15f, false);
                    Rect fillR = new Rect(bar.x + 4f * S, bar.y + 4f * S, (bar.width - 8f * S) * gun.ReloadProgress, bar.height - 8f * S);
                    RoundFill(fillR, Yellow, 11f * S);
                    Txt(bar, "RELOADING...", 20, Color.white, TextAnchor.MiddleCenter, 2f);
                }
                else if (game.State == RatState.Playing && gun.Ammo == 0)
                {
                    bool blink = Mathf.Repeat(t, 0.6f) < 0.4f;
                    string msg = gun.Reserve > 0 ? "PRESS " + KeyBindings.KeyFor(GameAction.Reload) + " TO RELOAD" : "OUT OF AMMO!";
                    if (blink) Txt(R(W - 28f - 520f, 1080f - 28f - 128f - 54f, 520f, 44f), msg, 30, gun.Reserve > 0 ? Yellow : Red, TextAnchor.MiddleRight, 4f);
                }
            }

            Txt(R(30f, 1080f - 52f, 1000f, 34f), "RIGHT CLICK aim    " + KeyBindings.KeyFor(GameAction.Reload) + " reload    ESC pause", 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft, 2f);

            DrawAlarms();
            DrawPopups();
            DrawCrosshair();
            DrawBanner();
        }

        void DrawCrosshair()
        {
            if (!Repaint) return;
            CartoonGun gun = game.gun;
            float aim = gun != null ? gun.AimBlend : 0f;
            float cxp = Screen.width * 0.5f, cyp = Screen.height * 0.5f;
            Color col = onTarget ? new Color(1f, 0.32f, 0.22f) : Color.white;
            float alpha = Mathf.Lerp(1f, 0.15f, Mathf.SmoothStep(0.4f, 1f, aim));
            float gap = (9f + shotPulse * 16f + (1f - aim) * 6f + (onTarget ? -2f : 0f)) * S;
            float len = 15f * S, th = 5f * S;
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = i == 0 ? Vector2.up : (i == 1 ? Vector2.down : (i == 2 ? Vector2.left : Vector2.right));
                Vector2 c = new Vector2(cxp, cyp) + d * (gap + len * 0.5f);
                bool vert = i < 2;
                Rect r = new Rect(c.x - (vert ? th : len) * 0.5f, c.y - (vert ? len : th) * 0.5f, vert ? th : len, vert ? len : th);
                RoundFill(new Rect(r.x - 2f * S, r.y - 2f * S, r.width + 4f * S, r.height + 4f * S), new Color(0.15f, 0.08f, 0.04f, 0.85f * alpha), th);
                RoundFill(r, A(col, alpha), th * 0.5f);
            }
            float ds = 7f * S;
            RoundFill(new Rect(cxp - ds * 0.5f - 2f * S, cyp - ds * 0.5f - 2f * S, ds + 4f * S, ds + 4f * S), new Color(0.15f, 0.08f, 0.04f, 0.85f), ds);
            RoundFill(new Rect(cxp - ds * 0.5f, cyp - ds * 0.5f, ds, ds), col, ds * 0.5f);

            // hit marker
            float since = Time.unscaledTime - hitTime;
            if (since < 0.3f)
            {
                float a = 1f - since / 0.3f;
                Color hc = hitHead ? new Color(1f, 0.35f, 0.2f, a) : new Color(1f, 0.92f, 0.3f, a);
                Matrix4x4 old = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cxp, cyp));
                float hg = (14f + since * 30f) * S, hl = 16f * S, ht = 5f * S;
                RoundFill(new Rect(cxp - ht * 0.5f, cyp - hg - hl, ht, hl), hc, ht * 0.5f);
                RoundFill(new Rect(cxp - ht * 0.5f, cyp + hg, ht, hl), hc, ht * 0.5f);
                RoundFill(new Rect(cxp - hg - hl, cyp - ht * 0.5f, hl, ht), hc, ht * 0.5f);
                RoundFill(new Rect(cxp + hg, cyp - ht * 0.5f, hl, ht), hc, ht * 0.5f);
                GUI.matrix = old;
            }
        }

        bool ToScreen(Vector3 world, out Vector2 gui)
        {
            gui = Vector2.zero;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        void DrawAlarms()
        {
            if (game.spawner == null) return;
            List<MouseAI> act = game.spawner.Active;
            for (int i = 0; i < act.Count; i++)
            {
                MouseAI m = act[i];
                if (m == null || !m.IsVisible) continue;
                float age = Time.time - m.AlarmTime;
                if (age < 0f || age > 0.9f) continue;
                Vector2 p;
                if (!ToScreen(m.HeadPosition + Vector3.up * 0.45f, out p)) continue;
                float pop = age < 0.15f ? age / 0.15f * 1.25f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.15f) / 0.15f));
                float a = Mathf.Clamp01((0.9f - age) / 0.2f);
                float sz = 40f * S * pop;
                Rect r = new Rect(p.x - sz * 0.5f, p.y - sz * 0.5f, sz, sz);
                RoundFill(r, new Color(1f, 1f, 1f, a), sz * 0.5f);
                RoundBorder(r, A(Dark, a), 3f * S, sz * 0.5f);
                Txt(new Rect(r.x, r.y - 2f * S, r.width, r.height), "!", Mathf.RoundToInt(30 * pop), A(Red, a), TextAnchor.MiddleCenter, 0f);
            }
        }

        void DrawPopups()
        {
            float now = Time.unscaledTime;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                Pop p = pops[i];
                float age = now - p.born;
                if (age > 1.2f) { pops.RemoveAt(i); continue; }
                Vector2 sp;
                if (!ToScreen(p.world + Vector3.up * (age * 0.9f), out sp)) continue;
                float pop = age < 0.12f ? Mathf.Lerp(0.4f, 1.3f, age / 0.12f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((age - 0.12f) / 0.15f));
                float a = Mathf.Clamp01((1.2f - age) / 0.4f);
                int size = Mathf.RoundToInt(44 * p.size * pop);
                Txt(new Rect(sp.x - 300f * S, sp.y - 40f * S, 600f * S, 80f * S), p.text, size, A(p.color, a), TextAnchor.MiddleCenter, 4f);
            }
        }

        void DrawBanner()
        {
            float age = Time.unscaledTime - bannerBorn;
            if (age > 2.3f || string.IsNullOrEmpty(bannerTitle)) return;
            float cx = W * 0.5f;
            float pop = age < 0.15f ? Mathf.Lerp(0.5f, 1.15f, age / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - 0.15f) / 0.2f));
            float a = Mathf.Clamp01((2.3f - age) / 0.4f);
            Txt(R(cx - 700f, 300f, 1400f, 100f), bannerTitle, Mathf.RoundToInt(70 * pop), A(Yellow, a), TextAnchor.MiddleCenter, 6f);
            if (!string.IsNullOrEmpty(bannerSub))
                Txt(R(cx - 700f, 390f, 1400f, 50f), bannerSub, 30, A(Color.white, a), TextAnchor.MiddleCenter, 3f);
        }

        // ------------------------------------------------------------------ pause

        void DrawPause()
        {
            float cx = W * 0.5f;
            Panel(R(cx - 340f, 150f, 680f, 800f), Cream, Dark, 34f);
            Txt(R(cx - 340f, 168f, 680f, 100f), "PAUSED", 78, Orange, TextAnchor.MiddleCenter, 6f);
            float bx = cx - 260f;
            if (Btn(R(bx, 300f, 520f, 80f), "RESUME", Green, 36)) game.Resume();
            if (Btn(R(bx, 398f, 520f, 76f), "RESTART LEVEL", Orange, 32)) game.RetryLevel();
            if (Btn(R(bx, 492f, 520f, 76f), "HOW TO PLAY", Blue, 32)) sub = Sub.HowTo;
            if (Btn(R(bx, 586f, 520f, 76f), "MAIN MENU", Red, 32)) game.ReturnToMainMenu();

            // mouse sensitivity (shared with Neon Breach settings)
            Txt(R(bx, 690f, 520f, 36f), "MOUSE SENSITIVITY  " + GameSettings.Sensitivity.ToString("0.00"), 24, Dark, TextAnchor.MiddleCenter, 0f);
            Rect track = R(bx + 20f, 736f, 480f, 26f);
            float v = Mathf.InverseLerp(0.2f, 3f, GameSettings.Sensitivity);
            RoundFill(track, new Color(0.35f, 0.22f, 0.12f), 13f * S);
            RoundFill(new Rect(track.x + 4f * S, track.y + 4f * S, (track.width - 8f * S) * v, track.height - 8f * S), Yellow, 9f * S);
            Rect knob = new Rect(track.x + track.width * v - 20f * S, track.y - 9f * S, 40f * S, 44f * S);
            RoundFill(knob, Color.white, 14f * S);
            RoundBorder(knob, Dark, 3f * S, 14f * S);
            Event e = Event.current;
            Rect hit = new Rect(track.x - 20f * S, track.y - 16f * S, track.width + 40f * S, track.height + 32f * S);
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { dragSens = true; e.Use(); }
            if (e.type == EventType.MouseUp && dragSens) { dragSens = false; GameSettings.Save(); e.Use(); }
            if (dragSens && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
            {
                float nv = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                GameSettings.Sensitivity = Mathf.Round(Mathf.Lerp(0.2f, 3f, nv) * 20f) / 20f;
                e.Use();
            }

            Txt(R(cx - 340f, 800f, 680f, 40f), "Level " + (game.LevelIndex + 1) + "   |   Score " + game.Score.ToString("N0") + "   |   " + Clock(game.TimeLeft) + " left", 24, Brown, TextAnchor.MiddleCenter, 0f);
            Txt(R(cx - 340f, 845f, 680f, 40f), game.MiceRemaining + " mice to go   |   " + (game.gun != null ? game.gun.TotalAmmo : 0) + " bullets", 24, Brown, TextAnchor.MiddleCenter, 0f);
            Txt(R(cx - 340f, 892f, 680f, 34f), "Press ESC to resume", 20, Dark, TextAnchor.MiddleCenter, 0f);
        }

        // ------------------------------------------------------------------ results

        void StarRow(float cx, float y, int earned, float startTime)
        {
            for (int i = 0; i < 3; i++)
            {
                float appear = startTime + 0.35f + i * 0.3f;
                float age = game.StateTime - appear;
                bool got = i < earned;
                float s = 1f;
                if (age < 0f) s = got ? 0f : 1f;
                else if (got && age < 0.25f) s = Mathf.Lerp(0.2f, 1.35f, age / 0.25f);
                else if (got && age < 0.4f) s = Mathf.Lerp(1.35f, 1f, (age - 0.25f) / 0.15f);
                if (got && age >= 0f && Repaint && (dingMask & (1 << i)) == 0) { dingMask |= 1 << i; RatAudio.Play2D(RatSound.Ding, 0.6f, 1f + i * 0.12f); }
                float size = (i == 1 ? 150f : 120f) * s;
                float x = cx + (i - 1) * 150f;
                float yy = y + (i == 1 ? 0f : 22f);
                if (s > 0.01f) Icon(R(x - size * 0.5f, yy + (i == 1 ? 150f : 120f) * 0.5f - size * 0.5f, size, size), got ? star : starGrey, Color.white);
            }
        }

        void StatRow(float x, float y, float w, string name, string value, float appearAt, Color valueColor)
        {
            if (game.StateTime < appearAt) return;
            float k = Mathf.Clamp01((game.StateTime - appearAt) / 0.2f);
            RoundFill(R(x, y, w, 48f), new Color(1f, 1f, 1f, 0.5f * k), 16f * S);
            Txt(R(x + 20f, y, w * 0.6f, 48f), name, 26, A(Dark, k), TextAnchor.MiddleLeft, 0f);
            bool darkValue = valueColor == Dark;
            Txt(R(x + w * 0.4f, y, w * 0.6f - 20f, 48f), value, 30, A(valueColor, k), TextAnchor.MiddleRight, darkValue ? 0f : 2f, false, A(Dark, k));
        }

        void DrawLevelComplete()
        {
            float cx = W * 0.5f;
            Panel(R(cx - 440f, 90f, 880f, 900f), Cream, Dark, 34f);
            Txt(R(cx - 440f, 110f, 880f, 90f), "LEVEL " + (game.LevelIndex + 1) + " CLEAR!", 66, Green, TextAnchor.MiddleCenter, 6f);
            StarRow(cx, 200f, game.SummaryStars, 0f);
            float x = cx - 360f, w = 720f, y = 380f;
            StatRow(x, y, w, "Mice caught", game.LevelKills + " / " + game.Level.mice, 0.9f, Dark);
            StatRow(x, y + 56f, w, "Accuracy", Mathf.RoundToInt(game.LevelAccuracy * 100f) + "%", 1.05f, Dark);
            StatRow(x, y + 112f, w, "Time bonus", "+" + game.SummaryTimeBonus.ToString("N0"), 1.2f, Orange);
            StatRow(x, y + 168f, w, "Ammo bonus", "+" + game.SummaryAmmoBonus.ToString("N0"), 1.35f, Orange);
            StatRow(x, y + 224f, w, "Accuracy bonus", "+" + game.SummaryAccuracyBonus.ToString("N0"), 1.5f, Orange);
            StatRow(x, y + 280f, w, "Level score", game.SummaryLevelScore.ToString("N0"), 1.65f, Blue);
            StatRow(x, y + 336f, w, "TOTAL SCORE", game.Score.ToString("N0"), 1.8f, Green);
            if (game.StateTime > 1.9f)
            {
                RatLevel next = game.levels[Mathf.Min(game.LevelIndex + 1, game.levels.Length - 1)];
                Txt(R(cx - 440f, 776f, 880f, 40f), "Next: " + next.title + "  -  " + next.mice + " mice, faster and sneakier!", 24, Brown, TextAnchor.MiddleCenter, 0f);
            }
            if (Btn(R(cx - 400f, 850f, 390f, 84f), "NEXT LEVEL", Green, 36)) game.NextLevel();
            if (Btn(R(cx + 10f, 850f, 390f, 84f), "MAIN MENU", Red, 32)) game.ReturnToMainMenu();
        }

        void DrawGameOver()
        {
            float cx = W * 0.5f;
            float shake = game.StateTime < 0.5f ? Mathf.Sin(game.StateTime * 60f) * (0.5f - game.StateTime) * 20f : 0f;
            Panel(R(cx - 440f, 110f, 880f, 860f), Cream, Dark, 34f);
            Txt(R(cx - 440f + shake, 130f, 880f, 110f), "GAME OVER", 88, Red, TextAnchor.MiddleCenter, 7f);
            Txt(R(cx - 440f, 236f, 880f, 60f), game.GameOverReason, 46, Orange, TextAnchor.MiddleCenter, 4f);
            Icon(R(cx - 70f, 300f, 140f, 140f), genericMouse, Color.white);
            Txt(R(cx - 440f, 438f, 880f, 40f), "The mice are partying in the barn...", 24, Brown, TextAnchor.MiddleCenter, 0f);
            float x = cx - 340f, w = 680f, y = 492f;
            StatRow(x, y, w, "Score", game.Score.ToString("N0"), 0.3f, Blue);
            StatRow(x, y + 56f, w, "Level reached", (game.LevelIndex + 1) + " - " + game.Level.title, 0.4f, Dark);
            StatRow(x, y + 112f, w, "Mice caught", game.Kills.ToString(), 0.5f, Dark);
            StatRow(x, y + 168f, w, "Accuracy", Mathf.RoundToInt(game.Accuracy * 100f) + "%", 0.6f, Dark);
            StatRow(x, y + 224f, w, "Best score", RatShooterGame.BestScore.ToString("N0") + (game.NewHighScore ? "  NEW!" : ""), 0.7f, game.NewHighScore ? Orange : Dark);
            float bw = 266f;
            if (Btn(R(cx - 410f, 862f, bw, 80f), "RETRY LEVEL", Green, 28)) game.RetryLevel();
            if (Btn(R(cx - bw * 0.5f, 862f, bw, 80f), "NEW GAME", Orange, 28)) game.StartGame();
            if (Btn(R(cx + 410f - bw, 862f, bw, 80f), "MAIN MENU", Red, 28)) game.ReturnToMainMenu();
        }

        void DrawVictory()
        {
            DrawConfetti();
            float cx = W * 0.5f;
            float t = Time.unscaledTime;
            Panel(R(cx - 460f, 70f, 920f, 940f), Cream, Dark, 34f);
            float bob = Mathf.Sin(t * 2.5f) * 8f;
            float pop = Mathf.Clamp01(game.StateTime / 0.4f);
            Icon(R(cx - 110f * pop, 95f + bob, 220f * pop, 220f * pop), trophy, Color.white);
            Txt(R(cx - 460f, 312f, 920f, 100f), "FARM SAVED!", 84, Yellow, TextAnchor.MiddleCenter, 7f);
            Txt(R(cx - 460f, 400f, 920f, 44f), "You cleared all " + game.levels.Length + " levels on " + RatDifficultyInfo.Name(game.Difficulty) + "!", 28, Brown, TextAnchor.MiddleCenter, 0f);
            if (game.NewHighScore)
            {
                float s = 1f + Mathf.Sin(t * 6f) * 0.06f;
                Rect nb = R(cx - 200f * s, 452f, 400f * s, 56f * s);
                Panel(nb, Orange, Dark, 28f);
                Txt(nb, "NEW HIGH SCORE!", 30, Color.white, TextAnchor.MiddleCenter, 3f);
            }
            float x = cx - 340f, w = 680f, y = 528f;
            StatRow(x, y, w, "FINAL SCORE", game.Score.ToString("N0"), 0.5f, Green);
            StatRow(x, y + 56f, w, "Mice caught", game.Kills.ToString(), 0.65f, Dark);
            StatRow(x, y + 112f, w, "Accuracy", Mathf.RoundToInt(game.Accuracy * 100f) + "%", 0.8f, Dark);
            StatRow(x, y + 168f, w, "Head shots", game.Headshots.ToString(), 0.95f, Dark);
            StatRow(x, y + 224f, w, "Best combo", "x" + game.BestCombo, 1.1f, Orange);
            if (Btn(R(cx - 400f, 900f, 390f, 84f), "PLAY AGAIN", Green, 36)) game.StartGame();
            if (Btn(R(cx + 10f, 900f, 390f, 84f), "MAIN MENU", Red, 32)) game.ReturnToMainMenu();
        }

        void SpawnConfetti()
        {
            confetti.Clear();
            Color[] cols = { Yellow, Orange, Green, Blue, Red, new Color(0.9f, 0.4f, 0.9f) };
            for (int i = 0; i < 140; i++)
            {
                confetti.Add(new Confetti
                {
                    p = new Vector2(Random.Range(0f, Screen.width), Random.Range(-Screen.height, 0f)),
                    v = new Vector2(Random.Range(-60f, 60f), Random.Range(140f, 320f)) * S,
                    rot = Random.Range(0f, 360f),
                    vr = Random.Range(-300f, 300f),
                    w = Random.Range(10f, 20f) * S,
                    h = Random.Range(6f, 12f) * S,
                    c = cols[Random.Range(0, cols.Length)]
                });
            }
        }

        void DrawConfetti()
        {
            if (!Repaint) return;
            float dt = Time.unscaledDeltaTime;
            Matrix4x4 old = GUI.matrix;
            for (int i = 0; i < confetti.Count; i++)
            {
                Confetti c = confetti[i];
                c.p += c.v * dt + new Vector2(Mathf.Sin(Time.unscaledTime * 3f + i) * 30f * S * dt, 0f);
                c.rot += c.vr * dt;
                if (c.p.y > Screen.height + 20f) { c.p.y = -20f; c.p.x = Random.Range(0f, Screen.width); }
                GUI.matrix = old;
                GUIUtility.RotateAroundPivot(c.rot, c.p);
                GUI.DrawTexture(new Rect(c.p.x - c.w * 0.5f, c.p.y - c.h * 0.5f, c.w, c.h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c.c, 0f, 2f);
            }
            GUI.matrix = old;
        }
    }
}
