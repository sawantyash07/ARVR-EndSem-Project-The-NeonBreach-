using System.Collections;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Static facade for all visual effects: sparks/bursts (one shared particle system), tracers, decals,
    /// explosions, shockwave rings and debris. Everything is built from code.
    /// </summary>
    public class FX : MonoBehaviour
    {
        static FX inst;
        ParticleSystem ps;
        GameObject[] decals;
        int decalIndex;

        static FX I
        {
            get
            {
                if (inst == null)
                {
                    GameObject go = new GameObject("[FX]");
                    inst = go.AddComponent<FX>();
                    if (inst.ps == null) inst.Init();
                }
                return inst;
            }
        }

        void Awake()
        {
            if (inst == null) inst = this;
            if (ps == null) Init();
        }

        void Init()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.gravityModifier = 0.8f;
            main.maxParticles = 3000;

            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            ParticleSystemRenderer rend = GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = Mats.FxParticle;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();

            decals = new GameObject[56];
        }

        // ---------------------------------------------------------------- particles

        static void EmitOne(Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = pos;
            ep.velocity = vel;
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = life;
            I.ps.Emit(ep, 1);
        }

        /// <summary>Radial burst of glowing particles.</summary>
        public static void Burst(Vector3 p, Color c, int count, float speed, float size, float life)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = Random.onUnitSphere * speed * Random.Range(0.3f, 1f);
                EmitOne(p, v, c, size * Random.Range(0.6f, 1.3f), life * Random.Range(0.6f, 1.2f));
            }
        }

        /// <summary>Particles thrown out of a surface along its normal.</summary>
        public static void Sparks(Vector3 p, Vector3 n, Color c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (n * Random.Range(1f, 4f) + Random.insideUnitSphere * 2.5f);
                EmitOne(p + n * 0.02f, v, c, Random.Range(0.05f, 0.11f), Random.Range(0.25f, 0.55f));
            }
        }

        public static void Impact(Vector3 p, Vector3 n, Color c)
        {
            Sparks(p, n, c, 6);
            Sparks(p, n, new Color(1f, 1f, 1f, 1f), 2);
        }

        // ---------------------------------------------------------------- tracer + decal

        public static void Tracer(Vector3 a, Vector3 b, Color c)
        {
            GameObject go = new GameObject("Tracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.035f;
            lr.endWidth = 0.012f;
            lr.sharedMaterial = Mats.FxTracer;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, 0.15f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, 0.07f);
        }

        public static void Decal(Vector3 p, Vector3 n)
        {
            FX fx = I;
            GameObject d = fx.decals[fx.decalIndex];
            if (d == null)
            {
                d = GameObject.CreatePrimitive(PrimitiveType.Quad);
                d.name = "Decal";
                Collider c = d.GetComponent<Collider>();
                if (c != null) Destroy(c);
                d.GetComponent<Renderer>().sharedMaterial = Mats.FxDecal;
                d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.transform.SetParent(fx.transform, true);
                fx.decals[fx.decalIndex] = d;
            }
            fx.decalIndex = (fx.decalIndex + 1) % fx.decals.Length;
            d.transform.position = p + n * 0.012f;
            d.transform.rotation = Quaternion.LookRotation(-n) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = Random.Range(0.14f, 0.24f);
            d.transform.localScale = new Vector3(s, s, s);
        }

        public static void ClearDecals()
        {
            if (inst == null || inst.decals == null) return;
            for (int i = 0; i < inst.decals.Length; i++)
                if (inst.decals[i] != null) Destroy(inst.decals[i]);
            inst.decalIndex = 0;
        }

        // ---------------------------------------------------------------- explosions & rings

        public static void Explosion(Vector3 p, float radius)
        {
            Burst(p, new Color(1f, 0.6f, 0.15f, 1f), 70, radius * 2.2f, 0.28f, 0.9f);
            Burst(p, new Color(1f, 0.15f, 0.1f, 1f), 30, radius * 1.4f, 0.4f, 0.8f);
            Burst(p, new Color(1f, 0.95f, 0.7f, 1f), 25, radius * 3f, 0.12f, 0.5f);
            I.StartCoroutine(I.ExplosionRoutine(p, radius));
        }

        IEnumerator ExplosionRoutine(Vector3 p, float radius)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider c = orb.GetComponent<Collider>();
            if (c != null) Destroy(c);
            orb.GetComponent<Renderer>().sharedMaterial = Mats.Get("NeonOrange");
            orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orb.transform.position = p;

            Light l = Util.AddLight(orb.transform, LightType.Point, new Color(1f, 0.55f, 0.2f), 7f, radius * 2.5f, Vector3.zero);

            float t = 0f;
            const float dur = 0.4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(0.5f, radius * 1.8f, 1f - (1f - k) * (1f - k));
                orb.transform.localScale = new Vector3(s, s, s);
                l.intensity = Mathf.Lerp(7f, 0f, k);
                yield return null;
            }
            Destroy(orb);
        }

        /// <summary>Expanding flat ring (boss shockwave, landing marker...).</summary>
        public static void Ring(Vector3 p, float radius, string material, float duration)
        {
            I.StartCoroutine(I.RingRoutine(p, radius, material, duration));
        }

        IEnumerator RingRoutine(Vector3 p, float radius, string material, float duration)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider c = ring.GetComponent<Collider>();
            if (c != null) Destroy(c);
            ring.GetComponent<Renderer>().sharedMaterial = Mats.Get(material);
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.transform.position = new Vector3(p.x, 0.06f, p.z);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float d = Mathf.Lerp(0.5f, radius * 2f, k);
                float thick = Mathf.Lerp(0.12f, 0.02f, k);
                ring.transform.localScale = new Vector3(d, thick, d);
                yield return null;
            }
            Destroy(ring);
        }

        // ---------------------------------------------------------------- debris

        public static void Debris(Vector3 p, string material, int count, float size, float force)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                d.name = "Debris";
                d.layer = Layers.FX;
                d.GetComponent<Renderer>().sharedMaterial = Mats.Get(material);
                d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.transform.position = p + Random.insideUnitSphere * 0.3f;
                d.transform.rotation = Random.rotation;
                float s = size * Random.Range(0.5f, 1.4f);
                d.transform.localScale = new Vector3(s, s * Random.Range(0.5f, 1f), s * Random.Range(0.5f, 1.2f));
                Rigidbody rb = d.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                Vector3 dir = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                rb.AddForce(dir * force * Random.Range(0.5f, 1f), ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * force, ForceMode.Impulse);
                Destroy(d, Random.Range(1.8f, 3f));
            }
        }
    }
}
