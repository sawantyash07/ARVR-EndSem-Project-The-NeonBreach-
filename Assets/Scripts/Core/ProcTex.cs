using UnityEngine;

namespace NeonBreach
{
    /// <summary>Procedurally generated textures, so the project needs no imported art.</summary>
    public static class ProcTex
    {
        public static Texture2D Grid(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color32[] px = new Color32[size * size];
            Color32 bg = new Color32(7, 9, 16, 255);
            Color32 minor = new Color32(14, 70, 90, 255);
            Color32 major = new Color32(30, 225, 255, 255);
            int th = Mathf.Max(2, size / 64);
            int sub = size / 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 c = bg;
                    if (x < th || y < th) c = major;
                    else if ((x % sub) < 1 || (y % sub) < 1) c = minor;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }

        public static Texture2D SoftCircle(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a *= a;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false);
            return t;
        }

        public static Texture2D Decal(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r * 1.1f));
                    px[y * size + x] = new Color(0.02f, 0.02f, 0.03f, a * 0.9f);
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false);
            return t;
        }

        /// <summary>Fraction of the half size covered by the scoring rings of <see cref="TargetFace"/>.</summary>
        public const float TargetRingFraction = 0.95f;

        /// <summary>Archery target face: dark square with gold / red / blue / black / white rings and a bullseye.</summary>
        public static Texture2D TargetFace(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color[] px = new Color[size * size];
            float half = size * 0.5f;
            Color paper = new Color(0.07f, 0.08f, 0.11f, 1f);
            Color[] zone =
            {
                new Color(1f, 0.97f, 0.70f, 1f),    // bullseye (r < 0.1)
                new Color(1f, 0.80f, 0.10f, 1f),    // 10
                new Color(0.95f, 0.15f, 0.15f, 1f), // 8
                new Color(0.15f, 0.45f, 1f, 1f),    // 6
                new Color(0.12f, 0.12f, 0.14f, 1f), // 4
                new Color(0.92f, 0.92f, 0.90f, 1f)  // 2
            };
            float[] edge = { 0.1f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
            float line = 1.6f / (size * TargetRingFraction * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / TargetRingFraction;
                    Color c = paper;
                    for (int k = 0; k < edge.Length; k++)
                    {
                        if (r <= edge[k])
                        {
                            c = zone[k];
                            if (edge[k] - r < line) c = k == 4 ? new Color(0.8f, 0.8f, 0.8f, 1f) : new Color(0.05f, 0.05f, 0.05f, 1f);
                            break;
                        }
                    }
                    if (r < 0.06f && (Mathf.Abs(dx - dy) < 0.006f || Mathf.Abs(dx + dy) < 0.006f)) c = new Color(0.2f, 0.1f, 0f, 1f);
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Top-down colour map for a height field (parachute island): seabed, beach, grass with noise patches and rock on
        /// steep or high ground. height(x, z) is sampled over [origin, origin + worldSize] on both axes.
        /// </summary>
        public static Texture2D HeightColorMap(int size, System.Func<float, float, float> height, float origin, float worldSize, float water)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color[] px = new Color[size * size];
            float cell = worldSize / size;
            Color seabed = new Color(0.55f, 0.50f, 0.36f), sand = new Color(0.88f, 0.80f, 0.56f);
            Color grassA = new Color(0.30f, 0.52f, 0.18f), grassB = new Color(0.16f, 0.36f, 0.12f);
            Color rock = new Color(0.46f, 0.44f, 0.41f), rockHi = new Color(0.66f, 0.64f, 0.60f);
            for (int y = 0; y < size; y++)
            {
                float wz = origin + (y + 0.5f) * cell;
                for (int x = 0; x < size; x++)
                {
                    float wx = origin + (x + 0.5f) * cell;
                    float h = height(wx, wz);
                    float hx = height(wx + cell, wz) - h, hz = height(wx, wz + cell) - h;
                    float slope = Mathf.Sqrt(hx * hx + hz * hz) / cell;
                    float n = Mathf.PerlinNoise(wx * 0.013f + 31f, wz * 0.013f + 7f);
                    float n2 = Mathf.PerlinNoise(wx * 0.08f, wz * 0.08f);
                    Color c;
                    if (h < water - 0.5f) c = Color.Lerp(sand, seabed, Mathf.Clamp01((water - h) / 12f));
                    else if (h < water + 4f) c = sand;
                    else
                    {
                        float g = Mathf.Clamp01((n - 0.3f) / 0.4f);
                        c = Color.Lerp(grassA, grassB, g * g * (3f - 2f * g));
                        c = Color.Lerp(c, sand, Mathf.Clamp01((water + 7f - h) / 3f));
                        float rockK = Mathf.Max(Mathf.Clamp01((h - 75f) / 40f), Mathf.Clamp01((slope - 0.55f) / 0.3f));
                        c = Color.Lerp(c, Color.Lerp(rock, rockHi, Mathf.Clamp01((h - 110f) / 40f)), rockK);
                    }
                    c *= 0.9f + 0.2f * n2;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }

        /// <summary>Red screen-edge vignette used for damage feedback (alpha grows towards the edges).</summary>
        public static Texture2D Vignette(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.3f);
                    float a = r * r * r;
                    px[y * size + x] = new Color(0.9f, 0.04f, 0.04f, a);
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false);
            return t;
        }
    }
}
