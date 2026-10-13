using System;
using System.Linq;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    [CreateAssetMenu(menuName = "Hit Boss/Multiplayer Map")]
    public sealed class MultiplayerMap : ScriptableObject
    {
        public string mapId;
        public string displayName;
        public string sceneName;
        public int sortOrder;
        public Sprite preview;

        public void ValidateConfiguration(MultiplayerMode mode)
        {
            if (string.IsNullOrWhiteSpace(mapId) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(sceneName))
                throw new InvalidOperationException("This map needs an ID, name and scene.");

            if (mode == null || !string.Equals(sceneName, mode.sceneName, StringComparison.Ordinal))
                throw new InvalidOperationException("This map is not available for the selected mode.");
        }
    }

    public static class MultiplayerMaps
    {
        public static MultiplayerMap[] ForMode(MultiplayerMap[] maps, MultiplayerMode mode)
        {
            if (maps == null || mode == null)
                return Array.Empty<MultiplayerMap>();

            return maps
                .Where(map => map != null &&
                    string.Equals(map.sceneName, mode.sceneName, StringComparison.Ordinal))
                .OrderBy(map => map.sortOrder)
                .ThenBy(map => map.displayName)
                .ToArray();
        }

        public static MultiplayerMap Find(MultiplayerMap[] maps, string mapId)
        {
            if (maps == null || string.IsNullOrWhiteSpace(mapId))
                return null;

            return maps.FirstOrDefault(map =>
                map != null &&
                string.Equals(map.mapId, mapId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
