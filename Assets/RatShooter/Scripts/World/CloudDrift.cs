using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>Slowly drifts a cartoon cloud across the sky and wraps it around, with a gentle bob.</summary>
    public class CloudDrift : MonoBehaviour
    {
        public Vector3 velocity = new Vector3(1.2f, 0f, 0f);
        public float wrapX = 200f;
        float baseY, seed;

        void Awake()
        {
            baseY = transform.position.y;
            seed = Random.value * 10f;
        }

        void Update()
        {
            Vector3 p = transform.position + velocity * Time.deltaTime;
            if (p.x > wrapX) p.x = -wrapX;
            if (p.x < -wrapX) p.x = wrapX;
            p.y = baseY + Mathf.Sin(Time.time * 0.1f + seed) * 1.5f;
            transform.position = p;
        }
    }
}
