using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach
{
    /// <summary>
    /// Optional VR mode (OpenXR). Uses Unity's core XR input API for controllers and the XR Hands package for
    /// controller-free hand tracking. When a headset is active:
    ///   - the camera and both hands follow the tracked devices (hands fall back to hand-tracking poses),
    ///   - the weapon is held in the right hand (aim with your hand, a laser sight shows where it points),
    ///   - controller buttons / hand gestures fill the same PlayerInputState used by keyboard + mouse,
    ///   - comfort options: tunnelling vignette while moving, snap OR smooth turning, seated mode (height offset +
    ///     crouch on the stick), recenter; locomotion and turning are locked in mixed-reality mode,
    ///   - a wrist display on the left hand shows health / ammo / wave / score and a world-space menu replaces the
    ///     desktop menus (see VRMenu).
    /// With no headset, this component stays idle and the game plays on desktop.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class VRRig : MonoBehaviour
    {
        public bool forceDesktop;
        public Transform RightHand { get; private set; }
        public Transform LeftHand { get; private set; }
        public HandTracking Hands { get; private set; }
        public VRMenu Menu { get; private set; }

        public static bool HeadsetPresent { get { return InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid; } }
        public bool Active { get { return !forceDesktop && HeadsetPresent; } }
        public bool MenuPressed { get; private set; }
        public float TurnRate { get; private set; }     // degrees per second this frame (smooth turning)
        /// <summary>Mini-game modes put their own read-out on the wrist display (null = shooter stats).</summary>
        public string WristOverride;
        public bool UsingHands { get; private set; }    // true when no controllers are held and hands are tracked

        public Vector3 HeadForwardFlat
        {
            get
            {
                Transform c = cam != null ? cam.transform : transform;
                Vector3 f = c.forward;
                f.y = 0f;
                return f.sqrMagnitude < 0.0001f ? transform.forward : f.normalized;
            }
        }

        PlayerController pc;
        Camera cam;
        Transform pivot;
        TextMesh wrist;
        TextMesh banner;
        LineRenderer laser;
        ComfortVignette vignette;
        float bannerUntil;
        float wristTimer;
        bool calibrated;
        bool calibratedSeated;
        float heightOffset;
        float snapCooldown;
        bool pTrigR, pGripR, pA, pB, pTrigL, pGripL, pX, pY, pMenu, pStickR;
        Transform rightShell, leftShell;

        void Start()
        {
            pc = GetComponent<PlayerController>();
            cam = pc != null ? pc.cam : GetComponentInChildren<Camera>();
            pivot = pc != null && pc.cameraPivot != null ? pc.cameraPivot : (cam != null ? cam.transform.parent : transform);

            RightHand = MakeHand("RightHand", false, out rightShell);
            LeftHand = MakeHand("LeftHand", true, out leftShell);
            BuildWrist();
            BuildBanner();
            BuildLaser();
            SetVisible(false);

            Hands = gameObject.AddComponent<HandTracking>();
            Menu = gameObject.AddComponent<VRMenu>();
            Menu.Init(this);
            if (cam != null) vignette = ComfortVignette.Create(cam);
        }

        Transform MakeHand(string name, bool left, out Transform shell)
        {
            GameObject h = new GameObject(name);
            h.transform.SetParent(pivot, false);
            // small controller shell so the hand is visible
            GameObject s = Util.Part(h.transform, PrimitiveType.Cube, "Shell", new Vector3(0f, -0.02f, 0.02f), Vector3.zero,
                                     new Vector3(0.05f, 0.04f, 0.12f), left ? "GunMetal" : "GunGrip");
            Util.Part(h.transform, PrimitiveType.Sphere, "Glow", new Vector3(0f, 0.01f, 0.07f), Vector3.zero,
                      Vector3.one * 0.035f, left ? "NeonMagenta" : "NeonCyan");
            Util.NoShadows(h);
            shell = s.transform;
            return h.transform;
        }

        Font BuiltinFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        public TextMesh MakeText(Transform parent, string name, int size, float charSize, TextAnchor anchor)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            TextMesh tm = g.AddComponent<TextMesh>();
            Font f = BuiltinFont();
            tm.font = f;
            tm.fontSize = size;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Left;
            tm.color = new Color(0.2f, 0.95f, 1f, 1f);
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (f != null) mr.sharedMaterial = f.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        void BuildWrist()
        {
            wrist = MakeText(LeftHand, "WristDisplay", 64, 0.0035f, TextAnchor.MiddleCenter);
            wrist.transform.localPosition = new Vector3(0f, 0.045f, 0.0f);
            wrist.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wrist.alignment = TextAlignment.Center;
        }

        void BuildBanner()
        {
            banner = MakeText(transform, "VRBanner", 80, 0.03f, TextAnchor.MiddleCenter);
            banner.alignment = TextAlignment.Center;
            banner.gameObject.SetActive(false);
        }

        void BuildLaser()
        {
            GameObject g = new GameObject("Laser");
            g.transform.SetParent(RightHand, false);
            laser = g.AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.startWidth = 0.004f;
            laser.endWidth = 0.002f;
            laser.sharedMaterial = Mats.FxTracer;
            laser.startColor = new Color(1f, 0.1f, 0.1f, 0.8f);
            laser.endColor = new Color(1f, 0.1f, 0.1f, 0.1f);
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.useWorldSpace = true;
        }

        void SetVisible(bool on)
        {
            if (RightHand != null) RightHand.gameObject.SetActive(on);
            if (LeftHand != null) LeftHand.gameObject.SetActive(on);
        }

        public void ShowBanner(string text)
        {
            if (banner == null || !Active) return;
            banner.text = text;
            bannerUntil = Time.unscaledTime + 3.5f;
            banner.gameObject.SetActive(true);
            PlaceBanner();
        }

        void PlaceBanner()
        {
            if (cam == null) return;
            Vector3 fwd = HeadForwardFlat;
            banner.transform.position = cam.transform.position + fwd * 3f + Vector3.up * 0.2f;
            banner.transform.rotation = Quaternion.LookRotation(banner.transform.position - cam.transform.position, Vector3.up);
        }

        /// <summary>Re-measure standing / seated height and put the head back over the body.</summary>
        public void Recenter()
        {
            calibrated = false;
            if (pc != null) pc.RecenterVr();
        }

        // ------------------------------------------------------------------------------------ input

        static bool Btn(InputDevice d, InputFeatureUsage<bool> usage)
        {
            bool v;
            return d.isValid && d.TryGetFeatureValue(usage, out v) && v;
        }

        static Vector2 Stick(InputDevice d)
        {
            Vector2 v;
            if (d.isValid && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out v))
            {
                if (v.magnitude < 0.18f) return Vector2.zero;
                return v;
            }
            return Vector2.zero;
        }

        /// <summary>Translate controller state (or hand gestures) into the shared PlayerInputState.</summary>
        public void Read(PlayerInputState s)
        {
            InputDevice L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool mr = MixedReality.Active;
            float dt = Time.deltaTime;

            // --- locomotion (disabled in mixed reality: you walk for real there)
            Vector2 ls = Stick(L);
            if (!mr) s.move = Vector2.ClampMagnitude(ls, 1f);

            // --- turning: snap (with a short blink) or smooth
            Vector2 rs = Stick(R);
            TurnRate = 0f;
            if (!mr)
            {
                if (GameSettings.Turn == TurnMode.Smooth)
                {
                    float x = Mathf.Abs(rs.x) > 0.25f ? rs.x : 0f;
                    TurnRate = x * GameSettings.SmoothTurnSpeed;
                    s.snapTurn = TurnRate * dt;
                }
                else
                {
                    snapCooldown -= dt;
                    if (Mathf.Abs(rs.x) > 0.7f && snapCooldown <= 0f)
                    {
                        s.snapTurn = Mathf.Sign(rs.x) * GameSettings.SnapAngle;
                        snapCooldown = 0.3f;
                        if (vignette != null) vignette.Blink();
                    }
                    if (Mathf.Abs(rs.x) < 0.3f) snapCooldown = 0f;
                }
            }
            // pull the right stick back to crouch (essential in seated mode)
            if (rs.y < -0.7f) s.crouchHeld = true;

            // --- controller buttons
            bool trigR = Btn(R, CommonUsages.triggerButton);
            bool gripR = Btn(R, CommonUsages.gripButton);
            bool a = Btn(R, CommonUsages.primaryButton);
            bool b = Btn(R, CommonUsages.secondaryButton);
            bool stickR = Btn(R, CommonUsages.primary2DAxisClick);
            bool trigL = Btn(L, CommonUsages.triggerButton);
            bool gripL = Btn(L, CommonUsages.gripButton);
            bool x2 = Btn(L, CommonUsages.primaryButton);
            bool y = Btn(L, CommonUsages.secondaryButton);
            bool menu = Btn(L, CommonUsages.menuButton);

            s.fireHeld |= trigR;
            s.firePressed |= trigR && !pTrigR;
            s.reloadPressed |= gripR && !pGripR;
            s.jumpPressed |= a && !pA;
            if (b && !pB) s.weaponCycle = 1;
            s.dashPressed |= stickR && !pStickR && !mr;
            s.grenadePressed |= trigL && !pTrigL;
            s.zoomHeld |= trigL;
            s.meleePressed |= gripL && !pGripL;
            s.flashlightPressed |= x2 && !pX;
            s.medkitPressed |= y && !pY;
            s.sprintHeld |= Btn(L, CommonUsages.primary2DAxisClick) && !mr;
            MenuPressed = menu && !pMenu;

            pTrigR = trigR; pGripR = gripR; pA = a; pB = b; pStickR = stickR;
            pTrigL = trigL; pGripL = gripL; pX = x2; pY = y; pMenu = menu;

            // --- hand-tracking gestures (only for a hand that is not holding a controller)
            if (Hands != null)
            {
                if (!R.isValid && Hands.Right.tracked)
                {
                    s.fireHeld |= Hands.Right.pinch;
                    s.firePressed |= Hands.Right.pinchDown;
                    s.reloadPressed |= Hands.Right.palmTriggered;
                }
                if (!L.isValid && Hands.Left.tracked)
                {
                    s.grenadePressed |= Hands.Left.pinchDown;
                    s.medkitPressed |= Hands.Left.palmTriggered;
                }
            }
        }

        // ------------------------------------------------------------------------------------ tracking

        void Update()
        {
            // keep the menu button working while the game is paused (PlayerController is idle then)
            if (Active && GameManager.Instance != null && !GameManager.Instance.Playing)
            {
                InputDevice L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                bool menu = Btn(L, CommonUsages.menuButton);
                MenuPressed = menu && !pMenu;
                pMenu = menu;
            }
            if (calibratedSeated != GameSettings.Seated) { calibrated = false; calibratedSeated = GameSettings.Seated; }
        }

        void LateUpdate()
        {
            bool on = Active;
            if (RightHand != null && RightHand.gameObject.activeSelf != on) SetVisible(on);
            if (vignette != null) vignette.SetRig(on ? this : null, pc);
            if (!on || cam == null) return;

            Track(XRNode.CenterEye, cam.transform, true);
            InputDevice rd = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice ld = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool rTracked = Track(XRNode.RightHand, RightHand, false);
            bool lTracked = Track(XRNode.LeftHand, LeftHand, false);
            UsingHands = false;
            if (Hands != null)
            {
                if ((!rd.isValid || !rTracked) && Hands.Right.tracked) { ApplyPose(RightHand, Hands.Right.aim); UsingHands = true; }
                if ((!ld.isValid || !lTracked) && Hands.Left.tracked) { ApplyPose(LeftHand, Hands.Left.aim); UsingHands = true; }
            }
            if (rightShell != null) rightShell.gameObject.SetActive(!UsingHands);
            if (leftShell != null) leftShell.gameObject.SetActive(!UsingHands);

            // laser sight along the muzzle (hidden while a menu panel is open - the menu draws its own pointer)
            bool menuOpen = Menu != null && Menu.Visible;
            if (laser != null) laser.enabled = !menuOpen && GameManager.Instance != null && GameManager.Instance.Playing
                                               && (ModeManager.Instance == null || ModeManager.Instance.IsShooter);
            if (pc != null && pc.weapons != null && laser != null && laser.enabled)
            {
                Transform m = pc.weapons.CurrentMuzzle;
                Vector3 end = m.position + m.forward * 30f;
                RaycastHit hit;
                if (Physics.Raycast(m.position, m.forward, out hit, 30f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) end = hit.point;
                laser.SetPosition(0, m.position);
                laser.SetPosition(1, end);
            }

            // wrist display
            wristTimer -= Time.unscaledDeltaTime;
            if (wristTimer <= 0f && wrist != null && GameManager.Instance != null && !string.IsNullOrEmpty(WristOverride))
            {
                wristTimer = 0.15f;
                wrist.text = WristOverride;
            }
            else if (wristTimer <= 0f && wrist != null && GameManager.Instance != null)
            {
                wristTimer = 0.15f;
                GameManager gm = GameManager.Instance;
                WeaponSystem w = pc.weapons;
                string extra = "";
                if (PowerUps.DoubleDamage) extra += "\nx2 DMG " + Mathf.CeilToInt(PowerUps.DoubleDamageLeft);
                if (PowerUps.SlowMo) extra += "\nSLOW-MO " + Mathf.CeilToInt(PowerUps.SlowMoLeft);
                wrist.text = "HP " + Mathf.CeilToInt(pc.Health) + "  SH " + Mathf.CeilToInt(pc.Shield) +
                             "\n" + w.Current.name.Split(' ')[0] + "  " + w.Mag + " / " + w.Reserve +
                             "\n" + Loc.T("WAVE") + " " + Mathf.Max(1, gm.Waves.Wave) + "  " + gm.Waves.Remaining +
                             "\n" + gm.Score + "  $" + gm.Credits + extra;
            }

            if (banner != null && banner.gameObject.activeSelf)
            {
                if (Time.unscaledTime > bannerUntil) banner.gameObject.SetActive(false);
                else PlaceBanner();
            }
        }

        void ApplyPose(Transform t, Pose trackingPose)
        {
            t.localPosition = trackingPose.position + Vector3.up * heightOffset;
            t.localRotation = trackingPose.rotation;
        }

        bool Track(XRNode node, Transform t, bool calibrate)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            Vector3 pos;
            Quaternion rot;
            if (!d.isValid) return false;
            bool tracked;
            if (d.TryGetFeatureValue(CommonUsages.isTracked, out tracked) && !tracked && !calibrate) return false;
            if (!d.TryGetFeatureValue(CommonUsages.devicePosition, out pos)) return false;
            if (!d.TryGetFeatureValue(CommonUsages.deviceRotation, out rot)) return false;

            if (calibrate && !calibrated)
            {
                calibrated = true;
                calibratedSeated = GameSettings.Seated;
                if (GameSettings.Seated)
                {
                    // seated: lift the view to standing eye height so cover and enemies line up
                    heightOffset = Mathf.Max(0f, 1.62f - pos.y);
                }
                else
                {
                    // headsets that track relative to the device (not the floor) report a head height near zero
                    heightOffset = pos.y < 0.7f ? 1.6f : 0f;
                }
            }
            t.localPosition = pos + Vector3.up * heightOffset;
            t.localRotation = rot;
            return true;
        }
    }
}
