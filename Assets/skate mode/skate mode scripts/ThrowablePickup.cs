using UnityEngine;

public class ThrowablePickup : MonoBehaviour
{
    public ThrowableInventory.ThrowableType type =
        ThrowableInventory.ThrowableType.Bomb;


    void OnTriggerEnter(Collider other)
    {
        ThrowableInventory inventory =
            other.GetComponentInParent<ThrowableInventory>();


        if (inventory == null)
            return;


        if (inventory.TryPickup(type))
        {
            Destroy(gameObject);
        }
    }
}