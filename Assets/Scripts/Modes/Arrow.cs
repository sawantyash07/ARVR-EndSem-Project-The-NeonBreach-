using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// An arrow with custom ballistics (no Rigidbody): gravity, light air drag and wind drift, integrated per frame with
    /// a swept raycast along the travelled segment, so even an 80 m/s arrow cannot tunnel through a target.
    /// It sticks into whatever it hits (riding along with moving targets), quivers, and fades out after a while.
    /// The model's pivot is the arrow tip; the shaft extends backwards along -Z.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        public static readonly List<Arrow> All = new List<Arrow>();
        public static int InFlight
        {
            get
            {
                int n = 0;
                for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].flying) n++;
                return n;
            }
        }

        public const float Length = 0.72f;
        const float Drag = 0.0006f;         // 1/m: quadratic drag, ~3.6 m/s^2 at 78 m/s - arrows keep most of their speed over 40 m
        const float WindCoupling = 0.45f;   // how strongly the arrow is pushed towards the wind velocity (1/s)
        const float MaxLife = 6f;
        const float StuckLife = 7f;
        const float FadeTime = 1.2f;

        Vector3 velocity;
        bool flying;
        float life;
        float stuckTime;
        float quiver;
        Quaternion stuckRot;
        ArcheryMode owner;
        Material[] mats;
        Color[] baseColors;
        TrailRenderer trail;

        public bool Flying { get { return flying; } }
        /// <summary>Round and shot number this arrow belongs to (filled in by ArcheryMode).</summary>
        public int Round, Index;

        public static Arrow Spawn(Vector3 position, Vector3 velocity, ArcheryMode owner, Color fletch)
        {
            GameObject go = new GameObject("Arrow");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(velocity.sqrMagnitude > 0.01f ? velocity : Vector3.forward);
            Arrow a = go.AddComponent<Arrow>();
            a.velocity = velocity;
            a.owner = owner;
            a.flying = true;
            a.BuildModel(fletch);
            All.Add(a);
            return a;
        }

        /// <summary>Builds the arrow from primitives under parent (also used for the nocked arrow on the bow).</summary>
        public static void BuildMesh(Transform parent, Material shaftMat, Material headMat, Material fletchMat)
        {
            GameObject shaft = Util.Part(parent, PrimitiveType.Cylinder, "Shaft", new Vector3(0f, 0f, -Length * 0.5f), new Vector3(90f, 0f, 0f),
                                         new Vector3(0.009f, Length * 0.5f, 0.009f), "GunMetal");
            GameObject head = Util.Part(parent, PrimitiveType.Cube, "Head", new Vector3(0f, 0f, -0.025f), new Vector3(0f, 0f, 45f),
                                        new Vector3(0.016f, 0.016f, 0.05f), "GunMetal");
            GameObject f1 = Util.Part(parent, PrimitiveType.Cube, "Fletch", new Vector3(0f, 0f, -Length + 0.06f), Vector3.zero,
                                      new Vector3(0.002f, 0.03f, 0.09f), "NeonCyan");
            GameObject f2 = Util.Part(parent, PrimitiveType.Cube, "Fletch", new Vector3(0f, 0f, -Length + 0.06f), Vector3.zero,
                                      new Vector3(0.03f, 0.002f, 0.09f), "NeonCyan");
            GameObject nock = Util.Part(parent, PrimitiveType.Cube, "Nock", new Vector3(0f, 0f, -Length + 0.005f), Vector3.zero,
                                        new Vector3(0.014f, 0.014f, 0.012f), "NeonWhite");
            if (shaftMat != null) shaft.GetComponent<Renderer>().sharedMaterial = shaftMat;
            if (headMat != null) head.GetComponent<Renderer>().sharedMaterial = headMat;
            if (fletchMat != null)
            {
                f1.GetComponent<Renderer>().sharedMaterial = fletchMat;
                f2.GetComponent<Renderer>().sharedMaterial = fletchMat;
                nock.GetComponent<Renderer>().sharedMaterial = fletchMat;
            }
            Util.NoShadows(parent.gameObject);
        }

        void BuildModel(Color fletch)
        {
            // own unlit materials so the arrow can fade out with alpha
            mats = new Material[3];
            baseColors = new Color[3];
            baseColors[0] = new Color(0.16f, 0.17f, 0.2f, 1f);
            baseColors[1] = new Color(0.75f, 0.78f, 0.85f, 1f);
            baseColors[2] = fletch;
            for (int i = 0; i < 3; i++) mats[i] = Mats.FxColor(baseColors[i]);
            BuildMesh(transform, mats[0], mats[1], mats[2]);

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = 0.025f;
            trail.endWidth = 0f;
            trail.sharedMaterial = Mats.FxTracer;
            trail.startColor = new Color(fletch.r, fletch.g, fletch.b, 0.55f);
            trail.endColor = new Color(fletch.r, fletch.g, fletch.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.minVertexDistance = 0.1f;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            float dt = Time.deltaTime;
            if (gm != null && gm.State == GameState.Paused) return;

            if (flying) Fly(dt);
            else Stuck(dt);
        }

        static int syncedFrame = -1;

        void Fly(float dt)
        {
            life += dt;
            // targets move their colliders through the transform: sync once per frame so hits match what you see
            if (syncedFrame != Time.frameCount) { syncedFrame = Time.frameCount; Physics.SyncTransforms(); }
            Vector3 wind = WindSystem.Instance != null ? WindSystem.Instance.Current : Vector3.zero;
            Vector3 accel = Physics.gravity - velocity * (velocity.magnitude * Drag);
            // the wind pushes sideways on the arrow (relative air velocity across the shaft)
            Vector3 rel = wind - velocity;
            Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
            accel += Vector3.ProjectOnPlane(rel, dir) * WindCoupling;

            // swept collision along this frame's path (sub-stepped so the curve is followed closely)
            int steps = Mathf.Clamp(Mathf.CeilToInt(velocity.magnitude * dt / 0.75f), 1, 8);
            float h = dt / steps;
            Vector3 p = transform.position;
            for (int s = 0; s < steps; s++)
            {
                Vector3 next = p + velocity * h + 0.5f * accel * h * h;
                Vector3 seg = next - p;
                float len = seg.magnitude;
                RaycastHit hit;
                if (len > 0.0001f && Physics.Raycast(p, seg / len, out hit, len, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                {
                    Stick(hit, seg / len);
                    return;
                }
                velocity += accel * h;
                p = next;
            }
            transform.position = p;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);

            if (life > MaxLife || p.y < -5f)
            {
                flying = false;
                if (owner != null) owner.OnArrowLanded(this, null, p, 0, 0f, false);
                Destroy(gameObject);
            }
        }

        void Stick(RaycastHit hit, Vector3 dir)
        {
            flying = false;
            float speed = velocity.magnitude;
            float sink = Mathf.Clamp(speed * 0.0025f, 0.04f, 0.16f);
            transform.position = hit.point + dir * sink;
            transform.rotation = Quaternion.LookRotation(dir);
            stuckRot = transform.localRotation;
            quiver = Mathf.Clamp01(speed / 60f);
            if (trail != null) trail.emitting = false;

            ArcheryTarget target = hit.collider.GetComponentInParent<ArcheryTarget>();
            int points = 0;
            float rn = 99f;
            if (target != null)
            {
                transform.SetParent(target.transform, true);   // the target root is never scaled
                stuckRot = transform.localRotation;
                points = target.ScoreAt(hit.point, out rn);
            }
            if (owner != null) owner.OnArrowLanded(this, target, hit.point, points, rn, true);
            FX.Sparks(hit.point, hit.normal, points > 0 ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 0.6f, 0.4f), points > 0 ? 8 : 4);
        }

        void Stuck(float dt)
        {
            stuckTime += dt;
            // short damped quiver after the impact
            if (quiver > 0.001f)
            {
                quiver = Mathf.MoveTowards(quiver, 0f, dt * 1.4f);
                float a = Mathf.Sin(stuckTime * 55f) * quiver * 2.5f;
                transform.localRotation = stuckRot * Quaternion.Euler(a, a * 0.6f, 0f);
            }
            if (stuckTime > StuckLife)
            {
                float k = 1f - Mathf.Clamp01((stuckTime - StuckLife) / FadeTime);
                for (int i = 0; i < mats.Length; i++)
                {
                    Color c = baseColors[i];
                    c.a *= k;
                    mats[i].color = c;
                }
                if (k <= 0f) Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (mats != null) for (int i = 0; i < mats.Length; i++) if (mats[i] != null) Destroy(mats[i]);
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject);
            All.Clear();
        }
    }
}
