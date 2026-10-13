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

    [Header("Arena Pickup Points")]
    public Transform[] pickupSpawnPoints;

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

    void Awake()
    {
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        if (!isActiveAndEnabled)
            return;

        GameModeManager localMatch =
            FindFirstObjectByType<GameModeManager>(FindObjectsInactive.Include);

        if (localMatch != null)
            localMatch.spawnPoints = playerSpawnPoints;

        ArenaPickupSpawner localPickups =
            FindFirstObjectByType<ArenaPickupSpawner>(FindObjectsInactive.Include);

        if (localPickups != null)
            localPickups.spawnPoints = pickupSpawnPoints;

        SkateArena onlineArena =
            FindFirstObjectByType<SkateArena>(FindObjectsInactive.Include);

        if (onlineArena != null)
        {
            onlineArena.spawnPoints = playerSpawnPoints;
            onlineArena.pickupPoints = pickupSpawnPoints;
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

        selected.Apply();
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
