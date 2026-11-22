using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// VR motion-sickness reduction: a "tunnelling" vignette that narrows the field of view while the player moves
    /// or turns with the stick (vection - seeing motion the inner ear does not feel - is the main cause of VR sickness;
    /// reducing peripheral motion reduces it). Also blinks to black for a few frames on a snap turn.
    /// Rendered as a quad in front of the camera so it works in both eyes.
    /// </summary>
    public class ComfortVignette : MonoBehaviour
    {
        Material mat;
        Renderer rend;
        VRRig rig;
        PlayerController pc;
        float amount;
        float blink;

        /// <summary>0..1 motion level reported by a mode that moves the player itself (parachute); -1 = use walking speed.</summary>
        public static float ExternalMotion = -1f;

        static readonly int idInner = Shader.PropertyToID("_Inner");
        static readonly int idOuter = Shader.PropertyToID("_Outer");
        static readonly int idBlink = Shader.PropertyToID("_Blink");
        static readonly int idStrength = Shader.PropertyToID("_Strength");

        public static ComfortVignette Create(Camera cam)
        {
            Shader sh = Mats.LoadShader("NB_Vignette");
            if (sh == null) return null;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "ComfortVignette";
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(cam.transform, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            ComfortVignette v = q.AddComponent<ComfortVignette>();
            v.rend = q.GetComponent<Renderer>();
            v.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v.rend.receiveShadows = false;
            v.mat = new Material(sh);
            v.rend.sharedMaterial = v.mat;
            v.rend.enabled = false;
            return v;
        }

        public void SetRig(VRRig r, PlayerController p) { rig = r; pc = p; }

        public void Blink() { if (GameSettings.ComfortVignette) blink = 1f; }

        void LateUpdate()
        {
            if (rig == null || pc == null || mat == null)
            {
                if (rend != null && rend.enabled) rend.enabled = false;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            float target = 0f;
            GameManager gm = GameManager.Instance;
            if (GameSettings.ComfortVignette && gm != null && gm.Playing)
            {
                Vector3 v = pc.Velocity;
                v.y = 0f;
                float speed = v.magnitude;
                float motion = ExternalMotion >= 0f ? ExternalMotion : speed / 4.5f;
                target = Mathf.Clamp01(motion + Mathf.Abs(rig.TurnRate) / 100f) * GameSettings.VignetteStrength;
            }
            amount = Mathf.MoveTowards(amount, target, dt * (target > amount ? 4f : 2f));
            blink = Mathf.MoveTowards(blink, 0f, dt / 0.12f);

            bool visible = amount > 0.01f || blink > 0.01f;
            if (rend.enabled != visible) rend.enabled = visible;
            if (!visible) return;
            float inner = Mathf.Lerp(0.85f, 0.18f, amount);
            mat.SetFloat(idInner, inner);
            mat.SetFloat(idOuter, inner + 0.3f);
            mat.SetFloat(idStrength, Mathf.Clamp01(amount * 1.5f));
            mat.SetFloat(idBlink, blink);
        }
    }
}
