using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>Deterministic value noise (2D / 3D) and fBm used by the procedural meshes and textures.</summary>
    public static class RatNoise
    {
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        public static float Value3(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float xf = Smooth(p.x - xi), yf = Smooth(p.y - yi), zf = Smooth(p.z - zi);
            float a = Mathf.Lerp(Hash(xi, yi, zi), Hash(xi + 1, yi, zi), xf);
            float b = Mathf.Lerp(Hash(xi, yi + 1, zi), Hash(xi + 1, yi + 1, zi), xf);
            float c = Mathf.Lerp(Hash(xi, yi, zi + 1), Hash(xi + 1, yi, zi + 1), xf);
            float d = Mathf.Lerp(Hash(xi, yi + 1, zi + 1), Hash(xi + 1, yi + 1, zi + 1), xf);
            return Mathf.Lerp(Mathf.Lerp(a, b, yf), Mathf.Lerp(c, d, yf), zf);
        }

        public static float Value2(float x, float y, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = Smooth(x - xi), yf = Smooth(y - yi);
            float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), xf);
            float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), xf);
            return Mathf.Lerp(a, b, yf);
        }

        /// <summary>Tileable 2D value noise with the given integer period.</summary>
        public static float Tile2(float x, float y, int period, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = Smooth(x - xi), yf = Smooth(y - yi);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), xf);
            float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), xf);
            return Mathf.Lerp(a, b, yf);
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        public static float Fbm2(float x, float y, int octaves, int seed = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value2(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Tileable fBm over a texture of 'period' base cells.</summary>
        public static float TileFbm(float x, float y, int period, int octaves, int seed = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int p = period;
            for (int i = 0; i < octaves; i++)
            {
                sum += Tile2(x, y, p, seed + i * 31) * amp;
                norm += amp;
                x *= 2f; y *= 2f; p *= 2; amp *= 0.5f;
            }
            return sum / norm;
        }
    }
}
