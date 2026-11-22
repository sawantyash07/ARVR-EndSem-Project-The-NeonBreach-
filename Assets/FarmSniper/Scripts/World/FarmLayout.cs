using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Shared description of the farm: terrain height, fields, roads and building positions. The world builder,
    /// the ground texture and the gameplay code all read from here so everything lines up.
    /// Coordinates are world metres; +Z is north (towards the fields), the farm gate is in the south.
    /// </summary>
    public static class FarmLayout
    {
        public const float GroundSize = 420f;     // whole ground mesh, incl. distant hills
        public const float FenceHalf = 95f;       // perimeter fence (square)
        public const float GateHalfWidth = 4f;    // gap in the south fence

        public enum CropKind { Corn, Wheat, Cabbage, Pumpkin, Garden }

        public struct Field
        {
            public string name;
            public CropKind kind;
            public Rect rect;          // x = west edge, y = south edge, in world XZ
            public Field(string n, CropKind k, float x, float z, float w, float h) { name = n; kind = k; rect = new Rect(x, z, w, h); }
        }

        public static readonly Field[] Fields =
        {
            new Field("Corn Field", CropKind.Corn, -82f, 8f, 56f, 50f),
            new Field("Cabbage Rows", CropKind.Cabbage, -18f, 18f, 24f, 46f),
            new Field("Wheat Field", CropKind.Wheat, 17f, 10f, 63f, 52f),
            new Field("Pumpkin Patch", CropKind.Pumpkin, 48f, -40f, 34f, 26f),
            new Field("Vegetable Garden", CropKind.Garden, -64f, -40f, 15f, 10f),
        };

        // dirt roads as polylines (x, z) with a width
        public static readonly Vector2[][] Roads =
        {
            new[] { new Vector2(0f, -110f), new Vector2(1f, -70f), new Vector2(2f, -38f) },
            new[] { new Vector2(-40f, -44f), new Vector2(-10f, -40f), new Vector2(12f, -40f), new Vector2(30f, -47f) },
            new[] { new Vector2(11f, -40f), new Vector2(11.5f, 10f), new Vector2(11f, 70f) },
            new[] { new Vector2(11f, 67f), new Vector2(-30f, 64f), new Vector2(-86f, 63f) },
            new[] { new Vector2(12f, -40f), new Vector2(44f, -44f), new Vector2(50f, -12f), new Vector2(82f, -8f) },
        };
        public const float RoadWidth = 3.6f;

        public static readonly Vector2 YardCenter = new Vector2(-2f, -42f);
        public static readonly Vector2 YardRadii = new Vector2(34f, 13f);

        public static readonly Vector3 HousePos = new Vector3(-40f, 0f, -52f);
        public static readonly Vector3 BarnPos = new Vector3(31f, 0f, -61f);
        public static readonly Vector3 SiloPos = new Vector3(44f, 0f, -67f);
        public static readonly Vector3 TowerPos = new Vector3(-2f, 0f, -16f);
        public static readonly Vector3 PlayerSpawn = new Vector3(-2f, 0f, -26f);

        public static readonly Vector2 PondCenter = new Vector2(62f, 80f);
        public const float PondRadius = 10f;

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float z, int oct)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++)
            {
                sum += (Mathf.PerlinNoise(x * f + 31.7f * i, z * f - 17.3f * i) - 0.5f) * amp;
                f *= 2.03f; amp *= 0.5f;
            }
            return sum;
        }

        /// <summary>Terrain height in metres. Gentle unevenness inside the farm, rolling hills outside the fence.</summary>
        public static float Height(float x, float z)
        {
            float h = Fbm(x * 0.035f, z * 0.035f, 3) * 0.35f;

            float d = Mathf.Sqrt(x * x + z * z);
            float hill = Smooth(102f, 200f, d);
            h += hill * hill * (16f + 26f * Mathf.PerlinNoise(x * 0.011f + 5f, z * 0.011f + 9f));
            h += hill * Fbm(x * 0.02f, z * 0.02f, 3) * 6f;

            float pd = Vector2.Distance(new Vector2(x, z), PondCenter);
            h -= (1f - Smooth(PondRadius * 0.35f, PondRadius * 1.25f, pd)) * 1.4f;

            // keep building pads and the yard flat
            float flat = Mathf.Max(Pad(x, z, HousePos, 12f), Pad(x, z, BarnPos, 15f));
            flat = Mathf.Max(flat, Pad(x, z, SiloPos, 6f));
            flat = Mathf.Max(flat, Pad(x, z, TowerPos, 6f));
            return Mathf.Lerp(h, 0f, flat);
        }

        static float Pad(float x, float z, Vector3 c, float r)
        {
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.z));
            return 1f - Smooth(r, r * 1.6f, d);
        }

        public static Vector3 OnGround(float x, float z, float yOffset = 0f) { return new Vector3(x, Height(x, z) + yOffset, z); }

        public static float RoadDistance(float x, float z)
        {
            float best = 1e9f;
            Vector2 p = new Vector2(x, z);
            for (int r = 0; r < Roads.Length; r++)
            {
                Vector2[] line = Roads[r];
                for (int i = 0; i < line.Length - 1; i++)
                {
                    Vector2 a = line[i], b = line[i + 1];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    float dd = Vector2.Distance(p, a + ab * t);
                    if (dd < best) best = dd;
                }
            }
            return best;
        }

        public static int FieldAt(float x, float z, float margin = 0f)
        {
            for (int i = 0; i < Fields.Length; i++)
            {
                Rect r = Fields[i].rect;
                if (x >= r.xMin - margin && x <= r.xMax + margin && z >= r.yMin - margin && z <= r.yMax + margin) return i;
            }
            return -1;
        }

        /// <summary>Footprints of the big buildings (no grass inside them).</summary>
        public static bool NearBuilding(float x, float z)
        {
            if (Mathf.Abs(x - HousePos.x) < 8f && Mathf.Abs(z - HousePos.z) < 8f) return true;
            if (Mathf.Abs(x - BarnPos.x) < 8.5f && Mathf.Abs(z - BarnPos.z) < 12.5f) return true;
            return Vector2.Distance(new Vector2(x, z), new Vector2(SiloPos.x, SiloPos.z)) < 4f;
        }

        public static bool InYard(float x, float z)
        {
            Vector2 d = new Vector2((x - YardCenter.x) / YardRadii.x, (z - YardCenter.y) / YardRadii.y);
            return d.sqrMagnitude < 1f;
        }
    }
}
