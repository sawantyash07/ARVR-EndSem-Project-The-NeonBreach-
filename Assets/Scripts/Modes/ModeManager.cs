using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach
{
    public enum GameMode { Shooter, Archery, Parachute }

    /// <summary>
    /// Base class of a mini-game mode. A mode builds its own world from primitives when it begins, owns its own score,
    /// and removes everything it created in <see cref="Teardown"/>. The shooter is not a MiniGame: it is the existing
    /// GameManager / WaveManager flow, which ModeManager hides and restores.
    /// </summary>
    public abstract class MiniGame : MonoBehaviour
    {
        public abstract GameMode Kind { get; }
        public abstract string Title { get; }
        public int Score { get; protected set; }
        public bool NewBest { get; protected set; }
        public bool Running { get; protected set; }

        /// <summary>Build the mode's world and place the player. The GameManager is already in the Playing state.</summary>
        public abstract void Begin(PlayerController p);

        /// <summary>Destroy everything this mode created and give the player back to the shooter controller.</summary>
        public abstract void Teardown();

        /// <summary>Results screen: title, and label/value rows.</summary>
        public abstract string ResultTitle { get; }
        public abstract void ResultRows(List<string> rows);

        /// <summary>One line for the pause menu.</summary>
        public abstract string StatusLine { get; }

        /// <summary>Finish the run: store the per-mode high score and switch the GameManager to its results state.</summary>
        protected void FinishRun()
        {
            Running = false;
            Score = Mathf.Max(0, Score);
            NewBest = ModeManager.SubmitScore(Kind, Score);
            if (GameManager.Instance != null) GameManager.Instance.EndModeRun();
        }
    }

    /// <summary>
    /// Switches between the three game modes: the original arena shooter, Archery and Parachute.
    /// Switching tears the old mode down (enemies, projectiles, pickups, FX, targets, arrows, rings...), builds the new
    /// one and resets the player. Each mode has its own score and its own PlayerPrefs high-score key.
    /// Hotkeys: F5 shooter, F6 archery, F7 parachute (any time). Menus call <see cref="SwitchTo"/>.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ModeManager : MonoBehaviour
    {
        public static ModeManager Instance { get; private set; }

        public GameMode Current { get; private set; }
        public bool IsShooter { get { return Current == GameMode.Shooter; } }
        public ArcheryMode Archery { get; private set; }
        public ParachuteMode Parachute { get; private set; }
        public WindSystem Wind { get; private set; }

        /// <summary>The running mini-game, or null in shooter mode.</summary>
        public MiniGame Active
        {
            get
            {
                if (Current == GameMode.Archery) return Archery;
                if (Current == GameMode.Parachute) return Parachute;
                return null;
            }
        }

        public static readonly GameMode[] All = { GameMode.Shooter, GameMode.Archery, GameMode.Parachute };

        // shooter state saved while a mini-game runs
        GameObject hiddenArena;
        Vector3 savedSpawn;
        bool envSaved;
        bool fog;
        FogMode fogMode;
        float fogDensity, fogStart, fogEnd;
        Color fogColor, ambient;
        AmbientMode ambientMode;
        Material skybox;
        CameraClearFlags camClear;
        Color camBg;
        float camNear, camFar;
        bool switching;

        // ------------------------------------------------------------------------------------ high scores

        public static string HighKey(GameMode m)
        {
            return m == GameMode.Shooter ? "nb_high" : "nb_high_" + m.ToString().ToLowerInvariant();
        }

        public static int Best(GameMode m) { return PlayerPrefs.GetInt(HighKey(m), 0); }

        /// <summary>Store a finished run's score; returns true when it is a new best for that mode.</summary>
        public static bool SubmitScore(GameMode m, int score)
        {
            if (score <= Best(m)) return false;
            PlayerPrefs.SetInt(HighKey(m), score);
            PlayerPrefs.Save();
            return true;
        }

        public static string Name(GameMode m)
        {
            switch (m)
            {
                case GameMode.Archery: return Loc.T("ARCHERY");
                case GameMode.Parachute: return Loc.T("PARACHUTE");
                default: return Loc.T("SHOOTER");
            }
        }

        public static string Description(GameMode m)
        {
            switch (m)
            {
                case GameMode.Archery: return Loc.T("3 rounds x 10 arrows at 10-40 m. Wind changes every round.");
                case GameMode.Parachute: return Loc.T("Jump from 3000 m, fly through rings, land on the pad.");
                default: return Loc.T("Survive endless waves in the neon arena.");
            }
        }

        public static string HotKey(GameMode m) { return m == GameMode.Archery ? "F6" : (m == GameMode.Parachute ? "F7" : "F5"); }

        // ------------------------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            Current = GameMode.Shooter;
            Wind = GetComponent<WindSystem>();
            if (Wind == null) Wind = gameObject.AddComponent<WindSystem>();
            Archery = GetComponent<ArcheryMode>();
            if (Archery == null) Archery = gameObject.AddComponent<ArcheryMode>();
            Parachute = GetComponent<ParachuteMode>();
            if (Parachute == null) Parachute = gameObject.AddComponent<ParachuteMode>();
        }

        void Update()
        {
            if (AutoTest.Active) return;   // the smoke test drives the shooter only
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (DesktopInput.Pressed(KeyCode.F5)) HotSwitch(gm, GameMode.Shooter);
            else if (DesktopInput.Pressed(KeyCode.F6)) HotSwitch(gm, GameMode.Archery);
            else if (DesktopInput.Pressed(KeyCode.F7)) HotSwitch(gm, GameMode.Parachute);
        }

        void HotSwitch(GameManager gm, GameMode m)
        {
            // never throw away a run in progress by pressing the key of the mode you are already playing
            if (m == Current && gm.State == GameState.Playing)
            {
                if (gm.Hud != null) gm.Hud.Toast(Name(m) + ": " + Loc.T("already running (pause menu: RESTART)"));
                return;
            }
            SwitchTo(m);
        }

        /// <summary>Start (or restart) a mode from any state: menu, playing, paused or results.</summary>
        public void SwitchTo(GameMode m)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || switching) return;
            switching = true;
            try
            {
                Time.timeScale = 1f;
                if (gm.Hud != null) gm.Hud.CloseOverlays();
                if (m == GameMode.Shooter)
                {
                    // StartFromMenu -> StartGame / StartTutorial call ReturnToShooter() first
                    gm.StartFromMenu();
                    if (gm.Hud != null) gm.Hud.Banner(Name(m), Loc.T("Survive the waves"));
                    return;
                }

                TeardownActive();
                if (Current == GameMode.Shooter) HideShooter(gm);
                Current = m;
                gm.BeginModeRun();
                MiniGame game = Active;
                game.Begin(gm.Player);
                Sfx.Play2D(SfxId.WaveStart, 0.6f);
            }
            finally
            {
                switching = false;
            }
        }

        /// <summary>Restart the current mode (pause menu / results screen).</summary>
        public void Restart()
        {
            if (IsShooter) GameManager.Instance.StartGame();
            else SwitchTo(Current);
        }

        /// <summary>Called by GameManager before any shooter state change (menu, start, tutorial): restore the shooter.</summary>
        public void ReturnToShooter()
        {
            if (Current == GameMode.Shooter) return;
            TeardownActive();
            RestoreShooter(GameManager.Instance);
            Current = GameMode.Shooter;
        }

        void TeardownActive()
        {
            MiniGame g = Active;
            if (g != null) g.Teardown();
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Player != null) ReleasePlayer(gm.Player);
            if (Wind != null) Wind.Calm();
            ComfortVignette.ExternalMotion = -1f;
        }

        // ------------------------------------------------------------------------------------ shooter hide / restore

        void HideShooter(GameManager gm)
        {
            if (gm.Waves != null) { gm.Waves.StopRun(); gm.Waves.ClearEnemies(); }
            if (MixedReality.Active) MixedReality.Disable();
            ArenaInfo info = ArenaManager.Current != null ? ArenaManager.Current : FindAnyObjectByType<ArenaInfo>();
            hiddenArena = info != null ? info.gameObject : null;
            if (hiddenArena != null) hiddenArena.SetActive(false);
            savedSpawn = gm.playerSpawn;
            SaveEnvironment(gm.Player != null ? gm.Player.cam : null);
        }

        void RestoreShooter(GameManager gm)
        {
            if (hiddenArena != null) hiddenArena.SetActive(true);
            hiddenArena = null;
            if (gm != null)
            {
                gm.playerSpawn = savedSpawn;
                RestoreEnvironment(gm.Player != null ? gm.Player.cam : null);
            }
        }

        void SaveEnvironment(Camera cam)
        {
            envSaved = true;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            fogColor = RenderSettings.fogColor;
            ambientMode = RenderSettings.ambientMode;
            ambient = RenderSettings.ambientLight;
            skybox = RenderSettings.skybox;
            if (cam != null)
            {
                camClear = cam.clearFlags;
                camBg = cam.backgroundColor;
                camNear = cam.nearClipPlane;
                camFar = cam.farClipPlane;
            }
        }

        void RestoreEnvironment(Camera cam)
        {
            if (!envSaved) return;
            envSaved = false;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.skybox = skybox;
            if (cam != null)
            {
                cam.clearFlags = camClear;
                cam.backgroundColor = camBg;
                cam.nearClipPlane = camNear;
                cam.farClipPlane = camFar;
            }
        }

        // ------------------------------------------------------------------------------------ player helpers

        /// <summary>Prepare the player for a mini-game: reset at a position, put the guns away.</summary>
        public static void TakePlayer(PlayerController p, Vector3 position, float yawDeg)
        {
            p.ClearModeHooks();
            p.ResetPlayer(position);
            p.SetLook(yawDeg, 0f);
            if (p.weapons != null)
            {
                p.weapons.enabled = false;
                if (p.weapons.holder != null) p.weapons.holder.gameObject.SetActive(false);
            }
            if (p.vr != null) p.vr.WristOverride = null;
        }

        /// <summary>Give the player back to the shooter: hooks off, guns back.</summary>
        public static void ReleasePlayer(PlayerController p)
        {
            p.ClearModeHooks();
            if (p.Body != null && !p.Body.enabled) p.Body.enabled = true;
            if (p.weapons != null)
            {
                p.weapons.enabled = true;
                if (p.weapons.holder != null) p.weapons.holder.gameObject.SetActive(true);
            }
            if (p.vr != null) p.vr.WristOverride = null;
            Util.SetFov(p.cam, GameSettings.Fov);
        }

        /// <summary>A world-space text label (distance signs etc.) using Unity's built-in font - no font assets.</summary>
        public static TextMesh Label(Transform parent, string text, Vector3 pos, float yRot, float charSize, Color color)
        {
            GameObject g = new GameObject("Label_" + text);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            TextMesh tm = g.AddComponent<TextMesh>();
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = f;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (f != null) mr.sharedMaterial = f.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }
    }
}
