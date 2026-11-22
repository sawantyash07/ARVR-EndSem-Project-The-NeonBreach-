using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>A mouse hole: mice pop out of burrows when they spawn, and frightened mice may dive into one and reappear elsewhere.</summary>
    public class MouseBurrow : MonoBehaviour
    {
        [Tooltip("Can new mice appear here?")]
        public bool spawnPoint = true;

        public Vector3 Position { get { return transform.position; } }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
}
