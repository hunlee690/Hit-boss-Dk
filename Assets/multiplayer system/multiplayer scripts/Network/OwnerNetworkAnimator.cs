using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    public sealed class OwnerNetworkAnimator : NetworkAnimator
    {
        readonly NetworkVariable<float> attackWeight = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<float> throwWeight = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        int attackLayer = -1;
        int throwLayer = -1;
        Coroutine syncRoutine;

        protected override bool OnIsServerAuthoritative() => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (Animator != null)
            {
                attackLayer = Animator.GetLayerIndex("Attack Layer");
                throwLayer = Animator.GetLayerIndex("throw Layer");
            }

            syncRoutine = StartCoroutine(SyncLayerWeights());
        }

        IEnumerator SyncLayerWeights()
        {
            while (IsSpawned)
            {
                if (Animator != null)
                {
                    if (IsOwner)
                    {
                        SyncOwnerWeight(attackLayer, attackWeight);
                        SyncOwnerWeight(throwLayer, throwWeight);
                    }
                    else
                    {
                        if (attackLayer >= 0)
                            Animator.SetLayerWeight(attackLayer, attackWeight.Value);

                        if (throwLayer >= 0)
                            Animator.SetLayerWeight(throwLayer, throwWeight.Value);
                    }
                }

                yield return null;
            }
        }

        void SyncOwnerWeight(int layer, NetworkVariable<float> value)
        {
            if (layer < 0) return;

            float weight = Animator.GetLayerWeight(layer);

            if (Mathf.Abs(weight - value.Value) > 0.001f)
                value.Value = weight;
        }

        public override void OnNetworkDespawn()
        {
            if (syncRoutine != null)
                StopCoroutine(syncRoutine);

            syncRoutine = null;
            base.OnNetworkDespawn();
        }
    }
}
