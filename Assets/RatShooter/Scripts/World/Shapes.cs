using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Smooth, rounded primitive shapes for the cartoon style: ellipsoids / noisy blobs, rounded boxes, lathes
    /// (revolved profiles), tubes along a path, tori, stars, grass blades and a height-field grid.
    /// </summary>
    public static class Shapes
    {
        // ------------------------------------------------------------------ ellipsoid / blob

        /// <summary>UV ellipsoid. taperZ &gt; 0 narrows the +Z end (snouts, pear-shaped bodies); noise makes a lumpy blob.</summary>
        public static MeshKit Ellipsoid(Vector3 radii, int lon, int lat, Color col, float taperZ = 0f, float noise = 0f, int seed = 0, float noiseFreq = 1.6f)
        {
            MeshKit k = new MeshKit();
            Vector3 so = new Vector3(seed * 13.17f, seed * 7.31f, seed * 3.77f);
            float avg = (radii.x + radii.y + radii.z) / 3f;
            for (int y = 0; y <= lat; y++)
            {
                float vt = (float)y / lat;
                float phi = vt * Mathf.PI;
                float sy = Mathf.Cos(phi), r = Mathf.Sin(phi);
                for (int x = 0; x <= lon; x++)
                {
                    float u = (float)x / lon;
                    float th = u * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Cos(th) * r, sy, Mathf.Sin(th) * r);
                    float s = 1f - taperZ * p.z;
                    Vector3 q = new Vector3(p.x * radii.x * s, p.y * radii.y * s, p.z * radii.z);
                    if (noise > 0f) q += p * ((RatNoise.Value3(p * noiseFreq + so) - 0.5f) * 2f * noise * avg);
                    Vector3 nn = new Vector3(p.x / Mathf.Max(radii.x, 1e-4f), p.y / Mathf.Max(radii.y, 1e-4f), p.z / Mathf.Max(radii.z, 1e-4f)).normalized;
                    k.Add(q, nn, new Vector2(u, 1f - vt), col);
                }
            }
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + 1, d = a + lon + 1, e = d + 1;
                    k.Quad(a, b, e, d);
                }
            }
            k.OrientOutward();
            if (noise > 0f || Mathf.Abs(taperZ) > 1e-4f) k.SmoothNormals();
            return k;
        }

        public static MeshKit Sphere(float r, int lon, int lat, Color col) { return Ellipsoid(new Vector3(r, r, r), lon, lat, col); }

        public static MeshKit Blob(Vector3 radii, int seed, float noise, Color col, int lon = 14, int lat = 10)
        {
            return Ellipsoid(radii, lon, lat, col, 0f, noise, seed);
        }

        // ------------------------------------------------------------------ rounded box

        static List<float> AxisCoords(float h, float r, int steps, int inner)
        {
            List<float> l = new List<float>();
            r = Mathf.Clamp(r, 0f, h);
            if (r > 1e-5f)
                for (int s = 0; s < steps; s++) l.Add(-(h - r) - r * Mathf.Sin((1f - (float)s / steps) * Mathf.PI * 0.5f));
            for (int s = 0; s <= inner; s++) l.Add(Mathf.Lerp(-(h - r), h - r, (float)s / inner));
            if (r > 1e-5f)
                for (int s = 1; s <= steps; s++) l.Add((h - r) + r * Mathf.Sin((float)s / steps * Mathf.PI * 0.5f));
            return l;
        }

        static float Comp(Vector3 a, int i) { return i == 0 ? a.x : (i == 1 ? a.y : a.z); }

        static int AxisIndex(Vector3 a) { return Mathf.Abs(a.x) > 0.5f ? 0 : (Mathf.Abs(a.y) > 0.5f ? 1 : 2); }

        /// <summary>Box with rounded edges and corners (flat faces, smooth bevels). Centered on the origin.</summary>
        public static MeshKit RoundedBox(Vector3 size, float radius, Color col, float uvScale = 1f, int steps = 3, int inner = 1)
        {
            MeshKit k = new MeshKit();
            Vector3 H = size * 0.5f;
            float rad = Mathf.Min(radius, Mathf.Min(H.x, Mathf.Min(H.y, H.z)));
            Vector3 lim = H - Vector3.one * rad;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] uAxes = { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right };
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], U = uAxes[f], V = Vector3.Cross(N, U);
                int ni = AxisIndex(N), ui = AxisIndex(U), vi = AxisIndex(V);
                List<float> us = AxisCoords(Comp(H, ui), rad, steps, inner);
                List<float> vs = AxisCoords(Comp(H, vi), rad, steps, inner);
                int start = k.Count;
                for (int j = 0; j < vs.Count; j++)
                {
                    for (int i = 0; i < us.Count; i++)
                    {
                        Vector3 p = N * Comp(H, ni) + U * us[i] + V * vs[j];
                        Vector3 innerP = new Vector3(Mathf.Clamp(p.x, -lim.x, lim.x), Mathf.Clamp(p.y, -lim.y, lim.y), Mathf.Clamp(p.z, -lim.z, lim.z));
                        Vector3 d = p - innerP;
                        Vector3 nrm = d.sqrMagnitude > 1e-12f ? d.normalized : N;
                        Vector3 pos = rad > 1e-5f ? innerP + nrm * rad : p;
                        k.Add(pos, nrm, new Vector2(Vector3.Dot(pos, U), Vector3.Dot(pos, V)) * uvScale, col);
                    }
                }
                int w = us.Count;
                for (int j = 0; j < vs.Count - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = start + j * w + i;
                        k.Quad(a, a + 1, a + w + 1, a + w);
                    }
            }
            k.OrientOutward();
            return k;
        }

        // ------------------------------------------------------------------ lathe

        /// <summary>Revolve a (radius, y) profile, listed bottom to top, around the Y axis.</summary>
        public static MeshKit Lathe(IList<Vector2> prof, int seg, Color col, float vScale = 1f, float uRepeat = 1f)
        {
            MeshKit k = new MeshKit();
            int pc = prof.Count;
            float len = 0f;
            for (int i = 0; i < pc; i++)
            {
                if (i > 0) len += Vector2.Distance(prof[i], prof[i - 1]);
                Vector2 prev = prof[Mathf.Max(i - 1, 0)], next = prof[Mathf.Min(i + 1, pc - 1)];
                Vector2 tan = (next - prev);
                if (tan.sqrMagnitude < 1e-12f) tan = Vector2.up;
                tan.Normalize();
                Vector2 n2 = new Vector2(tan.y, -tan.x);
                for (int j = 0; j <= seg; j++)
                {
                    float a = (float)j / seg * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 pos = new Vector3(prof[i].x * ca, prof[i].y, prof[i].x * sa);
                    Vector3 nrm = new Vector3(n2.x * ca, n2.y, n2.x * sa).normalized;
                    k.Add(pos, nrm, new Vector2((float)j / seg * uRepeat, len * vScale), col);
                }
            }
            for (int i = 0; i < pc - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j;
                    k.Quad(a, a + 1, a + seg + 2, a + seg + 1);
                }
            k.OrientOutward();
            return k;
        }

        /// <summary>Profile of a cylinder of radius r and height h with rounded (bevelled) top and bottom edges.</summary>
        public static List<Vector2> RoundedCylinderProfile(float r, float h, float bevel, int steps = 3)
        {
            List<Vector2> p = new List<Vector2>();
            bevel = Mathf.Min(bevel, Mathf.Min(r, h * 0.5f));
            p.Add(new Vector2(0f, 0f));
            for (int s = 0; s <= steps; s++)
            {
                float a = -Mathf.PI * 0.5f + (float)s / steps * Mathf.PI * 0.5f;
                p.Add(new Vector2(r - bevel + Mathf.Cos(a) * bevel, bevel + Mathf.Sin(a) * bevel));
            }
            for (int s = 0; s <= steps; s++)
            {
                float a = (float)s / steps * Mathf.PI * 0.5f;
                p.Add(new Vector2(r - bevel + Mathf.Cos(a) * bevel, h - bevel + Mathf.Sin(a) * bevel));
            }
            p.Add(new Vector2(0f, h));
            return p;
        }

        public static MeshKit RoundedCylinder(float r, float h, float bevel, int seg, Color col, float vScale = 1f)
        {
            return Lathe(RoundedCylinderProfile(r, h, bevel), seg, col, vScale);
        }

        // ------------------------------------------------------------------ torus / tube

        public static MeshKit Torus(float R, float r, int segR, int segr, Color col, float arcDegrees = 360f)
        {
            MeshKit k = new MeshKit();
            float arc = arcDegrees * Mathf.Deg2Rad;
            for (int i = 0; i <= segR; i++)
            {
                float u = (float)i / segR * arc;
                Vector3 radial = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= segr; j++)
                {
                    float w = (float)j / segr * Mathf.PI * 2f;
                    Vector3 dir = radial * Mathf.Cos(w) + Vector3.up * Mathf.Sin(w);
                    k.Add(radial * R + dir * r, dir, new Vector2((float)i / segR, (float)j / segr), col);
                }
            }
            for (int i = 0; i < segR; i++)
                for (int j = 0; j < segr; j++)
                {
                    int a = i * (segr + 1) + j;
                    k.Quad(a, a + 1, a + segr + 2, a + segr + 1);
                }
            k.OrientOutward();
            return k;
        }

        /// <summary>Tube along a path (rotation-minimising frames), radius interpolated from r0 to r1, optional end caps.</summary>
        public static MeshKit Tube(IList<Vector3> path, float r0, float r1, int seg, Color col, bool caps = true)
        {
            MeshKit k = new MeshKit();
            int pc = path.Count;
            if (pc < 2) return k;
            Vector3[] T = new Vector3[pc];
            for (int i = 0; i < pc; i++)
            {
                Vector3 d = path[Mathf.Min(i + 1, pc - 1)] - path[Mathf.Max(i - 1, 0)];
                T[i] = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
            }
            Vector3 N = Vector3.Cross(T[0], Mathf.Abs(T[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float len = 0f;
            for (int i = 0; i < pc; i++)
            {
                if (i > 0)
                {
                    len += Vector3.Distance(path[i], path[i - 1]);
                    N = (N - T[i] * Vector3.Dot(N, T[i])).normalized;
                }
                Vector3 B = Vector3.Cross(T[i], N);
                float r = Mathf.Lerp(r0, r1, (float)i / (pc - 1));
                for (int j = 0; j <= seg; j++)
                {
                    float a = (float)j / seg * Mathf.PI * 2f;
                    Vector3 dir = N * Mathf.Cos(a) + B * Mathf.Sin(a);
                    k.Add(path[i] + dir * r, dir, new Vector2((float)j / seg, len), col);
                }
            }
            for (int i = 0; i < pc - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j;
                    k.Quad(a, a + 1, a + seg + 2, a + seg + 1);
                }
            if (caps)
            {
                for (int e = 0; e < 2; e++)
                {
                    int ring = e == 0 ? 0 : pc - 1;
                    Vector3 nn = e == 0 ? -T[0] : T[pc - 1];
                    int center = k.Add(path[ring], nn, new Vector2(0.5f, 0.5f), col);
                    int first = k.Count;
                    for (int j = 0; j <= seg; j++)
                    {
                        int src = ring * (seg + 1) + j;
                        k.Add(k.v[src], nn, k.uv[src], col);
                    }
                    for (int j = 0; j < seg; j++) k.Tri(center, first + j, first + j + 1);
                }
            }
            k.OrientOutward();
            return k;
        }

        // ------------------------------------------------------------------ flat shapes

        /// <summary>Flat star in the XY plane facing -Z (towards a camera when rotated like it). Centre opaque, tips faded.</summary>
        public static MeshKit Star(int points, float rOuter, float rInner, Color center, Color tip)
        {
            MeshKit k = new MeshKit();
            int c = k.Add(Vector3.zero, Vector3.back, new Vector2(0.5f, 0.5f), center);
            int n = points * 2;
            for (int i = 0; i <= n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                float r = i % 2 == 0 ? rOuter : rInner;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                k.Add(p, Vector3.back, new Vector2(p.x / (2f * rOuter) + 0.5f, p.y / (2f * rOuter) + 0.5f), i % 2 == 0 ? tip : Color.Lerp(center, tip, 0.35f));
            }
            for (int i = 0; i < n; i++) k.Tri(c, c + 1 + i, c + 2 + i);
            k.OrientOutward();
            return k;
        }

        /// <summary>Unit quad in the XY plane, centred, facing -Z.</summary>
        public static MeshKit Quad(float w, float h, Color col)
        {
            MeshKit k = new MeshKit();
            int a = k.Add(new Vector3(-w * 0.5f, -h * 0.5f, 0f), Vector3.back, new Vector2(0f, 0f), col);
            int b = k.Add(new Vector3(w * 0.5f, -h * 0.5f, 0f), Vector3.back, new Vector2(1f, 0f), col);
            int d = k.Add(new Vector3(w * 0.5f, h * 0.5f, 0f), Vector3.back, new Vector2(1f, 1f), col);
            int e = k.Add(new Vector3(-w * 0.5f, h * 0.5f, 0f), Vector3.back, new Vector2(0f, 1f), col);
            k.Quad(a, b, d, e);
            k.OrientOutward();
            return k;
        }

        /// <summary>Disc in the XZ plane facing up.</summary>
        public static MeshKit Disc(float r, int seg, Color center, Color rim)
        {
            MeshKit k = new MeshKit();
            int c = k.Add(Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f), center);
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                k.Add(p, Vector3.up, new Vector2(p.x / (2f * r) + 0.5f, p.z / (2f * r) + 0.5f), rim);
            }
            for (int i = 0; i < seg; i++) k.Tri(c, c + 1 + i, c + 2 + i);
            k.OrientOutward();
            return k;
        }

        /// <summary>Triangular prism (wedge) - cheese wedges, roof gables. Base in XZ, apex line along Z at height h.</summary>
        public static MeshKit Wedge(float width, float height, float depth, Color col)
        {
            MeshKit k = new MeshKit();
            Vector3 a = new Vector3(-width * 0.5f, 0f, -depth * 0.5f), b = new Vector3(width * 0.5f, 0f, -depth * 0.5f), t = new Vector3(-width * 0.5f, height, -depth * 0.5f);
            Vector3 dz = new Vector3(0f, 0f, depth);
            Vector3 center = (a + b + t) / 3f + dz * 0.5f;
            AddFace(k, new[] { a, b, t }, col, center);
            AddFace(k, new[] { a + dz, t + dz, b + dz }, col, center);
            AddFace(k, new[] { a, a + dz, b + dz, b }, col, center);
            AddFace(k, new[] { a, t, t + dz, a + dz }, col, center);
            AddFace(k, new[] { b, b + dz, t + dz, t }, col, center);
            k.OrientOutward();
            return k;
        }

        /// <summary>Flat convex polygon whose normal points away from 'center'.</summary>
        static void AddFace(MeshKit k, Vector3[] pts, Color col, Vector3 center)
        {
            Vector3 nrm = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
            Vector3 fc = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) fc += pts[i];
            fc /= pts.Length;
            if (Vector3.Dot(nrm, fc - center) < 0f) nrm = -nrm;
            int s = k.Count;
            for (int i = 0; i < pts.Length; i++) k.Add(pts[i], nrm, new Vector2(pts[i].x + pts[i].z, pts[i].y), col);
            for (int i = 1; i < pts.Length - 1; i++) k.Tri(s, s + i, s + i + 1);
        }

        // ------------------------------------------------------------------ vegetation

        /// <summary>A tuft of curved grass blades (vertex-coloured dark root to bright tip, wind weight by height).</summary>
        public static MeshKit GrassTuft(System.Random rng, float height, Color root, Color tip)
        {
            MeshKit k = new MeshKit();
            int blades = 3 + rng.Next(3);
            for (int b = 0; b < blades; b++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float h = height * (0.65f + (float)rng.NextDouble() * 0.5f);
                float lean = 0.25f + (float)rng.NextDouble() * 0.35f;
                float w = 0.035f + (float)rng.NextDouble() * 0.02f;
                Vector3 basePos = dir * (float)rng.NextDouble() * 0.06f;
                const int segs = 3;
                int start = k.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float f = (float)s / segs;
                    Vector3 c = basePos + Vector3.up * (h * f) + dir * (lean * h * f * f);
                    float ww = w * (1f - f * 0.92f);
                    Color col = Color.Lerp(root, tip, f);
                    Vector3 nrm = (Vector3.up * 0.8f - dir * 0.4f).normalized;
                    k.Add(c - side * ww, nrm, new Vector2(0f, f), col);
                    k.Add(c + side * ww, nrm, new Vector2(1f, f), col);
                }
                for (int s = 0; s < segs; s++)
                {
                    int a = start + s * 2;
                    k.Tri(a, a + 1, a + 3);
                    k.Tri(a, a + 3, a + 2);
                }
            }
            k.WindByHeight(0f, height);
            return k;
        }

        /// <summary>A long, arching leaf strip (corn leaves, carrot tops). Base at origin, grows along dir.</summary>
        public static MeshKit LeafStrip(Vector3 dir, float length, float width, float droop, Color root, Color tip, int segs = 5)
        {
            MeshKit k = new MeshKit();
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z).normalized + Vector3.forward * 1e-4f).normalized;
            for (int s = 0; s <= segs; s++)
            {
                float f = (float)s / segs;
                Vector3 c = dir * (length * f) + Vector3.down * (droop * f * f * length);
                float ww = width * Mathf.Sin(Mathf.Lerp(0.25f, 1f, f) * Mathf.PI) * (1f - f * 0.3f);
                Color col = Color.Lerp(root, tip, f);
                k.Add(c - side * ww, Vector3.up, new Vector2(0f, f), col);
                k.Add(c + side * ww, Vector3.up, new Vector2(1f, f), col);
            }
            for (int s = 0; s < segs; s++)
            {
                int a = s * 2;
                k.Tri(a, a + 1, a + 3);
                k.Tri(a, a + 3, a + 2);
            }
            return k;
        }

        // ------------------------------------------------------------------ height-field

        public delegate float HeightFn(float x, float z);
        public delegate Color ColorFn(float x, float z);

        /// <summary>Grid over [x0, x0+sx] x [z0, z0+sz] with heights and vertex colours from functions.</summary>
        public static MeshKit Grid(float x0, float z0, float sx, float sz, int rx, int rz, HeightFn height, ColorFn color, float uvTile)
        {
            MeshKit k = new MeshKit();
            for (int j = 0; j <= rz; j++)
            {
                for (int i = 0; i <= rx; i++)
                {
                    float x = x0 + sx * i / rx, z = z0 + sz * j / rz;
                    k.Add(new Vector3(x, height(x, z), z), Vector3.up, new Vector2(x, z) / uvTile, color(x, z));
                }
            }
            for (int j = 0; j < rz; j++)
                for (int i = 0; i < rx; i++)
                {
                    int a = j * (rx + 1) + i;
                    k.Quad(a, a + 1, a + rx + 2, a + rx + 1);
                }
            k.OrientOutward();
            k.SmoothNormals();
            return k;
        }
    }
}
