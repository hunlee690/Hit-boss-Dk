using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public enum SkateItemKind { Stamina, BombPickup, MinePickup, Bomb, Mine }
    public sealed class SkateNetworkItem : NetworkBehaviour
    {
        public SkateItemKind kind;
        public int damage = 3, restoreAmount = 1;
        public float radius = 3, force = 8, lift = 1.5f, downTime = 2.5f;
        public GameObject explosionEffect;
        SkateCombat attacker;
        double born, explodeAt = double.MaxValue;
        bool consumed;
        public void SetAttacker(SkateCombat value) => attacker = value;
        public override void OnNetworkSpawn()
        {
            born = NetworkManager.ServerTime.Time;
            var body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = !IsServer || kind != SkateItemKind.Bomb;
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = IsServer;
                if (attacker != null) foreach (var own in attacker.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, own);
            }
        }
        void Update()
        {
            if (!IsSpawned || !IsServer || consumed) return;
            double age = NetworkManager.ServerTime.Time - born;
            if (!(MatchConnection.Instance.ActiveRules is SkateMatchRules rules) || !rules.Running) return;
            if (kind == SkateItemKind.Bomb)
            {
                if (age >= 4 || NetworkManager.ServerTime.Time >= explodeAt) Explode();
                return;
            }
            if (kind == SkateItemKind.Mine && age < .75) return;
            foreach (var player in SkateCombat.Players.ToArray())
            {
                if (player == null || !player.Active || player.Downed.Value || Vector3.Distance(player.Position, transform.position) > 1.25f) continue;
                if (kind == SkateItemKind.Mine)
                {
                    if (player != attacker) { Explode(); return; }
                }
                else if (player.CollectOnServer(kind, restoreAmount)) { consumed = true; NetworkObject.Despawn(); return; }
            }
            if (age > (kind == SkateItemKind.Mine ? 15 : 40)) { consumed = true; NetworkObject.Despawn(); }
        }
        void OnCollisionEnter(Collision collision)
        {
            if (IsServer && kind == SkateItemKind.Bomb && explodeAt == double.MaxValue && collision.collider.GetComponentInParent<SkateCombat>() != attacker)
                explodeAt = NetworkManager.ServerTime.Time + .1;
        }
        void Explode()
        {
            if (consumed) return;
            consumed = true;
            foreach (var player in SkateCombat.Players.ToArray())
                if (player != null && player != attacker && Vector3.Distance(player.Position, transform.position) <= radius && SkateCombat.ClearPath(transform.position, player.Position))
                    player.DamageOnServer(damage, attacker, transform.position, force, lift, downTime);
            EffectRpc(transform.position); NetworkObject.Despawn();
        }
        [Rpc(SendTo.Everyone)]
        void EffectRpc(Vector3 position)
        {
            if (explosionEffect != null) Destroy(Instantiate(explosionEffect, position, Quaternion.identity), 4);
        }
    }
}
