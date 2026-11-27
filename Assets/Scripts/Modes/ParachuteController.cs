using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Flies the player in Parachute mode. Plugs into PlayerController as an <see cref="IPlayerDriver"/>, so it reads
    /// the same PlayerInputState as every other mode (keyboard+mouse, gamepad, VR) and moves the existing
    /// CharacterController (its collision flags give the touchdown). Every number comes from
    /// <see cref="SkydiveSettings"/> on the ParachuteMode component, so it is tuned in the Inspector.
    ///
    /// Phases: Plane (stand in the door, JUMP) -> FreeFall (gravity + quadratic air drag; WASD tracks, the mouse turns
    /// and leans, SPEED UP / SLOW DOWN change the body position between a head-down dive and a spread eagle) ->
    /// Deploying (OPEN PARACHUTE inside the deploy window, automatic at the auto-deploy altitude with a penalty) ->
    /// Canopy (A/D steer, W / SPEED UP faster, S / SLOW DOWN brakes, hold FLARE near the ground) -> Landed (knee bend,
    /// run-out, canopy collapses behind you).
    /// Desktop: a smooth third-person chase camera (toggle with V / R3) or the first-person view; a procedural
    /// <see cref="SkydiverRig"/> animates the body. VR: always first person - the camera is never rotated on its own,
    /// a comfort vignette follows the motion and a head-locked prompt shows OPEN PARACHUTE.
    /// </summary>
    public class ParachuteController : MonoBehaviour, IPlayerDriver
    {
        public enum Phase { Plane, FreeFall, Deploying, Canopy, Landed }

        public Phase State { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Agl { get; private set; }
        public float Heading { get; private set; }
        public float FlareEnergy { get; private set; }
        public bool Flaring { get; private set; }
        public float DeployProgress { get; private set; }
        public float Lean { get; private set; }
        /// <summary>-1 = slowest (spread eagle / brakes) .. 0 = normal .. +1 = fastest (head-down dive / front risers).</summary>
        public float SpeedControl { get; private set; }
        /// <summary>Terminal velocity (m/s) of the current free-fall body position.</summary>
        public float TerminalVelocity { get; private set; }
        /// <summary>0..1 knee bend of the landing (animation + first-person camera dip).</summary>
        public float LandCompress { get; private set; }
        public bool ThirdPerson { get; private set; }
        public string BodyPosition { get; private set; }
        public bool Airborne { get { return State == Phase.FreeFall || State == Phase.Deploying || State == Phase.Canopy; } }
        public bool CanopyOpen { get { return State == Phase.Canopy; } }
        /// <summary>Inside the deploy window: the OPEN PARACHUTE option is shown and accepted.</summary>
        public bool CanDeploy { get { return State == Phase.FreeFall && mode != null && Agl <= S.deployAltitude; } }
        public Vector3 HeadingForward { get { return Quaternion.Euler(0f, Heading, 0f) * Vector3.forward; } }

        ParachuteMode mode;
        PlayerController p;
        CharacterController cc;
        Transform canopy;
        LineRenderer[] lines;
        SkydiverRig rig;
        GameObject oldBody;
        TextMesh vrPrompt;
        int originalMask;
        float phaseTime;
        float lookYaw, lookPitch;     // desktop look relative to the body
        float ffPitch = 45f;          // desktop free-fall camera pitch (also the lean)
        float roll;
        float vrYaw;                  // VR: accumulated stick turning while in the plane
        Vector3 hv;                   // horizontal velocity
        float vy;
        Collider lastHit;
        float tooHighToast;
        float comfort;
        Vector3 prevHv;
        float prevHeading;
        bool deployRequested;
        bool flareLatched;            // the flare input was still held from the deploy click: ignore until released
        Vector2 lastMove;
        Vector3 landRun;
        float camBlend, camDist = 3f, camHeight = 1.5f, camShake;
        Vector3 tpOffset;
        Quaternion tpRotSmooth = Quaternion.identity;
        bool tpInit;

        SkydiveSettings S { get { return mode.Settings; } }

        public void Init(ParachuteMode m, PlayerController pc)
        {
            mode = m;
            p = pc;
            cc = pc.Body;
            State = Phase.Plane;
            FlareEnergy = 1f;
            lookYaw = 0f;
            lookPitch = 12f;
            BodyPosition = "";
            ThirdPerson = S.thirdPersonByDefault;
            camBlend = ThirdPerson && !pc.VrOn ? 1f : 0f;
            camDist = S.planeCameraDistance;
            if (pc.cam != null) originalMask = pc.cam.cullingMask;
            BuildCanopy();
            // the skydiver body replaces the shooter's simple body while the mode runs
            Transform b = transform.Find("Body");
            if (b != null) { oldBody = b.gameObject; oldBody.SetActive(false); }
            rig = SkydiverRig.Build(transform, Layers.PlayerBody(pc.index), pc.index == 0 ? "NeonCyan" : "NeonGreen");
        }

        public void Shutdown()
        {
            if (canopy != null) Destroy(canopy.gameObject);
            if (rig != null) Destroy(rig.gameObject);
            if (vrPrompt != null) Destroy(vrPrompt.gameObject);
            if (oldBody != null) oldBody.SetActive(true);
            if (p != null && p.cam != null)
            {
                Util.SetFov(p.cam, GameSettings.Fov);
                p.cam.cullingMask = originalMask;
                if (!p.VrOn && p.cameraPivot != null)
                {
                    p.cameraPivot.localPosition = new Vector3(0f, 1.62f, 0f);
                    p.cameraPivot.localRotation = Quaternion.identity;
                }
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit) { lastHit = hit.collider; }

        /// <summary>Knock the player sideways (obstacle hit).</summary>
        public void Bump(Vector3 dir)
        {
            dir.y = 0f;
            hv += dir.normalized * 8f;
            if (State == Phase.FreeFall) vy *= 0.7f;
            camShake = Mathf.Max(camShake, 0.3f);
        }

        /// <summary>The HUD's OPEN PARACHUTE button (mouse / touch). Ignored outside the deploy window.</summary>
        public void RequestDeploy() { deployRequested = true; }

        // ===================================================================================== driver

        public bool DrivePlayer(PlayerController pc, float dt)
        {
            if (mode == null || cc == null) return false;
            PlayerInputState inp = pc.Inp;
            bool vr = pc.VrOn;
            float sens = GameSettings.Sensitivity * 2.2f;
            float inv = GameSettings.InvertY ? -1f : 1f;
            phaseTime += dt;
            lastMove = inp.move;
            UpdateAgl();

            if (!vr && SkydiveSettings.Pressed(inp, S.cameraToggleInput))
            {
                ThirdPerson = !ThirdPerson;
                Sfx.Play2D(SfxId.Click, 0.5f);
            }
            UpdateSpeedControl(inp, dt);

            switch (State)
            {
                case Phase.Plane: PlanePhase(inp, dt, vr, sens, inv); break;
                case Phase.FreeFall: FreeFall(inp, dt, vr, sens, inv); break;
                case Phase.Deploying: Deploying(inp, dt, vr); break;
                case Phase.Canopy: CanopyFlight(inp, dt, vr, sens, inv); break;
                default: LandedPhase(inp, dt, vr, sens, inv); break;
            }
            deployRequested = false;

            if (Airborne) MoveAndCollide(dt);
            else if (State == Phase.Landed) RunOut(dt);
            UpdateCamera(dt, vr);
            UpdateRig(dt);
            UpdateComfort(dt, vr);
            UpdateVrPrompt(vr);
            return true;
        }

        void UpdateAgl()
        {
            Vector3 pos = transform.position;
            RaycastHit hit;
            float ground = ParachuteMode.WaterLevel;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 8000f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                ground = Mathf.Max(ground, hit.point.y);
            Agl = Mathf.Max(0f, pos.y - ground);
        }

        /// <summary>SPEED UP / SLOW DOWN, eased so the body position (and the drag) changes smoothly.</summary>
        void UpdateSpeedControl(PlayerInputState inp, float dt)
        {
            float target = 0f;
            if (SkydiveSettings.Held(inp, S.speedUpInput)) target += 1f;
            if (SkydiveSettings.Held(inp, S.slowDownInput) || SkydiveSettings.Held(inp, S.slowDownAltInput)) target -= 1f;
            SpeedControl = Mathf.MoveTowards(SpeedControl, Mathf.Clamp(target, -1f, 1f), S.speedControlResponse * dt);
        }

        // ----------------------------------------------------------------------------------- plane

        void PlanePhase(PlayerInputState inp, float dt, bool vr, float sens, float inv)
        {
            mode.StepPlane(dt);
            p.Teleport(mode.DoorPoint());
            Velocity = mode.PlaneVelocity;
            float outYaw = mode.PlaneYaw + 90f;
            if (vr)
            {
                vrYaw += inp.snapTurn;
                transform.rotation = Quaternion.Euler(0f, outYaw + vrYaw, 0f);
            }
            else
            {
                lookYaw = Mathf.Clamp(lookYaw + inp.look.x * sens, -110f, 110f);
                lookPitch = Mathf.Clamp(lookPitch - inp.look.y * sens * inv, -60f, 85f);
                transform.rotation = Quaternion.Euler(0f, outYaw + lookYaw, 0f);
            }
            BodyPosition = mode.GreenLight ? Loc.T("READY") : Loc.T("IN THE DOOR");

            bool canJump = phaseTime > S.minTimeInPlane;
            if ((canJump && SkydiveSettings.Pressed(inp, S.jumpInput)) || mode.PastJumpPoint) Jump(vr);
        }

        void Jump(bool vr)
        {
            State = Phase.FreeFall;
            phaseTime = 0f;
            Vector3 outward = mode.PlaneTransform != null ? mode.PlaneTransform.right : transform.forward;
            hv = mode.PlaneVelocity + outward * S.exitPush;
            vy = 0f;
            Heading = vr && p.vr != null ? Quaternion.LookRotation(p.vr.HeadForwardFlat).eulerAngles.y : transform.eulerAngles.y;
            ffPitch = Mathf.Clamp(lookPitch + 25f, S.leanPitchRange.x + 20f, 70f);
            prevHeading = Heading;
            prevHv = hv;
            mode.OnJumped();
        }

        // ----------------------------------------------------------------------------------- free fall

        void FreeFall(PlayerInputState inp, float dt, bool vr, float sens, float inv)
        {
            Vector3 fwd;
            float sideTilt = 0f;
            float minPitch = S.leanPitchRange.x, maxPitch = S.leanPitchRange.y;
            if (vr)
            {
                Heading += inp.snapTurn;
                // lean = how far you look down; head roll drifts you sideways
                Vector3 hf = p.cam.transform.forward;
                ffPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(-hf.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
                sideTilt = HeadRollInput();
                fwd = p.vr != null ? p.vr.HeadForwardFlat : HeadingForward;
            }
            else
            {
                Heading += inp.look.x * sens * S.freeFallTurnSensitivity;
                ffPitch = Mathf.Clamp(ffPitch - inp.look.y * sens * inv, minPitch, maxPitch);
                fwd = HeadingForward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Lean = Mathf.InverseLerp(minPitch, maxPitch, ffPitch);           // 0 = flat / arched, 1 = head-down

            // --- vertical: gravity against quadratic air drag. The drag coefficient is chosen so that drag equals
            // gravity at the terminal velocity of the current body position, so changing position (lean, SPEED UP,
            // SLOW DOWN) accelerates or brakes you smoothly towards the new terminal velocity.
            float vtBase = Mathf.Lerp(S.bellyTerminalVelocity, S.leanTerminalVelocity, Lean);
            float vt = SpeedControl >= 0f ? Mathf.Lerp(vtBase, S.diveTerminalVelocity, SpeedControl)
                                          : Mathf.Lerp(vtBase, S.spreadTerminalVelocity, -SpeedControl);
            TerminalVelocity = vt;
            float drag = S.gravity / (vt * vt);
            float before = vy;
            vy += (-S.gravity + drag * vy * vy * (vy < 0f ? 1f : -1f)) * dt;
            if (before < -vt && vy > -vt) vy = -vt;                          // braking settles on, not past, vt

            // --- horizontal: tracking moves you across the ground; air resistance pulls you to the target speed
            float mv = inp.move.y;
            float trackMul = Mathf.Max(0.2f, 1f + S.speedControlTrackEffect * SpeedControl);
            float trackFwd = mv > 0f ? mv * Mathf.Lerp(S.trackForwardSpeed, S.trackForwardSpeedDiving, Lean) * trackMul : mv * S.trackBackSpeed;
            float trackSide = Mathf.Clamp(inp.move.x + sideTilt, -1f, 1f) * S.trackSideSpeed;
            Vector3 wind = WindSystem.Instance != null ? WindSystem.Instance.At(transform.position.y) : Vector3.zero;
            Vector3 wish = fwd * trackFwd + right * trackSide + wind * S.freeFallWindInfluence;
            hv = Vector3.Lerp(hv, wish, 1f - Mathf.Exp(-S.horizontalAirResistance * dt));

            roll = Mathf.Lerp(roll, -inp.move.x * S.bankAngle, 1f - Mathf.Exp(-5f * dt));

            if (SpeedControl > 0.5f) BodyPosition = Loc.T("HEAD-DOWN DIVE");
            else if (SpeedControl < -0.5f) BodyPosition = Loc.T("SPREAD EAGLE");
            else if (mv > 0.3f) BodyPosition = Loc.T("TRACKING");
            else if (mv < -0.3f) BodyPosition = Loc.T("BACK SLIDE");
            else BodyPosition = Lean > 0.6f ? Loc.T("DIVING") : Loc.T("BELLY TO EARTH");

            // --- OPEN PARACHUTE: deploy key, ripcord (VR grip), a click / trigger, or the HUD button
            bool keyPull = SkydiveSettings.Pressed(inp, S.deployInput) || SkydiveSettings.Pressed(inp, S.deployAltInput);
            bool clickPull = SkydiveSettings.Pressed(inp, S.deployClickInput) || deployRequested;
            if (keyPull || clickPull)
            {
                if (CanDeploy) Deploy(false, clickPull);
                else if (keyPull && Time.time > tooHighToast)
                {
                    tooHighToast = Time.time + 2f;
                    GameManager gm = GameManager.Instance;
                    if (gm.Hud != null) gm.Hud.Toast(Loc.T("Too high - deploy below") + " " + Mathf.RoundToInt(S.deployAltitude) + " m");
                    Sfx.Play2D(SfxId.Deny, 0.6f);
                }
            }
            if (State == Phase.FreeFall && S.autoDeploy && Agl <= S.autoDeployAltitude) Deploy(true, false);
        }

        void Deploy(bool automatic, bool byClick)
        {
            State = Phase.Deploying;
            phaseTime = 0f;
            DeployProgress = 0f;
            flareLatched = byClick || S.flareInput == S.deployInput;
            if (canopy != null)
            {
                canopy.gameObject.SetActive(true);
                canopy.localScale = Vector3.one * 0.05f;
                canopy.localPosition = new Vector3(0f, 1.2f, 0f);
                canopy.localRotation = Quaternion.identity;
            }
            camShake = 0.35f;
            if (p.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.LeftHand, 0.7f, 0.2f);
            if (p.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.RightHand, 0.7f, 0.2f);
            mode.OnDeployed(automatic);
        }

        void Deploying(PlayerInputState inp, float dt, bool vr)
        {
            DeployProgress = Mathf.Clamp01(phaseTime / S.deployDuration);
            float k = Mathf.SmoothStep(0f, 1f, DeployProgress);
            // the canopy snatches open with a little overshoot
            if (canopy != null) canopy.localScale = Vector3.one * (Mathf.Lerp(0.05f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.08f);
            vy = Mathf.MoveTowards(vy, -S.openingDescentRate, S.openingDeceleration * dt);   // the opening shock
            Vector3 wind = WindSystem.Instance != null ? WindSystem.Instance.At(transform.position.y) : Vector3.zero;
            hv = Vector3.Lerp(hv, HeadingForward * S.canopyForwardSpeed * 0.8f + wind * S.canopyWindInfluence, 1f - Mathf.Exp(-2f * dt));
            if (vr) Heading += inp.snapTurn;
            if (!vr) ffPitch = Mathf.Lerp(ffPitch, 8f, 1f - Mathf.Exp(-3f * dt));
            roll = Mathf.Lerp(roll, 0f, 1f - Mathf.Exp(-4f * dt));
            BodyPosition = Loc.T("OPENING");
            if (DeployProgress >= 1f)
            {
                State = Phase.Canopy;
                phaseTime = 0f;
                lookYaw = 0f;
                lookPitch = ffPitch;
                FlareEnergy = 1f;
                if (canopy != null) canopy.localScale = Vector3.one;
            }
        }

        // ----------------------------------------------------------------------------------- canopy

        void CanopyFlight(PlayerInputState inp, float dt, bool vr, float sens, float inv)
        {
            float turn = inp.move.x;
            if (vr) turn = Mathf.Clamp(turn + HeadRollInput(), -1f, 1f);
            // W / SPEED UP = front risers (faster, steeper), S / SLOW DOWN = brakes (slower, flatter)
            float accel = Mathf.Clamp01(Mathf.Max(0f, inp.move.y) + Mathf.Max(0f, SpeedControl));
            float brake = Mathf.Clamp01(Mathf.Max(0f, -inp.move.y) + Mathf.Max(0f, -SpeedControl));

            bool flareHeld = SkydiveSettings.Held(inp, S.flareInput);
            if (!flareHeld) flareLatched = false;
            if (flareLatched) flareHeld = false;
            Flaring = flareHeld && FlareEnergy > 0f;
            if (Flaring) FlareEnergy = Mathf.Max(0f, FlareEnergy - S.flareDrain * dt);
            else if (!flareHeld) FlareEnergy = Mathf.Min(1f, FlareEnergy + S.flareRecharge * dt);

            float fwdSpeed = S.canopyForwardSpeed + accel * S.canopySpeedUpForward - brake * S.canopyBrakeForward;
            float sink = S.canopySinkRate + accel * S.canopySpeedUpSink - brake * S.canopyBrakeSink + Mathf.Abs(turn) * S.canopyTurnSink;
            float response = S.canopyResponse;
            if (Flaring) { fwdSpeed = S.flareForwardSpeed; sink = S.flareSinkRate; response = S.canopyResponse * 1.6f; }
            else if (flareHeld) { fwdSpeed = S.flareForwardSpeed * 0.75f; sink = S.canopySinkRate * 1.2f; }   // flare held too long: stall
            fwdSpeed = Mathf.Max(0.5f, fwdSpeed);
            sink = Mathf.Max(0.3f, sink);

            Heading += turn * S.canopyTurnRate * dt + (vr ? inp.snapTurn : 0f);
            Vector3 wind = WindSystem.Instance != null ? WindSystem.Instance.At(transform.position.y) : Vector3.zero;
            vy = Mathf.Lerp(vy, -sink, 1f - Mathf.Exp(-response * dt));
            hv = Vector3.Lerp(hv, HeadingForward * fwdSpeed + wind * S.canopyWindInfluence, 1f - Mathf.Exp(-1.6f * dt));

            if (!vr)
            {
                lookYaw = Mathf.Clamp(lookYaw + inp.look.x * sens, -120f, 120f);
                lookPitch = Mathf.Clamp(lookPitch - inp.look.y * sens * inv, -70f, 80f);
            }
            roll = Mathf.Lerp(roll, -turn * 10f, 1f - Mathf.Exp(-4f * dt));
            if (canopy != null)
            {
                float sway = Mathf.Sin(Time.time * 1.3f) * 1.5f;
                canopy.localRotation = Quaternion.Euler((Flaring ? -14f : brake * -6f) + accel * 8f, 0f, -turn * 16f + sway);
            }

            if (Flaring) BodyPosition = Loc.T("FLARE");
            else if (brake > 0.3f) BodyPosition = Loc.T("BRAKES");
            else if (accel > 0.3f) BodyPosition = Loc.T("FRONT RISERS");
            else BodyPosition = Mathf.Abs(turn) > 0.3f ? Loc.T("TURNING") : Loc.T("FULL FLIGHT");
        }

        void LandedPhase(PlayerInputState inp, float dt, bool vr, float sens, float inv)
        {
            if (!vr)
            {
                lookYaw = Mathf.Clamp(lookYaw + inp.look.x * sens, -170f, 170f);
                lookPitch = Mathf.Clamp(lookPitch - inp.look.y * sens * inv, -70f, 80f);
            }
            // knee bend: in fast, out slowly
            LandCompress = Mathf.MoveTowards(LandCompress, 0f, dt * (phaseTime < 0.6f ? 0.4f : 1.1f));
            if (canopy != null && canopy.gameObject.activeSelf)
            {
                // the canopy loses pressure, keeps flying forward a moment and settles on the ground in front of you
                // (in front, so the chase camera behind the jumper never ends up inside the fabric)
                float a = 1f - Mathf.Exp(-1.5f * dt);
                canopy.localPosition = Vector3.Lerp(canopy.localPosition, new Vector3(0f, -1.0f, 1.5f), a);
                canopy.localRotation = Quaternion.Slerp(canopy.localRotation, Quaternion.Euler(80f, 0f, 0f), a);
                canopy.localScale = Vector3.Lerp(canopy.localScale, new Vector3(1f, 0.35f, 0.85f), a);
            }
            BodyPosition = mode.Crashed ? Loc.T("DOWN") : (landRun.sqrMagnitude > 0.5f ? Loc.T("RUN-OUT") : Loc.T("STANDING"));
        }

        /// <summary>After a safe landing the jumper runs out the remaining forward speed instead of stopping dead.</summary>
        void RunOut(float dt)
        {
            Velocity = landRun;
            if (landRun.sqrMagnitude < 0.0025f) { landRun = Vector3.zero; return; }
            cc.Move((landRun + Vector3.down * 4f) * dt);
            landRun = Vector3.Lerp(landRun, Vector3.zero, 1f - Mathf.Exp(-S.landingRunOutDamping * dt));
        }

        /// <summary>VR head tilt (roll) as a -1..1 steering input with a dead zone.</summary>
        float HeadRollInput()
        {
            if (p.cam == null) return 0f;
            Quaternion local = Quaternion.Inverse(transform.rotation) * p.cam.transform.rotation;
            float r = local.eulerAngles.z;
            if (r > 180f) r -= 360f;
            if (Mathf.Abs(r) < 8f) return 0f;
            return Mathf.Clamp(-(r - Mathf.Sign(r) * 8f) / 22f, -1f, 1f);   // tilt left (positive roll) = steer left
        }

        // ===================================================================================== movement + landing

        void MoveAndCollide(float dt)
        {
            Vector3 v = hv + Vector3.up * vy;
            Velocity = v;
            Vector3 a = transform.position + Vector3.up;
            lastHit = null;
            CollisionFlags f = cc.Move(v * dt);
            Vector3 b = transform.position + Vector3.up;
            mode.CheckPath(a, b);

            bool water = transform.position.y <= ParachuteMode.WaterLevel + 0.1f;
            bool ground = (f & CollisionFlags.Below) != 0;
            bool tree = lastHit != null && mode.IsTree(lastHit) && (f & (CollisionFlags.Sides | CollisionFlags.Below)) != 0;
            bool wall = (f & CollisionFlags.Sides) != 0 && v.magnitude > 16f;
            if (water || ground || tree || wall) Touchdown(v, water);
        }

        void Touchdown(Vector3 impact, bool water)
        {
            bool open = State == Phase.Canopy;
            State = Phase.Landed;
            phaseTime = 0f;
            hv = Vector3.zero;
            vy = 0f;
            Velocity = Vector3.zero;
            Flaring = false;
            landRun = Vector3.zero;
            if (water)
            {
                Vector3 pos = transform.position;
                pos.y = ParachuteMode.WaterLevel - 1.1f;   // bobbing in the sea
                p.Teleport(pos);
            }
            lookPitch = Mathf.Clamp(lookPitch, -10f, 40f);
            mode.OnLanded(transform.position, impact, lastHit, water, open);
            if (!open && canopy != null) canopy.gameObject.SetActive(false);

            float vert = Mathf.Max(0f, -impact.y);
            if (!mode.Crashed)
            {
                // safe landing: knee bend scaled by the vertical speed, then run out the horizontal speed
                LandCompress = Mathf.Clamp01(0.35f + vert / S.safeVerticalSpeed * 0.65f);
                Vector3 flat = new Vector3(impact.x, 0f, impact.z);
                landRun = flat * S.landingRunOut;
                if (open) Sfx.Play2D(SfxId.CanopyRustle, 0.7f);
            }
            else camShake = 0.6f;
            if (p.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.LeftHand, mode.Crashed ? 1f : 0.5f, 0.15f);
            if (p.VrOn) Haptics.PulseXR(UnityEngine.XR.XRNode.RightHand, mode.Crashed ? 1f : 0.5f, 0.15f);
        }

        // ===================================================================================== camera + comfort

        void UpdateCamera(float dt, bool vr)
        {
            Camera cam = p.cam;
            if (cam == null || p.cameraPivot == null) return;

            // keep the depth buffer precise: a larger near plane far above the island
            float nearFp = State == Phase.FreeFall ? Mathf.Clamp(Agl * 0.003f, 0.1f, 4f) : (vr ? 0.05f : 0.08f);

            if (vr)
            {
                cam.nearClipPlane = nearFp;
                // never rotate a VR camera on its own: only the body yaw (stick turning) moves the view
                if (State != Phase.Plane) transform.rotation = Quaternion.Euler(0f, Heading, 0f);
                return;
            }

            // --- the view direction (shared by the first- and third-person views, so toggling never jumps)
            float fov = GameSettings.Fov;
            float vPitch, vYaw = 0f, vRoll = 0f;
            float wantDist, wantHeight;
            float deployK = State == Phase.Deploying ? Mathf.SmoothStep(0f, 1f, DeployProgress) : 0f;
            switch (State)
            {
                case Phase.Plane:
                    vPitch = lookPitch;
                    wantDist = S.planeCameraDistance; wantHeight = 1.55f;
                    break;
                case Phase.FreeFall:
                case Phase.Deploying:
                {
                    transform.rotation = Quaternion.Euler(0f, Heading, 0f);
                    vPitch = ffPitch; vRoll = roll;
                    fov += Mathf.Clamp01(Velocity.magnitude / 70f) * S.speedFovBoost;
                    // the opening pulls the camera back and up so the canopy blossoms into view
                    wantDist = Mathf.Lerp(S.freeFallCameraDistance, S.canopyCameraDistance, deployK);
                    wantHeight = Mathf.Lerp(1.0f, 2.8f, deployK);
                    break;
                }
                case Phase.Canopy:
                    transform.rotation = Quaternion.Euler(0f, Heading, 0f);
                    vPitch = lookPitch; vYaw = lookYaw; vRoll = roll;
                    wantDist = S.canopyCameraDistance; wantHeight = 2.8f;
                    break;
                default:
                    vPitch = lookPitch; vYaw = lookYaw; vRoll = mode.Crashed ? 25f : 0f;
                    wantDist = S.landedCameraDistance; wantHeight = 1.3f;
                    break;
            }
            Quaternion viewLocal = Quaternion.Euler(vPitch, vYaw, vRoll);

            float a = 1f - Mathf.Exp(-S.cameraSmoothing * dt);
            camBlend = Mathf.Lerp(camBlend, ThirdPerson ? 1f : 0f, a);
            camDist = Mathf.Lerp(camDist, wantDist, a);
            camHeight = Mathf.Lerp(camHeight, wantHeight, a);
            camShake = Mathf.MoveTowards(camShake, 0f, dt * 1.2f);

            // first person: the eyes (with a dip on the landing knee bend)
            Quaternion fpRot = transform.rotation * viewLocal;
            Vector3 eye = transform.TransformPoint(new Vector3(0f, 1.62f - LandCompress * 0.55f, 0f));

            // third person: orbit behind the jumper along the same view direction (less roll, it's a chase cam)
            // under the canopy the chase camera may not orbit up into the fabric: limit how steeply it looks down
            float tpPitch = vPitch;
            if (State == Phase.Canopy) tpPitch = Mathf.Clamp(vPitch, -30f, 30f);
            else if (State == Phase.Deploying) tpPitch = Mathf.Min(vPitch, Mathf.Lerp(89f, 30f, deployK));
            Quaternion tpRot = transform.rotation * Quaternion.Euler(tpPitch, vYaw, vRoll * 0.35f);
            Vector3 target = transform.position + Vector3.up * camHeight;
            Vector3 wantOffset = -(tpRot * Vector3.forward) * camDist;
            if (State == Phase.Plane && mode.PlaneTransform != null)
            {
                // in the door: an orbit would put the camera inside the fuselage, so watch the jumper from outside the
                // hull, a little aft and below the wing; mouse look shifts the framing towards where the jumper looks
                Transform pl = mode.PlaneTransform;
                wantOffset = pl.right * 2.4f - pl.forward * camDist + Vector3.up * 0.15f;
                tpRot = Quaternion.LookRotation(target + fpRot * Vector3.forward * 1.5f - (target + wantOffset), Vector3.up);
            }
            // the chase offset (relative to the jumper, so there is no lag at 50 m/s) and its rotation are eased, so
            // the cuts between door, free fall, opening and canopy views become smooth glides
            float ease = 1f - Mathf.Exp(-S.cameraSmoothing * 3f * dt);
            if (!tpInit) { tpOffset = wantOffset; tpRotSmooth = tpRot; tpInit = true; }
            tpOffset = Vector3.Lerp(tpOffset, wantOffset, ease);
            tpRotSmooth = Quaternion.Slerp(tpRotSmooth, tpRot, ease);
            tpRot = tpRotSmooth;
            float offLen = tpOffset.magnitude;
            Vector3 back = offLen > 0.001f ? tpOffset / offLen : Vector3.back;
            float dist = offLen;
            RaycastHit hit;
            if (offLen > 0.001f && Physics.SphereCast(target, 0.3f, back, out hit, offLen, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.4f, hit.distance - 0.1f);
            Vector3 tpPos = target + back * dist;
            if (tpPos.y < ParachuteMode.WaterLevel + 0.6f) tpPos.y = ParachuteMode.WaterLevel + 0.6f;

            float t = Mathf.SmoothStep(0f, 1f, camBlend);
            Vector3 pos = Vector3.Lerp(eye, tpPos, t);
            Quaternion rot = Quaternion.Slerp(fpRot, tpRot, t);
            if (camShake > 0.001f) pos += Random.insideUnitSphere * camShake * 0.12f * GameSettings.ShakeIntensity;
            p.cameraPivot.SetPositionAndRotation(pos, rot);

            // near plane: big up high, but always well in front of the jumper in third person
            cam.nearClipPlane = Mathf.Lerp(nearFp, Mathf.Clamp(dist * 0.3f, 0.08f, nearFp > 0.1f ? nearFp : 0.3f), t);
            Util.SetFov(cam, Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4f * dt)));

            // show the skydiver body to its own camera only in third person
            int bodyBit = 1 << Layers.PlayerBody(p.index);
            cam.cullingMask = t > 0.3f ? (cam.cullingMask | bodyBit) : (cam.cullingMask & ~bodyBit);

            // suspension lines: hair-thin from the harness, a little thicker from the chase distance so they don't vanish
            if (lines != null && canopy != null && canopy.gameObject.activeSelf)
            {
                float w = Mathf.Lerp(0.008f, 0.022f, t);
                for (int i = 0; i < lines.Length; i++) { lines[i].startWidth = w; lines[i].endWidth = w; }
            }
        }

        void UpdateRig(float dt)
        {
            if (rig == null) return;
            SkydiverRig.FlightState s = new SkydiverRig.FlightState
            {
                phase = State,
                lean = Lean,
                speedControl = State == Phase.Canopy ? Mathf.Clamp(lastMove.y + SpeedControl, -1f, 1f) : SpeedControl,
                move = lastMove,
                deploy = DeployProgress,
                flaring = Flaring,
                landCompress = LandCompress,
                crashed = mode.Crashed,
                speed = Velocity.magnitude
            };
            rig.Drive(s, dt);
        }

        void UpdateComfort(float dt, bool vr)
        {
            if (!vr) { ComfortVignette.ExternalMotion = -1f; return; }
            float accel = dt > 0f ? (hv - prevHv).magnitude / dt : 0f;
            float yawRate = dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(prevHeading, Heading)) / dt : 0f;
            prevHv = hv;
            prevHeading = Heading;
            float target = Airborne ? Mathf.Clamp01(accel / 14f + yawRate / 80f + (State == Phase.Deploying ? 0.5f : 0f) + 0.15f) : 0f;
            comfort = Mathf.Lerp(comfort, target, 1f - Mathf.Exp(-4f * dt));
            ComfortVignette.ExternalMotion = comfort;
        }

        /// <summary>VR: the IMGUI HUD is not visible in the headset, so the key prompts float in front of the eyes.</summary>
        void UpdateVrPrompt(bool vr)
        {
            bool jumpZone = State == Phase.Plane && mode.GreenLight;
            bool show = vr && p.cam != null && (CanDeploy || jumpZone);
            if (!show)
            {
                if (vrPrompt != null && vrPrompt.gameObject.activeSelf) vrPrompt.gameObject.SetActive(false);
                return;
            }
            if (vrPrompt == null)
            {
                vrPrompt = ModeManager.Label(p.cam.transform, "", new Vector3(0f, -0.32f, 1.6f), 0f, 0.011f, Color.white);
                vrPrompt.gameObject.name = "VR_SkydivePrompt";
            }
            if (!vrPrompt.gameObject.activeSelf) vrPrompt.gameObject.SetActive(true);
            if (jumpZone)
            {
                vrPrompt.text = Loc.T("GREEN LIGHT - JUMP!") + "\n[" + SkydiveSettings.KeyName(S.jumpInput, true) + "]";
                vrPrompt.color = new Color(0.3f, 1f, 0.45f);
            }
            else
            {
                bool urgent = Agl <= S.warningAltitude;
                vrPrompt.text = Loc.T("OPEN PARACHUTE") + "\n[" + SkydiveSettings.KeyName(S.deployInput, true) + "]  " + Loc.T("or") + "  [" +
                                SkydiveSettings.KeyName(S.deployAltInput, true) + "]\n" + Mathf.RoundToInt(Agl) + " m";
                bool blink = urgent && Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.2f;
                vrPrompt.color = urgent ? (blink ? new Color(1f, 0.15f, 0.15f) : new Color(1f, 0.9f, 0.2f)) : new Color(0.3f, 1f, 0.45f);
            }
        }

        // ===================================================================================== canopy model

        void BuildCanopy()
        {
            GameObject root = new GameObject("Canopy");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            canopy = root.transform;
            const int cells = 9;
            const float arc = 7f;
            lines = new LineRenderer[cells];
            for (int i = 0; i < cells; i++)
            {
                float a = (i - (cells - 1) * 0.5f) * 9f;
                float r = a * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(r) * arc, Mathf.Cos(r) * arc, 0.6f);
                Util.Part(canopy, PrimitiveType.Cube, "Cell", pos, new Vector3(0f, 0f, -a), new Vector3(1.15f, 0.38f, 3.2f), i % 2 == 0 ? "Canopy" : "PlaneBody");
                GameObject lineGo = new GameObject("Line");
                lineGo.transform.SetParent(canopy, false);
                LineRenderer lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                Vector3 bottom = pos - new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0f) * 0.2f;
                // the lines meet at the risers above the shoulders (not at the camera) so they stay thin in view
                Vector3 riser = new Vector3(i < cells / 2 ? -0.28f : (i > cells / 2 ? 0.28f : 0f), 0.75f, -0.05f);
                lr.SetPosition(0, bottom + new Vector3(0f, 0f, 1.2f));
                lr.SetPosition(1, riser);
                lr.SetPosition(2, bottom + new Vector3(0f, 0f, -1.2f));
                lr.startWidth = 0.008f;
                lr.endWidth = 0.008f;
                lr.sharedMaterial = Mats.FxTracer;
                lr.startColor = new Color(0.9f, 0.9f, 0.95f, 0.75f);
                lr.endColor = new Color(0.9f, 0.9f, 0.95f, 0.75f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lines[i] = lr;
            }
            Util.NoShadows(root);
            root.SetActive(false);
        }
    }
}
