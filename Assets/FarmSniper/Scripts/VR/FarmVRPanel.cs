using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// VR user interface for Farm Sniper: a world-space menu panel that appears in front of the headset for the title,
    /// briefing, pause, victory and game-over screens (stick up/down to choose, trigger or A to confirm), a wrist
    /// read-out on the left controller during play (rats, time, ammo, score, crops) and a floating kill-feed line.
    /// Only active while a headset is in use - the desktop uses FarmHUD.
    /// </summary>
    public class FarmVRPanel : MonoBehaviour
    {
        public FarmGameManager game;
        public float panelDistance = 2.2f;
        public float panelWidth = 1.7f;
        public Color panelColor = new Color(0.06f, 0.045f, 0.03f, 0.88f);
        public Color textColor = new Color(1f, 0.95f, 0.86f, 1f);
        public Color highlight = new Color(1f, 0.8f, 0.32f, 1f);

        Transform root;
        TextMesh body, menu, wrist, feed;
        Renderer bg;
        Material bgMat;
        FarmState shownState = (FarmState)(-1);
        int selected;
        readonly List<string> options = new List<string>();
        float wristTimer;

        FarmPlayer Player { get { return game != null ? game.player : null; } }

        void Start()
        {
            if (game == null) game = GetComponent<FarmGameManager>();
            Build();
        }

        void OnDestroy() { if (bgMat != null) Destroy(bgMat); }

        TextMesh MakeText(Transform parent, string name, int size, float charSize, TextAnchor anchor)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            TextMesh tm = g.AddComponent<TextMesh>();
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = f;
            tm.fontSize = size;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Left;
            tm.richText = true;
            tm.color = textColor;
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (f != null) mr.sharedMaterial = f.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        void Build()
        {
            root = new GameObject("VR Menu Panel").transform;
            root.SetParent(transform, false);
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Background";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            q.transform.localScale = new Vector3(panelWidth, panelWidth * 0.62f, 1f);
            bg = q.GetComponent<Renderer>();
            bgMat = Mats.FxColor(panelColor);
            bg.sharedMaterial = bgMat;
            bg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            body = MakeText(root, "Body", 64, 0.0105f, TextAnchor.UpperLeft);
            body.transform.localPosition = new Vector3(-panelWidth * 0.46f, panelWidth * 0.28f, 0f);
            menu = MakeText(root, "Menu", 64, 0.013f, TextAnchor.UpperLeft);
            menu.transform.localPosition = new Vector3(panelWidth * 0.12f, -panelWidth * 0.02f, 0f);
            root.gameObject.SetActive(false);

            feed = MakeText(transform, "VR Feed", 64, 0.012f, TextAnchor.MiddleCenter);
            feed.alignment = TextAlignment.Center;
            feed.gameObject.SetActive(false);

            if (Player != null && Player.vr != null && Player.vr.leftHand != null)
            {
                wrist = MakeText(Player.vr.leftHand, "Wrist HUD", 64, 0.0032f, TextAnchor.MiddleCenter);
                wrist.transform.localPosition = new Vector3(0f, 0.05f, -0.02f);
                wrist.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                wrist.alignment = TextAlignment.Center;
            }
        }

        void Update()
        {
            if (game == null || root == null) return;
            FarmVRRig vr = Player != null ? Player.vr : null;
            bool on = vr != null && vr.Active;
            FarmState st = game.State;
            bool menuState = st != FarmState.Playing;
            if (!on)
            {
                if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
                if (feed.gameObject.activeSelf) feed.gameObject.SetActive(false);
                if (wrist != null && wrist.gameObject.activeSelf) wrist.gameObject.SetActive(false);
                shownState = (FarmState)(-1);
                return;
            }

            if (menuState)
            {
                if (st != shownState) { Open(st); }
                if (vr.MenuStep != 0 && options.Count > 0)
                {
                    selected = (selected + vr.MenuStep + options.Count) % options.Count;
                    FarmAudio.PlayUI(FarmSound.UiClick);
                    Refresh();
                }
                if (vr.ConfirmPressed && options.Count > 0) Choose(options[selected]);
                else if (st == FarmState.Title || st == FarmState.Victory || st == FarmState.GameOver) Refresh();
            }
            else if (root.gameObject.activeSelf) { root.gameObject.SetActive(false); shownState = st; }

            // wrist + feed during play
            if (wrist != null)
            {
                wrist.gameObject.SetActive(st == FarmState.Playing);
                wristTimer -= Time.unscaledDeltaTime;
                if (st == FarmState.Playing && wristTimer <= 0f)
                {
                    wristTimer = 0.15f;
                    SniperRifle r = Player.rifle;
                    int s = Mathf.CeilToInt(game.TimeLeft);
                    wrist.text = "RATS " + game.Kills + "/" + game.Target + "   " + (s / 60) + ":" + (s % 60).ToString("00") +
                                 "\nAMMO " + (r != null ? r.Mag + "/" + r.Reserve : "-") + (r != null && r.Reloading ? " RELOAD" : "") +
                                 "  ZOOM " + (r != null ? r.Zoom + "x" : "") +
                                 "\nSCORE " + game.Score + "  CROPS " + Mathf.RoundToInt(game.CropHealth * 100f) + "%";
                }
            }
            if (st == FarmState.Playing && game.Feed.Count > 0 && Player.cam != null)
            {
                FeedEntry e = game.Feed[game.Feed.Count - 1];
                float age = Time.unscaledTime - e.time;
                bool show = age < 2.2f;
                feed.gameObject.SetActive(show);
                if (show)
                {
                    feed.text = e.text;
                    Color c = e.color;
                    c.a = Mathf.Clamp01(2.2f - age);
                    feed.color = c;
                    Transform cam = Player.cam.transform;
                    Vector3 fwd = vr.HeadForwardFlat(Player.cam);
                    feed.transform.position = cam.position + fwd * 3f + Vector3.up * 0.55f;
                    feed.transform.rotation = Quaternion.LookRotation(feed.transform.position - cam.position, Vector3.up);
                }
            }
            else if (feed.gameObject.activeSelf) feed.gameObject.SetActive(false);
        }

        void Open(FarmState st)
        {
            shownState = st;
            selected = 0;
            root.gameObject.SetActive(true);
            if (Player != null && Player.cam != null)
            {
                Transform cam = Player.cam.transform;
                Vector3 fwd = Player.vr.HeadForwardFlat(Player.cam);
                root.position = cam.position + fwd * panelDistance;
                root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            Refresh();
        }

        void Refresh()
        {
            options.Clear();
            string t;
            DifficultyProfile p = game.Profile;
            switch (game.State)
            {
                case FarmState.Title:
                    t = "<size=110><color=#FFCC50><b>FARM SNIPER</b></color></size>\nPest control at Willow Creek Farm\n\n" +
                        "Difficulty: <b>" + p.displayName + "</b>\n" + Wrap(p.description, 34) + "\n\nBest: " + FarmGameManager.Best(game.difficulty) +
                        "\n\n<size=48>Stick: choose · Trigger / A: select</size>";
                    options.Add("START MISSION");
                    options.Add("DIFFICULTY: " + p.displayName);
                    if (game.CanLoadMainGame) options.Add("BACK TO NEON BREACH");
                    options.Add("QUIT");
                    break;
                case FarmState.Briefing:
                    t = "<size=80><color=#FFCC50><b>MISSION BRIEFING</b></color></size>\n\n" +
                        "Eliminate <b>" + p.ratsToEliminate + " rats</b> in " + Mathf.RoundToInt(p.timeLimit / 60f) + " min.\nKeep crops above " + Mathf.RoundToInt(game.cropFailThreshold * 100f) + "%.\n\n" +
                        "<size=50>R-trigger fire · R-grip reload\nB / L-trigger scope zoom\nL-stick move · R-stick turn\nX / Y ammo crate · Menu pause</size>";
                    options.Add("BEGIN");
                    options.Add("BACK");
                    break;
                case FarmState.Paused:
                    t = "<size=90><color=#FFCC50><b>PAUSED</b></color></size>\n\nRats " + game.Kills + " / " + game.Target + "\nScore " + game.Score;
                    options.Add("RESUME");
                    options.Add("RESTART");
                    options.Add("MAIN MENU");
                    options.Add("QUIT");
                    break;
                case FarmState.Victory:
                case FarmState.GameOver:
                    bool won = game.State == FarmState.Victory;
                    t = "<size=80><color=" + (won ? "#FFCC50" : "#FF5040") + "><b>" + (won ? "MISSION COMPLETE" : "MISSION FAILED") + "</b></color></size>\n\n" +
                        "Rats " + game.Kills + " / " + game.Target + "\nHeadshots " + game.Headshots + "\nAccuracy " + Mathf.RoundToInt(game.Accuracy * 100f) + "%\n" +
                        "Longest shot " + Mathf.RoundToInt(game.LongestShot) + " m\nCrops " + Mathf.RoundToInt(game.CropHealth * 100f) + "%\n\n<b>SCORE " + game.Score + "</b>" + (game.NewBest ? "  NEW BEST!" : "");
                    options.Add(won ? "PLAY AGAIN" : "RETRY");
                    options.Add("MAIN MENU");
                    break;
                default:
                    t = "";
                    break;
            }
            body.text = t;
            string m = "";
            for (int i = 0; i < options.Count; i++)
                m += (i == selected ? "<color=#FFCC50>> " + options[i] + "</color>" : "   " + options[i]) + "\n\n";
            menu.text = m;
        }

        static string Wrap(string s, int width)
        {
            string[] words = s.Split(' ');
            string outS = "", line = "";
            for (int i = 0; i < words.Length; i++)
            {
                if (line.Length + words[i].Length + 1 > width) { outS += line + "\n"; line = ""; }
                line += (line.Length > 0 ? " " : "") + words[i];
            }
            return outS + line;
        }

        void Choose(string o)
        {
            FarmAudio.PlayUI(FarmSound.UiSelect);
            if (o == "START MISSION") game.ShowBriefing();
            else if (o.StartsWith("DIFFICULTY")) { game.SetDifficulty((FarmDifficulty)(((int)game.difficulty + 1) % 3)); Refresh(); }
            else if (o == "BACK TO NEON BREACH") game.LoadMainGame();
            else if (o == "QUIT") game.Quit();
            else if (o == "BEGIN") game.BeginMission();
            else if (o == "BACK" || o == "MAIN MENU") game.GoTitle();
            else if (o == "RESUME") game.Resume();
            else if (o == "RESTART" || o == "PLAY AGAIN" || o == "RETRY") game.Restart();
        }
    }
}
