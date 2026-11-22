using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>A brass casing ejected from the rifle: bounces with a tink and is recycled after a few seconds.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShellCasing : MonoBehaviour
    {
        public float lifetime = 2.5f;
        float age;
        int bounces;
        Rigidbody rb;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        public void Launch(Vector3 pos, Quaternion rot, Vector3 velocity)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(pos, rot);
            rb.position = pos;
            rb.rotation = rot;
            rb.linearVelocity = velocity;
            rb.angularVelocity = new Vector3(Random.Range(-25f, 25f), Random.Range(-25f, 25f), Random.Range(-25f, 25f));
            age = 0f;
            bounces = 0;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > lifetime) gameObject.SetActive(false);
        }

        void OnCollisionEnter(Collision c)
        {
            if (bounces++ < 2 && c.relativeVelocity.magnitude > 0.6f)
                RatAudio.Play3D(RatSound.Casing, transform.position, bounces == 1 ? 0.35f : 0.18f, Random.Range(0.9f, 1.15f));
        }
    }
}
