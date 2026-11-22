using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>Something tasty (cheese, spilled grain, corn cob, apple...). Greedy mice walk here and sit up to nibble.</summary>
    public class MouseFood : MonoBehaviour
    {
        [Tooltip("How attractive this food is compared to others.")]
        public float appeal = 1f;
        [Tooltip("Mice eat within this radius around the food.")]
        public float radius = 0.35f;

        [System.NonSerialized] public int eaters;

        public Vector3 Position { get { return transform.position; } }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
