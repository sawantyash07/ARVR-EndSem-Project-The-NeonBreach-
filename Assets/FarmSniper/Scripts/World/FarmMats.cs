using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Material library for the farm (Standard shader, GPU instancing on). Separate from the arena's Mats library so a
    /// rebuild of the Neon Breach scene (which recreates Assets/Resources/Materials) never touches the farm.
    /// The editor builder saves every material and texture made here into Assets/FarmSniper/Generated.
    /// </summary>
    public static class FarmMats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Texture2D detail;

        public static void ClearCache() { cache.Clear(); detail = null; }

        static Shader Standard
        {
            get
            {
                Shader s = Shader.Find("Standard");
                return s != null ? s : Shader.Find("Sprites/Default");
            }
        }

        public static Texture2D Detail
        {
            get
            {
                if (detail == null) detail = FarmTextures.DetailNoise(256);
                return detail;
            }
        }

        public static Material Make(string name, Color color, float smooth, float metallic = 0f, Texture2D tex = null, Vector2? tiling = null, bool useDetail = false, float detailTiling = 6f)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            m = new Material(Standard);
            m.name = "Farm_" + name;
            m.enableInstancing = true;
            m.color = color;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling ?? Vector2.one;
            }
            if (useDetail && m.HasProperty("_DetailAlbedoMap"))
            {
                m.SetTexture("_DetailAlbedoMap", Detail);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiling, detailTiling));
                m.EnableKeyword("_DETAIL_MULX2");
            }
            cache[name] = m;
            return m;
        }

        public static Material Emissive(string name, Color color, Color emission)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            m = Make(name, color, 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        /// <summary>Transparent (fade) Standard material - pond water, window glass.</summary>
        public static Material Transparent(string name, Color color, float smooth)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            m = Make(name, color, smooth);
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        public static Material Unlit(string name, Color color, bool textured = false)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            Shader s = Shader.Find(textured ? "Unlit/Texture" : "Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            m = new Material(s);
            m.name = "Farm_" + name;
            m.color = color;
            cache[name] = m;
            return m;
        }

        // ------------------------------------------------------------------ named farm materials

        public static Material Ground
        {
            get
            {
                Material m;
                if (cache.TryGetValue("Ground", out m) && m != null) return m;
                m = Make("Ground", Color.white, 0.06f, 0f, FarmTextures.GroundMap(2048), Vector2.one, true, 180f);
                return m;
            }
        }

        public static Material Get(string key)
        {
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            switch (key)
            {
                case "BarnRed": return Make(key, new Color(0.62f, 0.14f, 0.10f), 0.12f, 0f, FarmTextures.Planks("Farm_BarnPlanks", new Color(0.95f, 0.95f, 0.95f), 6, 0.25f, 11), new Vector2(1f, 1f));
                case "WhiteTrim": return Make(key, new Color(0.90f, 0.89f, 0.84f), 0.2f, 0f, FarmTextures.Planks("Farm_TrimPlanks", Color.white, 3, 0.1f, 3), Vector2.one);
                case "HouseSiding": return Make(key, new Color(0.86f, 0.82f, 0.70f), 0.15f, 0f, FarmTextures.Planks("Farm_Siding", Color.white, 6, 0.2f, 5, true), Vector2.one);
                case "SiloMetal": return Make(key, new Color(0.70f, 0.72f, 0.74f), 0.4f, 0.45f, FarmTextures.Corrugated("Farm_SiloTex", Color.white), new Vector2(12f, 5f));
                case "Wood": return Make(key, new Color(0.55f, 0.40f, 0.26f), 0.1f, 0f, FarmTextures.Planks("Farm_WoodPlanks", Color.white, 5, 0.35f, 7), Vector2.one);
                case "FenceWood": return Make(key, new Color(0.50f, 0.42f, 0.34f), 0.08f, 0f, FarmTextures.Planks("Farm_FenceWood", Color.white, 2, 0.5f, 9), Vector2.one);
                case "DarkWood": return Make(key, new Color(0.30f, 0.21f, 0.14f), 0.12f, 0f, FarmTextures.Planks("Farm_DarkPlanks", Color.white, 4, 0.1f, 13), Vector2.one);
                case "RoofShingle": return Make(key, new Color(0.36f, 0.33f, 0.32f), 0.1f, 0f, FarmTextures.Shingles("Farm_Shingles", Color.white, 21), new Vector2(0.5f, 0.5f));
                case "RoofMetal": return Make(key, new Color(0.62f, 0.64f, 0.66f), 0.35f, 0.4f, FarmTextures.Corrugated("Farm_Corrugated", Color.white), new Vector2(0.5f, 0.5f));
                case "Stone": return Make(key, new Color(0.62f, 0.60f, 0.56f), 0.1f, 0f, FarmTextures.Mottled("Farm_Stone", new Color(0.45f, 0.44f, 0.42f), new Color(0.78f, 0.76f, 0.72f), 10f, 0f), Vector2.one);
                case "Brick": return Make(key, new Color(0.55f, 0.28f, 0.20f), 0.1f, 0f, FarmTextures.Mottled("Farm_Brick", new Color(0.45f, 0.20f, 0.15f), new Color(0.68f, 0.36f, 0.26f), 14f, 0f), Vector2.one);
                case "Bark": return Make(key, new Color(0.38f, 0.30f, 0.22f), 0.05f, 0f, FarmTextures.Bark("Farm_BarkTex", Color.white), new Vector2(2f, 3f));
                case "Leaves": return Make(key, new Color(0.30f, 0.46f, 0.18f), 0.08f, 0f, FarmTextures.Mottled("Farm_LeavesTex", new Color(0.55f, 0.75f, 0.45f), new Color(1f, 1f, 0.85f), 10f, 0f), new Vector2(3f, 3f));
                case "LeavesDark": return Make(key, new Color(0.20f, 0.34f, 0.15f), 0.08f, 0f, FarmTextures.Mottled("Farm_LeavesDarkTex", new Color(0.6f, 0.75f, 0.55f), new Color(1f, 1f, 0.9f), 9f, 0f), new Vector2(3f, 3f));
                case "PineNeedles": return Make(key, new Color(0.14f, 0.27f, 0.17f), 0.06f, 0f, FarmTextures.Mottled("Farm_PineTex", new Color(0.6f, 0.75f, 0.6f), new Color(1f, 1f, 1f), 16f, 0.4f), new Vector2(4f, 2f));
                case "Corn": return Make(key, new Color(0.42f, 0.55f, 0.20f), 0.15f);
                case "CornDry": return Make(key, new Color(0.62f, 0.60f, 0.30f), 0.1f);
                case "Wheat": return Make(key, new Color(0.84f, 0.70f, 0.38f), 0.12f);
                case "Cabbage": return Make(key, new Color(0.45f, 0.64f, 0.34f), 0.25f, 0f, FarmTextures.Mottled("Farm_CabbageTex", new Color(0.75f, 0.9f, 0.7f), Color.white, 8f, 0.5f), Vector2.one);
                case "Pumpkin": return Make(key, new Color(0.90f, 0.45f, 0.10f), 0.3f);
                case "Vine": return Make(key, new Color(0.25f, 0.40f, 0.15f), 0.15f);
                case "Grass": return Make(key, new Color(0.33f, 0.47f, 0.18f), 0.1f);
                case "GrassDry": return Make(key, new Color(0.55f, 0.55f, 0.30f), 0.08f);
                case "Hay": return Make(key, new Color(0.80f, 0.68f, 0.40f), 0.05f, 0f, FarmTextures.Mottled("Farm_HayTex", new Color(0.75f, 0.65f, 0.45f), new Color(1f, 0.95f, 0.75f), 12f, 0.6f), new Vector2(2f, 2f));
                case "Soil": return Make(key, new Color(0.30f, 0.22f, 0.15f), 0.05f, 0f, FarmTextures.Mottled("Farm_SoilTex", new Color(0.7f, 0.65f, 0.6f), Color.white, 12f, 0f), Vector2.one);
                case "BurrowHole": return Make(key, new Color(0.04f, 0.03f, 0.02f), 0f);
                case "Water": return Transparent(key, new Color(0.13f, 0.24f, 0.22f, 0.82f), 0.92f);
                case "Glass": return Make(key, new Color(0.18f, 0.24f, 0.28f), 0.95f, 0.1f);
                case "Metal": return Make(key, new Color(0.42f, 0.43f, 0.45f), 0.45f, 0.7f);
                case "RustyMetal": return Make(key, new Color(0.45f, 0.30f, 0.20f), 0.2f, 0.3f, FarmTextures.Corrugated("Farm_RustyTex", new Color(0.8f, 0.7f, 0.6f)), Vector2.one);
                case "TractorGreen": return Make(key, new Color(0.13f, 0.38f, 0.15f), 0.45f, 0.2f);
                case "TractorYellow": return Make(key, new Color(0.92f, 0.72f, 0.10f), 0.4f, 0.1f);
                case "Rubber": return Make(key, new Color(0.06f, 0.06f, 0.06f), 0.15f);
                case "Rope": return Make(key, new Color(0.55f, 0.48f, 0.34f), 0.05f);
                case "Cloth": return Make(key, new Color(0.62f, 0.55f, 0.42f), 0.05f);
                // farmer
                case "Denim": return Make(key, new Color(0.22f, 0.32f, 0.52f), 0.12f, 0f, FarmTextures.Mottled("Farm_DenimTex", new Color(0.8f, 0.85f, 0.95f), Color.white, 30f, 0.5f), new Vector2(2f, 2f));
                case "Flannel": return Make(key, Color.white, 0.08f, 0f, FarmTextures.Plaid("Farm_PlaidTex", new Color(0.62f, 0.12f, 0.10f), new Color(0.10f, 0.08f, 0.08f)), new Vector2(2f, 2f));
                case "Skin": return Make(key, new Color(0.86f, 0.64f, 0.50f), 0.3f);
                case "Straw": return Make(key, new Color(0.86f, 0.74f, 0.46f), 0.05f, 0f, FarmTextures.Mottled("Farm_StrawTex", new Color(0.8f, 0.7f, 0.5f), Color.white, 20f, 0.8f), new Vector2(3f, 1f));
                case "Leather": return Make(key, new Color(0.28f, 0.17f, 0.10f), 0.3f);
                case "HatBand": return Make(key, new Color(0.30f, 0.12f, 0.08f), 0.2f);
                // rifle
                case "GunSteel": return Make(key, new Color(0.10f, 0.10f, 0.11f), 0.65f, 0.85f);
                case "GunWood": return Make(key, new Color(0.42f, 0.24f, 0.12f), 0.55f, 0f, FarmTextures.Mottled("Farm_WalnutTex", new Color(0.7f, 0.55f, 0.45f), new Color(1f, 0.9f, 0.8f), 6f, 0.85f), new Vector2(1f, 4f));
                case "ScopeLens": return Make(key, new Color(0.05f, 0.08f, 0.12f), 0.98f, 0.2f);
                // rat
                case "RatFur": return Make(key, new Color(0.36f, 0.31f, 0.27f), 0.12f, 0f, FarmTextures.Mottled("Farm_RatFurTex", new Color(0.75f, 0.7f, 0.65f), new Color(1.1f, 1.05f, 1f), 18f, 0.5f), new Vector2(2f, 2f));
                case "RatSkin": return Make(key, new Color(0.78f, 0.58f, 0.55f), 0.3f);
                case "RatEye": return Make(key, new Color(0.02f, 0.02f, 0.02f), 0.95f);
                // misc
                case "RedPaint": return Make(key, new Color(0.65f, 0.12f, 0.10f), 0.35f);
                case "BluePaint": return Make(key, new Color(0.18f, 0.32f, 0.55f), 0.35f);
                case "AmmoGreen": return Make(key, new Color(0.24f, 0.30f, 0.18f), 0.25f);
                case "Lamp": return Emissive(key, new Color(1f, 0.9f, 0.6f), new Color(1.2f, 0.95f, 0.6f));
            }
            return Make(key, Color.magenta, 0.2f);
        }
    }
}
