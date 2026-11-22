using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Put one of these in a scene that is a self-contained level (e.g. the Farm Sniper scene). GameBootstrap then
    /// leaves the scene alone instead of generating the Neon Breach arena into it.
    /// </summary>
    [DisallowMultipleComponent]
    public class StandaloneSceneMarker : MonoBehaviour
    {
        [Tooltip("Shown in the console when the scene starts.")]
        public string levelName = "Standalone level";
    }
}
