using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Brain of a cartoon mouse (NavMesh agent + state machine). Mice wander to random spots, food and cover with
    /// randomised gaits (walk / run / dash), zig-zag paths, sudden stops and direction changes, then idle for a random
    /// time doing a random, non-repeating action (look around, stand up and sniff, groom, sit) before bolting again.
    /// Gunshots scare them: near misses make them startle-hop and flee at panic speed to a hiding spot behind cover
    /// (out of the player's line of sight) or into a burrow, then hide, peek out and carry on.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MouseAI : MonoBehaviour
    {
        public enum State { Emerging, Moving, Idle, Eating, Alert, Fleeing, Hiding, Peeking, Submerging, Burrowed, Dying }

        public MouseVariant variant = new MouseVariant();
        [Tooltip("Used for UI markers and head-shot checks.")]
        public Transform head;

        public State CurrentState { get { return state; } }
        public bool IsAlive { get { return state != State.Dying; } }
        public bool IsVisible { get { return IsAlive && state != State.Burrowed; } }
        public float AlarmTime { get; private set; }
        public int Points { get { return variant.points; } }
        public Vector3 HeadPosition { get { return head != null ? head.position : transform.position + Vector3.up * 0.3f; } }

        State state;
        NavMeshAgent agent;
        MouseRig rig;
        MouseSpawner spawner;
        Collider[] colliders;
        float speedMul = 1f, personal = 1f;
        float stateTime, stateDur;
        readonly List<Vector3> waypoints = new List<Vector3>();
        float moveSpeed;
        float dashTime;
        MouseFood food;
        MouseBurrow burrowTarget, lastBurrow;
        bool fleeToBurrow;
        bool hasHideSpot;
        Vector3 hideSpot;
        Vector3 faceTarget;
        bool faceSet;
        float lookTimer;
        int lastIdle = -1;
        int idleAction;
        float stuckTime;
        float squeakTimer;
        float nibbleTimer;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rig = GetComponent<MouseRig>();
            colliders = GetComponentsInChildren<Collider>(true);
            AlarmTime = -10f;
        }

        public void Init(MouseSpawner sp, MouseVariant v, float levelSpeed, MouseBurrow from)
        {
            spawner = sp;
            variant = v;
            speedMul = levelSpeed;
            personal = Random.Range(0.88f, 1.12f);
            agent.acceleration = 38f;
            agent.angularSpeed = 900f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);
            squeakTimer = Random.Range(3f, 9f);
            lastBurrow = from;
            Vector3 start = from != null ? from.Position : transform.position;
            Warp(start);
            if (from != null) Enter(State.Emerging, 0.55f);
            else Enter(State.Idle, Random.Range(0.3f, 1f));
        }

        bool Warp(Vector3 p)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 3f, NavMesh.AllAreas))
            {
                if (!agent.enabled) agent.enabled = true;
                agent.Warp(hit.position);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ state machine

        void Enter(State s, float duration)
        {
            if (state == State.Hiding && s != State.Hiding) ReleaseSpot();
            state = s;
            stateTime = 0f;
            stateDur = duration;
            faceSet = false;
            rig.shiver = false;
            bool moving = s == State.Moving || s == State.Fleeing;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = !moving;
            switch (s)
            {
                case State.Emerging:
                    rig.SetVisible(true);
                    SetColliders(true);
                    rig.sink = 1f;
                    rig.pose = MouseRig.Pose.Alert;
                    RatAudio.Play3D(RatSound.Pop, transform.position, 0.5f, Random.Range(0.9f, 1.2f));
                    RatFX.DirtPuff(transform.position, 0.6f);
                    break;
                case State.Idle:
                    PickIdleAction();
                    break;
                case State.Eating:
                    rig.pose = MouseRig.Pose.Eat;
                    nibbleTimer = 0.2f;
                    if (food != null) { faceTarget = food.Position; faceSet = true; }
                    break;
                case State.Alert:
                    rig.pose = MouseRig.Pose.Alert;
                    LookAtPlayer();
                    break;
                case State.Hiding:
                    rig.pose = MouseRig.Pose.Crouch;
                    rig.shiver = true;
                    break;
                case State.Peeking:
                    rig.pose = Random.value < 0.6f ? MouseRig.Pose.StandUp : MouseRig.Pose.Alert;
                    LookAtPlayer();
                    break;
                case State.Submerging:
                    rig.pose = MouseRig.Pose.Crouch;
                    break;
                case State.Burrowed:
                    rig.SetVisible(false);
                    SetColliders(false);
                    break;
                default:
                    rig.pose = MouseRig.Pose.Idle;
                    rig.lookYaw = 0f;
                    rig.lookPitch = 0f;
                    break;
            }
        }

        void ReleaseSpot()
        {
            if (hasHideSpot && spawner != null) spawner.ReleaseHideSpot(hideSpot);
            hasHideSpot = false;
        }

        /// <summary>Leaving a meal (for any reason) frees the spot for other mice.</summary>
        void StopEating()
        {
            if (state == State.Eating && food != null) food.eaters = Mathf.Max(0, food.eaters - 1);
            food = null;
        }

        void SetColliders(bool on)
        {
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = on;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || spawner == null) return;
            if (state == State.Dying) { rig.speed = 0f; return; }
            stateTime += dt;

            if (agent.enabled && !agent.isOnNavMesh && state != State.Burrowed) Warp(transform.position);

            switch (state)
            {
                case State.Emerging:
                    rig.sink = 1f - Mathf.SmoothStep(0f, 1f, stateTime / stateDur);
                    if (stateTime >= stateDur) { rig.sink = 0f; Enter(State.Idle, Random.Range(0.4f, 1.2f)); idleAction = 0; }
                    break;
                case State.Moving: UpdateMoving(dt, false); break;
                case State.Fleeing:
                    if (stateTime < 0.2f) { if (agent.isOnNavMesh) agent.isStopped = true; }   // startle freeze mid-hop
                    else UpdateMoving(dt, true);
                    break;
                case State.Idle: UpdateIdle(dt); break;
                case State.Eating:
                    nibbleTimer -= dt;
                    if (nibbleTimer <= 0f)
                    {
                        nibbleTimer = Random.Range(0.45f, 0.9f);
                        RatAudio.Play3D(RatSound.Nibble, transform.position, 0.28f, Random.Range(0.9f, 1.15f));
                        if (Random.value < 0.5f) RatFX.Crumbs(HeadPosition);
                    }
                    if (stateTime >= stateDur) PlanMove(false);
                    break;
                case State.Alert:
                    if (stateTime >= stateDur)
                    {
                        if (Random.value < 0.55f - variant.boldness * 0.35f) Panic(spawner.PlayerEye);
                        else PlanMove(true);
                    }
                    break;
                case State.Hiding:
                    if (stateTime >= stateDur) Enter(State.Peeking, Random.Range(0.5f, 1.6f) * (1.25f - variant.curiosity * 0.5f));
                    break;
                case State.Peeking:
                    if (stateTime >= stateDur)
                    {
                        if (Random.value < 0.4f * (1f - variant.boldness)) PlanSneak();
                        else PlanMove(Random.value < 0.6f);
                    }
                    break;
                case State.Submerging:
                    rig.sink = Mathf.SmoothStep(0f, 1f, stateTime / stateDur);
                    if (stateTime >= stateDur) Enter(State.Burrowed, Random.Range(2.5f, 6f));
                    break;
                case State.Burrowed:
                    if (stateTime >= stateDur)
                    {
                        MouseBurrow b = spawner.RandomBurrow(lastBurrow);
                        if (b != null && Warp(b.Position))
                        {
                            lastBurrow = b;
                            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        }
                        Enter(State.Emerging, 0.55f);
                    }
                    break;
            }

            // stationary states turn on the spot towards what they care about
            if (faceSet && (agent.isStopped || !agent.enabled))
            {
                Vector3 d = faceTarget - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * dt);
            }

            Vector3 vel = agent.enabled ? agent.velocity : Vector3.zero;
            vel.y = 0f;
            rig.speed = vel.magnitude;

            // chatter
            squeakTimer -= dt;
            if (squeakTimer <= 0f)
            {
                squeakTimer = Random.Range(4f, 11f);
                if (state != State.Burrowed && state != State.Hiding)
                    RatAudio.Play3D(Random.value < 0.5f ? RatSound.Squeak1 : (Random.value < 0.5f ? RatSound.Squeak2 : RatSound.Squeak3), transform.position, 0.4f, Random.Range(0.85f, 1.2f) / Mathf.Sqrt(variant.scale));
            }
        }

        void UpdateMoving(float dt, bool fleeing)
        {
            float spd = moveSpeed;
            if (dashTime > 0f) { dashTime -= dt; spd *= 1.35f; }
            agent.speed = spd * speedMul * personal;
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;

            if (!agent.pathPending)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid) { if (fleeing) ArriveFlee(); else PlanMove(false); return; }
                if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                {
                    if (waypoints.Count > 0)
                    {
                        agent.SetDestination(waypoints[0]);
                        waypoints.RemoveAt(0);
                        // a sharp cartoon direction change: tiny speed burst
                        if (variant.erratic > 0.5f) dashTime = Mathf.Max(dashTime, 0.25f);
                    }
                    else if (fleeing) ArriveFlee();
                    else Arrive();
                    return;
                }
            }

            if (!fleeing)
            {
                // unpredictable: random sudden stops and sudden changes of mind
                if (Random.value < dt * (0.08f + variant.curiosity * 0.12f)) { Enter(State.Idle, Random.Range(0.4f, 1.3f)); return; }
                if (Random.value < dt * variant.erratic * 0.22f) { PlanMove(Random.value < 0.5f); return; }
            }

            // stuck?
            if (agent.velocity.sqrMagnitude < 0.0025f && stateTime > 0.6f) stuckTime += dt; else stuckTime = 0f;
            if (stuckTime > 1.2f) { stuckTime = 0f; if (fleeing) ArriveFlee(); else PlanMove(true); }
        }

        void Arrive()
        {
            if (food != null)
            {
                food.eaters++;
                Enter(State.Eating, Random.Range(2f, 5f) * (0.5f + variant.greed));
            }
            else Enter(State.Idle, IdleDuration());
        }

        void ArriveFlee()
        {
            if (fleeToBurrow && burrowTarget != null)
            {
                lastBurrow = burrowTarget;
                fleeToBurrow = false;
                Enter(State.Submerging, 0.35f);
                RatAudio.Play3D(RatSound.Pop, transform.position, 0.45f, 0.8f);
                RatFX.DirtPuff(transform.position, 0.5f);
                return;
            }
            Enter(State.Hiding, Random.Range(1.5f, 4.5f) * Mathf.Lerp(1.4f, 0.7f, variant.boldness));
        }

        float IdleDuration() { return Random.Range(0.6f, 2.8f) * Mathf.Lerp(0.7f, 1.5f, variant.boldness); }

        // ------------------------------------------------------------------ idle

        void PickIdleAction()
        {
            float[] w =
            {
                1f,                                   // 0 look around
                0.5f + variant.curiosity,             // 1 stand up and sniff
                0.35f,                                // 2 groom
                0.5f,                                 // 3 sit and sniff
                variant.curiosity * 0.7f              // 4 stare at the farmer
            };
            if (lastIdle >= 0) w[lastIdle] *= 0.15f;
            float sum = 0f;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            float r = Random.value * sum;
            int a = 0;
            for (; a < w.Length - 1; a++) { r -= w[a]; if (r <= 0f) break; }
            idleAction = a;
            lastIdle = a;
            lookTimer = 0f;
            switch (a)
            {
                case 1: rig.pose = MouseRig.Pose.StandUp; break;
                case 2: rig.pose = MouseRig.Pose.Groom; break;
                case 4: rig.pose = MouseRig.Pose.Alert; LookAtPlayer(); break;
                default: rig.pose = MouseRig.Pose.Idle; break;
            }
        }

        void UpdateIdle(float dt)
        {
            lookTimer -= dt;
            if (lookTimer <= 0f && idleAction != 2)
            {
                lookTimer = Random.Range(0.35f, 0.9f);
                if (idleAction == 4) LookAtPlayer();
                else { rig.lookYaw = Random.Range(-70f, 70f); rig.lookPitch = Random.Range(-15f, 10f); }
            }
            if (stateTime >= stateDur)
            {
                // ...and suddenly runs off again
                PlanMove(Random.value < 0.35f + variant.erratic * 0.3f);
            }
        }

        void LookAtPlayer()
        {
            if (spawner == null) return;
            Vector3 d = spawner.PlayerEye - transform.position;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float yaw = Vector3.SignedAngle(transform.forward, flat, Vector3.up);
            if (Mathf.Abs(yaw) > 70f) { faceTarget = spawner.PlayerEye; faceSet = true; yaw = Mathf.Clamp(yaw, -70f, 70f); }
            rig.lookYaw = yaw;
            rig.lookPitch = -Mathf.Clamp(Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, 0f, 25f);
        }

        // ------------------------------------------------------------------ planning

        void PlanMove(bool hurry)
        {
            if (!agent.isOnNavMesh) { Enter(State.Idle, 0.5f); return; }
            StopEating();
            Vector3 pos = transform.position;
            Vector3 goal = pos;
            bool found = false;

            float wFood = spawner.HasFood ? 0.25f + variant.greed * 1.4f : 0f;
            float wOpen = 1f;
            float wSneak = (1f - variant.boldness) * 0.55f;
            float r = Random.value * (wFood + wOpen + wSneak);
            if (r < wFood)
            {
                MouseFood f = spawner.PickFood(pos);
                if (f != null)
                {
                    Vector2 c = Random.insideUnitCircle * f.radius;
                    goal = f.Position + new Vector3(c.x, 0f, c.y);
                    food = f;
                    found = true;
                }
            }
            else if (r < wFood + wSneak)
            {
                found = spawner.FindHideSpot(pos, spawner.PlayerEye, out goal, false);
            }
            if (!found && Random.value < 0.65f) found = spawner.RandomYardPoint(pos, 3f, out goal);
            if (!found) found = spawner.RandomPoint(pos, 3.5f, 16f, out goal);
            if (!found) { Enter(State.Idle, 1f); return; }

            BuildPath(goal, Random.value < variant.erratic * 0.8f + (hurry ? 0.15f : 0f));
            bool walk = !hurry && Random.value < 0.4f * (1f - variant.erratic);
            moveSpeed = walk ? variant.walkSpeed : variant.runSpeed * Random.Range(0.85f, 1.1f);
            dashTime = hurry || Random.value < 0.2f + variant.erratic * 0.3f ? Random.Range(0.35f, 1f) : 0f;
            Enter(State.Moving, 0f);
        }

        void PlanSneak()
        {
            StopEating();
            Vector3 goal;
            if (spawner.FindHideSpot(transform.position, spawner.PlayerEye, out goal, false))
            {
                BuildPath(goal, false);
                moveSpeed = variant.runSpeed;
                dashTime = 0.5f;
                food = null;
                Enter(State.Moving, 0f);
            }
            else PlanMove(true);
        }

        void BuildPath(Vector3 goal, bool zigzag)
        {
            waypoints.Clear();
            Vector3 pos = transform.position;
            if (zigzag)
            {
                Vector3 dir = goal - pos;
                dir.y = 0f;
                float len = dir.magnitude;
                if (len > 3f)
                {
                    Vector3 perp = Vector3.Cross(Vector3.up, dir / len);
                    int n = Mathf.Clamp(Mathf.FloorToInt(len / 3f), 1, 3);
                    float sign = Random.value < 0.5f ? 1f : -1f;
                    for (int k = 1; k <= n; k++)
                    {
                        Vector3 b = Vector3.Lerp(pos, goal, (float)k / (n + 1)) + perp * sign * Random.Range(0.8f, 2.2f);
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(b, out hit, 1.2f, NavMesh.AllAreas)) waypoints.Add(hit.position);
                        sign = -sign;
                    }
                }
            }
            waypoints.Add(goal);
            agent.isStopped = false;
            agent.SetDestination(waypoints[0]);
            waypoints.RemoveAt(0);
        }

        // ------------------------------------------------------------------ reactions

        /// <summary>A shot was fired. Near misses make the mouse flee and hide; distant ones may only startle it.</summary>
        public void OnGunshot(Vector3 rayOrigin, Vector3 rayDir, Vector3 impact)
        {
            if (!IsAlive || state == State.Burrowed || state == State.Submerging) return;
            Vector3 pos = transform.position + Vector3.up * 0.15f;
            float dImpact = Vector3.Distance(pos, impact);
            float along = Vector3.Dot(pos - rayOrigin, rayDir);
            float maxAlong = Vector3.Distance(rayOrigin, impact) + 1f;
            float dRay = along > 0f && along < maxAlong ? Vector3.Distance(pos, rayOrigin + rayDir * along) : float.MaxValue;
            float near = Mathf.Min(dImpact, dRay);

            if (state == State.Fleeing)
            {
                if (near < 3f && Random.value < 0.6f) Panic(impact);
                return;
            }
            if (state == State.Hiding)
            {
                if (near < 2.5f) Panic(impact);
                else stateTime = Mathf.Max(0f, stateTime - 1.5f);   // stays hidden a bit longer
                return;
            }
            if (near < 2.8f) Panic(impact);
            else if (near < 7.5f) { if (Random.value < 0.85f - variant.boldness * 0.5f) Panic(impact); else Startle(); }
            else if (Random.value < 0.6f - variant.boldness * 0.4f) Startle();
        }

        void Startle()
        {
            StopEating();
            AlarmTime = Time.time;
            rig.Hop(Random.Range(0.06f, 0.12f));
            Enter(State.Alert, Random.Range(0.5f, 1.3f));
            if (Random.value < 0.6f) RatAudio.Play3D(RatSound.Squeak2, transform.position, 0.45f, Random.Range(1f, 1.25f));
        }

        public void Panic(Vector3 threat)
        {
            if (!IsAlive || spawner == null) return;
            AlarmTime = Time.time;
            rig.Hop(Random.Range(0.14f, 0.24f));
            RatAudio.Play3D(RatSound.Eek, transform.position, 0.6f, Random.Range(0.92f, 1.15f) / Mathf.Sqrt(variant.scale));
            StopEating();
            ReleaseSpot();

            Vector3 goal = transform.position;
            fleeToBurrow = false;
            bool found = false;
            if (Random.value < variant.burrowChance)
            {
                MouseBurrow b = spawner.NearestBurrow(transform.position, 14f, threat);
                if (b != null) { burrowTarget = b; goal = b.Position; fleeToBurrow = true; found = true; }
            }
            if (!found && spawner.FindHideSpot(transform.position, threat, out goal, true))
            {
                found = true;
                hasHideSpot = true;
                hideSpot = goal;
            }
            if (!found) found = spawner.RandomPointAway(transform.position, threat, out goal);
            if (!found) { Enter(State.Alert, 0.5f); return; }

            if (!agent.isOnNavMesh) Warp(transform.position);
            if (!agent.isOnNavMesh) return;
            BuildPath(goal, Random.value < variant.erratic + 0.3f);
            moveSpeed = variant.panicSpeed;
            dashTime = 0.6f;
            Enter(State.Fleeing, 0f);
            agent.isStopped = true;   // released after the startle hop
        }

        /// <summary>Shot! Cartoon death, then removed from play.</summary>
        public void Kill(Vector3 hitPoint, Vector3 shotDir, bool headshot)
        {
            if (!IsAlive) return;
            StopEating();
            ReleaseSpot();
            Enter(State.Dying, 0f);
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.enabled = false;
            SetColliders(false);
            rig.speed = 0f;
            rig.sink = 0f;
            rig.onLanded = p => { RatAudio.Play3D(RatSound.Thud, p, 0.6f); RatFX.DirtPuff(p, 0.7f); };
            rig.PlayDeath(shotDir, p =>
            {
                RatFX.Poof(p, variant.scale);
                RatAudio.Play3D(RatSound.Poof, p, 0.7f, Random.Range(0.95f, 1.1f));
            }, () =>
            {
                if (spawner != null) spawner.NotifyRemoved(this);
                Destroy(gameObject);
            });
        }

        void OnDestroy()
        {
            StopEating();
            ReleaseSpot();
        }
    }
}
