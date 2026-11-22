using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// One kind of cartoon mouse: look, size, speeds, points and personality. Every value is editable on the
    /// MouseSpawner in the Inspector; <see cref="Defaults"/> provides the five built-in kinds.
    /// </summary>
    [System.Serializable]
    public class MouseVariant
    {
        public string id = "grey";
        public string displayName = "Field Mouse";
        [Tooltip("Shown on the How To Play screen.")]
        public string trait = "";
        [Tooltip("Prefab saved by the scene builder (Assets/RatShooter/Prefabs). Built at runtime if empty.")]
        public GameObject prefab;

        [Header("Look")]
        public float scale = 1f;
        public Color furColor = new Color(0.62f, 0.62f, 0.66f);
        public Color bellyColor = new Color(0.9f, 0.88f, 0.86f);
        public Color pinkColor = new Color(1f, 0.62f, 0.68f);
        public Color eyeColor = new Color(0.06f, 0.05f, 0.07f);

        [Header("Movement (m/s)")]
        public float walkSpeed = 1.1f;
        public float runSpeed = 3.2f;
        public float panicSpeed = 4.8f;

        [Header("Scoring")]
        public int points = 100;

        [Header("Personality (0..1)")]
        [Tooltip("Stays in the open longer and shrugs off distant shots.")]
        [Range(0f, 1f)] public float boldness = 0.5f;
        [Tooltip("Stands up, sniffs and looks around more; peeks out of cover sooner.")]
        [Range(0f, 1f)] public float curiosity = 0.5f;
        [Tooltip("Heads for food and eats for longer.")]
        [Range(0f, 1f)] public float greed = 0.5f;
        [Tooltip("Zig-zags and changes direction without warning.")]
        [Range(0f, 1f)] public float erratic = 0.2f;
        [Tooltip("Chance to dive into a burrow when scared (and pop out of another one later).")]
        [Range(0f, 1f)] public float burrowChance = 0.2f;

        public static MouseVariant[] Defaults()
        {
            return new[]
            {
                new MouseVariant
                {
                    id = "grey", displayName = "Field Mouse", trait = "Average speed, steady nerves",
                    scale = 1f, furColor = new Color(0.6f, 0.6f, 0.66f), bellyColor = new Color(0.93f, 0.9f, 0.88f),
                    walkSpeed = 1.1f, runSpeed = 3.1f, panicSpeed = 4.6f, points = 100,
                    boldness = 0.5f, curiosity = 0.5f, greed = 0.5f, erratic = 0.25f, burrowChance = 0.15f
                },
                new MouseVariant
                {
                    id = "brown", displayName = "House Mouse", trait = "Timid - hides at the first bang",
                    scale = 0.9f, furColor = new Color(0.62f, 0.42f, 0.27f), bellyColor = new Color(0.95f, 0.85f, 0.7f),
                    walkSpeed = 1.2f, runSpeed = 3.5f, panicSpeed = 5.2f, points = 120,
                    boldness = 0.2f, curiosity = 0.35f, greed = 0.4f, erratic = 0.35f, burrowChance = 0.3f
                },
                new MouseVariant
                {
                    id = "chubby", displayName = "Chubby Mouse", trait = "Big, slow and always eating",
                    scale = 1.35f, furColor = new Color(0.42f, 0.4f, 0.45f), bellyColor = new Color(0.85f, 0.82f, 0.8f),
                    walkSpeed = 0.8f, runSpeed = 2.4f, panicSpeed = 3.6f, points = 80,
                    boldness = 0.8f, curiosity = 0.3f, greed = 0.95f, erratic = 0.1f, burrowChance = 0.05f
                },
                new MouseVariant
                {
                    id = "white", displayName = "Snowy Mouse", trait = "Curious zig-zagger, peeks a lot",
                    scale = 0.95f, furColor = new Color(0.97f, 0.97f, 0.97f), bellyColor = new Color(1f, 0.97f, 0.95f),
                    pinkColor = new Color(1f, 0.58f, 0.66f), eyeColor = new Color(0.85f, 0.15f, 0.28f),
                    walkSpeed = 1.2f, runSpeed = 3.4f, panicSpeed = 5f, points = 150,
                    boldness = 0.45f, curiosity = 0.95f, greed = 0.3f, erratic = 0.7f, burrowChance = 0.2f
                },
                new MouseVariant
                {
                    id = "golden", displayName = "Golden Speedster", trait = "Rare, tiny and lightning fast!",
                    scale = 0.78f, furColor = new Color(1f, 0.72f, 0.18f), bellyColor = new Color(1f, 0.93f, 0.65f),
                    walkSpeed = 1.8f, runSpeed = 5f, panicSpeed = 7f, points = 300,
                    boldness = 0.3f, curiosity = 0.6f, greed = 0.2f, erratic = 0.9f, burrowChance = 0.35f
                }
            };
        }
    }
}
