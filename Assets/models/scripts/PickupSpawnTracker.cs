using UnityEngine;

public class PickupSpawnTracker : MonoBehaviour
{
    ArenaPickupSpawner spawner;

    int spawnIndex = -1;

    bool notified;


    public void Initialize(
        ArenaPickupSpawner owner,
        int index)
    {
        spawner = owner;
        spawnIndex = index;
    }


    void OnDestroy()
    {
        if (notified ||
            spawner == null)
            return;


        notified = true;


        spawner.NotifyPickupRemoved(
            spawnIndex
        );
    }
}