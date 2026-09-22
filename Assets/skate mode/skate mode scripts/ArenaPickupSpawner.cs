using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaPickupSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PickupOption
    {
        public string name;
        public GameObject prefab;

        [Tooltip("Higher value = more common.")]
        public int weight = 1;
    }


    [Header("Possible Pickups")]
    public PickupOption[] pickups;


    [Header("Spawn Points")]
    public Transform[] spawnPoints;


    [Header("Spawn Settings")]
    public int initialPickupCount = 4;
    public int maxActivePickups = 5;

    public float minSpawnDelay = 4f;
    public float maxSpawnDelay = 8f;


    bool[] occupied;
    int activePickups;


    void Start()
    {
        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogError(
                "ArenaPickupSpawner has no spawn points!"
            );

            return;
        }


        occupied =
            new bool[spawnPoints.Length];


        int count =
            Mathf.Min(
                initialPickupCount,
                spawnPoints.Length
            );


        for (int i = 0; i < count; i++)
            SpawnPickup();


        StartCoroutine(
            SpawnLoop()
        );
    }


    IEnumerator SpawnLoop()
    {
        while (true)
        {
            float delay =
                Random.Range(
                    minSpawnDelay,
                    maxSpawnDelay
                );


            yield return
                new WaitForSeconds(delay);


            if (GameModeManager.Instance != null &&
                !GameModeManager.Instance.MatchRunning)
                continue;


            if (activePickups <
                maxActivePickups)
            {
                SpawnPickup();
            }
        }
    }


    void SpawnPickup()
    {
        if (pickups == null ||
            pickups.Length == 0)
            return;


        List<int> freePoints =
            new List<int>();


        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            if (!occupied[i] &&
                spawnPoints[i] != null)
            {
                freePoints.Add(i);
            }
        }


        if (freePoints.Count == 0)
            return;


        int pointIndex =
            freePoints[
                Random.Range(
                    0,
                    freePoints.Count
                )
            ];


        GameObject prefab =
            GetRandomPickup();


        if (prefab == null)
            return;


        Transform point =
            spawnPoints[pointIndex];


        GameObject pickup =
            Instantiate(
                prefab,
                point.position,
                point.rotation
            );


        occupied[pointIndex] = true;
        activePickups++;


        PickupSpawnTracker tracker =
            pickup.GetComponent<PickupSpawnTracker>();


        if (tracker == null)
        {
            tracker =
                pickup.AddComponent<PickupSpawnTracker>();
        }


        tracker.Initialize(
            this,
            pointIndex
        );
    }


    GameObject GetRandomPickup()
    {
        int totalWeight = 0;


        foreach (PickupOption option
                 in pickups)
        {
            if (option.prefab != null &&
                option.weight > 0)
            {
                totalWeight +=
                    option.weight;
            }
        }


        if (totalWeight <= 0)
            return null;


        int random =
            Random.Range(
                0,
                totalWeight
            );


        foreach (PickupOption option
                 in pickups)
        {
            if (option.prefab == null ||
                option.weight <= 0)
                continue;


            if (random <
                option.weight)
            {
                return option.prefab;
            }


            random -=
                option.weight;
        }


        return null;
    }


    public void NotifyPickupRemoved(
        int pointIndex)
    {
        if (occupied == null)
            return;


        if (pointIndex >= 0 &&
            pointIndex < occupied.Length)
        {
            occupied[pointIndex] =
                false;
        }


        activePickups =
            Mathf.Max(
                activePickups - 1,
                0
            );
    }
}