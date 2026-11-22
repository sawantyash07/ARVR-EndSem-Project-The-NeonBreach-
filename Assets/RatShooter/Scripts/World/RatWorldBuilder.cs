using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Generates the complete Rat Shooter level in the active scene: a sunny cartoon farmyard (flat yard with a dirt
    /// path, rolling hills, farmhouse porch the player shoots from, fences, red barn, silo, windpump, trees, vegetable
    /// garden with a scarecrow, corn and pumpkin patches, hay bales, barrels, crates, trough, wheelbarrow, sacks, log
    /// pile, well, bushes, rocks, sunflowers, mouse burrows and bait food, drifting clouds and butterflies), the lighting,
    /// the player camera + cartoon rifle and every gameplay object (game manager, HUD, audio, effects, spawner).
    /// Called by the editor scene builder (Neon Breach > Rat Shooter); everything it creates is recorded in RatGen.
    /// </summary>
    public static class RatWorldBuilder
    {
        public class Result
        {
            public GameObject gameRoot;
            public RatShooterGame game;
            public MouseSpawner spawner;
            public Camera cam;
            public CartoonGun gun;
            public Light sun;
            public Material skybox;
        }

        // yard + path layout (shared with the grass planter)
        public const float YardHalfX = 19f;
        public const float YardBackZ = 34f;
        static readonly Vector2[] MainPath =
        {
            new Vector2(0f, 0.4f), new Vector2(0.4f, 5f), new Vector2(-1f, 10f), new Vector2(-2.5f, 15f), new Vector2(-4f, 20f),
            new Vector2(-5.5f, 25f), new Vector2(-6.8f, 30f), new Vector2(-8.2f, 34f), new Vector2(-9f, 38f)
        };
        static readonly Vector2[] BranchPath = { new Vector2(-1.6f, 12.5f), new Vector2(3f, 14.5f), new Vector2(8f, 16.2f), new Vector2(13f, 17.6f), new Vector2(19.5f, 18.6f) };
        static readonly Rect Garden = new Rect(-16f, 8f, 9f, 9f);
        static readonly Rect CornPatch = new Rect(7.5f, 22f, 8.5f, 9.5f);

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static Transform envRoot, propsRoot;
        static GrassPlanter planter;

        // materials
        static Material mGround, mVeg, mWood, mWoodDark, mFence, mBarnRed, mTrim, mSiding, mRoof, mRoofDark, mHay, mStone, mMetal, mSilo,
                        mLeaves, mBark, mSoil, mPumpkin, mBurlap, mCheese, mWater, mBurrow, mCloud, mDirt, mPlaid, mRed, mBlueTrim,
                        mGlass, mLeafFlat, mDark, mHills, mWhite;

        // ------------------------------------------------------------------ entry

        public static Result Build()
        {
            meshCache.Clear();
            RatMats.ClearCache();
            MouseFactory.ClearCache();
            Result res = new Result();

            BuildMaterials();
            envRoot = new GameObject("Environment").transform;
            propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(envRoot, false);
            GameObject grassGo = new GameObject("GrassAndFlowers");
            grassGo.transform.SetParent(envRoot, false);
            planter = grassGo.AddComponent<GrassPlanter>();

            res.sun = BuildLighting(out res.skybox);
            BuildGround();
            BuildPorchAndHouse();
            BuildFences();
            BuildBarn(new Vector3(-9f, 0f, 43f));
            BuildSilo(new Vector3(4.5f, 0f, 45f));
            BuildWindpump(new Vector3(15.5f, 0f, 41f));
            BuildTrees();
            BuildGarden();
            BuildCorn();
            BuildPumpkins();
            BuildProps();
            BuildBurrowsAndFood();
            BuildClouds();
            BuildButterflies();
            BuildGrass();

            BuildGameplay(res);
            return res;
        }

        // ------------------------------------------------------------------ helpers

        static Mesh Cached(string key, System.Func<MeshKit> f)
        {
            Mesh m;
            if (meshCache.TryGetValue(key, out m) && m != null) return m;
            m = RatGen.Mesh(f(), "RS_" + key);
            meshCache[key] = m;
            return m;
        }

        static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3? scale = null, bool shadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            go.layer = Layers.Environment;
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

        static Transform Group(string name, Transform parent, Vector3 pos, float yaw = 0f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.layer = Layers.Environment;
            return go.transform;
        }

        static BoxCollider Box(GameObject go, Vector3 center, Vector3 size)
        {
            BoxCollider b = go.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
            return b;
        }

        static void Surface(GameObject go, SurfaceKind k) { go.AddComponent<RatSurface>().kind = k; }

        static void Cover(Transform t, float radius)
        {
            RatCover c = t.gameObject.AddComponent<RatCover>();
            c.radius = radius;
        }

        static void KeepClear(Vector3 p, float r) { if (planter != null) planter.clearCircles.Add(new Vector3(p.x, p.z, r)); }

        static Material Toon(string n, Color c, Texture2D tex = null, float tiling = 1f, float spec = 0.12f, float rim = 0.3f, float gloss = 30f)
        {
            return RatMats.Toon(n, c, new RatMats.ToonOpts { tex = tex, tiling = new Vector2(tiling, tiling), spec = spec, rim = rim, gloss = gloss });
        }

        // ------------------------------------------------------------------ materials

        static void BuildMaterials()
        {
            Texture2D grass = CartoonTex.GrassDetail();
            Texture2D planks = CartoonTex.Planks("RS_PlanksTex", new Color(0.93f, 0.9f, 0.86f), new Color(0.72f, 0.66f, 0.6f), 4, 3);
            Texture2D fence = CartoonTex.Planks("RS_FenceTex", new Color(0.95f, 0.92f, 0.88f), new Color(0.7f, 0.64f, 0.58f), 1, 5);
            Texture2D hay = CartoonTex.Hay();
            Texture2D foliage = CartoonTex.Foliage();

            mGround = RatMats.Toon("RS_Ground", Color.white, new RatMats.ToonOpts { tex = grass, spec = 0.02f, rim = 0.05f, rampSmooth = 0.35f });
            mVeg = RatMats.Toon("RS_Vegetation", Color.white, new RatMats.ToonOpts { spec = 0.08f, rim = 0.2f, wind = 0.08f, twoSided = true, flipBackNormals = false, rampSmooth = 0.4f, rampOffset = -0.15f });
            mWood = Toon("RS_Wood", new Color(0.78f, 0.55f, 0.33f), planks, 1f, 0.1f);
            mWoodDark = Toon("RS_WoodDark", new Color(0.55f, 0.36f, 0.22f), planks, 1f, 0.1f);
            mFence = Toon("RS_FenceWood", new Color(0.82f, 0.62f, 0.4f), fence, 1f, 0.1f);
            mBarnRed = Toon("RS_BarnRed", new Color(0.82f, 0.22f, 0.17f), planks, 0.5f, 0.1f, 0.25f);
            mTrim = Toon("RS_WhiteTrim", new Color(0.97f, 0.95f, 0.9f), null, 1f, 0.15f);
            mSiding = Toon("RS_HouseSiding", new Color(0.99f, 0.9f, 0.66f), planks, 0.7f, 0.08f);
            mRoof = Toon("RS_Roof", new Color(0.42f, 0.3f, 0.28f), CartoonTex.Shingles(), 0.5f, 0.1f, 0.2f);
            mRoofDark = Toon("RS_RoofUnder", new Color(0.5f, 0.34f, 0.22f), planks, 1f, 0.05f);
            mHay = Toon("RS_Hay", new Color(1f, 0.86f, 0.45f), hay, 1.5f, 0.08f, 0.35f);
            mStone = Toon("RS_Stone", new Color(0.66f, 0.66f, 0.7f), CartoonTex.Stone(), 1f, 0.12f);
            mMetal = Toon("RS_Metal", new Color(0.6f, 0.64f, 0.7f), CartoonTex.Metal(), 1f, 0.6f, 0.4f, 60f);
            mSilo = Toon("RS_Silo", new Color(0.78f, 0.82f, 0.86f), CartoonTex.Metal(), 2f, 0.5f, 0.35f, 50f);
            mLeaves = RatMats.Toon("RS_Leaves", Color.white, new RatMats.ToonOpts { tex = foliage, tiling = new Vector2(2f, 2f), spec = 0.1f, rim = 0.45f, rampSmooth = 0.32f, shadeTint = new Color(0.55f, 0.7f, 0.85f) });
            mBark = Toon("RS_Bark", new Color(0.55f, 0.38f, 0.25f), CartoonTex.Bark(), 1f, 0.05f);
            mSoil = Toon("RS_Soil", new Color(0.5f, 0.34f, 0.22f), CartoonTex.Soil(), 1f, 0.03f, 0.1f);
            mPumpkin = Toon("RS_Pumpkin", new Color(1f, 0.55f, 0.12f), null, 1f, 0.35f, 0.35f, 40f);
            mBurlap = Toon("RS_Burlap", new Color(0.82f, 0.68f, 0.48f), CartoonTex.Burlap(), 2f, 0.05f);
            mCheese = Toon("RS_Cheese", new Color(1f, 0.84f, 0.3f), CartoonTex.Cheese(), 2f, 0.3f, 0.35f);
            mWater = RatMats.Toon("RS_Water", new Color(0.35f, 0.68f, 0.95f), new RatMats.ToonOpts { spec = 0.9f, gloss = 90f, rim = 0.6f, emission = new Color(0.05f, 0.12f, 0.2f) });
            mBurrow = RatMats.Toon("RS_Burrow", new Color(0.12f, 0.07f, 0.04f), new RatMats.ToonOpts { spec = 0f, rim = 0f });
            mDirt = Toon("RS_DirtMound", new Color(0.66f, 0.48f, 0.3f), CartoonTex.Soil(), 1f, 0.03f, 0.1f);
            mCloud = RatMats.Toon("RS_Cloud", Color.white, new RatMats.ToonOpts { spec = 0f, rim = 0.5f, rampSmooth = 0.45f, fog = 0.15f, shadeTint = new Color(0.78f, 0.84f, 1f), emission = new Color(0.12f, 0.12f, 0.14f) });
            mPlaid = RatMats.Toon("RS_ScarecrowShirt", Color.white, new RatMats.ToonOpts { tex = CartoonTex.Plaid(new Color(0.25f, 0.45f, 0.85f), new Color(0.12f, 0.15f, 0.35f)), tiling = new Vector2(2f, 2f), spec = 0.05f });
            mRed = Toon("RS_RedPaint", new Color(0.9f, 0.25f, 0.2f), null, 1f, 0.3f);
            mBlueTrim = Toon("RS_BlueTrim", new Color(0.35f, 0.55f, 0.85f), null, 1f, 0.2f);
            mGlass = RatMats.Toon("RS_Glass", new Color(0.55f, 0.78f, 0.95f), new RatMats.ToonOpts { spec = 1f, gloss = 120f, rim = 0.7f, emission = new Color(0.08f, 0.12f, 0.18f) });
            mLeafFlat = RatMats.Toon("RS_LeafFlat", new Color(0.35f, 0.65f, 0.25f), new RatMats.ToonOpts { spec = 0.1f, twoSided = true, flipBackNormals = false, rim = 0.3f, wind = 0.04f });
            mDark = Toon("RS_DarkIron", new Color(0.18f, 0.17f, 0.2f), null, 1f, 0.4f, 0.25f);
            mHills = RatMats.Toon("RS_Hills", Color.white, new RatMats.ToonOpts { tex = grass, tiling = new Vector2(1f, 1f), spec = 0f, rim = 0.15f, rampSmooth = 0.4f });
            mWhite = Toon("RS_White", Color.white, null, 1f, 0.2f);
        }

        // ------------------------------------------------------------------ lighting

        static Light BuildLighting(out Material sky)
        {
            sky = RatMats.Sky();
            sky.SetColor("_TopColor", new Color(0.24f, 0.52f, 0.94f));
            sky.SetColor("_HorizonColor", new Color(0.8f, 0.9f, 1f));
            sky.SetColor("_BottomColor", new Color(0.62f, 0.74f, 0.62f));
            sky.SetFloat("_Exponent", 0.55f);
            sky.SetColor("_SunColor", new Color(1f, 0.93f, 0.7f));
            sky.SetFloat("_SunSize", 0.004f);
            sky.SetFloat("_SunGlow", 0.5f);

            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(envRoot, false);
            sunGo.transform.rotation = Quaternion.Euler(46f, 38f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 0.98f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;

            RatEnvironment env = envRoot.gameObject.AddComponent<RatEnvironment>();
            env.skybox = sky;
            env.sun = sun;
            env.Apply();
            return sun;
        }

        // ------------------------------------------------------------------ ground

        static float PathDist(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float best = 99f;
            best = Mathf.Min(best, PolyDist(p, MainPath));
            best = Mathf.Min(best, PolyDist(p, BranchPath));
            return best;
        }

        static float PolyDist(Vector2 p, Vector2[] pts)
        {
            float best = 99f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - pts[i];
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * h)).magnitude);
            }
            return best;
        }

        public static float GroundHeight(float x, float z)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x) - 22f);
            float dz = Mathf.Max(0f, z - 38f) + Mathf.Max(0f, -8f - z);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= 0f) return 0f;
            float hill = Mathf.SmoothStep(0f, 1f, d / 28f) * (3f + RatNoise.Fbm2(x * 0.025f + 5f, z * 0.025f, 3, 5) * 12f);
            float bumps = (RatNoise.Fbm2(x * 0.12f, z * 0.12f, 2, 9) - 0.5f) * 0.4f * Mathf.SmoothStep(0f, 1f, d / 5f);
            return hill + bumps;
        }

        static Color GroundColor(float x, float z)
        {
            float n = RatNoise.Fbm2(x * 0.07f + 3f, z * 0.07f, 3, 3);
            float n2 = RatNoise.Fbm2(x * 0.3f, z * 0.3f, 2, 4);
            Color g = Color.Lerp(new Color(0.27f, 0.55f, 0.2f), new Color(0.44f, 0.74f, 0.27f), n);
            g *= 0.93f + n2 * 0.14f;
            float h = GroundHeight(x, z);
            g = Color.Lerp(g, new Color(0.5f, 0.72f, 0.3f), Mathf.Clamp01(h / 14f) * 0.5f);
            float pd = PathDist(x, z) + (n2 - 0.5f) * 0.45f;
            float path = 1f - Mathf.SmoothStep(0.7f, 1.3f, pd);
            Color dirt = Color.Lerp(new Color(0.72f, 0.54f, 0.33f), new Color(0.84f, 0.68f, 0.44f), n2);
            g = Color.Lerp(g, dirt, path);
            // worn earth under the garden and around the porch steps
            Rect gr = Garden;
            float inGarden = Mathf.Clamp01(Mathf.Min(Mathf.Min(x - gr.xMin, gr.xMax - x), Mathf.Min(z - gr.yMin, gr.yMax - z)) / 0.8f + 0.5f);
            g = Color.Lerp(g, new Color(0.5f, 0.37f, 0.24f), inGarden * 0.85f);
            float worn = Mathf.Clamp01(1f - new Vector2(x, z - 1.2f).magnitude / 2.5f);
            g = Color.Lerp(g, dirt, worn * 0.7f);
            g.a = 1f;
            return g;
        }

        static void BuildGround()
        {
            MeshKit k = Shapes.Grid(-70f, -25f, 140f, 135f, 140, 135, GroundHeight, GroundColor, 3f);
            Mesh m = RatGen.Mesh(k, "RS_Ground");
            GameObject g = Obj("Ground", envRoot, m, mGround, Vector3.zero, Vector3.zero);
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.AddComponent<MeshCollider>().sharedMesh = m;
            Surface(g, SurfaceKind.Grass);

            // far horizon ring of soft hills (fogged into the sky)
            MeshKit ring = new MeshKit();
            int seg = 96, rows = 6;
            for (int j = 0; j <= rows; j++)
            {
                float rr = Mathf.Lerp(105f, 230f, (float)j / rows);
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr + 20f;
                    float h = (RatNoise.Value2(Mathf.Cos(a) * 3f + 10f, Mathf.Sin(a) * 3f + 10f, 21) * 22f + 6f) * Mathf.Sin((float)j / rows * Mathf.PI) - 4f;
                    Color c = Color.Lerp(new Color(0.42f, 0.66f, 0.3f), new Color(0.55f, 0.72f, 0.45f), (float)j / rows);
                    ring.Add(new Vector3(x, h, z), Vector3.up, new Vector2(x, z) / 20f, c);
                }
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * (seg + 1) + i;
                    ring.Quad(a, a + 1, a + seg + 2, a + seg + 1);
                }
            ring.OrientOutward().SmoothNormals();
            Obj("HorizonHills", envRoot, RatGen.Mesh(ring, "RS_HorizonHills"), mHills, Vector3.zero, Vector3.zero, null, false);
        }

        // ------------------------------------------------------------------ porch + house (where the farmer stands)

        static void BuildPorchAndHouse()
        {
            Transform porch = Group("FarmhousePorch", envRoot, Vector3.zero);
            const float deckY = 0.6f;
            MeshKit deck = new MeshKit();
            for (int i = 0; i < 16; i++)
            {
                float z = -3.2f + i * 0.2f;
                float tint = 0.9f + (i * 37 % 10) * 0.02f;
                MeshKit b = Shapes.RoundedBox(new Vector3(9f, 0.08f, 0.19f), 0.03f, new Color(tint, tint, tint), 1f, 2);
                deck.Append(b, new Vector3(0f, deckY - 0.04f, z), Quaternion.Euler(0f, (i % 3 - 1) * 0.3f, 0f), Vector3.one);
            }
            deck.Append(Shapes.RoundedBox(new Vector3(9.2f, 0.5f, 0.12f), 0.04f, new Color(0.85f, 0.85f, 0.85f)), new Vector3(0f, deckY - 0.3f, -0.05f), Quaternion.identity, Vector3.one);
            // steps
            for (int s = 0; s < 3; s++)
                deck.Append(Shapes.RoundedBox(new Vector3(1.8f, 0.07f, 0.34f), 0.03f, Color.white), new Vector3(0f, deckY - 0.18f - s * 0.18f, 0.18f + s * 0.32f), Quaternion.identity, Vector3.one);
            Obj("Deck", porch, RatGen.Mesh(deck, "RS_PorchDeck"), mWood, Vector3.zero, Vector3.zero);

            // railing (no colliders - never blocks a shot)
            MeshKit rail = new MeshKit();
            MeshKit post = Shapes.RoundedBox(new Vector3(0.13f, 0.95f, 0.13f), 0.03f, Color.white, 1f, 2);
            MeshKit bal = Shapes.RoundedBox(new Vector3(0.045f, 0.7f, 0.045f), 0.015f, Color.white, 1f, 1);
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = 0.95f * side, x1 = 4.4f * side;
                rail.Append(Shapes.RoundedBox(new Vector3(Mathf.Abs(x1 - x0), 0.09f, 0.15f), 0.035f, Color.white), new Vector3((x0 + x1) * 0.5f, deckY + 0.9f, -0.12f), Quaternion.identity, Vector3.one);
                rail.Append(Shapes.RoundedBox(new Vector3(Mathf.Abs(x1 - x0), 0.06f, 0.08f), 0.02f, Color.white), new Vector3((x0 + x1) * 0.5f, deckY + 0.16f, -0.12f), Quaternion.identity, Vector3.one);
                rail.Append(post, new Vector3(x0, deckY + 0.47f, -0.12f), Quaternion.identity, Vector3.one);
                for (float x = Mathf.Min(x0, x1) + 0.22f; x < Mathf.Max(x0, x1) - 0.1f; x += 0.22f)
                    rail.Append(bal, new Vector3(x, deckY + 0.53f, -0.12f), Quaternion.identity, Vector3.one);
            }
            Obj("Railing", porch, RatGen.Mesh(rail, "RS_PorchRailing"), mTrim, Vector3.zero, Vector3.zero);

            // posts + roof overhead
            Mesh postMesh = Cached("PorchPost", () => Shapes.RoundedBox(new Vector3(0.22f, 3f, 0.22f), 0.05f, Color.white));
            Obj("PorchPostL", porch, postMesh, mTrim, new Vector3(-4.4f, deckY + 1.5f, -0.12f), Vector3.zero);
            Obj("PorchPostR", porch, postMesh, mTrim, new Vector3(4.4f, deckY + 1.5f, -0.12f), Vector3.zero);
            Obj("PorchRoof", porch, Cached("PorchRoof", () => Shapes.RoundedBox(new Vector3(9.8f, 0.2f, 3.9f), 0.06f, Color.white)), mRoofDark, new Vector3(0f, deckY + 3.05f, -1.55f), new Vector3(-6f, 0f, 0f));
            Obj("PorchRoofTrim", porch, Cached("PorchRoofTrim", () => Shapes.RoundedBox(new Vector3(9.9f, 0.22f, 0.12f), 0.04f, Color.white)), mTrim, new Vector3(0f, deckY + 2.85f, 0.38f), Vector3.zero);

            // hanging lantern + flower pot + bucket for charm
            Transform lantern = Group("Lantern", porch, new Vector3(-4.4f, deckY + 2.2f, 0.05f));
            Obj("Glass", lantern, Cached("LanternGlass", () => Shapes.RoundedBox(new Vector3(0.18f, 0.24f, 0.18f), 0.05f, Color.white)), mGlass, Vector3.zero, Vector3.zero);
            Obj("Cap", lantern, Cached("LanternCap", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.03f, 0.12f), new Vector2(0f, 0.13f) }, 12, Color.white)), mDark, new Vector3(0f, 0.12f, 0f), Vector3.zero);
            Obj("Pot", porch, Cached("FlowerPot", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.22f, 0.3f), new Vector2(0.25f, 0.32f), new Vector2(0.2f, 0.3f), new Vector2(0f, 0.28f) }, 16, Color.white)), mRed, new Vector3(3.8f, deckY, -0.6f), Vector3.zero);
            MeshKit potFlowers = new MeshKit();
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51f * Mathf.Deg2Rad;
                Color pc = i % 2 == 0 ? new Color(1f, 0.45f, 0.6f) : new Color(1f, 0.85f, 0.25f);
                potFlowers.Append(Shapes.Blob(new Vector3(0.08f, 0.07f, 0.08f), i, 0.15f, pc, 8, 6), new Vector3(Mathf.Cos(a) * 0.13f, 0.38f + (i % 3) * 0.04f, Mathf.Sin(a) * 0.13f), Quaternion.identity, Vector3.one);
            }
            potFlowers.Append(Shapes.Blob(new Vector3(0.2f, 0.1f, 0.2f), 9, 0.2f, new Color(0.3f, 0.6f, 0.22f), 10, 6), new Vector3(0f, 0.32f, 0f), Quaternion.identity, Vector3.one);
            Obj("PotFlowers", porch, RatGen.Mesh(potFlowers, "RS_PotFlowers"), mVeg, new Vector3(3.8f, deckY, -0.6f), Vector3.zero);

            // farmhouse front wall either side of the porch + a window each side
            Transform house = Group("Farmhouse", envRoot, Vector3.zero);
            Mesh wall = Cached("HouseWall", () => Shapes.RoundedBox(new Vector3(8f, 4.2f, 0.3f), 0.05f, Color.white, 0.5f));
            Obj("WallL", house, wall, mSiding, new Vector3(-8.4f, 2.1f, -3.4f), Vector3.zero);
            Obj("WallR", house, wall, mSiding, new Vector3(8.4f, 2.1f, -3.4f), Vector3.zero);
            Obj("WallBack", house, Cached("HouseWallMid", () => Shapes.RoundedBox(new Vector3(9f, 4.2f, 0.3f), 0.05f, Color.white, 0.5f)), mSiding, new Vector3(0f, 2.1f, -3.4f), Vector3.zero);
            Obj("Eave", house, Cached("HouseEave", () => Shapes.RoundedBox(new Vector3(25f, 0.3f, 1.2f), 0.08f, Color.white)), mRoof, new Vector3(0f, 4.3f, -3.1f), new Vector3(-12f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                Transform win = Group("Window", house, new Vector3(8.2f * s, 2.1f, -3.22f));
                Obj("Frame", win, Cached("WinFrame", () => Shapes.RoundedBox(new Vector3(1.5f, 1.4f, 0.1f), 0.06f, Color.white)), mTrim, Vector3.zero, Vector3.zero);
                Obj("Glass", win, Cached("WinGlass", () => Shapes.RoundedBox(new Vector3(1.25f, 1.15f, 0.06f), 0.04f, Color.white)), mGlass, new Vector3(0f, 0f, 0.04f), Vector3.zero);
                Obj("Bar", win, Cached("WinBarV", () => Shapes.RoundedBox(new Vector3(0.07f, 1.2f, 0.08f), 0.02f, Color.white)), mTrim, new Vector3(0f, 0f, 0.07f), Vector3.zero);
                Obj("Bar", win, Cached("WinBarH", () => Shapes.RoundedBox(new Vector3(1.3f, 0.07f, 0.08f), 0.02f, Color.white)), mTrim, new Vector3(0f, 0f, 0.07f), Vector3.zero);
                Obj("ShutterL", win, Cached("Shutter", () => Shapes.RoundedBox(new Vector3(0.5f, 1.45f, 0.08f), 0.04f, Color.white)), mBlueTrim, new Vector3(-1.05f, 0f, 0.02f), Vector3.zero);
                Obj("ShutterR", win, Cached("Shutter", () => Shapes.RoundedBox(new Vector3(0.5f, 1.45f, 0.08f), 0.04f, Color.white)), mBlueTrim, new Vector3(1.05f, 0f, 0.02f), Vector3.zero);
            }
            Obj("HouseFoundation", house, Cached("Foundation", () => Shapes.RoundedBox(new Vector3(25f, 0.6f, 0.5f), 0.08f, Color.white)), mStone, new Vector3(0f, 0.3f, -3.35f), Vector3.zero);
        }

        // ------------------------------------------------------------------ fences

        static void BuildFences()
        {
            Transform fences = Group("Fences", envRoot, Vector3.zero);
            MeshKit all = new MeshKit();
            List<Vector3[]> segs = new List<Vector3[]>
            {
                new[] { new Vector3(-YardHalfX, 0f, 1.5f), new Vector3(-YardHalfX, 0f, YardBackZ) },
                new[] { new Vector3(YardHalfX, 0f, 1.5f), new Vector3(YardHalfX, 0f, YardBackZ) },
                new[] { new Vector3(-YardHalfX, 0f, YardBackZ), new Vector3(-10.4f, 0f, YardBackZ) },
                new[] { new Vector3(-7.4f, 0f, YardBackZ), new Vector3(YardHalfX, 0f, YardBackZ) },
                new[] { new Vector3(-12.6f, 0f, -3.1f), new Vector3(-YardHalfX, 0f, 1.5f) },
                new[] { new Vector3(12.6f, 0f, -3.1f), new Vector3(YardHalfX, 0f, 1.5f) }
            };
            int seed = 0;
            foreach (Vector3[] s in segs) FenceLine(all, fences, s[0], s[1], ref seed);
            // gate posts
            Mesh gp = Cached("GatePost", () => Shapes.RoundedBox(new Vector3(0.26f, 1.6f, 0.26f), 0.06f, Color.white));
            GameObject g1 = Obj("GatePost", fences, gp, mWoodDark, new Vector3(-10.4f, 0.8f, YardBackZ), Vector3.zero);
            GameObject g2 = Obj("GatePost", fences, gp, mWoodDark, new Vector3(-7.4f, 0.8f, YardBackZ), Vector3.zero);
            Box(g1, Vector3.zero, new Vector3(0.26f, 1.6f, 0.26f));
            Box(g2, Vector3.zero, new Vector3(0.26f, 1.6f, 0.26f));
            GameObject f = Obj("FenceMesh", fences, RatGen.Mesh(all, "RS_Fences"), mFence, Vector3.zero, Vector3.zero);
            Surface(f, SurfaceKind.Wood);
            Surface(fences.gameObject, SurfaceKind.Wood);
        }

        static void FenceLine(MeshKit all, Transform parent, Vector3 a, Vector3 b, ref int seed)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
            Vector3 dir = d / len;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            MeshKit post = Shapes.RoundedBox(new Vector3(0.16f, 1.15f, 0.16f), 0.05f, Color.white, 1f, 1);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = a + d * ((float)i / n);
                float tilt = ((seed++ * 37) % 7 - 3) * 0.8f;
                all.Append(post, p + Vector3.up * 0.575f, Quaternion.Euler(tilt, yaw, tilt * 0.5f), Vector3.one);
                GameObject c = new GameObject("PostCollider");
                c.transform.SetParent(parent, false);
                c.transform.position = p + Vector3.up * 0.575f;
                c.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                c.layer = Layers.Environment;
                Box(c, Vector3.zero, new Vector3(0.16f, 1.15f, 0.16f));
                if (i == n) break;
                Vector3 m = a + d * ((i + 0.5f) / n);
                float segLen = len / n + 0.15f;
                for (int r = 0; r < 2; r++)
                {
                    float h = r == 0 ? 0.48f : 0.92f;
                    float wob = ((seed++ * 53) % 5 - 2) * 0.6f;
                    all.Append(Shapes.RoundedBox(new Vector3(0.07f, 0.13f, segLen), 0.03f, Color.white, 1f, 1), m + Vector3.up * h, Quaternion.Euler(wob, yaw, 0f), Vector3.one);
                }
                GameObject rc = new GameObject("RailCollider");
                rc.transform.SetParent(parent, false);
                rc.transform.position = m + Vector3.up * 0.7f;
                rc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                rc.layer = Layers.Environment;
                Box(rc, Vector3.zero, new Vector3(0.08f, 0.56f, segLen));
                // rails: let mice and grass under, but block the NavMesh line so mice stay in the yard
            }
        }

        // ------------------------------------------------------------------ barn, silo, windpump

        static void BuildBarn(Vector3 pos)
        {
            Transform barn = Group("RedBarn", envRoot, pos);
            const float w = 12f, dpt = 10f, wallH = 5f;
            Obj("Walls", barn, Cached("BarnWalls", () => Shapes.RoundedBox(new Vector3(w, wallH, dpt), 0.12f, Color.white, 0.5f)), mBarnRed, new Vector3(0f, wallH * 0.5f, 0f), Vector3.zero);
            // gambrel attic (prism) + roof slabs
            Vector2[] prof = { new Vector2(-w * 0.5f, wallH), new Vector2(-w * 0.38f, wallH + 2.6f), new Vector2(0f, wallH + 4.5f), new Vector2(w * 0.38f, wallH + 2.6f), new Vector2(w * 0.5f, wallH) };
            MeshKit attic = new MeshKit();
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? -dpt * 0.5f : dpt * 0.5f;
                Vector3 nrm = s == 0 ? Vector3.back : Vector3.forward;
                int c = attic.Add(new Vector3(0f, wallH + 1.5f, z), nrm, new Vector2(0f, wallH + 1.5f), Color.white);
                for (int i = 0; i < prof.Length; i++) attic.Add(new Vector3(prof[i].x, prof[i].y, z), nrm, new Vector2(prof[i].x, prof[i].y), Color.white);
                for (int i = 0; i < prof.Length - 1; i++) attic.Tri(c, c + 1 + i, c + 2 + i);
                attic.Tri(c, c + prof.Length, c + 1);
            }
            attic.OrientOutward();
            Obj("Gables", barn, RatGen.Mesh(attic, "RS_BarnGables"), mBarnRed, Vector3.zero, Vector3.zero);
            for (int i = 0; i < prof.Length - 1; i++)
            {
                Vector2 a = prof[i], b = prof[i + 1];
                Vector2 mid = (a + b) * 0.5f, dir = (b - a);
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Vector2 outN = new Vector2(-dir.y, dir.x).normalized;
                float len = dir.magnitude + 0.5f;
                Obj("RoofSlab" + i, barn, Cached("BarnRoof" + i, () => Shapes.RoundedBox(new Vector3(len, 0.25f, dpt + 1.2f), 0.08f, Color.white, 0.4f)), mRoof,
                    new Vector3(mid.x + outN.x * 0.12f, mid.y + outN.y * 0.12f, 0f), new Vector3(0f, 0f, ang));
            }
            // front: big doors with white X trim, hayloft door, trim
            Transform front = Group("Front", barn, new Vector3(0f, 0f, -dpt * 0.5f - 0.08f));
            Mesh trimV = Cached("BarnTrimV", () => Shapes.RoundedBox(new Vector3(0.28f, 3.6f, 0.12f), 0.05f, Color.white));
            Mesh trimH = Cached("BarnTrimH", () => Shapes.RoundedBox(new Vector3(4.6f, 0.28f, 0.12f), 0.05f, Color.white));
            Mesh diag = Cached("BarnTrimD", () => Shapes.RoundedBox(new Vector3(0.22f, 4.1f, 0.1f), 0.05f, Color.white));
            Obj("DoorPanel", front, Cached("BarnDoor", () => Shapes.RoundedBox(new Vector3(4.4f, 3.5f, 0.1f), 0.05f, Color.white, 0.5f)), mRed, new Vector3(0f, 1.8f, 0f), Vector3.zero);
            Obj("TrimL", front, trimV, mTrim, new Vector3(-2.2f, 1.8f, -0.06f), Vector3.zero);
            Obj("TrimR", front, trimV, mTrim, new Vector3(2.2f, 1.8f, -0.06f), Vector3.zero);
            Obj("TrimM", front, trimV, mTrim, new Vector3(0f, 1.8f, -0.06f), Vector3.zero);
            Obj("TrimT", front, trimH, mTrim, new Vector3(0f, 3.6f, -0.06f), Vector3.zero);
            Obj("TrimB", front, trimH, mTrim, new Vector3(0f, 0.1f, -0.06f), Vector3.zero);
            Obj("XL1", front, diag, mTrim, new Vector3(-1.1f, 1.85f, -0.08f), new Vector3(0f, 0f, 32f));
            Obj("XL2", front, diag, mTrim, new Vector3(-1.1f, 1.85f, -0.08f), new Vector3(0f, 0f, -32f));
            Obj("XR1", front, diag, mTrim, new Vector3(1.1f, 1.85f, -0.08f), new Vector3(0f, 0f, 32f));
            Obj("XR2", front, diag, mTrim, new Vector3(1.1f, 1.85f, -0.08f), new Vector3(0f, 0f, -32f));
            Obj("Loft", front, Cached("BarnLoft", () => Shapes.RoundedBox(new Vector3(1.8f, 1.6f, 0.12f), 0.06f, Color.white)), mTrim, new Vector3(0f, 6.4f, 0f), Vector3.zero);
            Obj("LoftDoor", front, Cached("BarnLoftDoor", () => Shapes.RoundedBox(new Vector3(1.45f, 1.25f, 0.1f), 0.05f, Color.white)), mRed, new Vector3(0f, 6.4f, -0.04f), Vector3.zero);
            Obj("LoftHay", front, Cached("LoftHay", () => Shapes.Blob(new Vector3(0.6f, 0.25f, 0.2f), 4, 0.3f, Color.white)), mHay, new Vector3(0f, 5.85f, -0.12f), Vector3.zero);
            Obj("Cupola", barn, Cached("Cupola", () => Shapes.RoundedBox(new Vector3(1.2f, 1.1f, 1.2f), 0.06f, Color.white)), mTrim, new Vector3(0f, wallH + 4.9f, 0f), Vector3.zero);
            Obj("CupolaRoof", barn, Cached("CupolaRoof", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(1.05f, 0f), new Vector2(0.05f, 0.9f), new Vector2(0f, 0.92f) }, 4, Color.white)), mRoof, new Vector3(0f, wallH + 5.45f, 0f), new Vector3(0f, 45f, 0f));
            Obj("Vane", barn, Cached("Vane", () => Shapes.RoundedBox(new Vector3(0.05f, 0.9f, 0.05f), 0.02f, Color.white)), mDark, new Vector3(0f, wallH + 6.7f, 0f), Vector3.zero);
            Obj("VaneArrow", barn, Cached("VaneArrow", () => Shapes.Wedge(0.1f, 0.25f, 0.5f, Color.white)), mDark, new Vector3(0f, wallH + 7f, 0f), new Vector3(0f, 0f, 0f));
            BoxCollider bc = Box(barn.gameObject, new Vector3(0f, 4.5f, 0f), new Vector3(w, 9f, dpt));
            bc.enabled = true;
            Surface(barn.gameObject, SurfaceKind.Wood);
        }

        static void BuildSilo(Vector3 pos)
        {
            Transform silo = Group("Silo", envRoot, pos);
            List<Vector2> p = new List<Vector2> { new Vector2(0f, 0f), new Vector2(2.5f, 0f), new Vector2(2.5f, 11f) };
            for (int i = 1; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 0.5f;
                p.Add(new Vector2(Mathf.Cos(a) * 2.6f, 11f + Mathf.Sin(a) * 2.2f));
            }
            p[p.Count - 1] = new Vector2(0f, 13.2f);
            Obj("Body", silo, Cached("SiloBody", () => Shapes.Lathe(p, 32, Color.white, 0.3f, 4f)), mSilo, Vector3.zero, Vector3.zero);
            Mesh band = Cached("SiloBand", () => Shapes.Torus(2.53f, 0.06f, 40, 6, Color.white));
            for (int i = 1; i <= 5; i++) Obj("Band", silo, band, mDark, new Vector3(0f, i * 1.9f, 0f), Vector3.zero);
            Obj("Cap", silo, Cached("SiloCap", () => Shapes.Sphere(0.35f, 12, 8, Color.white)), mRed, new Vector3(0f, 13.25f, 0f), Vector3.zero);
            CapsuleCollider c = silo.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 6.5f, 0f);
            c.radius = 2.5f;
            c.height = 13f;
            Surface(silo.gameObject, SurfaceKind.Metal);
        }

        static void BuildWindpump(Vector3 pos)
        {
            Transform wp = Group("Windpump", envRoot, pos, -20f);
            const float h = 9f;
            MeshKit tower = new MeshKit();
            Vector3[] baseC = { new Vector3(-1.3f, 0f, -1.3f), new Vector3(1.3f, 0f, -1.3f), new Vector3(1.3f, 0f, 1.3f), new Vector3(-1.3f, 0f, 1.3f) };
            Vector3 top = new Vector3(0f, h, 0f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = baseC[i], b = top + baseC[i] * 0.2f;
                tower.Append(Shapes.Tube(new List<Vector3> { a, b }, 0.07f, 0.05f, 6, Color.white));
                Vector3 a2 = baseC[(i + 1) % 4];
                for (int k = 1; k <= 3; k++)
                {
                    float f = k / 4f;
                    Vector3 p1 = Vector3.Lerp(a, b, f), p2 = Vector3.Lerp(a2, top + a2 * 0.2f, f);
                    tower.Append(Shapes.Tube(new List<Vector3> { p1, p2 }, 0.03f, 0.03f, 5, Color.white));
                }
            }
            Obj("Tower", wp, RatGen.Mesh(tower, "RS_WindpumpTower"), mMetal, Vector3.zero, Vector3.zero);
            Transform head = Group("Head", wp, top + Vector3.up * 0.3f);
            Obj("Gearbox", head, Cached("Gearbox", () => Shapes.RoundedBox(new Vector3(0.45f, 0.45f, 0.8f), 0.12f, Color.white)), mRed, Vector3.zero, Vector3.zero);
            Obj("Tail", head, Cached("PumpTail", () => Shapes.RoundedBox(new Vector3(0.05f, 0.9f, 1.4f), 0.03f, Color.white)), mRed, new Vector3(0f, 0.15f, 1.3f), Vector3.zero);
            Transform fan = Group("Fan", head, new Vector3(0f, 0f, -0.5f));
            MeshKit blades = new MeshKit();
            for (int i = 0; i < 16; i++)
                blades.Append(Shapes.RoundedBox(new Vector3(0.28f, 1.4f, 0.03f), 0.02f, i % 2 == 0 ? Color.white : new Color(0.85f, 0.88f, 0.92f), 1f, 1), Quaternion.Euler(0f, 0f, i * 22.5f) * new Vector3(0f, 1.0f, 0f), Quaternion.Euler(0f, 0f, i * 22.5f) * Quaternion.Euler(0f, 18f, 0f), Vector3.one);
            blades.Append(Shapes.Torus(1.65f, 0.035f, 40, 5, Color.white), Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            blades.Append(Shapes.Sphere(0.18f, 12, 8, Color.white));
            Obj("Blades", fan, RatGen.Mesh(blades, "RS_WindpumpFan"), mMetal, Vector3.zero, Vector3.zero);
            RatSpinner sp = fan.gameObject.AddComponent<RatSpinner>();
            sp.axis = Vector3.forward;
            sp.speed = 70f;
        }

        // ------------------------------------------------------------------ trees + bushes + rocks

        static MeshKit FoliageCluster(int seed, float size, int lon = 16, int lat = 12)
        {
            System.Random r = new System.Random(seed);
            MeshKit k = new MeshKit();
            int n = 5 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float rr = i == 0 ? 0f : size * (0.45f + (float)r.NextDouble() * 0.35f);
                Vector3 c = new Vector3(Mathf.Cos(a) * rr, (i == 0 ? size * 0.35f : (float)r.NextDouble() * size * 0.5f), Mathf.Sin(a) * rr);
                float s = size * (i == 0 ? 0.85f : 0.55f + (float)r.NextDouble() * 0.25f);
                Color col = Color.Lerp(new Color(0.3f, 0.6f, 0.2f), new Color(0.45f, 0.75f, 0.25f), (float)r.NextDouble());
                MeshKit b = Shapes.Blob(new Vector3(s, s * 0.85f, s), seed * 7 + i, 0.18f, col, lon, lat);
                // darker underside: simple vertex-colour occlusion
                for (int v = 0; v < b.c.Count; v++)
                {
                    float k2 = Mathf.Lerp(0.62f, 1.08f, Mathf.Clamp01(b.v[v].y / s * 0.5f + 0.5f));
                    Color cc = b.c[v];
                    b.c[v] = new Color(cc.r * k2, cc.g * k2, cc.b * k2, 1f);
                }
                k.Append(b, c, Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f), Vector3.one);
            }
            return k;
        }

        static void Tree(Transform parent, Vector3 pos, float scale, int seed, bool apples)
        {
            Transform t = Group("Tree", parent, pos, seed * 47f);
            float h = 3.2f * scale;
            List<Vector3> trunkPath = new List<Vector3>();
            for (int i = 0; i <= 5; i++)
            {
                float f = i / 5f;
                trunkPath.Add(new Vector3(Mathf.Sin(f * 2f + seed) * 0.25f * scale * f, h * f, Mathf.Cos(f * 1.7f + seed) * 0.15f * scale * f));
            }
            Obj("Trunk", t, RatGen.Mesh(Shapes.Tube(trunkPath, 0.32f * scale, 0.18f * scale, 12, Color.white), "RS_Trunk" + seed), mBark, Vector3.zero, Vector3.zero);
            MeshKit fol = FoliageCluster(seed, 1.9f * scale);
            if (apples)
            {
                System.Random r = new System.Random(seed + 99);
                MeshKit apple = Shapes.Sphere(0.12f * scale, 10, 8, new Color(0.95f, 0.2f, 0.15f));
                for (int i = 0; i < 10; i++)
                {
                    Vector3 d = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 0.6f - 0.4f, (float)r.NextDouble() * 2f - 1f).normalized;
                    fol.Append(apple, d * 1.9f * scale * 0.95f + Vector3.up * 0.4f * scale, Quaternion.identity, Vector3.one);
                }
            }
            Obj("Foliage", t, RatGen.Mesh(fol, "RS_Foliage" + seed), mLeaves, trunkPath[trunkPath.Count - 1] + Vector3.up * 0.4f * scale, Vector3.zero);
            CapsuleCollider c = t.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, h * 0.5f, 0f);
            c.radius = 0.3f * scale;
            c.height = h;
            SphereCollider s = t.gameObject.AddComponent<SphereCollider>();
            s.center = trunkPath[trunkPath.Count - 1] + Vector3.up * 0.9f * scale;
            s.radius = 1.9f * scale;
            Surface(t.gameObject, SurfaceKind.Leaves);
        }

        static void BuildTrees()
        {
            Transform trees = Group("Trees", envRoot, Vector3.zero);
            Vector4[] spots =
            {
                new Vector4(-24f, 0f, 30f, 1.3f), new Vector4(-26f, 0f, 14f, 1.15f), new Vector4(-23.5f, 0f, 3f, 1f), new Vector4(24f, 0f, 26f, 1.25f),
                new Vector4(26f, 0f, 9f, 1.1f), new Vector4(23f, 0f, 37f, 1f), new Vector4(-19f, 0f, 47f, 1.4f), new Vector4(-31f, 0f, 40f, 1.2f),
                new Vector4(31f, 0f, 46f, 1.35f), new Vector4(11f, 0f, 53f, 1.1f), new Vector4(-2f, 0f, 55f, 1.25f), new Vector4(-15f, 0f, -6f, 1.2f),
                new Vector4(16f, 0f, -6f, 1.25f), new Vector4(-34f, 0f, 22f, 1.3f), new Vector4(34f, 0f, 18f, 1.2f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = new Vector3(spots[i].x, GroundHeight(spots[i].x, spots[i].z), spots[i].z);
                Tree(trees, p, spots[i].w, i + 1, i % 3 == 0);
            }

            // distant woods on the hills (one combined mesh)
            MeshKit far = new MeshKit();
            System.Random r = new System.Random(77);
            MeshKit[] puffs = { FoliageCluster(201, 2.2f, 8, 6), FoliageCluster(202, 2.6f, 8, 6), FoliageCluster(203, 2f, 8, 6) };
            MeshKit trunk = Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.35f, 0f), new Vector2(0.28f, 2.5f), new Vector2(0f, 2.5f) }, 6, new Color(0.45f, 0.3f, 0.2f));
            for (int i = 0; i < 120; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float rad = 42f + (float)r.NextDouble() * 50f;
                float x = Mathf.Cos(a) * rad, z = Mathf.Sin(a) * rad + 18f;
                if (z < -12f) continue;
                if (Mathf.Abs(x) < 26f && z < 58f) continue;
                float y = GroundHeight(x, z);
                float s = 0.9f + (float)r.NextDouble() * 0.9f;
                far.Append(trunk, new Vector3(x, y - 0.2f, z), Quaternion.identity, Vector3.one * s);
                far.Append(puffs[r.Next(puffs.Length)], new Vector3(x, y + 3f * s, z), Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f), Vector3.one * s);
            }
            Obj("DistantWoods", trees, RatGen.Mesh(far, "RS_DistantWoods"), mVeg, Vector3.zero, Vector3.zero, null, false);
        }

        static void Bush(Transform parent, Vector3 pos, float size, int seed, bool berries)
        {
            Transform b = Group("Bush", parent, pos, seed * 31f);
            MeshKit k = FoliageCluster(seed + 500, 0.75f * size);
            if (berries)
            {
                System.Random r = new System.Random(seed);
                MeshKit berry = Shapes.Sphere(0.06f, 8, 6, new Color(0.9f, 0.15f, 0.3f));
                for (int i = 0; i < 14; i++)
                {
                    Vector3 d = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 0.8f, (float)r.NextDouble() * 2f - 1f).normalized;
                    k.Append(berry, d * 0.72f * size + Vector3.up * 0.25f * size, Quaternion.identity, Vector3.one);
                }
            }
            Obj("Leaves", b, RatGen.Mesh(k, "RS_Bush" + seed), mLeaves, new Vector3(0f, 0.45f * size, 0f), Vector3.zero, new Vector3(1.2f, 0.8f, 1f));
            SphereCollider c = b.gameObject.AddComponent<SphereCollider>();
            c.center = new Vector3(0f, 0.55f * size, 0f);
            c.radius = 0.85f * size;
            Surface(b.gameObject, SurfaceKind.Leaves);
            Cover(b, 0.95f * size);
            KeepClear(pos, 0.9f * size);
        }

        static void Rock(Transform parent, Vector3 pos, float size, int seed)
        {
            GameObject r = Obj("Rock", parent, Cached("Rock" + seed % 3, () => Shapes.Blob(new Vector3(0.5f, 0.32f, 0.42f), 300 + seed % 3, 0.3f, Color.white, 14, 10)), mStone, pos + Vector3.up * 0.1f * size, new Vector3(0f, seed * 67f, 0f), Vector3.one * size);
            SphereCollider c = r.AddComponent<SphereCollider>();
            c.radius = 0.42f;
            Surface(r, SurfaceKind.Stone);
            KeepClear(pos, 0.5f * size);
        }

        // ------------------------------------------------------------------ garden, corn, pumpkins

        static MeshKit Cabbage(int seed)
        {
            System.Random r = new System.Random(seed);
            MeshKit k = new MeshKit();
            k.Append(Shapes.Sphere(0.16f, 12, 8, new Color(0.7f, 0.88f, 0.45f)), new Vector3(0f, 0.14f, 0f), Quaternion.identity, Vector3.one);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51.4f + (float)r.NextDouble() * 20f;
                Quaternion q = Quaternion.Euler(-35f - (float)r.NextDouble() * 20f, a, 0f);
                Color c = Color.Lerp(new Color(0.35f, 0.68f, 0.3f), new Color(0.55f, 0.82f, 0.4f), (float)r.NextDouble());
                k.Append(Shapes.Ellipsoid(new Vector3(0.17f, 0.03f, 0.2f), 8, 4, c), q * new Vector3(0f, 0f, 0.12f) + Vector3.up * 0.1f, q, Vector3.one);
            }
            k.WindByHeight(0f, 0.6f);
            return k;
        }

        static MeshKit Carrot(int seed)
        {
            System.Random r = new System.Random(seed);
            MeshKit k = new MeshKit();
            k.Append(Shapes.Lathe(new List<Vector2> { new Vector2(0f, -0.06f), new Vector2(0.045f, 0.02f), new Vector2(0.05f, 0.07f), new Vector2(0f, 0.08f) }, 10, new Color(1f, 0.5f, 0.1f)));
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 90f + (float)r.NextDouble() * 30f, 0f) * new Vector3(0.25f, 1f, 0f);
                k.Append(Shapes.LeafStrip(d, 0.32f, 0.035f, 0.25f, new Color(0.25f, 0.55f, 0.15f), new Color(0.45f, 0.8f, 0.25f), 4), new Vector3(0f, 0.07f, 0f), Quaternion.identity, Vector3.one);
            }
            k.WindByHeight(0f, 0.4f);
            return k;
        }

        static void BuildGarden()
        {
            Transform garden = Group("VegetableGarden", envRoot, Vector3.zero);
            Mesh bed = Cached("GardenBed", () => Shapes.RoundedBox(new Vector3(8f, 0.1f, 1.4f), 0.05f, Color.white, 1f));
            MeshKit plants = new MeshKit();
            MeshKit[] cab = { Cabbage(1), Cabbage(2), Cabbage(3) };
            MeshKit[] car = { Carrot(4), Carrot(5), Carrot(6) };
            for (int b = 0; b < 3; b++)
            {
                float z = Garden.y + 1.2f + b * 3f;
                float cxm = Garden.center.x;
                Obj("Bed" + b, garden, bed, mSoil, new Vector3(cxm, 0.05f, z), Vector3.zero, null, false);
                if (b == 1)
                {
                    for (float x = Garden.xMin + 0.8f; x < Garden.xMax - 0.6f; x += 0.42f)
                        for (int row = -1; row <= 1; row += 2)
                            plants.Append(car[Mathf.Abs((int)(x * 10f)) % 3], new Vector3(x, 0.1f, z + row * 0.3f), Quaternion.Euler(0f, x * 97f, 0f), Vector3.one);
                }
                else
                {
                    for (float x = Garden.xMin + 0.9f; x < Garden.xMax - 0.6f; x += 0.85f)
                        plants.Append(cab[Mathf.Abs((int)(x * 3f)) % 3], new Vector3(x, 0.1f, z), Quaternion.Euler(0f, x * 131f, 0f), Vector3.one * (0.9f + Mathf.Abs(Mathf.Sin(x * 7f)) * 0.3f));
                }
            }
            Obj("GardenPlants", garden, RatGen.Mesh(plants, "RS_GardenPlants"), mVeg, Vector3.zero, Vector3.zero);
            GameObject coverGo = new GameObject("GardenCover");
            coverGo.transform.SetParent(garden, false);
            coverGo.transform.position = new Vector3(Garden.center.x, 0f, Garden.center.y);
            RatCover gc = coverGo.AddComponent<RatCover>();
            gc.soft = true;
            gc.softHalfSize = new Vector2(Garden.width * 0.45f, Garden.height * 0.45f);
            if (planter != null) planter.clearRects.Add(new Vector4(Garden.xMin, Garden.yMin, Garden.xMax, Garden.yMax));

            BuildScarecrow(garden, new Vector3(Garden.center.x, 0f, Garden.y + 2.7f));
        }

        static void BuildScarecrow(Transform parent, Vector3 pos)
        {
            Transform s = Group("Scarecrow", parent, pos, 15f);
            Obj("Post", s, Cached("ScarePost", () => Shapes.RoundedBox(new Vector3(0.12f, 2.2f, 0.12f), 0.04f, Color.white)), mWoodDark, new Vector3(0f, 1.1f, 0f), Vector3.zero);
            Obj("Bar", s, Cached("ScareBar", () => Shapes.RoundedBox(new Vector3(1.6f, 0.1f, 0.1f), 0.04f, Color.white)), mWoodDark, new Vector3(0f, 1.62f, 0f), Vector3.zero);
            Obj("Shirt", s, Cached("ScareShirt", () => Shapes.RoundedBox(new Vector3(0.62f, 0.75f, 0.34f), 0.15f, Color.white, 1f)), mPlaid, new Vector3(0f, 1.42f, 0f), Vector3.zero);
            Mesh sleeve = Cached("ScareSleeve", () => Shapes.Tube(new List<Vector3> { Vector3.zero, new Vector3(0.6f, -0.03f, 0f) }, 0.13f, 0.11f, 10, Color.white));
            Obj("SleeveL", s, sleeve, mPlaid, new Vector3(-0.25f, 1.62f, 0f), new Vector3(0f, 180f, 0f));
            Obj("SleeveR", s, sleeve, mPlaid, new Vector3(0.25f, 1.62f, 0f), Vector3.zero);
            Mesh straw = Cached("StrawTuft", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.09f, 0.02f), new Vector2(0.03f, 0.2f), new Vector2(0f, 0.21f) }, 8, Color.white));
            Obj("StrawL", s, straw, mHay, new Vector3(-0.88f, 1.6f, 0f), new Vector3(0f, 0f, 90f));
            Obj("StrawR", s, straw, mHay, new Vector3(0.88f, 1.6f, 0f), new Vector3(0f, 0f, -90f));
            Obj("Head", s, Cached("ScareHead", () => Shapes.Blob(new Vector3(0.24f, 0.26f, 0.22f), 12, 0.08f, Color.white)), mBurlap, new Vector3(0f, 2.05f, 0f), Vector3.zero);
            Mesh button = Cached("ScareEye", () => Shapes.Ellipsoid(new Vector3(0.045f, 0.045f, 0.02f), 10, 6, Color.white));
            Obj("EyeL", s, button, mDark, new Vector3(-0.08f, 2.1f, -0.2f), Vector3.zero);
            Obj("EyeR", s, button, mDark, new Vector3(0.08f, 2.1f, -0.2f), Vector3.zero);
            List<Vector3> smile = new List<Vector3>();
            for (int i = 0; i <= 8; i++) { float f = i / 8f; smile.Add(new Vector3(Mathf.Lerp(-0.11f, 0.11f, f), 1.98f - Mathf.Sin(f * Mathf.PI) * 0.04f, -0.205f + Mathf.Sin(f * Mathf.PI) * -0.01f)); }
            Obj("Smile", s, RatGen.Mesh(Shapes.Tube(smile, 0.012f, 0.012f, 5, Color.white), "RS_ScareSmile"), mDark, Vector3.zero, Vector3.zero);
            Obj("Nose", s, Cached("ScareNose", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.035f, 0.01f), new Vector2(0f, 0.12f) }, 8, Color.white)), mPumpkin, new Vector3(0f, 2.05f, -0.21f), new Vector3(-90f, 0f, 0f));
            Obj("HatBrim", s, Cached("HatBrim", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.46f, -0.02f), new Vector2(0.48f, 0.02f), new Vector2(0f, 0.04f) }, 20, Color.white)), mHay, new Vector3(0f, 2.24f, 0f), new Vector3(-8f, 0f, 6f));
            Obj("HatCrown", s, Cached("HatCrown", () => Shapes.RoundedCylinder(0.22f, 0.26f, 0.08f, 16, Color.white)), mHay, new Vector3(0f, 2.25f, 0f), new Vector3(-8f, 0f, 6f));
            Obj("HatBand", s, Cached("HatBand", () => Shapes.Torus(0.225f, 0.025f, 20, 5, Color.white)), mRed, new Vector3(0f, 2.29f, 0f), new Vector3(-8f, 0f, 6f));
            Box(s.gameObject, new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 2.4f, 0.3f));
            Surface(s.gameObject, SurfaceKind.Hay);
        }

        static MeshKit CornPlant(System.Random r)
        {
            MeshKit k = new MeshKit();
            float h = 1.55f + (float)r.NextDouble() * 0.6f;
            List<Vector3> stalk = new List<Vector3>();
            float lean = ((float)r.NextDouble() - 0.5f) * 0.15f;
            for (int i = 0; i <= 4; i++) { float f = i / 4f; stalk.Add(new Vector3(lean * f * f, h * f, 0f)); }
            k.Append(Shapes.Tube(stalk, 0.035f, 0.018f, 6, new Color(0.45f, 0.68f, 0.25f), false));
            int leaves = 6 + r.Next(3);
            for (int i = 0; i < leaves; i++)
            {
                float y = h * (0.15f + 0.65f * i / leaves);
                Vector3 d = Quaternion.Euler(0f, i * 137f + (float)r.NextDouble() * 30f, 0f) * new Vector3(1f, 0.9f, 0f);
                Color root = new Color(0.32f, 0.6f, 0.2f), tip = Color.Lerp(new Color(0.55f, 0.82f, 0.3f), new Color(0.8f, 0.8f, 0.35f), (float)r.NextDouble() * 0.4f);
                k.Append(Shapes.LeafStrip(d, 0.55f + (float)r.NextDouble() * 0.25f, 0.06f, 0.5f, root, tip, 5), new Vector3(0f, y, 0f), Quaternion.identity, Vector3.one);
            }
            int cobs = 1 + r.Next(2);
            for (int i = 0; i < cobs; i++)
            {
                float y = h * (0.45f + i * 0.15f);
                Quaternion q = Quaternion.Euler(-25f, i * 160f + 40f, 0f);
                Vector3 off = q * new Vector3(0f, 0f, 0.07f);
                k.Append(Shapes.Ellipsoid(new Vector3(0.05f, 0.05f, 0.13f), 7, 5, new Color(1f, 0.85f, 0.25f), 0.3f), new Vector3(0f, y, 0f) + off, q, Vector3.one);
                k.Append(Shapes.Ellipsoid(new Vector3(0.06f, 0.055f, 0.12f), 7, 5, new Color(0.5f, 0.75f, 0.3f), 0.2f), new Vector3(0f, y, 0f) + off * 0.8f + Vector3.down * 0.02f, q * Quaternion.Euler(8f, 0f, 0f), new Vector3(1.05f, 1.05f, 0.8f));
            }
            for (int i = 0; i < 5; i++)
                k.Append(Shapes.LeafStrip(Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0.4f, 1f, 0f), 0.22f, 0.012f, 0.3f, new Color(0.85f, 0.75f, 0.4f), new Color(0.95f, 0.85f, 0.5f), 3), stalk[4], Quaternion.identity, Vector3.one);
            k.WindByHeight(0f, h);
            return k;
        }

        static void BuildCorn()
        {
            Transform corn = Group("CornPatch", envRoot, Vector3.zero);
            System.Random r = new System.Random(42);
            MeshKit[] variants = new MeshKit[6];
            for (int i = 0; i < variants.Length; i++) variants[i] = CornPlant(r);
            MeshKit field = new MeshKit();
            for (float x = CornPatch.xMin + 0.4f; x < CornPatch.xMax; x += 0.95f)
                for (float z = CornPatch.yMin + 0.4f; z < CornPatch.yMax; z += 0.85f)
                {
                    Vector3 p = new Vector3(x + ((float)r.NextDouble() - 0.5f) * 0.3f, 0f, z + ((float)r.NextDouble() - 0.5f) * 0.25f);
                    field.Append(variants[r.Next(variants.Length)], p, Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f), Vector3.one * (0.9f + (float)r.NextDouble() * 0.25f));
                }
            Material cornMat = RatMats.Toon("RS_CornField", Color.white, new RatMats.ToonOpts { spec = 0.1f, rim = 0.3f, wind = 0.12f, twoSided = true, flipBackNormals = false, rampSmooth = 0.4f, rampOffset = -0.1f });
            Obj("CornStalks", corn, RatGen.Mesh(field, "RS_CornField"), cornMat, Vector3.zero, Vector3.zero);
            GameObject cv = new GameObject("CornCover");
            cv.transform.SetParent(corn, false);
            cv.transform.position = new Vector3(CornPatch.center.x, 0f, CornPatch.center.y);
            RatCover rc = cv.AddComponent<RatCover>();
            rc.soft = true;
            rc.softHalfSize = new Vector2(CornPatch.width * 0.45f, CornPatch.height * 0.45f);
            if (planter != null) planter.clearRects.Add(new Vector4(CornPatch.xMin, CornPatch.yMin, CornPatch.xMax, CornPatch.yMax));
        }

        static MeshKit PumpkinMesh(float size)
        {
            List<Vector2> prof = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float a = -Mathf.PI * 0.5f + i / 12f * Mathf.PI;
                prof.Add(new Vector2(Mathf.Max(0.001f, Mathf.Cos(a)) * 0.42f * size, (Mathf.Sin(a) * 0.3f + 0.3f) * size));
            }
            prof[0] = new Vector2(0f, 0.02f * size);
            prof[prof.Count - 1] = new Vector2(0f, 0.56f * size);
            MeshKit k = Shapes.Lathe(prof, 36, Color.white);
            // ribs
            for (int i = 0; i < k.v.Count; i++)
            {
                Vector3 p = k.v[i];
                float a = Mathf.Atan2(p.z, p.x);
                float rib = 1f - 0.09f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 3f);
                k.v[i] = new Vector3(p.x * rib, p.y, p.z * rib);
                float sh = Mathf.Lerp(0.82f, 1f, rib);
                k.c[i] = new Color(sh, sh, sh, 1f);
            }
            k.SmoothNormals();
            return k;
        }

        static void BuildPumpkins()
        {
            Transform patch = Group("PumpkinPatch", envRoot, Vector3.zero);
            Vector4[] ps = { new Vector4(9.6f, 0f, 8.4f, 1.15f), new Vector4(11.2f, 0f, 9.3f, 0.85f), new Vector4(12.8f, 0f, 8.2f, 1.3f), new Vector4(10.4f, 0f, 11f, 0.95f), new Vector4(13.6f, 0f, 10.8f, 1f), new Vector4(14.6f, 0f, 9f, 0.75f), new Vector4(12f, 0f, 12.4f, 0.8f) };
            Mesh stem = Cached("PumpkinStem", () => Shapes.Tube(new List<Vector3> { Vector3.zero, new Vector3(0.02f, 0.08f, 0f), new Vector3(0.06f, 0.13f, 0.01f) }, 0.04f, 0.025f, 6, Color.white));
            Material stemMat = Toon("RS_Stem", new Color(0.45f, 0.4f, 0.2f));
            MeshKit vines = new MeshKit();
            for (int i = 0; i < ps.Length; i++)
            {
                Vector3 p = new Vector3(ps[i].x, 0f, ps[i].z);
                float s = ps[i].w;
                Transform t = Group("Pumpkin", patch, p, i * 53f);
                Obj("Body", t, Cached("Pumpkin", () => PumpkinMesh(1f)), mPumpkin, Vector3.zero, Vector3.zero, Vector3.one * s);
                Obj("Stem", t, stem, stemMat, new Vector3(0f, 0.53f * s, 0f), Vector3.zero, Vector3.one * s);
                SphereCollider c = t.gameObject.AddComponent<SphereCollider>();
                c.center = new Vector3(0f, 0.28f * s, 0f);
                c.radius = 0.38f * s;
                Surface(t.gameObject, SurfaceKind.Leaves);
                if (s > 0.9f) Cover(t, 0.45f * s);
                KeepClear(p, 0.45f * s);
                if (i > 0)
                {
                    Vector3 a = new Vector3(ps[i - 1].x, 0.03f, ps[i - 1].z), b = p + Vector3.up * 0.03f;
                    List<Vector3> vine = new List<Vector3>();
                    for (int j = 0; j <= 8; j++) { float f = j / 8f; vine.Add(Vector3.Lerp(a, b, f) + Vector3.Cross(Vector3.up, (b - a).normalized) * Mathf.Sin(f * Mathf.PI * 2f) * 0.25f); }
                    vines.Append(Shapes.Tube(vine, 0.025f, 0.025f, 5, new Color(0.35f, 0.6f, 0.2f), false));
                    for (int j = 1; j < 8; j += 2)
                        vines.Append(Shapes.Ellipsoid(new Vector3(0.22f, 0.025f, 0.2f), 10, 5, new Color(0.3f, 0.62f, 0.22f), 0f, 0.15f, j + i * 10), vine[j] + Vector3.up * 0.06f, Quaternion.Euler(-10f, j * 70f, 8f), Vector3.one);
                }
            }
            Obj("Vines", patch, RatGen.Mesh(vines, "RS_PumpkinVines"), mVeg, Vector3.zero, Vector3.zero, null, false);
        }

        // ------------------------------------------------------------------ props

        static void HayRound(Transform parent, Vector3 pos, float yaw, bool upright)
        {
            Transform t = Group("HayBale", parent, pos, yaw);
            Mesh m = Cached("HayRound", () => Shapes.RoundedCylinder(0.75f, 1.2f, 0.2f, 28, Color.white, 2f));
            if (upright) Obj("Bale", t, m, mHay, Vector3.zero, Vector3.zero);
            else Obj("Bale", t, m, mHay, new Vector3(-0.6f, 0.75f, 0f), new Vector3(0f, 0f, -90f));
            Mesh band = Cached("HayBand", () => Shapes.Torus(0.76f, 0.02f, 36, 4, Color.white));
            if (!upright)
            {
                Obj("Band1", t, band, mRed, new Vector3(-0.3f, 0.75f, 0f), new Vector3(0f, 0f, 90f));
                Obj("Band2", t, band, mRed, new Vector3(0.3f, 0.75f, 0f), new Vector3(0f, 0f, 90f));
                Box(t.gameObject, new Vector3(0f, 0.75f, 0f), new Vector3(1.2f, 1.5f, 1.45f));
            }
            else Box(t.gameObject, new Vector3(0f, 0.6f, 0f), new Vector3(1.45f, 1.2f, 1.45f));
            Surface(t.gameObject, SurfaceKind.Hay);
            Cover(t, 0.85f);
            KeepClear(pos, 1.1f);
        }

        static void HayStack(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Group("HayStack", parent, pos, yaw);
            Mesh m = Cached("HaySquare", () => Shapes.RoundedBox(new Vector3(1.1f, 0.5f, 0.55f), 0.1f, Color.white, 1.5f));
            Vector3[] spots = { new Vector3(-0.58f, 0.25f, 0f), new Vector3(0.58f, 0.25f, 0f), new Vector3(0f, 0.25f, 0.62f), new Vector3(0f, 0.75f, 0.2f), new Vector3(-0.3f, 0.75f, -0.05f) };
            for (int i = 0; i < spots.Length; i++)
                Obj("Bale", t, m, mHay, spots[i], new Vector3(0f, i * 9f - 12f, 0f));
            Box(t.gameObject, new Vector3(0f, 0.5f, 0.3f), new Vector3(2.2f, 1f, 1.3f));
            Surface(t.gameObject, SurfaceKind.Hay);
            Cover(t, 1.1f);
            KeepClear(pos + t.rotation * new Vector3(0f, 0f, 0.3f), 1.3f);
        }

        static List<Vector2> BarrelProfile()
        {
            List<Vector2> p = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.3f, 0f) };
            for (int i = 0; i <= 8; i++) { float f = i / 8f; p.Add(new Vector2(0.3f + Mathf.Sin(f * Mathf.PI) * 0.06f, 0.02f + f * 0.86f)); }
            p.Add(new Vector2(0.26f, 0.9f));
            p.Add(new Vector2(0.25f, 0.86f));
            p.Add(new Vector2(0f, 0.86f));
            return p;
        }

        static void Barrel(Transform parent, Vector3 pos, float yaw, bool tipped)
        {
            Transform t = Group("Barrel", parent, pos, yaw);
            Transform body = Group("Body", t, tipped ? new Vector3(0f, 0.34f, -0.45f) : Vector3.zero);
            if (tipped) body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Obj("Staves", body, Cached("Barrel", () => Shapes.Lathe(BarrelProfile(), 24, Color.white, 1f, 6f)), mWood, Vector3.zero, Vector3.zero);
            Mesh hoop = Cached("BarrelHoop", () => Shapes.Torus(0.33f, 0.022f, 28, 5, Color.white));
            Mesh hoopMid = Cached("BarrelHoopMid", () => Shapes.Torus(0.36f, 0.022f, 28, 5, Color.white));
            Obj("Hoop", body, hoop, mDark, new Vector3(0f, 0.12f, 0f), Vector3.zero);
            Obj("Hoop", body, hoopMid, mDark, new Vector3(0f, 0.36f, 0f), Vector3.zero);
            Obj("Hoop", body, hoop, mDark, new Vector3(0f, 0.76f, 0f), Vector3.zero);
            if (tipped) Box(t.gameObject, new Vector3(0f, 0.34f, 0f), new Vector3(0.72f, 0.68f, 0.9f));
            else
            {
                CapsuleCollider c = t.gameObject.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0f, 0.45f, 0f);
                c.radius = 0.36f;
                c.height = 0.9f;
            }
            Surface(t.gameObject, SurfaceKind.Wood);
            Cover(t, tipped ? 0.55f : 0.4f);
            KeepClear(pos, 0.55f);
        }

        static void Crate(Transform parent, Vector3 pos, float yaw, float size)
        {
            Transform t = Group("Crate", parent, pos, yaw);
            Obj("Box", t, Cached("Crate", () => Shapes.RoundedBox(new Vector3(0.8f, 0.8f, 0.8f), 0.05f, Color.white, 1.2f)), mWood, new Vector3(0f, 0.4f * size, 0f), Vector3.zero, Vector3.one * size);
            MeshKit frame = new MeshKit();
            MeshKit edge = Shapes.RoundedBox(new Vector3(0.84f, 0.1f, 0.1f), 0.03f, Color.white, 1f, 1);
            for (int f = 0; f < 4; f++)
            {
                Quaternion q = Quaternion.Euler(0f, f * 90f, 0f);
                frame.Append(edge, q * new Vector3(0f, 0.37f, 0.37f), q, Vector3.one);
                frame.Append(edge, q * new Vector3(0f, -0.37f, 0.37f), q, Vector3.one);
                frame.Append(edge, q * new Vector3(0.37f, 0f, 0.37f), q * Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                frame.Append(Shapes.RoundedBox(new Vector3(1.0f, 0.09f, 0.06f), 0.03f, Color.white, 1f, 1), q * new Vector3(0f, 0f, 0.405f), q * Quaternion.Euler(0f, 0f, 45f), Vector3.one);
            }
            Obj("Frame", t, Cached("CrateFrame", () => frame), mWoodDark, new Vector3(0f, 0.4f * size, 0f), Vector3.zero, Vector3.one * size);
            Box(t.gameObject, new Vector3(0f, 0.4f * size, 0f), Vector3.one * 0.82f * size);
            Surface(t.gameObject, SurfaceKind.Wood);
            Cover(t, 0.5f * size);
            KeepClear(pos, 0.6f * size);
        }

        static void Trough(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Group("WaterTrough", parent, pos, yaw);
            MeshKit k = new MeshKit();
            k.Append(Shapes.RoundedBox(new Vector3(2.2f, 0.1f, 0.8f), 0.04f, Color.white), new Vector3(0f, 0.12f, 0f), Quaternion.identity, Vector3.one);
            k.Append(Shapes.RoundedBox(new Vector3(2.2f, 0.6f, 0.1f), 0.04f, Color.white), new Vector3(0f, 0.4f, 0.37f), Quaternion.identity, Vector3.one);
            k.Append(Shapes.RoundedBox(new Vector3(2.2f, 0.6f, 0.1f), 0.04f, Color.white), new Vector3(0f, 0.4f, -0.37f), Quaternion.identity, Vector3.one);
            k.Append(Shapes.RoundedBox(new Vector3(0.1f, 0.6f, 0.8f), 0.04f, Color.white), new Vector3(1.05f, 0.4f, 0f), Quaternion.identity, Vector3.one);
            k.Append(Shapes.RoundedBox(new Vector3(0.1f, 0.6f, 0.8f), 0.04f, Color.white), new Vector3(-1.05f, 0.4f, 0f), Quaternion.identity, Vector3.one);
            for (int s = -1; s <= 1; s += 2)
                k.Append(Shapes.RoundedBox(new Vector3(0.12f, 0.16f, 0.9f), 0.04f, Color.white), new Vector3(0.85f * s, 0.06f, 0f), Quaternion.identity, Vector3.one);
            Obj("Trough", t, RatGen.Mesh(k, "RS_Trough"), mWoodDark, Vector3.zero, Vector3.zero);
            Obj("Water", t, Cached("TroughWater", () => Shapes.RoundedBox(new Vector3(2f, 0.04f, 0.62f), 0.02f, Color.white)), mWater, new Vector3(0f, 0.58f, 0f), Vector3.zero, null, false);
            Box(t.gameObject, new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.7f, 0.84f));
            Surface(t.gameObject, SurfaceKind.Wood);
            Cover(t, 0.9f);
            KeepClear(pos, 1.3f);
        }

        static void Wheelbarrow(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Group("Wheelbarrow", parent, pos, yaw);
            MeshKit tray = new MeshKit();
            tray.Append(Shapes.RoundedBox(new Vector3(0.7f, 0.08f, 0.9f), 0.04f, Color.white), new Vector3(0f, 0.5f, 0f), Quaternion.identity, Vector3.one);
            tray.Append(Shapes.RoundedBox(new Vector3(0.08f, 0.35f, 0.95f), 0.03f, Color.white), new Vector3(0.38f, 0.68f, 0f), Quaternion.Euler(0f, 0f, 12f), Vector3.one);
            tray.Append(Shapes.RoundedBox(new Vector3(0.08f, 0.35f, 0.95f), 0.03f, Color.white), new Vector3(-0.38f, 0.68f, 0f), Quaternion.Euler(0f, 0f, -12f), Vector3.one);
            tray.Append(Shapes.RoundedBox(new Vector3(0.8f, 0.35f, 0.08f), 0.03f, Color.white), new Vector3(0f, 0.68f, 0.5f), Quaternion.Euler(-15f, 0f, 0f), Vector3.one);
            tray.Append(Shapes.RoundedBox(new Vector3(0.8f, 0.35f, 0.08f), 0.03f, Color.white), new Vector3(0f, 0.68f, -0.45f), Quaternion.Euler(12f, 0f, 0f), Vector3.one);
            Obj("Tray", t, RatGen.Mesh(tray, "RS_BarrowTray"), mRed, Vector3.zero, Vector3.zero);
            MeshKit frame = new MeshKit();
            for (int s = -1; s <= 1; s += 2)
            {
                frame.Append(Shapes.Tube(new List<Vector3> { new Vector3(0.25f * s, 0.32f, 0.6f), new Vector3(0.28f * s, 0.45f, 0f), new Vector3(0.32f * s, 0.6f, -1.0f) }, 0.03f, 0.03f, 6, Color.white));
                frame.Append(Shapes.Tube(new List<Vector3> { new Vector3(0.28f * s, 0.45f, -0.35f), new Vector3(0.28f * s, 0f, -0.4f) }, 0.025f, 0.025f, 6, Color.white));
            }
            Obj("Frame", t, RatGen.Mesh(frame, "RS_BarrowFrame"), mDark, Vector3.zero, Vector3.zero);
            Obj("Handles", t, Cached("BarrowGrip", () => Shapes.RoundedCylinder(0.045f, 0.2f, 0.02f, 8, Color.white)), mWoodDark, new Vector3(0.32f, 0.6f, -1.05f), new Vector3(90f, 0f, 0f));
            Obj("Handles", t, Cached("BarrowGrip", () => Shapes.RoundedCylinder(0.045f, 0.2f, 0.02f, 8, Color.white)), mWoodDark, new Vector3(-0.32f, 0.6f, -1.05f), new Vector3(90f, 0f, 0f));
            Obj("Tyre", t, Cached("BarrowWheel", () => Shapes.Torus(0.2f, 0.07f, 24, 10, Color.white)), mDark, new Vector3(0f, 0.27f, 0.62f), new Vector3(0f, 0f, 90f));
            Obj("Hub", t, Cached("BarrowHub", () => Shapes.Sphere(0.08f, 10, 8, Color.white)), mRed, new Vector3(0f, 0.27f, 0.62f), Vector3.zero, new Vector3(1.4f, 1f, 1f));
            Obj("Dirt", t, Cached("BarrowDirt", () => Shapes.Blob(new Vector3(0.33f, 0.12f, 0.42f), 41, 0.25f, Color.white)), mDirt, new Vector3(0f, 0.66f, 0f), Vector3.zero);
            Box(t.gameObject, new Vector3(0f, 0.45f, -0.1f), new Vector3(0.9f, 0.9f, 1.6f));
            Surface(t.gameObject, SurfaceKind.Metal);
            Cover(t, 0.8f);
            KeepClear(pos, 1f);
        }

        static void Sack(Transform parent, Vector3 pos, float yaw, int seed)
        {
            Transform t = Group("GrainSack", parent, pos, yaw);
            Obj("Sack", t, Cached("Sack" + seed % 2, () => Shapes.Blob(new Vector3(0.32f, 0.38f, 0.26f), 60 + seed % 2, 0.12f, Color.white)), mBurlap, new Vector3(0f, 0.36f, 0f), Vector3.zero);
            Obj("Neck", t, Cached("SackNeck", () => Shapes.Blob(new Vector3(0.1f, 0.12f, 0.09f), 66, 0.2f, Color.white)), mBurlap, new Vector3(0f, 0.78f, 0f), Vector3.zero);
            Obj("Tie", t, Cached("SackTie", () => Shapes.Torus(0.08f, 0.018f, 16, 4, Color.white)), mRed, new Vector3(0f, 0.72f, 0f), Vector3.zero);
            CapsuleCollider c = t.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 0.4f, 0f);
            c.radius = 0.3f;
            c.height = 0.85f;
            Surface(t.gameObject, SurfaceKind.Hay);
            Cover(t, 0.35f);
            KeepClear(pos, 0.45f);
        }

        static void LogPile(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Group("LogPile", parent, pos, yaw);
            Mesh log = Cached("Log", () => Shapes.RoundedCylinder(0.17f, 1.6f, 0.05f, 14, Color.white));
            Mesh ring = Cached("LogEnd", () => Shapes.Disc(0.155f, 14, new Color(1f, 0.92f, 0.7f), new Color(0.85f, 0.7f, 0.45f)));
            Material endMat = Toon("RS_LogEnd", Color.white);
            int[] rows = { 4, 3, 2 };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r]; i++)
                {
                    float x = (i - (rows[r] - 1) * 0.5f) * 0.35f;
                    float y = 0.17f + r * 0.3f;
                    Obj("Log", t, log, mBark, new Vector3(x, y, -0.8f), new Vector3(90f, 0f, 0f));
                    Obj("End", t, ring, endMat, new Vector3(x, y, -0.805f), new Vector3(-90f, 0f, 0f), null, false);
                }
            Box(t.gameObject, new Vector3(0f, 0.45f, 0f), new Vector3(1.45f, 0.9f, 1.6f));
            Surface(t.gameObject, SurfaceKind.Wood);
            Cover(t, 0.9f);
            KeepClear(pos, 1.1f);
        }

        static void Well(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Group("Well", parent, pos, yaw);
            List<Vector2> wall = new List<Vector2> { new Vector2(0.55f, 0.85f), new Vector2(0.55f, 0f), new Vector2(0.85f, 0f), new Vector2(0.88f, 0.8f), new Vector2(0.8f, 0.9f), new Vector2(0.6f, 0.9f) };
            Obj("Wall", t, Cached("WellWall", () => Shapes.Lathe(wall, 24, Color.white, 1f, 4f)), mStone, Vector3.zero, Vector3.zero);
            Obj("Water", t, Cached("WellWater", () => Shapes.Disc(0.56f, 20, Color.white, Color.white)), mWater, new Vector3(0f, 0.55f, 0f), Vector3.zero, null, false);
            Mesh post = Cached("WellPost", () => Shapes.RoundedBox(new Vector3(0.12f, 1.9f, 0.12f), 0.04f, Color.white));
            Obj("PostL", t, post, mWoodDark, new Vector3(-0.75f, 0.95f, 0f), Vector3.zero);
            Obj("PostR", t, post, mWoodDark, new Vector3(0.75f, 0.95f, 0f), Vector3.zero);
            Mesh slab = Cached("WellRoofSlab", () => Shapes.RoundedBox(new Vector3(2.1f, 0.07f, 0.75f), 0.03f, Color.white, 1f));
            Obj("RoofA", t, slab, mRoof, new Vector3(0f, 2.08f, 0.27f), new Vector3(36f, 0f, 0f));
            Obj("RoofB", t, slab, mRoof, new Vector3(0f, 2.08f, -0.27f), new Vector3(-36f, 0f, 0f));
            Obj("Axle", t, Cached("WellAxle", () => Shapes.RoundedCylinder(0.06f, 1.5f, 0.02f, 10, Color.white)), mWood, new Vector3(-0.75f, 1.45f, 0f), new Vector3(0f, 0f, -90f));
            Obj("Bucket", t, Cached("Bucket", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.17f, 0.25f), new Vector2(0.15f, 0.25f), new Vector2(0f, 0.04f) }, 14, Color.white)), mMetal, new Vector3(0f, 1.05f, 0f), Vector3.zero);
            CapsuleCollider c = t.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 0.45f, 0f);
            c.radius = 0.88f;
            c.height = 0.9f;
            Surface(t.gameObject, SurfaceKind.Stone);
            Cover(t, 0.95f);
            KeepClear(pos, 1.2f);
        }

        static void Sunflower(Transform parent, Vector3 pos, float h, int seed)
        {
            MeshKit k = new MeshKit();
            k.Append(Shapes.Tube(new List<Vector3> { Vector3.zero, new Vector3(0.05f, h * 0.5f, 0f), new Vector3(0f, h, 0.05f) }, 0.035f, 0.025f, 6, new Color(0.35f, 0.6f, 0.2f), false));
            for (int i = 0; i < 3; i++)
                k.Append(Shapes.LeafStrip(Quaternion.Euler(0f, i * 120f + seed * 20f, 0f) * new Vector3(1f, 0.3f, 0f), 0.35f, 0.1f, 0.4f, new Color(0.3f, 0.58f, 0.2f), new Color(0.45f, 0.75f, 0.28f), 4), new Vector3(0f, h * (0.3f + i * 0.18f), 0f), Quaternion.identity, Vector3.one);
            Vector3 head = new Vector3(0f, h, 0.05f);
            Quaternion face = Quaternion.Euler(-70f, 180f + seed * 10f, 0f);
            for (int i = 0; i < 14; i++)
            {
                Quaternion q = face * Quaternion.Euler(0f, i * 25.7f, 0f);
                k.Append(Shapes.Ellipsoid(new Vector3(0.05f, 0.012f, 0.12f), 8, 4, new Color(1f, 0.82f, 0.1f)), head + q * new Vector3(0f, 0f, 0.15f), q, Vector3.one);
            }
            k.Append(Shapes.Ellipsoid(new Vector3(0.12f, 0.04f, 0.12f), 14, 6, new Color(0.45f, 0.25f, 0.1f)), head + face * new Vector3(0f, 0.01f, 0f), face, Vector3.one);
            k.WindByHeight(0f, h * 1.6f);
            Obj("Sunflower", parent, RatGen.Mesh(k, "RS_Sunflower" + seed), mVeg, pos, new Vector3(0f, seed * 30f, 0f));
        }

        static void BuildProps()
        {
            Transform p = propsRoot;
            HayRound(p, new Vector3(-3.6f, 0f, 23.4f), 20f, false);
            HayRound(p, new Vector3(-1.8f, 0f, 25.2f), 75f, false);
            HayRound(p, new Vector3(-13.4f, 0f, 21f), 0f, true);
            HayStack(p, new Vector3(12.4f, 0f, 3.6f), -25f);
            Barrel(p, new Vector3(-5.3f, 0f, 5.8f), 0f, false);
            Barrel(p, new Vector3(-4.4f, 0f, 6.6f), 40f, false);
            Barrel(p, new Vector3(-6.1f, 0f, 7.0f), 70f, true);
            Barrel(p, new Vector3(16.6f, 0f, 24f), 10f, false);
            Crate(p, new Vector3(6.2f, 0f, 7.2f), 12f, 1f);
            Crate(p, new Vector3(7.1f, 0f, 7.6f), -8f, 0.85f);
            Crate(p, new Vector3(6.6f, 0.8f, 7.35f), 30f, 0.75f);
            Crate(p, new Vector3(-9.8f, 0f, 29f), 5f, 1f);
            Trough(p, new Vector3(2.8f, 0f, 18.4f), 12f);
            Wheelbarrow(p, new Vector3(-9.2f, 0f, 20.5f), 35f);
            Sack(p, new Vector3(5.2f, 0f, 26.2f), 0f, 0);
            Sack(p, new Vector3(5.9f, 0f, 26.8f), 40f, 1);
            Sack(p, new Vector3(4.6f, 0f, 27f), -30f, 1);
            LogPile(p, new Vector3(-15.2f, 0f, 28.6f), 15f);
            Well(p, new Vector3(14.6f, 0f, 14.6f), 25f);

            Transform b = Group("Bushes", p, Vector3.zero);
            Bush(b, new Vector3(-17.2f, 0f, 4.2f), 1.1f, 1, true);
            Bush(b, new Vector3(-17.6f, 0f, 24.5f), 1.2f, 2, false);
            Bush(b, new Vector3(17.4f, 0f, 9f), 1f, 3, false);
            Bush(b, new Vector3(17.3f, 0f, 30.5f), 1.25f, 4, true);
            Bush(b, new Vector3(-4.5f, 0f, 32.8f), 1.1f, 5, false);
            Bush(b, new Vector3(3.6f, 0f, 32.6f), 1.15f, 6, true);
            Bush(b, new Vector3(9.4f, 0f, 19.6f), 0.85f, 7, false);
            Bush(b, new Vector3(-6.6f, 0f, 12.4f), 0.8f, 8, true);
            Bush(b, new Vector3(1.2f, 0f, 29.5f), 0.9f, 9, false);

            Transform r = Group("Rocks", p, Vector3.zero);
            Rock(r, new Vector3(-2.4f, 0f, 8.6f), 0.8f, 1);
            Rock(r, new Vector3(8.6f, 0f, 13.2f), 1f, 2);
            Rock(r, new Vector3(-12.6f, 0f, 25.6f), 1.2f, 3);
            Rock(r, new Vector3(15.4f, 0f, 20.8f), 0.9f, 4);
            Rock(r, new Vector3(-17f, 0f, 16f), 1.1f, 5);
            Rock(r, new Vector3(3.6f, 0f, 4.2f), 0.6f, 6);

            Transform sf = Group("Sunflowers", p, Vector3.zero);
            for (int i = 0; i < 6; i++) Sunflower(sf, new Vector3(-18.3f + (i % 2) * 0.4f, 0f, 5.6f + i * 0.9f), 1.6f + (i % 3) * 0.25f, i);
            for (int i = 0; i < 4; i++) Sunflower(sf, new Vector3(18.2f - (i % 2) * 0.4f, 0f, 3f + i * 0.85f), 1.5f + (i % 2) * 0.3f, i + 10);
        }

        // ------------------------------------------------------------------ burrows + food

        static void BuildBurrowsAndFood()
        {
            Transform holes = Group("MouseBurrows", envRoot, Vector3.zero);
            Vector3[] bp =
            {
                new Vector3(-18.1f, 0f, 10.5f), new Vector3(-12.4f, 0f, 32.6f), new Vector3(-2.2f, 0f, 33.2f), new Vector3(8.6f, 0f, 33.2f),
                new Vector3(18.1f, 0f, 21.4f), new Vector3(15.6f, 0f, 5.6f), new Vector3(-7.4f, 0f, 4.6f), new Vector3(0.8f, 0f, 21.6f),
                new Vector3(5.4f, 0f, 11.6f), new Vector3(-10.6f, 0f, 19.2f)
            };
            Mesh hole = Cached("BurrowHole", () => Shapes.Disc(0.2f, 16, Color.white, Color.white));
            Mesh mound = Cached("BurrowMound", () => Shapes.Torus(0.3f, 0.12f, 20, 8, Color.white));
            for (int i = 0; i < bp.Length; i++)
            {
                Transform h = Group("Burrow", holes, bp[i], i * 40f);
                Obj("Hole", h, hole, mBurrow, new Vector3(0f, 0.012f, 0f), Vector3.zero, null, false);
                Obj("Mound", h, mound, mDirt, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(1f, 0.55f, 1f));
                h.gameObject.AddComponent<MouseBurrow>();
                KeepClear(bp[i], 0.5f);
            }

            Transform food = Group("MouseFood", envRoot, Vector3.zero);
            Mesh cheese = Cached("Cheese", () => Shapes.Wedge(0.32f, 0.2f, 0.3f, Color.white));
            Mesh grain = Cached("GrainPile", () => Shapes.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.45f, 0f), new Vector2(0.3f, 0.07f), new Vector2(0.1f, 0.13f), new Vector2(0f, 0.14f) }, 18, Color.white, 2f, 3f));
            Mesh cob = Cached("CornCob", () => Shapes.Ellipsoid(new Vector3(0.06f, 0.06f, 0.16f), 12, 8, Color.white, 0.25f));
            Mesh apple = Cached("Apple", () => Shapes.Sphere(0.09f, 12, 8, Color.white));
            Material cobMat = Toon("RS_Corn", new Color(1f, 0.82f, 0.22f), null, 1f, 0.3f);
            Material appleMat = Toon("RS_Apple", new Color(0.92f, 0.18f, 0.14f), null, 1f, 0.5f, 0.35f, 60f);
            FoodSpot(food, "Cheese", cheese, mCheese, new Vector3(1.6f, 0f, 9.4f), 1.4f, new Vector3(0f, 25f, 0f));
            FoodSpot(food, "Cheese", cheese, mCheese, new Vector3(10.4f, 0f, 20.8f), 1.3f, new Vector3(0f, -40f, 0f));
            FoodSpot(food, "GrainPile", grain, mHay, new Vector3(-2.4f, 0f, 14.6f), 1f, Vector3.zero);
            FoodSpot(food, "SpilledGrain", grain, mHay, new Vector3(4.4f, 0f, 25.4f), 1.1f, Vector3.zero);
            FoodSpot(food, "CornCob", cob, cobMat, new Vector3(6.6f, 0.06f, 21.4f), 0.9f, new Vector3(0f, 60f, 90f));
            FoodSpot(food, "Apple", apple, appleMat, new Vector3(-7.6f, 0.09f, 26.4f), 0.8f, Vector3.zero);
            FoodSpot(food, "CarrotSnack", cob, Toon("RS_Carrot", new Color(1f, 0.5f, 0.1f)), new Vector3(-6.4f, 0.05f, 9.6f), 0.9f, new Vector3(0f, -20f, 90f));
            FoodSpot(food, "Cheese", cheese, mCheese, new Vector3(-12.6f, 0f, 18.6f), 1.2f, new Vector3(0f, 80f, 0f));
        }

        static void FoodSpot(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, float appeal, Vector3 euler)
        {
            GameObject go = Obj(name, parent, mesh, mat, pos, euler);
            go.layer = Layers.FX;   // decoration only: never blocks a shot
            MouseFood f = go.AddComponent<MouseFood>();
            f.appeal = appeal;
            KeepClear(pos, 0.45f);
        }

        // ------------------------------------------------------------------ sky life

        static void BuildClouds()
        {
            Transform clouds = Group("Clouds", envRoot, Vector3.zero);
            System.Random r = new System.Random(5);
            for (int i = 0; i < 12; i++)
            {
                MeshKit k = new MeshKit();
                int n = 4 + r.Next(4);
                for (int j = 0; j < n; j++)
                {
                    float s = 5f + (float)r.NextDouble() * 6f;
                    k.Append(Shapes.Blob(new Vector3(s, s * 0.7f, s * 0.8f), i * 10 + j, 0.12f, Color.white, 14, 10), new Vector3((j - n * 0.5f) * 5.5f + (float)r.NextDouble() * 3f, (float)r.NextDouble() * 3f + (j % 2) * 2f, (float)r.NextDouble() * 4f), Quaternion.identity, Vector3.one);
                }
                float a = Mathf.Lerp(-80f, 80f, (float)r.NextDouble()) * Mathf.Deg2Rad;
                float dist = 120f + (float)r.NextDouble() * 70f;
                Vector3 pos = new Vector3(Mathf.Sin(a) * dist, 48f + (float)r.NextDouble() * 30f, Mathf.Cos(a) * dist + 10f);
                GameObject c = Obj("Cloud", clouds, RatGen.Mesh(k, "RS_Cloud" + i), mCloud, pos, new Vector3(0f, (float)r.NextDouble() * 40f - 20f, 0f), null, false);
                c.GetComponent<MeshRenderer>().receiveShadows = false;
                CloudDrift d = c.AddComponent<CloudDrift>();
                d.velocity = new Vector3(0.8f + (float)r.NextDouble() * 1f, 0f, 0f);
                d.wrapX = 190f;
            }
        }

        static void BuildButterflies()
        {
            Transform bf = Group("Butterflies", envRoot, Vector3.zero);
            Color[] cols = { new Color(1f, 0.6f, 0.15f), new Color(0.45f, 0.7f, 1f), new Color(1f, 0.95f, 0.4f), new Color(1f, 0.5f, 0.75f) };
            Vector3[] homes = { new Vector3(-11f, 1.2f, 11f), new Vector3(12f, 1.1f, 10f), new Vector3(-17f, 1.4f, 7f), new Vector3(3f, 1.3f, 30f) };
            Mesh body = Cached("BflyBody", () => Shapes.Ellipsoid(new Vector3(0.012f, 0.012f, 0.05f), 8, 6, Color.white));
            for (int i = 0; i < homes.Length; i++)
            {
                Transform t = Group("Butterfly", bf, homes[i]);
                Obj("Body", t, body, mDark, Vector3.zero, Vector3.zero, null, false);
                MeshKit wing = new MeshKit();
                wing.Append(Shapes.Ellipsoid(new Vector3(0.07f, 0.004f, 0.05f), 10, 4, cols[i]), new Vector3(0.065f, 0f, 0.015f), Quaternion.identity, Vector3.one);
                wing.Append(Shapes.Ellipsoid(new Vector3(0.05f, 0.004f, 0.035f), 10, 4, Color.Lerp(cols[i], Color.white, 0.3f)), new Vector3(0.05f, 0f, -0.04f), Quaternion.identity, Vector3.one);
                Mesh wm = RatGen.Mesh(wing, "RS_BflyWing" + i);
                Transform wl = Group("WingL", t, Vector3.zero);
                Transform wr = Group("WingR", t, Vector3.zero);
                Obj("Wing", wl, wm, mLeafFlat2(), Vector3.zero, new Vector3(0f, 180f, 0f), new Vector3(1f, 1f, -1f), false);
                Obj("Wing", wr, wm, mLeafFlat2(), Vector3.zero, Vector3.zero, null, false);
                Butterfly b = t.gameObject.AddComponent<Butterfly>();
                b.wingL = wl;
                b.wingR = wr;
            }
        }

        static Material mLeafFlat2()
        {
            return RatMats.Toon("RS_ButterflyWing", Color.white, new RatMats.ToonOpts { twoSided = true, flipBackNormals = false, spec = 0.2f, rim = 0.4f, emission = new Color(0.15f, 0.15f, 0.15f) });
        }

        // ------------------------------------------------------------------ grass

        static void BuildGrass()
        {
            planter.material = mVeg;
            for (int i = 0; i < MainPath.Length; i++) planter.pathPoints.Add(MainPath[i]);
            for (int i = 0; i + 1 < BranchPath.Length; i++)
                for (float f = 0f; f < 1f; f += 0.15f)
                {
                    Vector2 q = Vector2.Lerp(BranchPath[i], BranchPath[i + 1], f);
                    planter.clearCircles.Add(new Vector3(q.x, q.y, 0.9f));
                }
            planter.clearRects.Add(new Vector4(-1.4f, -1f, 1.4f, 2.2f));   // porch steps
            planter.Plant();
        }

        // ------------------------------------------------------------------ gameplay objects

        static void BuildGameplay(Result res)
        {
            // player rig on the porch
            GameObject rig = new GameObject("Player");
            rig.transform.position = new Vector3(0f, 2.2f, -0.95f);
            Transform yaw = new GameObject("YawPivot").transform;
            yaw.SetParent(rig.transform, false);
            Transform pitch = new GameObject("PitchPivot").transform;
            pitch.SetParent(yaw, false);
            GameObject camGo = new GameObject("RatShooterCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pitch, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 450f;
            cam.fieldOfView = 62f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<RatPostFX>();
            res.cam = cam;

            Material flash = RatMats.Fx("RS_MuzzleFlash", Color.white, null, true);
            CartoonGun gun = GunFactory.Build(cam.transform, flash);
            res.gun = gun;
            RatShooterPlayer player = rig.AddComponent<RatShooterPlayer>();
            player.yawPivot = yaw;
            player.pitchPivot = pitch;
            player.cam = cam;
            player.gun = gun;

            // game root
            GameObject root = new GameObject("RatShooterGame");
            root.AddComponent<StandaloneSceneMarker>().levelName = "Rat Shooter";
            RatShooterGame game = root.AddComponent<RatShooterGame>();
            RatHUD hud = root.AddComponent<RatHUD>();
            hud.game = game;
            hud.cam = cam;
            root.AddComponent<RatAudio>();

            RatFX fx = root.AddComponent<RatFX>();
            fx.puffMaterial = RatMats.Fx("RS_FxPuff", Color.white, CartoonTex.Puff(), false);
            fx.bitsMaterial = RatMats.Fx("RS_FxBits", Color.white, CartoonTex.SoftCircle(), false);
            fx.starMaterial = RatMats.Fx("RS_FxStar", Color.white, CartoonTex.StarSprite(), false);
            fx.sparkMaterial = RatMats.Fx("RS_FxSpark", Color.white, CartoonTex.SoftCircle(), true);
            fx.tracerMaterial = RatMats.Fx("RS_FxTracer", Color.white, CartoonTex.SoftCircle(), true);
            fx.casingMaterial = RatMats.Toon("RS_GunBrass", Color.white);
            // the casing mesh is built along Y; rotate it to lie along Z via the mesh itself
            MeshKit casing = Shapes.RoundedCylinder(0.008f, 0.045f, 0.003f, 10, Color.white);
            fx.casingMesh = RatGen.Mesh(casing.Transformed(Matrix4x4.TRS(new Vector3(0f, 0f, -0.0225f), Quaternion.Euler(90f, 0f, 0f), Vector3.one)), "RS_CasingZ");

            GameObject spGo = new GameObject("MouseSpawner");
            spGo.transform.SetParent(root.transform, false);
            MouseSpawner spawner = spGo.AddComponent<MouseSpawner>();
            spawner.player = cam.transform;
            spawner.playArea = new Bounds(new Vector3(0f, 0f, (2.2f + YardBackZ - 0.4f) * 0.5f), new Vector3(YardHalfX * 2f - 0.8f, 8f, YardBackZ - 0.4f - 2.2f));
            spawner.navMeshLayers = 1 << Layers.Environment;

            game.player = player;
            game.gun = gun;
            game.spawner = spawner;
            game.hud = hud;
            res.game = game;
            res.spawner = spawner;
            res.gameRoot = root;
        }

        public static GrassPlanter Planter { get { return planter; } }
    }
}
