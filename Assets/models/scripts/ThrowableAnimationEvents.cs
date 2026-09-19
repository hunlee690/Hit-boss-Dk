using UnityEngine;

public class ThrowableAnimationEvents : MonoBehaviour
{
    ThrowableInventory inventory;


    void Awake()
    {
        inventory =
            GetComponentInParent<ThrowableInventory>();
    }


    public void ReleaseThrowable()
    {
        if (inventory != null)
        {
            inventory.ReleaseThrowable();
        }
    }
}