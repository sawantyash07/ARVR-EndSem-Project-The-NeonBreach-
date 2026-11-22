using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>Tells the rifle which impact effect to play when a bullet hits this collider.</summary>
    public class FarmSurface : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Dirt;

        public static SurfaceKind Of(Collider c)
        {
            if (c == null) return SurfaceKind.Dirt;
            FarmSurface s = c.GetComponentInParent<FarmSurface>();
            return s != null ? s.kind : SurfaceKind.Dirt;
        }
    }
}
