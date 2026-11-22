using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    /// <summary>Low-level helpers used by the farm builders (work in edit mode and play mode).</summary>
    public static class FarmBuild
    {
        static readonly Dictionary<string, Mesh> boxCache = new Dictionary<string, Mesh>();

        public static void ClearCache() { boxCache.Clear(); }

        public static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material m, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, Vector3 euler, Vector3 scale, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows;
            return go;
        }

        /// <summary>Box mesh whose UVs are in metres * uvScale, so textures keep a real-world size.</summary>
        public static Mesh BoxMesh(Vector3 size, float uvScale)
        {
            string key = size.x.ToString("0.###") + "_" + size.y.ToString("0.###") + "_" + size.z.ToString("0.###") + "_" + uvScale;
            Mesh m;
            if (boxCache.TryGetValue(key, out m) && m != null) return m;
            MeshBuilder b = new MeshBuilder();
            FarmMeshes.Box(b, Vector3.zero, size, Quaternion.identity, uvScale);
            m = b.ToMesh("Farm_Box_" + key);
            boxCache[key] = m;
            return m;
        }

        /// <summary>Textured box with real-world UVs and (optionally) a BoxCollider + bullet surface type.</summary>
        public static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, Material m, float uvScale = 1f, bool collider = true, SurfaceKind surface = SurfaceKind.Wood)
        {
            GameObject go = MeshObj(parent, name, BoxMesh(size, uvScale), m, pos, euler, Vector3.one);
            if (collider)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.size = size;
                go.AddComponent<FarmSurface>().kind = surface;
            }
            return go;
        }

        public static void Surface(GameObject go, SurfaceKind kind)
        {
            FarmSurface s = go.GetComponent<FarmSurface>();
            if (s == null) s = go.AddComponent<FarmSurface>();
            s.kind = kind;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++) SetLayer(go.transform.GetChild(i).gameObject, layer);
        }

        public static void Shadows(GameObject go, ShadowCastingMode mode, bool receive = true)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) { rs[i].shadowCastingMode = mode; rs[i].receiveShadows = receive; }
        }

        public static Transform Node(Transform parent, string name, Vector3 pos, Vector3 euler = default(Vector3))
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            return go.transform;
        }

        /// <summary>Cylinder primitive lying along local Z (Unity's cylinder is along Y, height 2).</summary>
        public static GameObject RodZ(Transform parent, string name, Vector3 pos, float radius, float length, Material m)
        {
            return Part(parent, PrimitiveType.Cylinder, name, pos, new Vector3(90f, 0f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f), m);
        }
    }
}
