using Unity.Netcode;
using UnityEngine;

public class ThrowableInventory : NetworkBehaviour
{
    public enum ThrowableType
    {
        None,
        Bomb,
        Mine
    }

    [Header("References")]
    public PlayerController playerController;
    public CombatStats ownerStats;

    [Header("Prefabs")]
    public GameObject bombPrefab;
    public GameObject minePrefab;

    [Header("Bomb Throw")]
    public Transform throwPoint;
    [Tooltip("Use the player ROOT here, not the rotated visual model.")]
    public Transform throwDirectionSource;
    public float bombForwardSpeed = 12f;
    public float bombUpwardSpeed = 4f;

    [Header("Mine")]
    public int minesPerPickup = 3;
    [Tooltip("Mine is placed slightly behind the player.")]
    public float minePlaceDistance = 0.6f;
    public float mineRayHeight = 2f;
    public LayerMask groundLayers;

    public ThrowableType CurrentItem { get; private set; } = ThrowableType.None;
    public int CurrentCount { get; private set; }

    bool usingItem;

    void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (ownerStats == null)
            ownerStats = GetComponent<CombatStats>();

        if (ownerStats == null)
            ownerStats = GetComponentInParent<CombatStats>();

        if (throwDirectionSource == null)
            throwDirectionSource = transform;
    }

    public bool TryPickup(ThrowableType type)
    {
        if (type == ThrowableType.None)
            return false;

        if (IsSpawned && !IsOwner)
            return false;

        if (CurrentItem != ThrowableType.None)
            return false;

        CurrentItem = type;
        CurrentCount = type == ThrowableType.Bomb ? 1 : minesPerPickup;
        return true;
    }

    public bool TryUseThrowable()
    {
        if (IsSpawned && !IsOwner)
            return false;

        if (CurrentItem == ThrowableType.None || CurrentCount <= 0 || usingItem)
            return false;

        if (ownerStats != null && ownerStats.IsDead)
            return false;

        usingItem = true;

        if (playerController != null)
            playerController.PlayCombatAnimation("throw");
        else
            ReleaseThrowable();

        return true;
    }

    public void ReleaseThrowable()
    {
        if (!usingItem || CurrentItem == ThrowableType.None)
            return;

        ThrowableType type = CurrentItem;

        if (IsSpawned)
        {
            if (IsOwner)
                SpawnThrowableServerRpc((int)type);
        }
        else
        {
            SpawnThrowable(type, false);
        }

        ConsumeOne();
        usingItem = false;
    }

    [ServerRpc]
    void SpawnThrowableServerRpc(int rawType)
    {
        ThrowableType type = (ThrowableType)rawType;

        if (type != ThrowableType.Bomb && type != ThrowableType.Mine)
            return;

        SpawnThrowable(type, true);
    }

    void SpawnThrowable(ThrowableType type, bool networked)
    {
        if (type == ThrowableType.Bomb)
            SpawnBomb(networked);
        else if (type == ThrowableType.Mine)
            SpawnMine(networked);
    }

    void SpawnBomb(bool networked)
    {
        if (bombPrefab == null || throwPoint == null)
            return;

        Vector3 forward = throwDirectionSource != null
            ? throwDirectionSource.forward
            : transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        forward.Normalize();

        GameObject bomb = Instantiate(
            bombPrefab,
            throwPoint.position,
            Quaternion.LookRotation(forward)
        );

        Bomb bombScript = bomb.GetComponent<Bomb>();
        if (bombScript != null)
            bombScript.SetOwner(ownerStats);

        NetworkObject networkObject = bomb.GetComponent<NetworkObject>();

        if (networked)
        {
            if (networkObject == null)
            {
                Debug.LogError("Bomb prefab needs a NetworkObject for multiplayer.");
                Destroy(bomb);
                return;
            }

            networkObject.Spawn(true);
        }

        Rigidbody bombRb = bomb.GetComponent<Rigidbody>();

        if (bombRb != null)
            bombRb.linearVelocity =
                forward * bombForwardSpeed +
                Vector3.up * bombUpwardSpeed;
    }

    void SpawnMine(bool networked)
    {
        if (minePrefab == null)
            return;

        Vector3 position =
            transform.position -
            transform.forward * minePlaceDistance;

        Vector3 rayStart =
            position +
            Vector3.up * mineRayHeight;

        if (Physics.Raycast(
            rayStart,
            Vector3.down,
            out RaycastHit hit,
            mineRayHeight * 2f + 2f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
        }

        GameObject mine = Instantiate(
            minePrefab,
            position,
            Quaternion.identity
        );

        Mine mineScript = mine.GetComponent<Mine>();
        if (mineScript != null)
            mineScript.SetOwner(ownerStats);

        if (!networked)
            return;

        NetworkObject networkObject = mine.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("Mine prefab needs a NetworkObject for multiplayer.");
            Destroy(mine);
            return;
        }

        networkObject.Spawn(true);
    }

    void ConsumeOne()
    {
        CurrentCount--;

        if (CurrentCount <= 0)
        {
            CurrentCount = 0;
            CurrentItem = ThrowableType.None;
        }
    }
}
