using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Builds a complete cartoon mouse: rounded body and belly, big head with a tapered snout, pink nose, glossy eyes
    /// with highlights, large round ears, whiskers, buck teeth, front paws, back legs, a 9-segment tail, hidden X-eyes
    /// and dizzy stars for the death animation, hitboxes (body + head), NavMeshAgent, MouseRig and MouseAI.
    /// Meshes are shared between variants; each variant gets its own fur colours. Used by the scene builder to make
    /// the mouse prefabs, and at runtime as a fallback if a prefab is missing.
    /// </summary>
    public static class MouseFactory
    {
        /// <summary>All mice are drawn larger than life (cartoon proportions, readable at 30 m).</summary>
        public const float BaseScale = 1.4f;

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Texture2D furTex;

        public static void ClearCache() { meshes.Clear(); furTex = null; }

        static Mesh M(string key, System.Func<MeshKit> build)
        {
            Mesh m;
            if (meshes.TryGetValue(key, out m) && m != null) return m;
            m = RatGen.Mesh(build(), "RS_Mouse_" + key);
            meshes[key] = m;
            return m;
        }

        public class Mats
        {
            public Material fur, belly, pink, eye, white, dark, star;
        }

        public static Mats MaterialsFor(MouseVariant v)
        {
            if (furTex == null) furTex = CartoonTex.Fur();
            Mats m = new Mats();
            m.fur = RatMats.Toon("RS_MouseFur_" + v.id, v.furColor, new RatMats.ToonOpts { tex = furTex, tiling = new Vector2(3f, 3f), spec = 0.12f, gloss = 22f, rim = 0.45f, rampSmooth = 0.3f });
            m.belly = RatMats.Toon("RS_MouseBelly_" + v.id, v.bellyColor, new RatMats.ToonOpts { tex = furTex, tiling = new Vector2(3f, 3f), spec = 0.08f, gloss = 20f, rim = 0.35f, rampSmooth = 0.3f });
            m.pink = RatMats.Toon("RS_MousePink_" + v.id, v.pinkColor, new RatMats.ToonOpts { spec = 0.3f, gloss = 50f, rim = 0.35f, rampSmooth = 0.3f });
            m.eye = RatMats.Toon("RS_MouseEye_" + v.id, v.eyeColor, new RatMats.ToonOpts { spec = 0.9f, gloss = 120f, rim = 0.2f });
            m.white = RatMats.Toon("RS_MouseWhite", Color.white, new RatMats.ToonOpts { spec = 0.2f, rim = 0.1f, emission = new Color(0.35f, 0.35f, 0.35f) });
            m.dark = RatMats.Toon("RS_MouseDark", new Color(0.12f, 0.1f, 0.12f), new RatMats.ToonOpts { spec = 0.1f, rim = 0.1f });
            m.star = RatMats.Toon("RS_DizzyStar", new Color(1f, 0.86f, 0.2f), new RatMats.ToonOpts { twoSided = true, flipBackNormals = false, emission = new Color(0.55f, 0.42f, 0.05f), rim = 0f });
            return m;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, bool shadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.layer = parent.gameObject.layer;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            return go;
        }

        static Transform Node(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            return Part(parent, name, null, null, pos, euler).transform;
        }

        public static GameObject Build(MouseVariant v, Mats mats = null)
        {
            if (mats == null) mats = MaterialsFor(v);
            Color w = Color.white;

            Mesh body = M("Body", () => Shapes.Ellipsoid(new Vector3(0.15f, 0.14f, 0.25f), 22, 14, w, 0.22f));
            Mesh belly = M("Belly", () => Shapes.Ellipsoid(new Vector3(0.125f, 0.11f, 0.2f), 18, 12, w, 0.2f));
            Mesh head = M("Head", () => Shapes.Ellipsoid(new Vector3(0.125f, 0.115f, 0.13f), 20, 14, w, 0.1f));
            Mesh snout = M("Snout", () => Shapes.Ellipsoid(new Vector3(0.08f, 0.07f, 0.115f), 18, 12, w, 0.5f));
            Mesh nose = M("Nose", () => Shapes.Ellipsoid(new Vector3(0.036f, 0.03f, 0.03f), 14, 10, w));
            Mesh eye = M("Eye", () => Shapes.Ellipsoid(new Vector3(0.034f, 0.045f, 0.028f), 14, 10, w));
            Mesh glint = M("Glint", () => Shapes.Sphere(0.012f, 8, 6, w));
            Mesh ear = M("Ear", () => Shapes.Ellipsoid(new Vector3(0.09f, 0.088f, 0.022f), 20, 12, w));
            Mesh earIn = M("EarInner", () => Shapes.Ellipsoid(new Vector3(0.064f, 0.064f, 0.012f), 16, 10, w));
            Mesh leg = M("FrontLeg", () => Shapes.Ellipsoid(new Vector3(0.03f, 0.065f, 0.032f), 10, 8, w));
            Mesh paw = M("Paw", () => Shapes.Ellipsoid(new Vector3(0.038f, 0.022f, 0.045f), 10, 8, w));
            Mesh thigh = M("Thigh", () => Shapes.Ellipsoid(new Vector3(0.065f, 0.075f, 0.085f), 12, 10, w));
            Mesh foot = M("Foot", () => Shapes.Ellipsoid(new Vector3(0.042f, 0.022f, 0.09f), 12, 8, w, 0.25f));
            Mesh whisk = M("Whiskers", () => WhiskerMesh());
            Mesh teeth = M("Teeth", () =>
            {
                MeshKit k = new MeshKit();
                MeshKit tooth = Shapes.RoundedBox(new Vector3(0.019f, 0.03f, 0.01f), 0.004f, w);
                k.Append(tooth, new Vector3(-0.0105f, 0f, 0f), Quaternion.identity, Vector3.one);
                k.Append(tooth, new Vector3(0.0105f, 0f, 0f), Quaternion.identity, Vector3.one);
                return k;
            });
            Mesh xbar = M("XBar", () => Shapes.RoundedBox(new Vector3(0.075f, 0.017f, 0.014f), 0.006f, w));
            Mesh star = M("Star", () => Shapes.Star(5, 0.055f, 0.024f, w, w));
            const int tailSegs = 12;
            const float segLen = 0.045f;

            GameObject root = new GameObject("CartoonMouse_" + v.id);
            root.layer = Layers.Enemy;
            Transform visual = Node(root.transform, "Visual", Vector3.zero, Vector3.zero);
            float sc = v.scale * BaseScale;
            visual.localScale = Vector3.one * sc;

            Transform bodyPivot = Node(visual, "BodyPivot", new Vector3(0f, 0.1f, -0.15f), Vector3.zero);
            GameObject bodyGo = Part(bodyPivot, "Body", body, mats.fur, new Vector3(0f, 0.07f, 0.15f), Vector3.zero);
            Part(bodyGo.transform, "Belly", belly, mats.belly, new Vector3(0f, -0.035f, 0.03f), Vector3.zero);

            Transform headPivot = Node(bodyPivot, "HeadPivot", new Vector3(0f, 0.12f, 0.34f), Vector3.zero);
            Part(headPivot, "Head", head, mats.fur, new Vector3(0f, 0.035f, 0.08f), Vector3.zero);
            Transform snoutT = Node(headPivot, "Snout", new Vector3(0f, 0f, 0.16f), new Vector3(6f, 0f, 0f));
            Part(snoutT, "SnoutMesh", snout, mats.fur, new Vector3(0f, 0f, 0.04f), Vector3.zero);
            Part(snoutT, "Nose", nose, mats.pink, new Vector3(0f, 0.018f, 0.152f), Vector3.zero);
            Transform whiskT = Node(snoutT, "WhiskerPivot", new Vector3(0f, -0.005f, 0.1f), Vector3.zero);
            Part(whiskT, "Whiskers", whisk, mats.dark, Vector3.zero, Vector3.zero, false);
            Part(snoutT, "Teeth", teeth, mats.white, new Vector3(0f, -0.05f, 0.118f), new Vector3(-8f, 0f, 0f), false);

            Transform eyeL = Part(headPivot, "EyeL", eye, mats.eye, new Vector3(-0.06f, 0.075f, 0.165f), new Vector3(0f, -18f, 0f)).transform;
            Transform eyeR = Part(headPivot, "EyeR", eye, mats.eye, new Vector3(0.06f, 0.075f, 0.165f), new Vector3(0f, 18f, 0f)).transform;
            Part(eyeL, "Glint", glint, mats.white, new Vector3(0.008f, 0.02f, 0.021f), Vector3.zero, false);
            Part(eyeR, "Glint", glint, mats.white, new Vector3(0.008f, 0.02f, 0.021f), Vector3.zero, false);

            Transform earL = Node(headPivot, "EarL", new Vector3(-0.085f, 0.13f, 0.03f), new Vector3(0f, -18f, 28f));
            Part(earL, "Ear", ear, mats.fur, new Vector3(0f, 0.065f, 0f), Vector3.zero);
            Part(earL, "EarInner", earIn, mats.pink, new Vector3(0f, 0.066f, 0.014f), Vector3.zero, false);
            Transform earR = Node(headPivot, "EarR", new Vector3(0.085f, 0.13f, 0.03f), new Vector3(0f, 18f, -28f));
            Part(earR, "Ear", ear, mats.fur, new Vector3(0f, 0.065f, 0f), Vector3.zero);
            Part(earR, "EarInner", earIn, mats.pink, new Vector3(0f, 0.066f, 0.014f), Vector3.zero, false);

            GameObject xEyes = Node(headPivot, "XEyes", Vector3.zero, Vector3.zero).gameObject;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = new Vector3(0.06f * s, 0.075f, 0.185f);
                Part(xEyes.transform, "X1", xbar, mats.dark, c, new Vector3(0f, 18f * s, 45f), false);
                Part(xEyes.transform, "X2", xbar, mats.dark, c, new Vector3(0f, 18f * s, -45f), false);
            }
            xEyes.SetActive(false);

            Transform legFL = Node(bodyPivot, "LegFL", new Vector3(-0.075f, 0.03f, 0.27f), Vector3.zero);
            Part(legFL, "Leg", leg, mats.fur, new Vector3(0f, -0.055f, 0f), Vector3.zero);
            Part(legFL, "Paw", paw, mats.pink, new Vector3(0f, -0.112f, 0.02f), Vector3.zero);
            Transform legFR = Node(bodyPivot, "LegFR", new Vector3(0.075f, 0.03f, 0.27f), Vector3.zero);
            Part(legFR, "Leg", leg, mats.fur, new Vector3(0f, -0.055f, 0f), Vector3.zero);
            Part(legFR, "Paw", paw, mats.pink, new Vector3(0f, -0.112f, 0.02f), Vector3.zero);

            Transform legBL = Node(visual, "LegBL", new Vector3(-0.1f, 0.1f, -0.11f), Vector3.zero);
            Part(legBL, "Thigh", thigh, mats.fur, Vector3.zero, Vector3.zero);
            Part(legBL, "Foot", foot, mats.pink, new Vector3(0f, -0.08f, 0.045f), Vector3.zero);
            Transform legBR = Node(visual, "LegBR", new Vector3(0.1f, 0.1f, -0.11f), Vector3.zero);
            Part(legBR, "Thigh", thigh, mats.fur, Vector3.zero, Vector3.zero);
            Part(legBR, "Foot", foot, mats.pink, new Vector3(0f, -0.08f, 0.045f), Vector3.zero);

            // tail: chain of overlapping segments pointing backwards, gently curving up then down
            Transform tailRoot = Node(bodyPivot, "TailRoot", new Vector3(0f, 0.04f, -0.08f), new Vector3(0f, 180f, 0f));
            Transform[] tail = new Transform[tailSegs];
            Transform prev = tailRoot;
            for (int i = 0; i < tailSegs; i++)
            {
                float k = (float)i / (tailSegs - 1);
                float r = Mathf.Lerp(0.023f, 0.006f, k);
                Mesh seg = M("Tail" + i, () => Shapes.Ellipsoid(new Vector3(r, r, segLen * 0.95f), 10, 8, w));
                float restPitch = i == 0 ? -22f : (i < 4 ? 3f : 5.5f);
                Transform t = Node(prev, "Tail" + i, new Vector3(0f, 0f, i == 0 ? 0f : segLen), new Vector3(restPitch, 0f, 0f));
                Part(t, "Seg", seg, mats.pink, new Vector3(0f, 0f, segLen * 0.5f), Vector3.zero, i < 6);
                tail[i] = t;
                prev = t;
            }

            // dizzy stars (shown during the death)
            Transform stars = Node(root.transform, "DizzyStars", new Vector3(0f, 0.42f * sc, 0.28f * sc), Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Part(stars, "Star" + i, star, mats.star, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.14f * sc, new Vector3(0f, -i * 120f + 90f, 0f), false)
                    .transform.localScale = Vector3.one * sc;
            }
            stars.gameObject.SetActive(false);

            // hitboxes (a little generous - it's a cartoon)
            GameObject hb = Node(bodyPivot, "HitBody", Vector3.zero, Vector3.zero).gameObject;
            hb.layer = Layers.Enemy;
            CapsuleCollider cap = hb.AddComponent<CapsuleCollider>();
            cap.direction = 2;
            cap.center = new Vector3(0f, 0.07f, 0.15f);
            cap.radius = 0.17f;
            cap.height = 0.64f;
            GameObject hh = Node(headPivot, "HitHead", Vector3.zero, Vector3.zero).gameObject;
            hh.layer = Layers.EnemyHead;
            SphereCollider sph = hh.AddComponent<SphereCollider>();
            sph.center = new Vector3(0f, 0.04f, 0.12f);
            sph.radius = 0.16f;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.13f * sc;
            agent.height = 0.3f * sc;
            agent.baseOffset = 0f;
            agent.speed = v.runSpeed;
            agent.acceleration = 38f;
            agent.angularSpeed = 900f;
            agent.stoppingDistance = 0.05f;

            MouseRig rig = root.AddComponent<MouseRig>();
            rig.visual = visual;
            rig.bodyPivot = bodyPivot;
            rig.body = bodyGo.transform;
            rig.headPivot = headPivot;
            rig.snout = snoutT;
            rig.whiskers = whiskT;
            rig.earL = earL;
            rig.earR = earR;
            rig.eyeL = eyeL;
            rig.eyeR = eyeR;
            rig.legFL = legFL;
            rig.legFR = legFR;
            rig.legBL = legBL;
            rig.legBR = legBR;
            rig.tail = tail;
            rig.xEyes = xEyes;
            rig.starsPivot = stars;

            MouseAI ai = root.AddComponent<MouseAI>();
            ai.variant = v;
            ai.head = headPivot;
            return root;
        }

        static MeshKit WhiskerMesh()
        {
            MeshKit k = new MeshKit();
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float up = (i - 1) * 0.022f;
                    List<Vector3> path = new List<Vector3>();
                    for (int j = 0; j <= 4; j++)
                    {
                        float f = j / 4f;
                        path.Add(new Vector3(s * (0.03f + f * 0.17f), up + (i - 1) * 0.03f * f - 0.012f * f * f, -0.02f - f * 0.05f));
                    }
                    k.Append(Shapes.Tube(path, 0.0035f, 0.0012f, 4, Color.white, false));
                }
            }
            return k;
        }
    }
}
