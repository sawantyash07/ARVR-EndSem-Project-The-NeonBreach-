using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Short interactive first-run tutorial. Each step waits for the player to actually do the thing
    /// (look, move, sprint + jump, shoot targets, reload, aim, grenade, collect a power-up), then the real mission starts.
    /// Instructions adapt to the device: keyboard (using the player's own key bindings), gamepad, VR controllers or
    /// hand tracking. No damage can be taken while the tutorial runs.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        enum Step { Look, Move, SprintJump, Shoot, Reload, Aim, Grenade, PowerUp, Done }

        public bool Running { get; private set; }
        public string Title { get; private set; }
        public string Instruction { get; private set; }
        public string Progress { get; private set; }
        public int StepIndex { get { return (int)step; } }
        public int StepCount { get { return (int)Step.Done; } }

        PlayerController p;
        Step step;
        float stepTime;
        float baseline;
        int baseCount;
        Vector3 lastHead;
        float headTravel, headYawTravel, lastYaw;
        readonly List<TrainingTarget> targets = new List<TrainingTarget>();
        bool powerUpTaken;
        float doneTimer;
        float grenadeWatch = -1f;

        bool Vr { get { return p != null && p.VrOn; } }
        bool Hands { get { return Vr && p.vr.UsingHands; } }
        bool Pad { get { return p != null && p.gamepadIndex >= 0 && !Vr; } }

        public void Begin(PlayerController player)
        {
            Stop();
            p = player;
            Running = true;
            Go(Step.Look);
        }

        public void Stop()
        {
            Running = false;
            ClearTargets();
            Pickup[] ps = FindObjectsByType<Pickup>();
            for (int i = 0; i < ps.Length; i++) Destroy(ps[i].gameObject);
        }

        public void OnPickup(PickupType t) { if (t == PickupType.DoubleDamage || t == PickupType.SlowMotion) powerUpTaken = true; }

        void ClearTargets()
        {
            for (int i = 0; i < targets.Count; i++) if (targets[i] != null) Destroy(targets[i].gameObject);
            targets.Clear();
        }

        static string K(GameAction a) { return KeyBindings.KeyFor(a); }

        void Go(Step s)
        {
            step = s;
            stepTime = 0f;
            ClearTargets();
            GameManager gm = GameManager.Instance;
            WeaponSystem w = p.weapons;
            switch (s)
            {
                case Step.Look:
                    Title = Loc.T("LOOK AROUND");
                    Instruction = Vr ? Loc.T("Turn your head and look around the arena.")
                                : Pad ? Loc.T("Use the right stick to look around.")
                                : Loc.T("Move the mouse to look around.");
                    baseline = p.LookAmount;
                    lastYaw = p.cam.transform.eulerAngles.y;
                    headYawTravel = 0f;
                    break;
                case Step.Move:
                    Title = Loc.T("MOVE");
                    Instruction = MixedReality.Active ? Loc.T("Walk around your room - in mixed reality you move for real.")
                                : Vr ? Loc.T("Push the left stick to walk (or physically step around).")
                                : Pad ? Loc.T("Use the left stick to walk.")
                                : string.Format(Loc.T("Walk with {0} {1} {2} {3}."), K(GameAction.MoveForward), K(GameAction.MoveLeft), K(GameAction.MoveBack), K(GameAction.MoveRight));
                    baseline = p.DistanceMoved;
                    lastHead = p.cam.transform.position;
                    headTravel = 0f;
                    break;
                case Step.SprintJump:
                    Title = Loc.T("SPRINT AND JUMP");
                    Instruction = Vr ? Loc.T("Click the left stick to sprint, press A to jump.")
                                : Pad ? Loc.T("Click the left stick to sprint, press A to jump.")
                                : string.Format(Loc.T("Hold {0} to sprint, press {1} to jump."), K(GameAction.Sprint), K(GameAction.Jump));
                    baseline = p.SprintTime;
                    baseCount = p.Jumps;
                    break;
                case Step.Shoot:
                    Title = Loc.T("SHOOT THE TARGETS");
                    Instruction = Hands ? Loc.T("Point your right hand and PINCH (thumb + index) to fire.")
                                : Vr ? Loc.T("Aim with your right hand and pull the TRIGGER to fire.")
                                : Pad ? Loc.T("Pull RT to fire.")
                                : string.Format(Loc.T("Aim at the targets and press {0} to fire."), K(GameAction.Fire));
                    SpawnTargets(3, MixedReality.Active ? 1.8f : 8f, MixedReality.Active ? 3f : 13f, 50f);
                    break;
                case Step.Reload:
                    Title = Loc.T("RELOAD");
                    Instruction = Hands ? Loc.T("Hold your right hand OPEN (palm flat) to reload.")
                                : Vr ? Loc.T("Squeeze the right GRIP to reload.")
                                : Pad ? Loc.T("Press X to reload.")
                                : string.Format(Loc.T("Press {0} to reload."), K(GameAction.Reload));
                    baseCount = w.ReloadsDone;
                    // make sure there is something to reload
                    w.AddAmmo(0.2f);
                    break;
                case Step.Aim:
                    Title = Loc.T("AIM DOWN SIGHTS");
                    Instruction = Pad ? Loc.T("Hold LT to aim, then hit the far target.")
                                : string.Format(Loc.T("Hold {0} to aim down sights, then hit the far target."), K(GameAction.Aim));
                    SpawnTargets(1, 20f, 26f, 25f);
                    break;
                case Step.Grenade:
                    Title = Loc.T("THROW A GRENADE");
                    Instruction = Hands ? Loc.T("PINCH with your left hand to throw a grenade at the targets.")
                                : Vr ? Loc.T("Pull the LEFT trigger to throw a grenade at the targets.")
                                : Pad ? Loc.T("Press RB to throw a grenade at the targets.")
                                : string.Format(Loc.T("Press {0} to throw a grenade at the targets."), K(GameAction.Grenade));
                    SpawnTargets(3, MixedReality.Active ? 2f : 9f, MixedReality.Active ? 3f : 11f, 12f);
                    w.AddGrenades(2);
                    baseCount = w.GrenadesThrown;
                    grenadeWatch = -1f;
                    break;
                case Step.PowerUp:
                    Title = Loc.T("POWER-UPS");
                    Instruction = Loc.T("Enemies sometimes drop power-ups: DOUBLE DAMAGE (red) and SLOW MOTION (blue). Walk into this one.");
                    powerUpTaken = false;
                    Vector3 fwd = p.VrOn ? p.vr.HeadForwardFlat : p.transform.forward;
                    Vector3 pos = p.transform.position + fwd * (MixedReality.Active ? 1.2f : 5f);
                    pos.y = p.transform.position.y + 0.8f;
                    Pickup.Spawn(PickupType.DoubleDamage, pos);
                    break;
                default:
                    Title = Loc.T("TRAINING COMPLETE");
                    Instruction = Loc.T("Between waves you can spend credits in the ARMORY. The mission starts now - good luck!");
                    doneTimer = 4f;
                    Sfx.Play2D(SfxId.WaveStart, 0.8f);
                    break;
            }
            Progress = "";
            if (gm.Hud != null) gm.Hud.Banner(Title, "");
            Sfx.Play2D(SfxId.Beep, 0.6f, 1.2f);
        }

        void SpawnTargets(int count, float minD, float maxD, float arc)
        {
            Vector3 head = p.cam.transform.position;
            Vector3 fwd = p.VrOn ? p.vr.HeadForwardFlat : p.transform.forward;
            int placed = 0;
            for (int tries = 0; tries < 60 && placed < count; tries++)
            {
                float a = Random.Range(-arc, arc);
                float d = Random.Range(minD, maxD);
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * fwd;
                Vector3 pos = p.transform.position + dir * d;
                RaycastHit ground;
                if (!Physics.Raycast(pos + Vector3.up * 6f, Vector3.down, out ground, 12f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                pos = ground.point;
                if (Physics.CheckSphere(pos + Vector3.up * 1.1f, 0.6f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(head, pos + Vector3.up * 1.45f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                bool crowded = false;
                for (int i = 0; i < targets.Count; i++) if (Vector3.Distance(targets[i].transform.position, pos) < 1.4f) crowded = true;
                if (crowded) continue;
                targets.Add(TrainingTarget.Spawn(pos, p.transform.position));
                placed++;
            }
            // fallback: straight ahead, ignoring cover
            for (; placed < count; placed++)
                targets.Add(TrainingTarget.Spawn(p.transform.position + fwd * (minD + placed * 1.5f) + p.transform.right * (placed - 1) * 1.5f, p.transform.position));
        }

        int TargetsDown()
        {
            int n = 0;
            for (int i = 0; i < targets.Count; i++) if (targets[i] == null || targets[i].IsDead) n++;
            return n;
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (!Running || p == null || gm == null || !gm.Playing || !gm.InTutorial) return;
            stepTime += Time.deltaTime;
            WeaponSystem w = p.weapons;

            switch (step)
            {
                case Step.Look:
                {
                    float yaw = p.cam.transform.eulerAngles.y;
                    headYawTravel += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                    lastYaw = yaw;
                    float amount = Vr ? headYawTravel / 120f : (p.LookAmount - baseline) / 60f;
                    Progress = Mathf.RoundToInt(Mathf.Clamp01(amount) * 100f) + "%";
                    if (amount >= 1f && stepTime > 1f) Go(Step.Move);
                    break;
                }
                case Step.Move:
                {
                    Vector3 h = p.cam.transform.position;
                    Vector3 d = h - lastHead;
                    d.y = 0f;
                    headTravel += d.magnitude;
                    lastHead = h;
                    float need = MixedReality.Active ? 1.5f : 6f;
                    float moved = Mathf.Max(p.DistanceMoved - baseline, Vr ? headTravel : 0f);
                    Progress = Mathf.RoundToInt(Mathf.Clamp01(moved / need) * 100f) + "%";
                    if (moved >= need) Go(MixedReality.Active ? Step.Shoot : Step.SprintJump);
                    break;
                }
                case Step.SprintJump:
                {
                    bool sprinted = p.SprintTime - baseline >= 0.8f;
                    bool jumped = p.Jumps > baseCount;
                    Progress = (sprinted ? "[x] " : "[ ] ") + Loc.T("Sprint") + "    " + (jumped ? "[x] " : "[ ] ") + Loc.T("Jump");
                    if (sprinted && jumped) Go(Step.Shoot);
                    break;
                }
                case Step.Shoot:
                    Progress = TargetsDown() + " / " + targets.Count;
                    if (TargetsDown() >= targets.Count && stepTime > 0.5f) Go(Step.Reload);
                    break;
                case Step.Reload:
                    Progress = "";
                    if (w.ReloadsDone > baseCount) Go(Vr ? Step.Grenade : Step.Aim);
                    break;
                case Step.Aim:
                    Progress = TargetsDown() + " / " + targets.Count + (w.IsAds ? "   " + Loc.T("AIMING") : "");
                    if (TargetsDown() >= targets.Count && stepTime > 0.5f) Go(Step.Grenade);
                    break;
                case Step.Grenade:
                    Progress = TargetsDown() + " / " + targets.Count;
                {
                    // done when every target is down, or 3 s after the first grenade was thrown
                    if (w.GrenadesThrown > baseCount && grenadeWatch < 0f) grenadeWatch = 3.2f;
                    bool timeout = false;
                    if (grenadeWatch > 0f)
                    {
                        grenadeWatch -= Time.deltaTime;
                        if (grenadeWatch <= 0f) timeout = true;
                    }
                    if (TargetsDown() >= targets.Count || timeout) Go(Step.PowerUp);
                    else if (w.Grenades == 0 && grenadeWatch < 0f) w.AddGrenades(1);
                    break;
                }
                case Step.PowerUp:
                    Progress = "";
                    if (powerUpTaken) Go(Step.Done);
                    break;
                default:
                    doneTimer -= Time.deltaTime;
                    Progress = Mathf.CeilToInt(Mathf.Max(0f, doneTimer)).ToString();
                    if (doneTimer <= 0f) gm.FinishTutorial(true);
                    break;
            }

            // in VR the instruction is repeated as a floating banner
            if (Vr && Mathf.Repeat(stepTime, 8f) < Time.deltaTime && gm.Player.vr != null)
                gm.Player.vr.ShowBanner(Title + "\n" + Instruction);
        }
    }
}
