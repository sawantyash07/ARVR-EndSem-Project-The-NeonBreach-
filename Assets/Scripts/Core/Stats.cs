using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    // ======================================================================================== leaderboard

    [System.Serializable]
    public class RunRecord
    {
        public string name;
        public int score;
        public int wave;
        public int kills;
        public int headshots;
        public float accuracy;
        public float time;
        public int difficulty;
        public int arena;
        public bool coop;
        public string date;
    }

    /// <summary>Local top-10 leaderboard, stored as JSON in PlayerPrefs.</summary>
    public static class Leaderboard
    {
        public const int Size = 10;
        const string Key = "nb_board_v2";

        [System.Serializable]
        class Board { public List<RunRecord> runs = new List<RunRecord>(); }

        static Board board;

        public static List<RunRecord> Runs
        {
            get { Load(); return board.runs; }
        }

        static void Load()
        {
            if (board != null) return;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { board = JsonUtility.FromJson<Board>(json); }
                catch (System.Exception) { board = null; }
            }
            if (board == null) board = new Board();
            if (board.runs == null) board.runs = new List<RunRecord>();
        }

        /// <summary>Insert a run; returns its rank (1-based) or 0 if it did not make the top 10.</summary>
        public static int Submit(RunRecord r)
        {
            Load();
            int idx = board.runs.Count;
            for (int i = 0; i < board.runs.Count; i++)
            {
                if (r.score > board.runs[i].score) { idx = i; break; }
            }
            if (idx >= Size) return 0;
            board.runs.Insert(idx, r);
            while (board.runs.Count > Size) board.runs.RemoveAt(board.runs.Count - 1);
            Save();
            return idx + 1;
        }

        public static void Clear()
        {
            Load();
            board.runs.Clear();
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(board));
            PlayerPrefs.Save();
        }
    }

    // ======================================================================================== lifetime stats

    [System.Serializable]
    public class LifetimeData
    {
        public int games;
        public int kills;
        public int headshots;
        public int shotsFired;
        public int shotsHit;
        public int bestWave;
        public int bestScore;
        public int bossKills;
        public int deaths;
        public int creditsSpent;
        public float timePlayed;
        public int[] killsByType = new int[6];
    }

    public static class LifetimeStats
    {
        const string Key = "nb_lifetime_v1";
        static LifetimeData data;

        public static LifetimeData Data
        {
            get
            {
                if (data == null)
                {
                    string json = PlayerPrefs.GetString(Key, "");
                    if (!string.IsNullOrEmpty(json))
                    {
                        try { data = JsonUtility.FromJson<LifetimeData>(json); }
                        catch (System.Exception) { data = null; }
                    }
                    if (data == null) data = new LifetimeData();
                    if (data.killsByType == null || data.killsByType.Length < 6) data.killsByType = new int[6];
                }
                return data;
            }
        }

        public static float Accuracy { get { return Data.shotsFired > 0 ? (float)Data.shotsHit / Data.shotsFired : 0f; } }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            data = new LifetimeData();
            Save();
        }
    }

    // ======================================================================================== run analytics

    /// <summary>
    /// Telemetry for one run, shown on the game-over ANALYTICS tab: damage dealt/taken per 5-second bucket,
    /// kills per enemy type, per-weapon accuracy, and death positions (persisted per arena for the heatmap).
    /// </summary>
    public static class RunAnalytics
    {
        public const float Bucket = 5f;
        public static readonly List<float> Dealt = new List<float>();
        public static readonly List<float> Taken = new List<float>();
        public static readonly int[] KillsByType = new int[6];
        public static readonly int[] WeaponShots = new int[3];
        public static readonly int[] WeaponHits = new int[3];
        public static readonly List<int> WaveTimes = new List<int>();
        public static Vector2 LastDeath;
        public static bool HasDeath;

        static float runStart;

        public static void Begin()
        {
            Dealt.Clear(); Taken.Clear(); WaveTimes.Clear();
            for (int i = 0; i < KillsByType.Length; i++) KillsByType[i] = 0;
            for (int i = 0; i < 3; i++) { WeaponShots[i] = 0; WeaponHits[i] = 0; }
            HasDeath = false;
            runStart = Time.time;
        }

        static int Index()
        {
            int i = Mathf.Max(0, Mathf.FloorToInt((Time.time - runStart) / Bucket));
            while (Dealt.Count <= i) { Dealt.Add(0f); Taken.Add(0f); }
            return i;
        }

        public static void OnDealt(float amount) { int i = Index(); Dealt[i] += amount; }
        public static void OnTaken(float amount) { int i = Index(); Taken[i] += amount; }

        public static void OnKill(EnemyType t)
        {
            int i = Mathf.Clamp((int)t, 0, KillsByType.Length - 1);
            KillsByType[i]++;
        }

        public static void OnShot(int weapon, bool hit)
        {
            if (weapon < 0 || weapon >= 3) return;
            WeaponShots[weapon]++;
            if (hit) WeaponHits[weapon]++;
        }

        public static void OnWaveCleared() { WaveTimes.Add(Mathf.RoundToInt(Time.time - runStart)); }

        public static void OnDeath(Vector3 worldPos, int arena)
        {
            LastDeath = new Vector2(worldPos.x, worldPos.z);
            HasDeath = true;
            DeathMap.Add(arena, LastDeath);
        }

        /// <summary>Make sure the series covers the whole run (so the chart reaches the end time).</summary>
        public static void Finish() { Index(); }
    }

    /// <summary>All recorded death positions per arena (last 300), used for the heatmap.</summary>
    public static class DeathMap
    {
        public const int Max = 300;

        [System.Serializable]
        class Store { public List<Vector2> points = new List<Vector2>(); }

        static readonly Dictionary<int, Store> stores = new Dictionary<int, Store>();

        static Store Get(int arena)
        {
            Store s;
            if (stores.TryGetValue(arena, out s)) return s;
            string json = PlayerPrefs.GetString("nb_deaths_" + arena, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { s = JsonUtility.FromJson<Store>(json); }
                catch (System.Exception) { s = null; }
            }
            if (s == null) s = new Store();
            if (s.points == null) s.points = new List<Vector2>();
            stores[arena] = s;
            return s;
        }

        public static List<Vector2> Points(int arena) { return Get(arena).points; }

        public static void Add(int arena, Vector2 p)
        {
            Store s = Get(arena);
            s.points.Add(p);
            while (s.points.Count > Max) s.points.RemoveAt(0);
            PlayerPrefs.SetString("nb_deaths_" + arena, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        public static void Clear(int arena)
        {
            Get(arena).points.Clear();
            PlayerPrefs.DeleteKey("nb_deaths_" + arena);
        }
    }
}
