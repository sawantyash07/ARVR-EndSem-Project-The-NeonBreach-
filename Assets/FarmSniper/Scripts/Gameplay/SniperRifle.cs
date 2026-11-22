using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Bolt-action scoped rifle.
    ///  - Ammunition: detachable box magazine + finite reserve; reload (partial reloads keep the rest), auto-reload on empty.
    ///  - Accuracy: a cone that depends on hip / scoped, movement, being airborne, crouching and shot bloom.
    ///  - Scope: smooth aim-down-sights, variable magnification (scroll / D-pad), breathing sway that you can steady by
    ///    holding your breath (Sprint key while scoped, limited).
    ///  - Ballistics: every bullet is simulated (muzzle velocity + gravity) with swept raycasts each frame, so a moving
    ///    rat far away needs a little lead and hold-over - hits are exact against the rat's body and head colliders.
    ///  - Feel: recoil kick (camera on desktop, none in VR for comfort), weapon kick, bolt-cycle and reload animations,
    ///    muzzle flash + smoke, synthesized shot with valley echo, haptics on gamepad and VR controllers.
    /// Desktop: the rifle sits in front of the head. VR: it is held in the right hand and the scope lens shows a live
    /// magnified picture (a headset's field of view cannot be zoomed).
    /// </summary>
    public class SniperRifle : MonoBehaviour
    {
        [Header("Model parts (assigned by the scene builder)")]
        public FarmPlayer owner;
        public Transform model;
        public Transform muzzle;
        public Transform bolt;
        public Transform magazine;
        public Transform scopeEye;
        public Renderer scopeLens;
        public Camera scopeCamera;
        public Light muzzleLight;
        public Renderer muzzleFlash;

        [Header("Ammunition")]
        [Min(1)] public int magazineSize = 5;
        [Min(0)] public int startReserve = 20;
        [Min(0)] public int maxReserve = 40;
        public bool autoReloadWhenEmpty = true;

        [Header("Handling (seconds)")]
        [Min(0.1f)] public float boltCycleTime = 1.05f;
        [Min(0.3f)] public float reloadTime = 2.7f;
        [Min(0.05f)] public float adsTime = 0.24f;

        [Header("Ballistics")]
        [Tooltip("Metres per second.")]
        [Min(50f)] public float muzzleVelocity = 820f;
        [Range(0f, 3f)] public float gravityMultiplier = 1f;
        [Min(10f)] public float maxRange = 600f;
        public LayerMask hitMask = (1 << 0) | (1 << 9) | (1 << 10);

        [Header("Accuracy (cone, degrees)")]
        public float hipSpread = 2.6f;
        public float scopedSpread = 0.02f;
        public float vrSpread = 0.05f;
        [Tooltip("Added at full movement speed.")]
        public float moveSpread = 1.6f;
        public float airSpread = 4f;
        [Range(0.1f, 1f)] public float crouchMultiplier = 0.6f;
        [Tooltip("Extra spread right after a shot, recovering over time.")]
        public float shotBloom = 1.2f;
        public float bloomRecovery = 2.5f;

        [Header("Scope")]
        public float[] zoomLevels = { 4f, 8f, 12f };
        public int defaultZoom = 1;
        public float zoomSmoothing = 12f;
        [Tooltip("Breathing sway while scoped, degrees (scaled by the difficulty).")]
        public float swayAmplitude = 0.32f;
        public float swayFrequency = 0.45f;
        [Tooltip("Seconds you can hold your breath.")]
        public float holdBreathTime = 4.5f;
        [Tooltip("Breath regained per second (fraction of the full breath).")]
        public float breathRecovery = 0.3f;
        [Range(0f, 1f)] public float heldBreathSway = 0.12f;
        public float exhaustedSwayMultiplier = 2.2f;
        [Tooltip("Mouse / stick sensitivity scales with 1 / magnification to the power of this.")]
        [Range(0f, 1f)] public float scopedSensitivityExponent = 0.85f;
        public float eyeRelief = 0.07f;

        [Header("Recoil")]
        public float recoilPitch = 3.4f;
        public float recoilYaw = 0.9f;
        public float weaponKickBack = 0.11f;
        public float weaponKickPitch = 7f;
        public float kickRecovery = 9f;

        [Header("Poses (local to the head)")]
        public Vector3 hipPosition = new Vector3(0.21f, -0.24f, 0.5f);
        public Vector3 hipEuler = new Vector3(2f, -3f, 0f);
        public Vector3 thirdPersonPosition = new Vector3(0.17f, -0.12f, 0.28f);
        public Vector3 reloadEuler = new Vector3(-12f, 6f, 38f);
        public float lookLag = 0.35f;

        [Header("Effects")]
        public bool showBulletTrail = true;
        public Color trailColor = new Color(1f, 0.96f, 0.85f, 0.28f);
        public bool impactDecals = true;
        public float flashDuration = 0.05f;

        [Header("VR")]
        public Vector3 vrGripPosition = new Vector3(0f, -0.035f, -0.16f);
        public Vector3 vrGripEuler = new Vector3(8f, 0f, 0f);
        [Tooltip("Field of view of the scope camera at 1x; divided by the magnification.")]
        public float vrScopeFov = 28f;
        [Range(128, 2048)] public int vrScopeResolution = 512;

        // ------------------------------------------------------------------ state

        public int Mag { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get; private set; }
        public bool Cycling { get; private set; }
        public float AdsAmount { get; private set; }
        /// <summary>Desktop scope overlay is up (rifle hidden, FOV zoomed).</summary>
        public bool Scoped { get { return AdsAmount > 0.92f && !VrMode; } }
        public float Zoom { get { return zoomLevels.Length > 0 ? zoomLevels[Mathf.Clamp(zoomIndex, 0, zoomLevels.Length - 1)] : 1f; } }
        public float CurrentZoom { get; private set; }
        public float Breath { get; private set; }
        public bool HoldingBreath { get; private set; }
        public bool Exhausted { get; private set; }
        public float CurrentSpread { get; private set; }
        public Vector2 SwayOffset { get; private set; }
        public float ReloadProgress { get { return Reloading ? Mathf.Clamp01(reloadT / reloadTime) : 0f; } }
        public float CycleProgress { get { return Cycling ? Mathf.Clamp01(cycleT / boltCycleTime) : 0f; } }
        public float SensitivityScale { get { return 1f / Mathf.Pow(Mathf.Lerp(1f, CurrentZoom, AdsAmount), scopedSensitivityExponent); } }
        public bool VrMode { get { return owner != null && owner.VrActive; } }

        struct Bullet
        {
            public Vector3 pos, vel, origin;
            public float travelled;
            public bool trailStarted;
        }

        readonly List<Bullet> bullets = new List<Bullet>();
        int zoomIndex;
        float reloadT, cycleT, flashUntil, bloom;
        float kickBack, kickPitch;
        int reloadStage, cycleStage;
        Vector3 adsPosition;
        Vector3 boltBasePos, magBasePos;
        Quaternion boltBaseRot;
        Vector2 lagOffset;
        Renderer[] renderers;
        RenderTexture scopeRT;
        Material lensMat;
        bool wasHeldBreath;
        float swaySeed;
        bool lastVr;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            if (bolt != null) { boltBasePos = bolt.localPosition; boltBaseRot = bolt.localRotation; }
            if (magazine != null) magBasePos = magazine.localPosition;
            Vector3 eye = scopeEye != null ? transform.InverseTransformPoint(scopeEye.position) : new Vector3(0f, 0.08f, 0f);
            adsPosition = new Vector3(0f, 0f, eyeRelief) - eye;
            swaySeed = Random.value * 50f;
            if (scopeCamera != null) scopeCamera.enabled = false;
            if (muzzleLight != null) muzzleLight.enabled = false;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            ResetRifle();
        }

        void OnDestroy()
        {
            if (scopeRT != null) { scopeRT.Release(); Destroy(scopeRT); }
            if (lensMat != null) Destroy(lensMat);
        }

        public void ResetRifle()
        {
            Mag = magazineSize;
            Reserve = Mathf.Min(startReserve, maxReserve);
            Reloading = Cycling = false;
            zoomIndex = Mathf.Clamp(defaultZoom, 0, Mathf.Max(0, zoomLevels.Length - 1));
            CurrentZoom = Zoom;
            Breath = 1f;
            Exhausted = false;
            bloom = 0f;
            bullets.Clear();
            if (bolt != null) { bolt.localPosition = boltBasePos; bolt.localRotation = boltBaseRot; }
            if (magazine != null) { magazine.localPosition = magBasePos; magazine.gameObject.SetActive(true); }
        }

        /// <summary>Adds reserve rounds (ammo crate). Returns how many were added.</summary>
        public int AddReserve(int rounds)
        {
            int room = maxReserve - Reserve;
            int add = Mathf.Clamp(rounds, 0, room);
            Reserve += add;
            return add;
        }

        // ------------------------------------------------------------------ per-frame (called by FarmPlayer)

        public void Tick(PlayerInputState input, bool allowFire, float dt)
        {
            bool vr = VrMode;
            if (vr != lastVr) { lastVr = vr; SetupVrScope(vr); }

            // --- aim down sights (desktop / gamepad: hold aim; VR: the scope is always live, no ADS pose)
            bool wantAds = !vr && allowFire && input.aimHeld && !Reloading && owner != null && !owner.Sprinting;
            AdsAmount = Mathf.MoveTowards(AdsAmount, wantAds ? 1f : 0f, dt / adsTime);

            // --- magnification
            if (allowFire && (Scoped || vr) && input.weaponCycle != 0 && zoomLevels.Length > 1)
            {
                int ni = Mathf.Clamp(zoomIndex + input.weaponCycle, 0, zoomLevels.Length - 1);
                if (ni != zoomIndex) { zoomIndex = ni; FarmAudio.Play2D(FarmSound.ScopeZoom, 0.6f); }
            }
            if (vr && allowFire && owner != null && owner.VrZoomPressed) { zoomIndex = (zoomIndex + 1) % Mathf.Max(1, zoomLevels.Length); FarmAudio.Play2D(FarmSound.ScopeZoom, 0.6f); }
            CurrentZoom = Mathf.Lerp(CurrentZoom, Zoom, 1f - Mathf.Exp(-zoomSmoothing * dt));

            // --- breath + sway (desktop scope only; in VR your own hands sway)
            UpdateBreath(input, dt);

            // --- spread
            bloom = Mathf.MoveTowards(bloom, 0f, bloomRecovery * dt);
            float moveK = owner != null ? Mathf.Clamp01(owner.MoveSpeed / 4f) : 0f;
            float spread = vr ? vrSpread : Mathf.Lerp(hipSpread, scopedSpread, AdsAmount * AdsAmount);
            spread += moveSpread * moveK;
            if (owner != null && !owner.Grounded) spread += airSpread;
            spread += bloom;
            if (owner != null && owner.Crouching) spread *= crouchMultiplier;
            CurrentSpread = spread;

            // --- actions
            if (allowFire)
            {
                if (input.reloadPressed) StartReload();
                if (input.firePressed) TryFire();
            }

            UpdateCycle(dt);
            UpdateReload(dt);
            UpdateBullets(dt);
            UpdatePose(input, dt, vr);
            UpdateEffects(vr);
        }

        void UpdateBreath(PlayerInputState input, float dt)
        {
            bool scoped = Scoped;
            float diff = FarmGameManager.Instance != null && FarmGameManager.Instance.Profile != null ? FarmGameManager.Instance.Profile.swayMultiplier : 1f;
            bool hold = scoped && input.sprintHeld && !Exhausted && Breath > 0f;
            HoldingBreath = hold;
            if (hold) Breath = Mathf.Max(0f, Breath - dt / holdBreathTime);
            else Breath = Mathf.Min(1f, Breath + breathRecovery * dt);
            if (Breath <= 0f && !Exhausted) Exhausted = true;
            if (Exhausted && Breath >= 0.5f) Exhausted = false;
            if (wasHeldBreath && !hold) FarmAudio.Play2D(FarmSound.Breath, Exhausted ? 0.9f : 0.5f);
            wasHeldBreath = hold;

            if (!scoped) { SwayOffset = Vector2.Lerp(SwayOffset, Vector2.zero, dt * 8f); return; }
            float t = Time.time * swayFrequency;
            float k = swayAmplitude * diff * (hold ? heldBreathSway : (Exhausted ? exhaustedSwayMultiplier : 1f));
            if (owner != null && owner.Crouching) k *= 0.7f;
            float px = (Mathf.PerlinNoise(t, swaySeed) - 0.5f) * 2f;
            float py = (Mathf.PerlinNoise(swaySeed, t * 1.3f) - 0.5f) * 2f;
            // figure-eight breathing component
            px += Mathf.Sin(t * 2.1f) * 0.35f;
            py += Mathf.Sin(t * 4.2f) * 0.25f;
            SwayOffset = Vector2.Lerp(SwayOffset, new Vector2(px, py) * k, dt * 10f);
        }

        void TryFire()
        {
            if (Reloading || Cycling) return;
            if (Mag <= 0)
            {
                FarmAudio.Play2D(FarmSound.DryFire, 0.8f);
                if (autoReloadWhenEmpty && Reserve > 0) StartReload();
                return;
            }
            Mag--;

            Vector3 origin, dir;
            AimRay(out origin, out dir);
            Vector2 cone = Random.insideUnitCircle * (CurrentSpread * 0.5f);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(dir, right);
            dir = Quaternion.AngleAxis(cone.x, up) * Quaternion.AngleAxis(-cone.y, right) * dir;

            Bullet b = new Bullet { pos = origin, origin = origin, vel = dir.normalized * muzzleVelocity, travelled = 0f };
            bullets.Add(b);
            // resolve the first slice immediately so point-blank shots never lag a frame
            StepBullet(bullets.Count - 1, Time.deltaTime > 0f ? Time.deltaTime : 0.016f);

            bloom += shotBloom;
            kickBack = weaponKickBack;
            kickPitch = weaponKickPitch;
            flashUntil = Time.time + flashDuration;
            Vector3 mp = muzzle != null ? muzzle.position : origin;
            FX.Burst(mp + dir * 0.15f, new Color(0.82f, 0.80f, 0.76f, 0.45f), 6, 0.6f, 0.12f, 0.9f);
            FarmAudio.Play2D(FarmSound.RifleShot, 1f, Random.Range(0.96f, 1.04f));

            if (owner != null)
            {
                if (!VrMode) owner.AddRecoil(recoilPitch * Random.Range(0.85f, 1.15f), recoilYaw * Random.Range(-1f, 1f));
                if (VrMode) Haptics.PulseXR(XRNode.RightHand, 1f, 0.09f);
                if (GamepadInput.Connected(0)) Haptics.Rumble(0, 0.7f, 1f, 0.12f);
            }

            FarmGameManager gm = FarmGameManager.Instance;
            if (gm != null)
            {
                gm.RegisterShot();
                if (gm.spawner != null) gm.spawner.NotifyNoise(origin, 1f);
            }

            Cycling = true;
            cycleT = 0f;
            cycleStage = 0;
        }

        /// <summary>Where shots go: camera centre on desktop (third person: from the muzzle towards the crosshair target), muzzle in VR.</summary>
        public void AimRay(out Vector3 origin, out Vector3 dir)
        {
            if (VrMode || owner == null || owner.cam == null)
            {
                Transform m = muzzle != null ? muzzle : transform;
                origin = m.position;
                dir = m.forward;
                return;
            }
            Transform c = owner.cam.transform;
            if (!owner.ThirdPerson || Scoped)
            {
                origin = c.position;
                dir = c.forward;
                return;
            }
            RaycastHit hit;
            Vector3 target = c.position + c.forward * maxRange;
            if (Physics.Raycast(c.position, c.forward, out hit, maxRange, hitMask, QueryTriggerInteraction.Ignore)) target = hit.point;
            origin = muzzle != null ? muzzle.position : c.position;
            dir = (target - origin).normalized;
        }

        public void StartReload()
        {
            if (Reloading || Mag >= magazineSize || Reserve <= 0) return;
            Reloading = true;
            reloadT = 0f;
            reloadStage = 0;
        }

        void UpdateCycle(float dt)
        {
            if (!Cycling || bolt == null)
            {
                if (Cycling) { cycleT += dt; if (cycleT >= boltCycleTime) Cycling = false; }
                return;
            }
            cycleT += dt;
            float k = cycleT / boltCycleTime;
            // 0-.15 lift, .15-.45 pull back, .45-.75 push forward, .75-.9 lock down
            float lift = k < 0.15f ? k / 0.15f : (k < 0.75f ? 1f : Mathf.Clamp01(1f - (k - 0.75f) / 0.15f));
            float back = k < 0.15f ? 0f : (k < 0.45f ? (k - 0.15f) / 0.3f : Mathf.Clamp01(1f - (k - 0.45f) / 0.3f));
            if (cycleStage == 0 && k > 0.12f) { cycleStage = 1; FarmAudio.Play2D(FarmSound.BoltOpen, 0.8f); }
            if (cycleStage == 1 && k > 0.68f) { cycleStage = 2; FarmAudio.Play2D(FarmSound.BoltClose, 0.8f); }
            BoltPose(lift, back);
            if (cycleT >= boltCycleTime)
            {
                Cycling = false;
                BoltPose(0f, 0f);
                if (Mag <= 0 && autoReloadWhenEmpty && Reserve > 0) StartReload();
            }
        }

        void BoltPose(float lift, float back)
        {
            if (bolt == null) return;
            bolt.localRotation = boltBaseRot * Quaternion.Euler(0f, 0f, 70f * Mathf.SmoothStep(0f, 1f, lift));
            bolt.localPosition = boltBasePos + Vector3.back * 0.075f * Mathf.SmoothStep(0f, 1f, back);
        }

        void UpdateReload(float dt)
        {
            if (!Reloading) return;
            reloadT += dt;
            float k = reloadT / reloadTime;
            if (reloadStage == 0 && k > 0.1f) { reloadStage = 1; FarmAudio.Play2D(FarmSound.MagOut, 0.8f); }
            if (reloadStage == 1 && k > 0.55f) { reloadStage = 2; FarmAudio.Play2D(FarmSound.MagIn, 0.9f); }
            if (reloadStage == 2 && k > 0.7f) { reloadStage = 3; FarmAudio.Play2D(FarmSound.BoltOpen, 0.7f); }
            if (reloadStage == 3 && k > 0.86f) { reloadStage = 4; FarmAudio.Play2D(FarmSound.BoltClose, 0.8f); }

            if (magazine != null)
            {
                // old magazine drops out, a fresh one is pushed in
                float drop = k < 0.12f ? 0f : (k < 0.3f ? (k - 0.12f) / 0.18f : (k < 0.38f ? 1f : Mathf.Clamp01(1f - (k - 0.38f) / 0.17f)));
                magazine.localPosition = magBasePos + Vector3.down * 0.14f * Mathf.SmoothStep(0f, 1f, drop);
                magazine.gameObject.SetActive(!(k > 0.28f && k < 0.4f));
            }
            if (bolt != null)
            {
                float lift = k < 0.68f ? 0f : (k < 0.72f ? (k - 0.68f) / 0.04f : (k < 0.86f ? 1f : Mathf.Clamp01(1f - (k - 0.86f) / 0.05f)));
                float back = k < 0.72f ? 0f : (k < 0.78f ? (k - 0.72f) / 0.06f : Mathf.Clamp01(1f - (k - 0.78f) / 0.07f));
                BoltPose(lift, back);
            }
            if (reloadT >= reloadTime)
            {
                int need = magazineSize - Mag;
                int take = Mathf.Min(need, Reserve);
                Mag += take;
                Reserve -= take;
                Reloading = false;
                Cycling = false;
                if (magazine != null) { magazine.localPosition = magBasePos; magazine.gameObject.SetActive(true); }
                BoltPose(0f, 0f);
            }
        }

        // ------------------------------------------------------------------ bullets

        void UpdateBullets(float dt)
        {
            if (bullets.Count == 0) return;
            for (int i = bullets.Count - 1; i >= 0; i--) StepBullet(i, dt);
        }

        void StepBullet(int i, float dt)
        {
            if (i < 0 || i >= bullets.Count) return;
            Physics.SyncTransforms();   // rats move by transform: make the colliders current before the sweep
            Bullet b = bullets[i];
            Vector3 vel = b.vel + Physics.gravity * gravityMultiplier * dt;
            Vector3 step = (b.vel + vel) * 0.5f * dt;
            float len = step.magnitude;
            RaycastHit hit;
            bool done = false;
            if (len > 0f && Physics.Raycast(b.pos, step / len, out hit, len, hitMask, QueryTriggerInteraction.Ignore))
            {
                Trail(ref b, hit.point);
                OnHit(hit, b);
                done = true;
            }
            else
            {
                Vector3 np = b.pos + step;
                Trail(ref b, np);
                b.pos = np;
                b.vel = vel;
                b.travelled += len;
                if (b.travelled >= maxRange)
                {
                    done = true;
                    FarmGameManager gm = FarmGameManager.Instance;
                    if (gm != null) gm.RegisterMiss();
                }
            }
            if (done) bullets.RemoveAt(i);
            else bullets[i] = b;
        }

        void Trail(ref Bullet b, Vector3 to)
        {
            if (!showBulletTrail) return;
            // start the streak a few metres out so it never fills the screen in first person
            Vector3 from = b.pos;
            float fromDist = Vector3.Distance(b.origin, from);
            float toDist = Vector3.Distance(b.origin, to);
            if (toDist < 4f) return;
            if (fromDist < 4f) from = b.origin + (to - b.origin).normalized * 4f;
            b.trailStarted = true;
            FX.Tracer(from, to, trailColor);
        }

        void OnHit(RaycastHit hit, Bullet b)
        {
            FarmGameManager gm = FarmGameManager.Instance;
            RatAI rat = hit.collider.GetComponentInParent<RatAI>();
            if (rat != null && !rat.IsDead)
            {
                HitZone zone = hit.collider.GetComponent<HitZone>();
                bool head = zone != null && zone.isHead;
                float dist = Vector3.Distance(b.origin, hit.point);
                bool moving = rat.IsRunning;
                rat.Kill(hit.point, b.vel.normalized, head);
                FarmAudio.Play2D(FarmSound.HitConfirm, 0.9f);
                FarmAudio.Play2D(FarmSound.KillConfirm, 0.6f);
                if (VrMode) Haptics.PulseXR(XRNode.RightHand, 0.35f, 0.04f);
                if (gm != null)
                {
                    gm.RegisterKill(dist, head, moving, hit.point);
                    // the squeal and the bullet strike scare the rats nearby
                    if (gm.spawner != null) gm.spawner.NotifyNoise(hit.point, 0.6f);
                }
                return;
            }

            SurfaceKind kind = FarmSurface.Of(hit.collider);
            Impact(hit.point, hit.normal, kind);
            if (impactDecals && kind != SurfaceKind.Water && kind != SurfaceKind.Foliage) FX.Decal(hit.point, hit.normal);
            if (gm != null)
            {
                gm.RegisterMiss();
                if (gm.spawner != null) gm.spawner.NotifyNoise(hit.point, 0.45f);
            }
        }

        static void Impact(Vector3 p, Vector3 n, SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Wood:
                    FX.Sparks(p, n, new Color(0.62f, 0.48f, 0.32f, 1f), 9);
                    FX.Burst(p + n * 0.05f, new Color(0.7f, 0.62f, 0.5f, 0.5f), 4, 0.4f, 0.1f, 0.6f);
                    FarmAudio.Play3D(FarmSound.ImpactWood, p, 0.9f, Random.Range(0.9f, 1.1f), 2f, 60f);
                    break;
                case SurfaceKind.Metal:
                    FX.Sparks(p, n, new Color(1f, 0.85f, 0.45f, 1f), 10);
                    FarmAudio.Play3D(FarmSound.ImpactMetal, p, 0.8f, Random.Range(0.9f, 1.15f), 2f, 80f);
                    break;
                case SurfaceKind.Water:
                    FX.Sparks(p, Vector3.up, new Color(0.85f, 0.92f, 1f, 0.9f), 14);
                    FX.Burst(p, new Color(0.9f, 0.95f, 1f, 0.5f), 6, 0.8f, 0.14f, 0.6f);
                    FarmAudio.Play3D(FarmSound.ImpactWater, p, 0.9f, Random.Range(0.9f, 1.1f), 2f, 60f);
                    break;
                case SurfaceKind.Hay:
                    FX.Sparks(p, n, new Color(0.9f, 0.8f, 0.5f, 1f), 10);
                    FX.Burst(p, new Color(0.85f, 0.78f, 0.55f, 0.45f), 5, 0.5f, 0.14f, 0.9f);
                    FarmAudio.Play3D(FarmSound.ImpactHay, p, 0.8f, 1f, 2f, 40f);
                    break;
                case SurfaceKind.Stone:
                    FX.Sparks(p, n, new Color(0.75f, 0.74f, 0.7f, 1f), 9);
                    FX.Burst(p, new Color(0.7f, 0.7f, 0.68f, 0.5f), 5, 0.5f, 0.15f, 0.9f);
                    FarmAudio.Play3D(FarmSound.ImpactWood, p, 0.7f, 1.6f, 2f, 60f);
                    break;
                case SurfaceKind.Foliage:
                    FX.Sparks(p, n, new Color(0.35f, 0.55f, 0.2f, 1f), 8);
                    FarmAudio.Play3D(FarmSound.ImpactHay, p, 0.6f, 1.3f, 2f, 40f);
                    break;
                default:
                    FX.Sparks(p, n, new Color(0.45f, 0.36f, 0.26f, 1f), 10);
                    FX.Burst(p + n * 0.05f, new Color(0.55f, 0.47f, 0.37f, 0.55f), 7, 0.7f, 0.2f, 1.1f);
                    FarmAudio.Play3D(FarmSound.ImpactDirt, p, 0.9f, Random.Range(0.9f, 1.1f), 2f, 60f);
                    break;
            }
        }

        // ------------------------------------------------------------------ pose + effects

        void UpdatePose(PlayerInputState input, float dt, bool vr)
        {
            kickBack = Mathf.Lerp(kickBack, 0f, 1f - Mathf.Exp(-kickRecovery * dt));
            kickPitch = Mathf.Lerp(kickPitch, 0f, 1f - Mathf.Exp(-kickRecovery * dt));

            if (vr)
            {
                transform.localPosition = vrGripPosition + Vector3.back * kickBack * 0.4f;
                transform.localRotation = Quaternion.Euler(vrGripEuler) * Quaternion.Euler(-kickPitch * 0.4f, 0f, 0f);
                SetVisible(true);
                return;
            }

            // weapon lags slightly behind fast looking, settles back (desktop)
            Vector2 look = input.look * (owner != null ? owner.lookSensitivity : 1f);
            lagOffset = Vector2.Lerp(lagOffset, Vector2.ClampMagnitude(-look * lookLag, 4f), 1f - Mathf.Exp(-10f * dt));

            float ads = Mathf.SmoothStep(0f, 1f, AdsAmount);
            float reloadK = Reloading ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(reloadT / reloadTime)) : 0f;
            bool third = owner != null && owner.ThirdPerson;
            Vector3 hip = third ? thirdPersonPosition : hipPosition;
            Vector3 pos = Vector3.Lerp(hip, adsPosition, ads);
            if (owner != null && !third) pos += owner.WeaponBob * (1f - ads * 0.85f);
            pos += Vector3.back * kickBack + Vector3.down * reloadK * 0.06f;
            if (Cycling && !third) pos += Vector3.down * 0.015f * Mathf.Sin(Mathf.PI * CycleProgress) * (1f - ads * 0.5f);

            Quaternion rot = Quaternion.Euler(Vector3.Lerp(hipEuler, Vector3.zero, ads));
            rot *= Quaternion.Euler(-kickPitch + lagOffset.y * (1f - ads), lagOffset.x * (1f - ads), -lagOffset.x * 0.6f * (1f - ads));
            rot *= Quaternion.Euler(reloadEuler * reloadK);
            transform.localPosition = pos;
            transform.localRotation = rot;
            SetVisible(!Scoped);
        }

        void SetVisible(bool on)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r == muzzleFlash) continue;
                if (r.enabled != on) r.enabled = on;
            }
        }

        void UpdateEffects(bool vr)
        {
            bool flash = Time.time < flashUntil;
            if (muzzleLight != null) muzzleLight.enabled = flash;
            if (muzzleFlash != null)
            {
                muzzleFlash.enabled = flash && !Scoped;
                if (flash) muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            if (scopeCamera != null && vr)
            {
                scopeCamera.fieldOfView = vrScopeFov / Mathf.Max(1f, CurrentZoom);
            }
        }

        void SetupVrScope(bool vr)
        {
            if (scopeCamera == null) return;
            scopeCamera.enabled = vr;
            if (!vr) return;
            if (scopeRT == null)
            {
                scopeRT = new RenderTexture(vrScopeResolution, vrScopeResolution, 16);
                scopeRT.name = "ScopeRT";
            }
            scopeCamera.targetTexture = scopeRT;
            if (scopeLens != null)
            {
                if (lensMat == null) lensMat = new Material(scopeLens.sharedMaterial);
                lensMat.mainTexture = scopeRT;
                scopeLens.sharedMaterial = lensMat;
            }
        }
    }
}
