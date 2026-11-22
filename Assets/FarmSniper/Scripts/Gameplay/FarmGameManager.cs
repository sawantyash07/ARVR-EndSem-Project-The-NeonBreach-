using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonBreach.Farm
{
    /// <summary>
    /// "Farm Sniper" mission controller: Title → Briefing → Playing ⇄ Paused → Victory / Game Over.
    /// Holds the difficulty profiles, mission timer, score (kill + headshot + distance + moving-target + streak bonuses),
    /// accuracy, crop health and the kill feed. Independent of the Neon Breach GameManager: it only shares the
    /// project's input, settings, audio and effect helpers.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FarmGameManager : MonoBehaviour
    {
        public static FarmGameManager Instance { get; private set; }

        [Header("References (assigned by the scene builder)")]
        public FarmPlayer player;
        public RatSpawner spawner;
        public FarmHUD hud;

        [Header("Difficulty")]
        public FarmDifficulty difficulty = FarmDifficulty.Normal;
        public DifficultyProfile easy = DifficultyProfile.DefaultEasy();
        public DifficultyProfile normal = DifficultyProfile.DefaultNormal();
        public DifficultyProfile hard = DifficultyProfile.DefaultHard();

        [Header("Scoring")]
        public int killScore = 100;
        public int headshotBonus = 60;
        [Tooltip("Bonus points per metre beyond 'distanceBonusFrom'.")]
        public float distanceBonusPerMetre = 1.5f;
        public float distanceBonusFrom = 25f;
        public int movingTargetBonus = 40;
        [Tooltip("Each consecutive hit without a miss adds this multiplier (0.1 = +10 %).")]
        [Range(0f, 0.5f)] public float streakStep = 0.1f;
        [Range(0f, 2f)] public float streakMax = 0.5f;
        public int timeBonusPerSecond = 5;
        public int cropBonusPerPercent = 8;
        public int accuracyBonusPerPercent = 6;

        [Header("Mission rules")]
        public bool cropLossEndsMission = true;
        [Tooltip("Mission fails when the farm's total crop health falls to this.")]
        [Range(0f, 0.9f)] public float cropFailThreshold = 0.25f;
        public bool outOfAmmoEndsMission = true;
        [Tooltip("Seconds of 'GET READY' after the briefing before rats appear.")]
        public float startDelay = 2f;
        public float warnTimeLeft = 30f;

        [Header("Scenes")]
        [Tooltip("Scene loaded by 'Back to Neon Breach' (must be in Build Settings).")]
        public string mainSceneName = "NeonBreach";

        [Header("Rendering")]
        public float shadowDistance = 150f;
        public int shadowCascades = 4;

        // ------------------------------------------------------------------ state

        public FarmState State { get; private set; }
        public DifficultyProfile Profile { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int Headshots { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public float LongestShot { get; private set; }
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public float StartCountdown { get; private set; }
        public FailReason Reason { get; private set; }
        public int TimeBonus { get; private set; }
        public int CropBonus { get; private set; }
        public int AccuracyBonus { get; private set; }
        public bool NewBest { get; private set; }
        public float LastHitTime { get; private set; }
        public bool LastHitHead { get; private set; }
        public readonly List<FeedEntry> Feed = new List<FeedEntry>();

        public int Target { get { return Profile != null ? Profile.ratsToEliminate : 0; } }
        public int RemainingRats { get { return Mathf.Max(0, Target - Kills); } }
        public float Accuracy { get { return ShotsFired > 0 ? ShotsHit / (float)ShotsFired : 0f; } }
        public float CropHealth { get { return spawner != null ? spawner.CropHealth : 1f; } }
        public bool Playing { get { return State == FarmState.Playing; } }
        public bool VrActive { get { return player != null && player.VrActive; } }

        float savedShadowDistance;
        int savedCascades;
        FarmState pausedFrom;
        int lastTickSecond = -1;

        public DifficultyProfile ProfileFor(FarmDifficulty d)
        {
            return d == FarmDifficulty.Easy ? easy : (d == FarmDifficulty.Hard ? hard : normal);
        }

        public static string BestKey(FarmDifficulty d) { return "farm_best_" + (int)d; }
        public static int Best(FarmDifficulty d) { return PlayerPrefs.GetInt(BestKey(d), 0); }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSettings.Load();
            difficulty = (FarmDifficulty)Mathf.Clamp(PlayerPrefs.GetInt("farm_diff", (int)difficulty), 0, 2);
            Profile = ProfileFor(difficulty);
            savedShadowDistance = QualitySettings.shadowDistance;
            savedCascades = QualitySettings.shadowCascades;
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = shadowCascades;
            if (spawner != null && player != null) spawner.Player = player;
            Debug.Log("[Farm Sniper] Scene ready - " + (VrActive ? "VR" : "desktop") + " mode.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // leave the shared quality settings as we found them for the other scenes
            QualitySettings.shadowDistance = savedShadowDistance;
            QualitySettings.shadowCascades = savedCascades;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Start() { GoTitle(); }

        // ------------------------------------------------------------------ flow

        public void SetDifficulty(FarmDifficulty d)
        {
            difficulty = d;
            Profile = ProfileFor(d);
            PlayerPrefs.SetInt("farm_diff", (int)d);
        }

        public void GoTitle()
        {
            State = FarmState.Title;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (spawner != null)
            {
                spawner.StopMission(true);
                for (int i = 0; i < spawner.fields.Length; i++) if (spawner.fields[i] != null) spawner.fields[i].ResetHealth();
            }
            if (player != null) { player.InputEnabled = false; player.ResetPlayer(); player.AttractMode = true; }
            SetCursor(false);
        }

        public void ShowBriefing()
        {
            Profile = ProfileFor(difficulty);
            State = FarmState.Briefing;
            SetCursor(false);
            FarmAudio.PlayUI(FarmSound.UiSelect);
        }

        public void BeginMission()
        {
            Profile = ProfileFor(difficulty);
            Score = Kills = Headshots = ShotsFired = ShotsHit = Streak = BestStreak = 0;
            LongestShot = 0f;
            TimeLeft = Profile.timeLimit;
            Elapsed = 0f;
            Reason = FailReason.None;
            TimeBonus = CropBonus = AccuracyBonus = 0;
            NewBest = false;
            Feed.Clear();
            StartCountdown = startDelay;
            lastTickSecond = -1;

            if (spawner != null)
            {
                spawner.StopMission(true);
                spawner.Player = player;
                for (int i = 0; i < spawner.fields.Length; i++) if (spawner.fields[i] != null) spawner.fields[i].ResetHealth();
            }
            if (player != null) { player.ResetPlayer(); player.AttractMode = false; player.InputEnabled = true; }
            for (int i = 0; player != null && i < player.ammoCrates.Length; i++) if (player.ammoCrates[i] != null) player.ammoCrates[i].ResetCrate();
            FX.ClearDecals();

            State = FarmState.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetCursor(!VrActive);
            FarmAudio.PlayUI(FarmSound.UiSelect);
            PushFeed("GET READY", new Color(1f, 0.9f, 0.5f));
        }

        public void Pause()
        {
            if (State != FarmState.Playing) return;
            pausedFrom = State;
            State = FarmState.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (player != null) player.InputEnabled = false;
            SetCursor(false);
            FarmAudio.PlayUI(FarmSound.UiClick);
        }

        public void Resume()
        {
            if (State != FarmState.Paused) return;
            State = pausedFrom;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (player != null) player.InputEnabled = true;
            SetCursor(!VrActive);
            FarmAudio.PlayUI(FarmSound.UiClick);
        }

        public void Restart() { BeginMission(); }

        public void LoadMainGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (Application.CanStreamedLevelBeLoaded(mainSceneName)) SceneManager.LoadScene(mainSceneName);
            else Debug.LogWarning("[Farm Sniper] Scene '" + mainSceneName + "' is not in Build Settings.");
        }

        public bool CanLoadMainGame { get { return !string.IsNullOrEmpty(mainSceneName) && Application.CanStreamedLevelBeLoaded(mainSceneName); } }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Victory()
        {
            State = FarmState.Victory;
            EndRun();
            TimeBonus = Mathf.RoundToInt(TimeLeft) * timeBonusPerSecond;
            CropBonus = Mathf.RoundToInt(CropHealth * 100f) * cropBonusPerPercent;
            AccuracyBonus = Mathf.RoundToInt(Accuracy * 100f) * accuracyBonusPerPercent;
            Score += Mathf.RoundToInt((TimeBonus + CropBonus + AccuracyBonus) * Profile.scoreMultiplier);
            SubmitBest();
            FarmAudio.PlayUI(FarmSound.Victory);
        }

        void Fail(FailReason r)
        {
            State = FarmState.GameOver;
            Reason = r;
            EndRun();
            SubmitBest();
            FarmAudio.PlayUI(FarmSound.Fail);
        }

        void EndRun()
        {
            if (spawner != null) spawner.StopMission(false);
            if (player != null) player.InputEnabled = false;
            SetCursor(false);
        }

        void SubmitBest()
        {
            int best = Best(difficulty);
            if (Score > best)
            {
                PlayerPrefs.SetInt(BestKey(difficulty), Score);
                PlayerPrefs.Save();
                NewBest = Score > 0;
            }
        }

        /// <summary>Stars for the results screen (1-3).</summary>
        public int Stars
        {
            get
            {
                if (State != FarmState.Victory) return 0;
                int s = 1;
                if (Accuracy >= 0.6f && CropHealth >= 0.6f) s++;
                if (Accuracy >= 0.8f && CropHealth >= 0.8f && TimeLeft >= Profile.timeLimit * 0.25f) s++;
                return s;
            }
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            bool vrMenu = player != null && player.vr != null && player.vr.MenuPressed;
            bool pausePressed = DesktopInput.Pressed(KeyCode.Escape) || GamepadInput.AnyStartPressed() || vrMenu;

            switch (State)
            {
                case FarmState.Playing:
                    if (pausePressed) { Pause(); break; }
                    // click back into the game after alt-tab
                    if (!VrActive && Cursor.lockState != CursorLockMode.Locked && DesktopInput.MousePressed(0)) SetCursor(true);
                    TickMission(Time.deltaTime);
                    break;
                case FarmState.Paused:
                    if (pausePressed) Resume();
                    break;
                case FarmState.Briefing:
                    if (pausePressed) GoTitle();
                    break;
            }
            for (int i = Feed.Count - 1; i >= 0; i--) if (Time.unscaledTime - Feed[i].time > 3.2f) Feed.RemoveAt(i);
        }

        void TickMission(float dt)
        {
            if (StartCountdown > 0f)
            {
                StartCountdown -= dt;
                if (StartCountdown <= 0f && spawner != null)
                {
                    spawner.BeginMission(Profile, Profile.ratsToEliminate);
                    PushFeed("PROTECT THE CROPS!", new Color(1f, 0.85f, 0.4f));
                }
                return;
            }

            Elapsed += dt;
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
            if (TimeLeft <= warnTimeLeft && TimeLeft > 0f)
            {
                int sec = Mathf.CeilToInt(TimeLeft);
                if (sec != lastTickSecond && sec <= 10) { lastTickSecond = sec; FarmAudio.PlayUI(FarmSound.Tick, 0.8f); }
            }

            if (Kills >= Target) { Victory(); return; }
            if (TimeLeft <= 0f) { Fail(FailReason.TimeUp); return; }
            if (cropLossEndsMission && CropHealth <= cropFailThreshold) { Fail(FailReason.CropsDestroyed); return; }
            if (outOfAmmoEndsMission && player != null && player.rifle != null)
            {
                SniperRifle r = player.rifle;
                bool cratesLeft = false;
                for (int i = 0; i < player.ammoCrates.Length; i++) if (player.ammoCrates[i] != null) cratesLeft = true;
                if (r.Mag == 0 && r.Reserve == 0 && !r.Reloading && !cratesLeft) Fail(FailReason.OutOfAmmo);
            }
        }

        // ------------------------------------------------------------------ events from the rifle

        public void RegisterShot() { ShotsFired++; }

        public void RegisterMiss()
        {
            if (Streak > 0) PushFeed("MISS", new Color(0.8f, 0.8f, 0.8f));
            Streak = 0;
        }

        public void RegisterKill(float distance, bool headshot, bool moving, Vector3 where)
        {
            if (State != FarmState.Playing) return;
            Kills++;
            ShotsHit++;
            if (headshot) Headshots++;
            Streak++;
            BestStreak = Mathf.Max(BestStreak, Streak);
            LongestShot = Mathf.Max(LongestShot, distance);
            LastHitTime = Time.unscaledTime;
            LastHitHead = headshot;

            float pts = killScore;
            if (headshot) pts += headshotBonus;
            if (moving) pts += movingTargetBonus;
            pts += Mathf.Max(0f, distance - distanceBonusFrom) * distanceBonusPerMetre;
            float mult = 1f + Mathf.Min(streakMax, (Streak - 1) * streakStep);
            int gained = Mathf.RoundToInt(pts * mult * Profile.scoreMultiplier);
            Score += gained;

            string text = "+" + gained + "   " + (headshot ? "HEADSHOT" : "RAT DOWN") + "   " + Mathf.RoundToInt(distance) + " m";
            if (moving) text += "   MOVING";
            if (Streak > 1) text += "   x" + Streak;
            PushFeed(text, headshot ? new Color(1f, 0.82f, 0.3f) : Color.white);
        }

        public void PushFeed(string text, Color c)
        {
            Feed.Add(new FeedEntry { text = text, color = c, time = Time.unscaledTime });
            if (Feed.Count > 5) Feed.RemoveAt(0);
        }

        void SetCursor(bool locked)
        {
            if (VrActive) locked = false;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
