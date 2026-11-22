using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Records every texture, material and mesh generated while the Rat Shooter world is built, so the editor
    /// scene builder can save them as assets (and nothing generated is left only in memory).
    /// </summary>
    public static class RatGen
    {
        public static readonly List<Object> Created = new List<Object>();

        public static void Register(Object o) { if (o != null && !Created.Contains(o)) Created.Add(o); }

        public static void Clear() { Created.Clear(); }

        public static Mesh Mesh(MeshKit k, string name)
        {
            Mesh m = k.ToMesh(name);
            Register(m);
            return m;
        }
    }

    /// <summary>Material factory for the cartoon look (RatShooter/Toon, RatShooter/FX, RatShooter/Sky shaders).</summary>
    public static class RatMats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void ClearCache() { cache.Clear(); }

        public static Shader ToonShader { get { return Shader.Find("RatShooter/Toon"); } }
        public static Shader FxShader { get { return Shader.Find("RatShooter/FX"); } }

        public class ToonOpts
        {
            public Texture2D tex;
            public Vector2 tiling = Vector2.one;
            public float rampSmooth = 0.22f;
            public float rampOffset = 0f;
            public float spec = 0.15f;
            public float gloss = 40f;
            public float rim = 0.3f;
            public float rimPower = 3f;
            public float wind = 0f;
            public bool twoSided;
            public bool flipBackNormals = true;
            public float vertexColor = 1f;
            public float fog = 1f;
            public Color shadeTint = new Color(0.62f, 0.68f, 0.95f, 1f);
            public Color emission = Color.black;
        }

        public static Material Toon(string name, Color color, ToonOpts o = null)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            if (o == null) o = new ToonOpts();
            Shader sh = ToonShader;
            if (sh == null) { Debug.LogError("[Rat Shooter] Shader RatShooter/Toon not found."); sh = Shader.Find("Standard"); }
            m = new Material(sh);
            m.name = name;
            m.SetColor("_Color", color);
            if (o.tex != null)
            {
                m.SetTexture("_MainTex", o.tex);
                m.SetTextureScale("_MainTex", o.tiling);
            }
            m.SetFloat("_RampSmooth", o.rampSmooth);
            m.SetFloat("_RampOffset", o.rampOffset);
            m.SetFloat("_SpecAmount", o.spec);
            m.SetFloat("_Gloss", o.gloss);
            m.SetFloat("_RimAmount", o.rim);
            m.SetFloat("_RimPower", o.rimPower);
            m.SetFloat("_Wind", o.wind);
            m.SetFloat("_Cull", o.twoSided ? 0f : 2f);
            m.SetFloat("_FlipBackNormals", o.flipBackNormals ? 1f : 0f);
            m.SetFloat("_VertexColorAmount", o.vertexColor);
            m.SetFloat("_FogAmount", o.fog);
            m.SetColor("_ShadeTint", o.shadeTint);
            m.SetColor("_EmissionColor", o.emission);
            m.enableInstancing = true;
            cache[name] = m;
            RatGen.Register(m);
            return m;
        }

        public static Material Toon(string name, Color color, Texture2D tex, float tiling = 1f, float spec = 0.15f, float gloss = 40f, float rim = 0.3f)
        {
            return Toon(name, color, new ToonOpts { tex = tex, tiling = new Vector2(tiling, tiling), spec = spec, gloss = gloss, rim = rim });
        }

        /// <summary>Unlit effect material: additive (glows) or alpha blended (puffs, smoke).</summary>
        public static Material Fx(string name, Color color, Texture2D tex, bool additive)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;
            Shader sh = FxShader;
            if (sh == null) { Debug.LogError("[Rat Shooter] Shader RatShooter/FX not found."); sh = Shader.Find("Sprites/Default"); }
            m = new Material(sh);
            m.name = name;
            m.SetColor("_Color", color);
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3000;
            m.enableInstancing = true;
            cache[name] = m;
            RatGen.Register(m);
            return m;
        }

        public static Material Sky()
        {
            Material m;
            if (cache.TryGetValue("RS_Sky", out m) && m != null) return m;
            Shader sh = Shader.Find("RatShooter/Sky");
            if (sh == null) sh = Shader.Find("Skybox/Procedural");
            m = new Material(sh);
            m.name = "RS_Sky";
            cache["RS_Sky"] = m;
            RatGen.Register(m);
            return m;
        }
    }
}
