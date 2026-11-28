using UnityEngine;
using UnityEngine.UI;

namespace NeonBreach
{
    /// <summary>
    /// One theatre curtain of the cinematic intro, drawn as a deformable cloth mesh on the intro Canvas. The outer edge
    /// stays on the track at the side of the screen while the leading edge travels outwards, so the velvet folds bunch
    /// up as the curtain opens. The hem can lag behind the top (<see cref="openBottom"/>, driven by a spring in
    /// <see cref="CinematicIntro"/>) and the cloth ripples while it moves. Stage lighting (centre spotlight, footlights)
    /// is baked into the vertex colours from the screen position, so the folds move through the light.
    /// The neon piping on the leading edge keeps a fixed width however much the cloth is compressed.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]   // uGUI 2.x Graphic no longer requires it itself
    public class IntroCurtain : MaskableGraphic
    {
        /// <summary>Texture u where the neon piping strip starts (see <see cref="IntroArt.Velvet"/>).</summary>
        public const float TrimU = 0.95f;
        const int Cols = 40, Rows = 14;

        public Texture cloth;
        /// <summary>false = left curtain, true = right curtain (mirrored).</summary>
        public bool mirror;
        /// <summary>Opening of the top and of the hem: 0 = closed (meeting in the centre), 1 = fully open.</summary>
        public float openTop, openBottom;
        /// <summary>Width of the opened, bunched-up curtain as a fraction of the screen width.</summary>
        public float stack = 0.13f;
        public float overlap = 2f, trimWidth = 22f;
        /// <summary>Ripple amplitude (canvas units) and travelling phase.</summary>
        public float ripple, phase;
        public float spot = 1f, foot = 0.4f, ambient = 0.32f;
        public Color spotColor = new Color(1f, 0.93f, 0.86f, 1f);
        public Color footColor = new Color(1f, 0.3f, 0.7f, 1f);

        public override Texture mainTexture { get { return cloth != null ? cloth : s_WhiteTexture; } }

        static float Sq(float x) { return x * x; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float W = r.width, H = r.height;
            if (W < 2f || H < 2f) return;

            float closed = W * 0.5f + overlap, opened = W * stack;
            float inTop = Mathf.LerpUnclamped(closed, opened, openTop);
            float inBot = Mathf.LerpUnclamped(closed, opened, openBottom);
            const float outer = -18f;
            float yBottom = -8f, yTop = H + 12f;
            int vcols = Cols + 3;
            Color tint = color;
            UIVertex vert = UIVertex.simpleVert;

            for (int j = 0; j <= Rows; j++)
            {
                float v = j / (float)Rows;
                float y = Mathf.Lerp(yBottom, yTop, v);
                float inner = Mathf.Lerp(inBot, inTop, 1f - Mathf.Pow(1f - v, 1.6f));   // the hem lags the most
                float clothEnd = Mathf.Max(outer + 6f, inner - trimWidth);
                float hang = Mathf.Pow(1f - v, 1.25f);
                float ny = y / H;
                float topShade = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((ny - 0.75f) / 0.25f));
                for (int c = 0; c < vcols; c++)
                {
                    float x, u, s;
                    bool trim;
                    if (c <= Cols)
                    {
                        s = c / (float)Cols;
                        x = Mathf.Lerp(outer, clothEnd, s);
                        u = s * TrimU;
                        trim = false;
                    }
                    else
                    {
                        float q = (c - Cols) * 0.5f;
                        s = 1f;
                        x = Mathf.Lerp(clothEnd, inner, q);
                        u = Mathf.Lerp(TrimU, 1f, q);
                        trim = true;
                    }
                    x += ripple * Mathf.Sin((s * 2.3f - phase) * 6.2832f) * hang * s;
                    float X = mirror ? W - x : x;
                    float nx = X / W;

                    float sp = spot * Mathf.Exp(-Sq((nx - 0.5f) / 0.27f) - Sq((ny - 0.5f) / 0.6f));
                    float ft = foot * Mathf.Exp(-Mathf.Max(0f, ny) / 0.15f);
                    float side = ambient * Mathf.Lerp(0.5f, 1f, 1f - Mathf.Abs(nx - 0.5f) * 2f);
                    Color L = new Color(side + spotColor.r * sp + footColor.r * ft,
                                        side + spotColor.g * sp + footColor.g * ft,
                                        side + spotColor.b * sp + footColor.b * ft, 1f) * topShade;
                    if (trim) L = Color.Lerp(L, Color.white, 0.8f);   // the piping is emissive
                    L.r = Mathf.Clamp01(L.r) * tint.r;
                    L.g = Mathf.Clamp01(L.g) * tint.g;
                    L.b = Mathf.Clamp01(L.b) * tint.b;
                    L.a = tint.a;

                    vert.position = new Vector3(r.xMin + X, r.yMin + y, 0f);
                    vert.uv0 = new Vector4(u, Mathf.Clamp01(v), 0f, 0f);
                    vert.color = L;
                    vh.AddVert(vert);
                }
            }
            for (int j = 0; j < Rows; j++)
                for (int c = 0; c < vcols - 1; c++)
                {
                    int i0 = j * vcols + c;
                    vh.AddTriangle(i0, i0 + vcols, i0 + vcols + 1);
                    vh.AddTriangle(i0, i0 + vcols + 1, i0 + 1);
                }
        }
    }
}
