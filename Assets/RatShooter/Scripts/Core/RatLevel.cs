using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// One Rat Shooter level: how many mice must be caught, how many may run around at once, the time limit,
    /// how fast they are and which kinds appear. Levels get progressively harder (more targets, faster, more
    /// skittish kinds). Edit them on the RatShooterGame component in the Inspector.
    /// </summary>
    [System.Serializable]
    public class RatLevel
    {
        public string title = "Morning Chores";
        [TextArea] public string subtitle = "";
        [Min(1)] public int mice = 6;
        [Min(1)] public int maxActive = 3;
        [Tooltip("Seconds.")]
        public float timeLimit = 90f;
        [Tooltip("Speed multiplier for every mouse in this level.")]
        public float speed = 0.85f;
        [Tooltip("Average seconds between mice popping out of burrows.")]
        public float spawnInterval = 2f;
        [Tooltip("Rounds on top of two per mouse.")]
        public int extraAmmo = 8;

        [Header("Mix (relative chance)")]
        public float grey = 1f;
        public float brown;
        public float chubby;
        public float white;
        public float golden;

        public float Weight(string id)
        {
            switch (id)
            {
                case "grey": return grey;
                case "brown": return brown;
                case "chubby": return chubby;
                case "white": return white;
                case "golden": return golden;
                default: return 0f;
            }
        }

        public static RatLevel[] Defaults()
        {
            return new[]
            {
                new RatLevel { title = "Morning Chores", subtitle = "A few field mice are nibbling the grain.", mice = 6, maxActive = 3, timeLimit = 90f, speed = 0.85f, spawnInterval = 2.2f, extraAmmo = 10, grey = 1f, brown = 0.4f, chubby = 0.5f },
                new RatLevel { title = "Hungry Visitors", subtitle = "Word got out about the cheese...", mice = 9, maxActive = 4, timeLimit = 100f, speed = 0.95f, spawnInterval = 1.9f, extraAmmo = 9, grey = 1f, brown = 0.8f, chubby = 0.7f, white = 0.3f },
                new RatLevel { title = "Barnyard Rush", subtitle = "Snowy mice zig-zag - lead your shots!", mice = 12, maxActive = 5, timeLimit = 110f, speed = 1f, spawnInterval = 1.6f, extraAmmo = 8, grey = 1f, brown = 1f, chubby = 0.6f, white = 0.9f, golden = 0.15f },
                new RatLevel { title = "Midday Mayhem", subtitle = "Faster, sneakier, and a golden speedster.", mice = 15, maxActive = 6, timeLimit = 120f, speed = 1.1f, spawnInterval = 1.4f, extraAmmo = 8, grey = 0.8f, brown = 1f, chubby = 0.5f, white = 1f, golden = 0.3f },
                new RatLevel { title = "The Big Squeak", subtitle = "The whole mischief is here. Save the farm!", mice = 20, maxActive = 8, timeLimit = 140f, speed = 1.2f, spawnInterval = 1.1f, extraAmmo = 8, grey = 0.8f, brown = 1f, chubby = 0.6f, white = 1f, golden = 0.45f }
            };
        }
    }

    public enum RatDifficulty { Easy, Normal, Hard }

    public static class RatDifficultyInfo
    {
        static readonly float[] time = { 1.3f, 1f, 0.85f };
        static readonly float[] ammo = { 1.5f, 1.15f, 0.9f };
        static readonly float[] speed = { 0.85f, 1f, 1.15f };
        static readonly float[] score = { 0.75f, 1f, 1.4f };
        static readonly string[] names = { "EASY", "NORMAL", "HARD" };
        static readonly string[] blurbs = { "More time and ammo, slower mice - score x0.75", "The intended experience - score x1", "Less time, fast skittish mice - score x1.4" };

        static int I(RatDifficulty d) { return Mathf.Clamp((int)d, 0, 2); }
        public static float Time(RatDifficulty d) { return time[I(d)]; }
        public static float Ammo(RatDifficulty d) { return ammo[I(d)]; }
        public static float Speed(RatDifficulty d) { return speed[I(d)]; }
        public static float Score(RatDifficulty d) { return score[I(d)]; }
        public static string Name(RatDifficulty d) { return names[I(d)]; }
        public static string Blurb(RatDifficulty d) { return blurbs[I(d)]; }
    }
}
