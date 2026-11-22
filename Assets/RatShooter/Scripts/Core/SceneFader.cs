using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Smooth scene transitions: fades the picture (and the sound) to black, loads the next scene asynchronously,
    /// then fades back in. Survives the scene change on its own and removes itself afterwards.
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        static SceneFader active;

        public static bool Busy { get { return active != null; } }

        float alpha;
        string label = "";
        float volume = 1f;

        /// <summary>Fade out, load the scene by name, fade in. Ignored if a transition is already running.</summary>
        public static void Load(string sceneName, string loadingLabel = "LOADING...")
        {
            if (active != null) return;
            GameObject go = new GameObject("[SceneFader]");
            DontDestroyOnLoad(go);
            active = go.AddComponent<SceneFader>();
            active.label = loadingLabel;
            active.StartCoroutine(active.Run(sceneName));
        }

        IEnumerator Run(string sceneName)
        {
            volume = Mathf.Max(0.0001f, AudioListener.volume);
            const float fadeOut = 0.45f, fadeIn = 0.55f;
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                alpha = Mathf.SmoothStep(0f, 1f, t / fadeOut);
                AudioListener.volume = volume * (1f - alpha);
                yield return null;
            }
            alpha = 1f;
            AudioListener.volume = 0f;
            Time.timeScale = 1f;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError("[Rat Shooter] Scene '" + sceneName + "' could not be loaded - is it in File > Build Profiles?");
                AudioListener.volume = volume;
                active = null;
                Destroy(gameObject);
                yield break;
            }
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
            float target = GameSettings.MasterVolume;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                alpha = 1f - Mathf.SmoothStep(0f, 1f, t / fadeIn);
                AudioListener.volume = target * (1f - alpha);
                yield return null;
            }
            AudioListener.volume = target;
            active = null;
            Destroy(gameObject);
        }

        void OnGUI()
        {
            if (alpha <= 0.001f) return;
            GUI.depth = -10000;
            Color old = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.04f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (alpha > 0.95f && !string.IsNullOrEmpty(label))
            {
                GUIStyle s = new GUIStyle(GUI.skin.label);
                s.alignment = TextAnchor.MiddleCenter;
                s.fontSize = Mathf.RoundToInt(Screen.height / 30f);
                s.fontStyle = FontStyle.Bold;
                s.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, Screen.height * 0.45f, Screen.width, Screen.height * 0.1f), label, s);
            }
            GUI.color = old;
        }
    }
}
