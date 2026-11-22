using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Marks an object mice can hide behind (barrel, crate, hay bale, bush, trough...). The spawner turns every cover
    /// into hiding spots on the side facing away from the player. Soft cover (crops) hides mice visually but does
    /// not stop bullets, so its spots are inside the patch instead of behind it.
    /// </summary>
    public class RatCover : MonoBehaviour
    {
        [Tooltip("Approximate footprint radius (m).")]
        public float radius = 0.6f;
        [Tooltip("Crops etc.: mice hide inside rather than behind it, and it does not block shots.")]
        public bool soft;
        [Tooltip("Half size of the area for soft cover (x, z).")]
        public Vector2 softHalfSize = new Vector2(2f, 2f);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.6f);
            if (soft) Gizmos.DrawWireCube(transform.position, new Vector3(softHalfSize.x * 2f, 0.2f, softHalfSize.y * 2f));
            else Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
