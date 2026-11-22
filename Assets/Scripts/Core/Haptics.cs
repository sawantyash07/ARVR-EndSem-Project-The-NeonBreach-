using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach
{
    /// <summary>
    /// Vibration feedback: XR controller impulses (core UnityEngine.XR API, works with any OpenXR controller) and
    /// gamepad rumble. Shooting pulses the hand holding the gun (strength depends on the weapon), taking damage
    /// pulses both hands, explosions nearby give a long low rumble.
    /// </summary>
    public class Haptics : MonoBehaviour
    {
        static Haptics inst;
        readonly float[] padStopAt = { -1f, -1f, -1f, -1f };

        static Haptics I
        {
            get
            {
                if (inst == null)
                {
                    GameObject go = new GameObject("[Haptics]");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<Haptics>();
                }
                return inst;
            }
        }

        public static void PulseXR(XRNode node, float amplitude, float duration)
        {
            if (!GameSettings.Haptics) return;
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return;
            HapticCapabilities caps;
            if (d.TryGetHapticCapabilities(out caps) && !caps.supportsImpulse) return;
            d.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
        }

        public static void Rumble(int gamepad, float low, float high, float duration)
        {
            if (!GameSettings.Haptics || gamepad < 0 || gamepad >= 4) return;
            GamepadInput.SetRumble(gamepad, Mathf.Clamp01(low), Mathf.Clamp01(high));
            I.padStopAt[gamepad] = Time.unscaledTime + duration;
        }

        public static void StopAll()
        {
            for (int i = 0; i < 4; i++) GamepadInput.SetRumble(i, 0f, 0f);
            if (inst != null) for (int i = 0; i < 4; i++) inst.padStopAt[i] = -1f;
        }

        void Update()
        {
            for (int i = 0; i < padStopAt.Length; i++)
            {
                if (padStopAt[i] > 0f && Time.unscaledTime >= padStopAt[i])
                {
                    padStopAt[i] = -1f;
                    GamepadInput.SetRumble(i, 0f, 0f);
                }
            }
        }

        void OnDisable() { for (int i = 0; i < 4; i++) GamepadInput.SetRumble(i, 0f, 0f); }

        // ------------------------------------------------------------------ game events

        public static void OnShot(PlayerController p, WeaponStats st)
        {
            if (p == null) return;
            float strength = Mathf.Clamp01(0.25f + st.recoil * 0.09f);
            if (p.vr != null && p.vr.Active) PulseXR(XRNode.RightHand, strength, 0.04f + st.recoil * 0.008f);
            if (p.gamepadIndex >= 0) Rumble(p.gamepadIndex, strength * 0.3f, strength * 0.6f, 0.07f);
        }

        public static void OnDamaged(PlayerController p, float amount)
        {
            if (p == null) return;
            float k = Mathf.Clamp01(amount / 35f);
            if (p.vr != null && p.vr.Active)
            {
                PulseXR(XRNode.LeftHand, 0.4f + 0.6f * k, 0.12f + 0.15f * k);
                PulseXR(XRNode.RightHand, 0.4f + 0.6f * k, 0.12f + 0.15f * k);
            }
            if (p.gamepadIndex >= 0) Rumble(p.gamepadIndex, 0.5f + 0.5f * k, 0.3f, 0.18f + 0.15f * k);
        }

        public static void OnExplosion(PlayerController p, float closeness)
        {
            if (p == null || closeness <= 0.05f) return;
            if (p.vr != null && p.vr.Active)
            {
                PulseXR(XRNode.LeftHand, closeness, 0.3f);
                PulseXR(XRNode.RightHand, closeness, 0.3f);
            }
            if (p.gamepadIndex >= 0) Rumble(p.gamepadIndex, closeness, closeness * 0.5f, 0.35f);
        }

        public static void Tick(PlayerController p, XRNode node)
        {
            if (p != null && p.vr != null && p.vr.Active) PulseXR(node, 0.15f, 0.02f);
        }
    }
}
