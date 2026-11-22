using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// The farmer. First-person (or V / R3 for an over-the-shoulder view) movement and 360° look, reading the project's
    /// device-independent PlayerInputState, filled by DesktopInput (rebindable keys), GamepadInput and FarmVRRig.
    /// Walk / sprint / crouch / jump with a CharacterController, smoothed look, head bob, footsteps by surface, recoil
    /// with recovery, scope sensitivity scaling and ammo-crate interaction. In VR the camera follows the headset,
    /// locomotion follows the head direction and there is no camera recoil or bob (comfort).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FarmPlayer : MonoBehaviour
    {
        [Header("Rig (assigned by the scene builder)")]
        public Camera cam;
        public Transform trackingSpace;
        public Transform head;
        public SniperRifle rifle;
        public FarmerAnimator farmer;
        public FarmVRRig vr;
        public AmmoCrate[] ammoCrates = new AmmoCrate[0];

        [Header("Movement")]
        public float walkSpeed = 3.4f;
        public float sprintSpeed = 6f;
        public float crouchSpeed = 1.6f;
        public float scopedSpeed = 1.4f;
        public float vrSpeedMultiplier = 0.75f;
        public float acceleration = 14f;
        [Range(0f, 1f)] public float airControl = 0.3f;
        public float jumpHeight = 1f;
        public float gravity = -22f;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.15f;
        [Tooltip("Eye height below the top of the capsule.")]
        public float eyeBelowTop = 0.12f;

        [Header("Look")]
        [Tooltip("Degrees per mouse unit (multiplied by Settings → Sensitivity).")]
        public float lookSensitivity = 2f;
        [Tooltip("0 = raw input; higher values smooth the camera.")]
        [Range(0f, 0.2f)] public float lookSmoothing = 0.03f;
        [Range(10f, 89f)] public float pitchLimit = 85f;
        [Tooltip("Desktop field of view (use the Neon Breach Settings value if 'useGlobalFov').")]
        public float baseFov = 70f;
        public bool useGlobalFov = false;
        public float recoilRecovery = 7f;

        [Header("Camera feel")]
        public bool headBob = true;
        public float bobAmount = 0.035f;
        public float bobFrequency = 1.9f;
        public bool allowThirdPerson = true;
        public Vector3 thirdPersonOffset = new Vector3(0.6f, 0.25f, -2.4f);
        public LayerMask cameraCollision = 1 << 10;

        [Header("Footsteps")]
        public float stepDistance = 1.9f;

        // ------------------------------------------------------------------ state

        public PlayerInputState Input { get; private set; }
        public bool InputEnabled
        {
            get { return inputEnabled; }
            set
            {
                // the click / trigger that closed a menu must not also fire the rifle
                if (value && !inputEnabled) fireGraceUntil = Time.unscaledTime + 0.3f;
                inputEnabled = value;
            }
        }
        bool inputEnabled;
        float fireGraceUntil;
        /// <summary>Title screen: the view slowly pans over the farm.</summary>
        public bool AttractMode { get; set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Grounded { get; private set; }
        public float MoveSpeed { get; private set; }
        public Vector3 Velocity { get { return velocity; } }
        public bool ThirdPerson { get; private set; }
        public bool VrActive { get { return vr != null && vr.Active; } }
        public bool VrZoomPressed { get { return vr != null && vr.ZoomPressed; } }
        public Vector3 WeaponBob { get; private set; }
        public string InteractPrompt { get; private set; }
        public float ThirdPersonBlend { get; private set; }

        CharacterController cc;
        Vector3 velocity;
        Vector2 smoothLook;
        Vector2 recoil;            // pitch, yaw offset that recovers
        float bobPhase, stepAcc, height;
        bool lastVr;
        float landTimer;
        Vector3 spawnPos;
        float spawnYaw;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Input = new PlayerInputState();
            height = standHeight;
            spawnPos = transform.position;
            spawnYaw = transform.eulerAngles.y;
            Yaw = spawnYaw;
            if (rifle != null) rifle.owner = this;
        }

        void Start()
        {
            ApplyViewMode(true);
            if (cam != null) cam.fieldOfView = BaseFov;
        }

        float BaseFov { get { return useGlobalFov ? GameSettings.Fov : baseFov; } }

        public void ResetPlayer()
        {
            cc.enabled = false;
            transform.position = spawnPos;
            transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
            cc.enabled = true;
            Yaw = spawnYaw;
            Pitch = 0f;
            velocity = Vector3.zero;
            recoil = Vector2.zero;
            smoothLook = Vector2.zero;
            Crouching = false;
            if (rifle != null) rifle.ResetRifle();
            if (vr != null) vr.Recenter();
        }

        public void AddRecoil(float pitchUp, float yaw)
        {
            if (VrActive) return;
            recoil += new Vector2(pitchUp, yaw);
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool vrOn = VrActive;
            if (vrOn != lastVr) { lastVr = vrOn; ApplyViewMode(false); }

            Input.Clear();
            if (InputEnabled)
            {
                DesktopInput.Read(Input);
                GamepadInput.Read(0, Input);
                if (vrOn) vr.Read(Input, dt);
                if (Time.unscaledTime < fireGraceUntil) { Input.firePressed = false; Input.fireHeld = false; }
            }
            if (AttractMode && !vrOn) Input.look = new Vector2(3.5f * dt / Mathf.Max(0.01f, lookSensitivity * GameSettings.Sensitivity), 0f);

            if (InputEnabled && !vrOn && allowThirdPerson && Input.meleePressed) ThirdPerson = !ThirdPerson;

            Look(dt, vrOn);
            Move(dt, vrOn);
            Interact();

            if (rifle != null) rifle.Tick(Input, InputEnabled, dt);
            if (farmer != null) farmer.Animate(MoveSpeed, Crouching, Pitch, Grounded, dt);
        }

        void Look(float dt, bool vrOn)
        {
            if (vrOn)
            {
                // snap / smooth turn around the head so the view does not swing sideways
                if (Mathf.Abs(Input.snapTurn) > 0.0001f && cam != null)
                {
                    Vector3 pivot = cam.transform.position;
                    transform.RotateAround(new Vector3(pivot.x, transform.position.y, pivot.z), Vector3.up, Input.snapTurn);
                    Yaw = transform.eulerAngles.y;
                }
                Pitch = 0f;
                return;
            }

            float sens = lookSensitivity * GameSettings.Sensitivity * (rifle != null ? rifle.SensitivityScale : 1f);
            Vector2 raw = Input.look * sens;
            if (GameSettings.InvertY) raw.y = -raw.y;
            float k = lookSmoothing <= 0.0001f ? 1f : 1f - Mathf.Exp(-dt / lookSmoothing);
            smoothLook = Vector2.Lerp(smoothLook, raw, k);
            Yaw += smoothLook.x;
            Pitch = Mathf.Clamp(Pitch - smoothLook.y, -pitchLimit, pitchLimit);

            // recoil: kicks up, recovers smoothly
            recoil = Vector2.Lerp(recoil, Vector2.zero, 1f - Mathf.Exp(-recoilRecovery * dt));
            Vector2 sway = rifle != null ? rifle.SwayOffset : Vector2.zero;

            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (head != null)
                head.localRotation = Quaternion.Euler(Pitch - recoil.x + sway.y, recoil.y + sway.x, 0f);

            // field of view: scope magnification
            if (cam != null && rifle != null)
            {
                float zoom = Mathf.Lerp(1f, rifle.CurrentZoom, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, rifle.AdsAmount)));
                float adsFov = BaseFov * Mathf.Lerp(1f, 0.85f, rifle.AdsAmount);
                cam.fieldOfView = adsFov / zoom;
            }
        }

        void Move(float dt, bool vrOn)
        {
            bool wantCrouch = Input.crouchHeld;
            if (!wantCrouch && Crouching)
            {
                // stand up only if there is room
                Vector3 top = transform.position + Vector3.up * (crouchHeight - cc.radius);
                if (Physics.SphereCast(top, cc.radius * 0.9f, Vector3.up, out RaycastHit _, standHeight - crouchHeight, cameraCollision, QueryTriggerInteraction.Ignore)) wantCrouch = true;
            }
            Crouching = wantCrouch && !vrOn;

            bool scoped = rifle != null && rifle.AdsAmount > 0.5f;
            Sprinting = Input.sprintHeld && !Crouching && Input.move.y > 0.1f && !(rifle != null && rifle.AdsAmount > 0.05f) && Grounded;
            float speed = Crouching ? crouchSpeed : (Sprinting ? sprintSpeed : walkSpeed);
            if (scoped) speed = Mathf.Min(speed, scopedSpeed);
            if (vrOn) speed *= vrSpeedMultiplier;

            Vector3 fwd, right;
            if (vrOn && vr != null)
            {
                fwd = vr.HeadForwardFlat(cam);
                right = Vector3.Cross(Vector3.up, fwd);
            }
            else
            {
                fwd = transform.forward;
                right = transform.right;
            }
            Vector3 wish = (fwd * Input.move.y + right * Input.move.x) * speed;
            Vector3 horiz = new Vector3(velocity.x, 0f, velocity.z);
            float accel = Grounded ? acceleration : acceleration * airControl;
            horiz = Vector3.MoveTowards(horiz, wish, accel * speed * dt * 0.5f + accel * dt);

            bool wasGrounded = Grounded;
            Grounded = cc.isGrounded;
            if (Grounded && velocity.y < 0f) velocity.y = -2f;
            if (Grounded && Input.jumpPressed && !Crouching)
            {
                velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                FarmAudio.Play2D(FarmSound.Jump, 0.5f);
            }
            velocity.y += gravity * dt;
            velocity = new Vector3(horiz.x, velocity.y, horiz.z);
            cc.Move(velocity * dt);
            if (!wasGrounded && cc.isGrounded && landTimer <= 0f) { FarmAudio.Play2D(FarmSound.Land, 0.5f); landTimer = 0.3f; }
            landTimer -= dt;

            MoveSpeed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;

            // capsule height + eye height
            float targetH = Crouching ? crouchHeight : standHeight;
            height = Mathf.MoveTowards(height, targetH, dt * 5f);
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);

            // head bob + footsteps
            float bobK = Grounded ? Mathf.Clamp01(MoveSpeed / walkSpeed) : 0f;
            bobPhase += MoveSpeed * dt * bobFrequency;
            Vector3 bob = Vector3.zero;
            if (headBob && !vrOn && !ThirdPerson && !scoped)
                bob = new Vector3(Mathf.Cos(bobPhase * Mathf.PI) * bobAmount * 0.5f, -Mathf.Abs(Mathf.Sin(bobPhase * Mathf.PI)) * bobAmount, 0f) * bobK;
            WeaponBob = new Vector3(bob.x * 0.6f, bob.y * 0.5f, 0f);
            if (head != null && !vrOn) head.localPosition = new Vector3(0f, height - eyeBelowTop, 0f) + bob;

            if (Grounded && MoveSpeed > 0.5f)
            {
                stepAcc += MoveSpeed * dt;
                float dist = stepDistance * (Sprinting ? 1.25f : (Crouching ? 0.7f : 1f));
                if (stepAcc >= dist)
                {
                    stepAcc = 0f;
                    Vector3 p = transform.position;
                    FarmSound s = p.y > FarmLayout.Height(p.x, p.z) + 0.6f ? FarmSound.StepWood
                        : (FarmLayout.RoadDistance(p.x, p.z) < FarmLayout.RoadWidth * 0.6f || FarmLayout.InYard(p.x, p.z) ? FarmSound.StepDirt : FarmSound.StepGrass);
                    FarmAudio.Play2D(s, Crouching ? 0.25f : (Sprinting ? 0.6f : 0.42f), Random.Range(0.85f, 1.15f));
                }
            }
        }

        void Interact()
        {
            InteractPrompt = null;
            if (!InputEnabled || rifle == null) return;
            AmmoCrate near = null;
            for (int i = 0; i < ammoCrates.Length; i++)
                if (ammoCrates[i] != null && ammoCrates[i].InRange(transform.position)) { near = ammoCrates[i]; break; }
            if (near == null) return;
            if (!near.Ready) { InteractPrompt = "AMMO CRATE RESTOCKING  " + Mathf.CeilToInt(near.CooldownLeft) + " s"; return; }
            if (rifle.Reserve >= rifle.maxReserve) { InteractPrompt = "AMMO FULL"; return; }
            InteractPrompt = VrActive ? "PRESS X / Y TO TAKE AMMO" : "PRESS [E] TO TAKE AMMO";
            bool pressed = DesktopInput.KeyPressed(KeyCode.E) || Input.medkitPressed || (vr != null && vr.InteractPressed);
            if (pressed)
            {
                int got = near.Take(rifle);
                if (got > 0)
                {
                    FarmAudio.Play2D(FarmSound.AmmoPickup, 0.9f);
                    if (FarmGameManager.Instance != null) FarmGameManager.Instance.PushFeed("+" + got + " ROUNDS", new Color(0.9f, 0.85f, 0.5f));
                }
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            bool vrOn = VrActive;
            if (vrOn)
            {
                if (head != null) { head.localPosition = Vector3.zero; head.localRotation = Quaternion.identity; }
                vr.TrackPoses(cam);
                return;
            }

            // third-person camera: blends to first person while aiming
            bool scopedOrAiming = rifle != null && rifle.AdsAmount > 0.01f;
            float want = ThirdPerson && !scopedOrAiming ? 1f : 0f;
            ThirdPersonBlend = Mathf.MoveTowards(ThirdPersonBlend, want, Time.deltaTime * 4f);
            Vector3 local = Vector3.zero;
            if (ThirdPersonBlend > 0.001f)
            {
                Vector3 desired = thirdPersonOffset * Mathf.SmoothStep(0f, 1f, ThirdPersonBlend);
                Vector3 world = head.TransformPoint(desired);
                Vector3 from = head.position;
                Vector3 dir = world - from;
                RaycastHit hit;
                if (Physics.SphereCast(from, 0.2f, dir.normalized, out hit, dir.magnitude, cameraCollision, QueryTriggerInteraction.Ignore))
                    world = from + dir.normalized * Mathf.Max(0.3f, hit.distance);
                local = head.InverseTransformPoint(world);
            }
            cam.transform.localPosition = local;
            cam.transform.localRotation = Quaternion.identity;
            if (farmer != null) farmer.SetFirstPerson(ThirdPersonBlend < 0.3f);
        }

        void ApplyViewMode(bool initial)
        {
            bool vrOn = VrActive;
            if (rifle != null)
            {
                Transform parent = vrOn && vr != null && vr.rightHand != null ? vr.rightHand : head;
                if (rifle.transform.parent != parent) rifle.transform.SetParent(parent, false);
            }
            if (vr != null)
            {
                if (vr.rightHand != null) vr.rightHand.gameObject.SetActive(vrOn);
                if (vr.leftHand != null) vr.leftHand.gameObject.SetActive(vrOn);
            }
            if (vrOn) ThirdPerson = false;
            if (farmer != null) farmer.SetFirstPerson(true);
            if (!initial && cam != null) { cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity; }
        }
    }
}
