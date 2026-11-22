using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Thrown grenade with simple custom physics (gravity + sphere-cast bounce), a fuse, proximity trigger
    /// and a radial explosion with distance falloff. No Rigidbody is used, so it is independent of physics API changes.
    /// </summary>
    public class Grenade : MonoBehaviour
    {
        public float fuse = 2.4f;
        public float radius = 6.5f;
        public float maxDamage = 160f;

        public PlayerController owner;
        Vector3 vel;
        bool exploded;
        Transform glow;
        float blink;

        public static Grenade Spawn(Vector3 pos, Vector3 velocity, PlayerController owner = null)
        {
            GameObject go = new GameObject("Grenade");
            go.layer = Layers.FX;
            go.transform.position = pos;
            Util.Part(go.transform, PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.zero, Vector3.one * 0.22f, "GunMetal");
            Util.Part(go.transform, PrimitiveType.Cylinder, "Band", Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.115f, 0.1f), "GunGrip");
            GameObject g = Util.Part(go.transform, PrimitiveType.Sphere, "Glow", new Vector3(0f, 0.1f, 0f), Vector3.zero, Vector3.one * 0.09f, "NeonRed");
            Grenade gr = go.AddComponent<Grenade>();
            gr.vel = velocity;
            gr.owner = owner;
            gr.glow = g.transform;
            return gr;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.Playing || exploded) return;
            float dt = Time.deltaTime;

            fuse -= dt;
            blink += dt * Mathf.Lerp(6f, 24f, 1f - Mathf.Clamp01(fuse / 2.4f));
            if (glow != null) glow.gameObject.SetActive(Mathf.Sin(blink) > -0.2f);
            transform.Rotate(new Vector3(260f, 190f, 0f) * dt * Mathf.Clamp01(vel.magnitude / 4f), Space.Self);

            vel += Vector3.down * 20f * dt;
            Vector3 step = vel * dt;
            float len = step.magnitude;
            if (len > 0.0001f)
            {
                RaycastHit hit;
                int mask = Layers.EnvMask | Layers.EnemyMask;
                if (Physics.SphereCast(transform.position, 0.11f, step / len, out hit, len, mask, QueryTriggerInteraction.Ignore))
                {
                    int layer = hit.collider.gameObject.layer;
                    if (layer == Layers.Enemy || layer == Layers.EnemyHead)
                    {
                        Explode();
                        return;
                    }
                    transform.position = hit.point + hit.normal * 0.115f;
                    vel = Vector3.Reflect(vel, hit.normal) * 0.45f;
                    if (hit.normal.y > 0.7f && vel.magnitude < 1.5f) vel = Vector3.zero;
                    if (vel.magnitude > 2f) Sfx.Play3D(SfxId.Switch, transform.position, 0.5f, 0.6f);
                }
                else
                {
                    transform.position += step;
                }
            }

            if (Physics.CheckSphere(transform.position, 0.55f, Layers.EnemyMask, QueryTriggerInteraction.Ignore))
            {
                Explode();
                return;
            }
            if (fuse <= 0f) Explode();
        }

        void Explode()
        {
            if (exploded) return;
            exploded = true;
            Vector3 p = transform.position;

            FX.Explosion(p, radius);
            Sfx.Play3D(SfxId.Explosion, p, 1f);

            int mask = Layers.EnemyMask | (1 << Layers.Player);
            Collider[] cols = Physics.OverlapSphere(p, radius, mask, QueryTriggerInteraction.Ignore);
            HashSet<IDamageable> done = new HashSet<IDamageable>();
            GameManager gm = GameManager.Instance;

            for (int i = 0; i < cols.Length; i++)
            {
                IDamageable d = cols[i].GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead || done.Contains(d)) continue;
                done.Add(d);

                Component c = d as Component;
                Vector3 target = c != null ? c.transform.position + Vector3.up * 1f : p;
                float dist = Vector3.Distance(p, target);
                float f = Mathf.Clamp01(1f - dist / radius);
                if (f <= 0f) continue;

                bool isPlayer = d is PlayerController;
                float dmg = maxDamage * Mathf.Lerp(0.25f, 1f, f) * (isPlayer ? 0.3f : Upgrades.DamageMult * PowerUps.DamageMult);
                d.TakeDamage(dmg, target, false, gameObject);

                Enemy e = d as Enemy;
                if (e != null)
                {
                    Vector3 away = e.transform.position - p;
                    away.y = 0f;
                    e.Knock(away, 14f * f);
                }
            }

            if (gm != null)
            {
                for (int i = 0; i < gm.Players.Count; i++)
                {
                    PlayerController pl = gm.Players[i];
                    if (pl == null) continue;
                    float close = Mathf.Clamp01(1f - Vector3.Distance(pl.transform.position, p) / 25f);
                    pl.Shake(close * 0.5f);
                    Haptics.OnExplosion(pl, close);
                }
            }
            Destroy(gameObject);
        }
    }
}
