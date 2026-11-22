using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>Spins an object (windmill fan) with a gusty, varying speed.</summary>
    public class RatSpinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float speed = 90f;
        public float gustAmount = 0.4f;
        float seed;

        void Awake() { seed = Random.value * 10f; }

        void Update()
        {
            float gust = 1f + (Mathf.PerlinNoise(seed, Time.time * 0.2f) - 0.5f) * 2f * gustAmount;
            transform.Rotate(axis, speed * gust * Time.deltaTime, Space.Self);
        }
    }
}
