using UnityEngine;

namespace NeonBreach
{
    /// <summary>Glowing energy bolt fired by gunners, drones and the boss. Swept collision, so it never tunnels.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        Vector3 dir;
        float speed;
        float damage;
        float life = 6f;
        Color color;
        float radius = 0.16f;

        public static EnemyProjectile Spawn(Vector3 pos, Vector3 direction, float speed, float damage, string glowMaterial, float size = 0.3f)
        {
            GameObject go = new GameObject("EnemyProjectile");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(direction);
            Util.Part(go.transform, PrimitiveType.Sphere, "Bolt", Vector3.zero, Vector3.zero, Vector3.one * size, glowMaterial);
            Util.NoShadows(go);

            Color c = glowMaterial == "NeonOrange" ? new Color(1f, 0.5f, 0.1f) :
                      glowMaterial == "NeonYellow" ? new Color(1f, 0.9f, 0.2f) :
                      glowMaterial == "NeonGreen" ? new Color(0.3f, 1f, 0.35f) :
                      glowMaterial == "NeonCyan" ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.15f, 0.65f);
            Util.AddLight(go.transform, LightType.Point, c, 1.6f, 4.5f, Vector3.zero);

            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.startWidth = 0.22f * size / 0.3f;
            tr.endWidth = 0f;
            tr.sharedMaterial = Mats.FxTracer;
            tr.startColor = c;
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            EnemyProjectile p = go.AddComponent<EnemyProjectile>();
            p.dir = direction.normalized;
            p.speed = speed;
            p.damage = damage;
            p.color = c;
            p.radius = Mathf.Max(0.12f, size * 0.55f);
            return p;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.Playing) return;

            float dt = Time.deltaTime * PowerUps.EnemyTimeScale;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }

            float step = speed * dt;
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, radius, dir, out hit, step, Layers.PlayerShotMask, QueryTriggerInteraction.Ignore))
            {
                IDamageable d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null) d.TakeDamage(damage, hit.point, false, gameObject);
                FX.Burst(hit.point, color, 10, 4f, 0.12f, 0.4f);
                Destroy(gameObject);
                return;
            }
            transform.position += dir * step;
        }
    }
}
