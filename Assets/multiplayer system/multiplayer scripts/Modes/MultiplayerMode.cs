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
        public NetworkModeRules rulesPrefab;
    }
}
