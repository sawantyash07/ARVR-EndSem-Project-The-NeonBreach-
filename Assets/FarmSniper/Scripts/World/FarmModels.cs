using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    /// <summary>Procedural models: the bolt-action scoped rifle, the field rat and the farmer.</summary>
    public static class FarmModels
    {
        static GameObject P(Transform t, PrimitiveType p, string n, Vector3 pos, Vector3 e, Vector3 s, string mat)
        {
            return FarmBuild.Part(t, p, n, pos, e, s, FarmMats.Get(mat));
        }

        // ------------------------------------------------------------------ rifle

        /// <summary>Bolt-action rifle, forward +Z, origin at the trigger. Returns the configured SniperRifle.</summary>
        public static SniperRifle BuildRifle(Transform parent)
        {
            GameObject root = new GameObject("Sniper Rifle");
            root.transform.SetParent(parent, false);
            Transform m = FarmBuild.Node(root.transform, "Model", Vector3.zero);
            const float bore = 0.012f, scopeY = 0.078f;

            // stock + grip + forend (walnut)
            P(m, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.04f, -0.25f), new Vector3(-4f, 0f, 0f), new Vector3(0.042f, 0.10f, 0.34f), "GunWood");
            P(m, PrimitiveType.Cube, "CheekRest", new Vector3(0f, 0.022f, -0.22f), Vector3.zero, new Vector3(0.04f, 0.03f, 0.17f), "GunWood");
            P(m, PrimitiveType.Cube, "ButtPad", new Vector3(0f, -0.05f, -0.425f), new Vector3(-4f, 0f, 0f), new Vector3(0.046f, 0.125f, 0.022f), "Rubber");
            P(m, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.075f, -0.045f), new Vector3(22f, 0f, 0f), new Vector3(0.034f, 0.085f, 0.05f), "GunWood");
            P(m, PrimitiveType.Cube, "Forend", new Vector3(0f, -0.018f, 0.2f), Vector3.zero, new Vector3(0.048f, 0.055f, 0.34f), "GunWood");
            P(m, PrimitiveType.Cube, "Wrist", new Vector3(0f, -0.02f, -0.07f), Vector3.zero, new Vector3(0.04f, 0.05f, 0.08f), "GunWood");

            // action, barrel, muzzle brake (blued steel)
            FarmBuild.RodZ(m, "Receiver", new Vector3(0f, bore, 0.07f), 0.019f, 0.25f, FarmMats.Get("GunSteel"));
            FarmBuild.RodZ(m, "Barrel", new Vector3(0f, bore, 0.46f), 0.0115f, 0.52f, FarmMats.Get("GunSteel"));
            FarmBuild.RodZ(m, "MuzzleBrake", new Vector3(0f, bore, 0.735f), 0.016f, 0.05f, FarmMats.Get("GunSteel"));
            P(m, PrimitiveType.Cube, "TriggerGuard", new Vector3(0f, -0.052f, 0.0f), Vector3.zero, new Vector3(0.008f, 0.006f, 0.07f), "GunSteel");
            P(m, PrimitiveType.Cube, "Trigger", new Vector3(0f, -0.036f, 0.005f), new Vector3(15f, 0f, 0f), new Vector3(0.006f, 0.025f, 0.006f), "GunSteel");

            Transform mag = FarmBuild.Node(m, "Magazine", new Vector3(0f, -0.045f, 0.075f));
            P(mag, PrimitiveType.Cube, "Box", Vector3.zero, Vector3.zero, new Vector3(0.032f, 0.06f, 0.075f), "GunSteel");
            P(mag, PrimitiveType.Cube, "Floorplate", new Vector3(0f, -0.031f, 0f), Vector3.zero, new Vector3(0.036f, 0.006f, 0.08f), "Metal");

            Transform bolt = FarmBuild.Node(m, "Bolt", new Vector3(0.0f, bore, -0.02f));
            P(bolt, PrimitiveType.Cylinder, "Handle", new Vector3(0.032f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.008f, 0.03f, 0.008f), "GunSteel");
            P(bolt, PrimitiveType.Sphere, "Knob", new Vector3(0.064f, -0.004f, 0f), Vector3.zero, new Vector3(0.022f, 0.022f, 0.022f), "GunSteel");
            FarmBuild.RodZ(bolt, "BoltBody", new Vector3(0f, 0f, -0.035f), 0.012f, 0.06f, FarmMats.Get("Metal"));

            // scope
            FarmBuild.RodZ(m, "ScopeTube", new Vector3(0f, scopeY, 0.06f), 0.015f, 0.3f, FarmMats.Get("GunSteel"));
            FarmBuild.RodZ(m, "ObjectiveBell", new Vector3(0f, scopeY, 0.235f), 0.025f, 0.08f, FarmMats.Get("GunSteel"));
            FarmBuild.RodZ(m, "OcularBell", new Vector3(0f, scopeY, -0.12f), 0.02f, 0.07f, FarmMats.Get("GunSteel"));
            P(m, PrimitiveType.Cylinder, "TurretTop", new Vector3(0f, scopeY + 0.02f, 0.06f), Vector3.zero, new Vector3(0.022f, 0.012f, 0.022f), "GunSteel");
            P(m, PrimitiveType.Cylinder, "TurretSide", new Vector3(0.02f, scopeY, 0.06f), new Vector3(0f, 0f, 90f), new Vector3(0.022f, 0.012f, 0.022f), "GunSteel");
            P(m, PrimitiveType.Cube, "RingFront", new Vector3(0f, (scopeY + bore) * 0.5f, 0.14f), Vector3.zero, new Vector3(0.03f, scopeY - bore, 0.016f), "GunSteel");
            P(m, PrimitiveType.Cube, "RingRear", new Vector3(0f, (scopeY + bore) * 0.5f, -0.02f), Vector3.zero, new Vector3(0.03f, scopeY - bore, 0.016f), "GunSteel");
            GameObject objLens = P(m, PrimitiveType.Quad, "ObjectiveLens", new Vector3(0f, scopeY, 0.276f), Vector3.zero, new Vector3(0.044f, 0.044f, 1f), "ScopeLens");
            objLens.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject lens = FarmBuild.Part(m, PrimitiveType.Quad, "OcularLens", new Vector3(0f, scopeY, -0.156f), new Vector3(0f, 180f, 0f), new Vector3(0.034f, 0.034f, 1f), FarmMats.Unlit("ScopeView", Color.white, true));
            lens.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Transform eye = FarmBuild.Node(root.transform, "ScopeEye", new Vector3(0f, scopeY, -0.165f));

            // muzzle, flash, light, VR scope camera
            Transform muzzle = FarmBuild.Node(root.transform, "Muzzle", new Vector3(0f, bore, 0.765f));
            GameObject flash = FarmBuild.Part(muzzle, PrimitiveType.Sphere, "MuzzleFlash", new Vector3(0f, 0f, 0.05f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.16f),
                FarmMats.Emissive("MuzzleFlash", new Color(1f, 0.75f, 0.35f), new Color(2.2f, 1.4f, 0.5f)));
            Renderer flashR = flash.GetComponent<Renderer>();
            flashR.shadowCastingMode = ShadowCastingMode.Off;
            flashR.enabled = false;
            GameObject lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(muzzle, false);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.4f);
            l.range = 7f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.None;
            l.enabled = false;

            GameObject scopeCamGo = new GameObject("VR Scope Camera");
            scopeCamGo.transform.SetParent(root.transform, false);
            scopeCamGo.transform.localPosition = new Vector3(0f, scopeY, 0.29f);
            Camera sc = scopeCamGo.AddComponent<Camera>();
            sc.nearClipPlane = 0.05f;
            sc.farClipPlane = 900f;
            sc.fieldOfView = 6f;
            sc.depth = -2f;
            sc.stereoTargetEye = StereoTargetEyeMask.None;
            sc.enabled = false;

            FarmBuild.Shadows(m.gameObject, ShadowCastingMode.On);
            objLens.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            lens.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            SniperRifle r = root.AddComponent<SniperRifle>();
            r.model = m;
            r.muzzle = muzzle;
            r.bolt = bolt;
            r.magazine = mag;
            r.scopeEye = eye;
            r.scopeLens = lens.GetComponent<Renderer>();
            r.scopeCamera = sc;
            r.muzzleLight = l;
            r.muzzleFlash = flashR;
            return r;
        }

        // ------------------------------------------------------------------ rat

        /// <summary>Field rat (~0.45 m nose to tail tip), forward +Z, feet at y = 0. Inactive template with a disabled NavMeshAgent.</summary>
        public static RatAI BuildRat(Transform parent)
        {
            GameObject root = new GameObject("Rat Template");
            root.transform.SetParent(parent, false);
            Transform model = FarmBuild.Node(root.transform, "Model", new Vector3(0f, 0f, -0.06f));

            P(model, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.075f, 0.07f), Vector3.zero, new Vector3(0.13f, 0.11f, 0.27f), "RatFur");
            P(model, PrimitiveType.Sphere, "Haunch", new Vector3(0f, 0.072f, -0.03f), Vector3.zero, new Vector3(0.145f, 0.125f, 0.15f), "RatFur");
            P(model, PrimitiveType.Sphere, "Chest", new Vector3(0f, 0.075f, 0.17f), Vector3.zero, new Vector3(0.1f, 0.09f, 0.12f), "RatFur");

            Transform head = FarmBuild.Node(model, "Head", new Vector3(0f, 0.085f, 0.21f));
            P(head, PrimitiveType.Sphere, "Skull", new Vector3(0f, 0f, 0.035f), Vector3.zero, new Vector3(0.085f, 0.075f, 0.11f), "RatFur");
            P(head, PrimitiveType.Sphere, "Snout", new Vector3(0f, -0.012f, 0.095f), new Vector3(8f, 0f, 0f), new Vector3(0.045f, 0.04f, 0.07f), "RatFur");
            P(head, PrimitiveType.Sphere, "Nose", new Vector3(0f, -0.01f, 0.13f), Vector3.zero, new Vector3(0.016f, 0.014f, 0.014f), "RatSkin");
            P(head, PrimitiveType.Sphere, "EarL", new Vector3(-0.034f, 0.04f, 0.005f), new Vector3(0f, -20f, 15f), new Vector3(0.045f, 0.05f, 0.012f), "RatSkin");
            P(head, PrimitiveType.Sphere, "EarR", new Vector3(0.034f, 0.04f, 0.005f), new Vector3(0f, 20f, -15f), new Vector3(0.045f, 0.05f, 0.012f), "RatSkin");
            P(head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.029f, 0.016f, 0.066f), Vector3.zero, Vector3.one * 0.014f, "RatEye");
            P(head, PrimitiveType.Sphere, "EyeR", new Vector3(0.029f, 0.016f, 0.066f), Vector3.zero, Vector3.one * 0.014f, "RatEye");

            Vector3[] legPos = { new Vector3(-0.04f, 0.05f, 0.16f), new Vector3(0.04f, 0.05f, 0.16f), new Vector3(-0.055f, 0.05f, -0.03f), new Vector3(0.055f, 0.05f, -0.03f) };
            Transform[] legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Transform lg = FarmBuild.Node(model, "Leg" + i, legPos[i]);
                P(lg, PrimitiveType.Capsule, "Limb", new Vector3(0f, -0.025f, 0f), Vector3.zero, new Vector3(0.026f, 0.028f, 0.026f), "RatFur");
                P(lg, PrimitiveType.Sphere, "Paw", new Vector3(0f, -0.048f, 0.012f), Vector3.zero, new Vector3(0.024f, 0.01f, 0.038f), "RatSkin");
                legs[i] = lg;
            }

            Transform[] tail = new Transform[7];
            Transform prev = FarmBuild.Node(model, "Tail0", new Vector3(0f, 0.06f, -0.1f));
            tail[0] = prev;
            for (int i = 0; i < tail.Length; i++)
            {
                if (i > 0)
                {
                    prev = FarmBuild.Node(prev, "Tail" + i, new Vector3(0f, 0f, -0.042f));
                    tail[i] = prev;
                }
                float r = Mathf.Lerp(0.015f, 0.005f, i / (float)(tail.Length - 1));
                FarmBuild.RodZ(prev, "Seg", new Vector3(0f, 0f, -0.021f), r, 0.046f, FarmMats.Get("RatSkin"));
            }
            FarmBuild.Shadows(model.gameObject, ShadowCastingMode.On);

            // hit boxes (Enemy layer). Body capsule + a smaller head sphere for headshots.
            GameObject bodyHit = new GameObject("Hitbox_Body");
            bodyHit.transform.SetParent(model, false);
            CapsuleCollider cap = bodyHit.AddComponent<CapsuleCollider>();
            cap.direction = 2;
            cap.center = new Vector3(0f, 0.075f, 0.07f);
            cap.radius = 0.07f;
            cap.height = 0.40f;
            GameObject headHit = new GameObject("Hitbox_Head");
            headHit.transform.SetParent(head, false);
            SphereCollider sph = headHit.AddComponent<SphereCollider>();
            sph.center = new Vector3(0f, -0.004f, 0.06f);
            sph.radius = 0.055f;
            HitZone hz = headHit.AddComponent<HitZone>();
            hz.isHead = true;
            hz.multiplier = 2f;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.12f;
            agent.height = 0.24f;
            agent.baseOffset = 0f;
            agent.enabled = false;

            RatAI rat = root.AddComponent<RatAI>();
            rat.model = model;
            rat.head = head;
            rat.legs = legs;
            rat.tail = tail;
            rat.hitColliders = new Collider[] { cap, sph };
            FarmBuild.SetLayer(root, 9);   // Layers.Enemy
            root.SetActive(false);
            return rat;
        }

        // ------------------------------------------------------------------ farmer

        public static FarmerAnimator BuildFarmer(Transform parent)
        {
            GameObject root = new GameObject("Farmer");
            root.transform.SetParent(parent, false);
            Transform body = FarmBuild.Node(root.transform, "Body", new Vector3(0f, 0.95f, 0f));
            P(body, PrimitiveType.Cube, "Hips", Vector3.zero, Vector3.zero, new Vector3(0.36f, 0.2f, 0.22f), "Denim");

            Transform legL = FarmBuild.Node(body, "LegL", new Vector3(-0.1f, -0.05f, 0f));
            Transform legR = FarmBuild.Node(body, "LegR", new Vector3(0.1f, -0.05f, 0f));
            Transform kneeL = Leg(legL), kneeR = Leg(legR);

            Transform torso = FarmBuild.Node(body, "Torso", new Vector3(0f, 0.08f, 0f));
            P(torso, PrimitiveType.Cube, "Shirt", new Vector3(0f, 0.29f, 0f), Vector3.zero, new Vector3(0.42f, 0.5f, 0.24f), "Flannel");
            P(torso, PrimitiveType.Cube, "Bib", new Vector3(0f, 0.2f, 0.004f), Vector3.zero, new Vector3(0.36f, 0.32f, 0.246f), "Denim");
            P(torso, PrimitiveType.Cube, "StrapL", new Vector3(-0.11f, 0.43f, 0f), Vector3.zero, new Vector3(0.05f, 0.2f, 0.25f), "Denim");
            P(torso, PrimitiveType.Cube, "StrapR", new Vector3(0.11f, 0.43f, 0f), Vector3.zero, new Vector3(0.05f, 0.2f, 0.25f), "Denim");
            P(torso, PrimitiveType.Cube, "Pocket", new Vector3(0f, 0.25f, 0.128f), Vector3.zero, new Vector3(0.14f, 0.1f, 0.01f), "Denim");

            Transform headBone = FarmBuild.Node(torso, "Head", new Vector3(0f, 0.56f, 0f));
            P(headBone, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.11f, 0.05f, 0.11f), "Skin");
            P(headBone, PrimitiveType.Sphere, "Face", new Vector3(0f, 0.17f, 0.01f), Vector3.zero, new Vector3(0.21f, 0.25f, 0.22f), "Skin");
            P(headBone, PrimitiveType.Sphere, "Nose", new Vector3(0f, 0.16f, 0.115f), Vector3.zero, new Vector3(0.04f, 0.05f, 0.05f), "Skin");
            P(headBone, PrimitiveType.Sphere, "Beard", new Vector3(0f, 0.09f, 0.06f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.14f), "Leather");
            P(headBone, PrimitiveType.Cylinder, "HatBrim", new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.5f, 0.008f, 0.5f), "Straw");
            P(headBone, PrimitiveType.Cylinder, "HatCrown", new Vector3(0f, 0.33f, 0f), Vector3.zero, new Vector3(0.25f, 0.065f, 0.25f), "Straw");
            P(headBone, PrimitiveType.Cylinder, "HatBand", new Vector3(0f, 0.29f, 0f), Vector3.zero, new Vector3(0.256f, 0.018f, 0.256f), "HatBand");

            // arms raised forward holding the rifle
            Transform armR = Arm(torso, "ArmR", new Vector3(0.24f, 0.48f, 0f), new Vector3(-70f, -8f, 0f));
            Transform armL = Arm(torso, "ArmL", new Vector3(-0.24f, 0.48f, 0f), new Vector3(-82f, 28f, 0f));

            FarmBuild.Shadows(root, ShadowCastingMode.ShadowsOnly);
            FarmerAnimator fa = root.AddComponent<FarmerAnimator>();
            fa.body = body;
            fa.torso = torso;
            fa.headBone = headBone;
            fa.legL = legL; fa.legR = legR; fa.kneeL = kneeL; fa.kneeR = kneeR;
            fa.armL = armL; fa.armR = armR;
            return fa;
        }

        static Transform Leg(Transform hip)
        {
            P(hip, PrimitiveType.Capsule, "Thigh", new Vector3(0f, -0.22f, 0f), Vector3.zero, new Vector3(0.15f, 0.23f, 0.15f), "Denim");
            Transform knee = FarmBuild.Node(hip, "Knee", new Vector3(0f, -0.44f, 0f));
            P(knee, PrimitiveType.Capsule, "Shin", new Vector3(0f, -0.2f, 0f), Vector3.zero, new Vector3(0.13f, 0.21f, 0.13f), "Denim");
            P(knee, PrimitiveType.Cube, "Boot", new Vector3(0f, -0.42f, 0.04f), Vector3.zero, new Vector3(0.13f, 0.11f, 0.27f), "Leather");
            return knee;
        }

        static Transform Arm(Transform torso, string name, Vector3 shoulder, Vector3 euler)
        {
            Transform a = FarmBuild.Node(torso, name, shoulder, euler);
            P(a, PrimitiveType.Capsule, "Upper", new Vector3(0f, -0.16f, 0f), Vector3.zero, new Vector3(0.12f, 0.18f, 0.12f), "Flannel");
            P(a, PrimitiveType.Capsule, "Fore", new Vector3(0f, -0.42f, 0.03f), new Vector3(-12f, 0f, 0f), new Vector3(0.1f, 0.16f, 0.1f), "Flannel");
            P(a, PrimitiveType.Sphere, "Hand", new Vector3(0f, -0.59f, 0.06f), Vector3.zero, new Vector3(0.09f, 0.1f, 0.09f), "Skin");
            return a;
        }

        /// <summary>Simple controller shell for a VR hand.</summary>
        public static void BuildController(Transform hand, bool left)
        {
            P(hand, PrimitiveType.Cube, "Shell", new Vector3(0f, -0.02f, 0.02f), Vector3.zero, new Vector3(0.05f, 0.04f, 0.12f), left ? "GunSteel" : "Rubber");
            P(hand, PrimitiveType.Sphere, "Ring", new Vector3(0f, 0.01f, 0.07f), Vector3.zero, new Vector3(0.06f, 0.02f, 0.06f), "Metal");
            FarmBuild.Shadows(hand.gameObject, ShadowCastingMode.Off, false);
        }
    }
}
