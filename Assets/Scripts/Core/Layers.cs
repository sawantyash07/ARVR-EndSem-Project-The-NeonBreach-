using UnityEngine;

namespace NeonBreach
{
    /// <summary>Physics layer indices used by the game. Names are written into TagManager by the editor builder.</summary>
    public static class Layers
    {
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Environment = 10;
        public const int FX = 11;
        public const int EnemyHead = 12;
        public const int P1Body = 13;
        public const int P2Body = 14;
        public const int UI = 5;   // Unity's built-in "UI" layer (VR menu panels)

        public static readonly string[] Names = { "Player", "Enemy", "Environment", "FX", "EnemyHead", "P1Body", "P2Body" };

        public static int PlayerBody(int index) { return index == 0 ? P1Body : P2Body; }

        public static int EnvMask { get { return 1 << Environment; } }
        public static int EnemyMask { get { return (1 << Enemy) | (1 << EnemyHead); } }
        public static int ShootMask { get { return (1 << Enemy) | (1 << EnemyHead) | (1 << Environment); } }
        public static int PlayerShotMask { get { return (1 << Player) | (1 << Environment); } }

        /// <summary>Called once at runtime: debris/grenades never collide with actors, heads never collide with anything.</summary>
        public static void SetupCollisionMatrix()
        {
            Physics.IgnoreLayerCollision(FX, Player, true);
            Physics.IgnoreLayerCollision(FX, Enemy, true);
            Physics.IgnoreLayerCollision(FX, EnemyHead, true);
            Physics.IgnoreLayerCollision(FX, FX, true);
            Physics.IgnoreLayerCollision(EnemyHead, Player, true);
            Physics.IgnoreLayerCollision(EnemyHead, Enemy, true);
            Physics.IgnoreLayerCollision(EnemyHead, EnemyHead, true);
            Physics.IgnoreLayerCollision(EnemyHead, Environment, true);
        }
    }

    /// <summary>Anything that can be hurt by weapons: enemies and the player.</summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, Vector3 hitPoint, bool headshot, GameObject source);
    }
}
