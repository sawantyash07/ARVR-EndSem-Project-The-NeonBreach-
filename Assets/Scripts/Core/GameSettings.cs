using UnityEngine;

namespace NeonBreach
{
    public enum Difficulty { Easy, Normal, Hard }
    public enum TurnMode { Snap, Smooth }

    /// <summary>Player options, persisted with PlayerPrefs.</summary>
    public static class GameSettings
    {
        // gameplay
        public static float Sensitivity = 1f;   // 0.2 .. 3
        public static float Fov = 78f;
        public static bool InvertY = false;
        public static float ShakeIntensity = 1f;
        public static Difficulty Difficulty = Difficulty.Normal;
        public static int Arena = 0;
        public static bool CoOp = false;
        public static string PlayerName = "PLAYER";
        public static bool TutorialDone = false;

        // graphics
        public static int Quality = -1;          // -1 = keep the project default
        public static bool PostFx = true;
        public static float Bloom = 0.6f;
        public static bool VSync = true;
        public static bool ShowFps = false;

        // audio
        public static float MasterVolume = 0.8f;
        public static float MusicVolume = 0.45f;
        public static bool Hrtf = true;

        // language: 0 English, 1 Espanol, 2 Francais, 3 Deutsch
        public static int Language = 0;

        // VR comfort + interaction
        public static bool ComfortVignette = true;
        public static float VignetteStrength = 0.7f;
        public static TurnMode Turn = TurnMode.Snap;
        public static float SnapAngle = 45f;
        public static float SmoothTurnSpeed = 120f;
        public static bool Seated = false;
        public static bool HandTracking = true;
        public static bool Haptics = true;
        public static bool MixedReality = false;

        public static void Load()
        {
            Sensitivity = PlayerPrefs.GetFloat("nb_sens", 1f);
            Fov = PlayerPrefs.GetFloat("nb_fov", 78f);
            InvertY = PlayerPrefs.GetInt("nb_invy", 0) == 1;
            ShakeIntensity = PlayerPrefs.GetFloat("nb_shake", 1f);
            Difficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("nb_diff", 1), 0, 2);
            Arena = Mathf.Clamp(PlayerPrefs.GetInt("nb_arena", 0), 0, ArenaCatalog.Count - 1);
            CoOp = PlayerPrefs.GetInt("nb_coop", 0) == 1;
            PlayerName = PlayerPrefs.GetString("nb_name", "PLAYER");
            TutorialDone = PlayerPrefs.GetInt("nb_tut_done", 0) == 1;

            Quality = PlayerPrefs.GetInt("nb_quality", -1);
            PostFx = PlayerPrefs.GetInt("nb_postfx", 1) == 1;
            Bloom = PlayerPrefs.GetFloat("nb_bloom", 0.6f);
            VSync = PlayerPrefs.GetInt("nb_vsync", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("nb_fps", 0) == 1;

            MasterVolume = PlayerPrefs.GetFloat("nb_vol", 0.8f);
            MusicVolume = PlayerPrefs.GetFloat("nb_music", 0.45f);
            Hrtf = PlayerPrefs.GetInt("nb_hrtf", 1) == 1;
            Language = Mathf.Clamp(PlayerPrefs.GetInt("nb_lang", 0), 0, Loc.LanguageCount - 1);

            ComfortVignette = PlayerPrefs.GetInt("nb_vr_vig", 1) == 1;
            VignetteStrength = PlayerPrefs.GetFloat("nb_vr_vigs", 0.7f);
            Turn = (TurnMode)Mathf.Clamp(PlayerPrefs.GetInt("nb_vr_turn", 0), 0, 1);
            SnapAngle = PlayerPrefs.GetFloat("nb_vr_snap", 45f);
            SmoothTurnSpeed = PlayerPrefs.GetFloat("nb_vr_smooth", 120f);
            Seated = PlayerPrefs.GetInt("nb_vr_seated", 0) == 1;
            HandTracking = PlayerPrefs.GetInt("nb_vr_hands", 1) == 1;
            Haptics = PlayerPrefs.GetInt("nb_vr_haptics", 1) == 1;
            MixedReality = PlayerPrefs.GetInt("nb_vr_mr", 0) == 1;

            KeyBindings.Load();
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("nb_sens", Sensitivity);
            PlayerPrefs.SetFloat("nb_fov", Fov);
            PlayerPrefs.SetInt("nb_invy", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("nb_shake", ShakeIntensity);
            PlayerPrefs.SetInt("nb_diff", (int)Difficulty);
            PlayerPrefs.SetInt("nb_arena", Arena);
            PlayerPrefs.SetInt("nb_coop", CoOp ? 1 : 0);
            PlayerPrefs.SetString("nb_name", string.IsNullOrEmpty(PlayerName) ? "PLAYER" : PlayerName);
            PlayerPrefs.SetInt("nb_tut_done", TutorialDone ? 1 : 0);

            PlayerPrefs.SetInt("nb_quality", Quality);
            PlayerPrefs.SetInt("nb_postfx", PostFx ? 1 : 0);
            PlayerPrefs.SetFloat("nb_bloom", Bloom);
            PlayerPrefs.SetInt("nb_vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("nb_fps", ShowFps ? 1 : 0);

            PlayerPrefs.SetFloat("nb_vol", MasterVolume);
            PlayerPrefs.SetFloat("nb_music", MusicVolume);
            PlayerPrefs.SetInt("nb_hrtf", Hrtf ? 1 : 0);
            PlayerPrefs.SetInt("nb_lang", Language);

            PlayerPrefs.SetInt("nb_vr_vig", ComfortVignette ? 1 : 0);
            PlayerPrefs.SetFloat("nb_vr_vigs", VignetteStrength);
            PlayerPrefs.SetInt("nb_vr_turn", (int)Turn);
            PlayerPrefs.SetFloat("nb_vr_snap", SnapAngle);
            PlayerPrefs.SetFloat("nb_vr_smooth", SmoothTurnSpeed);
            PlayerPrefs.SetInt("nb_vr_seated", Seated ? 1 : 0);
            PlayerPrefs.SetInt("nb_vr_hands", HandTracking ? 1 : 0);
            PlayerPrefs.SetInt("nb_vr_haptics", Haptics ? 1 : 0);
            PlayerPrefs.SetInt("nb_vr_mr", MixedReality ? 1 : 0);

            KeyBindings.Save();
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            Sfx.Instance.SetMusicVolume(MusicVolume);
            if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        public static void ResetVrComfort()
        {
            ComfortVignette = true; VignetteStrength = 0.7f; Turn = TurnMode.Snap; SnapAngle = 45f;
            SmoothTurnSpeed = 120f; Seated = false; HandTracking = true; Haptics = true;
        }
    }

    /// <summary>Tuning multipliers for the three difficulty levels.</summary>
    public static class DifficultyInfo
    {
        static readonly float[] hp = { 0.75f, 1f, 1.35f };
        static readonly float[] dmg = { 0.6f, 1f, 1.4f };
        static readonly float[] count = { 0.8f, 1f, 1.25f };
        static readonly float[] score = { 0.75f, 1f, 1.5f };
        static readonly float[] spread = { 1.6f, 1f, 0.7f };
        static readonly float[] fire = { 1.35f, 1f, 0.8f };

        static int I { get { return (int)GameSettings.Difficulty; } }
        public static float EnemyHealth { get { return hp[I]; } }
        public static float EnemyDamage { get { return dmg[I]; } }
        public static float EnemyCount { get { return count[I]; } }
        public static float ScoreMultiplier { get { return score[I]; } }
        /// <summary>Enemy aim error multiplier (higher = worse aim).</summary>
        public static float EnemySpread { get { return spread[I]; } }
        /// <summary>Delay multiplier between enemy attacks (higher = slower).</summary>
        public static float EnemyFireDelay { get { return fire[I]; } }

        public static string Name(Difficulty d)
        {
            return d == Difficulty.Easy ? Loc.T("EASY") : (d == Difficulty.Hard ? Loc.T("HARD") : Loc.T("NORMAL"));
        }
    }
}
