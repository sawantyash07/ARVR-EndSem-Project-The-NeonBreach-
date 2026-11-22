using UnityEngine;

namespace NeonBreach
{
    public enum UpgradeId { Damage, FireRate, Capacity, ReloadSpeed, Shield }
    public enum ShopItem { AmmoRefill, Medkit, Grenades }

    /// <summary>
    /// Permanent (per run) weapon upgrades bought in the shop between waves with credits.
    /// Shared by both players in co-op (team upgrades).
    /// </summary>
    public static class Upgrades
    {
        public static readonly UpgradeId[] All = (UpgradeId[])System.Enum.GetValues(typeof(UpgradeId));
        static readonly int[] maxLevel = { 5, 5, 3, 3, 4 };
        static readonly int[] baseCost = { 900, 800, 700, 600, 750 };
        static readonly int[] level = new int[5];

        public static int Level(UpgradeId u) { return level[(int)u]; }
        public static int MaxLevel(UpgradeId u) { return maxLevel[(int)u]; }
        public static bool Maxed(UpgradeId u) { return level[(int)u] >= maxLevel[(int)u]; }
        public static int Cost(UpgradeId u) { return baseCost[(int)u] * (level[(int)u] + 1); }

        public static void Reset() { for (int i = 0; i < level.Length; i++) level[i] = 0; }
        public static void Buy(UpgradeId u) { if (!Maxed(u)) level[(int)u]++; }

        public static float DamageMult { get { return 1f + 0.15f * level[(int)UpgradeId.Damage]; } }
        public static float FireRateMult { get { return 1f + 0.10f * level[(int)UpgradeId.FireRate]; } }
        public static float CapacityMult { get { return 1f + 0.25f * level[(int)UpgradeId.Capacity]; } }
        public static float ReloadTimeMult { get { return 1f - 0.15f * level[(int)UpgradeId.ReloadSpeed]; } }
        public static float ShieldBonus { get { return 20f * level[(int)UpgradeId.Shield]; } }

        public static string Name(UpgradeId u)
        {
            switch (u)
            {
                case UpgradeId.Damage: return Loc.T("Damage");
                case UpgradeId.FireRate: return Loc.T("Fire rate");
                case UpgradeId.Capacity: return Loc.T("Ammo capacity");
                case UpgradeId.ReloadSpeed: return Loc.T("Reload speed");
                default: return Loc.T("Max shield");
            }
        }

        public static string Effect(UpgradeId u)
        {
            switch (u)
            {
                case UpgradeId.Damage: return "+15% " + Loc.T("damage per level");
                case UpgradeId.FireRate: return "+10% " + Loc.T("fire rate per level");
                case UpgradeId.Capacity: return "+25% " + Loc.T("magazine and reserve");
                case UpgradeId.ReloadSpeed: return "-15% " + Loc.T("reload time per level");
                default: return "+20 " + Loc.T("shield per level");
            }
        }

        public static int ItemCost(ShopItem i)
        {
            switch (i)
            {
                case ShopItem.AmmoRefill: return 350;
                case ShopItem.Medkit: return 500;
                default: return 450;
            }
        }

        public static string ItemName(ShopItem i)
        {
            switch (i)
            {
                case ShopItem.AmmoRefill: return Loc.T("Full ammo refill");
                case ShopItem.Medkit: return Loc.T("Medkit");
                default: return Loc.T("2 grenades");
            }
        }
    }

    /// <summary>
    /// Timed power-ups. Slow motion only slows the ENEMIES and their projectiles (not the player or the real
    /// time scale) - in VR, changing the global time scale makes head tracking feel wrong, so this keeps it comfortable.
    /// </summary>
    public static class PowerUps
    {
        public const float DoubleDamageDuration = 12f;
        public const float SlowMoDuration = 8f;

        static float ddUntil = -1f, slowUntil = -1f;

        public static bool DoubleDamage { get { return Time.time < ddUntil; } }
        public static bool SlowMo { get { return Time.time < slowUntil; } }
        public static float DoubleDamageLeft { get { return Mathf.Max(0f, ddUntil - Time.time); } }
        public static float SlowMoLeft { get { return Mathf.Max(0f, slowUntil - Time.time); } }

        /// <summary>Multiplier applied to enemy delta time.</summary>
        public static float EnemyTimeScale { get { return SlowMo ? 0.35f : 1f; } }
        public static float DamageMult { get { return DoubleDamage ? 2f : 1f; } }

        public static void GiveDoubleDamage() { ddUntil = Mathf.Max(ddUntil, Time.time) + DoubleDamageDuration; }
        public static void GiveSlowMo() { slowUntil = Mathf.Max(slowUntil, Time.time) + SlowMoDuration; }
        public static void Reset() { ddUntil = -1f; slowUntil = -1f; }
    }
}
