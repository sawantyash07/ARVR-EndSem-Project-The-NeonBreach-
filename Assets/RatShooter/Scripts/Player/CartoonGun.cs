using System.Collections;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// The first-person rifle: semi-automatic hitscan shots from the screen centre (small spread from the hip, none
    /// when aiming down the sights), magazine + reserve ammunition, animated reload (magazine drops out, a new one
    /// slides in, bolt is racked), recoil kick, weapon sway / breathing, a cartoon muzzle flash with light, smoke,
    /// tracer, ejected casings and impact effects. Input uses the shared Neon Breach key bindings (fire, aim, reload).
    /// </summary>
    public class CartoonGun : MonoBehaviour
    {
        public struct ShotResult
        {
            public bool hitAnything;
            public MouseAI mouse;
            public bool headshot;
            public Vector3 origin, direction, point, normal;
        }

        [Header("Parts (set by GunFactory)")]
        public Transform model;
        public Transform muzzle;
        public Transform ejectPort;
        public Transform magazine;
        public Transform boltHandle;
        public GameObject muzzleFlash;
        public Light flashLight;

        [Header("Ammunition")]
        public int magazineSize = 8;
        public float fireInterval = 0.22f;
        public float reloadTime = 1.5f;

        [Header("Ballistics")]
        public float range = 160f;
        [Tooltip("Random spread in degrees when firing from the hip.")]
        public float hipSpread = 0.7f;
        [Tooltip("Random spread in degrees when aiming down the sights.")]
        public float aimSpread = 0.05f;

        [Header("View model")]
        public Vector3 hipPosition = new Vector3(0.165f, -0.185f, 0.36f);
        public Vector3 hipEuler = new Vector3(-1f, -2.6f, -4f);
        public Vector3 adsPosition = new Vector3(0f, -0.053f, 0.34f);
        public float aimSpeed = 7f;
        public float swayAmount = 0.0016f;
        public float recoilBack = 0.075f;
        public float recoilPitch = 13f;

        public int Ammo { get; private set; }
        public int Reserve { get; set; }
        public bool InfiniteReserve { get; set; }
        public bool Reloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public float AimBlend { get; private set; }
        public float LastShotTime { get; private set; }
        public bool InputEnabled { get; set; }
        /// <summary>0 = shown, 1 = lowered out of view (title screen).</summary>
        public float Lowered { get; set; }

        public System.Action<ShotResult> onShot;
        public System.Action onDryFire;

        RatShooterPlayer player;
        Camera cam;
        float nextFire;
        Vector3 swayPos;
        Vector3 swayRot;
        float kick, kickRoll;
        float reloadPose;
        float boltPull;
        Vector3 magRestPos;
        Quaternion magRestRot;
        Vector3 boltRestPos;
        float flashTimer;
        float autoReloadAt = -1f;
        float lowerSmooth = 1f;
        float t;
        Coroutine reloadCo;

        bool inited;

        void Awake() { Init(); }

        void Init()
        {
            if (inited || magazine == null || boltHandle == null) return;
            inited = true;
            player = GetComponentInParent<RatShooterPlayer>();
            cam = GetComponentInParent<Camera>();
            magRestPos = magazine.localPosition;
            magRestRot = magazine.localRotation;
            boltRestPos = boltHandle.localPosition;
            Ammo = magazineSize;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        public void ResetAmmo(int reserve, bool infinite)
        {
            Init();
            if (reloadCo != null) { StopCoroutine(reloadCo); reloadCo = null; }
            Reloading = false;
            ReloadProgress = 0f;
            magazine.localPosition = magRestPos;
            magazine.localRotation = magRestRot;
            Ammo = magazineSize;
            Reserve = reserve;
            InfiniteReserve = infinite;
            autoReloadAt = -1f;
        }

        public int TotalAmmo { get { return Ammo + Reserve; } }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!inited) Init();
            if (dt <= 0f || !inited) return;
            t += dt;

            bool aimHeld = InputEnabled && KeyBindings.Held(GameAction.Aim);
            AimBlend = Mathf.MoveTowards(AimBlend, aimHeld && !Reloading ? 1f : 0f, dt * aimSpeed);

            if (InputEnabled && Cursor.lockState == CursorLockMode.Locked)
            {
                if (KeyBindings.Pressed(GameAction.Fire)) Fire();
                if (KeyBindings.Pressed(GameAction.Reload)) StartReload();
            }
            if (autoReloadAt > 0f && Time.time >= autoReloadAt) { autoReloadAt = -1f; StartReload(); }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f)
                {
                    if (muzzleFlash != null) muzzleFlash.SetActive(false);
                    if (flashLight != null) flashLight.enabled = false;
                }
                else if (flashLight != null) flashLight.intensity = 5f * (flashTimer / 0.06f);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (model == null || !inited) return;
            if (dt > 0f)
            {
                Vector2 look = player != null ? player.LookDelta : Vector2.zero;
                float swayK = Mathf.Lerp(1f, 0.25f, AimBlend);
                Vector3 targetPos = new Vector3(-look.x, -look.y, 0f) * swayAmount * swayK;
                targetPos = Vector3.ClampMagnitude(targetPos, 0.03f);
                swayPos = Vector3.Lerp(swayPos, targetPos, 1f - Mathf.Exp(-dt * 9f));
                Vector3 targetRot = new Vector3(look.y * 0.6f, -look.x * 0.6f, -look.x * 0.9f) * swayK;
                targetRot = Vector3.ClampMagnitude(targetRot, 6f);
                swayRot = Vector3.Lerp(swayRot, targetRot, 1f - Mathf.Exp(-dt * 9f));

                kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-dt * 11f));
                kickRoll = Mathf.Lerp(kickRoll, 0f, 1f - Mathf.Exp(-dt * 9f));
                lowerSmooth = Mathf.Lerp(lowerSmooth, Lowered, 1f - Mathf.Exp(-dt * 6f));
            }

            float aim = Mathf.SmoothStep(0f, 1f, AimBlend);
            float breathe = Mathf.Lerp(1f, 0.2f, aim);
            Vector3 bob = new Vector3(Mathf.Sin(t * 0.9f) * 0.002f, Mathf.Sin(t * 1.8f) * 0.0025f, 0f) * breathe;

            Vector3 pos = Vector3.Lerp(hipPosition, adsPosition, aim) + swayPos + bob;
            pos += new Vector3(0f, kick * 0.012f, -kick * recoilBack * Mathf.Lerp(1f, 0.6f, aim));
            pos += new Vector3(-0.04f, -0.06f, -0.03f) * reloadPose;
            pos += new Vector3(0.05f, -0.35f, -0.05f) * lowerSmooth;

            Quaternion rot = Quaternion.Euler(Vector3.Lerp(hipEuler, Vector3.zero, aim));
            rot *= Quaternion.Euler(swayRot);
            rot *= Quaternion.Euler(-kick * recoilPitch * Mathf.Lerp(1f, 0.5f, aim), 0f, kickRoll);
            rot *= Quaternion.Euler(-12f * reloadPose, 8f * reloadPose, 38f * reloadPose);
            rot *= Quaternion.Euler(35f * lowerSmooth, 0f, 0f);
            model.localPosition = pos;
            model.localRotation = rot;

            boltHandle.localPosition = boltRestPos + new Vector3(0f, 0f, -0.06f * boltPull);
        }

        // ------------------------------------------------------------------ firing

        public void Fire()
        {
            if (Reloading || Time.time < nextFire || Lowered > 0.5f) return;
            if (Ammo <= 0)
            {
                nextFire = Time.time + 0.25f;
                if (Reserve > 0 || InfiniteReserve) StartReload();
                else
                {
                    RatAudio.Play2D(RatSound.Empty, 0.8f);
                    if (onDryFire != null) onDryFire();
                }
                return;
            }
            Ammo--;
            nextFire = Time.time + fireInterval;
            LastShotTime = Time.time;

            Transform ct = cam != null ? cam.transform : transform;
            float spread = Mathf.Lerp(hipSpread, aimSpread, AimBlend);
            Vector2 rnd = Random.insideUnitCircle * spread;
            Vector3 dir = Quaternion.AngleAxis(rnd.x, ct.up) * Quaternion.AngleAxis(rnd.y, ct.right) * ct.forward;
            Vector3 origin = ct.position;

            ShotResult res = new ShotResult { origin = origin, direction = dir, point = origin + dir * range, normal = -dir };
            RaycastHit hit;
            int mask = Layers.ShootMask;
            if (Physics.Raycast(origin, dir, out hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                res.hitAnything = true;
                res.point = hit.point;
                res.normal = hit.normal;
                MouseAI m = hit.collider.GetComponentInParent<MouseAI>();
                if (m != null && m.IsAlive)
                {
                    res.mouse = m;
                    res.headshot = hit.collider.gameObject.layer == Layers.EnemyHead;
                }
                else
                {
                    RatSurface s = hit.collider.GetComponentInParent<RatSurface>();
                    RatFX.Impact(hit.point, hit.normal, s != null ? s.kind : SurfaceKind.Dirt);
                    RatAudio.Play3D(RatSound.Thud, hit.point, 0.35f, Random.Range(1.3f, 1.7f));
                }
            }

            // presentation
            if (muzzleFlash != null)
            {
                muzzleFlash.SetActive(true);
                muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                muzzleFlash.transform.localScale = Vector3.one * Random.Range(0.85f, 1.25f);
            }
            if (flashLight != null) { flashLight.enabled = true; flashLight.intensity = 5f; }
            flashTimer = 0.06f;
            RatFX.Tracer(muzzle.position, res.point);
            RatFX.MuzzleSmoke(muzzle.position, muzzle.forward);
            StartCoroutine(EjectLater(0.07f));
            RatAudio.Play2D(RatSound.Shot, 0.85f, Random.Range(0.95f, 1.06f));
            kick = 1f;
            kickRoll = Random.Range(-6f, 6f);
            if (player != null) player.AddRecoil(Random.Range(1.4f, 2.1f) * Mathf.Lerp(1f, 0.6f, AimBlend), Random.Range(-0.5f, 0.5f));

            if (onShot != null) onShot(res);

            if (Ammo == 0 && (Reserve > 0 || InfiniteReserve)) autoReloadAt = Time.time + 0.4f;
        }

        IEnumerator EjectLater(float delay)
        {
            float d = 0f;
            while (d < delay) { d += Time.deltaTime; yield return null; }
            if (ejectPort == null) yield break;
            Vector3 v = ejectPort.right * Random.Range(1.4f, 2f) + ejectPort.up * Random.Range(1.2f, 1.8f) - ejectPort.forward * 0.3f;
            RatFX.EjectCasing(ejectPort.position, ejectPort.rotation * Quaternion.Euler(0f, 90f, 0f), v);
            boltPull = 0f;
        }

        // ------------------------------------------------------------------ reload

        public bool CanReload { get { return !Reloading && Ammo < magazineSize && (Reserve > 0 || InfiniteReserve) && Lowered < 0.5f; } }

        public void StartReload()
        {
            if (!CanReload) return;
            autoReloadAt = -1f;
            reloadCo = StartCoroutine(ReloadRoutine());
        }

        IEnumerator ReloadRoutine()
        {
            Reloading = true;
            ReloadProgress = 0f;
            float time = 0f;
            bool dropped = false, inserted = false, racked = false;
            Vector3 below = magRestPos + new Vector3(0f, -0.22f, -0.02f);
            while (time < reloadTime)
            {
                float dt = Time.deltaTime;
                time += dt;
                float u = time / reloadTime;
                ReloadProgress = Mathf.Clamp01(u);
                reloadPose = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.15f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.82f) / 0.18f)));

                if (!dropped && u > 0.16f)
                {
                    dropped = true;
                    DropMagazine();
                    magazine.localPosition = below;
                    RatAudio.Play2D(RatSound.MagOut, 0.8f);
                }
                if (dropped && !inserted)
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.34f) / 0.22f));
                    magazine.localPosition = Vector3.Lerp(below, magRestPos, k);
                }
                if (!inserted && u > 0.56f)
                {
                    inserted = true;
                    magazine.localPosition = magRestPos;
                    RatAudio.Play2D(RatSound.MagIn, 0.85f);
                    int need = magazineSize - Ammo;
                    int take = InfiniteReserve ? need : Mathf.Min(need, Reserve);
                    Ammo += take;
                    if (!InfiniteReserve) Reserve -= take;
                }
                if (!racked && u > 0.66f) { racked = true; RatAudio.Play2D(RatSound.Bolt, 0.8f); }
                boltPull = u > 0.66f && u < 0.86f ? Mathf.Sin((u - 0.66f) / 0.2f * Mathf.PI) : 0f;
                yield return null;
            }
            reloadPose = 0f;
            boltPull = 0f;
            Reloading = false;
            ReloadProgress = 1f;
            reloadCo = null;
        }

        void DropMagazine()
        {
            GameObject drop = Instantiate(magazine.gameObject, magazine.position, magazine.rotation);
            drop.name = "DroppedMagazine";
            foreach (Transform c in drop.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = Layers.FX;
            drop.transform.localScale = magazine.lossyScale;
            BoxCollider bc = drop.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, -0.05f, 0f);
            bc.size = new Vector3(0.05f, 0.11f, 0.07f);
            Rigidbody rb = drop.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            Vector3 carry = player != null ? player.transform.forward * 0.2f : Vector3.zero;
            rb.linearVelocity = -magazine.up * 1.5f + carry;
            rb.angularVelocity = new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 3f), Random.Range(-6f, 6f));
            Destroy(drop, 2.5f);
        }
    }
}
