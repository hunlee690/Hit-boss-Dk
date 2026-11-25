using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class SkateSlopeLean : MonoBehaviour
    {
        [Range(0f, 35f)] public float maximumLean = 24f;
        [Min(1f)] public float smoothing = 10f;

        PlayerController skater;
        SkateCombat combat;
        RagdollController ragdoll;
        Transform model;
        Quaternion baseRotation, lean = Quaternion.identity;

        void Awake()
        {
            skater = GetComponent<PlayerController>();
            combat = GetComponentInParent<SkateCombat>();
            ragdoll = GetComponent<RagdollController>();
            model = skater.visualModel;
            if (model == null) { enabled = false; return; }
            baseRotation = model.localRotation;
        }

        void LateUpdate()
        {
            if (ragdoll != null && ragdoll.IsRagdolled) return;
            Quaternion wanted = Quaternion.identity;
            if (skater.IsSkateMode && !skater.IsJumping &&
                (combat == null || (!combat.Downed.Value && !combat.HasFinishedRace)) &&
                skater.SampleSkateSurface(out var normal))
            {
                Vector3 limitedUp = Vector3.RotateTowards(Vector3.up, normal, maximumLean * Mathf.Deg2Rad, 0f);
                wanted = Quaternion.FromToRotation(Vector3.up, limitedUp);
            }
            lean = Quaternion.Slerp(lean, wanted, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Quaternion worldBase = model.parent != null ? model.parent.rotation * baseRotation : baseRotation;
            model.rotation = lean * worldBase;
        }

        void OnDisable()
        {
            if (model != null && (ragdoll == null || !ragdoll.IsRagdolled)) model.localRotation = baseRotation;
        }
    }
}
