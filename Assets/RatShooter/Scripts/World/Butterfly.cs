using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>A cartoon butterfly that flutters around a home point (wings flap, wanders, bobs).</summary>
    public class Butterfly : MonoBehaviour
    {
        public Transform wingL, wingR;
        public float radius = 3f;
        public float speed = 1.4f;
        Vector3 home, target;
        float seed, retarget;

        void Awake()
        {
            home = transform.position;
            seed = Random.value * 100f;
            target = home;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float t = Time.time + seed;
            retarget -= dt;
            if (retarget <= 0f || (transform.position - target).sqrMagnitude < 0.05f)
            {
                retarget = Random.Range(1f, 3f);
                Vector2 c = Random.insideUnitCircle * radius;
                target = home + new Vector3(c.x, Random.Range(-0.3f, 0.8f), c.y);
            }
            Vector3 d = target - transform.position;
            Vector3 v = d.normalized * speed + new Vector3(Mathf.Sin(t * 3.1f), Mathf.Sin(t * 5.3f) * 1.5f, Mathf.Cos(t * 2.7f)) * 0.5f;
            transform.position += v * dt;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), dt * 4f);
            float flap = Mathf.Sin(t * 22f) * 60f;
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, 20f + flap);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -20f - flap);
        }
    }
}
