using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Builds the first-person cartoon rifle ("Farm Popper"): brass receiver, blued barrel with a muzzle crown, wooden
    /// stock / grip / forend, brass bands, glowing orange front bead + notch rear sight (used for aim-down-sights),
    /// box magazine (animated on reload), bolt handle, trigger + guard, leather sling, gloved hands and flannel sleeves,
    /// a cartoon star muzzle flash with a light and an ejection port. Returns the CartoonGun component.
    /// </summary>
    public static class GunFactory
    {
        public const float SightHeight = 0.053f;

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3? scale = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // first-person model: no self shadows on the world
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            return go;
        }

        static Mesh Mk(MeshKit k, string n) { return RatGen.Mesh(k, "RS_Gun_" + n); }

        public static CartoonGun Build(Transform cameraTransform, Material flashMat)
        {
            Color w = Color.white;
            Texture2D woodTex = CartoonTex.Planks("RS_GunWoodTex", new Color(0.86f, 0.5f, 0.24f), new Color(0.55f, 0.28f, 0.12f), 2, 7);
            Material wood = RatMats.Toon("RS_GunWood", Color.white, new RatMats.ToonOpts { tex = woodTex, tiling = new Vector2(4f, 4f), spec = 0.35f, gloss = 40f, rim = 0.35f, rampSmooth = 0.3f, shadeTint = new Color(0.7f, 0.62f, 0.75f) });
            Material brass = RatMats.Toon("RS_GunBrass", new Color(0.9f, 0.62f, 0.2f), new RatMats.ToonOpts { spec = 0.75f, gloss = 55f, rim = 0.45f, rampSmooth = 0.25f, shadeTint = new Color(0.75f, 0.55f, 0.5f) });
            Material steel = RatMats.Toon("RS_GunSteel", new Color(0.2f, 0.23f, 0.31f), new RatMats.ToonOpts { spec = 0.85f, gloss = 70f, rim = 0.5f, rampSmooth = 0.25f });
            Material dark = RatMats.Toon("RS_GunDark", new Color(0.13f, 0.12f, 0.14f), new RatMats.ToonOpts { spec = 0.3f, gloss = 30f, rim = 0.25f });
            Material bead = RatMats.Toon("RS_GunBead", new Color(1f, 0.45f, 0.1f), new RatMats.ToonOpts { spec = 0.6f, emission = new Color(0.9f, 0.35f, 0.05f), rim = 0.2f });
            Material leather = RatMats.Toon("RS_GunSling", new Color(0.5f, 0.3f, 0.18f), new RatMats.ToonOpts { spec = 0.2f, rim = 0.3f });
            Material glove = RatMats.Toon("RS_Glove", new Color(0.86f, 0.64f, 0.4f), new RatMats.ToonOpts { tex = CartoonTex.Burlap(), tiling = new Vector2(2f, 2f), spec = 0.15f, gloss = 25f, rim = 0.4f, rampSmooth = 0.32f });
            Material cuff = RatMats.Toon("RS_GloveCuff", new Color(0.6f, 0.4f, 0.24f), new RatMats.ToonOpts { spec = 0.15f, rim = 0.3f });
            Material sleeve = RatMats.Toon("RS_Sleeve", Color.white, new RatMats.ToonOpts { tex = CartoonTex.Plaid(new Color(0.85f, 0.2f, 0.18f), new Color(0.25f, 0.08f, 0.1f)), tiling = new Vector2(2f, 3f), spec = 0.05f, rim = 0.35f, rampSmooth = 0.35f });

            GameObject root = new GameObject("CartoonRifle");
            root.transform.SetParent(cameraTransform, false);
            Transform model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            // ---- receiver
            Part(model, "Receiver", Mk(Shapes.RoundedBox(new Vector3(0.07f, 0.085f, 0.24f), 0.02f, w), "Receiver"), steel, new Vector3(0f, 0f, 0.06f), Vector3.zero);
            Part(model, "ReceiverTop", Mk(Shapes.RoundedBox(new Vector3(0.05f, 0.012f, 0.2f), 0.005f, w), "ReceiverTop"), dark, new Vector3(0f, 0.044f, 0.06f), Vector3.zero);
            Part(model, "SidePlateL", Mk(Shapes.RoundedBox(new Vector3(0.006f, 0.05f, 0.15f), 0.003f, w), "SidePlate"), brass, new Vector3(-0.036f, -0.008f, 0.05f), Vector3.zero);
            Part(model, "SidePlateR", Mk(Shapes.RoundedBox(new Vector3(0.006f, 0.05f, 0.08f), 0.003f, w), "SidePlateShort"), brass, new Vector3(0.036f, -0.012f, -0.005f), Vector3.zero);
            Mesh screw = Mk(Shapes.Sphere(0.0065f, 8, 6, w), "Screw");
            for (int s = -1; s <= 1; s += 2)
            {
                Part(model, "Screw", screw, brass, new Vector3(0.037f * s, -0.015f, -0.03f), Vector3.zero);
                Part(model, "Screw", screw, brass, new Vector3(0.037f * s, -0.015f, 0.15f), Vector3.zero);
            }
            Part(model, "EjectionPort", Mk(Shapes.RoundedBox(new Vector3(0.006f, 0.026f, 0.065f), 0.003f, w), "Port"), dark, new Vector3(0.034f, 0.014f, 0.085f), Vector3.zero);

            // ---- barrel
            List<Vector2> bp = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.02f, 0f), new Vector2(0.0195f, 0.1f), new Vector2(0.018f, 0.38f),
                new Vector2(0.021f, 0.39f), new Vector2(0.025f, 0.405f), new Vector2(0.025f, 0.43f), new Vector2(0.022f, 0.445f),
                new Vector2(0.012f, 0.447f), new Vector2(0.009f, 0.44f), new Vector2(0f, 0.44f)
            };
            Part(model, "Barrel", Mk(Shapes.Lathe(bp, 20, w), "Barrel"), steel, new Vector3(0f, 0.012f, 0.17f), new Vector3(90f, 0f, 0f));
            Part(model, "Bore", Mk(Shapes.Disc(0.0085f, 12, w, w), "Bore"), dark, new Vector3(0f, 0.012f, 0.6095f), new Vector3(90f, 0f, 0f));

            // ---- forend + bands
            Part(model, "Forend", Mk(Shapes.RoundedBox(new Vector3(0.062f, 0.055f, 0.25f), 0.024f, w, 4f), "Forend"), wood, new Vector3(0f, -0.022f, 0.31f), Vector3.zero);
            Mesh band = Mk(Shapes.Torus(0.036f, 0.0065f, 24, 8, w), "Band");
            Part(model, "Band1", band, brass, new Vector3(0f, -0.008f, 0.255f), new Vector3(90f, 0f, 0f), new Vector3(1f, 1f, 1.25f));
            Part(model, "Band2", band, brass, new Vector3(0f, -0.006f, 0.43f), new Vector3(90f, 0f, 0f), new Vector3(0.95f, 1f, 1.15f));

            // ---- sights
            Part(model, "FrontSightBase", Mk(Shapes.RoundedBox(new Vector3(0.016f, 0.02f, 0.03f), 0.006f, w), "FSBase"), steel, new Vector3(0f, 0.032f, 0.585f), Vector3.zero);
            Part(model, "FrontSightBlade", Mk(Shapes.RoundedBox(new Vector3(0.006f, 0.02f, 0.012f), 0.002f, w), "FSBlade"), steel, new Vector3(0f, 0.042f, 0.588f), Vector3.zero);
            Part(model, "FrontBead", Mk(Shapes.Sphere(0.0062f, 12, 8, w), "Bead"), bead, new Vector3(0f, SightHeight, 0.588f), Vector3.zero);
            Mesh post = Mk(Shapes.RoundedBox(new Vector3(0.011f, 0.024f, 0.018f), 0.004f, w), "RearPost");
            Part(model, "RearSightBase", Mk(Shapes.RoundedBox(new Vector3(0.042f, 0.012f, 0.022f), 0.004f, w), "RSBase"), steel, new Vector3(0f, 0.052f - 0.012f, 0.125f), Vector3.zero);
            Part(model, "RearPostL", post, steel, new Vector3(-0.0125f, 0.052f, 0.125f), Vector3.zero);
            Part(model, "RearPostR", post, steel, new Vector3(0.0125f, 0.052f, 0.125f), Vector3.zero);

            // ---- grip, stock, butt plate
            Part(model, "Grip", Mk(Shapes.RoundedBox(new Vector3(0.05f, 0.11f, 0.085f), 0.022f, w, 4f), "Grip"), wood, new Vector3(0f, -0.05f, -0.085f), new Vector3(-22f, 0f, 0f));
            Part(model, "Stock", Mk(Shapes.RoundedBox(new Vector3(0.056f, 0.085f, 0.3f), 0.026f, w, 4f), "Stock"), wood, new Vector3(0f, -0.045f, -0.27f), new Vector3(7f, 0f, 0f));
            Part(model, "Butt", Mk(Shapes.RoundedBox(new Vector3(0.06f, 0.13f, 0.09f), 0.028f, w, 4f), "Butt"), wood, new Vector3(0f, -0.08f, -0.39f), new Vector3(4f, 0f, 0f));
            Part(model, "ButtPlate", Mk(Shapes.RoundedBox(new Vector3(0.063f, 0.135f, 0.02f), 0.009f, w), "ButtPlate"), dark, new Vector3(0f, -0.082f, -0.44f), new Vector3(4f, 0f, 0f));

            // ---- trigger + guard
            Part(model, "TriggerGuard", Mk(Shapes.Torus(0.03f, 0.0055f, 24, 8, w), "Guard"), brass, new Vector3(0f, -0.062f, 0.0f), new Vector3(0f, 0f, 90f), new Vector3(1f, 1f, 1.25f));
            Part(model, "Trigger", Mk(Shapes.RoundedBox(new Vector3(0.008f, 0.032f, 0.01f), 0.004f, w), "Trigger"), steel, new Vector3(0f, -0.058f, 0.004f), new Vector3(18f, 0f, 0f));

            // ---- magazine (animated)
            Transform mag = new GameObject("Magazine").transform;
            mag.SetParent(model, false);
            mag.localPosition = new Vector3(0f, -0.04f, 0.095f);
            mag.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Part(mag, "MagBody", Mk(Shapes.RoundedBox(new Vector3(0.048f, 0.105f, 0.07f), 0.012f, w), "Mag"), steel, new Vector3(0f, -0.05f, 0f), Vector3.zero);
            Part(mag, "MagPlate", Mk(Shapes.RoundedBox(new Vector3(0.054f, 0.014f, 0.078f), 0.006f, w), "MagPlate"), brass, new Vector3(0f, -0.103f, 0f), Vector3.zero);

            // ---- bolt handle (animated)
            Transform bolt = new GameObject("BoltHandle").transform;
            bolt.SetParent(model, false);
            bolt.localPosition = new Vector3(0.034f, 0.022f, 0.02f);
            Part(bolt, "Stem", Mk(Shapes.RoundedCylinder(0.007f, 0.05f, 0.003f, 10, w), "BoltStem"), steel, Vector3.zero, new Vector3(0f, 0f, -90f));
            Part(bolt, "Knob", Mk(Shapes.Sphere(0.016f, 14, 10, w), "BoltKnob"), brass, new Vector3(0.055f, 0f, 0f), Vector3.zero);

            // ---- sling
            List<Vector3> sl = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float f = i / 14f;
                Vector3 a = new Vector3(0.0f, -0.05f, 0.42f), b = new Vector3(0.0f, -0.13f, -0.36f);
                sl.Add(Vector3.Lerp(a, b, f) + new Vector3(-0.012f * Mathf.Sin(f * Mathf.PI), -0.11f * Mathf.Sin(f * Mathf.PI), 0f));
            }
            Part(model, "Sling", Mk(Shapes.Tube(sl, 0.006f, 0.006f, 6, w), "Sling"), leather, Vector3.zero, Vector3.zero, new Vector3(1.6f, 1f, 1f));

            // ---- hands
            BuildRightHand(model, glove, cuff, sleeve);
            BuildLeftHand(model, glove, cuff, sleeve);

            // ---- muzzle, flash, eject port
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.012f, 0.625f);
            GameObject flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(muzzle, false);
            Color hot = new Color(1f, 1f, 0.85f, 1f), tip = new Color(1f, 0.55f, 0.1f, 0f);
            GameObject starA = Part(flash.transform, "FlashStar", Mk(Shapes.Star(8, 0.11f, 0.045f, hot, tip), "FlashStar"), flashMat, new Vector3(0f, 0f, 0.02f), Vector3.zero);
            MeshKit side = new MeshKit();
            MeshKit cone = Shapes.Star(4, 0.03f, 0.012f, hot, tip);
            side.Append(Shapes.Quad(0.06f, 0.2f, hot), new Vector3(0f, 0f, 0.1f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            side.Append(Shapes.Quad(0.06f, 0.2f, hot), new Vector3(0f, 0f, 0.1f), Quaternion.Euler(90f, 90f, 0f), Vector3.one);
            side.Append(cone, Vector3.zero, Quaternion.identity, Vector3.one);
            Part(flash.transform, "FlashSide", Mk(side, "FlashSide"), flashMat, Vector3.zero, Vector3.zero);
            starA.transform.localScale = Vector3.one;
            flash.SetActive(false);
            Light fl = new GameObject("FlashLight").AddComponent<Light>();
            fl.transform.SetParent(muzzle, false);
            fl.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            fl.type = LightType.Point;
            fl.color = new Color(1f, 0.75f, 0.4f);
            fl.range = 7f;
            fl.intensity = 0f;
            fl.shadows = LightShadows.None;
            fl.enabled = false;

            Transform eject = new GameObject("EjectPort").transform;
            eject.SetParent(model, false);
            eject.localPosition = new Vector3(0.045f, 0.02f, 0.085f);

            CartoonGun gun = root.AddComponent<CartoonGun>();
            gun.model = model;
            gun.muzzle = muzzle;
            gun.muzzleFlash = flash;
            gun.flashLight = fl;
            gun.magazine = mag;
            gun.boltHandle = bolt;
            gun.ejectPort = eject;
            gun.adsPosition = new Vector3(0f, -SightHeight, 0.34f);
            return gun;
        }

        static void BuildRightHand(Transform model, Material glove, Material cuff, Material sleeve)
        {
            Color w = Color.white;
            Transform hand = new GameObject("RightHand").transform;
            hand.SetParent(model, false);
            hand.localPosition = new Vector3(0.012f, -0.05f, -0.09f);
            Part(hand, "Palm", Mk(Shapes.Blob(new Vector3(0.048f, 0.06f, 0.058f), 3, 0.06f, w), "RPalm"), glove, new Vector3(0.028f, -0.005f, -0.01f), new Vector3(-20f, 0f, 0f));
            Mesh finger = Mk(Shapes.Ellipsoid(new Vector3(0.017f, 0.017f, 0.034f), 10, 8, w), "Finger");
            for (int i = 0; i < 3; i++)
                Part(hand, "Finger" + i, finger, glove, new Vector3(-0.012f, -0.03f - i * 0.026f, 0.03f - i * 0.006f), new Vector3(-10f, 70f, 0f));
            Part(hand, "Thumb", Mk(Shapes.Ellipsoid(new Vector3(0.018f, 0.018f, 0.042f), 10, 8, w), "Thumb"), glove, new Vector3(-0.012f, 0.03f, 0.0f), new Vector3(10f, -25f, 0f));
            List<Vector3> idx = new List<Vector3> { new Vector3(0.03f, 0.0f, 0.02f), new Vector3(0.02f, -0.005f, 0.06f), new Vector3(0.006f, -0.012f, 0.088f) };
            Part(hand, "Index", Mk(Shapes.Tube(idx, 0.016f, 0.014f, 8, w), "RIndex"), glove, Vector3.zero, Vector3.zero);
            Part(hand, "Cuff", Mk(Shapes.RoundedCylinder(0.05f, 0.05f, 0.015f, 18, w), "Cuff"), cuff, new Vector3(0.045f, -0.03f, -0.07f), new Vector3(-70f, -15f, 0f));
            List<Vector3> arm = new List<Vector3> { new Vector3(0.05f, -0.04f, -0.09f), new Vector3(0.1f, -0.12f, -0.2f), new Vector3(0.2f, -0.3f, -0.36f) };
            Part(hand, "Sleeve", Mk(Shapes.Tube(arm, 0.052f, 0.07f, 16, w), "RSleeve"), sleeve, Vector3.zero, Vector3.zero);
        }

        static void BuildLeftHand(Transform model, Material glove, Material cuff, Material sleeve)
        {
            Color w = Color.white;
            Transform hand = new GameObject("LeftHand").transform;
            hand.SetParent(model, false);
            hand.localPosition = new Vector3(0f, -0.055f, 0.33f);
            Part(hand, "Palm", Mk(Shapes.Blob(new Vector3(0.05f, 0.034f, 0.065f), 5, 0.06f, w), "LPalm"), glove, new Vector3(0.0f, -0.012f, 0f), Vector3.zero);
            Mesh finger = Mk(Shapes.Ellipsoid(new Vector3(0.016f, 0.03f, 0.016f), 10, 8, w), "LFinger");
            for (int i = 0; i < 4; i++)
                Part(hand, "Finger" + i, finger, glove, new Vector3(-0.036f, 0.022f, -0.035f + i * 0.023f), new Vector3(0f, 0f, -15f));
            Part(hand, "Thumb", Mk(Shapes.Ellipsoid(new Vector3(0.017f, 0.017f, 0.04f), 10, 8, w), "LThumb"), glove, new Vector3(0.036f, 0.018f, -0.01f), new Vector3(-10f, 15f, 0f));
            Part(hand, "Cuff", Mk(Shapes.RoundedCylinder(0.048f, 0.05f, 0.015f, 18, w), "LCuff"), cuff, new Vector3(-0.012f, -0.035f, -0.075f), new Vector3(-75f, 25f, 0f));
            List<Vector3> arm = new List<Vector3> { new Vector3(-0.015f, -0.045f, -0.09f), new Vector3(-0.08f, -0.12f, -0.22f), new Vector3(-0.22f, -0.28f, -0.42f) };
            Part(hand, "Sleeve", Mk(Shapes.Tube(arm, 0.05f, 0.068f, 16, w), "LSleeve"), sleeve, Vector3.zero, Vector3.zero);
        }
    }
}
