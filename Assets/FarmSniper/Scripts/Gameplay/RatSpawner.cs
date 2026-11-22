using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Spawns rats at random burrows (filtered by the difficulty's spawn tier and distance from the farmer), keeps the
    /// number of live rats under the difficulty's cap, relays gunshot / impact noise to the rats and owns the rat
    /// NavMesh (pre-baked by the scene builder; rebuilt asynchronously at runtime if it is missing).
    /// </summary>
    public class RatSpawner : MonoBehaviour
    {
        [Header("References (set by the scene builder)")]
        public RatAI ratTemplate;
        public RatSpawnPoint[] burrows = new RatSpawnPoint[0];
        public HideSpot[] hideSpots = new HideSpot[0];
        public CropField[] fields = new CropField[0];

        [Header("Spawning")]
        [Tooltip("Burrows closer than this to the farmer are skipped.")]
        [Min(0f)] public float minSpawnDistance = 14f;
        [Tooltip("Burrows farther than this from the farmer are skipped (keeps rats in scope range).")]
        [Min(10f)] public float maxSpawnDistance = 140f;
        [Tooltip("A burrow is not reused for this many seconds.")]
        [Min(0f)] public float burrowCooldown = 6f;
        [Tooltip("Random scale range for each rat.")]
        public Vector2 ratScale = new Vector2(0.9f, 1.15f);

        [Header("Farm bounds (rats stay inside)")]
        public float farmHalfSize = 92f;

        [Header("NavMesh")]
        [Tooltip("Baked by Neon Breach > Farm Sniper > Build Farm Scene. If empty, a NavMesh is built when the scene starts.")]
        public NavMeshData navMeshData;
        public LayerMask navMeshLayers = 1 << 10;
        public float agentRadius = 0.16f;
        public float agentHeight = 0.3f;
        public float agentClimb = 0.22f;
        [Range(0f, 60f)] public float agentSlope = 42f;
        public float voxelSize = 0.1f;

        public FarmPlayer Player { get; set; }
        public bool NavReady { get; private set; }
        public bool Spawning { get; private set; }
        public int Alive { get { CleanList(); return active.Count; } }
        public IList<RatAI> ActiveRats { get { CleanList(); return active; } }

        readonly List<RatAI> active = new List<RatAI>();
        readonly List<RatSpawnPoint> candidates = new List<RatSpawnPoint>();
        NavMeshDataInstance navInstance;
        DifficultyProfile profile;
        float nextSpawn;
        int spawnBudget;     // how many more rats may still appear (= rats left to eliminate)

        void OnEnable()
        {
            if (navMeshData != null)
            {
                navInstance = NavMesh.AddNavMeshData(navMeshData);
                NavReady = navInstance.valid;
            }
            if (!NavReady) StartCoroutine(BuildNavMeshRuntime());
        }

        void OnDisable()
        {
            if (navInstance.valid) navInstance.Remove();
            NavReady = false;
        }

        /// <summary>Settings used both by the editor bake and the runtime fallback (agent type 0 with rat-sized values).</summary>
        public NavMeshBuildSettings BuildSettings()
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByID(0);
            s.agentRadius = agentRadius;
            s.agentHeight = agentHeight;
            s.agentClimb = agentClimb;
            s.agentSlope = agentSlope;
            s.overrideVoxelSize = true;
            s.voxelSize = voxelSize;
            s.overrideTileSize = true;
            s.tileSize = 256;
            s.minRegionArea = 0.5f;
            return s;
        }

        /// <summary>Areas rats must not walk on (the pond bed under the water).</summary>
        public static void AddNoWalkZones(List<NavMeshBuildSource> sources)
        {
            Vector2 c = FarmLayout.PondCenter;
            float d = FarmLayout.PondRadius * 2f + 0.5f;
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.Translate(new Vector3(c.x, 0f, c.y)),
                size = new Vector3(d, 8f, d),
                area = 1   // Not Walkable
            });
        }

        public Bounds BuildBounds { get { return new Bounds(new Vector3(0f, 5f, 0f), new Vector3(farmHalfSize * 2f + 6f, 30f, farmHalfSize * 2f + 6f)); } }

        IEnumerator BuildNavMeshRuntime()
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(BuildBounds, navMeshLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            AddNoWalkZones(sources);
            NavMeshBuildSettings s = BuildSettings();
            NavMeshData data = new NavMeshData(s.agentTypeID);
            navInstance = NavMesh.AddNavMeshData(data);
            AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(data, s, sources, BuildBounds);
            while (!op.isDone) yield return null;
            NavReady = true;
        }

        // ------------------------------------------------------------------ mission control

        public void BeginMission(DifficultyProfile p, int ratsNeeded)
        {
            profile = p;
            spawnBudget = ratsNeeded;
            Spawning = true;
            nextSpawn = Time.time + 1.2f;
            for (int i = 0; i < burrows.Length; i++) if (burrows[i] != null) burrows[i].lastUsed = -100f;
            for (int i = 0; i < fields.Length; i++) if (fields[i] != null) fields[i].ResetHealth();
        }

        public void StopMission(bool clear)
        {
            Spawning = false;
            if (!clear) return;
            for (int i = 0; i < active.Count; i++) if (active[i] != null) Destroy(active[i].gameObject);
            active.Clear();
        }

        void CleanList()
        {
            for (int i = active.Count - 1; i >= 0; i--) if (active[i] == null || active[i].IsDead) active.RemoveAt(i);
        }

        void Update()
        {
            if (!Spawning || profile == null || ratTemplate == null) return;
            if (Time.time < nextSpawn) return;
            CleanList();
            int cap = Mathf.Min(profile.maxActiveRats, spawnBudget);
            if (active.Count < cap)
            {
                if (SpawnOne()) nextSpawn = Time.time + Random.Range(profile.spawnInterval.x, profile.spawnInterval.y);
                else nextSpawn = Time.time + 0.5f;
            }
            else nextSpawn = Time.time + 0.4f;
        }

        bool SpawnOne()
        {
            candidates.Clear();
            Vector3 pp = Player != null ? Player.transform.position : Vector3.zero;
            for (int i = 0; i < burrows.Length; i++)
            {
                RatSpawnPoint b = burrows[i];
                if (b == null || b.tier > profile.maxSpawnTier) continue;
                if (Time.time - b.lastUsed < burrowCooldown) continue;
                float d = Vector3.Distance(new Vector3(pp.x, 0f, pp.z), new Vector3(b.transform.position.x, 0f, b.transform.position.z));
                if (d < minSpawnDistance || d > maxSpawnDistance) continue;
                candidates.Add(b);
            }
            if (candidates.Count == 0) return false;
            RatSpawnPoint pick = candidates[Random.Range(0, candidates.Count)];
            pick.lastUsed = Time.time;

            RatAI rat = Instantiate(ratTemplate, pick.transform.position, Quaternion.identity, transform);
            rat.name = "Rat";
            rat.transform.localScale = Vector3.one * Random.Range(ratScale.x, ratScale.y);
            rat.gameObject.SetActive(true);
            rat.Init(this, pick, profile);
            active.Add(rat);
            return true;
        }

        public void OnRatKilled(RatAI rat)
        {
            active.Remove(rat);
            spawnBudget = Mathf.Max(0, spawnBudget - 1);
        }

        public void OnRatEscaped(RatAI rat) { active.Remove(rat); }

        /// <summary>A gunshot at 'origin' and the bullet's impact at 'impact' scare rats within the hearing radius.</summary>
        public void NotifyNoise(Vector3 pos, float radiusScale)
        {
            float r = (profile != null ? profile.hearingRadius : 25f) * radiusScale;
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && !active[i].IsDead) active[i].HearNoise(pos, r);
        }

        // ------------------------------------------------------------------ queries used by rats

        public CropField RandomField(Vector3 near)
        {
            if (fields.Length == 0) return null;
            // prefer fields closer to the rat, but any field is possible
            CropField best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < fields.Length; i++)
            {
                CropField f = fields[i];
                if (f == null) continue;
                float score = Vector3.Distance(near, f.transform.position) * Random.Range(0.3f, 1.6f) / Mathf.Max(0.1f, f.weight);
                if (score < bestScore) { bestScore = score; best = f; }
            }
            return best;
        }

        public HideSpot FindHideSpot(Vector3 from, Vector3 threat)
        {
            HideSpot best = null;
            float bestScore = float.MaxValue;
            Vector3 toThreat = threat - from;
            toThreat.y = 0f;
            for (int i = 0; i < hideSpots.Length; i++)
            {
                HideSpot h = hideSpots[i];
                if (h == null) continue;
                Vector3 to = h.transform.position - from;
                to.y = 0f;
                float d = to.magnitude;
                if (d > 28f) continue;
                // running towards the farmer is a bad idea
                float towardsThreat = toThreat.sqrMagnitude > 0.01f ? Vector3.Dot(to.normalized, toThreat.normalized) : 0f;
                float score = d * (1f + Mathf.Max(0f, towardsThreat) * 2.5f) * Random.Range(0.7f, 1.3f);
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        public RatSpawnPoint FindEscapeBurrow(Vector3 from, Vector3 threat)
        {
            RatSpawnPoint best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < burrows.Length; i++)
            {
                RatSpawnPoint b = burrows[i];
                if (b == null) continue;
                float d = Vector3.Distance(from, b.transform.position);
                if (d > 35f) continue;
                float threatD = Vector3.Distance(threat, b.transform.position);
                if (threatD < 6f) continue;
                float score = d - threatD * 0.3f;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        public RatSpawnPoint NearestBurrow(Vector3 from)
        {
            RatSpawnPoint best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < burrows.Length; i++)
            {
                if (burrows[i] == null) continue;
                float d = (burrows[i].transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = burrows[i]; }
            }
            return best;
        }

        public Vector3 ClampToFarm(Vector3 p)
        {
            float h = farmHalfSize - 3f;
            p.x = Mathf.Clamp(p.x, -h, h);
            p.z = Mathf.Clamp(p.z, -h, h);
            p.y = FarmLayout.Height(p.x, p.z);
            return p;
        }

        /// <summary>Average crop health of all fields (0..1), weighted.</summary>
        public float CropHealth
        {
            get
            {
                float sum = 0f, w = 0f;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i] == null) continue;
                    sum += fields[i].health * fields[i].weight;
                    w += fields[i].weight;
                }
                return w > 0f ? sum / w : 1f;
            }
        }
    }
}
