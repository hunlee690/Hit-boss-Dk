using UnityEngine;

public class ThrowableInventory : MonoBehaviour
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


    public ThrowableType CurrentItem { get; private set; } =
        ThrowableType.None;

    public int CurrentCount { get; private set; }


    bool usingItem;


    void Awake()
    {
        if (playerController == null)
            playerController =
                GetComponent<PlayerController>();

        if (ownerStats == null)
            ownerStats =
                GetComponent<CombatStats>();

        if (ownerStats == null)
            ownerStats =
                GetComponentInParent<CombatStats>();


        if (throwDirectionSource == null)
            throwDirectionSource = transform;
    }


    // =====================================================
    // PICKUP
    // =====================================================

    public bool TryPickup(
        ThrowableType type)
    {
        if (type == ThrowableType.None)
            return false;


        // One inventory type at a time.
        if (CurrentItem != ThrowableType.None)
            return false;


        CurrentItem = type;


        switch (type)
        {
            case ThrowableType.Bomb:
                CurrentCount = 1;
                break;

            case ThrowableType.Mine:
                CurrentCount =
                    minesPerPickup;
                break;
        }


        Debug.Log(
            gameObject.name +
            " picked up " +
            type +
            " x" +
            CurrentCount
        );


        return true;
    }


    // =====================================================
    // USE
    // =====================================================

    public bool TryUseThrowable()
    {
        if (CurrentItem == ThrowableType.None ||
            CurrentCount <= 0 ||
            usingItem)
            return false;


        if (ownerStats != null &&
            ownerStats.IsDead)
            return false;


        usingItem = true;


        if (playerController != null)
        {
            playerController.PlayCombatAnimation(
                "throw"
            );
        }
        else
        {
            // Useful for testing if no animation/controller.
            ReleaseThrowable();
        }


        return true;
    }


    // =====================================================
    // ANIMATION EVENT
    // =====================================================

    public void ReleaseThrowable()
    {
        if (!usingItem ||
            CurrentItem == ThrowableType.None)
            return;


        switch (CurrentItem)
        {
            case ThrowableType.Bomb:
                ThrowBomb();
                ConsumeOne();
                break;


            case ThrowableType.Mine:
                PlaceMine();
                ConsumeOne();
                break;
        }


        usingItem = false;
    }


    // =====================================================
    // BOMB
    // =====================================================

    void ThrowBomb()
    {
        if (bombPrefab == null ||
            throwPoint == null)
            return;


        Vector3 forward =
            throwDirectionSource.forward;

        forward.y = 0f;
        forward.Normalize();


        GameObject bomb =
            Instantiate(
                bombPrefab,
                throwPoint.position,
                Quaternion.LookRotation(forward)
            );


        Bomb bombScript =
            bomb.GetComponent<Bomb>();


        if (bombScript != null)
        {
            bombScript.SetOwner(
                ownerStats
            );
        }


        Rigidbody bombRb =
            bomb.GetComponent<Rigidbody>();


        if (bombRb != null)
        {
            bombRb.linearVelocity =
                forward *
                bombForwardSpeed +
                Vector3.up *
                bombUpwardSpeed;
        }
    }


    // =====================================================
    // MINE
    // =====================================================

    void PlaceMine()
    {
        if (minePrefab == null)
            return;


        // Put the mine BEHIND the player,
        // useful while running/skating.
        Vector3 position =
            transform.position -
            transform.forward *
            minePlaceDistance;


        Vector3 rayStart =
            position +
            Vector3.up *
            mineRayHeight;


        if (Physics.Raycast(
            rayStart,
            Vector3.down,
            out RaycastHit hit,
            mineRayHeight * 2f + 2f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            position =
                hit.point;
        }


        GameObject mine =
            Instantiate(
                minePrefab,
                position,
                Quaternion.identity
            );


        Mine mineScript =
            mine.GetComponent<Mine>();


        if (mineScript != null)
        {
            mineScript.SetOwner(
                ownerStats
            );
        }
    }


    // =====================================================
    // INVENTORY
    // =====================================================

    void ConsumeOne()
    {
        CurrentCount--;


        if (CurrentCount <= 0)
        {
            CurrentCount = 0;
            CurrentItem =
                ThrowableType.None;
        }
    }
}