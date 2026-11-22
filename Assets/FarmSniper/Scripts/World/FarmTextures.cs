using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>Procedural textures for the farm. Pixel loops only - the editor builder saves the results as PNG assets.</summary>
    public static class FarmTextures
    {
        static float Fbm(float x, float y, int oct)
        {
            float sum = 0f, amp = 0.5f, f = 1f, norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                sum += Mathf.PerlinNoise(x * f + 13.1f * i, y * f + 7.7f * i) * amp;
                norm += amp;
                f *= 2.02f; amp *= 0.5f;
            }
            return sum / norm;
        }

        static Texture2D New(string name, int w, int h, bool linear = false)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear);
            t.name = name;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            return t;
        }

        static Texture2D Finish(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Whole-farm albedo: meadow grass, ploughed rows, wheat stubble, dirt roads, the yard and the pond bank.</summary>
        public static Texture2D GroundMap(int size)
        {
            Texture2D t = New("Farm_GroundMap", size, size);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[size * size];
            float world = FarmLayout.GroundSize, half = world * 0.5f;
            Color grassA = new Color(0.30f, 0.42f, 0.16f), grassB = new Color(0.42f, 0.50f, 0.20f), grassDry = new Color(0.55f, 0.52f, 0.28f);
            Color soil = new Color(0.33f, 0.24f, 0.16f), soilDark = new Color(0.22f, 0.16f, 0.11f);
            Color stubble = new Color(0.62f, 0.52f, 0.30f);
            Color road = new Color(0.52f, 0.44f, 0.33f), roadDark = new Color(0.40f, 0.33f, 0.24f);
            Color mud = new Color(0.25f, 0.21f, 0.15f);
            for (int y = 0; y < size; y++)
            {
                float wz = -half + (y + 0.5f) / size * world;
                for (int x = 0; x < size; x++)
                {
                    float wx = -half + (x + 0.5f) / size * world;
                    float n1 = Fbm(wx * 0.045f, wz * 0.045f, 4);
                    float n2 = Mathf.PerlinNoise(wx * 0.9f + 3f, wz * 0.9f + 1f);
                    Color c = Color.Lerp(grassA, grassB, n1);
                    c = Color.Lerp(c, grassDry, Mathf.Clamp01((Fbm(wx * 0.012f + 4f, wz * 0.012f, 3) - 0.55f) * 3f));
                    c *= 0.88f + n2 * 0.24f;

                    int f = FarmLayout.FieldAt(wx, wz, 0.6f);
                    if (f >= 0)
                    {
                        FarmLayout.CropKind k = FarmLayout.Fields[f].kind;
                        Rect r = FarmLayout.Fields[f].rect;
                        float edge = Mathf.Min(Mathf.Min(wx - r.xMin, r.xMax - wx), Mathf.Min(wz - r.yMin, r.yMax - wz));
                        float inside = Mathf.Clamp01((edge + 0.6f) / 1.2f);
                        Color fc;
                        if (k == FarmLayout.CropKind.Wheat)
                        {
                            float row = 0.5f + 0.5f * Mathf.Sin(wx * Mathf.PI * 2f / 0.7f);
                            fc = Color.Lerp(stubble * 0.8f, stubble, row * 0.5f + n2 * 0.5f);
                            fc = Color.Lerp(fc, soil, 0.25f * (1f - n1));
                        }
                        else
                        {
                            float spacing = k == FarmLayout.CropKind.Corn ? 1.0f : 1.2f;
                            float row = 0.5f + 0.5f * Mathf.Cos(wx * Mathf.PI * 2f / spacing);
                            fc = Color.Lerp(soilDark, soil, row);
                            fc *= 0.85f + n2 * 0.3f;
                            if (k == FarmLayout.CropKind.Pumpkin) fc = Color.Lerp(fc, grassA * 0.8f, 0.3f * n1);
                        }
                        c = Color.Lerp(c, fc, inside);
                    }

                    if (FarmLayout.InYard(wx, wz))
                    {
                        Vector2 d = new Vector2((wx - FarmLayout.YardCenter.x) / FarmLayout.YardRadii.x, (wz - FarmLayout.YardCenter.y) / FarmLayout.YardRadii.y);
                        float k = Mathf.Clamp01((1f - d.magnitude) * 3f) * Mathf.Clamp01(0.45f + n1);
                        c = Color.Lerp(c, Color.Lerp(road, roadDark, n2), k);
                    }

                    float rd = FarmLayout.RoadDistance(wx, wz);
                    float hw = FarmLayout.RoadWidth * 0.5f;
                    if (rd < hw + 1.2f)
                    {
                        float k = 1f - Mathf.Clamp01((rd - hw) / 1.2f);
                        // two darker wheel ruts and a grassy crown
                        float rut = Mathf.Exp(-Mathf.Pow((rd - hw * 0.55f) / 0.25f, 2f));
                        Color rc = Color.Lerp(road, roadDark, rut * 0.8f + n2 * 0.2f);
                        if (rd < 0.35f) rc = Color.Lerp(rc, grassA, 0.45f * n2);
                        c = Color.Lerp(c, rc, k * (0.85f + 0.15f * n1));
                    }

                    float pd = Vector2.Distance(new Vector2(wx, wz), FarmLayout.PondCenter);
                    if (pd < FarmLayout.PondRadius + 3f)
                        c = Color.Lerp(c, mud, Mathf.Clamp01((FarmLayout.PondRadius + 3f - pd) / 3f));

                    // outside the fence: wilder, darker meadow
                    float d0 = Mathf.Sqrt(wx * wx + wz * wz);
                    if (d0 > 110f) c = Color.Lerp(c, grassA * 0.85f, Mathf.Clamp01((d0 - 110f) / 60f));

                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return Finish(t, px);
        }

        /// <summary>Grey-ish tiling noise used as the Standard shader's detail albedo (x2) for close-up grain.</summary>
        public static Texture2D DetailNoise(int size)
        {
            Texture2D t = New("Farm_DetailNoise", size, size, true);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = TileNoise(u, v, 8f) * 0.6f + TileNoise(u, v, 32f) * 0.4f;
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.36f, 0.64f, n) * 255f), 0, 255);
                    px[y * size + x] = new Color32(b, b, b, 255);
                }
            return Finish(t, px);
        }

        /// <summary>Seamless Perlin noise (blend of four offset samples).</summary>
        static float TileNoise(float u, float v, float freq)
        {
            float a = Mathf.PerlinNoise(u * freq, v * freq);
            float b = Mathf.PerlinNoise((u - 1f) * freq, v * freq);
            float c = Mathf.PerlinNoise(u * freq, (v - 1f) * freq);
            float d = Mathf.PerlinNoise((u - 1f) * freq, (v - 1f) * freq);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Vertical wooden boards with grain, gaps and knots. One tile = 1 m.</summary>
        public static Texture2D Planks(string name, Color baseColor, int boards, float weather, int seed)
        {
            return Planks(name, baseColor, boards, weather, seed, false);
        }

        /// <summary>Boards; horizontal = clapboard siding (boards run along U).</summary>
        public static Texture2D Planks(string name, Color baseColor, int boards, float weather, int seed, bool horizontal)
        {
            const int size = 256;
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);
            float[] tone = new float[boards];
            for (int i = 0; i < boards; i++) tone[i] = 0.85f + (float)rng.NextDouble() * 0.3f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    if (horizontal) { float sw = u; u = v; v = sw; }
                    int bi = Mathf.Min(boards - 1, (int)(u * boards));
                    float bu = u * boards - bi;
                    float grain = TileNoise(u * 1f + bi * 0.37f, v, 3f) * 0.5f + Mathf.Sin((v * 40f + TileNoise(u, v, 6f) * 6f + bi * 3f)) * 0.08f;
                    float gap = bu < 0.04f || bu > 0.96f ? 0.45f : 1f;
                    float w = TileNoise(u, v, 12f);
                    Color c = baseColor * tone[bi] * (0.85f + grain * 0.3f) * gap;
                    c = Color.Lerp(c, new Color(0.55f, 0.53f, 0.5f), weather * Mathf.Clamp01(w - 0.35f));
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Overlapping roof shingles (rows along U).</summary>
        public static Texture2D Shingles(string name, Color baseColor, int seed)
        {
            const int size = 256;
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            const int rows = 8, cols = 6;
            System.Random rng = new System.Random(seed);
            float[] tone = new float[rows * cols * 2];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.8f + (float)rng.NextDouble() * 0.35f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    int r = (int)(v * rows);
                    float rv = v * rows - r;
                    float off = (r % 2) * 0.5f;
                    float cu = u * cols + off;
                    int c = ((int)cu) % cols;
                    float cuf = cu - Mathf.Floor(cu);
                    float shade = Mathf.Lerp(0.6f, 1.05f, rv);
                    float gap = cuf < 0.05f ? 0.55f : 1f;
                    Color col = baseColor * tone[(r * cols + c) % tone.Length] * shade * gap * (0.9f + TileNoise(u, v, 24f) * 0.2f);
                    col.a = 1f;
                    px[y * size + x] = col;
                }
            return Finish(t, px);
        }

        /// <summary>Corrugated, slightly rusty metal sheet (ridges along V).</summary>
        public static Texture2D Corrugated(string name, Color baseColor)
        {
            const int size = 256;
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            Color rust = new Color(0.45f, 0.24f, 0.12f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float ridge = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 10f);
                    float r = Mathf.Clamp01((TileNoise(u, v, 6f) * 0.7f + TileNoise(u, v, 20f) * 0.3f - 0.55f) * 3f);
                    Color c = Color.Lerp(baseColor * (0.75f + ridge * 0.35f), rust, r * 0.7f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Tree bark: vertical fissures.</summary>
        public static Texture2D Bark(string name, Color baseColor)
        {
            const int size = 128;
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = TileNoise(u * 1f, v * 0.25f, 16f);
                    float fiss = Mathf.Pow(Mathf.Abs(Mathf.Sin((u + TileNoise(u, v, 4f) * 0.15f) * Mathf.PI * 9f)), 0.35f);
                    Color c = baseColor * (0.55f + fiss * 0.45f) * (0.85f + n * 0.3f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Generic mottled texture (foliage, hay, fur, stone...).</summary>
        public static Texture2D Mottled(string name, Color a, Color b, float freq, float streak, int size = 128)
        {
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = TileNoise(u, v, freq) * 0.6f + TileNoise(u, v, freq * 3f) * 0.4f;
                    if (streak > 0f) n = Mathf.Lerp(n, 0.5f + 0.5f * Mathf.Sin((u * 30f + TileNoise(u, v, 4f) * 8f)), streak);
                    Color c = Color.Lerp(a, b, n);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Flannel shirt plaid.</summary>
        public static Texture2D Plaid(string name, Color baseC, Color stripe)
        {
            const int size = 64;
            Texture2D t = New(name, size, size);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool sx = (x % 32) < 8, sy = (y % 32) < 8;
                    bool thin = (x % 32) == 20 || (y % 32) == 20;
                    Color c = baseC;
                    if (sx && sy) c = stripe * 0.7f;
                    else if (sx || sy) c = Color.Lerp(baseC, stripe, 0.55f);
                    if (thin) c = Color.Lerp(c, Color.white, 0.35f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            t.filterMode = FilterMode.Bilinear;
            return Finish(t, px);
        }

        /// <summary>
        /// Sniper scope overlay for the desktop view: transparent lens with a duplex crosshair and mil-dots, black outside
        /// the tube, soft dark edge inside. Built at runtime (never saved).
        /// </summary>
        public static Texture2D ScopeOverlay(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.name = "Farm_ScopeOverlay";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float R = size * 0.48f;
            float thick = Mathf.Max(1.2f, size / 340f);          // thin centre lines
            float post = Mathf.Max(4f, size / 90f);             // thick outer posts
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (r > R) a = 1f;
                    else
                    {
                        float edge = Mathf.Clamp01((r - R * 0.82f) / (R * 0.18f));
                        a = edge * edge * 0.9f;
                        float adx = Mathf.Abs(dx), ady = Mathf.Abs(dy);
                        float lineW = r > R * 0.55f ? post : thick;
                        bool onH = ady < lineW * 0.5f && !(adx < R * 0.012f);
                        bool onV = adx < lineW * 0.5f && !(ady < R * 0.012f);
                        if (onH || onV) a = 1f;
                        // mil dots along both axes
                        float spacing = R * 0.09f;
                        for (int k = 1; k <= 5; k++)
                        {
                            float dd = k * spacing;
                            if ((Mathf.Abs(adx - dd) < size * 0.004f && ady < size * 0.004f) ||
                                (Mathf.Abs(ady - dd) < size * 0.004f && adx < size * 0.004f))
                                a = 1f;
                        }
                        // tiny red centre dot
                        if (r < size * 0.0035f) { px[y * size + x] = new Color32(230, 30, 20, 255); continue; }
                    }
                    px[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        /// <summary>Soft round dot (hit marker, ui).</summary>
        public static Texture2D White()
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.name = "Farm_White";
            t.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            t.Apply();
            return t;
        }
    }
}
