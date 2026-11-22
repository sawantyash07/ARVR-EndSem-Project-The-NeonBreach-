using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach
{
    public enum EnemyType { Grunt, Gunner, Drone, Boss, Sniper, Tank }

    /// <summary>
    /// One class for all enemy types. Visuals are built from primitives (with procedural walk animation),
    /// movement uses a CharacterController steered along NavMesh paths (AI Navigation package), and each type has its own AI:
    ///  Grunt  - runs at the player and claws.            Gunner - keeps distance, flanks, fires 3-round bursts.
    ///  Drone  - flies, circles the player, shoots.       Boss   - fan attack, ground slam (jump to dodge!), summons.
    ///  Sniper - finds a far perch with line of sight, telegraphs a laser for 1.4 s, then fires a high-damage bolt.
    ///  Tank   - slow, heavy, carries a frontal energy shield that blocks bullets: flank it or use grenades.
    /// </summary>
    public class Enemy : MonoBehaviour, IDamageable
    {
        public static readonly List<Enemy> All = new List<Enemy>();
        public static int AliveCount { get { return All.Count; } }

        public EnemyType type;
        public float maxHealth = 60f;
        public float health;
        public float speed = 4f;
        public float damage = 10f;
        public int score = 100;
        public bool IsDead { get; private set; }
        public float HealthFraction { get { return Mathf.Clamp01(health / Mathf.Max(1f, maxHealth)); } }

        // tank shield (read by the HUD)
        public float ShieldHealth { get; private set; }
        public float ShieldMax { get; private set; }
        public bool ShieldUp { get { return type == EnemyType.Tank && ShieldHealth > 0f; } }

        int wave;
        float scale = 1f;
        float attackRange = 1.9f;
        float hoverHeight = 3.2f;

        CharacterController cc;
        PlayerController target;
        float retargetTimer;
        Renderer[] renderers;
        MaterialPropertyBlock flashBlock;
        Transform legL, legR, armL, armR, eye, muzzle, glowCore, ring, shieldPlate;
        AudioSource loop;
        float loopPitch = 1f;

        float vy;
        Vector3 knock;
        float attackTimer, attackAnim;
        float fireTimer, burstTimer;
        int burstLeft;
        float strafeDir = 1f, strafeTimer;
        float walkPhase;
        float hitFlash;
        float avoidTimer, avoidSign = 1f;
        float orbitDir = 1f;
        float bobSeed;

        // navigation
        NavMeshPath path;
        readonly Vector3[] corners = new Vector3[24];
        int cornerCount, cornerIdx;
        float repathTimer;
        Vector3 lastGoal;

        // boss
        float fanTimer = 3f, slamTimer = 8f, slamWindup;
        bool summoned;

        // sniper
        enum SniperState { Reposition, Aim, Cooldown }
        SniperState sState = SniperState.Reposition;
        Vector3 perch;
        bool hasPerch;
        float sTimer;
        int shotsAtPerch;
        LineRenderer laser;

        // tank
        float shieldRegenAt;
        float shieldFlash;
        float lobTimer = 3f;

        // ------------------------------------------------------------------------------------ spawning

        public static Enemy Spawn(EnemyType t, Vector3 pos, int waveNumber)
        {
            GameObject go = new GameObject(t.ToString());
            go.layer = Layers.Enemy;
            go.transform.position = pos;
            Enemy e = go.AddComponent<Enemy>();
            e.Setup(t, waveNumber);
            return e;
        }

        void Setup(EnemyType t, int waveNumber)
        {
            type = t;
            wave = Mathf.Max(1, waveNumber);
            float hpScale = (1f + 0.10f * (wave - 1)) * DifficultyInfo.EnemyHealth;
            float dmgScale = (1f + 0.05f * (wave - 1)) * DifficultyInfo.EnemyDamage;
            bobSeed = Random.value * 10f;
            strafeDir = Random.value < 0.5f ? -1f : 1f;
            orbitDir = Random.value < 0.5f ? -1f : 1f;
            path = new NavMeshPath();
            repathTimer = Random.Range(0f, 0.3f);

            float radius, height;
            Vector3 center;

            switch (t)
            {
                case EnemyType.Gunner:
                    maxHealth = 45f * hpScale; speed = 3.3f; damage = 9f * dmgScale; score = 150;
                    radius = 0.38f; height = 1.5f; center = new Vector3(0f, 0.75f, 0f);
                    BuildHumanoid(1f, "EnemyBlue", "NeonMagenta", true);
                    fireTimer = Random.Range(0.5f, 2f);
                    break;
                case EnemyType.Drone:
                    maxHealth = 30f * hpScale; speed = 5.4f; damage = 8f * dmgScale; score = 125;
                    radius = 0.45f; height = 0.95f; center = Vector3.zero;
                    hoverHeight = MixedReality.Active ? Random.Range(1.6f, 2.4f) : Random.Range(2.6f, 4.6f);
                    BuildDrone();
                    fireTimer = Random.Range(1f, 2.5f);
                    break;
                case EnemyType.Boss:
                    scale = 2.1f;
                    maxHealth = 1400f * hpScale; speed = 3.1f; damage = 13f * dmgScale; score = 2500;
                    radius = 0.4f * scale; height = 1.5f * scale; center = new Vector3(0f, 0.75f * scale, 0f);
                    attackRange = 3.6f;
                    BuildHumanoid(scale, "EnemyDark", "NeonOrange", false);
                    break;
                case EnemyType.Sniper:
                    maxHealth = 35f * hpScale; speed = 3.8f; damage = 30f * dmgScale; score = 225;
                    radius = 0.35f; height = 1.55f; center = new Vector3(0f, 0.78f, 0f);
                    BuildHumanoid(1.02f, "EnemyGrey", "NeonGreen", true);
                    BuildLaser();
                    break;
                case EnemyType.Tank:
                    scale = 1.3f;
                    maxHealth = 230f * hpScale; speed = 2.3f; damage = 20f * dmgScale; score = 325;
                    radius = 0.42f * scale; height = 1.5f * scale; center = new Vector3(0f, 0.75f * scale, 0f);
                    attackRange = 2.6f;
                    BuildHumanoid(scale, "EnemyDark", "NeonCyan", false);
                    BuildShield();
                    ShieldMax = 160f * hpScale;
                    ShieldHealth = ShieldMax;
                    break;
                default:
                    maxHealth = 60f * hpScale; speed = Mathf.Min(4.2f + 0.08f * wave, 6.5f); damage = 12f * dmgScale; score = 100;
                    radius = 0.4f; height = 1.5f; center = new Vector3(0f, 0.75f, 0f);
                    BuildHumanoid(1f, "EnemyRed", "NeonRed", false);
                    break;
            }
            health = maxHealth;

            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = radius;
            cc.height = height;
            cc.center = center;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;
            cc.slopeLimit = 50f;

            renderers = GetComponentsInChildren<Renderer>();
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor("_Color", Color.white);

            // positional (binaural) sound so you can hear where enemies are
            switch (t)
            {
                case EnemyType.Drone: loop = Sfx.AttachLoop(gameObject, SfxId.DroneHum, 0.32f, 26f); loopPitch = Random.Range(0.9f, 1.15f); break;
                case EnemyType.Tank: loop = Sfx.AttachLoop(gameObject, SfxId.TankHum, 0.45f, 30f); break;
                case EnemyType.Boss: loop = Sfx.AttachLoop(gameObject, SfxId.TankHum, 0.6f, 45f); loopPitch = 0.7f; break;
                default: loop = Sfx.AttachLoop(gameObject, SfxId.ServoLoop, 0.16f, 14f); loopPitch = Random.Range(0.85f, 1.2f); break;
            }
            if (loop != null) loop.pitch = loopPitch;
            All.Add(this);
        }

        void OnDestroy() { All.Remove(this); }

        // ------------------------------------------------------------------------------------ visuals

        static GameObject Pivot(Transform parent, string name, Vector3 pos)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g;
        }

        void AddHead(Transform vis, Vector3 pos, float colliderRadius, float mult)
        {
            GameObject h = new GameObject("HeadHitbox");
            h.layer = Layers.EnemyHead;
            h.transform.SetParent(vis, false);
            h.transform.localPosition = pos;
            SphereCollider sc = h.AddComponent<SphereCollider>();
            sc.radius = colliderRadius;
            HitZone hz = h.AddComponent<HitZone>();
            hz.isHead = true;
            hz.multiplier = mult;
            Rigidbody rb = h.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        void BuildHumanoid(float s, string body, string glow, bool shooter)
        {
            Transform vis = new GameObject("Visual").transform;
            vis.SetParent(transform, false);
            vis.localScale = Vector3.one * s;
            bool tank = type == EnemyType.Tank;
            bool sniper = type == EnemyType.Sniper;

            Vector3 torso = tank ? new Vector3(0.78f, 0.7f, 0.46f) : (sniper ? new Vector3(0.5f, 0.6f, 0.3f) : new Vector3(0.6f, 0.62f, 0.34f));
            Util.Part(vis, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.05f, 0f), Vector3.zero, torso, body);
            Util.Part(vis, PrimitiveType.Cube, "Pelvis", new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(0.46f, 0.2f, 0.3f), "EnemyDark");
            GameObject core = Util.Part(vis, PrimitiveType.Cube, "Core", new Vector3(0f, 1.08f, torso.z * 0.5f + 0.01f), Vector3.zero, new Vector3(0.22f, 0.22f, 0.04f), glow);
            glowCore = core.transform;

            GameObject lL = Pivot(vis, "LegL", new Vector3(-0.15f, 0.62f, 0f));
            GameObject lR = Pivot(vis, "LegR", new Vector3(0.15f, 0.62f, 0f));
            float legW = tank ? 0.26f : 0.18f;
            Util.Part(lL.transform, PrimitiveType.Cube, "Leg", new Vector3(0f, -0.3f, 0f), Vector3.zero, new Vector3(legW, 0.6f, 0.2f), "EnemyDark");
            Util.Part(lR.transform, PrimitiveType.Cube, "Leg", new Vector3(0f, -0.3f, 0f), Vector3.zero, new Vector3(legW, 0.6f, 0.2f), "EnemyDark");
            legL = lL.transform; legR = lR.transform;

            float armX = tank ? 0.5f : 0.4f;
            GameObject aL = Pivot(vis, "ArmL", new Vector3(-armX, 1.3f, 0f));
            GameObject aR = Pivot(vis, "ArmR", new Vector3(armX, 1.3f, 0f));
            float armW = tank ? 0.2f : 0.14f;
            Util.Part(aL.transform, PrimitiveType.Cube, "Arm", new Vector3(0f, -0.28f, 0f), Vector3.zero, new Vector3(armW, 0.56f, armW), body);
            Util.Part(aR.transform, PrimitiveType.Cube, "Arm", new Vector3(0f, -0.28f, 0f), Vector3.zero, new Vector3(armW, 0.56f, armW), body);
            Util.Part(aL.transform, PrimitiveType.Cube, "Hand", new Vector3(0f, -0.58f, 0f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.16f), glow);
            Util.Part(aR.transform, PrimitiveType.Cube, "Hand", new Vector3(0f, -0.58f, 0f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.16f), glow);
            armL = aL.transform; armR = aR.transform;

            Util.Part(vis, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.64f, 0f), Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f), "EnemyDark");
            if (sniper)
            {
                // single glowing scope-eye instead of a visor
                Util.Part(vis, PrimitiveType.Sphere, "ScopeEye", new Vector3(0.07f, 1.67f, 0.15f), Vector3.zero, Vector3.one * 0.09f, glow);
                Util.Part(vis, PrimitiveType.Cube, "Hood", new Vector3(0f, 1.78f, -0.02f), Vector3.zero, new Vector3(0.38f, 0.08f, 0.38f), body);
            }
            else Util.Part(vis, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.66f, 0.15f), Vector3.zero, new Vector3(0.26f, 0.07f, 0.06f), glow);
            AddHead(vis, new Vector3(0f, 1.64f, 0f), 0.26f, type == EnemyType.Boss ? 1.6f : (tank ? 1.8f : 2.4f));

            if (shooter)
            {
                // right arm raised, gun in hand
                armR.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                // the gun's barrel (local +Z) must follow the arm (local -Y), hence the 90 degree roll
                float len = sniper ? 0.85f : 0.4f;
                GameObject gun = Util.Part(armR, PrimitiveType.Cube, "Gun", new Vector3(0f, -0.62f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.12f, len), "GunMetal");
                GameObject mz = new GameObject("Muzzle");
                mz.transform.SetParent(gun.transform, false);
                mz.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                muzzle = mz.transform;
                Util.Part(gun.transform, PrimitiveType.Cube, "GunGlow", new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(1.05f, 0.1f, 0.7f), glow);
                if (sniper) Util.Part(gun.transform, PrimitiveType.Cube, "Scope", new Vector3(0f, 1.1f, 0.05f), Vector3.zero, new Vector3(0.5f, 0.6f, 0.25f), "GunGrip");
            }
            else
            {
                // spiked shoulders
                Util.Part(vis, PrimitiveType.Cube, "ShoulderL", new Vector3(-armX - 0.02f, 1.42f, 0f), new Vector3(0f, 0f, 30f), new Vector3(0.28f, 0.1f, 0.3f), "EnemyDark");
                Util.Part(vis, PrimitiveType.Cube, "ShoulderR", new Vector3(armX + 0.02f, 1.42f, 0f), new Vector3(0f, 0f, -30f), new Vector3(0.28f, 0.1f, 0.3f), "EnemyDark");
            }

            if (type == EnemyType.Boss)
            {
                Util.Part(vis, PrimitiveType.Cube, "SpikeL", new Vector3(-0.5f, 1.65f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.08f, 0.4f, 0.08f), glow);
                Util.Part(vis, PrimitiveType.Cube, "SpikeR", new Vector3(0.5f, 1.65f, 0f), new Vector3(0f, 0f, -35f), new Vector3(0.08f, 0.4f, 0.08f), glow);
                Util.Part(vis, PrimitiveType.Cube, "Crown", new Vector3(0f, 1.9f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.12f, 0.12f, 0.12f), glow);
            }
            if (type == EnemyType.Boss || tank)
            {
                GameObject mz = new GameObject("Muzzle");
                mz.transform.SetParent(vis, false);
                mz.transform.localPosition = new Vector3(0f, 1.1f, 0.4f);
                muzzle = mz.transform;
            }
        }

        void BuildDrone()
        {
            Transform vis = new GameObject("Visual").transform;
            vis.SetParent(transform, false);

            Util.Part(vis, PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.zero, Vector3.one * 0.7f, "EnemyGrey");
            GameObject e = Util.Part(vis, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0f, 0.3f), Vector3.zero, Vector3.one * 0.26f, "NeonYellow");
            eye = e.transform;
            GameObject r = Util.Part(vis, PrimitiveType.Cylinder, "Ring", Vector3.zero, Vector3.zero, new Vector3(1.15f, 0.025f, 1.15f), "NeonYellow");
            ring = r.transform;
            Util.Part(vis, PrimitiveType.Sphere, "PodL", new Vector3(-0.5f, 0f, 0f), Vector3.zero, Vector3.one * 0.2f, "EnemyDark");
            Util.Part(vis, PrimitiveType.Sphere, "PodR", new Vector3(0.5f, 0f, 0f), Vector3.zero, Vector3.one * 0.2f, "EnemyDark");
            Util.Part(vis, PrimitiveType.Cube, "Fin", new Vector3(0f, 0.38f, -0.1f), Vector3.zero, new Vector3(0.03f, 0.25f, 0.3f), "EnemyDark");

            AddHead(vis, new Vector3(0f, 0f, 0.25f), 0.3f, 1.8f);
            muzzle = eye;
        }

        void BuildShield()
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "EnergyShield";
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(transform, false);
            s.transform.localPosition = new Vector3(0f, 1.25f * scale * 0.85f, 0.62f * scale);
            s.transform.localScale = new Vector3(1.25f * scale, 1.55f * scale, 0.05f);
            Renderer r = s.GetComponent<Renderer>();
            r.sharedMaterial = Mats.FxShield;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shieldPlate = s.transform;
            // emitter on the left forearm
            Util.Part(armL, PrimitiveType.Cube, "Emitter", new Vector3(0f, -0.4f, 0.1f), Vector3.zero, new Vector3(0.22f, 0.3f, 0.06f), "NeonCyan");
        }

        void BuildLaser()
        {
            GameObject g = new GameObject("SniperLaser");
            g.transform.SetParent(transform, false);
            laser = g.AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.sharedMaterial = Mats.FxTracer;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.enabled = false;
        }

        // ------------------------------------------------------------------------------------ AI

        void Update()
        {
            if (IsDead) return;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (!gm.Playing)
            {
                if (loop != null && loop.isPlaying) loop.Pause();
                return;
            }
            if (loop != null)
            {
                if (!loop.isPlaying) loop.UnPause();
                loop.pitch = loopPitch * (PowerUps.SlowMo ? 0.6f : 1f);
            }

            float dt = Time.deltaTime * PowerUps.EnemyTimeScale;

            retargetTimer -= dt;
            if (target == null || target.IsDead || retargetTimer <= 0f)
            {
                retargetTimer = 1f;
                target = gm.NearestAlivePlayer(transform.position);
            }
            if (target == null)
            {
                if (laser != null) laser.enabled = false;
                return;
            }

            if (hitFlash > 0f)
            {
                hitFlash -= Time.deltaTime;
                if (hitFlash <= 0f) SetFlash(false);
            }

            Vector3 toPlayer = target.BodyCenter - transform.position;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float dist = flat.magnitude;
            Vector3 dir = dist > 0.01f ? flat / dist : transform.forward;

            switch (type)
            {
                case EnemyType.Grunt: UpdateGrunt(dt, dir, dist, toPlayer.y); break;
                case EnemyType.Gunner: UpdateGunner(dt, dir, dist); break;
                case EnemyType.Drone: UpdateDrone(dt, dir, dist); break;
                case EnemyType.Sniper: UpdateSniper(dt, dir, dist); break;
                case EnemyType.Tank: UpdateTank(dt, dir, dist, toPlayer.y); break;
                default: UpdateBoss(dt, dir, dist, toPlayer.y); break;
            }
        }

        void UpdateGrunt(float dt, Vector3 dir, float dist, float dy)
        {
            attackTimer -= dt;
            Vector3 desired = Vector3.zero;
            if (dist > attackRange) desired = PathDir(target.transform.position, dt) * speed;
            else if (attackTimer <= 0f && Mathf.Abs(dy) < 2.6f)
            {
                attackTimer = 1.0f * DifficultyInfo.EnemyFireDelay;
                attackAnim = 0.3f;
                target.TakeDamage(damage, transform.position, false, gameObject);
            }
            Face(desired.sqrMagnitude > 0.01f && dist > 4f ? desired.normalized : dir, dt);
            Move(desired, dt);
            AnimateHumanoid(dt, desired.magnitude);
        }

        void UpdateGunner(float dt, Vector3 dir, float dist)
        {
            fireTimer -= dt;
            strafeTimer -= dt;
            if (strafeTimer <= 0f)
            {
                strafeDir = Random.value < 0.5f ? -1f : 1f;
                strafeTimer = Random.Range(1.5f, 3f);
            }

            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            bool blocked = Physics.Linecast(eyePos, target.BodyCenter, Layers.EnvMask, QueryTriggerInteraction.Ignore);

            Vector3 desired;
            if (blocked || dist > 14f)
            {
                // no line of sight: path around the cover (flank)
                desired = PathDir(target.transform.position, dt) * speed;
            }
            else
            {
                Vector3 move = Vector3.zero;
                if (dist < 8f) move -= dir;
                move += Vector3.Cross(Vector3.up, dir) * strafeDir * 0.8f;
                desired = move.sqrMagnitude > 0.01f ? Steer(move.normalized) * speed : Vector3.zero;
            }

            if (!blocked && fireTimer <= 0f && dist < 38f)
            {
                burstLeft = 3;
                burstTimer = 0f;
                fireTimer = Random.Range(2.0f, 3.0f) * DifficultyInfo.EnemyFireDelay;
            }
            if (burstLeft > 0)
            {
                burstTimer -= dt;
                if (burstTimer <= 0f)
                {
                    FireAt(24f, "NeonMagenta", 0.07f, SfxId.EnemyShot, damage);
                    burstLeft--;
                    burstTimer = 0.17f;
                }
            }
            Face(dir, dt);
            Move(desired, dt);
            AnimateHumanoid(dt, desired.magnitude);
        }

        void UpdateDrone(float dt, Vector3 dir, float dist)
        {
            fireTimer -= dt;
            Vector3 tangent = Vector3.Cross(Vector3.up, dir) * orbitDir;
            float far = MixedReality.Active ? 4f : 11f, near = MixedReality.Active ? 2f : 6f;
            Vector3 radial = dist > far ? dir : (dist < near ? -dir : Vector3.zero);
            Vector3 horizontal = (tangent * 0.9f + radial).normalized * speed * (MixedReality.Active ? 0.5f : 1f);

            float wantY = hoverHeight + Mathf.Sin(Time.time * 2f + bobSeed) * 0.35f;
            float vertical = Mathf.Clamp((wantY - transform.position.y) * 3f, -4f, 4f);

            Vector3 v = horizontal + Vector3.up * vertical + knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);
            cc.Move(v * dt);

            Vector3 eyePos = transform.position;
            bool blocked = Physics.Linecast(eyePos, target.BodyCenter, Layers.EnvMask, QueryTriggerInteraction.Ignore);
            if (!blocked && fireTimer <= 0f && dist < 32f)
            {
                fireTimer = Random.Range(1.6f, 2.6f) * DifficultyInfo.EnemyFireDelay;
                FireAt(20f, "NeonYellow", 0.05f, SfxId.EnemyShot, damage);
            }

            Face(dir, dt);
            if (ring != null) ring.Rotate(0f, 240f * dt, 0f, Space.Self);
        }

        void UpdateSniper(float dt, Vector3 dir, float dist)
        {
            sTimer -= dt;
            Vector3 desired = Vector3.zero;
            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            bool los = !Physics.Linecast(eyePos, target.BodyCenter, Layers.EnvMask, QueryTriggerInteraction.Ignore);

            switch (sState)
            {
                case SniperState.Reposition:
                    laser.enabled = false;
                    if (!hasPerch || sTimer < -8f) { hasPerch = FindPerch(out perch); sTimer = 0f; }
                    Vector3 toPerch = perch - transform.position;
                    toPerch.y = 0f;
                    if (!hasPerch || toPerch.magnitude < 1.2f)
                    {
                        if (los && dist < 45f) { sState = SniperState.Aim; sTimer = 1.4f; shotsAtPerch = 0; Sfx.Play3D(SfxId.SniperCharge, eyePos, 0.7f); }
                        else { hasPerch = FindPerch(out perch); if (!hasPerch) desired = PathDir(target.transform.position, dt) * speed; }
                    }
                    else desired = PathDir(perch, dt) * speed;
                    break;

                case SniperState.Aim:
                    if (!los || dist < 6f)
                    {
                        // lost the shot or the player rushed in: relocate
                        laser.enabled = false;
                        sState = SniperState.Reposition;
                        hasPerch = false;
                        break;
                    }
                    DrawLaser(1f - Mathf.Clamp01(sTimer / 1.4f));
                    if (sTimer <= 0f)
                    {
                        laser.enabled = false;
                        FireAt(75f, "NeonGreen", 0.012f, SfxId.SniperShot, damage, 0.22f);
                        shotsAtPerch++;
                        sState = SniperState.Cooldown;
                        sTimer = Random.Range(2.2f, 3.2f) * DifficultyInfo.EnemyFireDelay;
                    }
                    break;

                default:
                    laser.enabled = false;
                    if (sTimer <= 0f)
                    {
                        if (shotsAtPerch >= 2 || !los) { sState = SniperState.Reposition; hasPerch = false; }
                        else { sState = SniperState.Aim; sTimer = 1.4f; Sfx.Play3D(SfxId.SniperCharge, eyePos, 0.7f); }
                    }
                    break;
            }
            Face(desired.sqrMagnitude > 0.01f ? desired.normalized : dir, dt);
            Move(desired, dt);
            AnimateHumanoid(dt, desired.magnitude);
        }

        bool FindPerch(out Vector3 p)
        {
            Vector3 c = target.transform.position;
            float minD = MixedReality.Active ? 2.5f : 15f, maxD = MixedReality.Active ? 4.5f : 27f;
            for (int i = 0; i < 14; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Random.Range(minD, maxD);
                Vector3 cand = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                NavMeshHit hit;
                if (!ArenaManager.NavReady || !NavMesh.SamplePosition(cand, out hit, 2.5f, NavMesh.AllAreas)) continue;
                if (Physics.Linecast(hit.position + Vector3.up * 1.5f, target.BodyCenter, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                p = hit.position;
                return true;
            }
            p = transform.position;
            return false;
        }

        void DrawLaser(float charge)
        {
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
            Vector3 to = target.BodyCenter;
            RaycastHit h;
            if (Physics.Linecast(from, to, out h, Layers.EnvMask, QueryTriggerInteraction.Ignore)) to = h.point;
            laser.enabled = true;
            laser.SetPosition(0, from);
            laser.SetPosition(1, to);
            float w = Mathf.Lerp(0.008f, 0.05f, charge * charge);
            laser.startWidth = w;
            laser.endWidth = w * 0.6f;
            float blink = charge > 0.75f ? (Mathf.Sin(Time.time * 40f) > 0f ? 1f : 0.4f) : 1f;
            Color c = new Color(0.3f, 1f, 0.35f, (0.25f + 0.65f * charge) * blink);
            laser.startColor = c;
            laser.endColor = c;
        }

        void UpdateTank(float dt, Vector3 dir, float dist, float dy)
        {
            attackTimer -= dt;
            lobTimer -= dt;

            // shield regeneration after a quiet period
            if (ShieldHealth <= 0f && Time.time >= shieldRegenAt)
            {
                ShieldHealth = ShieldMax;
                Sfx.Play3D(SfxId.PowerUp, transform.position + Vector3.up, 0.6f, 0.7f);
            }
            if (shieldPlate != null)
            {
                bool up = ShieldHealth > 0f;
                if (shieldPlate.gameObject.activeSelf != up) shieldPlate.gameObject.SetActive(up);
                shieldFlash = Mathf.MoveTowards(shieldFlash, 0f, Time.deltaTime * 6f);
                float s = 1f + shieldFlash * 0.06f;
                shieldPlate.localScale = new Vector3(1.25f * scale * s, 1.55f * scale * s, 0.05f);
            }

            Vector3 desired = Vector3.zero;
            if (dist > attackRange) desired = PathDir(target.transform.position, dt) * speed;
            else if (attackTimer <= 0f && Mathf.Abs(dy) < 3f)
            {
                attackTimer = 1.4f * DifficultyInfo.EnemyFireDelay;
                attackAnim = 0.35f;
                target.TakeDamage(damage, transform.position, false, gameObject);
                target.Shake(0.15f);
            }
            if (lobTimer <= 0f && dist > 5f && dist < 30f)
            {
                lobTimer = Random.Range(3.5f, 5f) * DifficultyInfo.EnemyFireDelay;
                FireAt(13f, "NeonCyan", 0.04f, SfxId.EnemyShot, damage * 0.8f, 0.55f);
                attackAnim = 0.35f;
            }
            // the tank turns slowly - that is what makes flanking it possible
            FaceSlow(dir, dt, 1.6f);
            Move(desired, dt);
            AnimateHumanoid(dt, desired.magnitude);
        }

        void UpdateBoss(float dt, Vector3 dir, float dist, float dy)
        {
            fanTimer -= dt;
            slamTimer -= dt;
            attackTimer -= dt;
            strafeTimer -= dt;
            if (strafeTimer <= 0f) { strafeDir = Random.value < 0.5f ? -1f : 1f; strafeTimer = Random.Range(2f, 4f); }

            // reinforcements at half health
            if (!summoned && health < maxHealth * 0.5f)
            {
                summoned = true;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 off = new Vector3(Mathf.Cos(i * 2.1f), 0f, Mathf.Sin(i * 2.1f)) * 4f;
                    Enemy.Spawn(i == 2 && wave >= 10 ? EnemyType.Tank : EnemyType.Grunt, transform.position + off + Vector3.up * 0.1f, wave);
                }
                if (GameManager.Instance.Hud != null) GameManager.Instance.Hud.Banner(Loc.T("REINFORCEMENTS"), Loc.T("The boss calls for backup!"));
                Sfx.Play2D(SfxId.Beep, 0.8f, 0.6f);
            }

            Vector3 desired = Vector3.zero;
            if (slamWindup > 0f)
            {
                // telegraph: stand still, core pulses, ring shows the danger radius
                slamWindup -= dt;
                if (slamWindup <= 0f) DoSlam();
            }
            else
            {
                if (dist > 13f) desired = PathDir(target.transform.position, dt) * speed;
                else if (dist < 6f) desired = Steer(-dir) * speed * 0.6f;
                else desired = Steer(Vector3.Cross(Vector3.up, dir) * strafeDir) * speed * 0.7f;

                if (dist < attackRange && attackTimer <= 0f && Mathf.Abs(dy) < 4f)
                {
                    attackTimer = 1.2f;
                    attackAnim = 0.35f;
                    target.TakeDamage(damage * 2f, transform.position, false, gameObject);
                }
                if (fanTimer <= 0f)
                {
                    bool angry = health < maxHealth * 0.5f;
                    fanTimer = (angry ? 2.1f : 3.2f) * DifficultyInfo.EnemyFireDelay;
                    FireFan(angry ? 9 : 7, 55f);
                }
                if (slamTimer <= 0f)
                {
                    slamTimer = Random.Range(8f, 11f);
                    slamWindup = 1.0f;
                    FX.Ring(transform.position, 7f, "NeonRed", 1.0f);
                    Sfx.Play3D(SfxId.Beep, transform.position, 1f, 0.5f);
                }
            }
            Face(dir, dt);
            Move(desired, dt);
            AnimateHumanoid(dt, desired.magnitude);
            if (glowCore != null && slamWindup > 0f)
            {
                float p = 1f + Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 1.2f;
                glowCore.localScale = new Vector3(0.22f * p, 0.22f * p, 0.04f);
            }
        }

        void DoSlam()
        {
            Vector3 p = transform.position;
            FX.Explosion(p + Vector3.up * 0.3f, 4f);
            FX.Ring(p, 7f, "NeonOrange", 0.35f);
            Sfx.Play3D(SfxId.Slam, p, 1f);
            if (glowCore != null) glowCore.localScale = new Vector3(0.22f, 0.22f, 0.04f);
            GameManager gm = GameManager.Instance;
            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController pl = gm.Players[i];
                if (pl == null || pl.IsDead) continue;
                Vector3 d = pl.transform.position - p;
                d.y = 0f;
                if (d.magnitude < 7f && pl.Grounded)   // jumping dodges the slam
                    pl.TakeDamage(38f * DifficultyInfo.EnemyDamage, pl.transform.position, false, gameObject);
                pl.Shake(Mathf.Clamp01(1f - d.magnitude / 20f) * 0.4f);
            }
        }

        // ------------------------------------------------------------------------------------ navigation

        /// <summary>Direction to walk to reach goal: follows a NavMesh path around cover, falls back to raycast steering.</summary>
        Vector3 PathDir(Vector3 goal, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 direct = goal - pos;
            direct.y = 0f;
            Vector3 directN = direct.sqrMagnitude > 0.0001f ? direct.normalized : transform.forward;

            if (!ArenaManager.NavReady) return Separate(Steer(directN));

            repathTimer -= dt;
            if (repathTimer <= 0f || (goal - lastGoal).sqrMagnitude > 9f)
            {
                repathTimer = Random.Range(0.3f, 0.5f);
                lastGoal = goal;
                cornerCount = 0;
                NavMeshHit a, b;
                if (NavMesh.SamplePosition(pos, out a, 2f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(goal, out b, 3f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) &&
                    path.status != NavMeshPathStatus.PathInvalid)
                {
                    cornerCount = path.GetCornersNonAlloc(corners);
                    cornerIdx = 1;
                }
            }

            while (cornerIdx < cornerCount)
            {
                Vector3 d = corners[cornerIdx] - pos;
                d.y = 0f;
                if (d.magnitude < 0.7f) { cornerIdx++; continue; }
                return Separate(d.normalized);
            }
            return Separate(Steer(directN));
        }

        /// <summary>Push away from very close allies so groups spread out instead of stacking.</summary>
        Vector3 Separate(Vector3 dir)
        {
            Vector3 push = Vector3.zero;
            Vector3 p = transform.position;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy o = All[i];
                if (o == null || o == this || o.type == EnemyType.Drone) continue;
                Vector3 d = p - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                float r = 1.3f * Mathf.Max(scale, o.scale);
                if (m < r && m > 0.001f) push += d / m * (1f - m / r);
            }
            Vector3 v = dir + push * 0.8f;
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : dir;
        }

        Vector3 Steer(Vector3 dir)
        {
            avoidTimer -= Time.deltaTime;
            Vector3 o = transform.position + Vector3.up * 0.7f;
            RaycastHit h;
            if (Physics.SphereCast(o, 0.35f * scale, dir, out h, 2.2f * scale, Layers.EnvMask, QueryTriggerInteraction.Ignore))
            {
                if (avoidTimer <= 0f)
                {
                    Vector3 l = Quaternion.Euler(0f, -60f, 0f) * dir;
                    Vector3 r = Quaternion.Euler(0f, 60f, 0f) * dir;
                    float dl = Physics.Raycast(o, l, out h, 6f, Layers.EnvMask) ? h.distance : 6f;
                    float dr = Physics.Raycast(o, r, out h, 6f, Layers.EnvMask) ? h.distance : 6f;
                    avoidSign = dl > dr ? -1f : 1f;
                    avoidTimer = 0.7f;
                }
                return Quaternion.Euler(0f, 60f * avoidSign, 0f) * dir;
            }
            return dir;
        }

        // ------------------------------------------------------------------------------------ helpers

        void FireAt(float projectileSpeed, string glow, float spreadRadius, SfxId sound, float dmg, float size = 0.3f)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
            Vector3 aim = target.BodyCenter + target.Velocity * (Vector3.Distance(origin, target.BodyCenter) / projectileSpeed) * 0.4f;
            Vector3 dir = (aim - origin).normalized;
            dir += Random.insideUnitSphere * spreadRadius * DifficultyInfo.EnemySpread;
            EnemyProjectile.Spawn(origin, dir.normalized, projectileSpeed, dmg, glow, size);
            Sfx.Play3D(sound, origin, 0.8f, Random.Range(0.9f, 1.15f));
        }

        void FireFan(int count, float arc)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 2f;
            Vector3 baseDir = (target.BodyCenter - origin);
            baseDir.y = 0f;
            baseDir.Normalize();
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (float)(count - 1));
                Vector3 d = Quaternion.Euler(0f, a, 0f) * baseDir;
                d.y = (target.BodyCenter.y - origin.y) / 20f;
                EnemyProjectile.Spawn(origin, d.normalized, 17f, damage, "NeonOrange");
            }
            Sfx.Play3D(SfxId.EnemyShot, origin, 1f, 0.6f);
            attackAnim = 0.4f;
        }

        void Face(Vector3 dir, float dt) { FaceSlow(dir, dt, 8f); }

        void FaceSlow(Vector3 dir, float dt, float rate)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-rate * dt));
        }

        void Move(Vector3 desired, float dt)
        {
            Vector3 v = desired + knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);
            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy += -25f * dt;
            v.y = vy;
            cc.Move(v * dt);
            // fell out of the world (should never happen) - remove so the wave can finish
            if (transform.position.y < -20f) Die(false, null);
        }

        void AnimateHumanoid(float dt, float moveSpeed)
        {
            walkPhase += moveSpeed * dt * 2.4f / Mathf.Max(0.8f, scale * 0.7f);
            float sw = Mathf.Sin(walkPhase) * 35f * Mathf.Clamp01(moveSpeed / 2f);
            if (legL != null) legL.localRotation = Quaternion.Euler(sw, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-sw, 0f, 0f);
            bool gunArm = muzzle != null && (type == EnemyType.Gunner || type == EnemyType.Sniper);

            if (attackAnim > 0f)
            {
                attackAnim -= dt;
                float k = Mathf.Sin(Mathf.Clamp01(1f - attackAnim / 0.35f) * Mathf.PI);
                if (armL != null && type != EnemyType.Tank) armL.localRotation = Quaternion.Euler(-110f * k, 0f, 0f);
                if (armR != null && !gunArm) armR.localRotation = Quaternion.Euler(-110f * k, 0f, 0f);
            }
            else
            {
                // the tank keeps its shield arm raised
                if (armL != null) armL.localRotation = type == EnemyType.Tank ? Quaternion.Euler(-75f, 20f, 0f) : Quaternion.Euler(-sw * 0.8f, 0f, 0f);
                if (armR != null && !gunArm) armR.localRotation = Quaternion.Euler(sw * 0.8f, 0f, 0f);
            }
        }

        public void Knock(Vector3 direction, float force)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            float resist = type == EnemyType.Boss ? 0.15f : (type == EnemyType.Tank ? 0.35f : 1f);
            knock += direction.normalized * force * resist;
        }

        void SetFlash(bool on)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].transform == shieldPlate) continue;
                renderers[i].SetPropertyBlock(on ? flashBlock : null);
            }
        }

        // ------------------------------------------------------------------------------------ damage

        /// <summary>True if the hit came from in front of the tank, where the energy shield is.</summary>
        bool Frontal(Vector3 hitPoint)
        {
            Vector3 c = transform.position + Vector3.up * scale;
            Vector3 v = hitPoint - c;
            v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) return false;
            return Vector3.Dot(v.normalized, transform.forward) > 0.25f;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, bool headshot, GameObject source)
        {
            if (IsDead) return;

            // tank shield: blocks frontal bullets, grenades go around it
            bool explosive = source != null && source.GetComponent<Grenade>() != null;
            if (ShieldUp && !explosive && Frontal(hitPoint))
            {
                ShieldHealth -= amount;
                shieldFlash = 1f;
                shieldRegenAt = Time.time + 7f;
                FX.Burst(hitPoint, new Color(0.3f, 0.9f, 1f), 5, 3f, 0.08f, 0.25f);
                Sfx.Play3D(SfxId.ShieldBlock, hitPoint, 0.55f, Random.Range(0.9f, 1.1f));
                GameManager g = GameManager.Instance;
                if (g != null) g.OnShieldHit(this, source);
                if (ShieldHealth <= 0f)
                {
                    ShieldHealth = 0f;
                    FX.Burst(shieldPlate != null ? shieldPlate.position : hitPoint, new Color(0.3f, 0.9f, 1f), 30, 6f, 0.12f, 0.5f);
                    Sfx.Play3D(SfxId.Explosion, hitPoint, 0.5f, 1.6f);
                }
                return;
            }
            if (type == EnemyType.Tank) shieldRegenAt = Mathf.Max(shieldRegenAt, Time.time + 4f);

            health -= amount;
            hitFlash = 0.07f;
            SetFlash(true);

            bool killed = health <= 0f;
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.OnEnemyHit(this, amount, hitPoint, headshot, killed, source);

            Sfx.Play2D(headshot ? SfxId.Headshot : SfxId.Hit, headshot ? 0.7f : 0.5f, Random.Range(0.95f, 1.1f));
            FX.Burst(hitPoint, headshot ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.35f, 0.25f), headshot ? 8 : 4, 3.5f, 0.1f, 0.35f);

            if (killed) Die(headshot, source);
        }

        void Die(bool headshot, GameObject source)
        {
            if (IsDead) return;
            IsDead = true;
            All.Remove(this);
            if (laser != null) laser.enabled = false;
            Vector3 p = transform.position + Vector3.up * (type == EnemyType.Drone ? 0f : 1f) * scale;

            string glow = GlowMaterial();
            bool big = type == EnemyType.Boss;
            FX.Debris(p, glow, big ? 30 : 9, 0.13f * Mathf.Max(1f, scale * 0.7f), big ? 9f : 5f);
            FX.Debris(p, "EnemyDark", big ? 20 : 5, 0.16f * Mathf.Max(1f, scale * 0.7f), big ? 8f : 4f);
            if (big || type == EnemyType.Tank) FX.Explosion(p, big ? 7f : 4f);
            else FX.Burst(p, headshot ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.3f), 22, 6f, 0.14f, 0.6f);
            Sfx.Play3D(big || type == EnemyType.Tank ? SfxId.Explosion : SfxId.EnemyDie, p, 1f, Random.Range(0.9f, 1.1f));

            GameManager gm = GameManager.Instance;
            if (gm != null && transform.position.y > -20f) gm.OnEnemyKilled(this, headshot, source);

            // drops
            Vector3 drop = new Vector3(transform.position.x, transform.position.y + 0.6f, transform.position.z);
            if (type == EnemyType.Drone) drop.y = 0.6f;
            if (big)
            {
                Pickup.Spawn(PickupType.Medkit, drop + new Vector3(1.5f, 0f, 0f));
                Pickup.Spawn(PickupType.Ammo, drop + new Vector3(-1.5f, 0f, 0f));
                Pickup.Spawn(PickupType.Grenade, drop + new Vector3(0f, 0f, 1.5f));
                Pickup.Spawn(Random.value < 0.5f ? PickupType.DoubleDamage : PickupType.SlowMotion, drop + new Vector3(0f, 0f, -1.5f));
            }
            else if (transform.position.y > -20f)
            {
                float r = Random.value;
                float power = type == EnemyType.Tank || type == EnemyType.Sniper ? 0.12f : 0.035f;
                if (r < power) Pickup.Spawn(Random.value < 0.5f ? PickupType.DoubleDamage : PickupType.SlowMotion, drop);
                else if (r < power + 0.07f) Pickup.Spawn(PickupType.Medkit, drop);
                else if (r < power + 0.26f) Pickup.Spawn(PickupType.Ammo, drop);
                else if (r < power + 0.34f) Pickup.Spawn(PickupType.Grenade, drop);
            }
            Destroy(gameObject);
        }

        public string GlowMaterial()
        {
            switch (type)
            {
                case EnemyType.Gunner: return "NeonMagenta";
                case EnemyType.Drone: return "NeonYellow";
                case EnemyType.Boss: return "NeonOrange";
                case EnemyType.Sniper: return "NeonGreen";
                case EnemyType.Tank: return "NeonCyan";
                default: return "NeonRed";
            }
        }

        public static string TypeName(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Gunner: return Loc.T("Gunner");
                case EnemyType.Drone: return Loc.T("Drone");
                case EnemyType.Boss: return Loc.T("Overlord");
                case EnemyType.Sniper: return Loc.T("Sniper");
                case EnemyType.Tank: return Loc.T("Shield tank");
                default: return Loc.T("Grunt");
            }
        }
    }
}
