using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Spawns and tracks the mice: pops them out of burrows during a level (up to a maximum at once, with a mix of
    /// variants per level), keeps a few wandering in "attract mode" behind the title screen, owns the mouse NavMesh,
    /// computes hiding spots behind cover (out of the player's sight), hands out food and burrows, and relays
    /// gunshots so every mouse can react.
    /// </summary>
    public class MouseSpawner : MonoBehaviour
    {
        public static MouseSpawner Instance { get; private set; }

        [Header("Mice")]
        public MouseVariant[] variants = MouseVariant.Defaults();
        [Tooltip("Where the farmer stands (camera). Used for hiding-spot visibility checks.")]
        public Transform player;

        [Header("Play area")]
        public Bounds playArea = new Bounds(new Vector3(0f, 0f, 17f), new Vector3(34f, 8f, 26f));

        [Header("NavMesh")]
        [Tooltip("Baked by Neon Breach > Rat Shooter > Build Rat Shooter Scene. If empty, a NavMesh is built when the scene starts.")]
        public NavMeshData navMeshData;
        public LayerMask navMeshLayers = 1 << 10;
        public float agentRadius = 0.14f;
        public float agentHeight = 0.3f;
        public float agentClimb = 0.12f;
        public float agentSlope = 35f;
        public float voxelSize = 0.06f;

        [Header("Attract mode (title screen)")]
        public int attractCount = 4;

        public readonly List<MouseAI> Active = new List<MouseAI>();
        public bool NavReady { get; private set; }
        public int SpawnedThisLevel { get; private set; }
        public Vector3 PlayerEye { get { return player != null ? player.position : new Vector3(0f, 2.2f, -2f); } }
        public bool HasFood { get { return foods != null && foods.Length > 0; } }

        MouseBurrow[] burrows;
        MouseFood[] foods;
        RatCover[] covers;
        readonly List<Vector3> hideSpots = new List<Vector3>();
        readonly List<Vector3> claimed = new List<Vector3>();
        NavMeshDataInstance navInstance;

        bool attract, running;
        RatLevel level;
        float speedMul = 1f;
        float spawnTimer;
        int levelTotal;

        void Awake()
        {
            Instance = this;
            burrows = FindObjectsByType<MouseBurrow>();
            foods = FindObjectsByType<MouseFood>();
            covers = FindObjectsByType<RatCover>();
        }

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

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            while (!NavReady) yield return null;
            yield return null;
            ComputeHideSpots();
        }

        // ------------------------------------------------------------------ navmesh

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
            s.tileSize = 128;
            s.minRegionArea = 1f;
            return s;
        }

        public Bounds BuildBounds { get { Bounds b = playArea; b.Expand(new Vector3(2f, 4f, 2f)); return b; } }

        IEnumerator BuildNavMeshRuntime()
        {
            Physics.SyncTransforms();
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(BuildBounds, navMeshLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings s = BuildSettings();
            NavMeshData data = new NavMeshData(s.agentTypeID);
            navInstance = NavMesh.AddNavMeshData(data);
            AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(data, s, sources, BuildBounds);
            while (!op.isDone) yield return null;
            NavReady = true;
            Debug.Log("[Rat Shooter] Mouse NavMesh built at runtime.");
        }

        // ------------------------------------------------------------------ hiding spots

        void ComputeHideSpots()
        {
            hideSpots.Clear();
            Vector3 eye = PlayerEye;
            int mask = Layers.EnvMask;
            foreach (RatCover c in covers)
            {
                if (c == null) continue;
                Vector3 cp = c.transform.position;
                if (c.soft)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = cp + new Vector3(Random.Range(-c.softHalfSize.x, c.softHalfSize.x) * 0.8f, 0f, Random.Range(-c.softHalfSize.y, c.softHalfSize.y) * 0.8f);
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(p, out hit, 0.8f, NavMesh.AllAreas)) hideSpots.Add(hit.position);
                    }
                    continue;
                }
                Vector3 away = cp - eye;
                away.y = 0f;
                away.Normalize();
                for (int i = 0; i < 3; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, (i - 1) * 32f + Random.Range(-8f, 8f), 0f) * away;
                    Vector3 p = cp + dir * (c.radius + 0.3f);
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(p, out hit, 0.9f, NavMesh.AllAreas)) continue;
                    if (Physics.Linecast(eye, hit.position + Vector3.up * 0.15f, mask, QueryTriggerInteraction.Ignore))
                        hideSpots.Add(hit.position);
                }
            }
            Debug.Log("[Rat Shooter] " + hideSpots.Count + " hiding spots behind " + covers.Length + " covers.");
        }

        /// <summary>Best hiding spot for a mouse at 'from' running away from 'threat'.</summary>
        public bool FindHideSpot(Vector3 from, Vector3 threat, out Vector3 spot, bool claim)
        {
            spot = from;
            float best = float.MinValue;
            bool found = false;
            for (int i = 0; i < hideSpots.Count; i++)
            {
                Vector3 h = hideSpots[i];
                float d = Vector3.Distance(from, h);
                if (d < 1.2f || d > 16f) continue;
                if (IsClaimed(h)) continue;
                float threatD = Vector3.Distance(new Vector3(threat.x, h.y, threat.z), h);
                float score = threatD * 0.6f - Mathf.Abs(d - 6f) * 0.5f + Random.Range(0f, 4f);
                // don't run straight through the bullet impact
                Vector3 toSpot = (h - from).normalized, toThreat = (threat - from);
                toThreat.y = 0f;
                if (toThreat.magnitude < 6f && Vector3.Dot(toSpot, toThreat.normalized) > 0.6f) score -= 6f;
                if (score > best) { best = score; spot = h; found = true; }
            }
            if (found && claim) claimed.Add(spot);
            return found;
        }

        bool IsClaimed(Vector3 p)
        {
            for (int i = 0; i < claimed.Count; i++) if ((claimed[i] - p).sqrMagnitude < 0.04f) return true;
            return false;
        }

        public void ReleaseHideSpot(Vector3 p)
        {
            for (int i = claimed.Count - 1; i >= 0; i--) if ((claimed[i] - p).sqrMagnitude < 0.04f) { claimed.RemoveAt(i); return; }
        }

        // ------------------------------------------------------------------ random points / burrows / food

        public bool RandomPoint(Vector3 near, float minDist, float maxDist, out Vector3 p)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(minDist, maxDist);
                Vector3 q = near + new Vector3(c.x, 0f, c.y);
                if (!playArea.Contains(new Vector3(q.x, playArea.center.y, q.z))) continue;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(q, out hit, 1.5f, NavMesh.AllAreas)) { p = hit.position; return true; }
            }
            p = near;
            return false;
        }

        [Tooltip("Where mice like to roam (x, z, width, depth): the open middle of the yard, in good view of the porch.")]
        public Rect favouriteArea = new Rect(-14f, 4f, 28f, 20f);

        /// <summary>A random reachable point in the favourite area at least minDist away from 'from'.</summary>
        public bool RandomYardPoint(Vector3 from, float minDist, out Vector3 p)
        {
            for (int tries = 0; tries < 16; tries++)
            {
                Vector3 q = new Vector3(Random.Range(favouriteArea.xMin, favouriteArea.xMax), 0f, Random.Range(favouriteArea.yMin, favouriteArea.yMax));
                if ((q - from).sqrMagnitude < minDist * minDist) continue;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(q, out hit, 1.5f, NavMesh.AllAreas)) { p = hit.position; return true; }
            }
            p = from;
            return false;
        }

        public bool RandomPointAway(Vector3 from, Vector3 threat, out Vector3 p)
        {
            Vector3 away = from - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();
            for (int tries = 0; tries < 16; tries++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away;
                Vector3 q = from + dir * Random.Range(4f, 10f);
                if (!playArea.Contains(new Vector3(q.x, playArea.center.y, q.z))) continue;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(q, out hit, 1.5f, NavMesh.AllAreas)) { p = hit.position; return true; }
            }
            return RandomPoint(from, 3f, 10f, out p);
        }

        public MouseBurrow NearestBurrow(Vector3 pos, float maxDist, Vector3 threat)
        {
            MouseBurrow best = null;
            float bd = maxDist;
            foreach (MouseBurrow b in burrows)
            {
                if (b == null) continue;
                float d = Vector3.Distance(pos, b.Position);
                if (Vector3.Distance(threat, b.Position) < 2.5f) continue;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public MouseBurrow RandomBurrow(MouseBurrow exclude)
        {
            if (burrows == null || burrows.Length == 0) return null;
            for (int i = 0; i < 8; i++)
            {
                MouseBurrow b = burrows[Random.Range(0, burrows.Length)];
                if (b != exclude && b.spawnPoint) return b;
            }
            return burrows[Random.Range(0, burrows.Length)];
        }

        public MouseFood PickFood(Vector3 pos)
        {
            if (!HasFood) return null;
            MouseFood best = null;
            float bs = float.MinValue;
            foreach (MouseFood f in foods)
            {
                if (f == null) continue;
                float s = f.appeal * 3f - Vector3.Distance(pos, f.Position) * 0.15f - f.eaters * 2.5f + Random.Range(0f, 3f);
                if (s > bs) { bs = s; best = f; }
            }
            return best;
        }

        // ------------------------------------------------------------------ waves

        /// <summary>A few mice wander about behind the title screen; they are not counted.</summary>
        public void BeginAttract()
        {
            ClearAll();
            attract = true;
            running = true;
            level = null;
            speedMul = 0.9f;
            spawnTimer = 0.3f;
        }

        public void BeginLevel(RatLevel lvl, float speedMultiplier)
        {
            ClearAll();
            attract = false;
            running = true;
            level = lvl;
            levelTotal = lvl.mice;
            speedMul = lvl.speed * speedMultiplier;
            SpawnedThisLevel = 0;
            spawnTimer = 0.2f;
        }

        public void Stop() { running = false; }

        public void ClearAll()
        {
            running = false;
            for (int i = Active.Count - 1; i >= 0; i--) if (Active[i] != null) Destroy(Active[i].gameObject);
            Active.Clear();
            claimed.Clear();
            if (foods != null) foreach (MouseFood f in foods) if (f != null) f.eaters = 0;
        }

        void Update()
        {
            if (!running || !NavReady || Time.deltaTime <= 0f) return;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            if (attract)
            {
                if (Active.Count < attractCount) { Spawn(PickVariant(null)); spawnTimer = Random.Range(1.2f, 3f); }
                return;
            }
            if (level == null) return;
            if (SpawnedThisLevel < levelTotal && Active.Count < level.maxActive)
            {
                if (Spawn(PickVariant(level)) != null) SpawnedThisLevel++;
                spawnTimer = Random.Range(level.spawnInterval * 0.6f, level.spawnInterval * 1.4f);
            }
        }

        MouseVariant PickVariant(RatLevel lvl)
        {
            float[] w = new float[variants.Length];
            float sum = 0f;
            for (int i = 0; i < variants.Length; i++)
            {
                float wi = lvl != null ? lvl.Weight(variants[i].id) : (variants[i].id == "golden" ? 0.2f : 1f);
                if (variants[i].id == "golden" && CountAlive("golden") > 0) wi = 0f;
                w[i] = wi;
                sum += wi;
            }
            if (sum <= 0f) return variants[0];
            float r = Random.value * sum;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0f) return variants[i]; }
            return variants[variants.Length - 1];
        }

        int CountAlive(string id)
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++) if (Active[i] != null && Active[i].IsAlive && Active[i].variant.id == id) n++;
            return n;
        }

        public MouseAI Spawn(MouseVariant v)
        {
            MouseBurrow b = RandomBurrow(null);
            Vector3 pos = b != null ? b.Position : playArea.center;
            GameObject go = v.prefab != null ? Instantiate(v.prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform)
                                            : MouseFactory.Build(v);
            if (v.prefab == null)
            {
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            go.name = "Mouse_" + v.id;
            MouseAI ai = go.GetComponent<MouseAI>();
            ai.Init(this, v, speedMul, b);
            Active.Add(ai);
            if (!attract && v.id == "golden" && RatShooterGame.Instance != null) RatShooterGame.Instance.Announce("GOLDEN MOUSE!", "Worth " + v.points + " points - be quick!");
            return ai;
        }

        public void NotifyRemoved(MouseAI m) { Active.Remove(m); }

        /// <summary>Tell every mouse a shot was fired (the one that was hit is already dying and ignores it).</summary>
        public void BroadcastGunshot(Vector3 rayOrigin, Vector3 rayDir, Vector3 impact)
        {
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null) Active[i].OnGunshot(rayOrigin, rayDir, impact);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawWireCube(playArea.center, playArea.size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            foreach (Vector3 h in hideSpots) Gizmos.DrawSphere(h, 0.12f);
        }
    }
}
