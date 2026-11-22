using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    public enum VegetationKind { Corn, Wheat, Cabbage, Pumpkin, PumpkinVines, Lettuce, Grass }

    /// <summary>
    /// Grows a field of crops (or the meadow grass) from a seed when the scene loads, in the editor too.
    /// Thousands of plants are combined into chunk meshes for fast drawing. The meshes are rebuilt on load instead of
    /// being saved, which keeps the project small (they would be ~80 MB of text-serialized mesh data) - the result
    /// is identical every time because the seed is fixed. Change density, spacing, seed or material in the
    /// Inspector, then toggle the component (or press Regenerate in its context menu).
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    public class VegetationPlanter : MonoBehaviour
    {
        public VegetationKind kind = VegetationKind.Corn;
        [Tooltip("World XZ rectangle to plant (x, z, width, depth). Grass ignores this and covers the farm.")]
        public Rect area = new Rect(0f, 0f, 10f, 10f);
        public int seed = 1;
        [Range(0.2f, 2f)] public float density = 1f;
        public float rowSpacing = 1f;
        public float plantSpacing = 0.9f;
        public float jitter = 0.1f;
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
        [Range(0f, 0.9f)] public float skipChance = 0.03f;
        public float chunkSize = 12f;
        public bool castShadows = true;
        public Material material;
        [Tooltip("Grass only: second material for dry patches.")]
        public Material dryMaterial;
        [Tooltip("Grass only: number of tufts.")]
        public int grassCount = 3200;
        [Tooltip("Crop health is shown on these renderers (tint as the field is eaten).")]
        public CropField field;

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Renderer> myRenderers = new List<Renderer>();
        System.Random rng;

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        void OnEnable() { Regenerate(); }
        void OnDisable() { Clear(); }

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            Clear();
            if (material == null) return;
            rng = new System.Random(seed);
            List<Renderer> made = new List<Renderer>();
            if (kind == VegetationKind.Grass) PlantGrass(made);
            else PlantRows(made);
            myRenderers.AddRange(made);
            if (field != null) field.RegisterRenderers(made);
        }

        void Clear()
        {
            if (field != null) field.UnregisterRenderers(myRenderers);
            myRenderers.Clear();
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Kill(spawned[i]);
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Kill(meshes[i]);
            spawned.Clear();
            meshes.Clear();
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void Emit(MeshBuilder b, string name, Material mat, List<Renderer> outR)
        {
            if (b == null || b.Count == 0) return;
            Mesh m = b.ToMesh("Farm_" + name);
            m.hideFlags = HideFlags.HideAndDontSave;
            meshes.Add(m);
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.layer = gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            spawned.Add(go);
            outR.Add(r);
        }

        MeshBuilder[] Variants(int n, System.Func<System.Random, MeshBuilder> make)
        {
            MeshBuilder[] v = new MeshBuilder[n];
            for (int i = 0; i < n; i++) v[i] = make(rng);
            return v;
        }

        // ------------------------------------------------------------------ crops in rows

        void PlantRows(List<Renderer> outR)
        {
            MeshBuilder[] variants;
            switch (kind)
            {
                case VegetationKind.Corn: variants = Variants(6, r => FarmMeshes.CornPlant(r)); break;
                case VegetationKind.Wheat: variants = Variants(8, r => FarmMeshes.WheatTuft(r)); break;
                case VegetationKind.Cabbage: variants = Variants(5, Cabbage); break;
                case VegetationKind.Pumpkin: variants = Variants(4, PumpkinPlant); break;
                case VegetationKind.PumpkinVines: variants = Variants(4, VineLeaves); break;
                default: variants = Variants(4, Lettuce); break;
            }
            Rect r = area;
            const float margin = 0.7f;
            float spacing = plantSpacing / Mathf.Max(0.1f, density);
            int cx = Mathf.Max(1, Mathf.CeilToInt(r.width / chunkSize)), cz = Mathf.Max(1, Mathf.CeilToInt(r.height / chunkSize));
            MeshBuilder[,] chunks = new MeshBuilder[cx, cz];
            float x0 = Mathf.Ceil((r.xMin + margin) / rowSpacing) * rowSpacing;
            for (float x = x0; x < r.xMax - margin; x += rowSpacing)
                for (float z = r.yMin + margin + R(0f, spacing); z < r.yMax - margin; z += spacing)
                {
                    if (rng.NextDouble() < skipChance) continue;
                    float px = x + R(-jitter, jitter), pz = z + R(-jitter, jitter);
                    Vector3 p = new Vector3(px, FarmLayout.Height(px, pz) - 0.02f, pz);
                    int ix = Mathf.Clamp((int)((px - r.xMin) / chunkSize), 0, cx - 1), iz = Mathf.Clamp((int)((pz - r.yMin) / chunkSize), 0, cz - 1);
                    if (chunks[ix, iz] == null) chunks[ix, iz] = new MeshBuilder();
                    float sc = R(scaleRange.x, scaleRange.y);
                    chunks[ix, iz].Append(variants[rng.Next(variants.Length)], Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * sc));
                }
            for (int i = 0; i < cx; i++)
                for (int j = 0; j < cz; j++)
                    Emit(chunks[i, j], kind + " " + i + "-" + j, material, outR);
        }

        static MeshBuilder Cabbage(System.Random r)
        {
            MeshBuilder b = new MeshBuilder();
            b.Append(FarmMeshes.BlobBuilder(r.Next(1000), 1, 0.35f, new Vector3(0.2f, 0.17f, 0.2f)), Matrix4x4.Translate(new Vector3(0f, 0.14f, 0f)));
            b.Append(FarmMeshes.BlobBuilder(r.Next(1000), 1, 0.6f, new Vector3(0.34f, 0.09f, 0.34f)), Matrix4x4.Translate(new Vector3(0f, 0.07f, 0f)));
            return b;
        }

        static MeshBuilder Lettuce(System.Random r)
        {
            return FarmMeshes.BlobBuilder(r.Next(1000), 1, 0.7f, new Vector3(0.2f, 0.14f, 0.2f));
        }

        static MeshBuilder PumpkinPlant(System.Random r)
        {
            MeshBuilder b = new MeshBuilder();
            float s = 0.28f + (float)r.NextDouble() * 0.18f;
            b.Append(FarmMeshes.PumpkinBuilder(), Matrix4x4.TRS(new Vector3(0f, s * 0.68f, 0f), Quaternion.Euler(0f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 10f), Vector3.one * s));
            return b;
        }

        static MeshBuilder VineLeaves(System.Random r)
        {
            MeshBuilder b = new MeshBuilder();
            for (int i = 0; i < 3; i++)
                b.Append(FarmMeshes.BlobBuilder(r.Next(1000), 1, 0.6f, new Vector3(0.28f, 0.07f, 0.28f)),
                         Matrix4x4.Translate(new Vector3((float)r.NextDouble() * 0.6f - 0.3f, 0.06f, (float)r.NextDouble() * 0.6f - 0.3f)));
            return b;
        }

        // ------------------------------------------------------------------ meadow grass + pond reeds

        void PlantGrass(List<Renderer> outR)
        {
            MeshBuilder[] tufts = Variants(8, r => FarmMeshes.GrassTuft(r, 0.42f));
            MeshBuilder[] tall = Variants(4, r => FarmMeshes.GrassTuft(r, 0.8f));
            const float extent = 135f;
            float chunk = Mathf.Max(10f, chunkSize);
            int n = Mathf.CeilToInt(extent * 2f / chunk);
            MeshBuilder[,] green = new MeshBuilder[n, n], dry = new MeshBuilder[n, n];
            int count = Mathf.RoundToInt(grassCount * density);
            int placed = 0, guard = 0;
            while (placed < count && guard++ < count * 6)
            {
                float x = R(-extent, extent), z = R(-extent, extent);
                if (FarmLayout.FieldAt(x, z, 0.4f) >= 0) continue;
                if (FarmLayout.RoadDistance(x, z) < FarmLayout.RoadWidth * 0.5f + 0.4f) continue;
                if (FarmLayout.InYard(x, z) && rng.NextDouble() < 0.85) continue;
                if (Vector2.Distance(new Vector2(x, z), FarmLayout.PondCenter) < FarmLayout.PondRadius) continue;
                if (FarmLayout.NearBuilding(x, z)) continue;
                bool nearFence = Mathf.Abs(Mathf.Abs(x) - FarmLayout.FenceHalf) < 1.5f || Mathf.Abs(Mathf.Abs(z) - FarmLayout.FenceHalf) < 1.5f;
                int ix = Mathf.Clamp((int)((x + extent) / chunk), 0, n - 1), iz = Mathf.Clamp((int)((z + extent) / chunk), 0, n - 1);
                bool isDry = dryMaterial != null && Mathf.PerlinNoise(x * 0.02f + 3f, z * 0.02f) > 0.58f;
                MeshBuilder[,] set = isDry ? dry : green;
                if (set[ix, iz] == null) set[ix, iz] = new MeshBuilder();
                MeshBuilder v = nearFence || rng.NextDouble() < 0.08 ? tall[rng.Next(tall.Length)] : tufts[rng.Next(tufts.Length)];
                set[ix, iz].Append(v, Matrix4x4.TRS(new Vector3(x, FarmLayout.Height(x, z) - 0.03f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.7f, 1.3f)));
                placed++;
            }
            for (int i = 0; i < 70; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = FarmLayout.PondRadius + R(-0.8f, 1.6f);
                float x = FarmLayout.PondCenter.x + Mathf.Cos(a) * d, z = FarmLayout.PondCenter.y + Mathf.Sin(a) * d;
                int ix = Mathf.Clamp((int)((x + extent) / chunk), 0, n - 1), iz = Mathf.Clamp((int)((z + extent) / chunk), 0, n - 1);
                if (green[ix, iz] == null) green[ix, iz] = new MeshBuilder();
                green[ix, iz].Append(tall[rng.Next(tall.Length)], Matrix4x4.TRS(new Vector3(x, FarmLayout.Height(x, z) - 0.05f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(1f, R(1.4f, 2f), 1f)));
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    Emit(green[i, j], "Grass " + i + "-" + j, material, outR);
                    if (dryMaterial != null) Emit(dry[i, j], "DryGrass " + i + "-" + j, dryMaterial, outR);
                }
        }
    }
}
