using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NeonBreach
{
    /// <summary>
    /// Gamepad support (Xbox / PlayStation / generic) through the Input System package. Used by player 1 when
    /// playing solo and by player 2 in split-screen co-op. Compiles to a no-op if the Input System is disabled.
    ///   L-stick move    R-stick look    RT fire    LT aim    A jump    B crouch/slide    X reload    Y next weapon
    ///   LB dash    RB grenade    L3 sprint    R3 melee    D-pad up flashlight    D-pad down medkit    Start pause
    /// </summary>
    public static class GamepadInput
    {
#if ENABLE_INPUT_SYSTEM
        public static int Count { get { return Gamepad.all.Count; } }

        static Gamepad Get(int index)
        {
            if (index < 0 || index >= Gamepad.all.Count) return null;
            return Gamepad.all[index];
        }

        public static bool Connected(int index) { return Get(index) != null; }

        public static string DeviceName(int index)
        {
            Gamepad g = Get(index);
            return g == null ? "-" : g.displayName;
        }

        public static void Read(int index, PlayerInputState s)
        {
            Gamepad g = Get(index);
            if (g == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector2 m = g.leftStick.ReadValue();
            if (m.sqrMagnitude > s.move.sqrMagnitude) s.move = Vector2.ClampMagnitude(m, 1f);

            // response curve: precise near the centre, fast at full deflection (~200 deg/s)
            Vector2 r = g.rightStick.ReadValue();
            Vector2 curved = new Vector2(Mathf.Sign(r.x) * r.x * r.x, Mathf.Sign(r.y) * r.y * r.y);
            s.look += curved * 90f * dt;

            s.fireHeld |= g.rightTrigger.ReadValue() > 0.35f;
            s.firePressed |= g.rightTrigger.wasPressedThisFrame;
            s.aimHeld |= g.leftTrigger.ReadValue() > 0.35f;
            s.jumpPressed |= g.buttonSouth.wasPressedThisFrame;
            s.crouchHeld |= g.buttonEast.isPressed;
            s.reloadPressed |= g.buttonWest.wasPressedThisFrame;
            if (g.buttonNorth.wasPressedThisFrame) s.weaponCycle = 1;
            if (g.dpad.right.wasPressedThisFrame) s.weaponCycle = 1;
            if (g.dpad.left.wasPressedThisFrame) s.weaponCycle = -1;
            s.dashPressed |= g.leftShoulder.wasPressedThisFrame;
            s.grenadePressed |= g.rightShoulder.wasPressedThisFrame;
            s.sprintHeld |= g.leftStickButton.isPressed;
            s.meleePressed |= g.rightStickButton.wasPressedThisFrame;
            s.flashlightPressed |= g.dpad.up.wasPressedThisFrame;
            s.medkitPressed |= g.dpad.down.wasPressedThisFrame;
        }

        public static bool StartPressed(int index)
        {
            Gamepad g = Get(index);
            return g != null && g.startButton.wasPressedThisFrame;
        }

        public static bool AnyStartPressed()
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i].startButton.wasPressedThisFrame) return true;
            return false;
        }

        public static void SetRumble(int index, float low, float high)
        {
            Gamepad g = Get(index);
            if (g != null) g.SetMotorSpeeds(low, high);
        }
#else
        public static int Count { get { return 0; } }
        public static bool Connected(int index) { return false; }
        public static string DeviceName(int index) { return "-"; }
        public static void Read(int index, PlayerInputState s) { }
        public static bool StartPressed(int index) { return false; }
        public static bool AnyStartPressed() { return false; }
        public static void SetRumble(int index, float low, float high) { }
#endif
    }
}
