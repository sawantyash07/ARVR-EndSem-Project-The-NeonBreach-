using UnityEngine;

namespace NeonBreach
{
    [System.Serializable]
    public class WeaponStats
    {
        public string name;
        public string tagline;
        public float damage;          // per pellet
        public float fireRate;        // shots per second
        public bool automatic;
        public int magSize;
        public int startReserve;
        public int maxReserve;
        public float reloadTime;      // seconds (per shell when shellReload)
        public bool shellReload;
        public float hipSpread;       // degrees
        public float adsSpread;
        public int pellets;
        public float recoil;          // degrees of camera kick
        public float range;
        public float falloff;         // damage multiplier at max range
        public float adsFov;
        public Vector3 adsOffset;     // viewmodel position while aiming down sights
        public Color tracer;
        public SfxId sound;
    }

    public static class WeaponLibrary
    {
        public static WeaponStats[] Create()
        {
            WeaponStats pistol = new WeaponStats();
            pistol.name = "VX-9 PULSE PISTOL";
            pistol.tagline = "Accurate sidearm";
            pistol.damage = 26f; pistol.fireRate = 5.5f; pistol.automatic = false;
            pistol.magSize = 12; pistol.startReserve = 60; pistol.maxReserve = 120;
            pistol.reloadTime = 1.15f; pistol.shellReload = false;
            pistol.hipSpread = 1.2f; pistol.adsSpread = 0.15f; pistol.pellets = 1;
            pistol.recoil = 2.0f; pistol.range = 140f; pistol.falloff = 0.6f; pistol.adsFov = 62f;
            pistol.adsOffset = new Vector3(0f, -0.073f, 0.36f);
            pistol.tracer = new Color(0.2f, 0.95f, 1f, 1f);
            pistol.sound = SfxId.Pistol;

            WeaponStats rifle = new WeaponStats();
            rifle.name = "AR-77 PLASMA RIFLE";
            rifle.tagline = "Full-auto workhorse";
            rifle.damage = 16f; rifle.fireRate = 11f; rifle.automatic = true;
            rifle.magSize = 30; rifle.startReserve = 120; rifle.maxReserve = 240;
            rifle.reloadTime = 1.9f; rifle.shellReload = false;
            rifle.hipSpread = 2.6f; rifle.adsSpread = 0.45f; rifle.pellets = 1;
            rifle.recoil = 1.15f; rifle.range = 160f; rifle.falloff = 0.55f; rifle.adsFov = 52f;
            rifle.adsOffset = new Vector3(0f, -0.098f, 0.30f);
            rifle.tracer = new Color(1f, 0.25f, 0.75f, 1f);
            rifle.sound = SfxId.Rifle;

            WeaponStats shotgun = new WeaponStats();
            shotgun.name = "SG-12 SCATTER CANNON";
            shotgun.tagline = "Devastating up close";
            shotgun.damage = 11f; shotgun.fireRate = 1.3f; shotgun.automatic = false;
            shotgun.magSize = 6; shotgun.startReserve = 24; shotgun.maxReserve = 48;
            shotgun.reloadTime = 0.45f; shotgun.shellReload = true;
            shotgun.hipSpread = 5.2f; shotgun.adsSpread = 3.4f; shotgun.pellets = 9;
            shotgun.recoil = 7f; shotgun.range = 45f; shotgun.falloff = 0.25f; shotgun.adsFov = 66f;
            shotgun.adsOffset = new Vector3(0f, -0.062f, 0.34f);
            shotgun.tracer = new Color(1f, 0.6f, 0.1f, 1f);
            shotgun.sound = SfxId.Shotgun;

            return new WeaponStats[] { pistol, rifle, shotgun };
        }
    }
}
