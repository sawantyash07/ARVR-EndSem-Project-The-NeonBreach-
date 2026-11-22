using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Procedural, tileable, softly painted textures for the cartoon farm. Most are near-white detail maps that get
    /// their colour from the material / vertex colours, so one texture serves many objects.
    /// </summary>
    public static class CartoonTex
    {
        delegate Color PixelFn(float u, float v);

        static Texture2D Make(string name, int size, PixelFn fn, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.name = name;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = fn((x + 0.5f) / size, (y + 0.5f) / size);
            t.SetPixels(px);
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            RatGen.Register(t);
            return t;
        }

        static float Fbm(float u, float v, int period, int oct, int seed) { return RatNoise.TileFbm(u * period, v * period, period, oct, seed); }

        /// <summary>Soft grass detail (near white, tinted by the ground's vertex colours).</summary>
        public static Texture2D GrassDetail()
        {
            return Make("RS_GrassDetail", 256, (u, v) =>
            {
                float n = Fbm(u, v, 4, 4, 11);
                float strokes = RatNoise.Tile2(u * 64f, v * 18f, 64, 5) * 0.6f + RatNoise.Tile2(u * 96f, v * 30f, 96, 6) * 0.4f;
                float k = 0.82f + n * 0.16f + (strokes - 0.5f) * 0.12f;
                return new Color(k, k * 1.01f, k * 0.97f, 1f);
            });
        }

        /// <summary>Horizontal wooden planks (U runs along the boards). Base colours in, grain and gaps painted.</summary>
        public static Texture2D Planks(string name, Color light, Color dark, int boards, int seed)
        {
            return Make(name, 256, (u, v) =>
            {
                float bv = v * boards;
                int board = Mathf.FloorToInt(bv);
                float fv = bv - board;
                float tint = RatNoise.Tile2(board * 3.1f, 0.5f, 64, seed) * 0.25f;
                float grain = Mathf.Sin((fv * 6f + RatNoise.Tile2(u * 8f, v * boards * 2f, 8, seed + 3) * 2.2f + board * 1.7f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float fine = RatNoise.Tile2(u * 64f, fv * 4f + board * 9f, 64, seed + 7);
                Color c = Color.Lerp(light, dark, grain * 0.35f + tint + fine * 0.12f);
                // board ends (staggered butt joints)
                float joint = Mathf.Repeat(u + board * 0.37f, 1f);
                float edgeV = Mathf.Min(fv, 1f - fv);
                float gap = Mathf.Clamp01(1f - edgeV / 0.06f);
                float jg = Mathf.Clamp01(1f - Mathf.Min(joint, 1f - joint) / 0.012f);
                c = Color.Lerp(c, dark * 0.55f, Mathf.Max(gap * gap, jg * 0.8f));
                // a soft highlight along the top of each board (cartoon bevel)
                c = Color.Lerp(c, Color.Lerp(light, Color.white, 0.3f), Mathf.Clamp01(1f - Mathf.Abs(fv - 0.82f) / 0.08f) * 0.25f);
                // knots
                float kx = Mathf.Repeat(u * 2f + board * 0.61f, 1f) - 0.5f, ky = fv - 0.5f;
                float knot = Mathf.Clamp01(1f - Mathf.Sqrt(kx * kx * 9f + ky * ky * 2.5f) / 0.18f);
                if (RatNoise.Tile2(board * 7.7f, 1.5f, 64, seed + 11) > 0.62f) c = Color.Lerp(c, dark * 0.7f, knot * 0.8f);
                return c;
            });
        }

        public static Texture2D Bark()
        {
            return Make("RS_Bark", 256, (u, v) =>
            {
                float s = RatNoise.Tile2(u * 16f, v * 3f, 16, 21) * 0.6f + RatNoise.Tile2(u * 32f, v * 6f, 32, 22) * 0.4f;
                float ridge = Mathf.Abs(Mathf.Sin((u * 12f + s * 1.5f) * Mathf.PI));
                float k = 0.62f + ridge * 0.32f + (Fbm(u, v, 4, 3, 23) - 0.5f) * 0.2f;
                return new Color(k, k * 0.98f, k * 0.95f, 1f);
            });
        }

        public static Texture2D Hay()
        {
            return Make("RS_Hay", 256, (u, v) =>
            {
                float a = RatNoise.Tile2(u * 128f, v * 16f, 128, 31);
                float b = RatNoise.Tile2(u * 16f + 3f, v * 128f, 128, 32);
                float stroke = Mathf.Max(a, b * 0.85f);
                float k = 0.7f + stroke * 0.38f + (Fbm(u, v, 4, 3, 33) - 0.5f) * 0.15f;
                return new Color(k, k * 0.96f, k * 0.86f, 1f);
            });
        }

        public static Texture2D Fur()
        {
            return Make("RS_Fur", 128, (u, v) =>
            {
                float s = RatNoise.Tile2(u * 64f, v * 8f, 64, 41) * 0.5f + RatNoise.Tile2(u * 32f, v * 16f, 32, 42) * 0.5f;
                float k = 0.86f + (s - 0.5f) * 0.22f + (Fbm(u, v, 4, 2, 43) - 0.5f) * 0.12f;
                return new Color(k, k, k, 1f);
            });
        }

        /// <summary>Blotchy painterly foliage (near white, tinted by material/vertex colour).</summary>
        public static Texture2D Foliage()
        {
            return Make("RS_Foliage", 256, (u, v) =>
            {
                float n = Fbm(u, v, 8, 4, 51);
                float cells = RatNoise.Tile2(u * 24f, v * 24f, 24, 52);
                float k = 0.72f + n * 0.3f + Mathf.SmoothStep(0.45f, 0.75f, cells) * 0.12f;
                return new Color(k, k, k * 0.95f, 1f);
            });
        }

        public static Texture2D Stone()
        {
            return Make("RS_Stone", 256, (u, v) =>
            {
                float n = Fbm(u, v, 4, 5, 61);
                float spots = Mathf.SmoothStep(0.62f, 0.7f, RatNoise.Tile2(u * 32f, v * 32f, 32, 62)) * 0.12f;
                float k = 0.74f + n * 0.3f - spots;
                return new Color(k, k, k * 1.02f, 1f);
            });
        }

        public static Texture2D Metal()
        {
            return Make("RS_Metal", 128, (u, v) =>
            {
                float n = Fbm(u, v, 4, 3, 71);
                float scratch = RatNoise.Tile2(u * 128f, v * 4f, 128, 72);
                float k = 0.8f + n * 0.18f + Mathf.SmoothStep(0.8f, 0.9f, scratch) * 0.1f;
                return new Color(k, k, k, 1f);
            });
        }

        /// <summary>Overlapping rounded roof shingles in rows.</summary>
        public static Texture2D Shingles()
        {
            return Make("RS_Shingles", 256, (u, v) =>
            {
                const int rows = 8;
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float fv = rv - row;
                float fu = Mathf.Repeat(u * 6f + (row % 2) * 0.5f, 1f);
                float edge = Mathf.Sqrt((fu - 0.5f) * (fu - 0.5f) * 0.9f + fv * fv * 0.25f);
                float shade = Mathf.Lerp(0.7f, 1f, fv) * (0.92f + RatNoise.Tile2(row * 3f + Mathf.Floor(u * 6f), 0f, 64, 81) * 0.16f);
                float border = Mathf.Clamp01((Mathf.Min(fu, 1f - fu)) / 0.04f);
                float k = shade * Mathf.Lerp(0.6f, 1f, border) * (edge > 0.5f ? 0.85f : 1f);
                return new Color(k, k, k, 1f);
            });
        }

        public static Texture2D Burlap()
        {
            return Make("RS_Burlap", 128, (u, v) =>
            {
                float wu = Mathf.Abs(Mathf.Sin(u * 64f * Mathf.PI)), wv = Mathf.Abs(Mathf.Sin(v * 64f * Mathf.PI));
                float k = 0.72f + 0.14f * Mathf.Max(wu, wv) + (Fbm(u, v, 4, 3, 91) - 0.5f) * 0.18f;
                return new Color(k, k * 0.97f, k * 0.9f, 1f);
            });
        }

        public static Texture2D Plaid(Color a, Color b)
        {
            return Make("RS_Plaid", 128, (u, v) =>
            {
                float su = Mathf.Repeat(u * 4f, 1f), sv = Mathf.Repeat(v * 4f, 1f);
                float bandU = su < 0.35f ? 1f : (su < 0.45f ? 0.4f : 0f);
                float bandV = sv < 0.35f ? 1f : (sv < 0.45f ? 0.4f : 0f);
                float m = (bandU + bandV) * 0.5f;
                Color c = Color.Lerp(a, b, m);
                float thread = (Mathf.Sin((u + v) * 512f) * 0.5f + 0.5f) * 0.06f;
                return c * (0.97f - thread);
            });
        }

        public static Texture2D Soil()
        {
            return Make("RS_Soil", 256, (u, v) =>
            {
                float n = Fbm(u, v, 8, 4, 101);
                float clumps = Mathf.SmoothStep(0.55f, 0.75f, RatNoise.Tile2(u * 40f, v * 40f, 40, 102));
                float k = 0.7f + n * 0.25f + clumps * 0.15f;
                return new Color(k, k * 0.97f, k * 0.94f, 1f);
            });
        }

        public static Texture2D Cheese()
        {
            return Make("RS_Cheese", 128, (u, v) =>
            {
                float holes = RatNoise.Tile2(u * 10f, v * 10f, 10, 111);
                float h = Mathf.SmoothStep(0.72f, 0.76f, holes);
                float k = 1f - h * 0.35f;
                return new Color(k, k * 0.96f, k * 0.85f, 1f);
            });
        }

        // ------------------------------------------------------------------ FX sprites (clamped)

        public static Texture2D SoftCircle()
        {
            return Make("RS_SoftCircle", 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }, TextureWrapMode.Clamp);
        }

        /// <summary>Five-pointed cartoon star sprite with a bright centre and a soft outline.</summary>
        public static Texture2D StarSprite()
        {
            return Make("RS_StarSprite", 128, (u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
                float ang = Mathf.Atan2(p.y, p.x) + Mathf.PI * 0.5f;
                float sector = Mathf.PI * 2f / 5f;
                float t = Mathf.Abs(Mathf.Repeat(ang, sector) - sector * 0.5f) / (sector * 0.5f);
                float rStar = Mathf.Lerp(0.95f, 0.42f, Mathf.Pow(t, 0.8f));
                float d = p.magnitude;
                float a = Mathf.Clamp01((rStar - d) / 0.05f);
                float edge = Mathf.Clamp01((rStar - d) / 0.16f);
                Color c = Color.Lerp(new Color(1f, 0.62f, 0.05f), new Color(1f, 0.97f, 0.6f), edge);
                return new Color(c.r, c.g, c.b, a);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>Puffy cartoon smoke ball: crisp edge, shaded underside.</summary>
        public static Texture2D Puff()
        {
            return Make("RS_Puff", 128, (u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
                float d = p.magnitude + (RatNoise.Value2(Mathf.Atan2(p.y, p.x) * 2.5f + 10f, 0.5f, 121) - 0.5f) * 0.18f;
                float a = Mathf.Clamp01((0.92f - d) / 0.06f);
                float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(p.y * 0.6f + 0.6f));
                return new Color(shade, shade, shade, a);
            }, TextureWrapMode.Clamp);
        }
    }
}
