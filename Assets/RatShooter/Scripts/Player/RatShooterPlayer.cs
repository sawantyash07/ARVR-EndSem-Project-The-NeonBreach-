using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// First-person camera for the farmer standing on the porch: smoothed mouse aiming within a comfortable arc,
    /// recoil that kicks the view and recovers, light screen shake, breathing sway, FOV zoom when aiming down the
    /// sights, and a slow cinematic fly-around for the title screen that blends smoothly into the shooting view.
    /// Uses the shared Neon Breach sensitivity / invert-Y settings.
    /// </summary>
    public class RatShooterPlayer : MonoBehaviour
    {
        [Header("Rig")]
        public Transform yawPivot;
        public Transform pitchPivot;
        public Camera cam;
        public CartoonGun gun;

        [Header("Aiming")]
        [Tooltip("Degrees per mouse unit, multiplied by the Settings sensitivity.")]
        public float sensitivity = 2.1f;
        public float minYaw = -75f, maxYaw = 75f;
        public float minPitch = -32f, maxPitch = 14f;
        [Tooltip("Higher = snappier, lower = smoother camera.")]
        public float smoothing = 24f;
        public float hipFov = 62f;
        public float aimFov = 40f;
        [Range(0.2f, 1f)] public float aimSensitivity = 0.55f;

        [Header("Title camera")]
        public Vector3 titleCenter = new Vector3(0f, 0.6f, 16f);
        public float titleRadius = 15f;
        public float titleHeight = 4.2f;
        public float titleSpeed = 0.045f;

        public bool ControlEnabled { get; set; }
        public Vector2 LookDelta { get; private set; }
        public float Yaw { get { return yaw; } }

        float yaw, pitch, targetYaw, targetPitch;
        float kickPitch, kickYaw, recoilPitch, recoilYaw;
        float trauma;
        float fovKick;
        float titleBlend = 1f, titleTarget = 1f;
        float titleAngle;
        float t;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (gun == null) gun = GetComponentInChildren<CartoonGun>();
            titleAngle = Random.Range(-0.6f, 0.6f);
        }

        public void SetTitleMode(bool on, bool instant = false)
        {
            titleTarget = on ? 1f : 0f;
            if (instant) titleBlend = titleTarget;
            if (!on) { yaw = targetYaw = 0f; pitch = targetPitch = -6f; }
        }

        public bool InTitleView { get { return titleBlend > 0.5f; } }
        public float TitleBlend { get { return titleBlend; } }

        public void AddRecoil(float up, float side)
        {
            kickPitch += up;
            kickYaw += side;
            trauma = Mathf.Min(1f, trauma + 0.25f);
            fovKick = 1.6f;
        }

        public void Shake(float amount) { trauma = Mathf.Min(1f, trauma + amount); }

        /// <summary>Point the view at a world position (within the aiming limits). Used by scripted tests and demos.</summary>
        public void AimAt(Vector3 world, bool instant = false)
        {
            if (pitchPivot == null) return;
            Vector3 d = world - pitchPivot.position;
            float y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - transform.eulerAngles.y;
            float p = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            targetYaw = Mathf.Clamp(Mathf.DeltaAngle(0f, y), minYaw, maxYaw);
            targetPitch = Mathf.Clamp(p, minPitch, maxPitch);
            if (instant)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                pitchPivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
                if (titleBlend <= 0f && cam != null) { cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity; }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 raw = Vector2.zero;
            if (ControlEnabled && dt > 0f && Cursor.lockState == CursorLockMode.Locked) raw = DesktopInput.MouseDelta();
            LookDelta = raw;
            if (dt <= 0f) return;
            t += dt;

            float aim = gun != null ? gun.AimBlend : 0f;
            float sens = sensitivity * GameSettings.Sensitivity * Mathf.Lerp(1f, aimSensitivity, aim);
            targetYaw = Mathf.Clamp(targetYaw + raw.x * sens, minYaw, maxYaw);
            targetPitch = Mathf.Clamp(targetPitch + raw.y * sens * (GameSettings.InvertY ? -1f : 1f), minPitch, maxPitch);
            float k = 1f - Mathf.Exp(-smoothing * dt);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);

            // recoil: instant kick, smooth recovery
            kickPitch = Mathf.Lerp(kickPitch, 0f, 1f - Mathf.Exp(-dt * 7f));
            kickYaw = Mathf.Lerp(kickYaw, 0f, 1f - Mathf.Exp(-dt * 7f));
            recoilPitch = Mathf.Lerp(recoilPitch, kickPitch, 1f - Mathf.Exp(-dt * 35f));
            recoilYaw = Mathf.Lerp(recoilYaw, kickYaw, 1f - Mathf.Exp(-dt * 35f));
            trauma = Mathf.Max(0f, trauma - dt * 2.2f);
            fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-dt * 10f));

            titleBlend = Mathf.MoveTowards(titleBlend, titleTarget, dt / 1.4f);
            titleAngle += dt * titleSpeed;
        }

        void LateUpdate()
        {
            if (yawPivot == null || pitchPivot == null || cam == null) return;
            float aim = gun != null ? gun.AimBlend : 0f;
            float breathe = Mathf.Lerp(0.18f, 0.05f, aim);
            float shake = trauma * trauma * Mathf.Max(0.2f, GameSettings.ShakeIntensity);
            float sx = (Mathf.PerlinNoise(t * 22f, 0.3f) - 0.5f) * 2f * shake * 1.2f;
            float sy = (Mathf.PerlinNoise(0.7f, t * 22f) - 0.5f) * 2f * shake * 1.2f;

            yawPivot.localRotation = Quaternion.Euler(0f, yaw + recoilYaw + sy + Mathf.Sin(t * 0.7f) * breathe, 0f);
            pitchPivot.localRotation = Quaternion.Euler(-(pitch + recoilPitch) + sx + Mathf.Sin(t * 1.3f) * breathe * 0.7f, 0f, 0f);

            float fov = Mathf.Lerp(hipFov, aimFov, Mathf.SmoothStep(0f, 1f, aim)) + fovKick;

            // title fly-around, blended with the porch view
            float b = Mathf.SmoothStep(0f, 1f, titleBlend);
            if (b > 0.0001f)
            {
                float a = titleAngle;
                Vector3 tp = titleCenter + new Vector3(Mathf.Sin(a) * titleRadius, titleHeight + Mathf.Sin(a * 2.3f) * 0.8f, -Mathf.Cos(a) * titleRadius * 0.75f);
                Quaternion tr = Quaternion.LookRotation((titleCenter + new Vector3(Mathf.Sin(a * 0.7f) * 2f, 0f, 0f)) - tp);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(pitchPivot.position, tp, b), Quaternion.Slerp(pitchPivot.rotation, tr, b));
                fov = Mathf.Lerp(fov, 50f, b);
            }
            else
            {
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
            }
            cam.fieldOfView = fov;
        }
    }
}
