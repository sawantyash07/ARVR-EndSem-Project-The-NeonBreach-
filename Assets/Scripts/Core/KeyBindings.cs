using UnityEngine;

namespace NeonBreach
{
    public enum GameAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Sprint, Crouch, Dash,
        Fire, Aim, Reload, Grenade, Melee, Flashlight, Medkit
    }

    /// <summary>
    /// Rebindable keyboard/mouse controls. Bindings are stored as UnityEngine.KeyCode values (mouse buttons are
    /// KeyCode.Mouse0..6) and translated for whichever input backend is active (see DesktopInput).
    /// </summary>
    public static class KeyBindings
    {
        public static readonly GameAction[] All = (GameAction[])System.Enum.GetValues(typeof(GameAction));

        static readonly KeyCode[] defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.Q,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R, KeyCode.G, KeyCode.V, KeyCode.F, KeyCode.H
        };

        static KeyCode[] current = (KeyCode[])defaults.Clone();

        public static KeyCode Get(GameAction a) { return current[(int)a]; }

        public static void Set(GameAction a, KeyCode k)
        {
            // a key can only do one thing: swap with the action that currently owns it
            for (int i = 0; i < current.Length; i++)
                if (current[i] == k && i != (int)a) current[i] = current[(int)a];
            current[(int)a] = k;
        }

        public static bool IsBound(KeyCode k)
        {
            for (int i = 0; i < current.Length; i++) if (current[i] == k) return true;
            return false;
        }

        public static void ResetDefaults() { current = (KeyCode[])defaults.Clone(); }

        public static void Load()
        {
            for (int i = 0; i < current.Length; i++)
                current[i] = (KeyCode)PlayerPrefs.GetInt("nb_bind_" + i, (int)defaults[i]);
        }

        public static void Save()
        {
            for (int i = 0; i < current.Length; i++) PlayerPrefs.SetInt("nb_bind_" + i, (int)current[i]);
        }

        public static bool Held(GameAction a) { return DesktopInput.KeyHeld(current[(int)a]); }
        public static bool Pressed(GameAction a) { return DesktopInput.KeyPressed(current[(int)a]); }

        public static string ActionName(GameAction a)
        {
            switch (a)
            {
                case GameAction.MoveForward: return Loc.T("Move forward");
                case GameAction.MoveBack: return Loc.T("Move back");
                case GameAction.MoveLeft: return Loc.T("Move left");
                case GameAction.MoveRight: return Loc.T("Move right");
                case GameAction.Jump: return Loc.T("Jump");
                case GameAction.Sprint: return Loc.T("Sprint");
                case GameAction.Crouch: return Loc.T("Crouch / slide");
                case GameAction.Dash: return Loc.T("Dash");
                case GameAction.Fire: return Loc.T("Fire");
                case GameAction.Aim: return Loc.T("Aim down sights");
                case GameAction.Reload: return Loc.T("Reload");
                case GameAction.Grenade: return Loc.T("Throw grenade");
                case GameAction.Melee: return Loc.T("Melee");
                case GameAction.Flashlight: return Loc.T("Flashlight");
                default: return Loc.T("Use medkit");
            }
        }

        /// <summary>Short human-readable label for a key, e.g. "LMB", "SHIFT", "R".</summary>
        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.Mouse3: return "MOUSE4";
                case KeyCode.Mouse4: return "MOUSE5";
                case KeyCode.LeftShift: return "SHIFT";
                case KeyCode.RightShift: return "R-SHIFT";
                case KeyCode.LeftControl: return "CTRL";
                case KeyCode.RightControl: return "R-CTRL";
                case KeyCode.LeftAlt: return "ALT";
                case KeyCode.Space: return "SPACE";
                case KeyCode.Return: return "ENTER";
                case KeyCode.Tab: return "TAB";
                case KeyCode.CapsLock: return "CAPS";
                case KeyCode.BackQuote: return "`";
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "NUM" + s.Substring(6);
            return s.ToUpperInvariant();
        }

        public static string KeyFor(GameAction a) { return KeyName(Get(a)); }
    }
}
