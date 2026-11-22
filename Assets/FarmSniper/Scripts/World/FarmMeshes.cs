using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    /// <summary>Small helper that accumulates triangles and turns them into a Mesh.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public int Count { get { return v.Count; } }

        public int Add(Vector3 p, Vector3 normal, Vector2 tex)
        {
            v.Add(p); n.Add(normal); uv.Add(tex);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }

        /// <summary>Quad a-b-c-d (counter-clockwise seen from the front), flat normal.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool doubleSided = false, float u0 = 0f, float u1 = 1f, float v0 = 0f, float v1 = 1f)
        {
            Vector3 nn = Vector3.Cross(b - a, d - a).normalized;
            int i0 = Add(a, nn, new Vector2(u0, v0)), i1 = Add(b, nn, new Vector2(u1, v0));
            int i2 = Add(c, nn, new Vector2(u1, v1)), i3 = Add(d, nn, new Vector2(u0, v1));
            Tri(i0, i1, i2); Tri(i0, i2, i3);
            if (doubleSided)
            {
                int j0 = Add(a, -nn, new Vector2(u0, v0)), j1 = Add(b, -nn, new Vector2(u1, v0));
                int j2 = Add(c, -nn, new Vector2(u1, v1)), j3 = Add(d, -nn, new Vector2(u0, v1));
                Tri(j0, j2, j1); Tri(j0, j3, j2);
            }
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, bool doubleSided, Vector3 bend)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            Vector3 nb = (nn + bend).normalized;
            int i0 = Add(a, nb, new Vector2(0f, 0f)), i1 = Add(b, nb, new Vector2(1f, 0f)), i2 = Add(c, nb, new Vector2(0.5f, 1f));
            Tri(i0, i1, i2);
            if (doubleSided)
            {
                int j0 = Add(a, -nb, new Vector2(0f, 0f)), j1 = Add(b, -nb, new Vector2(1f, 0f)), j2 = Add(c, -nb, new Vector2(0.5f, 1f));
                Tri(j0, j2, j1);
            }
        }

        public void Append(Mesh m, Matrix4x4 mat)
        {
            Vector3[] mv = m.vertices; Vector3[] mn = m.normals; Vector2[] mu = m.uv; int[] mt = m.triangles;
            int b = v.Count;
            for (int i = 0; i < mv.Length; i++)
            {
                v.Add(mat.MultiplyPoint3x4(mv[i]));
                n.Add(mn.Length > i ? mat.MultiplyVector(mn[i]).normalized : Vector3.up);
                uv.Add(mu.Length > i ? mu[i] : Vector2.zero);
            }
            for (int i = 0; i < mt.Length; i++) t.Add(mt[i] + b);
        }

        public void Append(MeshBuilder o, Matrix4x4 mat)
        {
            int b = v.Count;
            for (int i = 0; i < o.v.Count; i++)
            {
                v.Add(mat.MultiplyPoint3x4(o.v[i]));
                n.Add(mat.MultiplyVector(o.n[i]).normalized);
                uv.Add(o.uv[i]);
            }
            for (int i = 0; i < o.t.Count; i++) t.Add(o.t[i] + b);
        }

        public Mesh ToMesh(string name, bool recalcNormals = false)
        {
            Mesh m = new Mesh();
            m.name = name;
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Procedural meshes for the farm (all generated in code, like the rest of the project).</summary>
    public static class FarmMeshes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static void ClearCache() { cache.Clear(); }

        static bool TryCache(string key, out Mesh m) { return cache.TryGetValue(key, out m) && m != null; }

        // ---------------------------------------------------------------- basic solids

        /// <summary>Cylinder / cone frustum along +Y from 0 to height.</summary>
        public static Mesh Cylinder(int seg, float rBottom, float rTop, float height, bool caps, string key = null)
        {
            key = key ?? ("cyl" + seg + "_" + rBottom + "_" + rTop + "_" + height + caps);
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = CylinderBuilder(seg, rBottom, rTop, height, caps);
            Mesh m = b.ToMesh("Farm_" + key);
            cache[key] = m;
            return m;
        }

        public static MeshBuilder CylinderBuilder(int seg, float rBottom, float rTop, float height, bool caps)
        {
            MeshBuilder b = new MeshBuilder();
            float slope = (rBottom - rTop) / height;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 nn = new Vector3(dir.x, slope, dir.z).normalized;
                b.Add(dir * rBottom, nn, new Vector2(i / (float)seg, 0f));
                b.Add(dir * rTop + Vector3.up * height, nn, new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = i * 2 + 2, b1 = i * 2 + 3;
                b.Tri(a0, a1, b1); b.Tri(a0, b1, b0);
            }
            if (caps)
            {
                int cb = b.Add(Vector3.zero, Vector3.down, new Vector2(0.5f, 0.5f));
                int ct = b.Add(Vector3.up * height, Vector3.up, new Vector2(0.5f, 0.5f));
                int firstB = b.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    b.Add(dir * rBottom, Vector3.down, new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
                    b.Add(dir * rTop + Vector3.up * height, Vector3.up, new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
                }
                for (int i = 0; i < seg; i++)
                {
                    int p = firstB + i * 2, q = firstB + i * 2 + 2;
                    b.Tri(cb, p, q);
                    b.Tri(ct, q + 1, p + 1);
                }
            }
            return b;
        }

        /// <summary>Icosphere with seeded noise displacement - foliage clumps, bushes, rocks, cabbage heads.</summary>
        public static Mesh Blob(int seed, int subdiv, float noise, string key = null)
        {
            key = key ?? ("blob" + seed + "_" + subdiv + "_" + noise);
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = BlobBuilder(seed, subdiv, noise, Vector3.one);
            Mesh m = b.ToMesh("Farm_" + key, true);
            cache[key] = m;
            return m;
        }

        public static MeshBuilder BlobBuilder(int seed, int subdiv, float noise, Vector3 scale)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            Icosphere(subdiv, verts, tris);
            float ox = seed * 13.17f, oz = seed * 7.31f;
            MeshBuilder b = new MeshBuilder();
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                float k = Mathf.PerlinNoise(p.x * 2.1f + ox, p.y * 2.1f + p.z * 1.3f + oz) - 0.5f;
                k += (Mathf.PerlinNoise(p.z * 5.3f + oz, p.x * 5.3f - p.y * 2f + ox) - 0.5f) * 0.5f;
                Vector3 q = p * (1f + k * noise);
                q = Vector3.Scale(q, scale);
                b.Add(q, p, new Vector2(Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) + 0.5f, p.y * 0.5f + 0.5f));
            }
            for (int i = 0; i < tris.Count; i += 3) b.Tri(tris[i], tris[i + 1], tris[i + 2]);
            return b;
        }

        static void Icosphere(int subdiv, List<Vector3> verts, List<int> tris)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] baseV =
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            int[] baseT =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < baseV.Length; i++) verts.Add(baseV[i].normalized);
            tris.AddRange(baseT);
            Dictionary<long, int> mid = new Dictionary<long, int>();
            for (int s = 0; s < subdiv; s++)
            {
                List<int> nt = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b, verts, mid), bc = Mid(b, c, verts, mid), ca = Mid(c, a, verts, mid);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris.Clear();
                tris.AddRange(nt);
            }
        }

        static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }

        /// <summary>Box from min to max corners (used for planks, walls).</summary>
        public static void Box(MeshBuilder b, Vector3 c, Vector3 size, Quaternion rot, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            for (int i = 0; i < 8; i++) p[i] = c + rot * p[i];
            float ux = size.x * uvScale, uy = size.y * uvScale, uz = size.z * uvScale;
            b.Quad(p[1], p[0], p[3], p[2], false, 0, ux, 0, uy);   // -z
            b.Quad(p[4], p[5], p[6], p[7], false, 0, ux, 0, uy);   // +z
            b.Quad(p[0], p[4], p[7], p[3], false, 0, uz, 0, uy);   // -x
            b.Quad(p[5], p[1], p[2], p[6], false, 0, uz, 0, uy);   // +x
            b.Quad(p[3], p[7], p[6], p[2], false, 0, ux, 0, uz);   // top
            b.Quad(p[0], p[1], p[5], p[4], false, 0, ux, 0, uz);   // bottom
        }

        // ---------------------------------------------------------------- roofs

        /// <summary>Gable roof: ridge along Z. width = X span (incl. overhang), length along Z, rise above the eaves.</summary>
        public static Mesh GableRoof(float width, float length, float rise, float thickness)
        {
            string key = "gable" + width + "_" + length + "_" + rise;
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = new MeshBuilder();
            float hw = width * 0.5f, hl = length * 0.5f;
            Vector3 ridgeF = new Vector3(0, rise, hl), ridgeB = new Vector3(0, rise, -hl);
            Vector3 lF = new Vector3(-hw, 0, hl), lB = new Vector3(-hw, 0, -hl), rF = new Vector3(hw, 0, hl), rB = new Vector3(hw, 0, -hl);
            float slopeLen = Mathf.Sqrt(hw * hw + rise * rise);
            // top faces (UV: u along the ridge, v down the slope - shingles run horizontally)
            b.Quad(lB, lF, ridgeF, ridgeB, false, 0, length, 0, slopeLen);
            b.Quad(rF, rB, ridgeB, ridgeF, false, 0, length, 0, slopeLen);
            // undersides
            b.Quad(lF + Vector3.down * thickness, lB + Vector3.down * thickness, ridgeB + Vector3.down * thickness, ridgeF + Vector3.down * thickness, false, 0, length, 0, slopeLen);
            b.Quad(rB + Vector3.down * thickness, rF + Vector3.down * thickness, ridgeF + Vector3.down * thickness, ridgeB + Vector3.down * thickness, false, 0, length, 0, slopeLen);
            // fascia edges
            b.Quad(lB + Vector3.down * thickness, lF + Vector3.down * thickness, lF, lB, false, 0, length, 0, thickness);
            b.Quad(rF + Vector3.down * thickness, rB + Vector3.down * thickness, rB, rF, false, 0, length, 0, thickness);
            Mesh m = b.ToMesh("Farm_" + key);
            cache[key] = m;
            return m;
        }

        /// <summary>Gable-end triangle (wall infill under a gable roof), facing +Z, base width at y = 0.</summary>
        public static Mesh GableEnd(float width, float rise, bool gambrel)
        {
            string key = "gableEnd" + width + "_" + rise + gambrel;
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = new MeshBuilder();
            float hw = width * 0.5f;
            List<Vector2> outline = GambrelProfile(width, rise, gambrel);
            // fan from the centre at mid height
            Vector3 cen = new Vector3(0, rise * 0.45f, 0);
            int ci = b.Add(cen, Vector3.forward, new Vector2(0.5f * width, cen.y));
            int first = b.Count;
            for (int i = 0; i < outline.Count; i++)
                b.Add(new Vector3(outline[i].x, outline[i].y, 0), Vector3.forward, new Vector2(outline[i].x + hw, outline[i].y));
            for (int i = 0; i < outline.Count - 1; i++) b.Tri(ci, first + i + 1, first + i);
            b.Tri(ci, first, first + outline.Count - 1);
            // back side
            int ci2 = b.Add(cen, Vector3.back, new Vector2(0.5f * width, cen.y));
            int first2 = b.Count;
            for (int i = 0; i < outline.Count; i++)
                b.Add(new Vector3(outline[i].x, outline[i].y, 0), Vector3.back, new Vector2(outline[i].x + hw, outline[i].y));
            for (int i = 0; i < outline.Count - 1; i++) b.Tri(ci2, first2 + i, first2 + i + 1);
            b.Tri(ci2, first2 + outline.Count - 1, first2);
            Mesh m = b.ToMesh("Farm_" + key);
            cache[key] = m;
            return m;
        }

        /// <summary>Roof outline from the left eave to the right eave (x, y), y = 0 at the eaves.</summary>
        public static List<Vector2> GambrelProfile(float width, float rise, bool gambrel)
        {
            float hw = width * 0.5f;
            List<Vector2> p = new List<Vector2>();
            p.Add(new Vector2(-hw, 0));
            if (gambrel)
            {
                p.Add(new Vector2(-hw * 0.62f, rise * 0.62f));
                p.Add(new Vector2(0, rise));
                p.Add(new Vector2(hw * 0.62f, rise * 0.62f));
            }
            else p.Add(new Vector2(0, rise));
            p.Add(new Vector2(hw, 0));
            return p;
        }

        /// <summary>Gambrel (barn) roof surfaces along Z, with overhang.</summary>
        public static Mesh GambrelRoof(float width, float length, float rise, float overhang, float thickness)
        {
            string key = "gambrel" + width + "_" + length + "_" + rise;
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = new MeshBuilder();
            List<Vector2> prof = GambrelProfile(width + overhang * 2f, rise + overhang * 0.4f, true);
            for (int i = 0; i < prof.Count; i++) prof[i] = new Vector2(prof[i].x, prof[i].y - overhang * 0.4f);
            float hl = length * 0.5f;
            float vAcc = 0f;
            for (int i = 0; i < prof.Count - 1; i++)
            {
                Vector2 a = prof[i], c = prof[i + 1];
                float segLen = Vector2.Distance(a, c);
                Vector3 a0 = new Vector3(a.x, a.y, -hl), a1 = new Vector3(a.x, a.y, hl);
                Vector3 c0 = new Vector3(c.x, c.y, -hl), c1 = new Vector3(c.x, c.y, hl);
                // travelling left to right over the top, the front side of (a0, a1, c1, c0) points outwards on every segment
                b.Quad(a0, a1, c1, c0, false, 0, length, vAcc, vAcc + segLen);
                Vector3 dn = Vector3.down * thickness;
                b.Quad(a1 + dn, a0 + dn, c0 + dn, c1 + dn, false, 0, length, 0, segLen);
                vAcc += segLen;
            }
            Mesh m = b.ToMesh("Farm_" + key);
            cache[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- plants

        /// <summary>One corn plant: stalk, arching leaves, a tassel and a cob. ~1.9-2.4 m tall.</summary>
        public static MeshBuilder CornPlant(System.Random rng)
        {
            MeshBuilder b = new MeshBuilder();
            float h = 1.9f + (float)rng.NextDouble() * 0.5f;
            // stalk: 4-sided tapered prism
            float r0 = 0.022f, r1 = 0.01f;
            for (int s = 0; s < 4; s++)
            {
                float a0 = s * Mathf.PI * 0.5f, a1 = (s + 1) * Mathf.PI * 0.5f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                b.Quad(d1 * r0, d0 * r0, d0 * r1 + Vector3.up * h, d1 * r1 + Vector3.up * h);
            }
            int leaves = 5 + rng.Next(2);
            for (int i = 0; i < leaves; i++)
            {
                float y = h * (0.18f + 0.68f * i / (float)leaves);
                float yaw = (float)(i * 2.4 + rng.NextDouble() * 0.6);
                float len = 0.55f + (float)rng.NextDouble() * 0.35f;
                Leaf(b, new Vector3(0, y, 0), yaw, len, 0.06f, 0.55f + (float)rng.NextDouble() * 0.3f);
            }
            // tassel
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 1.6f, Mathf.Sin(yaw)).normalized;
                Vector3 p0 = new Vector3(0, h, 0);
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.012f;
                b.Triangle(p0 - side, p0 + side, p0 + dir * 0.22f, true, Vector3.up);
            }
            // cob
            MeshBuilder cob = CylinderBuilder(5, 0.028f, 0.018f, 0.2f, false);
            b.Append(cob, Matrix4x4.TRS(new Vector3(0.02f, h * 0.55f, 0), Quaternion.Euler(0, 0, -20f), Vector3.one));
            return b;
        }

        /// <summary>Curved strap leaf from origin, arching out and down.</summary>
        static void Leaf(MeshBuilder b, Vector3 origin, float yaw, float len, float width, float droop)
        {
            Vector3 dir = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            const int segs = 3;
            Vector3 prevL = origin - side * width * 0.3f, prevR = origin + side * width * 0.3f;
            for (int s = 1; s <= segs; s++)
            {
                float k = s / (float)segs;
                float w = width * Mathf.Sin(Mathf.PI * Mathf.Min(0.95f, k * 0.85f + 0.15f)) * (1f - k * 0.6f);
                Vector3 c = origin + dir * len * k + Vector3.up * (len * 0.55f * k - len * droop * k * k);
                Vector3 l = c - side * w, r = c + side * w;
                b.Quad(prevL, prevR, r, l, true);
                prevL = l; prevR = r;
            }
        }

        /// <summary>Wheat tuft: a few thin stems with seed heads. ~0.8-1.0 m.</summary>
        public static MeshBuilder WheatTuft(System.Random rng)
        {
            MeshBuilder b = new MeshBuilder();
            int stems = 4 + rng.Next(2);
            for (int i = 0; i < stems; i++)
            {
                float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
                float lean = 0.05f + (float)rng.NextDouble() * 0.18f;
                float h = 0.75f + (float)rng.NextDouble() * 0.3f;
                Vector3 baseP = new Vector3((float)rng.NextDouble() * 0.12f - 0.06f, 0, (float)rng.NextDouble() * 0.12f - 0.06f);
                Vector3 top = baseP + new Vector3(Mathf.Cos(yaw) * lean, h, Mathf.Sin(yaw) * lean);
                Vector3 side = Vector3.Cross(top - baseP, Vector3.up).normalized * 0.008f;
                b.Quad(baseP - side, baseP + side, top + side * 0.6f, top - side * 0.6f, true);
                // seed head: a flattened spindle, turned a random way
                Vector3 hd = (top - baseP).normalized;
                Vector3 s1 = (Quaternion.AngleAxis((float)rng.NextDouble() * 180f, hd) * side).normalized * 0.02f;
                Vector3 h0 = top, h1 = top + hd * 0.11f;
                b.Quad(h0 - s1, h0 + s1, h1 + s1 * 0.3f, h1 - s1 * 0.3f, true);
            }
            return b;
        }

        /// <summary>Grass tuft: tapered blades. height ~0.25-0.5 m.</summary>
        public static MeshBuilder GrassTuft(System.Random rng, float height)
        {
            MeshBuilder b = new MeshBuilder();
            int blades = 6 + rng.Next(5);
            for (int i = 0; i < blades; i++)
            {
                float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
                float h = height * (0.6f + (float)rng.NextDouble() * 0.6f);
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
                Vector3 p0 = dir * (float)rng.NextDouble() * 0.08f;
                Vector3 tip = p0 + dir * h * (0.15f + (float)rng.NextDouble() * 0.35f) + Vector3.up * h;
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * (0.012f + (float)rng.NextDouble() * 0.01f);
                b.Triangle(p0 - side, p0 + side, tip, true, Vector3.up * 0.6f);
            }
            return b;
        }

        /// <summary>Ribbed pumpkin (squashed sphere with lobes).</summary>
        public static MeshBuilder PumpkinBuilder()
        {
            MeshBuilder b = new MeshBuilder();
            int seg = 20, rings = 10;
            for (int r = 0; r <= rings; r++)
            {
                float th = r / (float)rings * Mathf.PI;
                for (int s = 0; s <= seg; s++)
                {
                    float ph = s / (float)seg * Mathf.PI * 2f;
                    float lobe = 1f - 0.09f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ph * 5f)), 0.6f) * Mathf.Sin(th);
                    float sy = Mathf.Cos(th) * 0.72f;
                    float sr = Mathf.Sin(th) * lobe;
                    Vector3 p = new Vector3(Mathf.Cos(ph) * sr, sy, Mathf.Sin(ph) * sr);
                    b.Add(p, p.normalized, new Vector2(s / (float)seg, r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s, c = a + seg + 1;
                    b.Tri(a, a + 1, c + 1); b.Tri(a, c + 1, c);
                }
            return b;
        }

        // ---------------------------------------------------------------- ground

        public static Mesh Ground(int segments, float size)
        {
            MeshBuilder b = new MeshBuilder();
            float half = size * 0.5f;
            float step = size / segments;
            for (int z = 0; z <= segments; z++)
                for (int x = 0; x <= segments; x++)
                {
                    float wx = -half + x * step, wz = -half + z * step;
                    b.Add(new Vector3(wx, FarmLayout.Height(wx, wz), wz), Vector3.up, new Vector2(x / (float)segments, z / (float)segments));
                }
            int row = segments + 1;
            for (int z = 0; z < segments; z++)
                for (int x = 0; x < segments; x++)
                {
                    int a = z * row + x;
                    b.Tri(a, a + row, a + row + 1);
                    b.Tri(a, a + row + 1, a + 1);
                }
            Mesh m = b.ToMesh("Farm_Ground");
            m.RecalculateNormals();
            return m;
        }

        /// <summary>Flat disc (pond surface, burrow hole).</summary>
        public static Mesh Disc(int seg, float radius, string key)
        {
            Mesh cached;
            if (TryCache(key, out cached)) return cached;
            MeshBuilder b = new MeshBuilder();
            int c = b.Add(Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float wob = 1f + 0.08f * Mathf.Sin(a * 3f) + 0.05f * Mathf.Sin(a * 7f + 1f);
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * wob;
                b.Add(p, Vector3.up, new Vector2(0.5f + p.x / radius * 0.5f, 0.5f + p.z / radius * 0.5f));
            }
            for (int i = 0; i < seg; i++) b.Tri(c, c + i + 2, c + i + 1);
            Mesh m = b.ToMesh("Farm_" + key);
            cache[key] = m;
            return m;
        }
    }
}
