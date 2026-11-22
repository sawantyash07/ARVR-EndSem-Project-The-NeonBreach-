using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Pooled cartoon effects: puff clouds and stars when a mouse is hit, dirt / grass / splinter / straw / leaf /
    /// spark impacts, muzzle smoke, crumbs while eating, bullet tracers and ejected shell casings. Particle systems are
    /// created at startup from the materials assigned by the scene builder.
    /// </summary>
    public class RatFX : MonoBehaviour
    {
        public static RatFX Instance { get; private set; }

        [Header("Materials (set by the scene builder)")]
        public Material puffMaterial;
        public Material bitsMaterial;
        public Material starMaterial;
        public Material sparkMaterial;
        public Material tracerMaterial;

        [Header("Shell casings")]
        public Mesh casingMesh;
        public Material casingMaterial;
        public int casingPool = 10;

        [Header("Tracers")]
        public float tracerLife = 0.07f;
        public float tracerWidth = 0.025f;

        ParticleSystem puffs, bits, stars, sparks;
        readonly List<LineRenderer> tracers = new List<LineRenderer>();
        readonly List<float> tracerAge = new List<float>();
        readonly List<ShellCasing> casings = new List<ShellCasing>();
        int nextCasing, nextTracer;

        void Awake()
        {
            Instance = this;
            puffs = MakeSystem("Puffs", puffMaterial, -0.06f, 2.2f, false, true);
            bits = MakeSystem("Bits", bitsMaterial, 1.6f, 0.6f, false, false);
            stars = MakeSystem("Stars", starMaterial, 0.5f, 1.5f, true, true);
            sparks = MakeSystem("Sparks", sparkMaterial, 1.2f, 0.5f, false, false);
            ParticleSystemRenderer sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.04f;
            sr.lengthScale = 1.2f;

            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject("Tracer" + i);
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMaterial;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, tracerWidth * 0.4f), new Keyframe(1f, tracerWidth));
                lr.enabled = false;
                tracers.Add(lr);
                tracerAge.Add(10f);
            }

            if (casingMesh != null && casingMaterial != null)
            {
                for (int i = 0; i < casingPool; i++)
                {
                    GameObject go = new GameObject("Casing" + i);
                    go.transform.SetParent(transform, false);
                    go.layer = Layers.FX;
                    go.AddComponent<MeshFilter>().sharedMesh = casingMesh;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = casingMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                    cc.direction = 2;
                    cc.radius = 0.008f;
                    cc.height = 0.045f;
                    Rigidbody rb = go.AddComponent<Rigidbody>();
                    rb.mass = 0.01f;
                    rb.linearDamping = 0.2f;
                    rb.angularDamping = 0.5f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    casings.Add(go.AddComponent<ShellCasing>());
                    go.SetActive(false);
                }
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        ParticleSystem MakeSystem(string n, Material mat, float gravity, float drag, bool spin, bool grow)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.layer = Layers.FX;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve curve = grow
                ? new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.18f, 1f), new Keyframe(1f, 1.15f))
                : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            if (drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
                lv.enabled = true;
                lv.limit = 100f;
                lv.drag = drag;
                lv.multiplyDragByParticleSize = false;
                lv.multiplyDragByParticleVelocity = false;
            }
            if (spin)
            {
                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            }

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 1f;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        void Update()
        {
            for (int i = 0; i < tracers.Count; i++)
            {
                if (!tracers[i].enabled) continue;
                tracerAge[i] += Time.deltaTime;
                float a = 1f - tracerAge[i] / tracerLife;
                if (a <= 0f) { tracers[i].enabled = false; continue; }
                Color c = new Color(1f, 0.93f, 0.6f, a);
                tracers[i].startColor = new Color(c.r, c.g, c.b, a * 0.3f);
                tracers[i].endColor = c;
            }
        }

        // ------------------------------------------------------------------ emit helpers

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color col, float life, float rot = 0f)
        {
            if (ps == null) return;
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = pos;
            p.velocity = vel;
            p.startSize = size;
            p.startColor = col;
            p.startLifetime = life;
            p.rotation = rot;
            ps.Emit(p, 1);
        }

        static Vector3 Spray(Vector3 normal, float cone)
        {
            Vector3 r = Random.onUnitSphere;
            if (Vector3.Dot(r, normal) < 0f) r = -r;
            return Vector3.Slerp(normal, r, cone).normalized;
        }

        // ------------------------------------------------------------------ public effects

        /// <summary>The big cartoon "poof" when a mouse is defeated: white cloud, stars and sparkles.</summary>
        public static void Poof(Vector3 pos, float scale = 1f)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            for (int i = 0; i < 14; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + 0.1f;
                Emit(fx.puffs, pos + d * 0.08f * scale, d * Random.Range(1.2f, 2.6f) * scale, Random.Range(0.28f, 0.5f) * scale,
                     Color.Lerp(Color.white, new Color(0.95f, 0.93f, 0.88f), Random.value), Random.Range(0.45f, 0.8f));
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) + 0.4f;
                Emit(fx.stars, pos, d.normalized * Random.Range(2f, 3.4f), Random.Range(0.16f, 0.26f), Color.white, Random.Range(0.5f, 0.8f), Random.Range(0f, 360f));
            }
            for (int i = 0; i < 10; i++)
                Emit(fx.sparks, pos, Random.onUnitSphere * Random.Range(3f, 5f), Random.Range(0.03f, 0.05f), new Color(1f, 0.9f, 0.5f), Random.Range(0.2f, 0.35f));
        }

        /// <summary>Instant hit flash: a burst of stars where the bullet struck the mouse.</summary>
        public static void HitStars(Vector3 pos, Vector3 normal)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            for (int i = 0; i < 6; i++)
                Emit(fx.stars, pos, Spray(normal, 0.8f) * Random.Range(1.5f, 3f) + Vector3.up, Random.Range(0.12f, 0.2f), Color.white, Random.Range(0.35f, 0.55f), Random.Range(0f, 360f));
            Emit(fx.puffs, pos, Vector3.zero, 0.35f, new Color(1f, 1f, 1f, 0.9f), 0.18f);
        }

        public static void DirtPuff(Vector3 pos, float amount)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            int n = Mathf.RoundToInt(6 * amount) + 1;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 0.8f), Random.Range(-1f, 1f));
                Emit(fx.puffs, pos + Vector3.up * 0.05f, d * Random.Range(0.4f, 1.1f), Random.Range(0.14f, 0.26f) * (0.6f + amount * 0.5f),
                     new Color(0.78f, 0.64f, 0.45f, 0.9f), Random.Range(0.4f, 0.7f));
            }
        }

        public static void Crumbs(Vector3 pos)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            for (int i = 0; i < 3; i++)
                Emit(fx.bits, pos + Random.insideUnitSphere * 0.04f, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.2f), Random.Range(-0.6f, 0.6f)),
                     Random.Range(0.02f, 0.035f), new Color(1f, 0.85f, 0.35f), Random.Range(0.4f, 0.7f));
        }

        public static void MuzzleSmoke(Vector3 pos, Vector3 dir)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            for (int i = 0; i < 4; i++)
                Emit(fx.puffs, pos + dir * (0.05f * i), dir * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.3f + Vector3.up * 0.2f,
                     Random.Range(0.06f, 0.12f), new Color(0.92f, 0.92f, 0.92f, 0.55f), Random.Range(0.35f, 0.6f));
        }

        /// <summary>Bullet impact on scenery, by surface type.</summary>
        public static void Impact(Vector3 point, Vector3 normal, SurfaceKind kind)
        {
            RatFX fx = Instance;
            if (fx == null) return;
            Color dust, bit;
            int nBits = 8;
            switch (kind)
            {
                case SurfaceKind.Wood: dust = new Color(0.85f, 0.75f, 0.6f, 0.8f); bit = new Color(0.72f, 0.5f, 0.3f); break;
                case SurfaceKind.Metal: dust = new Color(0.8f, 0.8f, 0.82f, 0.6f); bit = new Color(0.6f, 0.6f, 0.62f); nBits = 3; break;
                case SurfaceKind.Hay: dust = new Color(0.95f, 0.88f, 0.6f, 0.8f); bit = new Color(1f, 0.85f, 0.35f); nBits = 12; break;
                case SurfaceKind.Leaves: dust = new Color(0.6f, 0.8f, 0.45f, 0.6f); bit = new Color(0.35f, 0.7f, 0.25f); nBits = 10; break;
                case SurfaceKind.Stone: dust = new Color(0.82f, 0.82f, 0.8f, 0.8f); bit = new Color(0.55f, 0.55f, 0.55f); break;
                case SurfaceKind.Grass: dust = new Color(0.8f, 0.7f, 0.5f, 0.85f); bit = new Color(0.4f, 0.72f, 0.28f); nBits = 10; break;
                case SurfaceKind.Water: dust = new Color(0.85f, 0.95f, 1f, 0.8f); bit = new Color(0.7f, 0.88f, 1f); nBits = 12; break;
                default: dust = new Color(0.78f, 0.64f, 0.45f, 0.9f); bit = new Color(0.5f, 0.36f, 0.22f); break;
            }
            for (int i = 0; i < 5; i++)
                Emit(fx.puffs, point + normal * 0.05f, Spray(normal, 0.6f) * Random.Range(0.5f, 1.6f), Random.Range(0.12f, 0.24f), dust, Random.Range(0.4f, 0.75f));
            for (int i = 0; i < nBits; i++)
                Emit(fx.bits, point + normal * 0.03f, Spray(normal, 0.55f) * Random.Range(1.5f, 3.5f), Random.Range(0.025f, 0.05f), bit, Random.Range(0.5f, 0.9f));
            if (kind == SurfaceKind.Metal || kind == SurfaceKind.Stone)
                for (int i = 0; i < 8; i++)
                    Emit(fx.sparks, point + normal * 0.02f, Spray(normal, 0.7f) * Random.Range(3f, 6f), Random.Range(0.02f, 0.035f), new Color(1f, 0.85f, 0.4f), Random.Range(0.15f, 0.3f));
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            RatFX fx = Instance;
            if (fx == null || fx.tracers.Count == 0) return;
            int i = fx.nextTracer;
            fx.nextTracer = (fx.nextTracer + 1) % fx.tracers.Count;
            LineRenderer lr = fx.tracers[i];
            // start the streak a little ahead of the muzzle so it reads as a fast bullet, not a laser
            Vector3 dir = (to - from);
            float len = dir.magnitude;
            Vector3 start = from + dir.normalized * Mathf.Min(0.6f, len * 0.2f);
            lr.SetPosition(0, start);
            lr.SetPosition(1, to);
            lr.enabled = true;
            fx.tracerAge[i] = 0f;
        }

        public static void EjectCasing(Vector3 pos, Quaternion rot, Vector3 velocity)
        {
            RatFX fx = Instance;
            if (fx == null || fx.casings.Count == 0) return;
            ShellCasing c = fx.casings[fx.nextCasing];
            fx.nextCasing = (fx.nextCasing + 1) % fx.casings.Count;
            c.Launch(pos, rot, velocity);
        }
    }
}
