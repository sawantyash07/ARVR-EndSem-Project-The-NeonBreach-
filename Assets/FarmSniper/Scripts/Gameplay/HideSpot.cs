using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>Cover a frightened rat can run behind (hay bale, crate, barrel, tractor...).</summary>
    public class HideSpot : MonoBehaviour
    {
        [Tooltip("Approximate radius of the object; rats hide this far plus a margin on the far side from the threat.")]
        [Min(0.2f)] public float radius = 1f;
        [Tooltip("Tall crops: rats are hidden anywhere inside, not only behind.")]
        public bool insideCover;

        /// <summary>A point on the side of this object facing away from the threat.</summary>
        public Vector3 PointBehind(Vector3 threat)
        {
            Vector3 away = transform.position - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away) * Random.Range(-0.4f, 0.4f) * radius;
            return transform.position + away * (insideCover ? Random.Range(0f, radius * 0.6f) : radius + 0.45f) + side;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
