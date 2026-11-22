using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// First-open automation: when the project is opened for the first time (no scene yet), build the game scene
    /// automatically. On later opens, simply open the game scene if an empty untitled scene is showing.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        const string SessionKey = "NeonBreach.BootstrapDone";

        static ProjectBootstrap()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= Tick;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            try
            {
                if (!File.Exists(SceneBuilder.ScenePath))
                {
                    SceneBuilder.Build(true);
                }
                else
                {
                    Scene active = SceneManager.GetActiveScene();
                    if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                        EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
                UnityEngine.Debug.LogWarning("[Neon Breach] Auto-build failed. Use the menu: Neon Breach > Build Game Scene (rebuild).");
            }
        }
    }
}
