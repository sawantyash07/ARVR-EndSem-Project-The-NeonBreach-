using UnityEngine;

namespace NeonBreach
{
    /// <summary>Small helpers shared by the procedural builders (work in edit mode and play mode).</summary>
    public static class Util
    {
        /// <summary>
        /// Set a camera's field of view, except while an XR headset renders it in stereo: the lens fixes the FOV there
        /// and Unity warns "Cannot set field of view on camera ... while VR is enabled".
        /// </summary>
        public static void SetFov(Camera cam, float fov)
        {
            if (cam != null && !cam.stereoEnabled) cam.fieldOfView = fov;
        }

        /// <summary>Create a primitive as a child of parent. Colliders are stripped unless keepCollider is true.</summary>
        public static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler,
                                      Vector3 scale, string material, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            t.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = Mats.Get(material);
                // glowing strips are light sources visually: a shadow from them looks wrong and costs a shadow-map draw each
                if (Perf.NeonCastNoShadows && material.StartsWith("Neon")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        public static void NoShadows(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rs[i].receiveShadows = false;
            }
        }

        public static Light AddLight(Transform parent, LightType type, Color color, float intensity, float range, Vector3 localPos)
        {
            GameObject go = new GameObject(type + " Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        public static void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
