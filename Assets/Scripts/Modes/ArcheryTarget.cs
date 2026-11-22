using UnityEngine;

namespace NeonBreach
{
    public enum TargetMotion { Static, Rail, PopUp }

    /// <summary>
    /// A concentric-ring archery target built from primitives: wooden stand, straw board and a procedural ring face.
    /// Scoring from the centre outwards: bullseye 50, then 10 / 8 / 6 / 4 / 2. Rail targets slide left-right on a
    /// track, pop-up targets fold down and spring back up. The root transform is never scaled, so stuck arrows can be
    /// parented to it (they ride along with moving targets without being distorted).
    /// </summary>
    public class ArcheryTarget : MonoBehaviour
    {
        public float radius = 0.5f;          // scoring radius (outer edge of the "2" ring), metres
        public float distance = 10f;         // from the shooting line
        public TargetMotion motion;
        public Transform face;               // centre of the face, forward = down-range
        public Transform hinge;              // pop-up pivot

        // rail
        public float railHalf = 8f;
        public float railSpeed = 2f;
        Vector3 railCenter;
        float railPhase;

        // pop-up
        float popTimer;
        bool up = true;
        float fold;                          // 0 = up, 1 = folded flat
        float upTime = 3.5f, downTime = 2.2f;

        float flash;
        Renderer faceRenderer;
        MaterialPropertyBlock mpb;

        public bool IsMoving { get { return motion != TargetMotion.Static; } }
        public bool IsUp { get { return fold < 0.5f; } }

        /// <summary>Bonus multiplier for harder targets.</summary>
        public float Bonus { get { return motion == TargetMotion.Rail ? 1.5f : (motion == TargetMotion.PopUp ? 1.25f : 1f); } }

        public static int RingScore(float normalizedRadius)
        {
            if (normalizedRadius <= 0.1f) return 50;
            if (normalizedRadius <= 0.2f) return 10;
            if (normalizedRadius <= 0.4f) return 8;
            if (normalizedRadius <= 0.6f) return 6;
            if (normalizedRadius <= 0.8f) return 4;
            if (normalizedRadius <= 1.0f) return 2;
            return 0;
        }

        /// <summary>Score of a hit at a world position (0 = outside the rings: board edge or stand).</summary>
        public int ScoreAt(Vector3 world, out float normalizedRadius)
        {
            Vector3 d = Vector3.ProjectOnPlane(world - face.position, face.forward);
            normalizedRadius = d.magnitude / Mathf.Max(0.01f, radius);
            return RingScore(normalizedRadius);
        }

        public void Flash() { flash = 1f; }

        // ------------------------------------------------------------------------------------ build

        public static ArcheryTarget Create(Transform parent, Vector3 basePos, float radius, float distance, TargetMotion motion, int index)
        {
            GameObject root = new GameObject("Target_" + index + "_" + motion + "_" + Mathf.RoundToInt(distance) + "m");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;
            ArcheryTarget t = root.AddComponent<ArcheryTarget>();
            t.radius = radius;
            t.distance = distance;
            t.motion = motion;
            t.railCenter = basePos;
            t.railPhase = index * 1.7f;
            t.popTimer = 1f + index * 0.9f;

            float board = radius * 2f / ProcTex.TargetRingFraction;   // board edge = edge of the face texture
            float centerY = 1.35f + radius;
            Transform r = root.transform;

            // stand: two legs and a cross bar (kept on the Environment layer so arrows stick into them)
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject leg = Util.Part(r, PrimitiveType.Cube, "Leg", new Vector3(s * board * 0.38f, centerY * 0.5f, 0.18f),
                                           new Vector3(-8f, 0f, 0f), new Vector3(0.09f, centerY, 0.09f), "Wood", true);
                leg.layer = Layers.Environment;
            }

            // pop-up targets fold backwards around a hinge at the bottom of the board
            Transform holder = r;
            if (motion == TargetMotion.PopUp)
            {
                GameObject h = new GameObject("Hinge");
                h.transform.SetParent(r, false);
                h.transform.localPosition = new Vector3(0f, centerY - board * 0.5f, 0f);
                t.hinge = h.transform;
                holder = h.transform;
            }
            Vector3 boardPos = holder == r ? new Vector3(0f, centerY, 0f) : new Vector3(0f, board * 0.5f, 0f);

            GameObject b = Util.Part(holder, PrimitiveType.Cube, "Board", boardPos, Vector3.zero, new Vector3(board, board, 0.22f), "Hay", true);
            b.layer = Layers.Environment;

            GameObject f = Util.Part(holder, PrimitiveType.Quad, "Face", boardPos + new Vector3(0f, 0f, -0.115f), Vector3.zero,
                                     new Vector3(board, board, 1f), "Paper");
            f.GetComponent<Renderer>().sharedMaterial = FaceMaterial();
            t.face = f.transform;
            t.faceRenderer = f.GetComponent<Renderer>();
            Util.NoShadows(f);

            // neon frame (moving targets glow magenta so they read as "bonus")
            string trim = motion == TargetMotion.Static ? "NeonCyan" : "NeonMagenta";
            float e = board * 0.5f + 0.02f;
            Util.Part(holder, PrimitiveType.Cube, "TrimT", boardPos + new Vector3(0f, e, -0.06f), Vector3.zero, new Vector3(board + 0.08f, 0.04f, 0.1f), trim);
            Util.Part(holder, PrimitiveType.Cube, "TrimB", boardPos + new Vector3(0f, -e, -0.06f), Vector3.zero, new Vector3(board + 0.08f, 0.04f, 0.1f), trim);
            Util.Part(holder, PrimitiveType.Cube, "TrimL", boardPos + new Vector3(-e, 0f, -0.06f), Vector3.zero, new Vector3(0.04f, board, 0.1f), trim);
            Util.Part(holder, PrimitiveType.Cube, "TrimR", boardPos + new Vector3(e, 0f, -0.06f), Vector3.zero, new Vector3(0.04f, board, 0.1f), trim);
            return t;
        }

        static Material faceMat;

        static Material FaceMaterial()
        {
            if (faceMat == null)
                faceMat = Mats.Textured("TargetFace", ProcTex.TargetFace(512), Color.white, 0.55f, 0.15f, Vector2.one);
            return faceMat;
        }

        // ------------------------------------------------------------------------------------ motion

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.Playing) return;
            float dt = Time.deltaTime;

            if (motion == TargetMotion.Rail)
            {
                railPhase += dt * railSpeed / Mathf.Max(0.5f, railHalf);
                // smooth back-and-forth with short pauses at the ends
                float s = Mathf.Sin(railPhase);
                float x = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f) * railHalf;
                transform.localPosition = railCenter + new Vector3(x, 0f, 0f);
            }
            else if (motion == TargetMotion.PopUp && hinge != null)
            {
                popTimer -= dt;
                if (popTimer <= 0f)
                {
                    up = !up;
                    popTimer = up ? upTime + Random.Range(-0.6f, 0.8f) : downTime + Random.Range(-0.4f, 0.6f);
                    Sfx.Play3D(SfxId.Switch, transform.position + Vector3.up, 0.5f, up ? 1.1f : 0.8f);
                }
                fold = Mathf.MoveTowards(fold, up ? 0f : 1f, dt / 0.28f);
                hinge.localRotation = Quaternion.Euler(fold * 88f, 0f, 0f);
            }

            if (flash > 0f && faceRenderer != null)
            {
                flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
                if (mpb == null) mpb = new MaterialPropertyBlock();
                float k = 0.55f + flash * 2.5f;
                mpb.SetColor("_EmissionColor", new Color(k, k, k, 1f));
                faceRenderer.SetPropertyBlock(mpb);
            }
        }

        public void ResetMotion()
        {
            up = true;
            fold = 0f;
            if (hinge != null) hinge.localRotation = Quaternion.identity;
        }
    }
}
