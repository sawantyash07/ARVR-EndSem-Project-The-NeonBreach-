using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreach
{
    /// <summary>
    /// Mesh effect for the intro's single-line UI Text: animated letter spacing, a staggered per-letter reveal
    /// (fade + rise, from the centre outwards), a soft neon glow built from offset copies of the glyphs, a magenta
    /// "split" layer like the main-menu title, a light sweep across the letters and a global flicker multiplier.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(Text))]
    public class IntroTextFX : BaseMeshEffect
    {
        [Tooltip("Extra space between letters, in canvas units.")]
        public float tracking;
        [Range(0f, 1f)] public float reveal = 1f;
        [Tooltip("Share of the reveal over which the letter start times are spread.")]
        [Range(0f, 0.95f)] public float stagger = 0.5f;
        public bool centerOut = true;
        [Tooltip("Letters rise this far into place while they fade in.")]
        public float rise;
        public float flicker = 1f;

        public Color glowColor = new Color(0.15f, 0.92f, 1f, 1f);
        public float glowAlpha;
        public float glowRadius = 8f;
        [Range(1, 4)] public int glowRings = 3;
        [Range(4, 16)] public int glowDirs = 10;

        public Color splitColor = new Color(1f, 0.2f, 0.7f, 1f);
        public Vector2 splitOffset = new Vector2(7f, -7f);
        public float splitAlpha;

        [Tooltip("Position of the light sweep across the line (0 = first letter, 1 = last). Outside -1..2 = off.")]
        public float shine = -10f;
        public float shineWidth = 0.14f;

        /// <summary>Visible glyphs in the last generated mesh and half of its width after letter spacing.</summary>
        public int GlyphCount { get; private set; }
        public float HalfExtent { get; private set; }

        static readonly List<UIVertex> src = new List<UIVertex>(256);
        static readonly List<UIVertex> dst = new List<UIVertex>(4096);
        static float[] slot = new float[32];

        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        static float EaseOut(float x) { x = Mathf.Clamp01(x); float k = 1f - x; return 1f - k * k * k; }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            src.Clear();
            vh.GetUIVertexStream(src);
            int n = src.Count / 6;
            GlyphCount = n;
            if (n == 0) { HalfExtent = 0f; return; }

            // Letter spacing follows character positions, spaces included (legacy Text emits no quads for spaces), so a
            // word gap stays wider than the gap between letters.
            if (slot.Length < n) slot = new float[Mathf.NextPowerOfTwo(n)];
            Text txt = graphic as Text;
            string s = txt != null ? txt.text : null;
            int visible = 0;
            if (s != null) for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) visible++;
            if (s != null && visible == n)
            {
                int k = 0;
                for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) slot[k++] = i;
                float centre = (s.Length - 1) * 0.5f;
                for (int j = 0; j < n; j++) slot[j] -= centre;
            }
            else for (int j = 0; j < n; j++) slot[j] = j - (n - 1) * 0.5f;

            float mid = (n - 1) * 0.5f;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                float dx = slot[k] * tracking;
                for (int i = 0; i < 6; i++)
                {
                    float x = src[k * 6 + i].position.x + dx;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
            HalfExtent = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
            dst.Clear();
            bool glow = glowAlpha > 0.001f && glowRadius > 0.01f;
            bool split = splitAlpha > 0.001f;

            // pass 0: glow rings (furthest back), pass 1: magenta split layer, pass 2: the letters themselves
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 0 && !glow) continue;
                if (pass == 1 && !split) continue;
                for (int k = 0; k < n; k++)
                {
                    float order = centerOut ? (mid > 0f ? Mathf.Abs(k - mid) / mid : 0f) : (n > 1 ? k / (float)(n - 1) : 0f);
                    float local = stagger >= 0.999f ? reveal : Mathf.Clamp01((reveal - order * stagger) / (1f - stagger));
                    float a = EaseOut(local) * Mathf.Clamp01(flicker);
                    if (a <= 0.002f) continue;
                    float dx = slot[k] * tracking;
                    float dy = (1f - EaseOut(local)) * -rise;
                    int b = k * 6;

                    if (pass == 2)
                    {
                        float pos = n > 1 ? k / (float)(n - 1) : 0.5f;
                        float sh = Mathf.Exp(-((pos - shine) * (pos - shine)) / (shineWidth * shineWidth));
                        for (int i = 0; i < 6; i++)
                        {
                            UIVertex v = src[b + i];
                            v.position.x += dx;
                            v.position.y += dy;
                            Color c = v.color;
                            c = Color.Lerp(c, Color.white, sh * 0.85f);
                            c.a *= a;
                            v.color = c;
                            dst.Add(v);
                        }
                    }
                    else if (pass == 1)
                    {
                        Color c = splitColor;
                        for (int i = 0; i < 6; i++)
                        {
                            UIVertex v = src[b + i];
                            v.position.x += dx + splitOffset.x;
                            v.position.y += dy + splitOffset.y;
                            c.a = splitColor.a * splitAlpha * a * (v.color.a / 255f);
                            v.color = c;
                            dst.Add(v);
                        }
                    }
                    else
                    {
                        for (int ring = 1; ring <= glowRings; ring++)
                        {
                            float rad = glowRadius * ring / glowRings;
                            float fall = 1f - (ring - 1) / (float)glowRings * 0.55f;
                            for (int d = 0; d < glowDirs; d++)
                            {
                                float ang = 6.2832f * d / glowDirs + ring * 0.37f;
                                float ox = Mathf.Cos(ang) * rad, oy = Mathf.Sin(ang) * rad;
                                Color c = glowColor;
                                for (int i = 0; i < 6; i++)
                                {
                                    UIVertex v = src[b + i];
                                    v.position.x += dx + ox;
                                    v.position.y += dy + oy;
                                    c.a = glowColor.a * glowAlpha * fall * a * (v.color.a / 255f);
                                    v.color = c;
                                    dst.Add(v);
                                }
                            }
                        }
                    }
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(dst);
        }
    }
}
