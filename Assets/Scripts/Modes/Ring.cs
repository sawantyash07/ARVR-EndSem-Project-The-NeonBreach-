using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// A floating horizontal neon ring for the parachute free-fall: fall through the hole for bonus points.
    /// Passing is detected geometrically (the player's path segment crosses the ring plane inside the radius), so it
    /// works at 60+ m/s without colliders. The torus is a procedural mesh shared by all rings.
    /// </summary>
    public class Ring : MonoBehaviour
    {
        public enum RingState { Waiting, Next, Passed, Missed }

        public float radius = 12f;
        public int index;
        public RingState State { get; private set; }

        Renderer rend;
        float anim;

        static Mesh torus;

        public static Ring Create(Transform parent, Vector3 pos, float radius, int index)
        {
            GameObject go = new GameObject("Ring_" + index);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * radius;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Torus();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Ring r = go.AddComponent<Ring>();
            r.radius = radius;
            r.index = index;
            r.rend = mr;
            r.SetState(RingState.Waiting);
            return r;
        }

        public void SetState(RingState s)
        {
            State = s;
            string m = s == RingState.Next ? "NeonYellow" : (s == RingState.Passed ? "NeonGreen" : (s == RingState.Missed ? "NeonRed" : "NeonCyan"));
            if (rend != null) rend.sharedMaterial = Mats.Get(m);
            anim = 0f;
        }

        /// <summary>Did the segment a -> b fall through the ring?</summary>
        public bool Crossed(Vector3 a, Vector3 b)
        {
            float y = transform.position.y;
            if (!(a.y >= y && b.y < y)) return false;
            float t = (a.y - y) / Mathf.Max(0.0001f, a.y - b.y);
            Vector3 p = Vector3.Lerp(a, b, t);
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude <= radius * 0.95f;
        }

        void Update()
        {
            anim += Time.deltaTime;
            float s = radius;
            if (State == RingState.Next) s *= 1f + 0.06f * Mathf.Sin(Time.time * 6f);
            else if (State == RingState.Passed) s *= 1f + Mathf.Clamp01(anim * 2f) * 0.6f;   // expands as a reward
            else if (State == RingState.Missed) s *= Mathf.Max(0.001f, 1f - anim * 0.5f);
            transform.localScale = Vector3.one * s;
            if (State == RingState.Passed && anim > 0.6f) gameObject.SetActive(false);
            transform.Rotate(0f, 20f * Time.deltaTime, 0f, Space.World);
        }

        /// <summary>Unit torus (major radius 1, minor radius 0.06) in the XZ plane.</summary>
        static Mesh Torus()
        {
            if (torus != null) return torus;
            const int seg = 48, side = 10;
            const float minor = 0.06f;
            Vector3[] v = new Vector3[(seg + 1) * (side + 1)];
            Vector3[] n = new Vector3[v.Length];
            Vector2[] uv = new Vector2[v.Length];
            int k = 0;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= side; j++)
                {
                    float b = j / (float)side * Mathf.PI * 2f;
                    Vector3 nn = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[k] = c + nn * minor;
                    n[k] = nn;
                    uv[k] = new Vector2(i / (float)seg, j / (float)side);
                    k++;
                }
            }
            int[] tri = new int[seg * side * 6];
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + 1, b0 = a0 + side + 1, b1 = b0 + 1;
                    tri[t++] = a0; tri[t++] = a1; tri[t++] = b0;
                    tri[t++] = a1; tri[t++] = b1; tri[t++] = b0;
                }
            }
            torus = new Mesh();
            torus.name = "NB_Torus";
            torus.vertices = v;
            torus.normals = n;
            torus.uv = uv;
            torus.triangles = tri;
            torus.RecalculateBounds();
            return torus;
        }
    }
}
