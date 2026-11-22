using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>Ammunition box. Walk up and press the interact key (E / D-pad down / VR Y) to restock reserve rounds.</summary>
    public class AmmoCrate : MonoBehaviour
    {
        [Min(1)] public int roundsPerRefill = 15;
        [Tooltip("Seconds before the crate can be used again.")]
        [Min(0f)] public float cooldown = 40f;
        [Min(0.5f)] public float interactRadius = 2.4f;
        [Tooltip("Shown while the crate is restocking.")]
        public GameObject fullVisual;

        float readyAt;

        public bool Ready { get { return Time.time >= readyAt; } }
        public float CooldownLeft { get { return Mathf.Max(0f, readyAt - Time.time); } }

        public bool InRange(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= interactRadius * interactRadius;
        }

        /// <summary>Returns how many rounds were taken.</summary>
        public int Take(SniperRifle rifle)
        {
            if (!Ready || rifle == null) return 0;
            int got = rifle.AddReserve(roundsPerRefill);
            if (got > 0)
            {
                readyAt = Time.time + cooldown;
                if (fullVisual != null) fullVisual.SetActive(false);
            }
            return got;
        }

        public void ResetCrate()
        {
            readyAt = 0f;
            if (fullVisual != null) fullVisual.SetActive(true);
        }

        void Update()
        {
            if (fullVisual != null && !fullVisual.activeSelf && Ready) fullVisual.SetActive(true);
        }
    }
}
