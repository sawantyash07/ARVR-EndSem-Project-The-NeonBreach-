using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Decides what the VR menu panel shows for the current game state: main menu (difficulty, arena, mixed reality,
    /// comfort options, tutorial, leaderboard), pause, shop and game over. Only active while a headset is in use.
    /// </summary>
    public class VRMenuDriver : MonoBehaviour
    {
        string sub = "";   // sub-page inside the main/pause menu: "", "comfort", "board", "modes"

        static string OnOff(bool b) { return b ? Loc.T("ON") : Loc.T("OFF"); }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.Player.vr == null) return;
            VRRig rig = gm.Player.vr;
            VRMenu menu = rig.Menu;
            if (menu == null) return;
            if (!rig.Active || gm.State == GameState.Playing)
            {
                menu.Hide();
                if (gm.State == GameState.Playing) sub = "";
                return;
            }

            List<VRMenu.Item> items = new List<VRMenu.Item>();
            switch (gm.State)
            {
                case GameState.Menu:
                    if (sub == "comfort") { Comfort(menu, items, "menu"); return; }
                    if (sub == "board") { Board(menu, items); return; }
                    if (sub == "modes") { Modes(menu, items, "menu"); return; }
                    items.Add(new VRMenu.Item(Loc.T("SHOOTER: START MISSION"), () => gm.StartFromMenu()));
                    items.Add(new VRMenu.Item(Loc.T("ARCHERY") + "  (" + Loc.T("best") + " " + ModeManager.Best(GameMode.Archery) + ")", () => gm.Modes.SwitchTo(GameMode.Archery)));
                    items.Add(new VRMenu.Item(Loc.T("PARACHUTE") + "  (" + Loc.T("best") + " " + ModeManager.Best(GameMode.Parachute) + ")", () => gm.Modes.SwitchTo(GameMode.Parachute)));
                    items.Add(new VRMenu.Item(Loc.T("DIFFICULTY") + ": " + DifficultyInfo.Name(GameSettings.Difficulty),
                        () => { GameSettings.Difficulty = (Difficulty)(((int)GameSettings.Difficulty + 1) % 3); GameSettings.Save(); }));
                    items.Add(new VRMenu.Item(Loc.T("ARENA") + ": " + ArenaCatalog.Name(GameSettings.Arena),
                        () => { GameSettings.Arena = (GameSettings.Arena + 1) % ArenaCatalog.Count; GameSettings.Save(); ArenaManager.Prepare(GameSettings.Arena, false); }));
                    items.Add(new VRMenu.Item(Loc.T("MIXED REALITY") + ": " + OnOff(GameSettings.MixedReality),
                        () => { GameSettings.MixedReality = !GameSettings.MixedReality; GameSettings.Save(); }));
                    items.Add(new VRMenu.Item(Loc.T("CO-OP (P2 ON GAMEPAD)") + ": " + OnOff(GameSettings.CoOp),
                        () => { GameSettings.CoOp = !GameSettings.CoOp; GameSettings.Save(); }));
                    items.Add(new VRMenu.Item(Loc.T("COMFORT & CONTROLS"), () => sub = "comfort"));
                    items.Add(new VRMenu.Item(Loc.T("TUTORIAL"), () => gm.StartTutorial()));
                    items.Add(new VRMenu.Item(Loc.T("LEADERBOARD"), () => sub = "board"));
                    items.Add(new VRMenu.Item(Loc.T("QUIT"), () => gm.QuitGame()));
                    menu.Show("main", "NEON BREACH", Loc.T("High score") + "  " + gm.HighScore.ToString("N0") +
                              (GameSettings.MixedReality ? "\n" + Loc.T("Mixed reality: enemies breach into your real room") : ""), items);
                    break;

                case GameState.Paused:
                    if (sub == "comfort") { Comfort(menu, items, "pause"); return; }
                    if (sub == "modes") { Modes(menu, items, "pause"); return; }
                    items.Add(new VRMenu.Item(Loc.T("RESUME"), () => gm.Resume()));
                    items.Add(new VRMenu.Item(Loc.T("COMFORT & CONTROLS"), () => sub = "comfort"));
                    items.Add(new VRMenu.Item(Loc.T("RECENTER VIEW"), () => rig.Recenter()));
                    if (gm.InTutorial) items.Add(new VRMenu.Item(Loc.T("SKIP TUTORIAL"), () => { gm.Resume(); gm.FinishTutorial(true); }));
                    else if (!gm.Modes.IsShooter) items.Add(new VRMenu.Item(Loc.T("RESTART"), () => { gm.Resume(); gm.Modes.Restart(); }));
                    else items.Add(new VRMenu.Item(Loc.T("RESTART"), () => { gm.Resume(); gm.StartGame(); }));
                    items.Add(new VRMenu.Item(Loc.T("SWITCH MODE"), () => sub = "modes"));
                    items.Add(new VRMenu.Item(Loc.T("MAIN MENU"), () => { Time.timeScale = 1f; gm.EnterMenu(); }));
                    items.Add(new VRMenu.Item(Loc.T("QUIT"), () => gm.QuitGame()));
                    menu.Show("pause", Loc.T("PAUSED"), gm.Modes.Active != null ? gm.Modes.Active.StatusLine : "", items);
                    break;

                case GameState.Shop:
                    for (int i = 0; i < Upgrades.All.Length; i++)
                    {
                        UpgradeId u = Upgrades.All[i];
                        string lvl = Upgrades.Level(u) + "/" + Upgrades.MaxLevel(u);
                        string price = Upgrades.Maxed(u) ? Loc.T("MAX") : "$" + Upgrades.Cost(u);
                        items.Add(new VRMenu.Item(Upgrades.Name(u).ToUpperInvariant() + "  " + lvl + "   " + price, () => gm.TryBuyUpgrade(u)));
                    }
                    ShopItem[] shopItems = { ShopItem.AmmoRefill, ShopItem.Medkit, ShopItem.Grenades };
                    for (int i = 0; i < shopItems.Length; i++)
                    {
                        ShopItem it = shopItems[i];
                        items.Add(new VRMenu.Item(Upgrades.ItemName(it).ToUpperInvariant() + "   $" + Upgrades.ItemCost(it), () => gm.TryBuyItem(it)));
                    }
                    items.Add(new VRMenu.Item(Loc.T("CONTINUE") + "  >>", () => gm.CloseShop()));
                    menu.Show("shop", Loc.T("ARMORY"), Loc.T("Credits") + ": $" + gm.Credits + "    " + Loc.T("Wave") + " " + gm.Waves.Wave + " " + Loc.T("cleared"), items);
                    break;

                case GameState.GameOver:
                    if (sub == "modes") { Modes(menu, items, "over"); return; }
                    if (!gm.Modes.IsShooter && gm.Modes.Active != null)
                    {
                        MiniGame g = gm.Modes.Active;
                        List<string> rows = new List<string>();
                        g.ResultRows(rows);
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        sb.Append(Loc.T("Score")).Append(' ').Append(g.Score.ToString("N0"));
                        if (g.NewBest) sb.Append("   ").Append(Loc.T("NEW HIGH SCORE!"));
                        for (int i = 0; i + 1 < rows.Count; i += 2) sb.Append('\n').Append(rows[i]).Append(":  ").Append(rows[i + 1]);
                        items.Add(new VRMenu.Item(Loc.T("PLAY AGAIN"), () => gm.Modes.Restart()));
                        items.Add(new VRMenu.Item(Loc.T("SWITCH MODE"), () => sub = "modes"));
                        items.Add(new VRMenu.Item(Loc.T("MAIN MENU"), () => gm.EnterMenu()));
                        menu.Show("over-" + g.Kind, g.ResultTitle, sb.ToString(), items);
                        break;
                    }
                    items.Add(new VRMenu.Item(Loc.T("RETRY"), () => gm.StartGame()));
                    items.Add(new VRMenu.Item(Loc.T("MAIN MENU"), () => gm.EnterMenu()));
                    string rank = gm.LastRank > 0 ? "\n" + Loc.T("Leaderboard rank") + " #" + gm.LastRank : "";
                    menu.Show("over", Loc.T("MISSION FAILED"),
                        Loc.T("Score") + " " + gm.Score.ToString("N0") + "   " + Loc.T("Wave") + " " + Mathf.Max(1, gm.BestWave) +
                        "\n" + Loc.T("Kills") + " " + gm.Kills + "   " + Loc.T("Headshots") + " " + gm.Headshots +
                        "\n" + Loc.T("Accuracy") + " " + Mathf.RoundToInt(gm.Accuracy * 100f) + "%" + rank, items);
                    break;
            }
        }

        void Comfort(VRMenu menu, List<VRMenu.Item> items, string back)
        {
            VRRig rig = GameManager.Instance.Player.vr;
            items.Add(new VRMenu.Item(Loc.T("TURNING") + ": " + (GameSettings.Turn == TurnMode.Snap ? Loc.T("SNAP") : Loc.T("SMOOTH")),
                () => { GameSettings.Turn = GameSettings.Turn == TurnMode.Snap ? TurnMode.Smooth : TurnMode.Snap; GameSettings.Save(); }));
            if (GameSettings.Turn == TurnMode.Snap)
                items.Add(new VRMenu.Item(Loc.T("SNAP ANGLE") + ": " + Mathf.RoundToInt(GameSettings.SnapAngle) + "°",
                    () => { GameSettings.SnapAngle = GameSettings.SnapAngle >= 90f ? 30f : (GameSettings.SnapAngle >= 45f ? 90f : 45f); GameSettings.Save(); }));
            else
                items.Add(new VRMenu.Item(Loc.T("TURN SPEED") + ": " + Mathf.RoundToInt(GameSettings.SmoothTurnSpeed) + "°/s",
                    () => { GameSettings.SmoothTurnSpeed = GameSettings.SmoothTurnSpeed >= 180f ? 60f : GameSettings.SmoothTurnSpeed + 30f; GameSettings.Save(); }));
            items.Add(new VRMenu.Item(Loc.T("COMFORT VIGNETTE") + ": " + OnOff(GameSettings.ComfortVignette),
                () => { GameSettings.ComfortVignette = !GameSettings.ComfortVignette; GameSettings.Save(); }));
            items.Add(new VRMenu.Item(Loc.T("VIGNETTE STRENGTH") + ": " + Mathf.RoundToInt(GameSettings.VignetteStrength * 100f) + "%",
                () => { GameSettings.VignetteStrength = GameSettings.VignetteStrength >= 0.95f ? 0.4f : GameSettings.VignetteStrength + 0.15f; GameSettings.Save(); }));
            items.Add(new VRMenu.Item(Loc.T("SEATED MODE") + ": " + OnOff(GameSettings.Seated),
                () => { GameSettings.Seated = !GameSettings.Seated; GameSettings.Save(); rig.Recenter(); }));
            items.Add(new VRMenu.Item(Loc.T("RECENTER VIEW"), () => rig.Recenter()));
            items.Add(new VRMenu.Item(Loc.T("HAND TRACKING") + ": " + OnOff(GameSettings.HandTracking),
                () => { GameSettings.HandTracking = !GameSettings.HandTracking; GameSettings.Save(); }));
            items.Add(new VRMenu.Item(Loc.T("HAPTICS") + ": " + OnOff(GameSettings.Haptics),
                () => { GameSettings.Haptics = !GameSettings.Haptics; GameSettings.Save(); }));
            items.Add(new VRMenu.Item(Loc.T("SPATIAL AUDIO (HRTF)") + ": " + OnOff(GameSettings.Hrtf),
                () => { GameSettings.Hrtf = !GameSettings.Hrtf; GameSettings.Save(); Sfx.RefreshSpatial(); }));
            items.Add(new VRMenu.Item(Loc.T("BACK"), () => sub = ""));
            string hands = rig.Hands != null && rig.Hands.Available ? Loc.T("Hand tracking available") : Loc.T("Hand tracking not detected");
            menu.Show("comfort-" + back, Loc.T("COMFORT & CONTROLS"), hands, items);
        }

        void Modes(VRMenu menu, List<VRMenu.Item> items, string back)
        {
            GameManager gm = GameManager.Instance;
            for (int i = 0; i < ModeManager.All.Length; i++)
            {
                GameMode m = ModeManager.All[i];
                items.Add(new VRMenu.Item(ModeManager.Name(m) + "   " + Loc.T("best") + " " + ModeManager.Best(m).ToString("N0"),
                    () => { sub = ""; gm.Modes.SwitchTo(m); }));
            }
            items.Add(new VRMenu.Item(Loc.T("BACK"), () => sub = ""));
            menu.Show("modes-" + back, Loc.T("SWITCH MODE"), Loc.T("Each mode keeps its own high score"), items);
        }

        void Board(VRMenu menu, List<VRMenu.Item> items)
        {
            List<RunRecord> runs = Leaderboard.Runs;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (runs.Count == 0) sb.Append(Loc.T("No runs yet - be the first!"));
            for (int i = 0; i < runs.Count && i < 10; i++)
            {
                RunRecord r = runs[i];
                if (i > 0) sb.Append('\n');
                sb.Append('#').Append(i + 1).Append("  ").Append(r.name).Append("   ").Append(r.score.ToString("N0"))
                  .Append("   W").Append(r.wave).Append("   ").Append(Mathf.RoundToInt(r.accuracy * 100f)).Append('%');
            }
            items.Add(new VRMenu.Item(Loc.T("BACK"), () => sub = ""));
            menu.Show("board", Loc.T("LEADERBOARD"), sb.ToString(), items);
        }
    }
}
