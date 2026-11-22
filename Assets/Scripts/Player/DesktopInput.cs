#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define NB_NEW_INPUT
#endif

using System.Collections.Generic;
using UnityEngine;
#if NB_NEW_INPUT
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NeonBreach
{
    /// <summary>
    /// Keyboard + mouse reader that compiles against BOTH Unity input backends (legacy Input Manager and the
    /// Input System package). Keys are addressed with UnityEngine.KeyCode so bindings work with either backend.
    /// </summary>
    public static class DesktopInput
    {
#if NB_NEW_INPUT
        static readonly Dictionary<KeyCode, Key> keyMap = new Dictionary<KeyCode, Key>();

        static Key ToKey(KeyCode kc)
        {
            Key k;
            if (keyMap.TryGetValue(kc, out k)) return k;
            k = Key.None;
            if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9) k = Key.Digit0 + (kc - KeyCode.Alpha0);
            else if (kc >= KeyCode.Keypad0 && kc <= KeyCode.Keypad9) k = Key.Numpad0 + (kc - KeyCode.Keypad0);
            else
            {
                switch (kc)
                {
                    case KeyCode.LeftControl: k = Key.LeftCtrl; break;
                    case KeyCode.RightControl: k = Key.RightCtrl; break;
                    case KeyCode.Return: k = Key.Enter; break;
                    case KeyCode.KeypadEnter: k = Key.NumpadEnter; break;
                    case KeyCode.BackQuote: k = Key.Backquote; break;
                    case KeyCode.LeftWindows: k = Key.LeftMeta; break;
                    case KeyCode.RightWindows: k = Key.RightMeta; break;
                    default:
                        Key parsed;
                        if (System.Enum.TryParse(kc.ToString(), out parsed)) k = parsed;
                        break;
                }
            }
            keyMap[kc] = k;
            return k;
        }

        static ButtonControl Control(KeyCode kc)
        {
            if (kc >= KeyCode.Mouse0 && kc <= KeyCode.Mouse6)
            {
                Mouse m = Mouse.current;
                if (m == null) return null;
                switch (kc)
                {
                    case KeyCode.Mouse0: return m.leftButton;
                    case KeyCode.Mouse1: return m.rightButton;
                    case KeyCode.Mouse2: return m.middleButton;
                    case KeyCode.Mouse3: return m.backButton;
                    case KeyCode.Mouse4: return m.forwardButton;
                    default: return null;
                }
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return null;
            Key key = ToKey(kc);
            if (key == Key.None) return null;
            return kb[key];
        }

        public static bool KeyHeld(KeyCode k) { ButtonControl c = Control(k); return c != null && c.isPressed; }
        public static bool KeyPressed(KeyCode k) { ButtonControl c = Control(k); return c != null && c.wasPressedThisFrame; }

        // Scaled by 0.1 so that the numbers match the legacy "Mouse X/Y" axes.
        public static Vector2 MouseDelta()
        {
            Mouse m = Mouse.current;
            return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
        }

        public static float Scroll()
        {
            Mouse m = Mouse.current;
            return m == null ? 0f : Mathf.Clamp(m.scroll.ReadValue().y, -1f, 1f);
        }
#else
        public static bool KeyHeld(KeyCode k) { return k != KeyCode.None && Input.GetKey(k); }
        public static bool KeyPressed(KeyCode k) { return k != KeyCode.None && Input.GetKeyDown(k); }

        public static Vector2 MouseDelta()
        {
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }

        public static float Scroll() { return Mathf.Clamp(Input.mouseScrollDelta.y, -1f, 1f); }
#endif

        public static bool Pressed(KeyCode k) { return KeyPressed(k); }
        public static bool MouseHeld(int b) { return KeyHeld(KeyCode.Mouse0 + b); }
        public static bool MousePressed(int b) { return KeyPressed(KeyCode.Mouse0 + b); }

        /// <summary>Fill the shared input state from keyboard and mouse (using the player's key bindings).</summary>
        public static void Read(PlayerInputState s)
        {
            float x = (KeyBindings.Held(GameAction.MoveRight) ? 1f : 0f) - (KeyBindings.Held(GameAction.MoveLeft) ? 1f : 0f);
            float y = (KeyBindings.Held(GameAction.MoveForward) ? 1f : 0f) - (KeyBindings.Held(GameAction.MoveBack) ? 1f : 0f);
            s.move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            s.look = Cursor.lockState == CursorLockMode.Locked ? MouseDelta() : Vector2.zero;

            s.fireHeld = KeyBindings.Held(GameAction.Fire);
            s.firePressed = KeyBindings.Pressed(GameAction.Fire);
            s.aimHeld = KeyBindings.Held(GameAction.Aim);

            s.reloadPressed = KeyBindings.Pressed(GameAction.Reload);
            s.jumpPressed = KeyBindings.Pressed(GameAction.Jump);
            s.dashPressed = KeyBindings.Pressed(GameAction.Dash);
            s.sprintHeld = KeyBindings.Held(GameAction.Sprint);
            s.crouchHeld = KeyBindings.Held(GameAction.Crouch) || (!KeyBindings.IsBound(KeyCode.C) && KeyHeld(KeyCode.C));
            s.grenadePressed = KeyBindings.Pressed(GameAction.Grenade);
            s.meleePressed = KeyBindings.Pressed(GameAction.Melee);
            s.flashlightPressed = KeyBindings.Pressed(GameAction.Flashlight);
            s.medkitPressed = KeyBindings.Pressed(GameAction.Medkit);

            s.weaponSelect = KeyPressed(KeyCode.Alpha1) ? 0 : (KeyPressed(KeyCode.Alpha2) ? 1 : (KeyPressed(KeyCode.Alpha3) ? 2 : -1));
            float sc = Scroll();
            s.weaponCycle = sc > 0.1f ? 1 : (sc < -0.1f ? -1 : 0);
        }
    }
}
