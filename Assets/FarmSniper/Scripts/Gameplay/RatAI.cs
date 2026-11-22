using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Field rat. Emerges from a burrow, scurries to a field in short zig-zag bursts, feeds on the crop (damaging it),
    /// and panics at gunshots, nearby impacts or a farmer walking up: it rears up, then zig-zags to cover (hay, crates,
    /// tall corn), to a burrow, or just away. Hidden rats peek out and later sneak back to feed or escape underground.
    /// Moves on the baked NavMesh; if there is none it falls back to simple ground-following steering.
    /// Animation is procedural (bounding legs, swaying tail, sniffing head, rearing up, a tumble when hit).
    /// </summary>
    public class RatAI : MonoBehaviour
    {
        public enum RatState { Emerging, Roaming, Feeding, Alert, Fleeing, Hiding, Peeking, Escaping, Dead }

        [Header("Speeds (m/s, before the difficulty multiplier)")]
        public float walkSpeed = 1.3f;
        public float runSpeed = 3.6f;
        public float fleeSpeed = 5.6f;
        public float acceleration = 32f;
        public float angularSpeed = 900f;

        [Header("Timing (seconds, random in range)")]
        public Vector2 feedTime = new Vector2(3f, 8f);
        public Vector2 alertTime = new Vector2(0.3f, 0.9f);
        public Vector2 hideTime = new Vector2(3f, 7f);
        public Vector2 scurryBurst = new Vector2(0.5f, 1.6f);
        public Vector2 scurryPause = new Vector2(0.15f, 0.7f);
        public Vector2 zigzagInterval = new Vector2(0.3f, 0.7f);

        [Header("Behaviour")]
        [Tooltip("Sideways distance of a flee zig-zag (scaled by the difficulty's zig-zag strength).")]
        public float zigzagWidth = 3f;
        [Tooltip("A walking farmer this close scares the rat. Crouching shrinks it.")]
        public float playerAlertDistance = 11f;
        [Range(0f, 1f)] public float crouchAlertMultiplier = 0.45f;
        [Range(0f, 1f)] public float peekChance = 0.6f;
        [Range(0f, 1f)] public float escapeChanceAfterHiding = 0.3f;
        [Range(0f, 1f)] public float squeakChance = 0.3f;
        [Tooltip("After this long a rat heads home to a burrow.")]
        public float maxLifetime = 150f;

        [Header("Model (assigned by the scene builder)")]
        public Transform model;
        public Transform head;
        public Transform[] legs = new Transform[0];
        public Transform[] tail = new Transform[0];
        public Collider[] hitColliders = new Collider[0];

        public RatState State { get; private set; }
        public bool IsDead { get { return State == RatState.Dead; } }
        public float Speed { get { return useAgent ? agent.velocity.magnitude : fallbackSpeed; } }
        /// <summary>Counts as a moving-target kill (bonus score).</summary>
        public bool IsRunning { get { return !IsDead && Speed > walkSpeed * speedMul * 1.4f; } }

        NavMeshAgent agent;
        bool useAgent;
        RatSpawner spawner;
        DifficultyProfile prof;
        float speedMul = 1f;
        float stateUntil, burstUntil, nextZig, nextSense, aliveSince, stuckTime;
        bool pausing;
        Vector3 goal, threat, hidePoint, peekPoint;
        CropField field;
        RatSpawnPoint homeBurrow;
        bool fleeToHide, fleeToBurrow;
        float emerge;     // 0 = underground, 1 = out
        float rear;       // 0 = on all fours, 1 = up on hind legs
        float phase;
        float deathTime;
        Vector3 deathAxis;
        Vector3 fallbackGoal;
        float fallbackSpeed, fallbackWant;
        Quaternion modelBaseRot = Quaternion.identity;
        Vector3 modelBasePos;

        public void Init(RatSpawner owner, RatSpawnPoint burrow, DifficultyProfile p)
        {
            spawner = owner;
            prof = p;
            speedMul = p != null ? p.speedMultiplier : 1f;
            homeBurrow = burrow;
            aliveSince = Time.time;
            if (model != null) { modelBasePos = model.localPosition; modelBaseRot = model.localRotation; }
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                agent.radius = 0.12f;
                agent.height = 0.24f;
                agent.baseOffset = 0f;
                agent.acceleration = acceleration;
                agent.angularSpeed = angularSpeed;
                agent.stoppingDistance = 0.25f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(30, 70);
            }
            emerge = 0f;
            SetState(RatState.Emerging, 0.7f);
            if (Random.value < squeakChance) Squeak(1f);
        }

        // ------------------------------------------------------------------ movement helpers

        void EnableMovement()
        {
            NavMeshHit hit;
            if (agent != null && NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                useAgent = agent.isOnNavMesh;
            }
            if (!useAgent && agent != null) agent.enabled = false;
        }

        void MoveTo(Vector3 p, float speed)
        {
            float s = speed * speedMul;
            if (useAgent)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(p, out hit, 4f, NavMesh.AllAreas)) p = hit.position;
                agent.speed = s;
                agent.isStopped = false;
                agent.SetDestination(p);
            }
            else
            {
                fallbackGoal = p;
                fallbackWant = s;
            }
        }

        void Stop()
        {
            if (useAgent) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            else fallbackWant = 0f;
        }

        float DistTo(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        bool Arrived(Vector3 p, float tol)
        {
            if (useAgent && agent.pathPending) return false;
            return DistTo(p) <= tol;
        }

        /// <summary>Waypoint towards target with a sideways offset (zig-zag / scurry).</summary>
        Vector3 Zig(Vector3 target, float maxStep, float width)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 0.01f) return target;
            Vector3 dir = to / d;
            Vector3 perp = Vector3.Cross(Vector3.up, dir);
            float step = Mathf.Min(d, maxStep);
            if (d <= maxStep * 0.8f) return target;
            return transform.position + dir * step + perp * Random.Range(-1f, 1f) * width;
        }

        void SetState(RatState s, float duration)
        {
            State = s;
            stateUntil = Time.time + duration;
        }

        // ------------------------------------------------------------------ behaviours

        void StartRoaming(bool sneaky)
        {
            field = spawner != null ? spawner.RandomField(transform.position) : null;
            goal = field != null ? field.RandomPoint() : transform.position + Random.insideUnitSphere * 10f;
            goal.y = FarmLayout.Height(goal.x, goal.z);
            SetState(RatState.Roaming, 999f);
            pausing = true;
            burstUntil = Time.time + (sneaky ? Random.Range(0.6f, 1.5f) : Random.Range(scurryPause.x, scurryPause.y));
            Stop();
        }

        void StartFeeding()
        {
            Stop();
            SetState(RatState.Feeding, Random.Range(feedTime.x, feedTime.y));
            if (field != null) transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        void Alert(Vector3 source, bool immediate)
        {
            if (IsDead || State == RatState.Escaping || State == RatState.Emerging) return;
            threat = source;
            if (State == RatState.Fleeing && !immediate) return;
            if (immediate || State == RatState.Hiding || State == RatState.Peeking)
            {
                StartFlee();
                return;
            }
            Stop();
            SetState(RatState.Alert, Random.Range(alertTime.x, alertTime.y));
            if (Random.value < squeakChance * 1.5f) Squeak(1.1f);
        }

        void StartFlee()
        {
            float hideChance = prof != null ? prof.hideChance : 0.6f;
            fleeToHide = fleeToBurrow = false;
            Vector3 away = transform.position - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();

            HideSpot spot = null;
            if (spawner != null && Random.value < hideChance) spot = spawner.FindHideSpot(transform.position, threat);
            RatSpawnPoint burrow = null;
            if (spot == null && spawner != null && Random.value < 0.5f) burrow = spawner.FindEscapeBurrow(transform.position, threat);

            if (spot != null) { hidePoint = spot.PointBehind(threat); goal = hidePoint; fleeToHide = true; }
            else if (burrow != null) { homeBurrow = burrow; goal = burrow.transform.position; fleeToBurrow = true; }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, away) * Random.Range(-8f, 8f);
                goal = transform.position + away * Random.Range(12f, 22f) + side;
                goal = spawner != null ? spawner.ClampToFarm(goal) : goal;
            }
            SetState(RatState.Fleeing, 25f);
            nextZig = 0f;
        }

        void StartEscape()
        {
            if (spawner != null)
            {
                RatSpawnPoint b = spawner.NearestBurrow(transform.position);
                if (b != null) homeBurrow = b;
            }
            goal = homeBurrow != null ? homeBurrow.transform.position : transform.position;
            fleeToBurrow = true;
            SetState(RatState.Escaping, 30f);
            MoveTo(goal, runSpeed);
        }

        /// <summary>Gunshot or bullet impact heard at 'at' within 'radius' metres.</summary>
        public void HearNoise(Vector3 at, float radius)
        {
            if (IsDead) return;
            float d = DistTo(at);
            if (d > radius) return;
            Alert(at, d < radius * 0.35f || d < 5f);
        }

        public void Kill(Vector3 hitPoint, Vector3 bulletDir, bool headshot)
        {
            if (IsDead) return;
            State = RatState.Dead;
            deathTime = Time.time;
            if (useAgent) { agent.isStopped = true; agent.enabled = false; }
            useAgent = false;
            for (int i = 0; i < hitColliders.Length; i++) if (hitColliders[i] != null) hitColliders[i].enabled = false;
            Vector3 flat = bulletDir;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f) flat = transform.forward;
            deathAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, flat.normalized));
            Squeak(1.35f);
            // small puff of dust and fur - no gore
            FX.Burst(hitPoint, new Color(0.55f, 0.50f, 0.44f, 0.8f), 7, 1.2f, 0.06f, 0.5f);
            if (spawner != null) spawner.OnRatKilled(this);
        }

        void Squeak(float pitch)
        {
            FarmAudio.Play3D(FarmSound.RatSqueak, transform.position + Vector3.up * 0.1f, 0.5f, pitch * Random.Range(0.9f, 1.15f), 1.5f, 40f);
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (State)
            {
                case RatState.Emerging:
                    emerge = Mathf.MoveTowards(emerge, 1f, dt / 0.7f);
                    if (emerge >= 1f) { EnableMovement(); StartRoaming(false); }
                    break;

                case RatState.Roaming:
                    if (pausing)
                    {
                        if (now >= burstUntil)
                        {
                            pausing = false;
                            float change = prof != null ? prof.directionChangeChance : 0.25f;
                            if (Random.value < change && spawner != null)
                            {
                                field = spawner.RandomField(transform.position);
                                if (field != null) goal = field.RandomPoint();
                            }
                            bool run = Random.value < 0.6f;
                            MoveTo(Zig(goal, run ? 7f : 4f, 1.6f), run ? runSpeed : walkSpeed);
                            burstUntil = now + Random.Range(scurryBurst.x, scurryBurst.y);
                        }
                    }
                    else if (now >= burstUntil)
                    {
                        pausing = true;
                        burstUntil = now + Random.Range(scurryPause.x, scurryPause.y);
                        Stop();
                    }
                    if (Arrived(goal, 1.2f)) StartFeeding();
                    break;

                case RatState.Feeding:
                    if (field != null && prof != null && field.Contains(transform.position, 0.5f))
                        field.Damage(prof.cropDamagePerSecond * dt);
                    if (now >= stateUntil)
                    {
                        if (field != null && Random.value < 0.55f)
                        {
                            goal = field.RandomPoint();
                            Vector3 near = transform.position + (goal - transform.position).normalized * Mathf.Min(6f, DistTo(goal));
                            goal = field.Contains(near) ? near : goal;
                            SetState(RatState.Roaming, 999f);
                            pausing = true;
                            burstUntil = now + 0.2f;
                        }
                        else StartRoaming(false);
                    }
                    break;

                case RatState.Alert:
                    FaceTowards(threat, dt);
                    if (now >= stateUntil) StartFlee();
                    break;

                case RatState.Fleeing:
                    if (now >= nextZig)
                    {
                        float strength = prof != null ? prof.zigzagStrength : 1f;
                        MoveTo(Zig(goal, 5f, zigzagWidth * strength), fleeSpeed);
                        nextZig = now + Random.Range(zigzagInterval.x, zigzagInterval.y);
                        // occasionally a rat just breaks the other way
                        if (Random.value < 0.05f * strength && !fleeToBurrow)
                        {
                            Vector3 side = Vector3.Cross(Vector3.up, (goal - transform.position).normalized);
                            goal = spawner != null ? spawner.ClampToFarm(transform.position + side * Random.Range(-12f, 12f)) : goal;
                            fleeToHide = false;
                        }
                    }
                    if (Arrived(goal, 0.9f) || now >= stateUntil)
                    {
                        if (fleeToHide) { Stop(); SetState(RatState.Hiding, Random.Range(hideTime.x, hideTime.y)); }
                        else if (fleeToBurrow) StartEscape();
                        else StartRoaming(true);
                    }
                    break;

                case RatState.Hiding:
                    if (now >= stateUntil)
                    {
                        if (Random.value < peekChance)
                        {
                            Vector3 toThreat = threat - transform.position;
                            toThreat.y = 0f;
                            Vector3 side = Vector3.Cross(Vector3.up, toThreat.normalized) * (Random.value < 0.5f ? -1.3f : 1.3f);
                            peekPoint = transform.position + side;
                            MoveTo(peekPoint, walkSpeed);
                            SetState(RatState.Peeking, Random.Range(1.2f, 2.2f));
                        }
                        else if (Random.value < escapeChanceAfterHiding) StartEscape();
                        else StartRoaming(true);
                    }
                    break;

                case RatState.Peeking:
                    if (now >= stateUntil)
                    {
                        if (Random.value < 0.5f) { MoveTo(hidePoint, walkSpeed); SetState(RatState.Hiding, Random.Range(1.5f, 3f)); peekChance *= 0.6f; }
                        else StartRoaming(true);
                    }
                    else if (Arrived(peekPoint, 0.4f)) FaceTowards(threat, dt);
                    break;

                case RatState.Escaping:
                    if (Arrived(goal, 0.6f) || now >= stateUntil)
                    {
                        Stop();
                        emerge = Mathf.MoveTowards(emerge, 0f, dt / 0.45f);
                        if (emerge <= 0f)
                        {
                            if (spawner != null) spawner.OnRatEscaped(this);
                            Destroy(gameObject);
                            return;
                        }
                    }
                    break;

                case RatState.Dead:
                    if (now - deathTime > 4.5f) { Destroy(gameObject); return; }
                    break;
            }

            if (!IsDead)
            {
                if (now >= nextSense) { nextSense = now + 0.25f; Sense(); }
                if (State != RatState.Escaping && now - aliveSince > maxLifetime && State != RatState.Fleeing) StartEscape();
                if (!useAgent && State != RatState.Emerging) FallbackMove(dt);
                CheckStuck(dt);
            }
        }

        void Sense()
        {
            if (spawner == null || spawner.Player == null) return;
            if (State != RatState.Roaming && State != RatState.Feeding && State != RatState.Peeking) return;
            FarmPlayer p = spawner.Player;
            float limit = playerAlertDistance * (p.Crouching ? crouchAlertMultiplier : 1f) * (p.MoveSpeed > 0.5f ? 1f : 0.55f);
            if (DistTo(p.transform.position) < limit) Alert(p.transform.position, false);
        }

        void CheckStuck(float dt)
        {
            bool shouldMove = (State == RatState.Roaming && !pausing) || State == RatState.Fleeing || State == RatState.Escaping;
            if (shouldMove && Speed < 0.15f) stuckTime += dt; else stuckTime = 0f;
            if (stuckTime > 2.5f)
            {
                stuckTime = 0f;
                if (State == RatState.Escaping) { emerge = 0f; if (spawner != null) spawner.OnRatEscaped(this); Destroy(gameObject); }
                else StartRoaming(false);
            }
        }

        void FallbackMove(float dt)
        {
            fallbackSpeed = Mathf.MoveTowards(fallbackSpeed, fallbackWant, acceleration * dt);
            Vector3 to = fallbackGoal - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.25f) { fallbackWant = 0f; return; }
            Vector3 dir = to.normalized;
            Vector3 p = transform.position + dir * fallbackSpeed * dt;
            p.y = FarmLayout.Height(p.x, p.z);
            transform.position = p;
            if (fallbackSpeed > 0.05f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), angularSpeed * dt);
        }

        void FaceTowards(Vector3 p, float dt)
        {
            Vector3 to = p - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * dt);
        }

        // ------------------------------------------------------------------ procedural animation

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;

            if (IsDead)
            {
                float k = Time.time - deathTime;
                float flip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.35f));
                float hop = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(k / 0.35f) * Mathf.PI)) * 0.12f;
                float sink = Mathf.Clamp01((k - 3f) / 1.4f);
                model.localRotation = modelBaseRot * Quaternion.AngleAxis(150f * flip, deathAxis);
                model.localPosition = modelBasePos + Vector3.up * (hop + 0.03f * flip) + Vector3.down * sink * 0.2f;
                model.localScale = Vector3.one * (1f - sink * 0.6f);
                for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(-60f * flip + Mathf.Sin(k * 30f) * 10f * (1f - Mathf.Clamp01(k)), 0f, 0f);
                return;
            }

            float speed = Speed;
            phase += speed * 13f * dt;
            float moveK = Mathf.Clamp01(speed / 2.5f);

            bool wantRear = State == RatState.Alert || (State == RatState.Peeking && Arrived(peekPoint, 0.4f));
            rear = Mathf.MoveTowards(rear, wantRear ? 1f : 0f, dt * 5f);

            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.018f * moveK;
            float under = (1f - emerge) * -0.32f;
            model.localPosition = modelBasePos + Vector3.up * (bob + under);
            model.localRotation = modelBaseRot * Quaternion.Euler(-38f * rear + Mathf.Sin(phase * 2f) * 4f * moveK, 0f, 0f);

            float[] offs = { 0f, 0.35f, Mathf.PI, Mathf.PI + 0.35f };
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float o = offs[i % offs.Length];
                float swing = Mathf.Sin(phase + o) * 55f * moveK;
                if (rear > 0.01f && i < 2) swing = Mathf.Lerp(swing, -50f, rear);
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] == null) continue;
                float sway = Mathf.Sin(t * (4f + speed) + i * 0.9f) * (6f + 6f * moveK);
                // +X pitch lifts a segment pointing backwards: the base droops, the rest curls gently back up
                tail[i].localRotation = Quaternion.Euler(i == 0 ? -12f + rear * 30f : 3.5f, sway, 0f);
            }

            if (head != null)
            {
                float pitch, yaw = 0f;
                if (State == RatState.Feeding) pitch = 28f + Mathf.Sin(t * 13f) * 9f;
                else if (State == RatState.Alert || wantRear)
                {
                    pitch = -12f + Mathf.Sin(t * 20f) * 3f;
                    Vector3 local = transform.InverseTransformPoint(threat);
                    yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50f, 50f);
                }
                else pitch = Mathf.Sin(t * 8f) * 6f * (1f - moveK) + 4f * moveK;
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(pitch, yaw, 0f), dt * 12f);
            }
        }
    }
}
