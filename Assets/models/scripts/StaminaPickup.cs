using UnityEngine;

public class StaminaPickup : MonoBehaviour
{
    public int restoreAmount = 1;

    bool collected;


    void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;


        CombatStats stats =
            other.GetComponentInParent<CombatStats>();


        if (stats == null ||
            stats.IsDead)
            return;


        if (stats.Stamina >=
            stats.maxStamina)
            return;


        collected = true;


        stats.AddStamina(
            restoreAmount
        );


        Destroy(gameObject);
    }
}