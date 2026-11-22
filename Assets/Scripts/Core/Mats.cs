using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Material library. Standard-shader materials are saved as assets in Assets/Resources/Materials by the
    /// editor builder (so shaders are included in builds). If an asset is missing, it is created on the fly.
    /// FX materials (particles, tracers, decals) use Sprites/Default and are always created at runtime.
    /// </summary>
    public static class Mats
    {
        public class Def
        {
            public string name;
            public Color color;
            public Color emission;
            public float metallic;
            public float smooth;

            public Def(string n, Color c, Color e, float m, float s)
            {
                name = n; color = c; emission = e; metallic = m; smooth = s;
            }
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }
        static Color E(float r, float g, float b, float k) { return new Color(r * k, g * k, b * k, 1f); }

        public static readonly List<Def> Defs = new List<Def>
        {
            new Def("Floor",       C(1f, 1f, 1f),            Color.black, 0.15f, 0.85f),
            new Def("Wall",        C(0.09f, 0.10f, 0.15f),   Color.black, 0.50f, 0.55f),
            new Def("Cover",       C(0.14f, 0.16f, 0.22f),   Color.black, 0.40f, 0.50f),
            new Def("Building",    C(0.035f, 0.04f, 0.065f), Color.black, 0.10f, 0.30f),
            new Def("NeonCyan",    C(0.02f, 0.05f, 0.06f),   E(0.10f, 0.90f, 1.00f, 2.2f), 0f, 0.4f),
            new Def("NeonMagenta", C(0.06f, 0.02f, 0.04f),   E(1.00f, 0.10f, 0.65f, 2.2f), 0f, 0.4f),
            new Def("NeonOrange",  C(0.06f, 0.03f, 0.01f),   E(1.00f, 0.45f, 0.05f, 2.4f), 0f, 0.4f),
            new Def("NeonRed",     C(0.06f, 0.01f, 0.01f),   E(1.00f, 0.08f, 0.08f, 2.4f), 0f, 0.4f),
            new Def("NeonYellow",  C(0.06f, 0.06f, 0.01f),   E(1.00f, 0.90f, 0.10f, 2.2f), 0f, 0.4f),
            new Def("NeonGreen",   C(0.01f, 0.06f, 0.02f),   E(0.20f, 1.00f, 0.30f, 2.2f), 0f, 0.4f),
            new Def("NeonWhite",   C(0.8f, 0.8f, 0.8f),      E(1.00f, 1.00f, 1.00f, 1.2f), 0f, 0.4f),
            new Def("GunMetal",    C(0.13f, 0.14f, 0.17f),   Color.black, 0.90f, 0.65f),
            new Def("GunGrip",     C(0.04f, 0.04f, 0.05f),   Color.black, 0.10f, 0.25f),
            new Def("EnemyDark",   C(0.07f, 0.07f, 0.09f),   Color.black, 0.70f, 0.50f),
            new Def("EnemyRed",    C(0.35f, 0.05f, 0.06f),   Color.black, 0.50f, 0.50f),
            new Def("EnemyBlue",   C(0.08f, 0.12f, 0.38f),   Color.black, 0.50f, 0.50f),
            new Def("EnemyGrey",   C(0.25f, 0.26f, 0.30f),   Color.black, 0.70f, 0.55f),
            // ---- mini-game modes (archery range, parachute island)
            new Def("Wood",        C(0.32f, 0.20f, 0.10f),   Color.black, 0.00f, 0.25f),
            new Def("Hay",         C(0.72f, 0.60f, 0.30f),   Color.black, 0.00f, 0.08f),
            new Def("Bark",        C(0.22f, 0.14f, 0.08f),   Color.black, 0.00f, 0.10f),
            new Def("Leaf",        C(0.13f, 0.40f, 0.13f),   Color.black, 0.00f, 0.15f),
            new Def("LeafDark",    C(0.07f, 0.26f, 0.10f),   Color.black, 0.00f, 0.12f),
            new Def("Water",       C(0.03f, 0.20f, 0.36f),   Color.black, 0.20f, 0.93f),
            new Def("Canopy",      C(0.85f, 0.10f, 0.25f),   E(1.00f, 0.10f, 0.30f, 0.35f), 0f, 0.35f),
            new Def("Balloon",     C(0.95f, 0.38f, 0.05f),   E(1.00f, 0.40f, 0.05f, 0.25f), 0f, 0.45f),
            new Def("PlaneBody",   C(0.75f, 0.77f, 0.80f),   Color.black, 0.60f, 0.60f),
            new Def("Paper",       C(0.90f, 0.90f, 0.85f),   Color.black, 0.00f, 0.10f),
        };

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Material fxParticle, fxTracer, fxDecal;

        public static void ClearCache() { cache.Clear(); }

        /// <summary>Load a shader from Assets/Resources/Shaders (always included in builds), falling back to Shader.Find.</summary>
        public static Shader LoadShader(string name)
        {
            Shader s = Resources.Load<Shader>("Shaders/" + name);
            if (s == null) s = Shader.Find("NeonBreach/" + name);
            return s;
        }

        public static Material Get(string name)
        {
            Material m;
            if (cache.TryGetValue(name, out m) && m != null) return m;

            m = Resources.Load<Material>("Materials/" + name);
#if UNITY_EDITOR
            if (m == null)
                m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/" + name + ".mat");
#endif
            if (m == null)
            {
                Def d = Defs.Find(x => x.name == name);
                if (d == null) d = Defs[0];
                m = Build(d);
                if (d.name == "Floor") ApplyGrid(m, ProcTex.Grid(256));
            }
            cache[name] = m;
            return m;
        }

        public static Material Build(Def d)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Material m = new Material(sh);
            m.name = d.name;
            m.enableInstancing = true;   // GPU instancing: repeated meshes that share a material draw in one call
            m.color = d.color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", d.metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", d.smooth);
            if (d.emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", d.emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>Neon grid floor: dark albedo, glowing lines through the emission map.</summary>
        public static void ApplyGrid(Material m, Texture2D tex)
        {
            Vector2 tiling = new Vector2(32f, 32f);
            m.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetTextureScale("_EmissionMap", tiling);
            m.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f, 1f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static readonly Dictionary<string, Material> texCache = new Dictionary<string, Material>();

        /// <summary>
        /// Standard material with a procedural texture (ProcTex) as albedo and, optionally, as a glowing emission map.
        /// Cached by name so a mode that is rebuilt reuses the same material.
        /// </summary>
        public static Material Textured(string name, Texture2D tex, Color tint, float emission, float smooth, Vector2 tiling)
        {
            Material m;
            if (texCache.TryGetValue(name, out m) && m != null) return m;
            m = Build(new Def(name, tint, Color.black, 0f, smooth));
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetTextureScale("_EmissionMap", tiling);
                m.SetColor("_EmissionColor", new Color(emission, emission, emission, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            texCache[name] = m;
            return m;
        }

        /// <summary>A new unlit, alpha-capable material (Sprites/Default) owned by the caller - for things that fade out.</summary>
        public static Material FxColor(Color c)
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.name = "FX_Color";
            m.color = c;
            // no GPU instancing here on purpose: Sprites/Default's instanced path takes its tint from per-instance
            // sprite arrays that a MeshRenderer never fills, and these transparent draws are depth-sorted anyway
            return m;
        }

        static Material fxSky;

        /// <summary>Vertex-coloured and unlit, drawn before all opaque geometry without writing depth (sky dome).</summary>
        public static Material FxSky
        {
            get
            {
                if (fxSky == null)
                {
                    fxSky = new Material(Shader.Find("Sprites/Default"));
                    fxSky.name = "FX_Sky";
                    fxSky.renderQueue = 1000;   // Background queue: everything else draws over it
                }
                return fxSky;
            }
        }

        public static Material FxParticle
        {
            get
            {
                if (fxParticle == null)
                {
                    fxParticle = new Material(Shader.Find("Sprites/Default"));
                    fxParticle.name = "FX_Particle";
                    fxParticle.mainTexture = ProcTex.SoftCircle(64);
                }
                return fxParticle;
            }
        }

        public static Material FxTracer
        {
            get
            {
                if (fxTracer == null)
                {
                    fxTracer = new Material(Shader.Find("Sprites/Default"));
                    fxTracer.name = "FX_Tracer";
                }
                return fxTracer;
            }
        }

        static Material fxShield;

        /// <summary>Translucent cyan energy (tank shield).</summary>
        public static Material FxShield
        {
            get
            {
                if (fxShield == null)
                {
                    fxShield = new Material(Shader.Find("Sprites/Default"));
                    fxShield.name = "FX_Shield";
                    fxShield.color = new Color(0.25f, 0.85f, 1f, 0.32f);
                }
                return fxShield;
            }
        }

        public static Material FxDecal
        {
            get
            {
                if (fxDecal == null)
                {
                    fxDecal = new Material(Shader.Find("Sprites/Default"));
                    fxDecal.name = "FX_Decal";
                    fxDecal.mainTexture = ProcTex.Decal(64);
                }
                return fxDecal;
            }
        }
    }
}
