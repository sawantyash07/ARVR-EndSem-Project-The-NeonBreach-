using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// Editor helpers for the cinematic startup intro: add the "Cinematic Intro" object to the game scene (the scene
    /// builder calls <see cref="Ensure"/> so a rebuilt scene keeps it), select it to edit its settings in the
    /// Inspector, and replay it in Play mode.
    /// </summary>
    public static class IntroSceneSetup
    {
        public const string ObjectName = "Cinematic Intro";

        [MenuItem("Neon Breach/Cinematic Intro/Add To Open Scene")]
        static void AddFromMenu()
        {
            CinematicIntro ci = Ensure();
            if (ci == null) return;
            Selection.activeObject = ci.gameObject;
            EditorGUIUtility.PingObject(ci.gameObject);
        }

        [MenuItem("Neon Breach/Cinematic Intro/Select Intro Settings")]
        static void SelectSettings()
        {
            CinematicIntro ci = Object.FindAnyObjectByType<CinematicIntro>(FindObjectsInactive.Include);
            if (ci == null)
            {
                Debug.LogWarning("[Neon Breach] No Cinematic Intro in the open scene - use Neon Breach > Cinematic Intro > Add To Open Scene.");
                return;
            }
            Selection.activeObject = ci.gameObject;
            EditorGUIUtility.PingObject(ci.gameObject);
        }

        [MenuItem("Neon Breach/Cinematic Intro/Replay (Play mode)")]
        static void Replay()
        {
            CinematicIntro ci = Object.FindAnyObjectByType<CinematicIntro>(FindObjectsInactive.Include);
            if (ci != null) ci.Replay();
        }

        [MenuItem("Neon Breach/Cinematic Intro/Replay (Play mode)", true)]
        static bool ReplayValidate() { return EditorApplication.isPlaying; }

        /// <summary>Make sure the open game scene has one Cinematic Intro object. Returns null outside the game scene.</summary>
        public static CinematicIntro Ensure()
        {
            CinematicIntro ci = Object.FindAnyObjectByType<CinematicIntro>(FindObjectsInactive.Include);
            if (ci != null) return ci;
            if (Object.FindAnyObjectByType<GameManager>() == null)
            {
                Debug.LogWarning("[Neon Breach] Open the Neon Breach game scene first: the intro belongs in the scene with the main menu.");
                return null;
            }
            GameObject go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Add Cinematic Intro");
            ci = go.AddComponent<CinematicIntro>();
            EditorSceneManager.MarkSceneDirty(go.scene);
            return ci;
        }
    }
}
