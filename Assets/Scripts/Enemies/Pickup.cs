using UnityEngine;

namespace NeonBreach
{
    public enum PickupType { Medkit, Ammo, Grenade, DoubleDamage, SlowMotion }

    /// <summary>Floating collectible dropped by enemies. Collected by walking close to it (either player in co-op).</summary>
    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        float born;
        Vector3 basePos;
        Transform visual;
        const float Lifetime = 28f;

        public bool IsPowerUp { get { return type == PickupType.DoubleDamage || type == PickupType.SlowMotion; } }

        public static Pickup Spawn(PickupType t, Vector3 pos)
        {
            GameObject go = new GameObject("Pickup_" + t);
            go.transform.position = pos;
            Transform v = new GameObject("Visual").transform;
            v.SetParent(go.transform, false);

            Color lightColor;
            switch (t)
            {
                case PickupType.Medkit:
                    Util.Part(v, PrimitiveType.Cube, "Case", Vector3.zero, Vector3.zero, new Vector3(0.5f, 0.38f, 0.5f), "NeonWhite");
                    Util.Part(v, PrimitiveType.Cube, "CrossA", new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(0.34f, 0.04f, 0.1f), "NeonGreen");
                    Util.Part(v, PrimitiveType.Cube, "CrossB", new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(0.1f, 0.04f, 0.34f), "NeonGreen");
                    lightColor = new Color(0.3f, 1f, 0.4f);
                    break;
                case PickupType.Ammo:
                    Util.Part(v, PrimitiveType.Cube, "Box", Vector3.zero, Vector3.zero, new Vector3(0.5f, 0.34f, 0.36f), "NeonYellow");
                    Util.Part(v, PrimitiveType.Cube, "Band", Vector3.zero, Vector3.zero, new Vector3(0.18f, 0.37f, 0.39f), "GunGrip");
                    lightColor = new Color(1f, 0.9f, 0.2f);
                    break;
                case PickupType.DoubleDamage:
                    // red crystal with two rings: "x2"
                    Util.Part(v, PrimitiveType.Cube, "Crystal", Vector3.zero, new Vector3(45f, 0f, 45f), Vector3.one * 0.36f, "NeonRed");
                    Util.Part(v, PrimitiveType.Cylinder, "RingA", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.015f, 0.7f), "NeonOrange");
                    Util.Part(v, PrimitiveType.Cylinder, "RingB", Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.015f, 0.7f), "NeonOrange");
                    lightColor = new Color(1f, 0.25f, 0.1f);
                    break;
                case PickupType.SlowMotion:
                    // blue hourglass
                    Util.Part(v, PrimitiveType.Cylinder, "Top", new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(0.38f, 0.02f, 0.38f), "NeonCyan");
                    Util.Part(v, PrimitiveType.Cylinder, "Bottom", new Vector3(0f, -0.2f, 0f), Vector3.zero, new Vector3(0.38f, 0.02f, 0.38f), "NeonCyan");
                    Util.Part(v, PrimitiveType.Sphere, "BulbA", new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.28f, 0.2f, 0.28f), "NeonWhite");
                    Util.Part(v, PrimitiveType.Sphere, "BulbB", new Vector3(0f, -0.1f, 0f), Vector3.zero, new Vector3(0.28f, 0.2f, 0.28f), "NeonWhite");
                    lightColor = new Color(0.3f, 0.7f, 1f);
                    break;
                default:
                    Util.Part(v, PrimitiveType.Sphere, "Grenade", Vector3.zero, Vector3.zero, Vector3.one * 0.38f, "NeonGreen");
                    Util.Part(v, PrimitiveType.Cylinder, "Ring", Vector3.zero, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), "NeonWhite");
                    lightColor = new Color(0.4f, 1f, 0.3f);
                    break;
            }
            Util.NoShadows(go);
            bool power = t == PickupType.DoubleDamage || t == PickupType.SlowMotion;
            Util.AddLight(go.transform, LightType.Point, lightColor, power ? 2.4f : 1.3f, power ? 5f : 3.5f, Vector3.zero);

            Pickup p = go.AddComponent<Pickup>();
            p.type = t;
            p.visual = v;
            p.basePos = pos;
            p.born = Time.time;
            return p;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.Playing) { born += Time.deltaTime; return; }

            float age = Time.time - born;
            if (age > Lifetime && !gm.InTutorial) { Destroy(gameObject); return; }

            transform.position = basePos + Vector3.up * (Mathf.Sin(age * 3f) * 0.12f);
            visual.Rotate(0f, (IsPowerUp ? 200f : 110f) * Time.deltaTime, 0f, Space.World);
            if (age > Lifetime - 5f && !gm.InTutorial) visual.gameObject.SetActive(Mathf.Sin(age * 18f) > 0f);

            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController pl = gm.Players[i];
                if (pl == null || pl.IsDead) continue;
                Vector3 d = pl.BodyCenter - transform.position;
                if (d.sqrMagnitude < 2.6f * 2.6f) { Collect(pl, gm); return; }
            }
        }

        void Collect(PlayerController pl, GameManager gm)
        {
            string msg = "";
            switch (type)
            {
                case PickupType.Medkit:
                    pl.AddMedkit();
                    pl.Heal(20f);
                    msg = "+ " + Loc.T("MEDKIT");
                    break;
                case PickupType.Ammo:
                    pl.weapons.AddAmmo(0.5f);
                    msg = "+ " + Loc.T("AMMO");
                    break;
                case PickupType.DoubleDamage:
                    PowerUps.GiveDoubleDamage();
                    msg = Loc.T("DOUBLE DAMAGE!");
                    if (gm.Hud != null) gm.Hud.Banner(Loc.T("DOUBLE DAMAGE"), Loc.T("All weapons deal x2 damage for 12 seconds"));
                    Sfx.Play2D(SfxId.PowerUp, 0.9f, 1.2f);
                    break;
                case PickupType.SlowMotion:
                    PowerUps.GiveSlowMo();
                    msg = Loc.T("SLOW MOTION!");
                    if (gm.Hud != null) gm.Hud.Banner(Loc.T("SLOW MOTION"), Loc.T("Enemies move at 35% speed for 8 seconds"));
                    Sfx.Play2D(SfxId.PowerUp, 0.9f, 0.7f);
                    break;
                default:
                    pl.weapons.AddGrenades(2);
                    msg = "+ 2 " + Loc.T("GRENADES");
                    break;
            }
            gm.OnPickup(pl, type);
            Sfx.Play2D(SfxId.Pickup, 0.8f);
            FX.Burst(transform.position, new Color(0.5f, 1f, 0.7f), 14, 3f, 0.1f, 0.5f);
            if (gm.Hud != null) gm.Hud.Toast(msg);
            Destroy(gameObject);
        }
    }
}
