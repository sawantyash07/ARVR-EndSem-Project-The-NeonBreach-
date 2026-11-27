using System.Collections;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// First-person character: look, walk, sprint (stamina), crouch, slide, jump, dash, flashlight, medkits,
    /// shield + health with regeneration, camera effects (bob, recoil, shake, FOV kick).
    /// Reads a device-independent PlayerInputState, filled by DesktopInput, GamepadInput and/or the VR rig.
    /// Player 1 (index 0) uses keyboard+mouse (+ a gamepad when solo) or VR; player 2 (co-op) uses a gamepad.
    /// </summary>
    /// <summary>A mode that moves the player itself (parachute). Return true to skip the normal look/move/camera.</summary>
    public interface IPlayerDriver
    {
        bool DrivePlayer(PlayerController p, float dt);
    }

    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("References (auto-found if empty)")]
        public Transform cameraPivot;
        public Camera cam;
        public WeaponSystem weapons;
        public Light flashlight;
        public VRRig vr;

        [Header("Identity / input")]
        public int index;
        public bool useKeyboardMouse = true;
        public int gamepadIndex = -1;

        [Header("Movement")]
        public float walkSpeed = 5.5f;
        public float sprintSpeed = 9.5f;
        public float crouchSpeed = 2.8f;
        public float jumpHeight = 1.25f;
        public float gravity = -26f;
        public float dashSpeed = 21f;
        public float dashTime = 0.16f;
        public float dashCooldown = 1.6f;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float baseShield = 60f;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate = 25f;
        public float maxStamina = 100f;

        [System.NonSerialized] public PlayerInputState Inp = new PlayerInputState();   // per-frame runtime state, never saved

        // ---- hooks for the mini-game modes (see Assets/Scripts/Modes). All neutral by default = shooter behaviour.
        /// <summary>When set, the driver replaces look, movement and camera (parachute). Input is still read here.</summary>
        public IPlayerDriver Driver { get; set; }
        /// <summary>&gt; 0: field of view to use instead of the normal one (archery zoom).</summary>
        public float FovOverride;
        /// <summary>Extra desktop camera rotation in degrees (x = yaw, y = pitch), e.g. breath sway while aiming a bow.</summary>
        public Vector2 AimOffset;
        /// <summary>Mouse / stick look sensitivity multiplier.</summary>
        public float LookScale = 1f;
        /// <summary>Sprint key does something else in this mode (archery: hold breath).</summary>
        public bool BlockSprint;
        public CharacterController Body { get { return cc; } }
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }

        /// <summary>Set the desktop look angles (keeps the internal state in sync when a mode moved the camera).</summary>
        public void SetLook(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Clear every mode hook (back to the plain shooter controller).</summary>
        public void ClearModeHooks()
        {
            Driver = null;
            FovOverride = 0f;
            AimOffset = Vector2.zero;
            LookScale = 1f;
            BlockSprint = false;
        }

        /// <summary>Debug / automated smoke test only: ignore all damage.</summary>
        public static bool GodMode;

        public float maxShield { get { return baseShield + Upgrades.ShieldBonus; } }
        public float Health { get; private set; }
        public float Shield { get; private set; }
        public float Stamina { get; private set; }
        public int Medkits { get; private set; }
        public bool IsDead { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sliding { get { return slideTimer > 0f; } }
        public bool Grounded { get; private set; }
        public bool UsingMedkit { get; private set; }
        public bool FlashlightOn { get { return flashlight != null && flashlight.enabled; } }
        public float DashReady { get { return 1f - Mathf.Clamp01(dashCd / dashCooldown); } }
        public Vector3 Velocity { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public Vector3 BobOffset { get; private set; }
        public float LastDamageTime { get; private set; }
        public bool VrOn { get { return vr != null && vr.Active; } }

        // per-player run stats (co-op summary) + counters the tutorial watches
        public int Kills;
        public float DistanceMoved { get; private set; }
        public float LookAmount { get; private set; }
        public int Jumps { get; private set; }
        public int Slides { get; private set; }
        public int Dashes { get; private set; }
        public float SprintTime { get; private set; }

        /// <summary>Approximate centre of the player's body (enemy aim point).</summary>
        public Vector3 BodyCenter
        {
            get
            {
                Vector3 head = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
                return head + Vector3.down * 0.45f;
            }
        }

        /// <summary>Accuracy penalty from moving / jumping / crouching (1 = standing still).</summary>
        public float SpreadMultiplier
        {
            get
            {
                float m = 1f;
                float speed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
                m += Mathf.Clamp01(speed / sprintSpeed) * 0.8f;
                if (!Grounded) m += 1.2f;
                if (Crouching) m *= 0.7f;
                return m;
            }
        }

        CharacterController cc;
        float yaw, pitch, roll;
        float recoil;
        float shake;
        Vector3 horiz;
        float vy;
        bool prevCrouch;
        bool wasGrounded = true;
        float slideTimer;
        Vector3 slideDir;
        float dashTimer, dashCd;
        Vector3 dashDir;
        bool sprintLocked;
        float lastSprintTime;
        float bobT;
        float stepDist;
        float eyeHeight = 1.62f;
        bool lastVr;
        float fallSpeed;

        // ----------------------------------------------------------------------------------------------

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cameraPivot == null && cam != null) cameraPivot = cam.transform.parent;
            if (weapons == null) weapons = GetComponentInChildren<WeaponSystem>();
            if (vr == null) vr = GetComponent<VRRig>();
            if (flashlight == null)
            {
                Transform f = cam != null ? cam.transform.Find("Flashlight") : null;
                if (f != null) flashlight = f.GetComponent<Light>();
            }
            yaw = transform.eulerAngles.y;
            Health = maxHealth;
            Shield = maxShield;
            Stamina = maxStamina;
            Medkits = 1;
            if (flashlight != null) flashlight.enabled = false;
            Util.SetFov(cam, GameSettings.Fov);
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || cam == null) return;
            float dt = Time.deltaTime;
            bool vrOn = VrOn;
            if (vrOn != lastVr) SwitchMode(vrOn);

            if (gm.State == GameState.Menu) { MenuOrbit(dt, vrOn); return; }
            if (IsDead) { DeathCam(dt, vrOn); return; }
            if (gm.State != GameState.Playing)
            {
                if (vrOn) FollowHead();
                return;
            }

            Inp.Clear();
            if (useKeyboardMouse) DesktopInput.Read(Inp);
            if (gamepadIndex >= 0) GamepadInput.Read(gamepadIndex, Inp);
            if (vrOn) vr.Read(Inp);
            LookDelta = Inp.look;
            LookAmount += Inp.look.magnitude + Mathf.Abs(Inp.snapTurn) * 0.2f;

            if (Driver != null && Driver.DrivePlayer(this, dt)) return;

            DoLook(vrOn);
            DoMove(dt, vrOn);
            DoActions();
            DoStats(dt);
            DoCamera(dt, vrOn);
        }

        // ----------------------------------------------------------------------------------------------
        // look
        // ----------------------------------------------------------------------------------------------

        void DoLook(bool vrOn)
        {
            if (vrOn)
            {
                // snap or smooth turn (VRRig fills snapTurn with the degrees for this frame)
                if (Mathf.Abs(Inp.snapTurn) > 0.001f)
                {
                    // rotate around the head, not the body origin, so the view does not swing sideways
                    Vector3 head = cam.transform.position;
                    yaw += Inp.snapTurn;
                    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 drift = cam.transform.position - head;
                    drift.y = 0f;
                    cameraPivot.position -= drift;
                }
                return;
            }
            float ads = weapons != null ? weapons.AdsAmount : 0f;
            float sens = GameSettings.Sensitivity * 2.2f * Mathf.Lerp(1f, 0.55f, ads) * LookScale;
            yaw += Inp.look.x * sens;
            float py = Inp.look.y * sens * (GameSettings.InvertY ? -1f : 1f);
            pitch = Mathf.Clamp(pitch - py, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void AddRecoil(float upDegrees, float sideDegrees)
        {
            if (VrOn) return;   // never move the camera on its own in VR (comfort)
            recoil = Mathf.Min(recoil + upDegrees, 14f);
            yaw += sideDegrees;
        }

        /// <summary>Point the desktop camera at a world position (used by the automated smoke test).</summary>
        public void LookAt(Vector3 point)
        {
            if (cam == null) return;
            Vector3 d = point - cam.transform.position;
            if (d.sqrMagnitude < 0.0001f) return;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Kill() { if (!IsDead) { Health = 0f; Die(); } }

        public void Shake(float amount)
        {
            if (VrOn) return;   // camera shake makes people sick in a headset; haptics are used instead
            shake = Mathf.Min(shake + amount * GameSettings.ShakeIntensity, 0.6f);
        }

        // ----------------------------------------------------------------------------------------------
        // movement
        // ----------------------------------------------------------------------------------------------

        void DoMove(float dt, bool vrOn)
        {
            bool grounded = cc.isGrounded;
            float ads = weapons != null ? weapons.AdsAmount : 0f;

            // direction basis
            Vector3 fwd = transform.forward;
            if (vrOn && vr != null) fwd = vr.HeadForwardFlat;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // crouch / slide
            bool crouchHeld = Inp.crouchHeld;
            if (crouchHeld && !prevCrouch && Sprinting && grounded && slideTimer <= 0f)
            {
                slideTimer = 0.85f;
                slideDir = fwd;
                Slides++;
                Sfx.Play2D(SfxId.Dash, 0.5f, 0.7f);
            }
            prevCrouch = crouchHeld;
            Crouching = crouchHeld || slideTimer > 0f;

            // sprint + stamina
            bool wantSprint = Inp.sprintHeld && !BlockSprint && Inp.move.y > 0.1f && !Crouching && ads < 0.4f && !sprintLocked && Stamina > 0f;
            Sprinting = wantSprint;
            if (Sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - 20f * dt);
                lastSprintTime = Time.time;
                SprintTime += dt;
                if (Stamina <= 0f) sprintLocked = true;
            }
            else if (Time.time - lastSprintTime > 0.8f)
            {
                Stamina = Mathf.Min(maxStamina, Stamina + 24f * dt);
                if (sprintLocked && Stamina > 25f) sprintLocked = false;
            }

            // horizontal velocity
            float target = Crouching ? crouchSpeed : (Sprinting ? sprintSpeed : walkSpeed);
            target *= Mathf.Lerp(1f, 0.65f, ads);
            if (vrOn) target *= 0.75f;   // slower artificial locomotion is more comfortable in VR
            Vector3 wish = right * Inp.move.x + fwd * Inp.move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float accel = grounded ? 60f : 16f;
            horiz = Vector3.MoveTowards(horiz, wish * target, accel * dt);

            if (slideTimer > 0f)
            {
                slideTimer -= dt;
                float k = Mathf.Clamp01(slideTimer / 0.85f);
                horiz = slideDir * Mathf.Lerp(crouchSpeed, 13f, k);
                if (Inp.jumpPressed || !grounded) slideTimer = 0f;
            }

            dashCd -= dt;
            if (Inp.dashPressed && dashCd <= 0f)
            {
                dashDir = wish.sqrMagnitude > 0.01f ? wish.normalized : fwd;
                dashTimer = dashTime;
                dashCd = dashCooldown;
                Dashes++;
                Sfx.Play2D(SfxId.Dash, 0.8f);
            }
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                horiz = dashDir * dashSpeed;
            }

            // vertical
            if (grounded && vy < 0f) vy = -2f;
            if (Inp.jumpPressed && grounded && !Crouching)
            {
                vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
                Jumps++;
                Sfx.Play2D(SfxId.Jump, 0.5f);
            }
            vy += gravity * dt;
            if (vy < fallSpeed) fallSpeed = vy;

            Vector3 before = transform.position;
            CollisionFlags flags = cc.Move((horiz + Vector3.up * vy) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            DistanceMoved += moved.magnitude;

            Grounded = cc.isGrounded;
            if (Grounded && !wasGrounded)
            {
                if (fallSpeed < -9f)
                {
                    Sfx.Play2D(SfxId.Land, 0.8f);
                    Shake(Mathf.Clamp01(-fallSpeed / 30f) * 0.2f);
                }
                fallSpeed = 0f;
            }
            if (Grounded) fallSpeed = 0f;
            wasGrounded = Grounded;
            Velocity = horiz + Vector3.up * vy;

            // safety net: never fall out of the world
            if (transform.position.y < -15f) Teleport(GameManager.Instance.playerSpawn);

            // capsule height (crouch)
            float targetH = Crouching ? 1.1f : 1.8f;
            if (targetH > cc.height && Physics.CheckSphere(transform.position + Vector3.up * (cc.height + 0.15f), 0.3f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                targetH = cc.height;
            float h = Mathf.MoveTowards(cc.height, targetH, 5.5f * dt);
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);

            // footsteps + bob
            float speed = new Vector3(horiz.x, 0f, horiz.z).magnitude;
            if (Grounded && speed > 1f)
            {
                stepDist += speed * dt;
                float stride = Sprinting ? 3.1f : 2.3f;
                if (stepDist > stride)
                {
                    stepDist = 0f;
                    if (index == 0) Sfx.Play2D(SfxId.Step, Crouching ? 0.15f : 0.35f, Random.Range(0.85f, 1.1f));
                    else Sfx.Play3D(SfxId.Step, transform.position, 0.5f, Random.Range(0.85f, 1.1f));
                }
                bobT += speed * dt * 1.5f;
            }
            float bobAmt = Grounded && !vrOn ? Mathf.Clamp01(speed / sprintSpeed) : 0f;
            BobOffset = new Vector3(Mathf.Cos(bobT) * 0.008f, Mathf.Abs(Mathf.Sin(bobT)) * 0.012f, 0f) * bobAmt * 2f;
        }

        // ----------------------------------------------------------------------------------------------
        // actions + stats
        // ----------------------------------------------------------------------------------------------

        void DoActions()
        {
            if (Inp.flashlightPressed && flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
                Sfx.Play2D(SfxId.Click, 0.6f);
            }
            if (Inp.medkitPressed) UseMedkit();
        }

        public void UseMedkit()
        {
            if (UsingMedkit || Medkits <= 0 || Health >= maxHealth) return;
            StartCoroutine(MedkitRoutine());
        }

        IEnumerator MedkitRoutine()
        {
            UsingMedkit = true;
            Sfx.Play2D(SfxId.Reload, 0.7f, 0.8f);
            yield return new WaitForSeconds(1.1f);
            if (!IsDead)
            {
                Medkits--;
                Heal(60f);
                Sfx.Play2D(SfxId.Pickup, 0.8f, 0.8f);
            }
            UsingMedkit = false;
        }

        void DoStats(float dt)
        {
            if (Time.time - LastDamageTime > shieldRegenDelay && Shield < maxShield)
                Shield = Mathf.Min(maxShield, Shield + shieldRegenRate * dt);
        }

        public void Heal(float amount) { Health = Mathf.Min(maxHealth, Health + amount); }
        public void AddShield(float amount) { Shield = Mathf.Min(maxShield, Shield + amount); }
        public void AddMedkit() { Medkits = Mathf.Min(3, Medkits + 1); }

        public void TakeDamage(float amount, Vector3 hitPoint, bool headshot, GameObject source)
        {
            GameManager gm = GameManager.Instance;
            if (IsDead || gm == null || !gm.Playing || gm.InTutorial || GodMode) return;

            float remaining = amount;
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, remaining);
                Shield -= absorbed;
                remaining -= absorbed;
            }
            Health -= remaining;
            LastDamageTime = Time.time;

            Vector3 from = source != null ? source.transform.position : hitPoint;
            if (gm.Hud != null) gm.Hud.OnPlayerHit(this, from, amount);
            gm.OnPlayerDamaged(this, amount);
            Shake(Mathf.Clamp(amount / 60f, 0.05f, 0.35f));
            Haptics.OnDamaged(this, amount);
            Sfx.Play2D(SfxId.Hurt, 0.9f, Random.Range(0.9f, 1.1f));

            if (Health <= 0f)
            {
                Health = 0f;
                Die();
            }
        }

        void Die()
        {
            IsDead = true;
            StopAllCoroutines();
            UsingMedkit = false;
            if (weapons != null && weapons.holder != null) weapons.holder.gameObject.SetActive(false);
            GameManager.Instance.OnPlayerDied(this);
        }

        /// <summary>Co-op: a fallen player comes back at the end of the wave.</summary>
        public void Revive(Vector3 position)
        {
            if (!IsDead) return;
            Teleport(position);
            IsDead = false;
            Health = maxHealth * 0.5f;
            Shield = maxShield * 0.5f;
            Stamina = maxStamina;
            LastDamageTime = -100f;
            roll = 0f; pitch = 0f;
            if (weapons != null && weapons.holder != null) weapons.holder.gameObject.SetActive(true);
            if (!VrOn && cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        public void Teleport(Vector3 position)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            horiz = Vector3.zero;
            vy = 0f;
        }

        public void ResetPlayer(Vector3 position)
        {
            StopAllCoroutines();
            Teleport(position);
            yaw = 0f; pitch = 0f; roll = 0f; recoil = 0f; shake = 0f;
            transform.rotation = Quaternion.identity;
            slideTimer = 0f; dashTimer = 0f; dashCd = 0f;
            Health = maxHealth; Shield = maxShield; Stamina = maxStamina; Medkits = 1;
            sprintLocked = false; UsingMedkit = false; IsDead = false;
            LastDamageTime = -100f;
            Kills = 0; DistanceMoved = 0f; LookAmount = 0f; Jumps = 0; Slides = 0; Dashes = 0; SprintTime = 0f;
            if (flashlight != null) flashlight.enabled = false;
            if (weapons != null) weapons.ResetAll();
            Util.SetFov(cam, GameSettings.Fov);
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            eyeHeight = 1.62f;
            if (VrOn && cameraPivot != null) { cameraPivot.localPosition = Vector3.zero; cameraPivot.localRotation = Quaternion.identity; }
        }

        // ----------------------------------------------------------------------------------------------
        // camera
        // ----------------------------------------------------------------------------------------------

        void DoCamera(float dt, bool vrOn)
        {
            recoil = Mathf.MoveTowards(recoil, 0f, (3.5f + recoil * 3f) * dt);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);

            if (vrOn)
            {
                FollowHead();
                return;
            }

            float ads = weapons != null ? weapons.AdsAmount : 0f;
            float hFrac = Mathf.InverseLerp(1.1f, 1.8f, cc.height);
            float targetEye = Mathf.Lerp(0.85f, 1.62f, hFrac);
            eyeHeight = Mathf.Lerp(eyeHeight, targetEye, 1f - Mathf.Exp(-14f * dt));

            Vector3 bob = BobOffset * 2.5f * (1f - ads * 0.8f);
            Vector3 shakeOff = shake > 0.001f ? Random.insideUnitSphere * shake * 0.12f : Vector3.zero;
            cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f) + bob + shakeOff;

            float targetRoll = -Inp.move.x * 1.6f + (Sliding ? -5f : 0f);
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-8f * dt));
            float shakeRoll = shake > 0.001f ? Random.Range(-1f, 1f) * shake * 6f : 0f;
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoil + AimOffset.y, AimOffset.x, roll + shakeRoll);

            float baseFov = GameSettings.Fov;
            float fov = baseFov;
            if (Sprinting) fov += 7f;
            if (dashTimer > 0f) fov += 14f;
            if (weapons != null && ads > 0f) fov = Mathf.Lerp(fov, weapons.Current.adsFov, ads);
            if (FovOverride > 0f) fov = FovOverride;
            Util.SetFov(cam, Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-12f * dt)));
        }

        /// <summary>
        /// VR room-scale: when the player physically walks, move the collision capsule under the head (and shift the
        /// tracking origin back), so enemies, bullets and pickups react to where the player really is.
        /// </summary>
        void FollowHead()
        {
            if (cam == null || cameraPivot == null) return;
            Vector3 delta = cam.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f) return;
            Vector3 before = transform.position;
            cc.Move(delta);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            cameraPivot.position -= moved;
        }

        /// <summary>VR: put the current head position back over the body (recenter).</summary>
        public void RecenterVr()
        {
            if (cameraPivot == null || cam == null) return;
            Vector3 headLocal = cameraPivot.localPosition + cameraPivot.localRotation * cam.transform.localPosition;
            cameraPivot.localPosition -= new Vector3(headLocal.x, 0f, headLocal.z);
        }

        void SwitchMode(bool vrOn)
        {
            lastVr = vrOn;
            if (weapons != null) weapons.SetVR(vrOn, vrOn && vr != null ? vr.RightHand : null);
            if (cam != null)
            {
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.nearClipPlane = vrOn ? 0.05f : 0.03f;
            }
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.identity;
                if (vrOn) cameraPivot.localPosition = Vector3.zero;
            }
        }

        void MenuOrbit(float dt, bool vrOn)
        {
            LookDelta = Vector2.zero;
            if (vrOn) { FollowHead(); return; }   // never rotate the world around a VR player
            yaw += 7f * dt;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = Mathf.Lerp(pitch, -6f, dt);
            if (cameraPivot != null)
            {
                cameraPivot.localPosition = new Vector3(0f, 1.62f + Mathf.Sin(Time.time * 0.6f) * 0.04f, 0f);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            if (cam != null) Util.SetFov(cam, Mathf.Lerp(cam.fieldOfView, GameSettings.Fov, dt * 4f));
        }

        void DeathCam(float dt, bool vrOn)
        {
            if (cameraPivot == null || vrOn) return;
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, new Vector3(0f, 0.3f, 0f), dt * 3f);
            roll = Mathf.Lerp(roll, 70f, dt * 2f);
            pitch = Mathf.Lerp(pitch, 25f, dt * 2f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
