using UnityEngine;

namespace NeonBreach
{
    /// <summary>Pop-up practice target used by the tutorial. Any hit (bullet, melee, grenade) knocks it down.</summary>
    public class TrainingTarget : MonoBehaviour, IDamageable
    {
        public bool IsDead { get; private set; }
        Transform pivot;
        float fall;

        public static TrainingTarget Spawn(Vector3 groundPos, Vector3 faceTowards)
        {
            GameObject go = new GameObject("TrainingTarget");
            go.transform.position = groundPos;
            Vector3 look = groundPos - faceTowards;   // +Z points away from the player; the face is on -Z
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            GameObject piv = new GameObject("Pivot");
            piv.transform.SetParent(go.transform, false);
            Util.Part(piv.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(0.08f, 0.6f, 0.08f), "GunMetal");
            Util.Part(piv.transform, PrimitiveType.Cylinder, "Disc", new Vector3(0f, 1.45f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.8f, 0.03f, 0.8f), "NeonOrange");
            Util.Part(piv.transform, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 1.45f, -0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, 0.03f, 0.5f), "NeonWhite");
            Util.Part(piv.transform, PrimitiveType.Cylinder, "Bull", new Vector3(0f, 1.45f, -0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.2f, 0.03f, 0.2f), "NeonRed");
            Util.NoShadows(go);

            // hit box on the Enemy layer so weapons, crosshair and grenades treat it like a target
            GameObject hb = new GameObject("Hitbox");
            hb.layer = Layers.Enemy;
            hb.transform.SetParent(piv.transform, false);
            hb.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            BoxCollider bc = hb.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.85f, 1.6f, 0.3f);

            Util.AddLight(go.transform, LightType.Point, new Color(1f, 0.55f, 0.15f), 1.2f, 3f, new Vector3(0f, 1.5f, -0.4f));
            FX.Burst(groundPos + Vector3.up, new Color(1f, 0.6f, 0.2f), 14, 3f, 0.1f, 0.5f);

            TrainingTarget t = go.AddComponent<TrainingTarget>();
            t.pivot = piv.transform;
            return t;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, bool headshot, GameObject source)
        {
            if (IsDead) return;
            IsDead = true;
            Collider[] cs = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cs.Length; i++) cs[i].enabled = false;
            FX.Burst(hitPoint, new Color(1f, 0.7f, 0.2f), 18, 4f, 0.1f, 0.5f);
            Sfx.Play3D(SfxId.Headshot, hitPoint, 0.8f, 0.8f);
            GameManager gm = GameManager.Instance;
            PlayerController pc = GameManager.PlayerFromSource(source);
            if (gm != null && gm.Hud != null) gm.Hud.AddHitMarker(pc != null ? pc.index : 0, true, true);
            if (pc != null) Haptics.Tick(pc, UnityEngine.XR.XRNode.RightHand);
        }

        void Update()
        {
            if (!IsDead || pivot == null) return;
            fall = Mathf.MoveTowards(fall, 1f, Time.deltaTime / 0.35f);
            pivot.localRotation = Quaternion.Euler(88f * fall * fall, 0f, 0f);   // tip over backwards
        }
    }
}
