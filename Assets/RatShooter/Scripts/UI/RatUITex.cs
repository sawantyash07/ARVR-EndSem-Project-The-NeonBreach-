using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Anti-aliased procedural UI art for the cartoon HUD: rounded panels (9-slice), gloss, circles, a cartoon mouse
    /// head icon per mouse kind, bullets, clock, star, trophy and a vignette. Drawn with signed distance functions.
    /// </summary>
    public static class RatUITex
    {
        static Texture2D New(int w, int h, string name)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.name = name;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static float AA(float d) { return Mathf.Clamp01(0.5f - d); }

        static float Circle(Vector2 p, Vector2 c, float r) { return Vector2.Distance(p, c) - r; }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
        }

        static float RoundRect(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x) - half.x + r, Mathf.Abs(p.y - c.y) - half.y + r);
            Vector2 mq = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return mq.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, ba.sqrMagnitude));
            return (pa - ba * h).magnitude - r;
        }

        static float Star(Vector2 p, Vector2 c, float ro, float ri)
        {
            Vector2 d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) - Mathf.PI * 0.5f;
            float sector = Mathf.PI * 2f / 5f;
            float t = Mathf.Abs(Mathf.Repeat(ang + sector * 0.5f, sector) - sector * 0.5f) / (sector * 0.5f);
            float r = Mathf.Lerp(ro, ri, t);
            return (d.magnitude - r) * 0.8f;
        }

        static Color Over(Color dst, Color src, float a)
        {
            a *= src.a;
            float outA = a + dst.a * (1f - a);
            if (outA < 1e-5f) return new Color(0f, 0f, 0f, 0f);
            Color c = (src * a + dst * dst.a * (1f - a)) / outA;
            c.a = outA;
            return c;
        }

        // ------------------------------------------------------------------ panels

        /// <summary>White rounded rectangle for 9-slice drawing (border = radius).</summary>
        public static Texture2D Round(int size, int radius)
        {
            Texture2D t = New(size, size, "RS_UIRound");
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f), half = c;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, AA(RoundRect(new Vector2(x + 0.5f, y + 0.5f), c, half, radius))));
            t.Apply();
            return t;
        }

        public static Texture2D RoundOutline(int size, int radius, float width)
        {
            Texture2D t = New(size, size, "RS_UIRoundOutline");
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f), half = c;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRect(new Vector2(x + 0.5f, y + 0.5f), c, half, radius);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, AA(d) * AA(-d - width)));
                }
            t.Apply();
            return t;
        }

        /// <summary>Top-half glossy highlight inside a rounded rect.</summary>
        public static Texture2D Gloss(int size, int radius)
        {
            Texture2D t = New(size, size, "RS_UIGloss");
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f), half = c - Vector2.one * 3f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRect(new Vector2(x + 0.5f, y + 0.5f), c, half, radius - 3);
                    float v = (float)y / size;
                    float g = Mathf.SmoothStep(0.45f, 0.95f, v) * 0.55f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, AA(d) * g));
                }
            t.Apply();
            return t;
        }

        public static Texture2D Circle(int size)
        {
            Texture2D t = New(size, size, "RS_UICircle");
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, AA(Circle(new Vector2(x + 0.5f, y + 0.5f), c, size * 0.5f - 1f))));
            t.Apply();
            return t;
        }

        public static Texture2D Vignette(int size)
        {
            Texture2D t = New(size, size, "RS_UIVignette");
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.55f, 1.25f, d)));
                }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ icons

        public static Texture2D MouseIcon(Color fur, Color belly, Color pink, Color eye)
        {
            const int S = 128;
            Texture2D t = New(S, S, "RS_UIMouse");
            Color outline = new Color(0.16f, 0.09f, 0.06f, 1f);
            Color furDark = fur * 0.82f; furDark.a = 1f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float earL = Circle(p, new Vector2(30f, 90f), 25f), earR = Circle(p, new Vector2(98f, 90f), 25f);
                    float head = Ellipse(p, new Vector2(64f, 54f), new Vector2(44f, 40f));
                    float union = Mathf.Min(head, Mathf.Min(earL, earR));
                    Color c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, outline, AA(union - 4.5f));
                    c = Over(c, furDark, AA(Mathf.Min(earL, earR)));
                    c = Over(c, pink, AA(Mathf.Min(Circle(p, new Vector2(30f, 90f), 15f), Circle(p, new Vector2(98f, 90f), 15f))));
                    c = Over(c, outline, AA(head - 2.5f) * AA(-head));
                    c = Over(c, fur, AA(head));
                    // shading on the lower half of the head
                    c = Over(c, new Color(0f, 0f, 0f, 0.12f), AA(head) * Mathf.Clamp01((40f - p.y) / 30f));
                    c = Over(c, belly, AA(Ellipse(p, new Vector2(64f, 37f), new Vector2(25f, 18f))));
                    c = Over(c, new Color(1f, 0.5f, 0.55f, 0.35f), AA(Circle(p, new Vector2(34f, 42f), 8f)) + AA(Circle(p, new Vector2(94f, 42f), 8f)));
                    float eyes = Mathf.Min(Ellipse(p, new Vector2(47f, 62f), new Vector2(8f, 11f)), Ellipse(p, new Vector2(81f, 62f), new Vector2(8f, 11f)));
                    c = Over(c, eye, AA(eyes));
                    c = Over(c, Color.white, AA(Mathf.Min(Circle(p, new Vector2(50f, 67f), 3.2f), Circle(p, new Vector2(84f, 67f), 3.2f))));
                    c = Over(c, outline, AA(Circle(p, new Vector2(64f, 46f), 8.5f)));
                    c = Over(c, pink, AA(Circle(p, new Vector2(64f, 46f), 6.5f)));
                    c = Over(c, Color.white, AA(Circle(p, new Vector2(62f, 48.5f), 2f)) * 0.8f);
                    float wh = Mathf.Min(Mathf.Min(Segment(p, new Vector2(42f, 40f), new Vector2(10f, 46f), 1.1f), Segment(p, new Vector2(42f, 36f), new Vector2(9f, 31f), 1.1f)),
                                         Mathf.Min(Segment(p, new Vector2(86f, 40f), new Vector2(118f, 46f), 1.1f), Segment(p, new Vector2(86f, 36f), new Vector2(119f, 31f), 1.1f)));
                    c = Over(c, outline, AA(wh));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        public static Texture2D Bullet(bool full)
        {
            const int W = 32, H = 80;
            Texture2D t = New(W, H, full ? "RS_UIBullet" : "RS_UIBulletEmpty");
            Color outline = new Color(0.18f, 0.1f, 0.05f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float casing = RoundRect(p, new Vector2(16f, 24f), new Vector2(11f, 20f), 3f);
                    float tip = Mathf.Max(Ellipse(p, new Vector2(16f, 44f), new Vector2(10f, 30f)), 44f - p.y);
                    float shape = Mathf.Min(casing, tip);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (full)
                    {
                        c = Over(c, outline, AA(shape - 2.5f));
                        c = Over(c, new Color(0.95f, 0.72f, 0.25f), AA(casing));
                        c = Over(c, new Color(0.85f, 0.45f, 0.25f), AA(tip));
                        c = Over(c, new Color(1f, 1f, 1f, 0.45f), AA(RoundRect(p, new Vector2(11f, 26f), new Vector2(2f, 16f), 2f)));
                        c = Over(c, new Color(0.7f, 0.5f, 0.15f), AA(RoundRect(p, new Vector2(16f, 7f), new Vector2(11f, 2f), 1f)));
                    }
                    else
                    {
                        c = Over(c, new Color(1f, 1f, 1f, 0.35f), AA(shape - 2f) * AA(-shape + 0.5f));
                        c = Over(c, new Color(0f, 0f, 0f, 0.3f), AA(shape));
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        public static Texture2D Clock()
        {
            const int S = 64;
            Texture2D t = New(S, S, "RS_UIClock");
            Color outline = new Color(0.16f, 0.1f, 0.08f);
            Vector2 c0 = new Vector2(32f, 30f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float face = Circle(p, c0, 24f);
                    float knob = RoundRect(p, new Vector2(32f, 58f), new Vector2(6f, 4f), 2f);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, outline, AA(Mathf.Min(face, knob) - 3.5f));
                    c = Over(c, new Color(1f, 0.35f, 0.3f), AA(knob));
                    c = Over(c, Color.white, AA(face));
                    c = Over(c, outline, AA(Segment(p, c0, new Vector2(32f, 46f), 2.2f)));
                    c = Over(c, outline, AA(Segment(p, c0, new Vector2(43f, 30f), 2.2f)));
                    c = Over(c, new Color(1f, 0.35f, 0.3f), AA(Circle(p, c0, 3.2f)));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        public static Texture2D StarIcon(Color fill, Color outline)
        {
            const int S = 96;
            Texture2D t = New(S, S, "RS_UIStar");
            Vector2 c0 = new Vector2(48f, 46f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float st = Star(p, c0, 42f, 19f);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, outline, AA(st - 3.5f));
                    c = Over(c, fill, AA(st));
                    c = Over(c, new Color(1f, 1f, 1f, 0.45f), AA(st) * Mathf.Clamp01((p.y - 50f) / 30f));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        public static Texture2D Trophy()
        {
            const int S = 160;
            Texture2D t = New(S, S, "RS_UITrophy");
            Color outline = new Color(0.3f, 0.16f, 0.04f);
            Color gold = new Color(1f, 0.78f, 0.2f), goldDark = new Color(0.9f, 0.55f, 0.1f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float cup = Mathf.Max(Ellipse(p, new Vector2(80f, 112f), new Vector2(46f, 60f)), p.y - 140f);
                    float handleL = Mathf.Abs(Circle(p, new Vector2(36f, 112f), 18f)) - 5f;
                    float handleR = Mathf.Abs(Circle(p, new Vector2(124f, 112f), 18f)) - 5f;
                    float stem = RoundRect(p, new Vector2(80f, 46f), new Vector2(9f, 14f), 3f);
                    float bse = RoundRect(p, new Vector2(80f, 24f), new Vector2(36f, 12f), 6f);
                    float rim = RoundRect(p, new Vector2(80f, 140f), new Vector2(50f, 6f), 4f);
                    float all = Mathf.Min(Mathf.Min(Mathf.Min(cup, handleL), Mathf.Min(handleR, stem)), Mathf.Min(bse, rim));
                    Color c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, outline, AA(all - 4.5f));
                    c = Over(c, goldDark, AA(Mathf.Min(handleL, handleR)));
                    c = Over(c, goldDark, AA(Mathf.Min(stem, bse)));
                    c = Over(c, gold, AA(cup));
                    c = Over(c, gold, AA(rim));
                    c = Over(c, new Color(1f, 1f, 1f, 0.5f), AA(RoundRect(p, new Vector2(60f, 108f), new Vector2(6f, 22f), 6f)));
                    c = Over(c, new Color(1f, 0.97f, 0.7f), AA(Star(p, new Vector2(80f, 104f), 18f, 8f)));
                    c = Over(c, new Color(0.45f, 0.2f, 0.05f, 0.55f), AA(RoundRect(p, new Vector2(80f, 24f), new Vector2(22f, 4f), 2f)));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}
