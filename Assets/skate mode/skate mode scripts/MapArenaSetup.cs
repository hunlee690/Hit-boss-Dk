using System;
using System.Linq;
using HitBoss.Multiplayer.Skate;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class MapArenaSetup : MonoBehaviour
{
    [Header("Map")]
    public string mapId;
    public string displayName;

    [Header("Player Spawn Points")]
    public Transform[] playerSpawnPoints;

    [Header("Mystery Box Reward Chances")]
    public MysteryBoxRewards boxRewards;
    [Header("Race Track Surface")]
    public MeshCollider raceSurface;

    [Header("Arena Pickup Points")]
    public Transform[] pickupSpawnPoints;

    [Header("Lap Race")]
    [Tooltip("Ordered centerline points. Point 0 is the start line; the last point loops back to it.")]
    public Transform[] raceRoute;
    [Min(2)] public float raceTrackHalfWidth = 16;
    [Min(1)] public int lapsToFinish = 3;

    public string EffectiveMapId =>
        string.IsNullOrWhiteSpace(mapId)
            ? transform.parent != null ? transform.parent.name : name
            : mapId;

    GameObject ArenaRoot
    {
        get
        {
            Transform root = transform;
            while (root.parent != null && root.parent.parent != null)
                root = root.parent;
            return root.gameObject;
        }
    }

    public static bool ActivateMap(string selectedMapId)
    {
        MapArenaSetup[] setups = FindObjectsByType<MapArenaSetup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (setups.Length == 0)
            return false;

        string wanted = Normalize(selectedMapId);
        MapArenaSetup selected = setups.FirstOrDefault(
            setup => Normalize(setup.EffectiveMapId) == wanted);

        selected ??= setups.FirstOrDefault(
            setup => setup.gameObject.activeInHierarchy);
        selected ??= setups[0];

        foreach (MapArenaSetup setup in setups)
            setup.ArenaRoot.SetActive(setup == selected);

        Debug.Log("[Maps] Loaded " +
            (string.IsNullOrWhiteSpace(selected.displayName)
                ? selected.EffectiveMapId
                : selected.displayName));
        return true;
    }

    static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }
}
