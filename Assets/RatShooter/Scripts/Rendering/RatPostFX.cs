using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Warm, sunny colour grading + soft bloom + vignette for the cartoon farm, reusing Neon Breach's Built-in
    /// pipeline post-processing shader (Resources/Shaders/NB_PostFX) with its own settings. Follows the shared
    /// "Post-processing" and "Bloom" options.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RatPostFX : MonoBehaviour
    {
        public float bloom = 0.35f;
        public float threshold = 0.92f;
        public float saturation = 1.1f;
        public float contrast = 1.1f;
        public float exposure = 0.97f;
        public float vignette = 0.3f;
        public Color lift = new Color(0.015f, 0.01f, 0.03f, 0f);
        public Color gain = new Color(1.02f, 1f, 0.97f, 0f);

        static readonly int idThreshold = Shader.PropertyToID("_Threshold");
        static readonly int idKnee = Shader.PropertyToID("_Knee");
        static readonly int idIntensity = Shader.PropertyToID("_Intensity");
        static readonly int idSaturation = Shader.PropertyToID("_Saturation");
        static readonly int idContrast = Shader.PropertyToID("_Contrast");
        static readonly int idVignette = Shader.PropertyToID("_Vignette");
        static readonly int idChroma = Shader.PropertyToID("_Chroma");
        static readonly int idExposure = Shader.PropertyToID("_Exposure");
        static readonly int idTint = Shader.PropertyToID("_Tint");
        static readonly int idLift = Shader.PropertyToID("_Lift");
        static readonly int idGain = Shader.PropertyToID("_Gain");
        static readonly int idBloomTex = Shader.PropertyToID("_BloomTex");

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[8];

        void Awake()
        {
            Shader sh = Mats.LoadShader("NB_PostFX");
            if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null || !GameSettings.PostFx)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat.SetFloat(idThreshold, threshold);
            mat.SetFloat(idKnee, 0.45f);
            mat.SetFloat(idIntensity, bloom * Mathf.Clamp(GameSettings.Bloom / 0.6f, 0f, 2f));
            mat.SetFloat(idSaturation, saturation);
            mat.SetFloat(idContrast, contrast);
            mat.SetFloat(idVignette, vignette);
            mat.SetFloat(idChroma, 0f);
            mat.SetFloat(idExposure, exposure);
            mat.SetVector(idTint, new Vector4(1f, 1f, 1f, 0f));
            mat.SetVector(idLift, new Vector4(lift.r, lift.g, lift.b, 0f));
            mat.SetVector(idGain, new Vector4(gain.r, gain.g, gain.b, 0f));

            RenderTextureFormat fmt = src.format;
            int w = src.width / 2, h = src.height / 2;
            RenderTexture cur = chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            int n = 1;
            for (; n < 6; n++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, chain[n], mat, 1);
                cur = chain[n];
            }
            for (int i = n - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, chain[i], mat, 2);
                RenderTexture.ReleaseTemporary(cur);
                cur = chain[i];
            }
            mat.SetTexture(idBloomTex, cur);
            Graphics.Blit(src, dst, mat, 3);
            RenderTexture.ReleaseTemporary(cur);
        }
    }
}
