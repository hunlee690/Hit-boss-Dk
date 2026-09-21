using System;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    [CreateAssetMenu(menuName = "Hit Boss/Multiplayer Mode")]
    public sealed class MultiplayerMode : ScriptableObject
    {
        public string modeId;
        public string displayName;
        public string sceneName;
        [Range(2, 8)] public int maxPlayers = 5;
        [Range(1, 8)] public int minPlayers = 2;
        public bool skating;
        public bool useCoopMovement;
        public NetworkModeRules rulesPrefab;

        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(modeId) || string.IsNullOrWhiteSpace(sceneName) || rulesPrefab == null)
                throw new InvalidOperationException("This mode needs an ID, scene and rules prefab before it can be played online.");
            if (minPlayers < 1 || maxPlayers < 2 || minPlayers > maxPlayers || maxPlayers > 8)
                throw new InvalidOperationException("This mode has invalid player limits.");
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException("This mode's scene must be enabled in Build Settings.");
        }
    }
}
