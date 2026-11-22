using UnityEngine;

namespace NeonBreach.RatShooter
{
    public enum RatState { Title, Countdown, Playing, Paused, LevelComplete, GameOver, Victory }

    /// <summary>
    /// Rat Shooter game flow: title screen (with mice wandering behind it) -> countdown -> playing (timer, ammo,
    /// score, combo) -> level complete with bonuses and star rating -> next, harder level ... -> victory. Running out
    /// of time or bullets ends the run (game over). Pause at any time; every end / pause screen can return to the
    /// Neon Breach main menu. High score stored in PlayerPrefs ("nb_high_rats").
    /// </summary>
    public class RatShooterGame : MonoBehaviour
    {
        public const string SceneName = "RatShooter";
        public const string MenuSceneName = "NeonBreach";
        public const string HighScoreKey = "nb_high_rats";
        const string DifficultyKey = "rs_difficulty";

        public static RatShooterGame Instance { get; private set; }
        public static int BestScore { get { return PlayerPrefs.GetInt(HighScoreKey, 0); } }

        [Header("References (set by the scene builder)")]
        public RatShooterPlayer player;
        public CartoonGun gun;
        public MouseSpawner spawner;
        public RatHUD hud;

        [Header("Levels (progressive difficulty)")]
        public RatLevel[] levels = RatLevel.Defaults();
        public float countdownTime = 3.5f;
        [Tooltip("Points per second left on the clock when a level is cleared.")]
        public int timeBonusPerSecond = 10;
        [Tooltip("Points per unused round when a level is cleared.")]
        public int ammoBonusPerRound = 15;

        public RatState State { get; private set; }
        public RatState StateBeforePause { get; private set; }
        public float StateTime { get; private set; }
        public int LevelIndex { get; private set; }
        public RatLevel Level { get { return levels[Mathf.Clamp(LevelIndex, 0, levels.Length - 1)]; } }
        public bool IsLastLevel { get { return LevelIndex >= levels.Length - 1; } }
        public RatDifficulty Difficulty { get; private set; }

        public int Score { get; private set; }
        public int LevelStartScore { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Kills { get; private set; }
        public int LevelKills { get; private set; }
        public int Headshots { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public int LevelShots { get; private set; }
        public int LevelHits { get; private set; }
        public float TimeLeft { get; private set; }
        public float CountdownLeft { get; private set; }
        public int MiceRemaining { get { return Mathf.Max(0, Level.mice - LevelKills); } }
        public float Accuracy { get { return ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f; } }
        public float LevelAccuracy { get { return LevelShots > 0 ? (float)LevelHits / LevelShots : 0f; } }
        public bool NewHighScore { get; private set; }
        public string GameOverReason { get; private set; }

        // level summary
        public int SummaryTimeBonus { get; private set; }
        public int SummaryAmmoBonus { get; private set; }
        public int SummaryAccuracyBonus { get; private set; }
        public int SummaryStars { get; private set; }
        public int SummaryLevelScore { get; private set; }

        float completeAt = -1f;
        float outOfAmmoAt = -1f;
        int lastCountdownBeep;
        int lastWarnSecond;
        int missStreak;
        float savedShadowDistance = -1f;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            GameSettings.Load();
            AudioListener.volume = SceneFader.Busy ? AudioListener.volume : GameSettings.MasterVolume;
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
            Layers.SetupCollisionMatrix();
            Difficulty = (RatDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 1), 0, 2);
            if (levels == null || levels.Length == 0) levels = RatLevel.Defaults();
            // the farm needs longer shadows than the neon arena; restored when leaving the scene
            savedShadowDistance = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = Mathf.Max(savedShadowDistance, 55f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (savedShadowDistance >= 0f) QualitySettings.shadowDistance = savedShadowDistance;
            Time.timeScale = 1f;
        }

        void Start()
        {
            if (gun != null) { gun.onShot = OnShot; gun.onDryFire = OnDryFire; }
            Debug.Log("[Rat Shooter] Scene started.");
            EnterTitle(true);
        }

        void SetState(RatState s)
        {
            State = s;
            StateTime = 0f;
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ------------------------------------------------------------------ flow

        public void EnterTitle(bool instant = false)
        {
            Time.timeScale = 1f;
            SetState(RatState.Title);
            if (player != null) { player.SetTitleMode(true, instant); player.ControlEnabled = false; }
            if (gun != null) { gun.InputEnabled = false; gun.Lowered = 1f; }
            if (spawner != null) spawner.BeginAttract();
            if (RatAudio.Instance != null) { RatAudio.Instance.DuckMusic(1f); RatAudio.Instance.SetPaused(false); }
            SetCursor(false);
        }

        public void SetDifficulty(RatDifficulty d)
        {
            Difficulty = d;
            PlayerPrefs.SetInt(DifficultyKey, (int)d);
            PlayerPrefs.Save();
        }

        public void StartGame()
        {
            Score = 0;
            Kills = 0;
            Headshots = 0;
            ShotsFired = 0;
            ShotsHit = 0;
            BestCombo = 0;
            NewHighScore = false;
            BeginLevel(0);
        }

        public void BeginLevel(int index)
        {
            Time.timeScale = 1f;
            LevelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
            LevelStartScore = Score;
            LevelKills = 0;
            LevelShots = 0;
            LevelHits = 0;
            Combo = 0;
            missStreak = 0;
            completeAt = -1f;
            outOfAmmoAt = -1f;
            lastWarnSecond = -1;
            RatLevel lvl = Level;
            TimeLeft = lvl.timeLimit * RatDifficultyInfo.Time(Difficulty);
            int total = Mathf.CeilToInt((lvl.mice * 2 + lvl.extraAmmo) * RatDifficultyInfo.Ammo(Difficulty));
            if (gun != null)
            {
                gun.ResetAmmo(Mathf.Max(0, total - gun.magazineSize), false);
                gun.InputEnabled = false;
                gun.Lowered = 0f;
            }
            if (spawner != null) spawner.BeginLevel(lvl, RatDifficultyInfo.Speed(Difficulty));
            if (player != null) { player.SetTitleMode(false); player.ControlEnabled = true; }
            if (hud != null) hud.OnLevelStart();
            if (RatAudio.Instance != null) { RatAudio.Instance.DuckMusic(0.8f); RatAudio.Instance.SetPaused(false); }
            CountdownLeft = countdownTime;
            lastCountdownBeep = 4;
            RatAudio.Play2D(RatSound.Whoosh, 0.6f);
            SetState(RatState.Countdown);
            SetCursor(true);
        }

        public void NextLevel()
        {
            if (IsLastLevel) { SetState(RatState.Victory); return; }
            BeginLevel(LevelIndex + 1);
        }

        public void RetryLevel()
        {
            Score = LevelStartScore;
            BeginLevel(LevelIndex);
        }

        public void Pause()
        {
            if (State != RatState.Playing && State != RatState.Countdown) return;
            StateBeforePause = State;
            State = RatState.Paused;
            Time.timeScale = 0f;
            if (player != null) player.ControlEnabled = false;
            if (gun != null) gun.InputEnabled = false;
            if (RatAudio.Instance != null) { RatAudio.Instance.SetPaused(true); RatAudio.Instance.DuckMusic(0.35f); }
            RatAudio.Play2D(RatSound.Click, 0.7f);
            SetCursor(false);
        }

        public void Resume()
        {
            if (State != RatState.Paused) return;
            State = StateBeforePause;
            Time.timeScale = 1f;
            if (player != null) player.ControlEnabled = true;
            if (gun != null) gun.InputEnabled = State == RatState.Playing;
            if (RatAudio.Instance != null) { RatAudio.Instance.SetPaused(false); RatAudio.Instance.DuckMusic(0.8f); }
            SetCursor(true);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SaveHighScore();
            GameSettings.Save();
            SetCursor(false);
            SceneFader.Load(MenuSceneName);
        }

        void GameOver(string reason)
        {
            GameOverReason = reason;
            SetState(RatState.GameOver);
            if (gun != null) gun.InputEnabled = false;
            if (player != null) player.ControlEnabled = false;
            if (spawner != null) spawner.Stop();
            SaveHighScore();
            if (RatAudio.Instance != null) RatAudio.Instance.DuckMusic(0.25f);
            RatAudio.Play2D(RatSound.GameOver, 0.9f);
            SetCursor(false);
        }

        void CompleteLevel()
        {
            SummaryTimeBonus = Mathf.RoundToInt(Mathf.Max(0f, TimeLeft)) * timeBonusPerSecond;
            SummaryAmmoBonus = (gun != null ? gun.TotalAmmo : 0) * ammoBonusPerRound;
            SummaryAccuracyBonus = Mathf.RoundToInt(LevelAccuracy * 100f) * 5;
            float sm = RatDifficultyInfo.Score(Difficulty);
            int bonus = Mathf.RoundToInt((SummaryTimeBonus + SummaryAmmoBonus + SummaryAccuracyBonus) * sm);
            Score += bonus;
            SummaryLevelScore = Score - LevelStartScore;
            float limit = Level.timeLimit * RatDifficultyInfo.Time(Difficulty);
            SummaryStars = 1 + (LevelAccuracy >= 0.6f ? 1 : 0) + (TimeLeft >= limit * 0.35f ? 1 : 0);
            if (gun != null) gun.InputEnabled = false;
            if (player != null) player.ControlEnabled = false;
            if (spawner != null) spawner.Stop();
            SaveHighScore();
            SetCursor(false);
            if (IsLastLevel)
            {
                SetState(RatState.Victory);
                if (RatAudio.Instance != null) RatAudio.Instance.DuckMusic(0.3f);
                RatAudio.Play2D(RatSound.Victory, 0.95f);
            }
            else
            {
                SetState(RatState.LevelComplete);
                if (RatAudio.Instance != null) RatAudio.Instance.DuckMusic(0.45f);
                RatAudio.Play2D(RatSound.LevelUp, 0.9f);
            }
        }

        void SaveHighScore()
        {
            if (Score > BestScore)
            {
                PlayerPrefs.SetInt(HighScoreKey, Score);
                PlayerPrefs.Save();
                NewHighScore = true;
            }
        }

        public void Announce(string title, string sub)
        {
            if (hud != null) hud.Banner(title, sub);
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            StateTime += Time.unscaledDeltaTime;

            bool pauseKey = DesktopInput.Pressed(KeyCode.Escape) || DesktopInput.Pressed(KeyCode.P);
            if (pauseKey)
            {
                if (State == RatState.Playing || State == RatState.Countdown) Pause();
                else if (State == RatState.Paused) { if (hud != null && hud.InSubPanel) hud.CloseSubPanel(); else Resume(); }
                else if (State == RatState.Title && hud != null && hud.InSubPanel) hud.CloseSubPanel();
            }
            bool confirm = DesktopInput.Pressed(KeyCode.Return) || DesktopInput.Pressed(KeyCode.KeypadEnter);
            if (confirm && StateTime > 0.6f)
            {
                if (State == RatState.Title && (hud == null || !hud.InSubPanel)) StartGame();
                else if (State == RatState.LevelComplete) NextLevel();
                else if (State == RatState.GameOver) RetryLevel();
            }

            float dt = Time.deltaTime;
            switch (State)
            {
                case RatState.Countdown:
                    CountdownLeft -= dt;
                    int sec = Mathf.CeilToInt(CountdownLeft);
                    if (sec < lastCountdownBeep && sec >= 1 && sec <= 3) { lastCountdownBeep = sec; RatAudio.Play2D(RatSound.Beep, 0.8f); }
                    if (CountdownLeft <= 0f)
                    {
                        SetState(RatState.Playing);
                        if (gun != null) gun.InputEnabled = true;
                        RatAudio.Play2D(RatSound.Go, 0.8f);
                    }
                    RelockCursor();
                    break;

                case RatState.Playing:
                    TimeLeft -= dt;
                    int s = Mathf.CeilToInt(TimeLeft);
                    if (s <= 10 && s >= 1 && s != lastWarnSecond) { lastWarnSecond = s; RatAudio.Play2D(RatSound.Warning, 0.45f, s <= 3 ? 1.25f : 1f); }
                    if (completeAt > 0f)
                    {
                        if (Time.time >= completeAt) { completeAt = -1f; CompleteLevel(); }
                        break;
                    }
                    if (TimeLeft <= 0f) { TimeLeft = 0f; GameOver("TIME'S UP!"); break; }
                    if (gun != null && gun.TotalAmmo == 0 && !gun.Reloading && MiceRemaining > 0)
                    {
                        if (outOfAmmoAt < 0f) outOfAmmoAt = Time.time + 1.4f;
                        else if (Time.time >= outOfAmmoAt) { GameOver("OUT OF AMMO!"); break; }
                    }
                    RelockCursor();
                    break;
            }
        }

        void RelockCursor()
        {
            // clicking back into the window (after alt-tab) captures the mouse again without firing
            if (Cursor.lockState != CursorLockMode.Locked && DesktopInput.MousePressed(0) && (hud == null || !hud.PointerOverPauseButton))
                SetCursor(true);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && (State == RatState.Playing || State == RatState.Countdown)) Pause();
        }

        // ------------------------------------------------------------------ shots

        void OnShot(CartoonGun.ShotResult r)
        {
            if (State != RatState.Playing) return;
            ShotsFired++;
            LevelShots++;
            if (r.mouse != null && r.mouse.IsAlive)
            {
                MouseAI m = r.mouse;
                ShotsHit++;
                LevelHits++;
                Combo++;
                BestCombo = Mathf.Max(BestCombo, Combo);
                missStreak = 0;
                if (r.headshot) Headshots++;
                float mult = 1f + Mathf.Min(Combo - 1, 4) * 0.5f;
                int pts = Mathf.RoundToInt(m.Points * mult * (r.headshot ? 1.5f : 1f) * RatDifficultyInfo.Score(Difficulty) / 5f) * 5;
                Score += pts;
                Kills++;
                LevelKills++;

                Vector3 popAt = m.HeadPosition + Vector3.up * 0.25f;
                m.Kill(r.point, r.direction, r.headshot);
                RatFX.HitStars(r.point, r.normal);
                RatAudio.Play2D(RatSound.Bonk, 0.85f, Random.Range(0.95f, 1.1f));
                RatAudio.Play2D(Combo >= 3 ? RatSound.Combo : RatSound.Ding, 0.55f);
                if (player != null) player.Shake(0.15f);
                if (hud != null)
                {
                    hud.HitMarker(r.headshot);
                    hud.Popup(popAt, "+" + pts, r.headshot ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.9f, 0.25f), 1f);
                    if (r.headshot) hud.Popup(popAt + Vector3.up * 0.35f, "HEADSHOT!", new Color(1f, 0.35f, 0.25f), 0.8f);
                    if (Combo >= 2) hud.ComboPop(Combo, mult);
                }
                if (LevelKills >= Level.mice && completeAt < 0f)
                {
                    completeAt = Time.time + 1.6f;
                    Announce("LEVEL CLEAR!", "All " + Level.mice + " mice caught");
                }
            }
            else
            {
                Combo = 0;
                missStreak++;
                if (hud != null && (missStreak == 1 && LevelShots <= 3 || missStreak == 3))
                    hud.Banner("MISSED!", missStreak >= 3 ? "Steady... aim with the right mouse button" : "The mice are spooked - watch them hide!");
            }
            if (spawner != null) spawner.BroadcastGunshot(r.origin, r.direction, r.point);
        }

        void OnDryFire()
        {
            if (hud != null) hud.Banner("CLICK!", "Out of ammo");
        }
    }
}
