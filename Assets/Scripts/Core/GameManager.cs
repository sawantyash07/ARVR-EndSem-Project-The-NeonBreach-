using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    public enum GameState { Menu, Playing, Paused, Shop, GameOver }

    /// <summary>
    /// Central game state machine: main menu -> (tutorial) -> playing <-> shop / paused -> game over.
    /// Owns score, credits, combo multiplier, statistics, the high score, the players (1 or 2 for split-screen
    /// co-op) and records every finished run in the leaderboard, lifetime stats and analytics.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerController Player;           // player 1 (keyboard/mouse or VR)
        public WaveManager Waves;
        public HUD Hud;
        public Vector3 playerSpawn = new Vector3(0f, 0.05f, 0f);

        public readonly List<PlayerController> Players = new List<PlayerController>();
        public PlayerController Player2 { get; private set; }

        public GameState State { get; private set; }
        public bool Playing { get { return State == GameState.Playing; } }
        public bool InTutorial { get; private set; }
        public bool CoOpRun { get; private set; }
        public Tutorial Tutorial { get; private set; }
        /// <summary>Switches between the shooter and the Archery / Parachute mini-games (Assets/Scripts/Modes).</summary>
        public ModeManager Modes { get; private set; }

        public int Score { get; private set; }
        public int Credits { get; private set; }
        public int CreditsSpent { get; private set; }
        public int Kills { get; private set; }
        public int Headshots { get; private set; }
        public int Combo { get; private set; }
        public int HighScore { get; private set; }
        public int BestWave;
        public int ShotsFired;
        public int ShotsHit;
        public float PlayTime { get; private set; }
        public float ComboTimeLeft { get { return Mathf.Max(0f, comboTimer); } }
        public int Multiplier { get { return 1 + Mathf.Min(Combo / 4, 4); } }
        public float Accuracy { get { return ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f; } }
        public int LastRank { get; private set; }
        public bool VrActive { get { return Player != null && Player.VrOn; } }

        float comboTimer;
        float inputCheck;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Enemy.All.Clear();
            Layers.SetupCollisionMatrix();
            GameSettings.Load();
            HighScore = PlayerPrefs.GetInt("nb_high", 0);

            if (Player == null) Player = FindAnyObjectByType<PlayerController>();
            if (Waves == null) Waves = GetComponent<WaveManager>();
            if (Hud == null) Hud = GetComponent<HUD>();
            Tutorial = GetComponent<Tutorial>();
            if (Tutorial == null) Tutorial = gameObject.AddComponent<Tutorial>();
            if (GetComponent<VRMenuDriver>() == null) gameObject.AddComponent<VRMenuDriver>();
            Modes = GetComponent<ModeManager>();
            if (Modes == null) Modes = gameObject.AddComponent<ModeManager>();
            Players.Clear();
            if (Player != null) { Player.index = 0; Players.Add(Player); }
        }

        void Start()
        {
            Sfx.Instance.StartMusic();
            ArenaManager.Prepare(GameSettings.Arena, false);
            PostFX.AttachTo(Player != null ? Player.cam : null);
            EnterMenu();
        }

        void Update()
        {
            // global hotkeys
            bool vrMenu = Player != null && Player.vr != null && Player.vr.Active && Player.vr.MenuPressed;
            bool pauseRequested = DesktopInput.Pressed(KeyCode.Escape) || GamepadInput.AnyStartPressed() || vrMenu;
            if (AutoTest.Active) pauseRequested = false;   // the smoke test pauses/resumes on its own
            if (pauseRequested)
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
                else if (State == GameState.Shop) CloseShop();
            }
            if (DesktopInput.Pressed(KeyCode.F1) && Hud != null) Hud.showHelp = !Hud.showHelp;
            if (DesktopInput.Pressed(KeyCode.F3)) { GameSettings.ShowFps = !GameSettings.ShowFps; GameSettings.Save(); }
            if (DesktopInput.Pressed(KeyCode.F4) && Hud != null)
            {
                Hud.showPerf = !Hud.showPerf;
                if (Hud.showPerf) Perf.StartRecording(); else Perf.StopRecording();
            }
            if (DesktopInput.Pressed(KeyCode.F12))
            {
                string file = "NeonBreach_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                ScreenCapture.CaptureScreenshot(file);
                if (Hud != null) Hud.Toast(Loc.T("Screenshot saved") + ": " + file);
            }

            if (State == GameState.Playing)
            {
                // clicking back into the window (after alt-tab) re-captures the mouse
                if (!VrActive && Cursor.lockState != CursorLockMode.Locked && DesktopInput.MousePressed(0)) SetCursor(true);

                if (!InTutorial) PlayTime += Time.deltaTime;
                if (comboTimer > 0f)
                {
                    comboTimer -= Time.deltaTime;
                    if (comboTimer <= 0f) Combo = 0;
                }
            }

            // gamepads can be plugged in at any time
            inputCheck -= Time.unscaledDeltaTime;
            if (inputCheck <= 0f) { inputCheck = 1f; AssignInputs(); }
        }

        // ---------------------------------------------------------------------------------- players

        void SetupPlayers(bool coop)
        {
            Players.Clear();
            Players.Add(Player);
            if (coop)
            {
                if (Player2 == null)
                {
                    Player2 = PlayerFactory.Create(playerSpawn + new Vector3(1.5f, 0f, 0f), 1);
                    PostFX.AttachTo(Player2.cam);
                }
                Player2.gameObject.SetActive(true);
                Players.Add(Player2);
            }
            else if (Player2 != null) Player2.gameObject.SetActive(false);
            CoOpRun = coop;
            AssignInputs();
            LayoutCameras();
        }

        void AssignInputs()
        {
            if (Player == null) return;
            int pads = GamepadInput.Count;
            Player.useKeyboardMouse = true;
            if (CoOpRun && Player2 != null)
            {
                Player2.useKeyboardMouse = false;
                Player2.gamepadIndex = pads >= 1 ? 0 : -1;
                Player.gamepadIndex = pads >= 2 ? 1 : -1;
            }
            else Player.gamepadIndex = pads >= 1 ? 0 : -1;
        }

        void LayoutCameras()
        {
            if (Player == null || Player.cam == null) return;
            bool split = CoOpRun && Player2 != null && Player2.gameObject.activeSelf;
            if (!split)
            {
                Player.cam.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            if (VrActive)
            {
                // asymmetric co-op: P1 in the headset, P2 on the monitor (P2's camera is not stereo)
                Player.cam.rect = new Rect(0f, 0f, 1f, 1f);
                Player2.cam.rect = new Rect(0f, 0f, 1f, 1f);
                Player2.cam.stereoTargetEye = StereoTargetEyeMask.None;
                Player2.cam.depth = 5f;
            }
            else
            {
                Player.cam.rect = new Rect(0f, 0f, 0.5f, 1f);
                Player2.cam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
                Player2.cam.stereoTargetEye = StereoTargetEyeMask.None;
                Player2.cam.depth = Player.cam.depth + 1f;
            }
        }

        public PlayerController NearestAlivePlayer(Vector3 pos)
        {
            PlayerController best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerController p = Players[i];
                if (p == null || p.IsDead || !p.gameObject.activeInHierarchy) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public bool AnyPlayerAlive
        {
            get
            {
                for (int i = 0; i < Players.Count; i++) if (Players[i] != null && !Players[i].IsDead) return true;
                return false;
            }
        }

        public void RevivePlayers()
        {
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerController p = Players[i];
                if (p == null || !p.IsDead) continue;
                PlayerController buddy = NearestAlivePlayer(playerSpawn);
                Vector3 pos = buddy != null ? buddy.transform.position + buddy.transform.right * 1.5f : playerSpawn;
                p.Revive(pos + Vector3.up * 0.1f);
                if (Hud != null) Hud.Toast(Loc.T("PLAYER") + " " + (p.index + 1) + " " + Loc.T("is back in the fight!"));
            }
        }

        // ---------------------------------------------------------------------------------- state changes

        void ResetRun()
        {
            Score = 0; Credits = 0; CreditsSpent = 0; Kills = 0; Headshots = 0; Combo = 0; comboTimer = 0f;
            ShotsFired = 0; ShotsHit = 0; BestWave = 0; PlayTime = 0f; LastRank = 0;
            Upgrades.Reset();
            PowerUps.Reset();
            ClearLooseObjects();
            if (Hud != null) Hud.ResetUi();
        }

        void PlacePlayers()
        {
            for (int i = 0; i < Players.Count; i++)
            {
                Vector3 off = i == 0 ? Vector3.zero : new Vector3(1.8f, 0f, 0f);
                Players[i].ResetPlayer(playerSpawn + off);
            }
        }

        public void EnterMenu()
        {
            if (Modes != null) Modes.ReturnToShooter();
            State = GameState.Menu;
            InTutorial = false;
            Time.timeScale = 1f;
            if (Tutorial != null) Tutorial.Stop();
            if (Waves != null) { Waves.StopRun(); Waves.ClearEnemies(); }
            PowerUps.Reset();
            ClearLooseObjects();
            SetupPlayers(false);
            if (Player != null) Player.ResetPlayer(playerSpawn);
            if (Hud != null) Hud.ResetUi();
            Haptics.StopAll();
            SetCursor(false);
        }

        /// <summary>Start a real mission with the settings chosen in the menu.</summary>
        public void StartGame()
        {
            if (Modes != null) Modes.ReturnToShooter();
            if (Tutorial != null) Tutorial.Stop();
            InTutorial = false;
            ArenaManager.Prepare(GameSettings.Arena, GameSettings.MixedReality && VrActive);
            if (Waves != null) Waves.FindSpawnPoints();
            SetupPlayers(GameSettings.CoOp);
            ResetRun();
            PlacePlayers();
            RunAnalytics.Begin();
            State = GameState.Playing;
            Time.timeScale = 1f;
            SetCursor(!VrActive);
            Waves.Begin();
            Sfx.Play2D(SfxId.Click, 0.8f);
            if (CoOpRun && GamepadInput.Count == 0 && Hud != null)
                Hud.Banner(Loc.T("CO-OP"), Loc.T("Connect a gamepad for player 2"));
        }

        public void StartTutorial()
        {
            if (Modes != null) Modes.ReturnToShooter();
            ArenaManager.Prepare(GameSettings.Arena, GameSettings.MixedReality && VrActive);
            if (Waves != null) { Waves.StopRun(); Waves.ClearEnemies(); Waves.FindSpawnPoints(); }
            SetupPlayers(false);
            ResetRun();
            PlacePlayers();
            InTutorial = true;
            State = GameState.Playing;
            Time.timeScale = 1f;
            SetCursor(!VrActive);
            Tutorial.Begin(Player);
        }

        /// <summary>Called by the tutorial when the last step is done (or skipped).</summary>
        public void FinishTutorial(bool startMission)
        {
            GameSettings.TutorialDone = true;
            GameSettings.Save();
            if (Tutorial != null) Tutorial.Stop();
            InTutorial = false;
            if (startMission) StartGame();
            else EnterMenu();
        }

        /// <summary>Menu "START": the very first time, play the tutorial first.</summary>
        public void StartFromMenu()
        {
            if (!GameSettings.TutorialDone && !GameSettings.CoOp) StartTutorial();
            else StartGame();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            Haptics.StopAll();
            SetCursor(false);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            SetCursor(!VrActive);
            if (Hud != null) Hud.CloseOverlays();
        }

        public void OpenShop()
        {
            if (State != GameState.Playing) return;
            State = GameState.Shop;
            Haptics.StopAll();
            SetCursor(false);
            Sfx.Play2D(SfxId.Buy, 0.6f, 0.8f);
        }

        public void CloseShop()
        {
            if (State != GameState.Shop) return;
            State = GameState.Playing;
            SetCursor(!VrActive);
            Sfx.Play2D(SfxId.Click, 0.8f);
        }

        public void OnPlayerDied(PlayerController p)
        {
            if (!InTutorial) RunAnalytics.OnDeath(p.transform.position, MixedReality.Active ? 100 : ArenaManager.CurrentId);
            if (AnyPlayerAlive)
            {
                if (Hud != null) Hud.Banner(Loc.T("PLAYER") + " " + (p.index + 1) + " " + Loc.T("DOWN"), Loc.T("Survive the wave to bring them back"));
                return;
            }
            GameOver();
        }

        public void GameOver()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            Time.timeScale = 1f;
            Waves.StopRun();
            Haptics.StopAll();
            BestWave = Mathf.Max(BestWave, Waves.Wave);
            RunAnalytics.Finish();

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt("nb_high", HighScore);
                PlayerPrefs.Save();
                if (Hud != null) Hud.newHighScore = true;
            }

            RunRecord r = new RunRecord();
            r.name = (string.IsNullOrEmpty(GameSettings.PlayerName) ? "PLAYER" : GameSettings.PlayerName) + (CoOpRun ? " +P2" : "");
            r.score = Score;
            r.wave = Mathf.Max(1, BestWave);
            r.kills = Kills;
            r.headshots = Headshots;
            r.accuracy = Accuracy;
            r.time = PlayTime;
            r.difficulty = (int)GameSettings.Difficulty;
            r.arena = ArenaManager.CurrentId;
            r.coop = CoOpRun;
            r.date = System.DateTime.Now.ToString("yyyy-MM-dd");
            LastRank = Leaderboard.Submit(r);

            LifetimeData d = LifetimeStats.Data;
            d.games++;
            d.kills += Kills;
            d.headshots += Headshots;
            d.shotsFired += ShotsFired;
            d.shotsHit += ShotsHit;
            d.bestWave = Mathf.Max(d.bestWave, BestWave);
            d.bestScore = Mathf.Max(d.bestScore, Score);
            d.timePlayed += PlayTime;
            d.deaths++;
            d.creditsSpent += CreditsSpent;
            for (int i = 0; i < d.killsByType.Length && i < RunAnalytics.KillsByType.Length; i++) d.killsByType[i] += RunAnalytics.KillsByType[i];
            d.bossKills = d.killsByType[(int)EnemyType.Boss];
            LifetimeStats.Save();

            SetCursor(false);
            if (Hud != null) Hud.OnGameOver();
        }

        // ---------------------------------------------------------------------------------- mini-game modes

        /// <summary>ModeManager: start a run of a mini-game (shooter content already torn down). Score resets to 0.</summary>
        public void BeginModeRun()
        {
            if (Tutorial != null) Tutorial.Stop();
            InTutorial = false;
            if (Waves != null) { Waves.StopRun(); Waves.ClearEnemies(); }
            SetupPlayers(false);
            ResetRun();
            State = GameState.Playing;
            Time.timeScale = 1f;
            Haptics.StopAll();
            SetCursor(!VrActive);
        }

        /// <summary>ModeManager: a mini-game run is over (results screen). High scores are kept per mode by ModeManager.</summary>
        public void EndModeRun()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            Time.timeScale = 1f;
            Haptics.StopAll();
            SetCursor(false);
        }

        void ClearLooseObjects()
        {
            Pickup[] picks = FindObjectsByType<Pickup>();
            for (int i = 0; i < picks.Length; i++) Destroy(picks[i].gameObject);
            EnemyProjectile[] shots = FindObjectsByType<EnemyProjectile>();
            for (int i = 0; i < shots.Length; i++) Destroy(shots[i].gameObject);
            Grenade[] gr = FindObjectsByType<Grenade>();
            for (int i = 0; i < gr.Length; i++) Destroy(gr[i].gameObject);
            FX.ClearDecals();
        }

        public static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void QuitGame()
        {
            Debug.Log("[Neon Breach] QuitGame called");
            if (AutoTest.Active) return;
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------------------------- shop

        public bool TryBuyUpgrade(UpgradeId u)
        {
            if (Upgrades.Maxed(u) || Credits < Upgrades.Cost(u)) { Sfx.Play2D(SfxId.Deny, 0.7f); return false; }
            int cost = Upgrades.Cost(u);
            Credits -= cost;
            CreditsSpent += cost;
            Upgrades.Buy(u);
            if (u == UpgradeId.Shield) for (int i = 0; i < Players.Count; i++) Players[i].AddShield(20f);
            Sfx.Play2D(SfxId.Buy, 0.8f);
            return true;
        }

        public bool TryBuyItem(ShopItem item)
        {
            int cost = Upgrades.ItemCost(item);
            if (Credits < cost) { Sfx.Play2D(SfxId.Deny, 0.7f); return false; }
            Credits -= cost;
            CreditsSpent += cost;
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerController p = Players[i];
                if (p == null) continue;
                switch (item)
                {
                    case ShopItem.AmmoRefill: p.weapons.RefillAll(); break;
                    case ShopItem.Medkit: p.AddMedkit(); break;
                    default: p.weapons.AddGrenades(2); break;
                }
            }
            Sfx.Play2D(SfxId.Buy, 0.8f);
            return true;
        }

        // ---------------------------------------------------------------------------------- scoring + events

        public void AddScore(int points)
        {
            Score += points;
            Credits += points;
        }

        public static PlayerController PlayerFromSource(GameObject source)
        {
            if (source == null) return null;
            PlayerController pc = source.GetComponent<PlayerController>();
            if (pc != null) return pc;
            Grenade g = source.GetComponent<Grenade>();
            return g != null ? g.owner : null;
        }

        public void OnEnemyHit(Enemy e, float amount, Vector3 point, bool headshot, bool killed, GameObject source)
        {
            if (!InTutorial) RunAnalytics.OnDealt(amount);
            PlayerController pc = PlayerFromSource(source);
            if (Hud != null)
            {
                Hud.AddHitMarker(pc != null ? pc.index : 0, headshot, killed);
                Hud.AddDamageNumber(point, amount, headshot);
            }
        }

        public void OnShieldHit(Enemy e, GameObject source)
        {
            PlayerController pc = PlayerFromSource(source);
            if (Hud != null) Hud.AddBlockedMarker(pc != null ? pc.index : 0);
        }

        public void OnEnemyKilled(Enemy e, bool headshot, GameObject source)
        {
            Kills++;
            if (headshot) Headshots++;
            Combo++;
            comboTimer = 4.5f;
            PlayerController pc = PlayerFromSource(source);
            if (pc != null) pc.Kills++;
            if (!InTutorial) RunAnalytics.OnKill(e.type);
            int pts = Mathf.RoundToInt(e.score * Multiplier * (headshot ? 1.5f : 1f) * DifficultyInfo.ScoreMultiplier);
            AddScore(pts);
            if (Hud != null) Hud.Toast("+" + pts + (headshot ? "  " + Loc.T("HEADSHOT") : "") + (Multiplier > 1 ? "   x" + Multiplier : ""));
        }

        public void OnPlayerDamaged(PlayerController p, float amount)
        {
            if (!InTutorial) RunAnalytics.OnTaken(amount);
        }

        public void OnPickup(PlayerController p, PickupType t)
        {
            if (Tutorial != null && InTutorial) Tutorial.OnPickup(t);
        }
    }
}
