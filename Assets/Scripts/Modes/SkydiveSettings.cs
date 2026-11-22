using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Which input of the shared <see cref="PlayerInputState"/> a skydive control listens to. Every device fills the
    /// same state (keyboard + mouse through the rebindable KeyBindings, gamepad, VR controllers, hand gestures), so one
    /// choice here works everywhere. Default mapping (desktop / gamepad / VR):
    ///   Jump    = Space / A / A          Fire  = LMB / RT / R-trigger      Aim = RMB / LT
    ///   Sprint  = Shift / L3 / L-stick click                               Crouch = Ctrl or C / B / R-stick back
    ///   Reload  = R / X / R-grip (or open right palm)                      Melee  = V / R3 / L-grip
    ///   Zoom    = VR left trigger
    /// </summary>
    public enum SkydiveInput { None, Jump, Fire, Aim, Sprint, Crouch, Reload, Dash, Melee, Grenade, Zoom }

    /// <summary>
    /// Every tunable number of the skydive (aircraft, free fall, deployment, canopy, landing, camera, controls).
    /// Lives on the <see cref="ParachuteMode"/> component of the GameManager object, so it is edited in the Inspector
    /// and saved with the scene. Changes made in Play mode apply immediately (values are read every frame).
    /// </summary>
    [System.Serializable]
    public class SkydiveSettings
    {
        // ------------------------------------------------------------------------------------------------ aircraft
        [Header("Aircraft")]
        [Tooltip("Altitude (m above sea level) the plane flies at, i.e. the exit altitude.")]
        [Range(800f, 6000f)] public float jumpAltitude = 3000f;
        [Tooltip("Plane ground speed (m/s). The jumper leaves the door with this forward speed.")]
        [Range(20f, 120f)] public float planeSpeed = 55f;
        [Tooltip("Seconds of flight before the plane reaches the jump point (the green-light zone).")]
        [Range(3f, 40f)] public float planeLeadTime = 12f;
        [Tooltip("Half length (m) of the green-light zone around the jump point.")]
        [Range(20f, 500f)] public float greenLightHalfWidth = 140f;
        [Tooltip("Distance (m) past the jump point after which the jumper is pushed out automatically.")]
        [Range(50f, 2000f)] public float autoJumpDistance = 380f;
        [Tooltip("Seconds in the door before JUMP is accepted (so the jump key from the menu doesn't jump instantly).")]
        [Range(0f, 5f)] public float minTimeInPlane = 1.5f;
        [Tooltip("Push away from the door when jumping (m/s).")]
        [Range(0f, 15f)] public float exitPush = 4f;

        // ------------------------------------------------------------------------------------------------ free fall
        [Header("Free fall - gravity and air resistance")]
        [Tooltip("Gravity (m/s²).")]
        [Range(1f, 20f)] public float gravity = 9.81f;
        [Tooltip("Terminal velocity (m/s) in the stable belly-to-earth position (looking ahead).")]
        [Range(20f, 90f)] public float bellyTerminalVelocity = 50f;
        [Tooltip("Terminal velocity (m/s) when leaning into a dive (looking straight down).")]
        [Range(30f, 110f)] public float leanTerminalVelocity = 64f;
        [Tooltip("Terminal velocity (m/s) at full SPEED UP (head-down dive).")]
        [Range(40f, 140f)] public float diveTerminalVelocity = 85f;
        [Tooltip("Terminal velocity (m/s) at full SLOW DOWN (spread eagle, maximum drag).")]
        [Range(15f, 60f)] public float spreadTerminalVelocity = 36f;
        [Tooltip("How quickly the speed control moves between slow / normal / dive (units per second, 0..1 range).")]
        [Range(0.2f, 10f)] public float speedControlResponse = 2.2f;
        [Tooltip("Horizontal air resistance: how fast the horizontal velocity settles on the tracking target (1/s).")]
        [Range(0.1f, 5f)] public float horizontalAirResistance = 0.7f;
        [Tooltip("Fraction of the wind the jumper drifts with in free fall.")]
        [Range(0f, 1f)] public float freeFallWindInfluence = 0.4f;

        [Header("Free fall - movement")]
        [Tooltip("Forward tracking speed (m/s) in the flat position.")]
        [Range(0f, 60f)] public float trackForwardSpeed = 26f;
        [Tooltip("Forward tracking speed (m/s) when leaning into a dive (less lift, steeper path).")]
        [Range(0f, 60f)] public float trackForwardSpeedDiving = 16f;
        [Tooltip("Backward slide speed (m/s).")]
        [Range(0f, 30f)] public float trackBackSpeed = 9f;
        [Tooltip("Sideways slide speed (m/s).")]
        [Range(0f, 30f)] public float trackSideSpeed = 12f;
        [Tooltip("Extra forward speed at full SPEED UP (fraction, 0.3 = +30 %). SLOW DOWN reduces it by the same amount.")]
        [Range(0f, 1f)] public float speedControlTrackEffect = 0.3f;
        [Tooltip("Mouse / right-stick turn sensitivity multiplier in free fall.")]
        [Range(0.1f, 3f)] public float freeFallTurnSensitivity = 1f;
        [Tooltip("Lean range (camera / body pitch, degrees): min = flat, max = head down.")]
        public Vector2 leanPitchRange = new Vector2(10f, 89f);
        [Tooltip("Body roll (degrees) when sliding sideways (visual banking).")]
        [Range(0f, 45f)] public float bankAngle = 14f;

        // ------------------------------------------------------------------------------------------------ deployment
        [Header("Deployment")]
        [Tooltip("Height above ground (m) at which the OPEN PARACHUTE option appears and deployment is allowed.")]
        [Range(100f, 5000f)] public float deployAltitude = 2000f;
        [Tooltip("Height above ground (m) at which the HUD and the audible altimeter warn you to pull now.")]
        [Range(50f, 3000f)] public float warningAltitude = 600f;
        [Tooltip("Open the canopy automatically at the auto-deploy altitude (with a score penalty).")]
        public bool autoDeploy = true;
        [Tooltip("Height above ground (m) of the automatic opening.")]
        [Range(30f, 1500f)] public float autoDeployAltitude = 300f;
        [Tooltip("Score penalty for an automatic opening.")]
        [Range(0, 2000)] public int autoDeployPenalty = 250;
        [Tooltip("Seconds from pulling the ripcord to a fully inflated canopy.")]
        [Range(0.3f, 5f)] public float deployDuration = 1.6f;
        [Tooltip("Descent rate (m/s) the opening shock slows you to.")]
        [Range(2f, 20f)] public float openingDescentRate = 8f;
        [Tooltip("Deceleration (m/s²) of the opening shock.")]
        [Range(5f, 80f)] public float openingDeceleration = 32f;
        [Tooltip("Audible altimeter: beeps at the deploy altitude and continuously below the warning altitude.")]
        public bool audibleAltimeter = true;

        // ------------------------------------------------------------------------------------------------ canopy
        [Header("Canopy flight")]
        [Tooltip("Forward airspeed (m/s) with no input.")]
        [Range(2f, 30f)] public float canopyForwardSpeed = 11f;
        [Tooltip("Extra forward speed (m/s) at full SPEED UP / forward input (front risers).")]
        [Range(0f, 20f)] public float canopySpeedUpForward = 4f;
        [Tooltip("Forward speed reduction (m/s) at full SLOW DOWN / back input (brakes).")]
        [Range(0f, 20f)] public float canopyBrakeForward = 5f;
        [Tooltip("Descent rate (m/s) with no input.")]
        [Range(1f, 15f)] public float canopySinkRate = 5.5f;
        [Tooltip("Extra descent (m/s) at full SPEED UP.")]
        [Range(0f, 10f)] public float canopySpeedUpSink = 2.5f;
        [Tooltip("Descent reduction (m/s) at full SLOW DOWN.")]
        [Range(0f, 5f)] public float canopyBrakeSink = 1.2f;
        [Tooltip("Turn rate (degrees per second) at full steering input.")]
        [Range(5f, 120f)] public float canopyTurnRate = 38f;
        [Tooltip("Extra descent (m/s) while turning hard.")]
        [Range(0f, 8f)] public float canopyTurnSink = 1.6f;
        [Tooltip("How quickly the descent rate follows the inputs (1/s).")]
        [Range(0.2f, 8f)] public float canopyResponse = 2.2f;
        [Tooltip("Fraction of the wind the canopy drifts with.")]
        [Range(0f, 1.5f)] public float canopyWindInfluence = 1f;

        [Header("Canopy flare")]
        [Range(0f, 15f)] public float flareForwardSpeed = 4f;
        [Range(0f, 10f)] public float flareSinkRate = 0.8f;
        [Tooltip("Flare energy used per second (1 = full).")]
        [Range(0.05f, 3f)] public float flareDrain = 0.42f;
        [Tooltip("Flare energy recovered per second when not flaring.")]
        [Range(0f, 2f)] public float flareRecharge = 0.15f;
        [Tooltip("Height above ground (m) at which the FLARE! prompt appears.")]
        [Range(2f, 80f)] public float flarePromptAltitude = 18f;

        // ------------------------------------------------------------------------------------------------ landing
        [Header("Landing")]
        [Tooltip("Highest vertical touchdown speed (m/s) that still counts as a safe landing.")]
        [Range(2f, 30f)] public float safeVerticalSpeed = 7.5f;
        [Tooltip("Highest horizontal touchdown speed (m/s) that still counts as a safe landing.")]
        [Range(2f, 40f)] public float safeHorizontalSpeed = 14f;
        [Tooltip("Fraction of the horizontal speed carried into the run-out after a safe landing.")]
        [Range(0f, 1f)] public float landingRunOut = 0.55f;
        [Tooltip("How fast the run-out stops (1/s).")]
        [Range(0.5f, 10f)] public float landingRunOutDamping = 2.2f;
        [Tooltip("Seconds between touchdown and the results screen.")]
        [Range(1f, 15f)] public float landingSequenceDuration = 3.5f;

        // ------------------------------------------------------------------------------------------------ camera
        [Header("Camera (desktop; VR is always first person)")]
        [Tooltip("Start in third person (chase camera behind the jumper) instead of first person.")]
        public bool thirdPersonByDefault = true;
        [Tooltip("Chase distance (m) behind the jumper in the plane door.")]
        [Range(1f, 15f)] public float planeCameraDistance = 3f;
        [Tooltip("Chase distance (m) in free fall.")]
        [Range(1f, 20f)] public float freeFallCameraDistance = 5f;
        [Tooltip("Chase distance (m) under canopy (far enough to see the canopy).")]
        [Range(2f, 30f)] public float canopyCameraDistance = 9f;
        [Tooltip("Chase distance (m) after landing.")]
        [Range(1f, 20f)] public float landedCameraDistance = 6f;
        [Tooltip("How quickly the camera moves between phases / views (1/s). Lower = smoother, slower transitions.")]
        [Range(0.5f, 15f)] public float cameraSmoothing = 3.5f;
        [Tooltip("Extra field of view (degrees) at high speed in free fall.")]
        [Range(0f, 30f)] public float speedFovBoost = 10f;

        // ------------------------------------------------------------------------------------------------ controls
        [Header("Controls (see SkydiveInput for what each input is on every device)")]
        public SkydiveInput jumpInput = SkydiveInput.Jump;
        [Tooltip("Opens the parachute inside the deploy window.")]
        public SkydiveInput deployInput = SkydiveInput.Jump;
        [Tooltip("Second way to open the parachute - VR: squeeze the right grip ('pull the ripcord').")]
        public SkydiveInput deployAltInput = SkydiveInput.Reload;
        [Tooltip("Clicking / pulling the trigger also opens the parachute inside the deploy window.")]
        public SkydiveInput deployClickInput = SkydiveInput.Fire;
        [Tooltip("Hold to fall / fly faster (free fall: head-down dive, canopy: front risers).")]
        public SkydiveInput speedUpInput = SkydiveInput.Sprint;
        [Tooltip("Hold to fall / fly slower (free fall: spread eagle, canopy: brakes).")]
        public SkydiveInput slowDownInput = SkydiveInput.Crouch;
        [Tooltip("Second slow-down input (VR left trigger by default).")]
        public SkydiveInput slowDownAltInput = SkydiveInput.Zoom;
        [Tooltip("Hold to flare the canopy before touchdown.")]
        public SkydiveInput flareInput = SkydiveInput.Fire;
        [Tooltip("Switch between first and third person (desktop / gamepad).")]
        public SkydiveInput cameraToggleInput = SkydiveInput.Melee;

        /// <summary>Keep the altitude thresholds in a sensible order (called from OnValidate).</summary>
        public void Validate()
        {
            deployAltitude = Mathf.Clamp(deployAltitude, 100f, jumpAltitude);
            warningAltitude = Mathf.Clamp(warningAltitude, 50f, deployAltitude);
            autoDeployAltitude = Mathf.Clamp(autoDeployAltitude, 30f, warningAltitude);
            leanPitchRange.x = Mathf.Clamp(leanPitchRange.x, -20f, 80f);
            leanPitchRange.y = Mathf.Clamp(leanPitchRange.y, leanPitchRange.x + 5f, 89f);
            spreadTerminalVelocity = Mathf.Min(spreadTerminalVelocity, bellyTerminalVelocity);
            leanTerminalVelocity = Mathf.Max(leanTerminalVelocity, bellyTerminalVelocity);
            diveTerminalVelocity = Mathf.Max(diveTerminalVelocity, leanTerminalVelocity);
        }

        // ------------------------------------------------------------------------------------------------ input helpers

        /// <summary>True while the input is held (one-shot inputs such as Jump report their press frame).</summary>
        public static bool Held(PlayerInputState s, SkydiveInput a)
        {
            switch (a)
            {
                case SkydiveInput.Jump: return s.jumpPressed;
                case SkydiveInput.Fire: return s.fireHeld;
                case SkydiveInput.Aim: return s.aimHeld;
                case SkydiveInput.Sprint: return s.sprintHeld;
                case SkydiveInput.Crouch: return s.crouchHeld;
                case SkydiveInput.Reload: return s.reloadPressed;
                case SkydiveInput.Dash: return s.dashPressed;
                case SkydiveInput.Melee: return s.meleePressed;
                case SkydiveInput.Grenade: return s.grenadePressed;
                case SkydiveInput.Zoom: return s.zoomHeld;
                default: return false;
            }
        }

        /// <summary>True on the frame the input was pressed (for held inputs: Fire uses firePressed, others are held).</summary>
        public static bool Pressed(PlayerInputState s, SkydiveInput a)
        {
            if (a == SkydiveInput.Fire) return s.firePressed;
            return Held(s, a);
        }

        /// <summary>Key / button name for the HUD hints.</summary>
        public static string KeyName(SkydiveInput a, bool vr)
        {
            if (vr)
            {
                switch (a)
                {
                    case SkydiveInput.Jump: return "A";
                    case SkydiveInput.Fire: return Loc.T("R-TRIGGER");
                    case SkydiveInput.Sprint: return Loc.T("L-STICK CLICK");
                    case SkydiveInput.Crouch: return Loc.T("R-STICK BACK");
                    case SkydiveInput.Reload: return Loc.T("R-GRIP");
                    case SkydiveInput.Melee: return Loc.T("L-GRIP");
                    case SkydiveInput.Zoom: return Loc.T("L-TRIGGER");
                    case SkydiveInput.Grenade: return Loc.T("L-TRIGGER");
                    case SkydiveInput.Dash: return Loc.T("R-STICK CLICK");
                    default: return "-";
                }
            }
            switch (a)
            {
                case SkydiveInput.Jump: return KeyBindings.KeyFor(GameAction.Jump);
                case SkydiveInput.Fire: return KeyBindings.KeyFor(GameAction.Fire);
                case SkydiveInput.Aim: return KeyBindings.KeyFor(GameAction.Aim);
                case SkydiveInput.Sprint: return KeyBindings.KeyFor(GameAction.Sprint);
                case SkydiveInput.Crouch: return KeyBindings.KeyFor(GameAction.Crouch);
                case SkydiveInput.Reload: return KeyBindings.KeyFor(GameAction.Reload);
                case SkydiveInput.Dash: return KeyBindings.KeyFor(GameAction.Dash);
                case SkydiveInput.Melee: return KeyBindings.KeyFor(GameAction.Melee);
                case SkydiveInput.Grenade: return KeyBindings.KeyFor(GameAction.Grenade);
                default: return "-";
            }
        }
    }
}
