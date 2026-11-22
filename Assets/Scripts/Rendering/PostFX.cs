using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Bloom + colour grading + vignette + chromatic aberration for the Built-in Render Pipeline, using
    /// OnRenderImage and a custom shader (Resources/Shaders/NB_PostFX). This gives the neon glow without switching the
    /// project to URP (which would mean redoing every material). Gameplay-driven effects:
    ///   taking damage -> chromatic aberration pulse,  slow motion -> cold desaturated tint,
    ///   double damage -> warm red tint,  low health -> desaturation.
    /// Disabled automatically while in VR (keeps frame rate high; the headset gets the comfort vignette instead).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
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
        PlayerController owner;
        readonly RenderTexture[] chain = new RenderTexture[8];
        float tintA;
        Color tint = Color.white;

        public static void AttachTo(Camera c)
        {
            if (c == null || c.GetComponent<PostFX>() != null) return;
            c.gameObject.AddComponent<PostFX>();
        }

        void Awake()
        {
            Shader sh = Mats.LoadShader("NB_PostFX");
            if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("[Neon Breach] Post-processing shader not available - post FX disabled.");
            owner = GetComponentInParent<PlayerController>();
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null || !GameSettings.PostFx || (owner != null && owner.VrOn))
            {
                Graphics.Blit(src, dst);
                return;
            }

            float now = Time.time;
            float hurt = owner != null ? Mathf.Clamp01(1f - (now - owner.LastDamageTime) / 0.45f) : 0f;
            float low = owner != null ? Mathf.Clamp01((0.35f - owner.Health / owner.maxHealth) / 0.35f) : 0f;
            bool playing = GameManager.Instance != null && GameManager.Instance.Playing;

            Color wantTint = Color.white;
            float wantA = 0f;
            if (playing && PowerUps.SlowMo) { wantTint = new Color(0.65f, 0.85f, 1.25f); wantA = 0.45f; }
            else if (playing && PowerUps.DoubleDamage) { wantTint = new Color(1.25f, 0.8f, 0.75f); wantA = 0.25f; }
            tint = Color.Lerp(tint, wantTint, Time.unscaledDeltaTime * 5f);
            tintA = Mathf.Lerp(tintA, wantA, Time.unscaledDeltaTime * 5f);

            mat.SetFloat(idThreshold, 0.85f);
            mat.SetFloat(idKnee, 0.4f);
            mat.SetFloat(idIntensity, GameSettings.Bloom);
            mat.SetFloat(idSaturation, Mathf.Lerp(1.18f, 0.35f, low) * (PowerUps.SlowMo && playing ? 0.7f : 1f));
            mat.SetFloat(idContrast, 1.08f);
            mat.SetFloat(idVignette, 0.45f + low * 0.5f);
            mat.SetFloat(idChroma, hurt * 1.6f + low * 0.4f);
            mat.SetFloat(idExposure, 1f);
            mat.SetVector(idTint, new Vector4(tint.r, tint.g, tint.b, tintA));
            mat.SetVector(idLift, new Vector4(0.0f, 0.025f, 0.05f, 0f));     // teal shadows
            mat.SetVector(idGain, new Vector4(1.03f, 0.98f, 1.04f, 0f));     // slightly magenta highlights

            // bloom: prefilter + downsample chain, then additive upsample back up
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
