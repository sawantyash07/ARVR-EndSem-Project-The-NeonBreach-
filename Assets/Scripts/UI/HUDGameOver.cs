using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Game-over screen with three tabs: SUMMARY (run stats, leaderboard rank, co-op split),
    /// ANALYTICS (death heatmap over a top-down map of the arena, damage dealt vs taken over time,
    /// kills per enemy type, per-weapon accuracy) and LEADERBOARD.
    /// </summary>
    public partial class HUD
    {
        int overTab;
        Texture2D heatmap;
        Texture2D chart;
        float chartMax;

        void DrawGameOver(GameManager gm)
        {
            float t = Time.unscaledTime - gameOverTime;
            float a = Mathf.Clamp01((t - 0.6f) / 0.8f);
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.06f, 0f, 0.02f, 0.82f * a));
            if (a <= 0f) return;
            float cx = W * 0.5f;
            Text(R(cx - 700f, 30f, 1400f, 110f), Loc.T("MISSION FAILED"), 90, new Color(Red.r, Red.g, Red.b, a), TextAnchor.MiddleCenter);
            string sub = newHighScore ? Loc.T("NEW HIGH SCORE!") : (gm.LastRank > 0 ? Loc.T("Leaderboard rank") + " #" + gm.LastRank : "");
            if (sub != "") Text(R(cx - 700f, 135f, 1400f, 40f), sub, 32, new Color(Yellow.r, Yellow.g, Yellow.b, a), TextAnchor.MiddleCenter);

            string[] tabs = { Loc.T("SUMMARY"), Loc.T("ANALYTICS"), Loc.T("LEADERBOARD") };
            for (int i = 0; i < tabs.Length; i++)
                if (Button(R(cx - 390f + i * 265f, 185f, 250f, 52f), tabs[i], a >= 1f, 24, overTab == i)) overTab = i;

            if (overTab == 0) DrawSummary(gm, cx, a);
            else if (overTab == 1) DrawAnalytics(gm, cx);
            else DrawBoardTable(cx - 440f, 270f, 880f, gm.LastRank);

            if (a >= 1f)
            {
                if (Button(R(cx - 250f, 960f, 240f, 70f), Loc.T("RETRY"))) gm.StartGame();
                if (Button(R(cx + 10f, 960f, 240f, 70f), Loc.T("MAIN MENU"))) gm.EnterMenu();
            }
        }

        void DrawSummary(GameManager gm, float cx, float a)
        {
            List<string> rows = new List<string>
            {
                Loc.T("SCORE"), gm.Score.ToString("N0"),
                Loc.T("HIGH SCORE"), gm.HighScore.ToString("N0"),
                Loc.T("WAVE REACHED"), Mathf.Max(1, gm.BestWave).ToString(),
                Loc.T("KILLS"), gm.Kills.ToString(),
                Loc.T("HEADSHOTS"), gm.Headshots.ToString(),
                Loc.T("ACCURACY"), Mathf.RoundToInt(gm.Accuracy * 100f) + "%",
                Loc.T("CREDITS SPENT"), "$" + gm.CreditsSpent.ToString("N0"),
                Loc.T("TIME SURVIVED"), Mathf.FloorToInt(gm.PlayTime / 60f) + ":" + Mathf.FloorToInt(gm.PlayTime % 60f).ToString("00"),
                Loc.T("MODE"), DifficultyInfo.Name(GameSettings.Difficulty) + "  /  " + ArenaCatalog.Name(ArenaManager.CurrentId) + (gm.CoOpRun ? "  /  CO-OP" : ""),
            };
            if (gm.CoOpRun && gm.Players.Count > 1)
            {
                rows.Add(Loc.T("KILLS P1 / P2"));
                rows.Add(gm.Players[0].Kills + " / " + gm.Players[1].Kills);
            }
            for (int i = 0; i < rows.Count / 2; i++)
            {
                float y = 270f + i * 56f;
                Text(R(cx - 380f, y, 420f, 50f), rows[i * 2], 28, new Color(1f, 1f, 1f, 0.75f * a), TextAnchor.MiddleLeft, false);
                Text(R(cx + 20f, y, 360f, 50f), rows[i * 2 + 1], 30, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.MiddleRight);
            }
        }

        // ---------------------------------------------------------------------------- analytics

        void DrawAnalytics(GameManager gm, float cx)
        {
            int arenaKey = MixedReality.Active ? 100 : ArenaManager.CurrentId;
            if (heatmap == null) heatmap = BuildHeatmap(arenaKey);
            if (chart == null) chart = BuildChart(out chartMax);

            // heatmap
            float hx = cx - 900f, hy = 260f, hs = 600f;
            Text(R(hx, hy, hs, 34f), Loc.T("DEATH HEATMAP") + "  (" + DeathMap.Points(arenaKey).Count + " " + Loc.T("deaths recorded") + ")", 22, Yellow, TextAnchor.MiddleLeft);
            float aspect = heatmap != null ? (float)heatmap.width / heatmap.height : 1f;
            float mw = aspect >= 1f ? hs : hs * aspect, mh = aspect >= 1f ? hs / aspect : hs;
            Rect mr = R(hx, hy + 40f, mw, mh);
            Fill(mr, new Color(0f, 0f, 0f, 0.6f));
            if (heatmap != null) GUI.DrawTexture(mr, heatmap, ScaleMode.StretchToFill);
            Border(mr, new Color(1f, 1f, 1f, 0.3f), 1.5f);
            Text(R(hx, hy + 48f + mh, hs, 60f), Loc.T("Red = where players die most. White X = this run. Use it to spot dangerous areas of the map."), 17, Dim, TextAnchor.UpperLeft, false, true);

            // damage over time
            float gx = cx - 260f, gy = 260f, gw = 1160f, gh = 300f;
            Text(R(gx, gy, gw, 34f), Loc.T("DAMAGE OVER TIME") + "  (" + Loc.T("per") + " " + RunAnalytics.Bucket + "s)", 22, Yellow, TextAnchor.MiddleLeft);
            Rect cr = R(gx, gy + 40f, gw, gh);
            Fill(cr, new Color(0f, 0f, 0f, 0.6f));
            if (chart != null) GUI.DrawTexture(cr, chart, ScaleMode.StretchToFill);
            Border(cr, new Color(1f, 1f, 1f, 0.3f), 1.5f);
            Text(R(gx + 8f, gy + 44f, 300f, 24f), Mathf.RoundToInt(chartMax).ToString(), 16, Dim, TextAnchor.MiddleLeft, false);
            float dur = RunAnalytics.Dealt.Count * RunAnalytics.Bucket;
            Text(R(gx + gw - 200f, gy + 44f + gh, 200f, 24f), Mathf.FloorToInt(dur / 60f) + ":" + Mathf.FloorToInt(dur % 60f).ToString("00"), 16, Dim, TextAnchor.MiddleRight, false);
            Text(R(gx, gy + 44f + gh, 200f, 24f), "0:00", 16, Dim, TextAnchor.MiddleLeft, false);
            Fill(R(gx + 260f, gy + 52f + gh, 24f, 8f), Cyan);
            Text(R(gx + 290f, gy + 44f + gh, 200f, 24f), Loc.T("dealt"), 16, Cyan, TextAnchor.MiddleLeft, false);
            Fill(R(gx + 420f, gy + 52f + gh, 24f, 8f), Red);
            Text(R(gx + 450f, gy + 44f + gh, 200f, 24f), Loc.T("taken"), 16, Red, TextAnchor.MiddleLeft, false);
            for (int i = 0; i < RunAnalytics.WaveTimes.Count; i++)
            {
                float k = dur > 0f ? RunAnalytics.WaveTimes[i] / dur : 0f;
                Fill(R(gx + gw * k, gy + 40f, 1.5f, gh), new Color(1f, 0.9f, 0.2f, 0.4f));
            }

            // kills by type
            float ky = gy + gh + 90f;
            Text(R(gx, ky, 560f, 30f), Loc.T("KILLS BY ENEMY TYPE"), 20, Yellow, TextAnchor.MiddleLeft);
            int maxK = 1;
            for (int i = 0; i < RunAnalytics.KillsByType.Length; i++) maxK = Mathf.Max(maxK, RunAnalytics.KillsByType[i]);
            Color[] typeCols = { Red, Magenta, Yellow, Orange, Green, Cyan };
            for (int i = 0; i < RunAnalytics.KillsByType.Length; i++)
            {
                float y = ky + 36f + i * 30f;
                Text(R(gx, y, 150f, 26f), Enemy.TypeName((EnemyType)i), 17, Color.white, TextAnchor.MiddleLeft, false);
                Fill(R(gx + 150f, y + 5f, 340f * RunAnalytics.KillsByType[i] / maxK, 16f), typeCols[i]);
                Text(R(gx + 500f, y, 60f, 26f), RunAnalytics.KillsByType[i].ToString(), 17, Color.white, TextAnchor.MiddleLeft);
            }

            // weapon accuracy
            float wx = gx + 620f;
            Text(R(wx, ky, 540f, 30f), Loc.T("ACCURACY BY WEAPON"), 20, Yellow, TextAnchor.MiddleLeft);
            string[] names = { "VX-9", "AR-77", "SG-12" };
            for (int i = 0; i < 3; i++)
            {
                float y = ky + 36f + i * 40f;
                int shots = RunAnalytics.WeaponShots[i];
                float acc = shots > 0 ? (float)RunAnalytics.WeaponHits[i] / shots : 0f;
                Text(R(wx, y, 100f, 30f), names[i], 18, Color.white, TextAnchor.MiddleLeft);
                Fill(R(wx + 100f, y + 6f, 320f, 18f), new Color(1f, 1f, 1f, 0.08f));
                Fill(R(wx + 100f, y + 6f, 320f * acc, 18f), Cyan);
                Text(R(wx + 430f, y, 140f, 30f), Mathf.RoundToInt(acc * 100f) + "%  (" + shots + ")", 17, Color.white, TextAnchor.MiddleLeft, false);
            }
        }

        static Color HeatColor(float k)
        {
            // transparent -> blue -> cyan -> yellow -> red
            if (k < 0.25f) return new Color(0.1f, 0.2f, 1f, k / 0.25f * 0.6f);
            if (k < 0.5f) return Color.Lerp(new Color(0.1f, 0.2f, 1f, 0.6f), new Color(0.1f, 0.9f, 1f, 0.75f), (k - 0.25f) / 0.25f);
            if (k < 0.75f) return Color.Lerp(new Color(0.1f, 0.9f, 1f, 0.75f), new Color(1f, 0.9f, 0.1f, 0.85f), (k - 0.5f) / 0.25f);
            return Color.Lerp(new Color(1f, 0.9f, 0.1f, 0.85f), new Color(1f, 0.1f, 0.05f, 0.95f), (k - 0.75f) / 0.25f);
        }

        /// <summary>Top-down map of the arena (raycast heights) with a Gaussian heat layer of every recorded death.</summary>
        Texture2D BuildHeatmap(int arenaKey)
        {
            ArenaInfo info = ArenaManager.Current;
            float hx, hz;
            Vector2 center = Vector2.zero;
            if (arenaKey == 100) { hx = Mathf.Max(2f, MixedReality.RoomSize.x * 0.5f + 0.5f); hz = Mathf.Max(2f, MixedReality.RoomSize.y * 0.5f + 0.5f); }
            else { hx = (info != null ? info.halfX : 30f) + 1f; hz = (info != null ? info.halfZ : 30f) + 1f; }
            int tw = 192;
            int th = Mathf.Clamp(Mathf.RoundToInt(tw * hz / hx), 32, 256);
            Color[] px = new Color[tw * th];
            float[] heat = new float[tw * th];

            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    float wx = center.x + Mathf.Lerp(-hx, hx, (x + 0.5f) / tw);
                    float wz = center.y + Mathf.Lerp(-hz, hz, (y + 0.5f) / th);
                    Color c = new Color(0.04f, 0.06f, 0.1f, 1f);
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(wx, 40f, wz), Vector3.down, out hit, 80f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                    {
                        float h = hit.point.y;
                        if (h > 0.3f) c = Color.Lerp(new Color(0.25f, 0.3f, 0.4f, 1f), new Color(0.6f, 0.7f, 0.85f, 1f), Mathf.Clamp01(h / 8f));
                        else if (((Mathf.FloorToInt(wx + 1000f) % 4) == 0) || ((Mathf.FloorToInt(wz + 1000f) % 4) == 0)) c = new Color(0.06f, 0.12f, 0.18f, 1f);
                    }
                    px[y * tw + x] = c;
                }
            }

            List<Vector2> pts = DeathMap.Points(arenaKey);
            float radius = 4.5f;
            int r = Mathf.CeilToInt(radius * 2f);
            float maxHeat = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                float fx = (pts[i].x - center.x + hx) / (2f * hx) * tw;
                float fy = (pts[i].y - center.y + hz) / (2f * hz) * th;
                int ix = Mathf.RoundToInt(fx), iy = Mathf.RoundToInt(fy);
                for (int dy = -r; dy <= r; dy++)
                {
                    int yy = iy + dy;
                    if (yy < 0 || yy >= th) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = ix + dx;
                        if (xx < 0 || xx >= tw) continue;
                        float d2 = (xx - fx) * (xx - fx) + (yy - fy) * (yy - fy);
                        float v = Mathf.Exp(-d2 / (2f * radius * radius));
                        heat[yy * tw + xx] += v;
                        if (heat[yy * tw + xx] > maxHeat) maxHeat = heat[yy * tw + xx];
                    }
                }
            }
            if (maxHeat > 0f)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    if (heat[i] <= 0.01f) continue;
                    Color hc = HeatColor(Mathf.Clamp01(heat[i] / maxHeat));
                    px[i] = Color.Lerp(px[i], new Color(hc.r, hc.g, hc.b, 1f), hc.a);
                }
            }

            // this run's death: white X
            if (RunAnalytics.HasDeath)
            {
                int ix = Mathf.RoundToInt((RunAnalytics.LastDeath.x - center.x + hx) / (2f * hx) * tw);
                int iy = Mathf.RoundToInt((RunAnalytics.LastDeath.y - center.y + hz) / (2f * hz) * th);
                for (int k = -5; k <= 5; k++)
                {
                    for (int w = -1; w <= 1; w++)
                    {
                        Plot(px, tw, th, ix + k + w, iy + k, Color.white);
                        Plot(px, tw, th, ix + k + w, iy - k, Color.white);
                    }
                }
            }

            Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        static void Plot(Color[] px, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        /// <summary>Line chart of damage dealt (cyan) and taken (red) per time bucket, with filled areas.</summary>
        Texture2D BuildChart(out float max)
        {
            int tw = 580, th = 150;
            Color[] px = new Color[tw * th];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < th; y += th / 4) for (int x = 0; x < tw; x++) px[y * tw + x] = new Color(1f, 1f, 1f, 0.08f);

            List<float> dealt = RunAnalytics.Dealt, taken = RunAnalytics.Taken;
            max = 10f;
            for (int i = 0; i < dealt.Count; i++) max = Mathf.Max(max, dealt[i], taken[i]);
            int n = dealt.Count;
            if (n >= 1)
            {
                Series(px, tw, th, dealt, max, new Color(0.15f, 0.92f, 1f, 1f));
                Series(px, tw, th, taken, max, new Color(1f, 0.2f, 0.2f, 1f));
            }
            Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        static void Series(Color[] px, int tw, int th, List<float> values, float max, Color col)
        {
            int n = values.Count;
            int prevY = -1;
            for (int x = 0; x < tw; x++)
            {
                float f = n == 1 ? 0f : x / (float)(tw - 1) * (n - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, n - 1);
                int i1 = Mathf.Min(i0 + 1, n - 1);
                float v = Mathf.Lerp(values[i0], values[i1], f - i0);
                int y = Mathf.Clamp(Mathf.RoundToInt(v / max * (th - 3)), 0, th - 1);
                for (int yy = 0; yy < y; yy++)
                {
                    Color under = px[yy * tw + x];
                    px[yy * tw + x] = Color.Lerp(under, new Color(col.r, col.g, col.b, 1f), 0.18f) + new Color(0f, 0f, 0f, 0.18f);
                }
                int from = prevY < 0 ? y : Mathf.Min(prevY, y), to = prevY < 0 ? y : Mathf.Max(prevY, y);
                for (int yy = from - 1; yy <= to + 1; yy++) Plot(px, tw, th, x, yy, col);
                prevY = y;
            }
        }
    }
}
