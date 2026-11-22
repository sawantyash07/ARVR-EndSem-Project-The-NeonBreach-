using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Safety net: if you press Play in a scene that has no game in it (e.g. the default SampleScene),
    /// the whole world is generated at runtime so the project always runs.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGame()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            // self-contained levels (e.g. the Farm Sniper scene) bring their own game
            if (Object.FindAnyObjectByType<StandaloneSceneMarker>() != null) return;

            // Remove default cameras / lights from a stock scene so there is exactly one camera and listener.
            Camera[] cams = Object.FindObjectsByType<Camera>();
            for (int i = 0; i < cams.Length; i++) Object.Destroy(cams[i].gameObject);
            Light[] lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++) Object.Destroy(lights[i].gameObject);

            Debug.Log("[Neon Breach] No game found in the open scene - building the world at runtime.");
            WorldBuilder.BuildAll();
        }
    }
}
