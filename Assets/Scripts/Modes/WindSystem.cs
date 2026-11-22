using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Shared horizontal wind used by the mini-games: arrows drift with it (archery), the canopy is carried by it and it
    /// gets stronger with altitude (parachute). A base wind is set per round / run; slow Perlin gusts vary its speed
    /// by +-gust and its direction by a few degrees. Direction is the compass heading the wind blows TOWARDS.
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        public static WindSystem Instance { get; private set; }

        public float BaseSpeed { get; private set; }
        /// <summary>Degrees, 0 = blowing towards +Z, 90 = towards +X.</summary>
        public float BaseDirection { get; private set; }
        public float Gust { get; private set; }

        /// <summary>Wind velocity right now (m/s, horizontal), including gusts.</summary>
        public Vector3 Current { get; private set; }
        public float Speed { get { return Current.magnitude; } }
        public float Direction
        {
            get { return Current.sqrMagnitude < 0.0001f ? BaseDirection : Mathf.Atan2(Current.x, Current.z) * Mathf.Rad2Deg; }
        }

        float seed;

        void Awake()
        {
            Instance = this;
            seed = Random.Range(0f, 100f);
        }

        public void Set(float speed, float directionDeg, float gust)
        {
            BaseSpeed = Mathf.Max(0f, speed);
            BaseDirection = directionDeg;
            Gust = Mathf.Clamp01(gust);
            seed = Random.Range(0f, 100f);
            Evaluate(0f);
        }

        /// <summary>Random wind between min and max m/s from any direction.</summary>
        public void Randomize(float min, float max, float gust)
        {
            Set(Random.Range(min, max), Random.Range(0f, 360f), gust);
        }

        public void Calm() { Set(0f, 0f, 0f); }

        /// <summary>Wind at a height above sea level: stronger aloft (logarithmic-ish profile, x2 at 3000 m).</summary>
        public Vector3 At(float altitude)
        {
            float k = 1f + Mathf.Clamp(altitude, 0f, 4000f) / 3000f;
            return Current * k;
        }

        void Update()
        {
            Evaluate(Time.time);
        }

        void Evaluate(float t)
        {
            float g = (Mathf.PerlinNoise(seed, t * 0.23f) - 0.5f) * 2f;          // -1..1, slow
            float d = (Mathf.PerlinNoise(seed + 17f, t * 0.11f) - 0.5f) * 2f;
            float speed = BaseSpeed * (1f + g * Gust);
            float dir = (BaseDirection + d * 12f * Gust) * Mathf.Deg2Rad;
            Current = new Vector3(Mathf.Sin(dir), 0f, Mathf.Cos(dir)) * speed;
        }

        /// <summary>Wind direction relative to a view direction, in degrees (0 = blowing away from you, 90 = to the right).</summary>
        public float RelativeTo(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            Vector3 w = Current.sqrMagnitude < 0.0001f
                ? new Vector3(Mathf.Sin(BaseDirection * Mathf.Deg2Rad), 0f, Mathf.Cos(BaseDirection * Mathf.Deg2Rad))
                : Current;
            return Vector3.SignedAngle(forward, w, Vector3.up);
        }
    }
}
