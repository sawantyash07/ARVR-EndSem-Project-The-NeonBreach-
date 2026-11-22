using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Automated smoke test. Does nothing unless the game is started with the command-line argument -nbSmokeTest.
    /// It plays through every system (menu, tutorial, combat with an aim-bot, shop, power-ups, sniper + tank,
    /// second arena with NavMesh, split-screen co-op, pause/settings, game over + analytics), takes screenshots,
    /// records every error/exception and writes a report, then quits. Used to verify builds without a human.
    /// </summary>
    [DefaultExecutionOrder(-30)]   // after PlayerController (-50) fills the input, before WeaponSystem (-20) reads it
    public class AutoTest : MonoBehaviour
    {
        float fireUntil;
        int selectWeapon = -1;
        string outDir;
        readonly StringBuilder report = new StringBuilder();
        readonly List<string> errors = new List<string>();
        int shots;

        /// <summary>True while the automated smoke test is driving the game (stray keyboard/mouse input is ignored).</summary>
        public static bool Active { get; private set; }
        bool autopilot;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool on = false;
            for (int i = 0; i < args.Length; i++) if (args[i] == "-nbSmokeTest") on = true;
            if (!on) return;
            GameObject go = new GameObject("[AutoTest]");
            DontDestroyOnLoad(go);
            Active = true;
            go.AddComponent<AutoTest>();
        }

        void Awake()
        {
            outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SmokeTest");
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived += OnLog;
            Application.wantsToQuit += () =>
            {
                if (!finished)
                {
                    Log("UNEXPECTED QUIT requested. Stack: " + System.Environment.StackTrace);
                    File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
                }
                return true;
            };
            PlayerPrefs.DeleteAll();
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(type + ": " + msg + "\n" + stack);
        }

        void Log(string s)
        {
            string line = "[" + Time.realtimeSinceStartup.ToString("0.0") + "s] " + s;
            report.AppendLine(line);
            Debug.Log("[SmokeTest] " + s);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            shots++;
            string file = Path.Combine(outDir, shots.ToString("00") + "_" + name + ".png");
            ScreenCapture.CaptureScreenshot(file);
            yield return null;
            yield return null;
            Log("screenshot " + Path.GetFileName(file));
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Aim-bot: look at the nearest enemy (or target) and fire. Runs before the weapon reads input.</summary>
        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.Playing) return;
            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController p = gm.Players[i];
                if (p == null || p.IsDead) continue;
                if (autopilot) Drive(p);
                if (Time.realtimeSinceStartup < fireUntil)
                {
                    p.Inp.fireHeld = true;
                    p.Inp.firePressed = Time.frameCount % 5 == 0;
                }
                if (selectWeapon >= 0) p.Inp.weaponSelect = selectWeapon;
            }
            selectWeapon = -1;
        }

        void Drive(PlayerController p)
        {
            if (p == null || p.IsDead) return;
            Vector3? aim = null;
            float best = float.MaxValue;
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                Enemy e = Enemy.All[i];
                if (e == null) continue;
                float d = (e.transform.position - p.transform.position).sqrMagnitude;
                if (d < best) { best = d; aim = e.transform.position + Vector3.up * (e.type == EnemyType.Drone ? 0f : 1.1f); }
            }
            TrainingTarget[] tt = FindObjectsByType<TrainingTarget>();
            for (int i = 0; i < tt.Length; i++)
            {
                if (tt[i].IsDead) continue;
                float d = (tt[i].transform.position - p.transform.position).sqrMagnitude;
                if (d < best) { best = d; aim = tt[i].transform.position + Vector3.up * 1.45f; }
            }
            if (aim.HasValue) p.LookAt(aim.Value);
        }

        IEnumerator Start()
        {
            yield return Wait(3f);
            GameManager gm = GameManager.Instance;
            if (gm == null) { Log("FAIL: no GameManager"); Finish(); yield break; }
            Log("GameManager found. Arena " + ArenaManager.CurrentId + ", NavReady=" + ArenaManager.NavReady);
            PlayerController.GodMode = true;

            // ---------------------------------------------------------------- menu + settings
            yield return Shot("main_menu");
            gm.Hud.DebugShow(1, 3); yield return Shot("settings_controls");
            gm.Hud.DebugShow(1, 4); yield return Shot("settings_vr");
            gm.Hud.DebugShow(3, 0); yield return Shot("leaderboard_empty");
            GameSettings.Language = 1; gm.Hud.DebugShow(0, 0); yield return Shot("menu_spanish");
            GameSettings.Language = 0;

            // ---------------------------------------------------------------- tutorial
            gm.StartTutorial();
            yield return Wait(1.5f);
            Log("Tutorial running=" + gm.Tutorial.Running + " step=" + gm.Tutorial.Title);
            yield return Shot("tutorial_step1");
            gm.FinishTutorial(true);
            yield return Wait(0.5f);
            Log("After tutorial: state=" + gm.State + " InTutorial=" + gm.InTutorial);

            // ---------------------------------------------------------------- combat wave 1
            autopilot = true;
            selectWeapon = 1;
            int shotsBefore = gm.ShotsFired;
            fireUntil = Time.realtimeSinceStartup + 14f;
            yield return Wait(6f);
            yield return Shot("combat_wave1");
            GameSettings.PostFx = false;
            yield return Shot("combat_wave1_no_postfx");
            GameSettings.PostFx = true;
            yield return Wait(8f);
            Log("Wave " + gm.Waves.Wave + ", shots fired " + (gm.ShotsFired - shotsBefore) + ", kills " + gm.Kills + ", alive " + Enemy.AliveCount + ", score " + gm.Score);

            // finish the wave quickly
            yield return ClearUntilShop(gm, 40f);
            Log("Shop open: " + (gm.State == GameState.Shop) + ", credits " + gm.Credits);
            yield return Shot("shop");
            gm.AddScore(20000);
            bool b1 = gm.TryBuyUpgrade(UpgradeId.Damage), b2 = gm.TryBuyUpgrade(UpgradeId.FireRate), b3 = gm.TryBuyItem(ShopItem.Grenades);
            Log("Bought damage=" + b1 + " firerate=" + b2 + " grenades=" + b3 + " -> damage x" + Upgrades.DamageMult + ", credits " + gm.Credits);
            yield return Shot("shop_after_buying");
            gm.CloseShop();
            yield return Wait(5f);

            // ---------------------------------------------------------------- power-ups + new enemies
            Vector3 pp = gm.Player.transform.position + gm.Player.transform.forward * 1.5f + Vector3.up * 0.8f;
            Pickup.Spawn(PickupType.SlowMotion, pp);
            yield return Wait(0.6f);
            Log("SlowMo active=" + PowerUps.SlowMo);
            Pickup.Spawn(PickupType.DoubleDamage, pp);
            yield return Wait(0.6f);
            Log("DoubleDamage active=" + PowerUps.DoubleDamage);
            Vector3 fwd = gm.Player.transform.forward;
            Enemy sniper = Enemy.Spawn(EnemyType.Sniper, gm.Player.transform.position + Quaternion.Euler(0f, 40f, 0f) * fwd * 18f + Vector3.up * 0.2f, 4);
            Enemy tank = Enemy.Spawn(EnemyType.Tank, gm.Player.transform.position + fwd * 9f + Vector3.up * 0.2f, 4);
            autopilot = false;
            gm.Player.LookAt(tank.transform.position + Vector3.up * 1.3f);
            yield return Wait(2.5f);
            yield return Shot("slowmo_tank_sniper");
            float shieldBefore = tank.ShieldHealth;
            for (int i = 0; i < 5; i++) tank.TakeDamage(10f, tank.transform.position + tank.transform.forward * 0.8f + Vector3.up, false, gm.Player.gameObject);
            Log("Tank frontal hits: shield " + shieldBefore + " -> " + tank.ShieldHealth + ", health " + tank.health + "/" + tank.maxHealth);
            tank.TakeDamage(10f, tank.transform.position - tank.transform.forward * 0.8f + Vector3.up, false, gm.Player.gameObject);
            Log("Tank rear hit: health " + tank.health);
            yield return Wait(2f);
            Log("Sniper alive=" + (sniper != null && !sniper.IsDead));
            autopilot = true;
            fireUntil = Time.realtimeSinceStartup + 6f;
            yield return Wait(6f);
            yield return Shot("double_damage_fight");

            // ---------------------------------------------------------------- pause
            gm.Pause();
            yield return Wait(0.3f);
            yield return Shot("pause");
            gm.Resume();

            // ---------------------------------------------------------------- arena 2 (Foundry) + NavMesh
            gm.EnterMenu();
            GameSettings.Arena = 1;
            gm.StartGame();
            yield return Wait(1f);
            Log("Arena " + ArenaManager.CurrentId + " NavReady=" + ArenaManager.NavReady + " spawn points " + (gm.Waves.spawnPoints != null ? gm.Waves.spawnPoints.Length : 0));
            fireUntil = Time.realtimeSinceStartup + 10f;
            yield return Wait(7f);
            yield return Shot("foundry_arena");
            yield return Wait(3f);
            Log("Foundry wave " + gm.Waves.Wave + " kills " + gm.Kills + " alive " + Enemy.AliveCount);

            // ---------------------------------------------------------------- co-op split screen
            gm.EnterMenu();
            GameSettings.Arena = 0;
            GameSettings.CoOp = true;
            gm.StartGame();
            yield return Wait(5f);
            Log("Co-op players " + gm.Players.Count + ", P2 cam rect " + (gm.Player2 != null ? gm.Player2.cam.rect.ToString() : "-"));
            yield return Shot("coop_split_screen");
            gm.Player2.Kill();
            yield return Wait(1f);
            Log("After P2 down: state=" + gm.State + " (should still be Playing)");
            yield return Shot("coop_p2_down");

            // ---------------------------------------------------------------- game over + analytics
            PlayerController.GodMode = false;
            gm.Player.Kill();
            yield return Wait(2.5f);
            Log("State after both down: " + gm.State + ", rank " + gm.LastRank + ", board size " + Leaderboard.Runs.Count);
            yield return Shot("gameover_summary");
            gm.Hud.DebugShow(0, 1); yield return Wait(0.3f);
            yield return Shot("gameover_analytics");
            gm.Hud.DebugShow(0, 2); yield return Wait(0.3f);
            yield return Shot("gameover_leaderboard");
            GameSettings.CoOp = false;

            Finish();
        }

        IEnumerator ClearUntilShop(GameManager gm, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (gm.State != GameState.Shop && Time.realtimeSinceStartup < end)
            {
                for (int i = Enemy.All.Count - 1; i >= 0; i--)
                    if (Enemy.All[i] != null) Enemy.All[i].TakeDamage(99999f, Enemy.All[i].transform.position + Vector3.up, true, gm.Player.gameObject);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        void Finish()
        {
            finished = true;
            Active = false;
            Log("Errors/exceptions logged: " + errors.Count);
            for (int i = 0; i < errors.Count && i < 30; i++) report.AppendLine("  " + errors[i]);
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Application.Quit();
        }
    }
}
