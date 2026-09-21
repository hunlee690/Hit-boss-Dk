using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkateArena : MonoBehaviour
    {
        public Transform[] spawnPoints, pickupPoints;
        public float matchDuration = 180, respawnDelay = 2;
    }
}
