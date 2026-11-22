using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Sunny cartoon lighting for the farm: sky material, warm sun, three-colour ambient (sky / horizon / ground)
    /// and soft distance fog matching the horizon. Applied when the scene loads (realtime lighting - nothing baked).
    /// </summary>
    public class RatEnvironment : MonoBehaviour
    {
        public Material skybox;
        public Light sun;
        public Color ambientSky = new Color(0.5f, 0.58f, 0.78f);
        public Color ambientEquator = new Color(0.54f, 0.56f, 0.5f);
        public Color ambientGround = new Color(0.33f, 0.3f, 0.24f);
        public Color fogColor = new Color(0.78f, 0.88f, 0.98f);
        public float fogStart = 45f;
        public float fogEnd = 170f;

        void Awake() { Apply(); }

        public void Apply()
        {
            if (skybox != null) RenderSettings.skybox = skybox;
            if (sun != null) RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}
