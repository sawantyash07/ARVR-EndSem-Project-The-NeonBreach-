using UnityEngine;

namespace NeonBreach
{
    /// <summary>The ARMORY: spend credits on team weapon upgrades and supplies between waves.</summary>
    public partial class HUD
    {
        int shopKeyFrame = -1;

        void DrawShop(GameManager gm)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.02f, 0.05f, 0.82f));
            float cx = W * 0.5f;
            Text(R(cx - 700f, 40f, 1400f, 90f), Loc.T("ARMORY"), 80, Cyan, TextAnchor.MiddleCenter);
            Text(R(cx - 700f, 128f, 1400f, 40f), Loc.T("Wave") + " " + gm.Waves.Wave + " " + Loc.T("cleared") + "  -  " + Loc.T("upgrades are shared by the whole team"), 24, Dim, TextAnchor.MiddleCenter, false);
            Text(R(cx - 700f, 170f, 1400f, 56f), Loc.T("CREDITS") + "   $" + gm.Credits.ToString("N0"), 44, Yellow, TextAnchor.MiddleCenter);

            // keyboard shortcuts 1-8, Enter to continue
            // (OnGUI runs several times per frame - read key presses only once per frame, so a press buys once)
            int quick = -1;
            bool cont = false;
            if (shopKeyFrame != Time.frameCount)
            {
                shopKeyFrame = Time.frameCount;
                for (int k = 0; k < 8; k++) if (DesktopInput.Pressed(KeyCode.Alpha1 + k)) quick = k;
                cont = DesktopInput.Pressed(KeyCode.Return) || DesktopInput.Pressed(KeyCode.KeypadEnter);
            }

            // upgrades
            float cardW = 330f, gap = 18f;
            int n = Upgrades.All.Length;
            float total = n * cardW + (n - 1) * gap;
            float x0 = cx - total * 0.5f;
            for (int i = 0; i < n; i++)
            {
                UpgradeId u = Upgrades.All[i];
                float x = x0 + i * (cardW + gap), y = 250f;
                Rect card = R(x, y, cardW, 340f);
                Fill(card, new Color(0.02f, 0.06f, 0.11f, 0.95f));
                Border(card, Upgrades.Maxed(u) ? Green : new Color(0.15f, 0.6f, 0.7f, 0.9f), 2f);
                Text(R(x, y + 10f, cardW, 30f), "[" + (i + 1) + "]", 18, Dim, TextAnchor.MiddleCenter);
                Text(R(x, y + 40f, cardW, 40f), Upgrades.Name(u).ToUpperInvariant(), 26, Cyan, TextAnchor.MiddleCenter);
                int lv = Upgrades.Level(u), mx = Upgrades.MaxLevel(u);
                float pipW = (cardW - 60f) / mx;
                for (int l = 0; l < mx; l++)
                    Fill(R(x + 30f + l * pipW + 3f, y + 95f, pipW - 6f, 14f), l < lv ? Cyan : new Color(1f, 1f, 1f, 0.15f));
                Text(R(x + 15f, y + 125f, cardW - 30f, 90f), Upgrades.Effect(u), 20, Color.white, TextAnchor.UpperCenter, false, true);
                string cost = Upgrades.Maxed(u) ? Loc.T("MAXED") : "$" + Upgrades.Cost(u).ToString("N0");
                bool afford = !Upgrades.Maxed(u) && gm.Credits >= Upgrades.Cost(u);
                Text(R(x, y + 215f, cardW, 40f), cost, 30, Upgrades.Maxed(u) ? Green : (afford ? Yellow : new Color(1f, 0.4f, 0.4f)), TextAnchor.MiddleCenter);
                if (Button(R(x + 40f, y + 265f, cardW - 80f, 56f), Loc.T("BUY"), afford, 26) || (quick == i && afford)) gm.TryBuyUpgrade(u);
                else if (quick == i) Sfx.Play2D(SfxId.Deny, 0.6f);
            }

            // supplies
            ShopItem[] items = { ShopItem.AmmoRefill, ShopItem.Medkit, ShopItem.Grenades };
            float iw = 420f;
            float ix0 = cx - (items.Length * iw + (items.Length - 1) * gap) * 0.5f;
            Text(R(cx - 700f, 610f, 1400f, 36f), Loc.T("SUPPLIES (for every player)"), 24, Orange, TextAnchor.MiddleCenter);
            for (int i = 0; i < items.Length; i++)
            {
                float x = ix0 + i * (iw + gap), y = 652f;
                Rect r = R(x, y, iw, 120f);
                Fill(r, new Color(0.06f, 0.04f, 0.02f, 0.9f));
                Border(r, new Color(0.8f, 0.45f, 0.1f, 0.8f), 2f);
                Text(R(x + 16f, y + 10f, iw - 32f, 36f), "[" + (n + i + 1) + "]  " + Upgrades.ItemName(items[i]), 24, Color.white, TextAnchor.MiddleLeft);
                int cost = Upgrades.ItemCost(items[i]);
                bool afford = gm.Credits >= cost;
                Text(R(x + 16f, y + 60f, 160f, 44f), "$" + cost, 28, afford ? Yellow : new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);
                if (Button(R(x + iw - 196f, y + 58f, 180f, 50f), Loc.T("BUY"), afford, 24) || (quick == n + i && afford)) gm.TryBuyItem(items[i]);
                else if (quick == n + i) Sfx.Play2D(SfxId.Deny, 0.6f);
            }

            // team status
            string status = "";
            for (int i = 0; i < gm.Players.Count; i++)
            {
                PlayerController p = gm.Players[i];
                if (p == null) continue;
                if (i > 0) status += "      ";
                status += "P" + (i + 1) + "  HP " + Mathf.CeilToInt(p.Health) + "  " + Loc.T("SHIELD") + " " + Mathf.CeilToInt(p.maxShield) +
                          "  " + Loc.T("MEDKITS") + " " + p.Medkits + "  " + Loc.T("GRENADES") + " " + p.weapons.Grenades;
            }
            Text(R(cx - 800f, 800f, 1600f, 36f), status, 22, Dim, TextAnchor.MiddleCenter, false);

            if (Button(R(cx - 220f, 860f, 440f, 80f), Loc.T("CONTINUE") + "  >>", true, 34) || cont) gm.CloseShop();
            Text(R(cx - 700f, 950f, 1400f, 30f), Loc.T("Keys 1-8 buy  -  ENTER / ESC / START continue"), 18, Dim, TextAnchor.MiddleCenter, false);
        }
    }
}
