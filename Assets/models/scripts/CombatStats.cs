using UnityEngine;
using System;

public class CombatStats : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;

    [Header("Stamina")]
    public int maxStamina = 3;


    public int Health { get; private set; }
    public int Stamina { get; private set; }

    public bool IsDead { get; private set; }


    public event Action<int, int> OnHealthChanged;
    public event Action<int, int> OnStaminaChanged;


    RagdollController ragdoll;


    void Awake()
    {
        ragdoll =
            GetComponentInParent<RagdollController>();


        ResetStats();
    }


    // =====================================================
    // RESET
    // =====================================================

    public void ResetStats()
    {
        Health = maxHealth;
        Stamina = maxStamina;

        IsDead = false;


        OnHealthChanged?.Invoke(
            Health,
            maxHealth
        );


        OnStaminaChanged?.Invoke(
            Stamina,
            maxStamina
        );
    }


    // =====================================================
    // STAMINA
    // =====================================================

    public bool HasStamina(
        int amount)
    {
        return
            !IsDead &&
            Stamina >= amount;
    }


    public bool UseStamina(
        int amount)
    {
        if (!HasStamina(amount))
            return false;


        Stamina -= amount;


        OnStaminaChanged?.Invoke(
            Stamina,
            maxStamina
        );


        return true;
    }


    public void AddStamina(
        int amount)
    {
        if (IsDead)
            return;


        Stamina =
            Mathf.Clamp(
                Stamina + amount,
                0,
                maxStamina
            );


        OnStaminaChanged?.Invoke(
            Stamina,
            maxStamina
        );
    }


    // =====================================================
    // DAMAGE
    // =====================================================

    public void TakeDamage(
        int amount,
        CombatStats attacker)
    {
        if (IsDead)
            return;


        // Fallen characters cannot be repeatedly damaged
        // until they get back up.
        if (ragdoll != null &&
            ragdoll.IsRagdolled)
        {
            return;
        }


        Health -= amount;


        Health =
            Mathf.Max(
                Health,
                0
            );


        OnHealthChanged?.Invoke(
            Health,
            maxHealth
        );


        if (Health <= 0)
        {
            Die(attacker);
        }
    }


    // =====================================================
    // DEATH
    // =====================================================

    void Die(
        CombatStats attacker)
    {
        if (IsDead)
            return;


        IsDead = true;


        if (ragdoll == null)
        {
            ragdoll =
                GetComponentInParent<RagdollController>();
        }


        if (ragdoll != null)
        {
            // Permanent until MatchParticipant respawns us.
            // No artificial force is added.
            ragdoll.EnterRagdoll(
                999f,
                true
            );
        }


        if (GameModeManager.Instance != null)
        {
            GameModeManager.Instance.HandleDeath(
                this,
                attacker
            );
        }
        else
        {
            Debug.LogWarning(
                "GameModeManager not found."
            );
        }
    }
}
