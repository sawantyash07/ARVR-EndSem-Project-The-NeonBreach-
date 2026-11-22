using System;
using UnityEngine;

namespace NeonBreach.Farm
{
    public enum FarmState { Title, Briefing, Playing, Paused, Victory, GameOver }
    public enum FarmDifficulty { Easy, Normal, Hard }
    public enum SurfaceKind { Dirt, Grass, Wood, Metal, Stone, Hay, Water, Foliage }
    public enum FailReason { None, TimeUp, CropsDestroyed, OutOfAmmo }

    /// <summary>
    /// Everything that changes between Easy / Normal / Hard. One instance per level lives on FarmGameManager,
    /// so all values are editable in the Inspector.
    /// </summary>
    [Serializable]
    public class DifficultyProfile
    {
        public string displayName = "NORMAL";
        [TextArea(2, 4)] public string description = "";

        [Header("Mission")]
        [Min(1)] public int ratsToEliminate = 15;
        [Min(10f)] public float timeLimit = 240f;
        [Tooltip("Crop health lost per feeding rat per second (0..1 scale, 0.004 = 0.4 %/s).")]
        [Range(0f, 0.05f)] public float cropDamagePerSecond = 0.0025f;

        [Header("Spawning")]
        [Range(1, 20)] public int maxActiveRats = 5;
        [Tooltip("Seconds between spawns (random in range).")]
        public Vector2 spawnInterval = new Vector2(2.5f, 5f);
        [Tooltip("Burrows up to this tier are used: 0 = open ground near the farmer, 1 = inside fields, 2 = far edges, buildings and corners.")]
        [Range(0, 2)] public int maxSpawnTier = 1;

        [Header("Rat behaviour")]
        [Tooltip("Multiplies every rat speed (walk, run, flee).")]
        [Range(0.3f, 3f)] public float speedMultiplier = 1f;
        [Tooltip("Rats within this distance of a gunshot or bullet impact panic.")]
        [Range(0f, 80f)] public float hearingRadius = 28f;
        [Tooltip("Chance that a panicking rat runs for cover instead of a burrow / open ground.")]
        [Range(0f, 1f)] public float hideChance = 0.6f;
        [Tooltip("Scales the sideways zig-zag while fleeing.")]
        [Range(0f, 3f)] public float zigzagStrength = 1f;
        [Tooltip("Chance per waypoint that a roaming rat suddenly changes direction.")]
        [Range(0f, 1f)] public float directionChangeChance = 0.25f;

        [Header("Shooting")]
        [Tooltip("Scope sway multiplier.")]
        [Range(0f, 3f)] public float swayMultiplier = 1f;
        [Range(0.1f, 5f)] public float scoreMultiplier = 1f;

        public DifficultyProfile Clone() { return (DifficultyProfile)MemberwiseClone(); }

        public static DifficultyProfile DefaultEasy()
        {
            return new DifficultyProfile
            {
                displayName = "EASY",
                description = "Slow rats that stay close to the farmhouse. Generous time and gentle scope sway.",
                ratsToEliminate = 10, timeLimit = 300f, cropDamagePerSecond = 0.0015f,
                maxActiveRats = 4, spawnInterval = new Vector2(3.5f, 6f), maxSpawnTier = 0,
                speedMultiplier = 0.75f, hearingRadius = 18f, hideChance = 0.4f, zigzagStrength = 0.6f,
                directionChangeChance = 0.15f, swayMultiplier = 0.6f, scoreMultiplier = 0.8f
            };
        }

        public static DifficultyProfile DefaultNormal()
        {
            return new DifficultyProfile
            {
                displayName = "NORMAL",
                description = "Rats raid every field. They panic at gunshots, zig-zag and dive behind cover.",
                ratsToEliminate = 15, timeLimit = 270f, cropDamagePerSecond = 0.0025f,
                maxActiveRats = 6, spawnInterval = new Vector2(2.5f, 4.5f), maxSpawnTier = 1,
                speedMultiplier = 1f, hearingRadius = 28f, hideChance = 0.6f, zigzagStrength = 1f,
                directionChangeChance = 0.25f, swayMultiplier = 1f, scoreMultiplier = 1f
            };
        }

        public static DifficultyProfile DefaultHard()
        {
            return new DifficultyProfile
            {
                displayName = "HARD",
                description = "Fast, skittish rats from every corner of the farm - barns, orchard, pond and far fences.",
                ratsToEliminate = 22, timeLimit = 240f, cropDamagePerSecond = 0.0038f,
                maxActiveRats = 8, spawnInterval = new Vector2(1.6f, 3.2f), maxSpawnTier = 2,
                speedMultiplier = 1.4f, hearingRadius = 40f, hideChance = 0.75f, zigzagStrength = 1.5f,
                directionChangeChance = 0.4f, swayMultiplier = 1.35f, scoreMultiplier = 1.5f
            };
        }
    }

    /// <summary>A line of the kill feed ("+175  HEADSHOT  64 m").</summary>
    public struct FeedEntry
    {
        public string text;
        public Color color;
        public float time;
    }
}
