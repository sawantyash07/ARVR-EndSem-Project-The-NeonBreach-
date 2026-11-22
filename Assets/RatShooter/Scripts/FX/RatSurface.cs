using UnityEngine;

namespace NeonBreach.RatShooter
{
    public enum SurfaceKind { Grass, Dirt, Wood, Metal, Hay, Leaves, Stone, Water }

    /// <summary>What a bullet hits on this object - picks the impact effect (dirt puff, splinters, sparks, straw...).</summary>
    public class RatSurface : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Wood;
    }
}
