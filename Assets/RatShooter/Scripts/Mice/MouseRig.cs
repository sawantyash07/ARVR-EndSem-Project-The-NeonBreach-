using System.Collections;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Procedural animation for the cartoon mouse (no Animator needed): scurrying gait with leg swing, body bob and
    /// squash, a wavy tail, head look-at, sniffing nose and whiskers, ear twitches, blinking, and poses for standing up,
    /// eating, grooming, crouching (with a scared shiver) and alert. Also the startle hop, burrow sink/emerge and the
    /// cartoon death (flip, X-eyes, legs in the air, orbiting stars). The AI only sets speed / pose / look direction.
    /// </summary>
    public class MouseRig : MonoBehaviour
    {
        public enum Pose { Idle, StandUp, Eat, Groom, Crouch, Alert }

        [Header("Bones (assigned by MouseFactory)")]
        public Transform visual;
        public Transform bodyPivot;
        public Transform body;
        public Transform headPivot;
        public Transform snout;
        public Transform whiskers;
        public Transform earL, earR;
        public Transform eyeL, eyeR;
        public Transform legFL, legFR, legBL, legBR;
        public Transform[] tail;
        public GameObject xEyes;
        public Transform starsPivot;

        [Header("Animation")]
        [Tooltip("Metres travelled per gait cycle.")]
        public float strideLength = 0.36f;
        public float legSwing = 48f;
        public float bobHeight = 0.035f;
        public float poseSharpness = 10f;
        [Tooltip("Height of the body centre above the feet, used for the death flip.")]
        public float bodyCenterHeight = 0.17f;

        // ---- inputs from the AI
        [System.NonSerialized] public float speed;
        [System.NonSerialized] public Pose pose = Pose.Idle;
        [System.NonSerialized] public float lookYaw;
        [System.NonSerialized] public float lookPitch;
        [System.NonSerialized] public bool shiver;
        [System.NonSerialized] public float sink;   // 0 = standing on the ground, 1 = fully inside a burrow

        public bool Dead { get; private set; }

        float scale = 1f;
        float t, phase;
        float curBodyPitch, curFrontLeg, curHeadPitch, curHeadRoll, curEarBack, curLift, curHeadYaw;
        float blinkTimer = 2f, blinkT = -1f;
        float earTimer = 1f, earTwitchL, earTwitchR;
        float hopY, hopV;
        bool grounded = true;
        float idleSeed;

        Quaternion bodyPivotRest, headRest, earLRest, earRRest, legFLRest, legFRRest, legBLRest, legBRRest, visualRest;
        Quaternion[] tailRest;
        Vector3 bodyRestScale, snoutRestScale, eyeRestScale, visualRestPos, visualRestScale;
        Renderer[] renderers;
        bool cached;

        public System.Action<Vector3> onLanded;

        void Awake() { Cache(); }

        void Cache()
        {
            if (cached || visual == null) return;
            cached = true;
            scale = visual.localScale.x;
            visualRestPos = visual.localPosition;
            visualRest = visual.localRotation;
            visualRestScale = visual.localScale;
            bodyPivotRest = bodyPivot.localRotation;
            headRest = headPivot.localRotation;
            earLRest = earL.localRotation; earRRest = earR.localRotation;
            legFLRest = legFL.localRotation; legFRRest = legFR.localRotation;
            legBLRest = legBL.localRotation; legBRRest = legBR.localRotation;
            bodyRestScale = body.localScale;
            snoutRestScale = snout.localScale;
            eyeRestScale = eyeL.localScale;
            tailRest = new Quaternion[tail.Length];
            for (int i = 0; i < tail.Length; i++) tailRest[i] = tail[i].localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            idleSeed = Random.value * 100f;
            if (xEyes != null) xEyes.SetActive(false);
            if (starsPivot != null) starsPivot.gameObject.SetActive(false);
        }

        public void SetVisible(bool on)
        {
            Cache();
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && (xEyes == null || !renderers[i].transform.IsChildOf(xEyes.transform)) &&
                    (starsPivot == null || !renderers[i].transform.IsChildOf(starsPivot)))
                    renderers[i].enabled = on;
        }

        /// <summary>Startle jump (cartoon hop).</summary>
        public void Hop(float height)
        {
            if (Dead) return;
            hopV = Mathf.Sqrt(2f * 12f * Mathf.Max(0.01f, height));
            grounded = false;
        }

        static float Damp(float cur, float target, float sharp, float dt) { return Mathf.Lerp(cur, target, 1f - Mathf.Exp(-sharp * dt)); }

        void LateUpdate()
        {
            if (!cached) Cache();
            float dt = Time.deltaTime;
            if (dt <= 0f || Dead || visual == null) return;
            t += dt;

            float run = Mathf.Clamp01(speed / 1.4f);
            phase += speed / Mathf.Max(0.05f, strideLength * scale) * dt;
            float p = phase * Mathf.PI * 2f;

            // ---- pose targets
            float bodyPitch = 0f, frontLeg = 0f, headPitch = 0f, headRoll = 0f, earBack = 0f, lift = 0f;
            float frontLegZ = 0f;
            switch (pose)
            {
                case Pose.StandUp:
                    bodyPitch = -52f; frontLeg = -70f; headPitch = 38f; earBack = -5f; lift = 0.01f;
                    break;
                case Pose.Eat:
                    bodyPitch = -38f; frontLeg = -98f + Mathf.Sin(t * 17f) * 10f; headPitch = 30f + Mathf.Sin(t * 17f) * 7f;
                    break;
                case Pose.Groom:
                    bodyPitch = -42f; frontLeg = -104f + Mathf.Sin(t * 9f) * 22f; frontLegZ = Mathf.Cos(t * 9f) * 14f;
                    headPitch = 28f; headRoll = Mathf.Sin(t * 4.5f) * 14f;
                    break;
                case Pose.Crouch:
                    bodyPitch = 6f; headPitch = 10f; earBack = -55f; lift = -0.025f;
                    break;
                case Pose.Alert:
                    bodyPitch = -16f; headPitch = 8f; earBack = 18f; frontLeg = -12f;
                    break;
            }
            float ps = poseSharpness * (pose == Pose.Eat || pose == Pose.Groom ? 1.6f : 1f);
            curBodyPitch = Damp(curBodyPitch, bodyPitch + Mathf.Sin(p * 2f) * 5f * run, ps, dt);
            curFrontLeg = Damp(curFrontLeg, frontLeg, ps, dt);
            curHeadPitch = Damp(curHeadPitch, headPitch + lookPitch, ps, dt);
            curHeadRoll = Damp(curHeadRoll, headRoll, ps, dt);
            curEarBack = Damp(curEarBack, earBack, 12f, dt);
            curLift = Damp(curLift, lift, ps, dt);

            // idle head wander (sniffing about) on top of the AI's look direction
            float wander = (Mathf.PerlinNoise(idleSeed, t * 0.6f) - 0.5f) * 40f * (1f - run);
            curHeadYaw = Damp(curHeadYaw, Mathf.Clamp(lookYaw + wander, -80f, 80f), 9f, dt);

            // ---- hop (startle) + burrow sink
            if (!grounded)
            {
                hopV -= 12f * dt;
                hopY += hopV * dt;
                if (hopY <= 0f) { hopY = 0f; hopV = 0f; grounded = true; }
            }
            float bob = Mathf.Abs(Mathf.Sin(p)) * bobHeight * run;
            float sinkK = Mathf.Clamp01(sink);
            visual.localPosition = visualRestPos + new Vector3(0f, (bob + hopY + curLift) * scale - sinkK * 0.5f * scale, 0f);
            visual.localScale = visualRestScale * (1f - sinkK * 0.25f);

            Quaternion shake = Quaternion.identity;
            if (shiver) shake = Quaternion.Euler(Mathf.Sin(t * 57f) * 2.2f, 0f, Mathf.Sin(t * 63f) * 3.2f);
            visual.localRotation = visualRest * shake;

            bodyPivot.localRotation = bodyPivotRest * Quaternion.Euler(curBodyPitch, 0f, 0f);

            // squash & stretch + breathing
            float breathe = Mathf.Sin(t * Mathf.PI * 2f * 1.4f) * 0.02f * (1f - run);
            float sq = Mathf.Sin(p * 2f) * 0.06f * run;
            body.localScale = Vector3.Scale(bodyRestScale, new Vector3(1f + sq * 0.5f + breathe, 1f - sq + breathe, 1f + 0.05f * run));

            // ---- legs
            float sw = legSwing * run;
            legFL.localRotation = legFLRest * Quaternion.Euler(curFrontLeg + Mathf.Sin(p) * sw, 0f, frontLegZ);
            legFR.localRotation = legFRRest * Quaternion.Euler(curFrontLeg + Mathf.Sin(p + 0.6f) * sw, 0f, -frontLegZ);
            legBL.localRotation = legBLRest * Quaternion.Euler(Mathf.Sin(p + Mathf.PI) * sw * 1.1f, 0f, 0f);
            legBR.localRotation = legBRRest * Quaternion.Euler(Mathf.Sin(p + Mathf.PI + 0.6f) * sw * 1.1f, 0f, 0f);

            // ---- head, snout, whiskers
            headPivot.localRotation = headRest * Quaternion.Euler(curHeadPitch, curHeadYaw, curHeadRoll);
            float sniff = run < 0.3f ? Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 6.5f)) : 0f;
            snout.localScale = Vector3.Scale(snoutRestScale, new Vector3(1f, 1f + sniff * 0.05f, 1f + sniff * 0.07f));
            if (whiskers != null) whiskers.localRotation = Quaternion.Euler(sniff * 6f, Mathf.Sin(t * 13f) * 3f * (1f - run), 0f);

            // ---- ears: random twitches, flatten when scared, perk when alert
            earTimer -= dt;
            if (earTimer <= 0f)
            {
                earTimer = Random.Range(1.2f, 3.5f);
                if (Random.value < 0.5f) earTwitchL = 1f; else earTwitchR = 1f;
            }
            earTwitchL = Mathf.MoveTowards(earTwitchL, 0f, dt * 5f);
            earTwitchR = Mathf.MoveTowards(earTwitchR, 0f, dt * 5f);
            earL.localRotation = earLRest * Quaternion.Euler(curEarBack - earTwitchL * 25f - run * 20f, 0f, 0f);
            earR.localRotation = earRRest * Quaternion.Euler(curEarBack - earTwitchR * 25f - run * 20f, 0f, 0f);

            // ---- blink
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blinkTimer = Random.Range(1.8f, 5f); blinkT = 0f; }
            float lid = 1f;
            if (blinkT >= 0f)
            {
                blinkT += dt;
                lid = Mathf.Abs(blinkT / 0.07f - 1f);
                if (blinkT > 0.14f) { blinkT = -1f; lid = 1f; }
            }
            Vector3 es = new Vector3(eyeRestScale.x, eyeRestScale.y * Mathf.Max(0.08f, lid), eyeRestScale.z);
            eyeL.localScale = es;
            eyeR.localScale = es;

            // ---- tail: lazy S-curve when idle, streams out (with a wiggle) when running
            float freq = Mathf.Lerp(2.2f, 7f, run);
            for (int i = 0; i < tail.Length; i++)
            {
                float k = (float)i / Mathf.Max(1, tail.Length - 1);
                float yaw = Mathf.Sin(t * freq - i * 0.55f) * (5f + k * 16f) * (1f - 0.4f * run);
                float pitch = Mathf.Lerp(i < 3 ? -7f : 5f, -2f, run) + (shiver ? Mathf.Sin(t * 50f + i) * 2f : 0f);
                tail[i].localRotation = tailRest[i] * Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        // ------------------------------------------------------------------ death

        public void PlayDeath(Vector3 hitDirWorld, System.Action<Vector3> onPoof, System.Action onDone)
        {
            if (Dead) return;
            Cache();
            Dead = true;
            StartCoroutine(DeathRoutine(hitDirWorld, onPoof, onDone));
        }

        IEnumerator DeathRoutine(Vector3 hitDirWorld, System.Action<Vector3> onPoof, System.Action onDone)
        {
            if (xEyes != null) xEyes.SetActive(true);
            eyeL.gameObject.SetActive(false);
            eyeR.gameObject.SetActive(false);
            shiver = false;
            // stunned expression: ears out, tongue-out style head tilt
            earL.localRotation = earLRest * Quaternion.Euler(25f, 0f, 20f);
            earR.localRotation = earRRest * Quaternion.Euler(25f, 0f, -20f);
            bodyPivot.localRotation = bodyPivotRest;
            body.localScale = bodyRestScale;

            Vector3 horiz = transform.InverseTransformDirection(new Vector3(hitDirWorld.x, 0f, hitDirWorld.z).normalized) * 1.1f;
            float spinDir = Random.value < 0.5f ? 1f : -1f;
            float centerH = bodyCenterHeight * scale;
            float vy = 4.2f, y = 0f, roll = 0f, time = 0f;
            Vector3 offset = Vector3.zero;
            Quaternion baseRot = visualRest;

            // launched into the air, spinning
            while (true)
            {
                float dt = Time.deltaTime;
                time += dt;
                vy -= 14f * dt;
                y += vy * dt;
                offset += horiz * dt;
                roll += 620f * spinDir * dt;
                bool landing = vy < 0f && y <= 0f && time > 0.15f;
                float targetRoll = landing ? 180f * spinDir : roll;
                Quaternion rot = baseRot * Quaternion.Euler(0f, 0f, landing ? targetRoll : roll);
                Vector3 c = visualRestPos + offset + new Vector3(0f, Mathf.Max(0f, y) + centerH, 0f);
                visual.localRotation = rot;
                visual.localPosition = c - rot * new Vector3(0f, centerH, 0f);
                for (int i = 0; i < tail.Length; i++) tail[i].localRotation = tailRest[i] * Quaternion.Euler(Mathf.Sin(time * 30f + i) * 12f, 0f, 0f);
                if (landing) break;
                yield return null;
            }

            // on its back, legs in the air, twitching, stars circling
            Quaternion backRot = baseRot * Quaternion.Euler(0f, 0f, 180f);
            visual.localRotation = backRot;
            Vector3 restC = visualRestPos + offset + new Vector3(0f, centerH * 0.85f, 0f);
            visual.localPosition = restC - backRot * new Vector3(0f, centerH, 0f);
            if (onLanded != null) onLanded(transform.TransformPoint(visualRestPos + offset));
            if (starsPivot != null)
            {
                starsPivot.gameObject.SetActive(true);
                starsPivot.localPosition = new Vector3(offset.x, starsPivot.localPosition.y, offset.z + 0.28f * scale);
            }
            float lie = 0f;
            while (lie < 1.05f)
            {
                float dt = Time.deltaTime;
                lie += dt;
                float tw = Mathf.Max(0f, 1f - lie) ;
                legFL.localRotation = legFLRest * Quaternion.Euler(-20f + Mathf.Sin(lie * 40f) * 25f * tw, 0f, 0f);
                legFR.localRotation = legFRRest * Quaternion.Euler(-20f + Mathf.Sin(lie * 37f + 1f) * 25f * tw, 0f, 0f);
                legBL.localRotation = legBLRest * Quaternion.Euler(-30f + Mathf.Sin(lie * 33f + 2f) * 20f * tw, 0f, 0f);
                legBR.localRotation = legBRRest * Quaternion.Euler(-30f + Mathf.Sin(lie * 35f + 3f) * 20f * tw, 0f, 0f);
                if (starsPivot != null) starsPivot.Rotate(0f, 320f * dt, 0f, Space.Self);
                // little bounce on landing
                float bounce = Mathf.Max(0f, Mathf.Sin(lie * 14f)) * Mathf.Max(0f, 0.3f - lie) * 0.15f;
                visual.localPosition = restC + new Vector3(0f, bounce, 0f) - backRot * new Vector3(0f, centerH, 0f);
                yield return null;
            }

            // poof!
            if (onPoof != null) onPoof(transform.TransformPoint(visualRestPos + offset + new Vector3(0f, centerH, 0f)));
            Vector3 startScale = visual.localScale;
            float s = 0f;
            while (s < 0.18f)
            {
                s += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(s / 0.18f);
                visual.localScale = startScale * (k * (1f + 0.3f * (1f - k)));
                if (starsPivot != null) starsPivot.localScale = Vector3.one * k;
                yield return null;
            }
            SetVisible(false);
            if (xEyes != null) xEyes.SetActive(false);
            if (starsPivot != null) starsPivot.gameObject.SetActive(false);
            if (onDone != null) onDone();
        }
    }
}
