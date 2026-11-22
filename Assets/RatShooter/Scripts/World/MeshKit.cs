using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Accumulates vertices (position, normal, uv, colour) and triangles and turns them into a Mesh.
    /// Shapes are generated with analytic outward normals; <see cref="OrientOutward"/> then fixes the winding of every
    /// triangle so shape code never has to care about it. Vertex colour alpha is used by the toon shader as wind weight.
    /// </summary>
    public class MeshKit
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<int> t = new List<int>();

        public int Count { get { return v.Count; } }

        public int Add(Vector3 p, Vector3 normal, Vector2 tex, Color col)
        {
            v.Add(p); n.Add(normal); uv.Add(tex); c.Add(col);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }

        public void Quad(int a, int b, int d, int e) { Tri(a, b, d); Tri(a, d, e); }

        /// <summary>Flip any triangle whose face normal points against its vertex normals.</summary>
        public MeshKit OrientOutward(int fromTriangleIndex = 0)
        {
            for (int i = fromTriangleIndex; i + 2 < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], d = t[i + 2];
                Vector3 fn = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
                Vector3 vn = n[a] + n[b] + n[d];
                if (Vector3.Dot(fn, vn) < 0f) { t[i + 1] = d; t[i + 2] = b; }
            }
            return this;
        }

        /// <summary>Area-weighted smooth normals, averaged across vertices that share a position (UV seams).</summary>
        public MeshKit SmoothNormals()
        {
            Vector3[] acc = new Vector3[v.Count];
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], d = t[i + 2];
                Vector3 fn = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
                acc[a] += fn; acc[b] += fn; acc[d] += fn;
            }
            Dictionary<Vector3Int, Vector3> shared = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < v.Count; i++)
            {
                Vector3Int k = Key(v[i]);
                Vector3 s;
                shared.TryGetValue(k, out s);
                shared[k] = s + acc[i];
            }
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 s = shared[Key(v[i])];
                n[i] = s.sqrMagnitude > 1e-12f ? s.normalized : Vector3.up;
            }
            return this;
        }

        static Vector3Int Key(Vector3 p) { return new Vector3Int(Mathf.RoundToInt(p.x * 4000f), Mathf.RoundToInt(p.y * 4000f), Mathf.RoundToInt(p.z * 4000f)); }

        public MeshKit Tint(Color col)
        {
            for (int i = 0; i < c.Count; i++) c[i] = c[i] * col;
            return this;
        }

        public MeshKit SetColor(Color col)
        {
            for (int i = 0; i < c.Count; i++) c[i] = col;
            return this;
        }

        /// <summary>Wind weight in vertex alpha: 0 at minY (rooted) to 1 at maxY (tips sway).</summary>
        public MeshKit WindByHeight(float minY, float maxY)
        {
            for (int i = 0; i < c.Count; i++)
            {
                Color col = c[i];
                float h = Mathf.Clamp01((v[i].y - minY) / Mathf.Max(0.001f, maxY - minY));
                col.a = h * h;
                c[i] = col;
            }
            return this;
        }

        /// <summary>Darken vertices close to the ground (cheap contact ambient occlusion).</summary>
        public MeshKit GroundOcclusion(float height, float strength)
        {
            for (int i = 0; i < c.Count; i++)
            {
                float k = 1f - strength * (1f - Mathf.Clamp01(v[i].y / Mathf.Max(0.001f, height)));
                Color col = c[i];
                c[i] = new Color(col.r * k, col.g * k, col.b * k, col.a);
            }
            return this;
        }

        public void Append(MeshKit o, Matrix4x4 m)
        {
            int b = v.Count;
            Matrix4x4 nm = m.inverse.transpose;
            for (int i = 0; i < o.v.Count; i++)
            {
                v.Add(m.MultiplyPoint3x4(o.v[i]));
                n.Add(nm.MultiplyVector(o.n[i]).normalized);
                uv.Add(o.uv[i]);
                c.Add(o.c[i]);
            }
            for (int i = 0; i < o.t.Count; i++) t.Add(o.t[i] + b);
        }

        public void Append(MeshKit o, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Append(o, Matrix4x4.TRS(pos, rot, scale));
        }

        public void Append(MeshKit o) { Append(o, Matrix4x4.identity); }

        public MeshKit Transformed(Matrix4x4 m)
        {
            MeshKit k = new MeshKit();
            k.Append(this, m);
            return k;
        }

        public Mesh ToMesh(string name)
        {
            Mesh m = new Mesh();
            m.name = name;
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
