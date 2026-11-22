using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Everything a player can do with a weapon: three guns (pistol / rifle / shotgun), hitscan shooting with
    /// spread and recoil, reloading, aim-down-sights, weapon switching, grenades and melee.
    /// The first-person weapon models are built from primitives at startup.
    /// Works for both the desktop camera and a VR hand (see SetVR).
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class WeaponSystem : MonoBehaviour
    {
        public PlayerController player;
        public Camera cam;
        public Transform holder;

        class Slot
        {
            public WeaponStats stats;
            public int mag;
            public int reserve;
            public GameObject model;
            public Transform muzzle;
        }

        Slot[] slots;
        int current;
        int pendingSlot;

        public int Grenades = 3;
        public const int MaxGrenades = 6;

        bool reloading;
        float reloadTimer;
        float nextFire;
        float autoReloadAt = -1f;
        float adsAmount;
        float sprintBlend;
        float switchT = -1f;
        bool swapped;
        float meleeT = -1f;
        float nextMelee;
        float nextGrenade;
        float kick;
        Vector3 sway;

        GameObject flashRoot;
        Light flashLight;
        float flashTimer;

        bool vrMode;
        Transform vrHand;
        const float VrPitch = 0f;

        readonly List<Enemy> tmpEnemies = new List<Enemy>();

        // ------------------------------------------------------------------ public read-only state (HUD etc.)

        public WeaponStats Current { get { return slots[current].stats; } }
        public int CurrentIndex { get { return current; } }
        public int SlotCount { get { return slots == null ? 0 : slots.Length; } }
        public string SlotName(int i) { return slots[i].stats.name; }
        public int Mag { get { return slots[current].mag; } }
        public int CurrentMagSize { get { return MagSize(slots[current]); } }
        public int ShotsFiredLife { get; private set; }
        public int ReloadsDone { get; private set; }
        public int GrenadesThrown { get; private set; }
        public int MeleeHits { get; private set; }

        static int MagSize(Slot s) { return Mathf.Max(1, Mathf.RoundToInt(s.stats.magSize * Upgrades.CapacityMult)); }
        static int MaxReserve(Slot s) { return Mathf.RoundToInt(s.stats.maxReserve * Upgrades.CapacityMult); }
        static float ReloadTime(Slot s) { return s.stats.reloadTime * Upgrades.ReloadTimeMult; }
        public int Reserve { get { return slots[current].reserve; } }
        public bool IsReloading { get { return reloading; } }
        public float ReloadProgress
        {
            get
            {
                if (!reloading) return 0f;
                WeaponStats st = slots[current].stats;
                if (st.shellReload)
                {
                    return Mathf.Clamp01(reloadTimer / ReloadTime(slots[current]));
                }
                return Mathf.Clamp01(reloadTimer / ReloadTime(slots[current]));
            }
        }
        public float AdsAmount { get { return adsAmount; } }
        public bool IsAds { get { return adsAmount > 0.5f; } }
        public bool TargetingEnemy { get; private set; }
        public Transform CurrentMuzzle { get { return slots[current].muzzle; } }

        /// <summary>Current cone of fire in degrees (before movement penalties) - drives the crosshair size.</summary>
        public float CurrentSpreadDeg
        {
            get
            {
                WeaponStats st = Current;
                float s = Mathf.Lerp(st.hipSpread, st.adsSpread, adsAmount);
                return s * (player != null ? player.SpreadMultiplier : 1f);
            }
        }

        Transform AimT { get { return (vrMode && vrHand != null) ? CurrentMuzzle : cam.transform; } }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (holder == null)
            {
                Transform h = transform.Find("WeaponHolder");
                if (h == null)
                {
                    GameObject go = new GameObject("WeaponHolder");
                    go.transform.SetParent(transform, false);
                    h = go.transform;
                }
                holder = h;
            }
            BuildSlots();
            BuildFlash();
            ResetAll();
        }

        void BuildSlots()
        {
            WeaponStats[] all = WeaponLibrary.Create();
            slots = new Slot[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                Slot s = new Slot();
                s.stats = all[i];
                GameObject model = new GameObject(all[i].name);
                model.transform.SetParent(holder, false);
                s.muzzle = BuildModel(i, model.transform);
                Util.NoShadows(model);
                model.SetActive(i == 0);
                s.model = model;
                slots[i] = s;
            }
        }

        void BuildFlash()
        {
            flashRoot = new GameObject("MuzzleFlash");
            flashRoot.transform.SetParent(holder, false);
            Util.Part(flashRoot.transform, PrimitiveType.Sphere, "Core", Vector3.zero, Vector3.zero, Vector3.one * 0.1f, "NeonYellow");
            flashLight = Util.AddLight(flashRoot.transform, LightType.Point, new Color(1f, 0.75f, 0.35f), 3f, 7f, Vector3.zero);
            Util.NoShadows(flashRoot);
            flashRoot.SetActive(false);
        }

        public void ResetAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].mag = slots[i].stats.magSize;
                slots[i].reserve = slots[i].stats.startReserve;
                ShotsFiredLife = 0; ReloadsDone = 0; GrenadesThrown = 0; MeleeHits = 0;
                slots[i].model.SetActive(i == 0);
            }
            current = 0;
            pendingSlot = 0;
            Grenades = 3;
            reloading = false;
            switchT = -1f;
            meleeT = -1f;
            adsAmount = 0f;
            kick = 0f;
            if (holder != null) holder.gameObject.SetActive(true);
            if (flashRoot != null) flashRoot.SetActive(false);
        }

        public void AddAmmo(float fraction)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int add = Mathf.CeilToInt(MaxReserve(slots[i]) * fraction);
                slots[i].reserve = Mathf.Min(MaxReserve(slots[i]), slots[i].reserve + add);
            }
        }

        /// <summary>Shop item: fill every magazine and reserve to the (upgraded) maximum.</summary>
        public void RefillAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].mag = MagSize(slots[i]);
                slots[i].reserve = MaxReserve(slots[i]);
            }
        }

        public void AddGrenades(int n) { Grenades = Mathf.Min(MaxGrenades, Grenades + n); }

        /// <summary>Move the weapon between the desktop camera and a VR controller.</summary>
        public void SetVR(bool on, Transform hand)
        {
            vrMode = on && hand != null;
            vrHand = hand;
            if (vrMode)
            {
                holder.SetParent(hand, false);
                holder.localPosition = Vector3.zero;
                holder.localRotation = Quaternion.Euler(VrPitch, 0f, 0f);
            }
            else
            {
                holder.SetParent(transform, false);
                holder.localPosition = new Vector3(0.23f, -0.21f, 0.45f);
                holder.localRotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ per-frame logic

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || player == null || slots == null) return;
            float dt = Time.deltaTime;

            if (gm.State == GameState.Menu)
            {
                AnimateViewmodel(dt);
                return;
            }
            if (!gm.Playing) return;

            PlayerInputState inp = player.Inp;

            // --- switching
            if (inp.weaponSelect >= 0) RequestSwitch(inp.weaponSelect);
            if (inp.weaponCycle != 0) RequestSwitch((current + inp.weaponCycle + slots.Length) % slots.Length);
            if (switchT >= 0f)
            {
                switchT += dt / 0.32f;
                if (switchT >= 0.5f && !swapped)
                {
                    swapped = true;
                    slots[current].model.SetActive(false);
                    current = pendingSlot;
                    slots[current].model.SetActive(true);
                }
                if (switchT >= 1f) { switchT = -1f; swapped = false; }
            }

            bool busy = switchT >= 0f || meleeT >= 0f;

            // --- aim down sights
            bool wantAds = inp.aimHeld && !player.Sprinting && !busy && (!reloading || Current.shellReload);
            adsAmount = Mathf.MoveTowards(adsAmount, wantAds ? 1f : 0f, dt * 9f);

            // --- reload
            if (!busy && inp.reloadPressed) StartReload();
            if (autoReloadAt > 0f && Time.time >= autoReloadAt)
            {
                autoReloadAt = -1f;
                if (slots[current].mag == 0) StartReload();
            }
            if (reloading) UpdateReload(dt);

            // --- fire
            if (!busy && !player.Sprinting)
            {
                bool trigger = Current.automatic ? inp.fireHeld : inp.firePressed;
                if (trigger) TryFire();
            }

            // --- grenade + melee
            if (inp.grenadePressed) ThrowGrenade();
            if (inp.meleePressed) Melee();

            // --- crosshair target
            RaycastHit h;
            Transform aim = AimT;
            TargetingEnemy = Physics.Raycast(aim.position, aim.forward, out h, 120f, Layers.ShootMask, QueryTriggerInteraction.Ignore)
                             && (h.collider.gameObject.layer == Layers.Enemy || h.collider.gameObject.layer == Layers.EnemyHead);

            // --- muzzle flash timer
            if (flashRoot.activeSelf)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) flashRoot.SetActive(false);
            }

            AnimateViewmodel(dt);
        }

        void RequestSwitch(int idx)
        {
            if (idx < 0 || idx >= slots.Length || idx == current || switchT >= 0f) return;
            pendingSlot = idx;
            switchT = 0f;
            swapped = false;
            reloading = false;
            Sfx.Play2D(SfxId.Switch, 0.7f);
        }

        // ------------------------------------------------------------------ reload

        void StartReload()
        {
            Slot s = slots[current];
            if (reloading || s.mag >= MagSize(s) || s.reserve <= 0) return;
            reloading = true;
            reloadTimer = 0f;
            Sfx.Play3D(SfxId.Reload, transform.position, 0.8f);
        }

        void UpdateReload(float dt)
        {
            Slot s = slots[current];
            reloadTimer += dt;
            if (s.stats.shellReload)
            {
                float per = ReloadTime(s);
                if (reloadTimer >= per)
                {
                    reloadTimer -= per;
                    s.mag++;
                    s.reserve--;
                    Sfx.Play3D(SfxId.Reload, transform.position, 0.6f, 1.15f);
                    Haptics.Tick(player, UnityEngine.XR.XRNode.RightHand);
                    if (s.mag >= MagSize(s) || s.reserve <= 0)
                    {
                        reloading = false;
                        ReloadsDone++;
                        Sfx.Play3D(SfxId.ReloadEnd, transform.position, 0.8f);
                    }
                }
            }
            else if (reloadTimer >= ReloadTime(s))
            {
                int take = Mathf.Min(MagSize(s) - s.mag, s.reserve);
                s.mag += take;
                s.reserve -= take;
                reloading = false;
                ReloadsDone++;
                Sfx.Play3D(SfxId.ReloadEnd, transform.position, 0.8f);
                Haptics.Tick(player, UnityEngine.XR.XRNode.RightHand);
            }
        }

        // ------------------------------------------------------------------ shooting

        void TryFire()
        {
            Slot s = slots[current];
            if (Time.time < nextFire) return;
            if (reloading)
            {
                if (s.stats.shellReload && s.mag > 0) reloading = false;   // interrupt shell reload
                else return;
            }
            if (s.mag <= 0)
            {
                nextFire = Time.time + 0.3f;
                Sfx.Play3D(SfxId.Dry, transform.position, 0.8f);
                if (s.reserve > 0) StartReload();
                return;
            }
            s.mag--;
            nextFire = Time.time + 1f / (s.stats.fireRate * Upgrades.FireRateMult);
            Shoot(s);
            if (s.mag == 0 && s.reserve > 0) autoReloadAt = Time.time + 0.35f;
        }

        void Shoot(Slot s)
        {
            WeaponStats st = s.stats;
            Transform aim = AimT;
            Vector3 origin = aim.position;
            Vector3 fwd = aim.forward;
            Vector3 right = aim.right;
            Vector3 up = aim.up;
            float spread = CurrentSpreadDeg;
            GameManager gm = GameManager.Instance;
            gm.ShotsFired++;
            ShotsFiredLife++;
            bool anyHit = false;
            float dmgMult = Upgrades.DamageMult * PowerUps.DamageMult;

            for (int i = 0; i < st.pellets; i++)
            {
                Vector2 r = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
                Vector3 dir = (fwd + right * r.x + up * r.y).normalized;
                Vector3 end = origin + dir * st.range;
                RaycastHit hit;
                if (Physics.Raycast(origin, dir, out hit, st.range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        if (!target.IsDead)
                        {
                            HitZone zone = hit.collider.GetComponent<HitZone>();
                            float mult = zone != null ? zone.multiplier : 1f;
                            bool head = zone != null && zone.isHead;
                            float fall = Mathf.Lerp(1f, st.falloff, Mathf.Clamp01(hit.distance / st.range));
                            target.TakeDamage(st.damage * mult * fall * dmgMult, hit.point, head, player.gameObject);
                            anyHit = true;
                        }
                    }
                    else
                    {
                        FX.Impact(hit.point, hit.normal, st.tracer);
                        FX.Decal(hit.point, hit.normal);
                    }
                }
                FX.Tracer(s.muzzle.position, end, st.tracer);
            }
            if (anyHit) gm.ShotsHit++;
            if (!gm.InTutorial) RunAnalytics.OnShot(current, anyHit);
            Haptics.OnShot(player, st);

            // feedback
            flashRoot.transform.SetParent(s.muzzle, false);
            flashRoot.transform.localPosition = Vector3.zero;
            float fs = Random.Range(0.7f, 1.5f) * (st.pellets > 1 ? 1.8f : 1f);
            flashRoot.transform.localScale = new Vector3(fs, fs, fs);
            flashLight.color = Color.Lerp(st.tracer, Color.white, 0.4f);
            flashRoot.SetActive(true);
            flashTimer = 0.045f;

            Sfx.Play3D(st.sound, s.muzzle.position, 0.9f, Random.Range(0.95f, 1.05f));
            float recoilScale = Mathf.Lerp(1f, 0.6f, adsAmount);
            player.AddRecoil(st.recoil * recoilScale, Random.Range(-0.35f, 0.35f) * st.recoil * recoilScale);
            player.Shake(0.004f * st.recoil);
            kick = Mathf.Min(kick + st.recoil * 0.014f, 0.16f);
        }

        // ------------------------------------------------------------------ grenade + melee

        void ThrowGrenade()
        {
            if (Grenades <= 0 || Time.time < nextGrenade || switchT >= 0f) return;
            Grenades--;
            GrenadesThrown++;
            nextGrenade = Time.time + 0.8f;
            Transform aim = AimT;
            Vector3 origin = aim.position + aim.forward * 0.6f - aim.up * 0.15f;
            Vector3 vel = aim.forward * 17f + Vector3.up * 3.5f + player.Velocity * 0.5f;
            Grenade.Spawn(origin, vel, player);
            Sfx.Play3D(SfxId.Throw, origin, 0.8f);
        }

        void Melee()
        {
            if (Time.time < nextMelee || switchT >= 0f) return;
            nextMelee = Time.time + 0.7f;
            meleeT = 0f;
            reloading = false;
            Transform aim = AimT;
            Sfx.Play3D(SfxId.Melee, aim.position, 0.8f);

            Vector3 center = aim.position + aim.forward * 1.3f;
            Collider[] cols = Physics.OverlapSphere(center, 1.4f, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            tmpEnemies.Clear();
            for (int i = 0; i < cols.Length; i++)
            {
                Enemy e = cols[i].GetComponentInParent<Enemy>();
                if (e == null || e.IsDead || tmpEnemies.Contains(e)) continue;
                tmpEnemies.Add(e);
            }
            for (int i = 0; i < tmpEnemies.Count; i++)
            {
                Enemy e = tmpEnemies[i];
                Vector3 flat = e.transform.position - player.transform.position;
                flat.y = 0f;
                e.Knock(flat, 10f);
                e.TakeDamage(55f * Upgrades.DamageMult * PowerUps.DamageMult, e.transform.position + Vector3.up * 1.1f, false, player.gameObject);
            }
            MeleeHits += tmpEnemies.Count;
            // melee also knocks over tutorial targets
            for (int i = 0; i < cols.Length; i++)
            {
                TrainingTarget tt = cols[i].GetComponentInParent<TrainingTarget>();
                if (tt != null) tt.TakeDamage(55f, tt.transform.position + Vector3.up, false, player.gameObject);
            }
            if (tmpEnemies.Count > 0) player.Shake(0.06f);
        }

        // ------------------------------------------------------------------ viewmodel animation

        void AnimateViewmodel(float dt)
        {
            if (holder == null || player == null) return;
            kick = Mathf.MoveTowards(kick, 0f, dt * 0.55f);

            Vector3 pos;
            Quaternion rot;

            if (vrMode)
            {
                pos = new Vector3(0f, 0f, -kick * 0.6f);
                rot = Quaternion.Euler(VrPitch - kick * 120f, 0f, 0f);
                if (reloading) rot *= Quaternion.Euler(0f, 0f, 35f * Mathf.Sin(ReloadProgress * Mathf.PI));
                holder.localPosition = pos;
                holder.localRotation = rot;
                return;
            }

            Vector3 hip = new Vector3(0.23f, -0.21f, 0.45f);
            pos = Vector3.Lerp(hip, Current.adsOffset, adsAmount);
            rot = Quaternion.identity;

            float sp = (player.Sprinting && !player.IsDead) ? 1f : 0f;
            sprintBlend = Mathf.MoveTowards(sprintBlend, sp, dt * 6f);

            // recoil kick-back
            pos += new Vector3(0f, kick * 0.15f, -kick);
            rot *= Quaternion.Euler(-kick * 90f, 0f, 0f);

            // sway from mouse movement + walk bob
            Vector2 look = player.LookDelta;
            sway = Vector3.Lerp(sway, new Vector3(-look.x, -look.y, 0f) * 0.012f, 1f - Mathf.Exp(-12f * dt));
            sway = Vector3.ClampMagnitude(sway, 0.05f);
            pos += sway * (1f - adsAmount * 0.8f);
            pos += player.BobOffset * (1f - adsAmount * 0.85f);

            // reload
            if (reloading)
            {
                float p = Mathf.Sin(Mathf.Clamp01(ReloadProgress) * Mathf.PI);
                if (Current.shellReload) p = 0.5f;
                pos += new Vector3(-0.04f * p, -0.14f * p, 0f);
                rot *= Quaternion.Euler(32f * p, 8f * p, -22f * p);
            }

            // weapon switch (lower, swap, raise)
            if (switchT >= 0f)
            {
                float lower = Mathf.Sin(Mathf.Clamp01(switchT) * Mathf.PI);
                pos += new Vector3(0f, -0.35f * lower, -0.05f * lower);
                rot *= Quaternion.Euler(45f * lower, 0f, 0f);
            }

            // sprint pose
            pos += new Vector3(-0.1f, -0.07f, -0.04f) * sprintBlend;
            rot *= Quaternion.Euler(18f * sprintBlend, -38f * sprintBlend, 6f * sprintBlend);

            // melee swing
            if (meleeT >= 0f)
            {
                meleeT += dt / 0.35f;
                float m = Mathf.Sin(Mathf.Clamp01(meleeT) * Mathf.PI);
                pos += new Vector3(-0.22f * m, 0.05f * m, 0.18f * m);
                rot *= Quaternion.Euler(-35f * m, 55f * m, -15f * m);
                if (meleeT >= 1f) meleeT = -1f;
            }

            float k = 1f - Mathf.Exp(-22f * dt);
            holder.localPosition = Vector3.Lerp(holder.localPosition, pos, k);
            holder.localRotation = Quaternion.Slerp(holder.localRotation, rot, k);
        }

        // ------------------------------------------------------------------ model construction

        static void P(Transform r, PrimitiveType t, float px, float py, float pz, float rx, float ry, float rz,
                      float sx, float sy, float sz, string mat)
        {
            Util.Part(r, t, t.ToString(), new Vector3(px, py, pz), new Vector3(rx, ry, rz), new Vector3(sx, sy, sz), mat);
        }

        static Transform MuzzlePoint(Transform r, float x, float y, float z)
        {
            GameObject m = new GameObject("Muzzle");
            m.transform.SetParent(r, false);
            m.transform.localPosition = new Vector3(x, y, z);
            return m.transform;
        }

        static Transform BuildModel(int index, Transform r)
        {
            const PrimitiveType Cube = PrimitiveType.Cube;
            const PrimitiveType Cyl = PrimitiveType.Cylinder;

            if (index == 0)
            {
                // ---- pistol
                P(r, Cube, 0f, 0.03f, 0.12f, 0, 0, 0, 0.055f, 0.06f, 0.30f, "GunMetal");     // slide
                P(r, Cube, 0f, -0.012f, 0.10f, 0, 0, 0, 0.05f, 0.03f, 0.26f, "GunGrip");      // frame
                P(r, Cyl, 0f, 0.03f, 0.29f, 90, 0, 0, 0.035f, 0.03f, 0.035f, "GunMetal");     // barrel tip
                P(r, Cube, 0f, -0.085f, 0.0f, -12, 0, 0, 0.05f, 0.14f, 0.07f, "GunGrip");     // grip
                P(r, Cube, 0f, 0.063f, 0.12f, 0, 0, 0, 0.012f, 0.006f, 0.22f, "NeonCyan");    // top glow strip
                P(r, Cube, 0.029f, 0.03f, 0.12f, 0, 0, 0, 0.004f, 0.012f, 0.18f, "NeonCyan"); // side glow strip
                P(r, Cube, -0.029f, 0.03f, 0.12f, 0, 0, 0, 0.004f, 0.012f, 0.18f, "NeonCyan");
                return MuzzlePoint(r, 0f, 0.03f, 0.33f);
            }
            if (index == 1)
            {
                // ---- rifle
                P(r, Cube, 0f, 0f, 0.25f, 0, 0, 0, 0.07f, 0.10f, 0.55f, "GunMetal");           // receiver
                P(r, Cube, 0f, 0.065f, 0.30f, 0, 0, 0, 0.04f, 0.03f, 0.50f, "GunGrip");        // top rail
                P(r, Cyl, 0f, 0.01f, 0.70f, 90, 0, 0, 0.035f, 0.18f, 0.035f, "GunMetal");      // barrel
                P(r, Cyl, 0f, 0.01f, 0.55f, 90, 0, 0, 0.055f, 0.12f, 0.055f, "GunGrip");       // shroud
                P(r, Cube, 0f, -0.14f, 0.20f, 8, 0, 0, 0.045f, 0.20f, 0.08f, "GunGrip");       // magazine
                P(r, Cube, 0f, -0.11f, 0.02f, -15, 0, 0, 0.05f, 0.14f, 0.06f, "GunGrip");      // grip
                P(r, Cube, 0f, -0.02f, -0.20f, 0, 0, 0, 0.06f, 0.11f, 0.25f, "GunMetal");      // stock
                P(r, Cube, 0f, 0.10f, 0.25f, 0, 0, 0, 0.03f, 0.045f, 0.12f, "GunGrip");        // sight body
                P(r, Cube, 0f, 0.10f, 0.32f, 0, 0, 0, 0.022f, 0.028f, 0.004f, "NeonRed");      // sight glow
                P(r, Cube, 0.036f, 0.01f, 0.25f, 0, 0, 0, 0.004f, 0.03f, 0.40f, "NeonMagenta");
                P(r, Cube, -0.036f, 0.01f, 0.25f, 0, 0, 0, 0.004f, 0.03f, 0.40f, "NeonMagenta");
                return MuzzlePoint(r, 0f, 0.01f, 0.90f);
            }
            // ---- shotgun
            P(r, Cyl, 0f, 0.025f, 0.50f, 90, 0, 0, 0.045f, 0.40f, 0.045f, "GunMetal");         // upper barrel
            P(r, Cyl, 0f, -0.02f, 0.50f, 90, 0, 0, 0.045f, 0.40f, 0.045f, "GunMetal");         // lower barrel
            P(r, Cube, 0f, -0.055f, 0.40f, 0, 0, 0, 0.08f, 0.06f, 0.20f, "GunGrip");           // pump
            P(r, Cube, 0f, 0f, 0.05f, 0, 0, 0, 0.07f, 0.11f, 0.30f, "GunMetal");               // body
            P(r, Cube, 0f, -0.03f, -0.22f, -8, 0, 0, 0.06f, 0.12f, 0.30f, "GunGrip");          // stock
            P(r, Cube, 0f, -0.10f, 0.0f, -15, 0, 0, 0.05f, 0.14f, 0.06f, "GunGrip");           // grip
            P(r, Cube, 0f, 0.0f, 0.82f, 0, 0, 0, 0.075f, 0.13f, 0.02f, "NeonOrange");          // muzzle glow ring
            P(r, Cube, 0.04f, 0.0f, 0.10f, 0, 0, 0, 0.004f, 0.05f, 0.18f, "NeonOrange");
            P(r, Cube, -0.04f, 0.0f, 0.10f, 0, 0, 0, 0.004f, 0.05f, 0.18f, "NeonOrange");
            return MuzzlePoint(r, 0f, 0.0f, 0.92f);
        }
    }
}
