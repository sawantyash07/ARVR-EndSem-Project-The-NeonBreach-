using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Endless wave director. Every wave is bigger and mixes in more dangerous enemy types (snipers from wave 3,
    /// shield tanks from wave 4); every 5th wave is a boss wave. Enemy count scales with difficulty.
    /// After each wave: bonus, partial resupply, fallen co-op partners revive, and the upgrade shop opens.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public Transform[] spawnPoints;

        public int Wave { get; private set; }
        public int TotalThisWave { get; private set; }
        public int SpawnedThisWave { get; private set; }
        public bool Intermission { get; private set; }
        public float IntermissionLeft { get; private set; }
        public bool Running { get { return routine != null; } }

        /// <summary>Enemies still to kill in this wave (alive + not yet spawned).</summary>
        public int Remaining { get { return Enemy.AliveCount + Mathf.Max(0, TotalThisWave - SpawnedThisWave); } }

        public Enemy Boss
        {
            get
            {
                for (int i = 0; i < Enemy.All.Count; i++)
                    if (Enemy.All[i] != null && Enemy.All[i].type == EnemyType.Boss) return Enemy.All[i];
                return null;
            }
        }

        Coroutine routine;

        void Awake()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) FindSpawnPoints();
        }

        public void FindSpawnPoints()
        {
            GameObject root = GameObject.Find("SpawnPoints");
            if (root == null) return;
            spawnPoints = new Transform[root.transform.childCount];
            for (int i = 0; i < spawnPoints.Length; i++) spawnPoints[i] = root.transform.GetChild(i);
        }

        public void Begin()
        {
            StopRun();
            ClearEnemies();
            Wave = 0;
            TotalThisWave = 0;
            SpawnedThisWave = 0;
            routine = StartCoroutine(Run());
        }

        public void StopRun()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Intermission = false;
        }

        public void ClearEnemies()
        {
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
                if (Enemy.All[i] != null) Destroy(Enemy.All[i].gameObject);
            Enemy.All.Clear();
        }

        public static List<EnemyType> Compose(int wave)
        {
            List<EnemyType> list = new List<EnemyType>();
            bool boss = wave % 5 == 0;
            float k = (boss ? 0.5f : 1f) * DifficultyInfo.EnemyCount;
            if (MixedReality.Active) k *= 0.45f;

            int grunts = Mathf.RoundToInt((3 + wave * 1.6f) * k);
            int gunners = wave >= 2 ? Mathf.RoundToInt(wave * 0.8f * k) : 0;
            int drones = wave >= 3 ? Mathf.RoundToInt((wave - 1) * 0.6f * k) : 0;
            int snipers = wave >= 3 ? Mathf.Max(1, Mathf.RoundToInt((wave - 1) / 3f * k)) : 0;
            int tanks = wave >= 4 ? Mathf.Max(1, Mathf.RoundToInt((wave - 2) / 3.5f * k)) : 0;
            if (boss) { snipers = Mathf.Min(snipers, 2); tanks = Mathf.Min(tanks, 1); }

            int cap = MixedReality.Active ? 16 : 40;
            while (grunts + gunners + drones + snipers + tanks > cap)
            {
                if (grunts > gunners && grunts > drones) grunts--;
                else if (gunners >= drones && gunners > 0) gunners--;
                else if (drones > 0) drones--;
                else if (snipers > 1) snipers--;
                else tanks--;
            }

            for (int i = 0; i < grunts; i++) list.Add(EnemyType.Grunt);
            for (int i = 0; i < gunners; i++) list.Add(EnemyType.Gunner);
            for (int i = 0; i < drones; i++) list.Add(EnemyType.Drone);
            for (int i = 0; i < snipers; i++) list.Add(EnemyType.Sniper);
            for (int i = 0; i < tanks; i++) list.Add(EnemyType.Tank);
            Util.Shuffle(list);
            if (boss) list.Insert(0, EnemyType.Boss);
            return list;
        }

        Vector3 PickSpawn(GameManager gm)
        {
            Vector3 best = new Vector3(0f, 0.1f, 20f);
            if (spawnPoints == null || spawnPoints.Length == 0) return best;
            float minDist = MixedReality.Active ? 1.2f : 14f;
            for (int tries = 0; tries < 16; tries++)
            {
                Transform t = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (t == null) continue;
                Vector3 p = t.position;
                bool tooClose = false;
                for (int i = 0; i < gm.Players.Count; i++)
                {
                    PlayerController pl = gm.Players[i];
                    if (pl == null || pl.IsDead) continue;
                    Vector3 d = p - pl.BodyCenter;
                    d.y = 0f;
                    if (d.magnitude < minDist) { tooClose = true; break; }
                }
                if (tooClose) continue;
                if (Physics.CheckSphere(p + Vector3.up * 1f, 0.7f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
            Transform any = spawnPoints[Random.Range(0, spawnPoints.Length)];
            return any != null ? any.position : best;
        }

        IEnumerator Run()
        {
            GameManager gm = GameManager.Instance;
            yield return new WaitForSeconds(1.5f);

            while (true)
            {
                Wave++;
                bool boss = Wave % 5 == 0;
                List<EnemyType> list = Compose(Wave);
                TotalThisWave = list.Count;
                SpawnedThisWave = 0;

                if (gm.Hud != null)
                {
                    if (boss) gm.Hud.Banner(Loc.T("BOSS WAVE") + " " + Wave, Loc.T("A heavy unit approaches - jump to dodge its slam"));
                    else if (Wave == 3) gm.Hud.Banner(Loc.T("WAVE") + " " + Wave, Loc.T("Snipers deployed - watch for green lasers"));
                    else if (Wave == 4) gm.Hud.Banner(Loc.T("WAVE") + " " + Wave, Loc.T("Shield tanks deployed - flank them or use grenades"));
                    else gm.Hud.Banner(Loc.T("WAVE") + " " + Wave, TotalThisWave + " " + Loc.T("hostiles incoming"));
                }
                Sfx.Play2D(SfxId.WaveStart, 0.8f);
                yield return new WaitForSeconds(2.5f);

                int maxAlive = MixedReality.Active ? 5 : Mathf.Min(14 + Wave / 2, 22);
                float interval = Mathf.Max(0.35f, 1.1f - Wave * 0.05f) * (MixedReality.Active ? 2f : 1f);
                for (int i = 0; i < list.Count; i++)
                {
                    while (Enemy.AliveCount >= maxAlive) yield return new WaitForSeconds(0.3f);
                    Vector3 p = PickSpawn(gm);
                    if (list[i] == EnemyType.Drone) p.y += MixedReality.Active ? 1.8f : 3f;
                    Enemy.Spawn(list[i], p + Vector3.up * 0.1f, Wave);
                    FX.Burst(p + Vector3.up * 0.5f, new Color(0.3f, 0.9f, 1f), 14, 3.5f, 0.12f, 0.5f);
                    SpawnedThisWave++;
                    yield return new WaitForSeconds(boss && i == 0 ? 2f : interval);
                }

                while (Enemy.AliveCount > 0) yield return new WaitForSeconds(0.25f);

                // wave cleared
                int bonus = Mathf.RoundToInt(250 * Wave * DifficultyInfo.ScoreMultiplier);
                gm.AddScore(bonus);
                gm.BestWave = Wave;
                RunAnalytics.OnWaveCleared();
                if (gm.Hud != null) gm.Hud.Banner(Loc.T("WAVE") + " " + Wave + " " + Loc.T("CLEARED"), "+" + bonus + " " + Loc.T("bonus") + "   -   " + Loc.T("resupplying..."));
                Sfx.Play2D(SfxId.Pickup, 0.9f, 0.7f);
                gm.RevivePlayers();
                for (int i = 0; i < gm.Players.Count; i++)
                {
                    PlayerController pl = gm.Players[i];
                    if (pl == null || pl.IsDead) continue;
                    pl.Heal(30f);
                    pl.AddShield(30f);
                    pl.weapons.AddAmmo(0.3f);
                    pl.weapons.AddGrenades(1);
                }

                // upgrade shop (the game waits while it is open)
                yield return new WaitForSeconds(1.2f);
                gm.OpenShop();
                while (gm.State == GameState.Shop || gm.State == GameState.Paused) yield return null;

                Intermission = true;
                IntermissionLeft = 4f;
                while (IntermissionLeft > 0f)
                {
                    IntermissionLeft -= Time.deltaTime;
                    yield return null;
                }
                Intermission = false;
            }
        }
    }
}
