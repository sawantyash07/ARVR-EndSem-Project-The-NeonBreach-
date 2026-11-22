using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach.Farm
{
    /// <summary>
    /// VR support for the farm (OpenXR through the core UnityEngine.XR API, the same way the Neon Breach VRRig does it).
    /// When a headset is active the camera follows the head, the rifle is held in the right controller and the
    /// controllers fill the shared PlayerInputState:
    ///   L-stick move · R-stick snap / smooth turn (Settings → VR comfort) · R-trigger fire · R-grip reload ·
    ///   A jump · B or L-trigger change scope magnification · X / Y interact (ammo crate) · L-stick click sprint ·
    ///   pull R-stick back crouch · Menu pause. Menus: either stick up/down + trigger or A.
    /// With no headset this component does nothing and the farm plays on keyboard + mouse / gamepad.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class FarmVRRig : MonoBehaviour
    {
        public bool forceDesktop;
        [Header("Rig (assigned by the scene builder)")]
        public Transform trackingSpace;
        public Transform rightHand;
        public Transform leftHand;
        [Tooltip("Eye height used for seated play / headsets that report device-relative tracking.")]
        public float standingEyeHeight = 1.62f;
        [Range(0.1f, 0.9f)] public float stickDeadZone = 0.2f;

        public static bool HeadsetPresent { get { return InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid; } }
        public bool Active { get { return !forceDesktop && HeadsetPresent; } }

        public bool MenuPressed { get; private set; }
        public bool ZoomPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        /// <summary>-1 = up, +1 = down, edge-triggered (for world-space menus).</summary>
        public int MenuStep { get; private set; }
        public float TurnRate { get; private set; }

        float heightOffset;
        bool calibrated;
        float snapCooldown;
        float navCooldown;
        bool pTrigR, pGripR, pA, pB, pTrigL, pX, pY, pMenu;

        public Vector3 HeadForwardFlat(Camera cam)
        {
            Vector3 f = cam != null ? cam.transform.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude < 0.0001f ? transform.forward : f.normalized;
        }

        static bool Btn(InputDevice d, InputFeatureUsage<bool> usage)
        {
            bool v;
            return d.isValid && d.TryGetFeatureValue(usage, out v) && v;
        }

        Vector2 Stick(InputDevice d)
        {
            Vector2 v;
            if (d.isValid && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out v))
                return v.magnitude < stickDeadZone ? Vector2.zero : v;
            return Vector2.zero;
        }

        /// <summary>Menu buttons are read every frame (also while paused).</summary>
        void Update()
        {
            MenuPressed = ZoomPressed = InteractPressed = ConfirmPressed = false;
            TriggerDown = GripDown = ADown = false;
            MenuStep = 0;
            if (!Active) return;
            InputDevice L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool menu = Btn(L, CommonUsages.menuButton) || Btn(R, CommonUsages.menuButton);
            bool trigR = Btn(R, CommonUsages.triggerButton);
            bool a = Btn(R, CommonUsages.primaryButton);
            bool b = Btn(R, CommonUsages.secondaryButton);
            bool trigL = Btn(L, CommonUsages.triggerButton);
            bool x = Btn(L, CommonUsages.primaryButton);
            bool y = Btn(L, CommonUsages.secondaryButton);
            bool gripR = Btn(R, CommonUsages.gripButton);
            MenuPressed = menu && !pMenu;
            TriggerDown = trigR && !pTrigR;
            GripDown = gripR && !pGripR;
            ADown = a && !pA;
            pGripR = gripR;
            ConfirmPressed = (trigR && !pTrigR) || (a && !pA);
            ZoomPressed = (b && !pB) || (trigL && !pTrigL);
            InteractPressed = (x && !pX) || (y && !pY);

            Vector2 nav = Stick(L) + Stick(R);
            navCooldown -= Time.unscaledDeltaTime;
            if (Mathf.Abs(nav.y) > 0.6f && navCooldown <= 0f) { MenuStep = nav.y > 0f ? -1 : 1; navCooldown = 0.28f; }
            if (Mathf.Abs(nav.y) < 0.3f) navCooldown = 0f;

            pMenu = menu; pTrigR = trigR; pA = a; pB = b; pTrigL = trigL; pX = x; pY = y;
        }

        /// <summary>Gameplay input from the controllers (called by FarmPlayer while playing).</summary>
        public void Read(PlayerInputState s, float dt)
        {
            InputDevice L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Vector2 ls = Stick(L);
            if (ls.sqrMagnitude > s.move.sqrMagnitude) s.move = Vector2.ClampMagnitude(ls, 1f);

            Vector2 rs = Stick(R);
            TurnRate = 0f;
            if (GameSettings.Turn == TurnMode.Smooth)
            {
                float tx = Mathf.Abs(rs.x) > 0.25f ? rs.x : 0f;
                TurnRate = tx * GameSettings.SmoothTurnSpeed;
                s.snapTurn += TurnRate * dt;
            }
            else
            {
                snapCooldown -= dt;
                if (Mathf.Abs(rs.x) > 0.7f && snapCooldown <= 0f) { s.snapTurn += Mathf.Sign(rs.x) * GameSettings.SnapAngle; snapCooldown = 0.3f; }
                if (Mathf.Abs(rs.x) < 0.3f) snapCooldown = 0f;
            }
            if (rs.y < -0.7f) s.crouchHeld = true;

            s.fireHeld |= Btn(R, CommonUsages.triggerButton);
            s.firePressed |= TriggerDown;
            s.reloadPressed |= GripDown;
            s.jumpPressed |= ADown;
            s.sprintHeld |= Btn(L, CommonUsages.primary2DAxisClick);
            s.zoomHeld |= Btn(L, CommonUsages.triggerButton);
        }

        // button edges, computed once per frame in Update (also while menus are open)
        bool TriggerDown, GripDown, ADown;

        /// <summary>Applies head and hand poses. Head pose is relative to the tracking space (floor level at the player's feet).</summary>
        public void TrackPoses(Camera cam)
        {
            if (cam == null) return;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            Vector3 pos;
            Quaternion rot;
            if (head.isValid && head.TryGetFeatureValue(CommonUsages.devicePosition, out pos) && head.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
            {
                if (!calibrated)
                {
                    calibrated = true;
                    if (GameSettings.Seated) heightOffset = Mathf.Max(0f, standingEyeHeight - pos.y);
                    else heightOffset = pos.y < 0.7f ? standingEyeHeight : 0f;
                }
                cam.transform.localPosition = pos + Vector3.up * heightOffset;
                cam.transform.localRotation = rot;
            }
            TrackHand(XRNode.RightHand, rightHand);
            TrackHand(XRNode.LeftHand, leftHand);
        }

        void TrackHand(XRNode node, Transform t)
        {
            if (t == null) return;
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            Vector3 pos;
            Quaternion rot;
            if (d.isValid && d.TryGetFeatureValue(CommonUsages.devicePosition, out pos) && d.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
            {
                t.localPosition = pos + Vector3.up * heightOffset;
                t.localRotation = rot;
            }
        }

        public void Recenter() { calibrated = false; }
    }
}
