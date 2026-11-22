using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Grows swaying grass tufts and little cartoon flowers over the yard from a seed when the scene loads (also in
    /// the Editor), combined into a few chunk meshes. They are regenerated instead of saved, which keeps the scene
    /// file small. Areas listed in the exclusions (paths, garden beds, props) stay clear.
    /// </summary>
    [ExecuteAlways]
    public class GrassPlanter : MonoBehaviour
    {
        public Material material;
        [Tooltip("x, z, width, depth of the planted area.")]
        public Rect area = new Rect(-18.5f, 1.2f, 37f, 32.5f);
        public float chunkSize = 8f;
        [Tooltip("Tufts per square metre close to the porch (thins out further away).")]
        public float density = 3.2f;
        public float flowerDensity = 0.1f;
        public int seed = 1234;
        [Tooltip("Circles to keep clear: x, z, radius.")]
        public List<Vector3> clearCircles = new List<Vector3>();
        [Tooltip("Rectangles to keep clear: xMin, zMin, xMax, zMax.")]
        public List<Vector4> clearRects = new List<Vector4>();
        [Tooltip("Polyline the dirt path follows (x, z) and its half width.")]
        public List<Vector2> pathPoints = new List<Vector2>();
        public float pathHalfWidth = 0.9f;

        readonly List<GameObject> chunks = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();

        void OnEnable() { Plant(); }
        void OnDisable() { Clear(); }

        void Clear()
        {
            for (int i = 0; i < chunks.Count; i++) if (chunks[i] != null) Kill(chunks[i]);
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Kill(meshes[i]);
            chunks.Clear();
            meshes.Clear();
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        bool Blocked(float x, float z)
        {
            for (int i = 0; i < clearCircles.Count; i++)
            {
                Vector3 c = clearCircles[i];
                float dx = x - c.x, dz = z - c.y;
                if (dx * dx + dz * dz < c.z * c.z) return true;
            }
            for (int i = 0; i < clearRects.Count; i++)
            {
                Vector4 r = clearRects[i];
                if (x > r.x && x < r.z && z > r.y && z < r.w) return true;
            }
            for (int i = 0; i + 1 < pathPoints.Count; i++)
            {
                Vector2 a = pathPoints[i], b = pathPoints[i + 1], p = new Vector2(x, z);
                Vector2 ab = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                if ((p - (a + ab * h)).sqrMagnitude < pathHalfWidth * pathHalfWidth) return true;
            }
            return false;
        }

        public void Plant()
        {
            Clear();
            if (material == null) return;
            System.Random vr = new System.Random(seed);
            MeshKit[] tufts = new MeshKit[8];
            for (int i = 0; i < tufts.Length; i++)
            {
                Color root = new Color(0.2f, 0.4f, 0.12f), tip = Color.Lerp(new Color(0.4f, 0.72f, 0.26f), new Color(0.56f, 0.8f, 0.3f), (float)vr.NextDouble());
                tufts[i] = Shapes.GrassTuft(vr, 0.24f + (float)vr.NextDouble() * 0.18f, root, tip);
            }
            Color[] petals = { new Color(1f, 1f, 1f), new Color(1f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.7f), new Color(0.7f, 0.55f, 1f), new Color(1f, 0.45f, 0.3f) };
            MeshKit[] flowers = new MeshKit[petals.Length];
            for (int i = 0; i < flowers.Length; i++) flowers[i] = Flower(petals[i]);

            int nx = Mathf.CeilToInt(area.width / chunkSize), nz = Mathf.CeilToInt(area.height / chunkSize);
            for (int cz = 0; cz < nz; cz++)
            {
                for (int cx = 0; cx < nx; cx++)
                {
                    System.Random rng = new System.Random(seed + cx * 7919 + cz * 104729);
                    float x0 = area.x + cx * chunkSize, z0 = area.y + cz * chunkSize;
                    float w = Mathf.Min(chunkSize, area.xMax - x0), d = Mathf.Min(chunkSize, area.yMax - z0);
                    MeshKit k = new MeshKit();
                    int count = Mathf.RoundToInt(w * d * density);
                    for (int i = 0; i < count; i++)
                    {
                        float x = x0 + (float)rng.NextDouble() * w, z = z0 + (float)rng.NextDouble() * d;
                        float keep = Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(area.y, area.yMax, z));
                        if (rng.NextDouble() > keep || Blocked(x, z)) continue;
                        float s = 0.75f + (float)rng.NextDouble() * 0.6f;
                        k.Append(tufts[rng.Next(tufts.Length)], new Vector3(x, 0f, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(s, s * (0.8f + (float)rng.NextDouble() * 0.5f), s));
                    }
                    int fl = Mathf.RoundToInt(w * d * flowerDensity);
                    for (int i = 0; i < fl; i++)
                    {
                        float x = x0 + (float)rng.NextDouble() * w, z = z0 + (float)rng.NextDouble() * d;
                        if (Blocked(x, z)) continue;
                        float s = 0.8f + (float)rng.NextDouble() * 0.5f;
                        k.Append(flowers[rng.Next(flowers.Length)], new Vector3(x, 0f, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s);
                    }
                    if (k.Count == 0) continue;
                    Mesh m = k.ToMesh("RS_GrassChunk_" + cx + "_" + cz);
                    m.hideFlags = HideFlags.HideAndDontSave;
                    meshes.Add(m);
                    GameObject go = new GameObject("GrassChunk");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(transform, false);
                    go.layer = gameObject.layer;
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    MeshRenderer r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = true;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    chunks.Add(go);
                }
            }
        }

        /// <summary>Small daisy: stem, five petals and a yellow centre (vertex coloured, sways in the wind).</summary>
        static MeshKit Flower(Color petal)
        {
            MeshKit k = new MeshKit();
            float h = 0.28f;
            k.Append(Shapes.Tube(new List<Vector3> { Vector3.zero, new Vector3(0.01f, h * 0.5f, 0f), new Vector3(0f, h, 0.01f) }, 0.008f, 0.006f, 4, new Color(0.3f, 0.6f, 0.2f), false));
            MeshKit p = Shapes.Ellipsoid(new Vector3(0.028f, 0.008f, 0.045f), 6, 4, petal);
            for (int i = 0; i < 5; i++)
            {
                Quaternion q = Quaternion.Euler(-12f, i * 72f, 0f);
                k.Append(p, new Vector3(0f, h, 0f) + q * new Vector3(0f, 0f, 0.04f), q, Vector3.one);
            }
            k.Append(Shapes.Ellipsoid(new Vector3(0.022f, 0.014f, 0.022f), 8, 5, new Color(1f, 0.78f, 0.15f)), new Vector3(0f, h + 0.008f, 0f), Quaternion.identity, Vector3.one);
            k.WindByHeight(0f, h);
            return k;
        }
    }
}
