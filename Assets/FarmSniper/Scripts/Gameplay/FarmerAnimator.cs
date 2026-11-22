using UnityEngine;
using UnityEngine.Rendering;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Procedural animation for the farmer model: walking legs, crouch, upper body and arms following the aim pitch
    /// (the arms hold the rifle). In first person the body only casts a shadow, in third person it is fully visible.
    /// </summary>
    public class FarmerAnimator : MonoBehaviour
    {
        [Header("Bones (assigned by the scene builder)")]
        public Transform body;
        public Transform torso;
        public Transform headBone;
        public Transform legL, legR, kneeL, kneeR;
        public Transform armL, armR;

        [Header("Motion")]
        public float strideDegrees = 32f;
        public float strideFrequency = 1.6f;
        public float crouchDrop = 0.5f;
        [Range(0f, 1f)] public float torsoFollowsPitch = 0.55f;

        Renderer[] renderers;
        bool firstPerson = true;
        float phase, crouchK;
        Quaternion armLBase, armRBase;
        Vector3 bodyBase;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            if (armL != null) armLBase = armL.localRotation;
            if (armR != null) armRBase = armR.localRotation;
            if (body != null) bodyBase = body.localPosition;
        }

        public void SetFirstPerson(bool fp)
        {
            if (renderers == null || fp == firstPerson) return;
            firstPerson = fp;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        void Start()
        {
            // apply the initial state explicitly
            bool fp = firstPerson;
            firstPerson = !fp;
            SetFirstPerson(fp);
        }

        public void Animate(float speed, bool crouching, float pitch, bool grounded, float dt)
        {
            phase += speed * strideFrequency * dt;
            float k = Mathf.Clamp01(speed / 3.5f);
            crouchK = Mathf.MoveTowards(crouchK, crouching ? 1f : 0f, dt * 5f);
            float swing = Mathf.Sin(phase * Mathf.PI) * strideDegrees * k * (1f - crouchK * 0.5f);
            if (!grounded) swing = 15f;

            if (legL != null) legL.localRotation = Quaternion.Euler(swing - crouchK * 55f, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-swing - crouchK * 55f, 0f, 0f);
            float kneeBendL = Mathf.Max(0f, -Mathf.Sin(phase * Mathf.PI)) * 35f * k + crouchK * 100f;
            float kneeBendR = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI)) * 35f * k + crouchK * 100f;
            if (kneeL != null) kneeL.localRotation = Quaternion.Euler(kneeBendL, 0f, 0f);
            if (kneeR != null) kneeR.localRotation = Quaternion.Euler(kneeBendR, 0f, 0f);

            if (body != null)
                body.localPosition = bodyBase + Vector3.down * crouchK * crouchDrop + Vector3.up * Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * 0.03f * k;
            if (torso != null) torso.localRotation = Quaternion.Euler(pitch * torsoFollowsPitch + crouchK * 15f, 0f, 0f);
            if (headBone != null) headBone.localRotation = Quaternion.Euler(pitch * (1f - torsoFollowsPitch), 0f, 0f);
            if (armL != null) armL.localRotation = armLBase;
            if (armR != null) armR.localRotation = armRBase;
        }
    }
}
