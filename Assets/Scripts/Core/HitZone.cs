using UnityEngine;

namespace NeonBreach
{
    /// <summary>Put on a collider that belongs to an IDamageable to give it a damage multiplier (e.g. a head).</summary>
    public class HitZone : MonoBehaviour
    {
        public float multiplier = 1f;
        public bool isHead;
    }
}
