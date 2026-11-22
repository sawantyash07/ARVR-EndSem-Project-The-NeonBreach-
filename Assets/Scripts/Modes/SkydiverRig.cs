using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Procedurally built and procedurally animated skydiver body (helmet, suit, container, jointed arms and legs),
    /// shown by the third-person chase camera in Parachute mode. Built from primitives with the shared instanced
    /// materials, like the rest of the game, so it needs no model or animation assets.
    ///
    /// <see cref="ParachuteController"/> feeds it the flight state every frame (<see cref="Drive"/>); the rig blends
    /// key poses (standing in the door, belly-to-earth box, head-down dive, track, spread eagle, canopy with toggles,
    /// flare, landing crouch, crash) and eases every joint towards the result, plus a little wind flutter.
    /// It lives on the player's own body layer, so the first-person and VR cameras never see it.
    /// </summary>
    public class SkydiverRig : MonoBehaviour
    {
        /// <summary>Flight state the rig animates from.</summary>
        public struct FlightState
        {
            public ParachuteController.Phase phase;
            public float lean;          // 0 flat .. 1 head down (look-down lean)
            public float speedControl;  // -1 slow (spread) .. +1 dive
            public Vector2 move;        // tracking / steering input
            public float deploy;        // 0..1 during the opening
            public bool flaring;
            public float landCompress;  // 0..1 knee bend after touchdown
            public bool crashed;
            public float speed;         // m/s, for the flutter
        }

        /// <summary>Joint angles of one pose (local Euler angles, left side; the right side is mirrored).</summary>
        struct Pose
        {
            public float pitch, roll, height;
            public Vector3 shoulder, elbow, hip, knee;
            public Vector3 shoulderR, elbowR, hipR, kneeR;   // only used when asymmetric = true
            public bool asymmetric;

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                Pose p = new Pose();
                p.pitch = Mathf.Lerp(a.pitch, b.pitch, t);
                p.roll = Mathf.Lerp(a.roll, b.roll, t);
                p.height = Mathf.Lerp(a.height, b.height, t);
                p.shoulder = Vector3.Lerp(a.shoulder, b.shoulder, t);
                p.elbow = Vector3.Lerp(a.elbow, b.elbow, t);
                p.hip = Vector3.Lerp(a.hip, b.hip, t);
                p.knee = Vector3.Lerp(a.knee, b.knee, t);
                p.asymmetric = a.asymmetric || b.asymmetric;
                p.shoulderR = Vector3.Lerp(a.R(a.shoulderR, a.shoulder), b.R(b.shoulderR, b.shoulder), t);
                p.elbowR = Vector3.Lerp(a.R(a.elbowR, a.elbow), b.R(b.elbowR, b.elbow), t);
                p.hipR = Vector3.Lerp(a.R(a.hipR, a.hip), b.R(b.hipR, b.hip), t);
                p.kneeR = Vector3.Lerp(a.R(a.kneeR, a.knee), b.R(b.kneeR, b.knee), t);
                return p;
            }

            /// <summary>Right-side angles: explicit when asymmetric, otherwise the mirror of the left side.</summary>
            public Vector3 R(Vector3 right, Vector3 left) { return asymmetric ? right : Mirror(left); }
        }

        static Vector3 Mirror(Vector3 e) { return new Vector3(e.x, -e.y, -e.z); }

        static Pose Sym(float pitch, float height, Vector3 shoulder, Vector3 elbow, Vector3 hip, Vector3 knee)
        {
            return new Pose { pitch = pitch, height = height, shoulder = shoulder, elbow = elbow, hip = hip, knee = knee };
        }

        static Pose MakeAsym(Pose p)
        {
            p.shoulderR = Mirror(p.shoulder); p.elbowR = Mirror(p.elbow); p.hipR = Mirror(p.hip); p.kneeR = Mirror(p.knee);
            p.asymmetric = true;
            return p;
        }

        // Key poses. Body pitch: 0 = upright, +90 = belly to earth (face down), +160 = head down.
        // Left shoulder z: 0 = arm down at the side, -90 = straight out to the left, -180 = above the head.
        static readonly Pose Stand    = Sym(0f,   0f,    new Vector3(0f, 0f, -8f),    new Vector3(-10f, 0f, 0f),  new Vector3(0f, 0f, -2f),   new Vector3(4f, 0f, 0f));
        static readonly Pose Box      = Sym(90f,  0f,    new Vector3(0f, 0f, -125f),  new Vector3(0f, 0f, -55f),  new Vector3(0f, 0f, -22f),  new Vector3(85f, 0f, 0f));
        static readonly Pose Dive     = Sym(158f, 0f,    new Vector3(-12f, 0f, -14f), new Vector3(0f, 0f, -4f),   new Vector3(0f, 0f, -3f),   new Vector3(6f, 0f, 0f));
        static readonly Pose Track    = Sym(102f, 0f,    new Vector3(-25f, 0f, -28f), new Vector3(0f, 0f, -6f),   new Vector3(0f, 0f, -8f),   new Vector3(4f, 0f, 0f));
        static readonly Pose Spread   = Sym(86f,  0f,    new Vector3(0f, 0f, -100f),  new Vector3(0f, 0f, -12f),  new Vector3(0f, 0f, -38f),  new Vector3(30f, 0f, 0f));
        static readonly Pose BackSlide= Sym(72f,  0f,    new Vector3(20f, 0f, -150f), new Vector3(0f, 0f, -30f),  new Vector3(-20f, 0f, -18f), new Vector3(60f, 0f, 0f));
        static readonly Pose Hang     = Sym(4f,   0f,    new Vector3(0f, 0f, -158f),  new Vector3(0f, 0f, 12f),   new Vector3(-8f, 0f, -4f),  new Vector3(12f, 0f, 0f));
        static readonly Pose Flare    = Sym(-8f,  0f,    new Vector3(-10f, 0f, -35f), new Vector3(-20f, 0f, 0f),  new Vector3(-38f, 0f, -4f), new Vector3(22f, 0f, 0f));
        static readonly Pose Crouch   = Sym(18f,  -0.42f, new Vector3(-35f, 0f, -18f), new Vector3(-40f, 0f, 0f), new Vector3(-75f, 0f, -6f), new Vector3(110f, 0f, 0f));
        static readonly Pose Crashed  = Sym(88f,  -0.72f, new Vector3(0f, 0f, -150f), new Vector3(0f, 0f, -20f),  new Vector3(0f, 0f, -14f),  new Vector3(10f, 0f, 0f));

        Transform body, shoulderL, shoulderR, elbowL, elbowR, hipL, hipR, kneeL, kneeR;
        float baseHeight;
        float flutterSeed;

        /// <summary>Build the rig as a child of the player root on the given (body) layer.</summary>
        public static SkydiverRig Build(Transform playerRoot, int layer, string accent)
        {
            GameObject root = new GameObject("SkydiverRig");
            root.transform.SetParent(playerRoot, false);
            SkydiverRig rig = root.AddComponent<SkydiverRig>();
            rig.Construct(accent);
            Util.SetLayerRecursive(root, layer);
            Util.NoShadows(root);
            return rig;
        }

        void Construct(string accent)
        {
            const string suit = "EnemyBlue", dark = "GunGrip", metal = "GunMetal";
            baseHeight = 0.98f;
            flutterSeed = Random.Range(0f, 100f);
            // body pivot at the hips/centre of mass: rotating it lays the jumper flat
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localPosition = new Vector3(0f, baseHeight, 0f);

            Util.Part(body, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.40f, 0.32f, 0.26f), suit);
            Util.Part(body, PrimitiveType.Cube, "ChestStripe", new Vector3(0f, 0.36f, 0.125f), Vector3.zero, new Vector3(0.30f, 0.04f, 0.02f), accent);
            Util.Part(body, PrimitiveType.Cube, "Container", new Vector3(0f, 0.30f, -0.18f), Vector3.zero, new Vector3(0.36f, 0.46f, 0.15f), "Canopy");
            Util.Part(body, PrimitiveType.Cube, "ContainerFlap", new Vector3(0f, 0.30f, -0.26f), Vector3.zero, new Vector3(0.30f, 0.38f, 0.02f), dark);
            Util.Part(body, PrimitiveType.Cube, "Harness", new Vector3(0f, 0.08f, 0.02f), Vector3.zero, new Vector3(0.42f, 0.06f, 0.29f), metal);
            Util.Part(body, PrimitiveType.Cylinder, "Ripcord", new Vector3(0.11f, 0.12f, -0.27f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.02f, 0.06f), "NeonYellow");
            // head: helmet + visor + goggles strap
            Util.Part(body, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 0.68f, 0f), Vector3.zero, new Vector3(0.27f, 0.28f, 0.29f), dark);
            Util.Part(body, PrimitiveType.Cube, "Visor", new Vector3(0f, 0.68f, 0.125f), Vector3.zero, new Vector3(0.2f, 0.07f, 0.05f), accent);
            Util.Part(body, PrimitiveType.Cube, "Strap", new Vector3(0f, 0.68f, 0f), Vector3.zero, new Vector3(0.275f, 0.03f, 0.27f), metal);

            shoulderL = Limb(body, "ShoulderL", new Vector3(-0.23f, 0.47f, 0f), 0.29f, 0.10f, suit, out elbowL, 0.26f, 0.085f, suit, dark);
            shoulderR = Limb(body, "ShoulderR", new Vector3(0.23f, 0.47f, 0f), 0.29f, 0.10f, suit, out elbowR, 0.26f, 0.085f, suit, dark);
            hipL = Limb(body, "HipL", new Vector3(-0.1f, 0.02f, 0f), 0.42f, 0.15f, suit, out kneeL, 0.42f, 0.12f, suit, dark);
            hipR = Limb(body, "HipR", new Vector3(0.1f, 0.02f, 0f), 0.42f, 0.15f, suit, out kneeR, 0.42f, 0.12f, suit, dark);
            // accent stripes down the legs
            Util.Part(hipL, PrimitiveType.Cube, "Stripe", new Vector3(-0.075f, -0.21f, 0f), Vector3.zero, new Vector3(0.01f, 0.34f, 0.04f), accent);
            Util.Part(hipR, PrimitiveType.Cube, "Stripe", new Vector3(0.075f, -0.21f, 0f), Vector3.zero, new Vector3(0.01f, 0.34f, 0.04f), accent);
        }

        /// <summary>A two-segment limb: joint -> upper segment -> second joint -> lower segment -> end cap (hand / boot).</summary>
        static Transform Limb(Transform parent, string name, Vector3 at, float upperLen, float upperW, string upperMat,
                              out Transform mid, float lowerLen, float lowerW, string lowerMat, string capMat)
        {
            Transform j = new GameObject(name).transform;
            j.SetParent(parent, false);
            j.localPosition = at;
            Util.Part(j, PrimitiveType.Capsule, "Upper", new Vector3(0f, -upperLen * 0.5f, 0f), Vector3.zero, new Vector3(upperW, upperLen * 0.5f + upperW * 0.25f, upperW), upperMat);
            mid = new GameObject(name + "Mid").transform;
            mid.SetParent(j, false);
            mid.localPosition = new Vector3(0f, -upperLen, 0f);
            Util.Part(mid, PrimitiveType.Capsule, "Lower", new Vector3(0f, -lowerLen * 0.5f, 0f), Vector3.zero, new Vector3(lowerW, lowerLen * 0.5f + lowerW * 0.25f, lowerW), lowerMat);
            bool leg = name.StartsWith("Hip");
            if (leg) Util.Part(mid, PrimitiveType.Cube, "Boot", new Vector3(0f, -lowerLen - 0.02f, 0.05f), Vector3.zero, new Vector3(lowerW * 1.05f, 0.09f, 0.24f), capMat);
            else Util.Part(mid, PrimitiveType.Sphere, "Glove", new Vector3(0f, -lowerLen - 0.03f, 0f), Vector3.zero, Vector3.one * lowerW * 1.15f, capMat);
            return j;
        }

        /// <summary>Animate towards the pose for this flight state. Call once per frame.</summary>
        public void Drive(FlightState s, float dt)
        {
            Pose target = Target(s);

            // eases: snappy for the opening shock and the landing, smooth in free fall
            float k = s.phase == ParachuteController.Phase.Deploying || s.phase == ParachuteController.Phase.Landed ? 7f : 4.5f;
            float a = 1f - Mathf.Exp(-k * dt);

            // wind flutter on the limbs (stronger at high speed, nothing on the ground)
            float flutter = s.phase == ParachuteController.Phase.Landed || s.phase == ParachuteController.Phase.Plane ? 0f : Mathf.Clamp01(s.speed / 60f) * 4f;
            float t = Time.time * 9f + flutterSeed;
            Vector3 fl = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, 0f, Mathf.PerlinNoise(0.3f, t) - 0.5f) * flutter * 2f;
            Vector3 fr = new Vector3(Mathf.PerlinNoise(t, 5.1f) - 0.5f, 0f, Mathf.PerlinNoise(5.3f, t) - 0.5f) * flutter * 2f;

            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(target.pitch, 0f, target.roll), a);
            Vector3 bp = body.localPosition;
            bp.y = Mathf.Lerp(bp.y, baseHeight + target.height, a);
            body.localPosition = bp;

            Ease(shoulderL, target.shoulder + fl, a);
            Ease(elbowL, target.elbow, a);
            Ease(shoulderR, target.R(target.shoulderR, target.shoulder) + fr, a);
            Ease(elbowR, target.R(target.elbowR, target.elbow), a);
            Ease(hipL, target.hip + fr * 0.5f, a);
            Ease(kneeL, target.knee, a);
            Ease(hipR, target.R(target.hipR, target.hip) + fl * 0.5f, a);
            Ease(kneeR, target.R(target.kneeR, target.knee), a);
        }

        static void Ease(Transform j, Vector3 euler, float a)
        {
            j.localRotation = Quaternion.Slerp(j.localRotation, Quaternion.Euler(euler), a);
        }

        Pose Target(FlightState s)
        {
            switch (s.phase)
            {
                case ParachuteController.Phase.Plane:
                    return Stand;

                case ParachuteController.Phase.FreeFall:
                {
                    // look-down lean: from the box towards a dive; speed control: spread eagle .. head-down
                    Pose p = Pose.Lerp(Box, Dive, s.lean * 0.45f);
                    if (s.speedControl > 0f) p = Pose.Lerp(p, Dive, s.speedControl);
                    else p = Pose.Lerp(p, Spread, -s.speedControl);
                    // tracking forward sweeps the arms back, sliding back reaches forward
                    if (s.move.y > 0f) p = Pose.Lerp(p, Track, s.move.y * (1f - Mathf.Max(0f, s.speedControl)) * 0.85f);
                    else if (s.move.y < 0f) p = Pose.Lerp(p, BackSlide, -s.move.y * 0.8f);
                    // sliding sideways: roll the body, drop one arm, lift the other
                    p = MakeAsym(p);
                    p.roll = -s.move.x * 18f;
                    p.shoulder += new Vector3(0f, 0f, s.move.x * 22f);
                    p.shoulderR += new Vector3(0f, 0f, s.move.x * 22f);
                    return p;
                }

                case ParachuteController.Phase.Deploying:
                {
                    // the opening shock swings the body from flat to upright, arms reach for the toggles
                    float d = Mathf.SmoothStep(0f, 1f, s.deploy);
                    Pose from = Pose.Lerp(Box, Spread, 0.3f);
                    return Pose.Lerp(from, Hang, d);
                }

                case ParachuteController.Phase.Canopy:
                {
                    if (s.flaring) return Flare;
                    Pose p = Hang;
                    // brakes pull both toggles down, front risers lean the body forward
                    float brake = Mathf.Max(0f, -s.speedControl);
                    float fast = Mathf.Max(0f, s.speedControl);
                    p = Pose.Lerp(p, Pose.Lerp(Hang, Flare, 0.55f), brake);
                    p.pitch += fast * 14f;
                    p = MakeAsym(p);
                    // steering: pull the toggle on the side you turn to
                    float turn = s.move.x;
                    if (turn < 0f) p.shoulder = Vector3.Lerp(p.shoulder, new Vector3(0f, 0f, -70f), -turn);
                    if (turn > 0f) p.shoulderR = Vector3.Lerp(p.shoulderR, new Vector3(0f, 0f, 70f), turn);
                    p.roll = -turn * 12f;
                    // legs together and slightly forward when close to the ground is handled by the flare pose
                    return p;
                }

                default:
                    if (s.crashed) return Crashed;
                    return Pose.Lerp(Stand, Crouch, s.landCompress);
            }
        }
    }
}
