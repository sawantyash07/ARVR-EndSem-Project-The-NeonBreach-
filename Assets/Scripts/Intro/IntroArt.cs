using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Procedural textures for the cinematic intro: velvet curtain cloth with neon piping, the scalloped valance,
    /// stage gradient, neon floor grid, spotlight beam, soft glows, vignette and film grain. Generated once at startup
    /// (the project ships no image assets) and destroyed by <see cref="CinematicIntro"/> when the intro ends.
    /// </summary>
    public static class IntroArt
    {
        static float Sq(float x) { return x * x; }
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        static Texture2D New(string name, int w, int h, bool mips, TextureWrapMode wrapU, TextureWrapMode wrapV)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, mips);
            t.name = "Intro_" + name;
            t.wrapModeU = wrapU;
            t.wrapModeV = wrapV;
            t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            t.anisoLevel = mips ? 2 : 0;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static Texture2D Done(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply(t.mipmapCount > 1, true);
            return t;
        }

        static Color32 C(Color c, float a)
        {
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
        }

        // ------------------------------------------------------------------ curtain cloth

        /// <summary>
        /// Velvet curtain, u = 0 at the outer edge, u = 1 at the inner (leading) edge. Folds are shaded with ambient
        /// occlusion in the valleys and velvet sheen on the turning flanks; they fan out slightly towards the hem.
        /// The last strip (u &gt; <see cref="IntroCurtain.TrimU"/>) is the neon piping, kept at a fixed width by the mesh.
        /// </summary>
        public static Texture2D Velvet(int w, int h, int folds, Color baseCol, Color trim, int seed)
        {
            System.Random rnd = new System.Random(seed);
            float p1 = (float)rnd.NextDouble() * 6.283f, p2 = (float)rnd.NextDouble() * 6.283f, p3 = (float)rnd.NextDouble() * 6.283f;
            float trimU = IntroCurtain.TrimU;
            int clothW = Mathf.Max(8, Mathf.RoundToInt(w * trimU));
            Color sheen = Color.Lerp(baseCol, new Color(1f, 0.62f, 0.7f), 0.5f);

            // fold height on a coarse set of rows (the folds change slowly from top to hem), columns -1 .. clothW
            const int R = 33;
            float[,] fh = new float[R, clothW + 2];
            for (int rr = 0; rr < R; rr++)
            {
                float v = rr / (float)(R - 1);
                float fan = 1f - v;
                for (int x = -1; x <= clothW; x++)
                {
                    float s = (x + 0.5f) / clothW;
                    float xf = s * folds;
                    float wv = xf + 0.20f * Mathf.Sin(xf * 0.83f + p1) + 0.11f * Mathf.Sin(xf * 1.93f + p2) + 0.16f * fan * Mathf.Sin(xf * 0.47f + p3);
                    float amp = 0.82f + 0.18f * Mathf.Sin(xf * 0.61f + p2 + v * 1.7f);
                    fh[rr, x + 1] = (Mathf.Sin(wv * 6.2832f) + 0.22f * Mathf.Sin(wv * 12.566f + 1.3f)) * amp;
                }
            }

            Texture2D tex = New("Velvet", w, h, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            float slopeNorm = 1f / (2f / clothW * folds * 6.2832f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float rf = v * (R - 1);
                int r0 = Mathf.Min((int)rf, R - 2);
                float rt = rf - r0;
                float vTop = 1f - 0.6f * Smooth(0.82f, 1f, v);
                float vBot = 0.62f + 0.38f * Smooth(0f, 0.16f, v);
                float hem = v < 0.028f ? 0.62f : (v < 0.04f ? 1.12f : 1f);   // rolled hem with a lit edge above it
                Color lastCloth = baseCol;
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (x < clothW)
                    {
                        float h0 = Mathf.Lerp(fh[r0, x + 1], fh[r0 + 1, x + 1], rt);
                        float hl = Mathf.Lerp(fh[r0, x], fh[r0 + 1, x], rt);
                        float hr = Mathf.Lerp(fh[r0, x + 2], fh[r0 + 1, x + 2], rt);
                        float slope = (hr - hl) * slopeNorm;
                        float facing = 1f / Mathf.Sqrt(1f + Sq(1.4f * slope));
                        float crest = Mathf.Clamp01((h0 / 1.2f + 1f) * 0.5f);
                        float ao = Mathf.Lerp(0.30f, 1f, Mathf.Pow(crest, 0.75f));
                        float diff = ao * (0.28f + 0.72f * facing) + 0.12f * slope;
                        float sh = Mathf.Pow(1f - facing, 1.7f) * 0.7f * (0.5f + 0.5f * crest);
                        float grain = 0.95f + 0.1f * (float)rnd.NextDouble();
                        c = (baseCol * diff * 1.3f + sheen * sh * 0.55f) * (vTop * vBot * hem * grain);
                        float s = (x + 0.5f) / clothW;
                        float spill = Mathf.Exp(-(1f - s) * clothW / 18f) * 0.35f;   // neon light spilling onto the velvet
                        c += trim * spill * (0.6f + 0.4f * facing) * vTop;
                        lastCloth = c;
                    }
                    else
                    {
                        float s2 = (x - clothW + 0.5f) / Mathf.Max(1, w - clothW);
                        float core = Mathf.Exp(-Sq((s2 - 0.5f) / 0.085f));
                        float glow = Mathf.Exp(-Sq((s2 - 0.5f) / 0.27f));
                        c = lastCloth * 0.55f * (1f - glow) + trim * glow * 0.95f + Color.white * core * 0.75f;
                        if (s2 > 0.84f) c *= Mathf.Lerp(1f, 0.3f, (s2 - 0.84f) / 0.16f);
                        c *= vTop;
                    }
                    px[y * w + x] = C(c, 1f);
                }
            }
            return Done(tex, px);
        }

        // ------------------------------------------------------------------ valance (top pelmet with swags)

        /// <summary>One swag of the valance per texture tile (repeat horizontally): flat pelmet band with a thin neon line,
        /// a draped festoon below it and a glowing neon edge along the scallop; transparent under the scallop.</summary>
        public static Texture2D Valance(int w, int h, Color baseCol, Color bandNeon, Color edgeNeon)
        {
            const float band = 0.62f, depth = 0.50f;
            float[] edge = new float[w];
            float[] sinU = new float[w];
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                sinU[x] = Mathf.Sin(Mathf.PI * u);
                edge[x] = band - depth * Mathf.Pow(sinU[x], 0.8f);
            }
            Texture2D tex = New("Valance", w, h, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float e = edge[x];
                    Color c;
                    float a;
                    if (v >= band)
                    {
                        float k = (v - band) / (1f - band);
                        float pleat = 0.86f + 0.14f * Mathf.Cos(u * 6.2832f * 5f);
                        c = baseCol * Mathf.Lerp(1.0f, 0.42f, k) * pleat * 1.15f;
                        float d = (v - band) * h;
                        float core = Mathf.Exp(-Sq((d - 5f) / 1.6f));
                        c += bandNeon * (Mathf.Exp(-Sq((d - 5f) / 6f)) * 0.5f + core * 0.6f) + Color.white * core * 0.45f;
                        if (v > 0.985f) c *= 0.4f;
                        a = 1f;
                    }
                    else if (v >= e)
                    {
                        float q = (v - e) / Mathf.Max(0.001f, band - e);
                        float fold = 0.62f + 0.38f * Mathf.Cos(q * 6.2832f * 2.5f);
                        float centre = 0.55f + 0.45f * Mathf.Pow(sinU[x], 0.6f);
                        float roll = q < 0.12f ? Mathf.Lerp(0.55f, 1f, q / 0.12f) : 1f;
                        c = baseCol * fold * centre * roll * 1.2f;
                        float dpx = (v - e) * h;
                        float core = Mathf.Exp(-Sq((dpx - 2f) / 1.4f));
                        c += edgeNeon * core * 0.9f + Color.white * core * 0.35f;
                        a = Mathf.Clamp01(dpx + 0.5f);
                        c = Color.Lerp(edgeNeon, c, a);
                        a = Mathf.Max(a, 0.55f);
                    }
                    else
                    {
                        float dpx = (e - v) * h;
                        c = edgeNeon;
                        a = Mathf.Exp(-dpx / 6f) * 0.55f;
                    }
                    px[y * w + x] = C(c, a);
                }
            }
            return Done(tex, px);
        }

        // ------------------------------------------------------------------ stage

        /// <summary>Vertical gradient (bottom, middle, top), 8 px wide.</summary>
        public static Texture2D VerticalGradient(Color bottom, Color mid, Color top)
        {
            const int w = 8, h = 256;
            Texture2D tex = New("Gradient", w, h, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                Color c = v < 0.5f ? Color.Lerp(bottom, mid, Smooth(0f, 0.5f, v)) : Color.Lerp(mid, top, Smooth(0.5f, 1f, v));
                for (int x = 0; x < w; x++) px[y * w + x] = C(c, c.a);
            }
            return Done(tex, px);
        }

        /// <summary>Soft round glow: white with a gaussian alpha falloff (tinted by the image colour).</summary>
        public static Texture2D Radial(int size)
        {
            Texture2D tex = New("Glow", size, size, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt(Sq((x + 0.5f - half) / half) + Sq((y + 0.5f - half) / half));
                    float a = Mathf.Exp(-r * r * 4.5f) * (1f - Smooth(0.85f, 1f, r));
                    px[y * size + x] = C(Color.white, a);
                }
            return Done(tex, px);
        }

        /// <summary>Volumetric spotlight cone: narrow at the top (the lamp), wide and faint towards the floor.</summary>
        public static Texture2D Beam(int w, int h)
        {
            Texture2D tex = New("Beam", w, h, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float spread = BeamSpread(v);
                float vert = (0.35f + 0.65f * Mathf.Pow(v, 0.6f)) * Smooth(0f, 0.18f, v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float dx = Mathf.Abs(u - 0.5f) / spread;
                    px[y * w + x] = C(Color.white, Mathf.Exp(-dx * dx * 2.2f) * vert);
                }
            }
            return Done(tex, px);
        }

        /// <summary>Half-width of the beam (fraction of the beam image width) at height v (1 = top).</summary>
        public static float BeamSpread(float v) { return Mathf.Lerp(0.10f, 0.48f, 1f - v); }

        /// <summary>Neon perspective floor grid: v = 0 is the stage front, v = 1 the horizon (with a glowing horizon line).</summary>
        public static Texture2D Grid(int w, int h, Color line, Color horizon)
        {
            Texture2D tex = New("Grid", w, h, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            Color fill = new Color(0.02f, 0.03f, 0.08f);
            for (int y = 0; y < h; y++)
            {
                float d = (y + 0.5f) / h;
                float z = 1f / (1f - d * 0.985f);
                float gz = z * 3f;
                float dgz = 3f * 0.985f * z * z / h;
                float fade = Mathf.Pow(1f - d, 0.55f);
                float hz = Mathf.Exp(-Sq((1f - d) / 0.05f));
                for (int x = 0; x < w; x++)
                {
                    float xs = (x + 0.5f) / w * 2f - 1f;
                    float gx = xs * z * 6f;
                    float dgx = z * 12f / w;
                    float distX = Mathf.Abs(gx - Mathf.Round(gx)) / dgx;
                    float distZ = Mathf.Abs(gz - Mathf.Round(gz)) / Mathf.Max(dgz, 1e-4f);
                    float lx = Mathf.Clamp01(1.2f - distX) * Mathf.Min(1f, 1f / (dgx * 1.5f));
                    float lz = Mathf.Clamp01(1.2f - distZ) * Mathf.Min(1f, 1f / (dgz * 2f));
                    float l = Mathf.Max(lx, lz) * fade;
                    Color c = Color.Lerp(Color.Lerp(fill, line, l), horizon, hz);
                    float a = Mathf.Clamp01(Mathf.Max(0.35f * (1f - d), l * 0.95f) + hz * 0.55f);
                    px[y * w + x] = C(c, a);
                }
            }
            return Done(tex, px);
        }

        /// <summary>Black vignette with a transparent centre.</summary>
        public static Texture2D Vignette(int size)
        {
            Texture2D tex = New("Vignette", size, size, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt(Sq((x + 0.5f - half) / half) + Sq((y + 0.5f - half) / half));
                    px[y * size + x] = C(Color.black, Mathf.Pow(Smooth(0.5f, 1.38f, r), 1.2f));
                }
            return Done(tex, px);
        }

        /// <summary>Two-tone film grain (light and dark specks around a transparent mid-point), tiles seamlessly.</summary>
        public static Texture2D Grain(int size, int seed)
        {
            System.Random rnd = new System.Random(seed);
            Texture2D tex = New("Grain", size, size, false, TextureWrapMode.Repeat, TextureWrapMode.Repeat);
            tex.filterMode = FilterMode.Point;
            Color32[] px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float n = (float)rnd.NextDouble();
                px[i] = n > 0.5f ? C(Color.white, (n - 0.5f) * 2f) : C(Color.black, (0.5f - n) * 2f);
            }
            return Done(tex, px);
        }

        /// <summary>Horizontal line that is bright at u = 0 and fades out towards u = 1.</summary>
        public static Texture2D FadeLine(int w)
        {
            const int h = 4;
            Texture2D tex = New("FadeLine", w, h, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float a = Mathf.Pow(1f - u, 1.5f) * Smooth(0f, 0.03f, u);
                for (int y = 0; y < h; y++) px[y * w + x] = C(Color.white, a);
            }
            return Done(tex, px);
        }

        /// <summary>Horizontal three-colour gradient (left, centre, right) whose alpha fades out at both ends.</summary>
        public static Texture2D Gradient3(Color left, Color mid, Color right)
        {
            const int w = 256, h = 4;
            Texture2D tex = New("Gradient3", w, h, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            Color32[] px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                Color c = u < 0.5f ? Color.Lerp(left, mid, u * 2f) : Color.Lerp(mid, right, (u - 0.5f) * 2f);
                float a = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
                for (int y = 0; y < h; y++) px[y * w + x] = C(c, a);
            }
            return Done(tex, px);
        }

        /// <summary>9-slice frame sprite (a 2 px line around a transparent centre) for the Skip Intro button.</summary>
        public static Sprite Frame(out Texture2D tex)
        {
            const int s = 16, b = 2;
            tex = New("Frame", s, s, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool edge = x < b || y < b || x >= s - b || y >= s - b;
                    px[y * s + x] = C(Color.white, edge ? 1f : 0f);
                }
            Done(tex, px);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b + 1, b + 1, b + 1, b + 1));
            sp.name = "Intro_Frame";
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }
    }
}
