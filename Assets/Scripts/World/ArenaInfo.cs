using UnityEngine;

namespace NeonBreach
{
    /// <summary>Describes a built arena (put on its root object).</summary>
    public class ArenaInfo : MonoBehaviour
    {
        public int id;
        public float halfX = 30f;
        public float halfZ = 30f;
        public Vector3 playerSpawn;
        public Transform[] spawnPoints;
        [System.NonSerialized] public bool batched;
    }
}
