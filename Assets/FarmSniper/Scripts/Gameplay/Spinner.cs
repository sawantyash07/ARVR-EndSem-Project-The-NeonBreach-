using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>Spins a transform (windmill rotor, weather vane) with gusty variation.</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degreesPerSecond = 60f;
        [Range(0f, 1f)] public float gustiness = 0.4f;
        float seed;

        void Awake() { seed = Random.value * 100f; }

        void Update()
        {
            float gust = 1f + (Mathf.PerlinNoise(Time.time * 0.15f, seed) - 0.5f) * 2f * gustiness;
            transform.Rotate(axis, degreesPerSecond * gust * Time.deltaTime, Space.Self);
        }
    }
}
