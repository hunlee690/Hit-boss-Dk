using UnityEngine;

namespace HitBoss.Multiplayer
{
    public sealed class SharedMovementRules : NetworkModeRules
    {
        Transform[] spawnPoints;
        Vector3 fallback;
        void Awake()
        {
            var legacy = FindFirstObjectByType<HitBoss.Multiplayer.Skate.SkateArena>();
            spawnPoints = legacy != null ? legacy.spawnPoints : null;
            // Prefer authored spawn points. Otherwise use the existing scene player position.
            var existing = FindFirstObjectByType<PlayerController>();
            fallback = existing != null ? existing.transform.position : Vector3.up * 2;
        }
        public override Pose SpawnPose(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[index % spawnPoints.Length] != null)
            { var point = spawnPoints[index % spawnPoints.Length]; return new Pose(point.position, point.rotation); }
            var position = fallback + new Vector3((index % 4) * 2, 0, (index / 4) * 2);
            if (Physics.Raycast(position + Vector3.up * 50, Vector3.down, out var hit, 150, ~0, QueryTriggerInteraction.Ignore)) position = hit.point + Vector3.up * 1.2f;
            return new Pose(position, Quaternion.identity);
        }
        public override void ConfigurePlayer(NetworkPlayer player, MultiplayerMode mode)
        {
            player.movement.startInSkateMode = mode.skating;
            // Existing locomotion stays in one place; each future mode can replace this adapter.
        }
    }
}
