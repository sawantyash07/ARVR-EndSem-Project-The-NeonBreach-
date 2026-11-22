using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>A rat burrow. Rats appear here and may escape back into any burrow.</summary>
    public class RatSpawnPoint : MonoBehaviour
    {
        [Tooltip("0 = open ground near the farmer (all difficulties), 1 = inside the fields (Normal+), 2 = far edges, buildings, corners (Hard).")]
        [Range(0, 2)] public int tier;
        [HideInInspector] public float lastUsed = -100f;

        void OnDrawGizmos()
        {
            Gizmos.color = tier == 0 ? Color.green : (tier == 1 ? Color.yellow : Color.red);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.4f);
        }
    }
}
