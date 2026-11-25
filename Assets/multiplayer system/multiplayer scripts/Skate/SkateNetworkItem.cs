using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public enum SkateItemKind { MysteryBox = 1, Bomb = 3, Mine = 4 }
    public sealed class SkateNetworkItem : NetworkBehaviour
    {
        public SkateItemKind kind;
        public int damage = 3;
        public float radius = 3, force = 8, lift = 1.5f, downTime = 2.5f;
        public GameObject explosionEffect;
        public GameObject visualPrefab;
        public static readonly System.Collections.Generic.List<SkateNetworkItem> ActiveItems = new System.Collections.Generic.List<SkateNetworkItem>();
        MysteryBoxReward reward;
        SkateMatchRules pickupSource; int pickupSlot = -1;
        double nextScan;
        public bool ArmedMine => kind == SkateItemKind.Mine && IsSpawned && NetworkManager.ServerTime.Time - born >= .75;
        public void SetPickupSource(SkateMatchRules source, int slot) { pickupSource = source; pickupSlot = slot; }
        public bool IsPickup => kind != SkateItemKind.Bomb && kind != SkateItemKind.Mine;
        SkateCombat attacker;
        double born, explodeAt = double.MaxValue;
        bool consumed;
        public void SetAttacker(SkateCombat value) => attacker = value;
        public void SetReward(MysteryBoxReward value) => reward = value;
        public override void OnNetworkSpawn()
        {
            born = NetworkManager.ServerTime.Time;
            ActiveItems.Add(this);
            EnsureVisualPrefab();
            if (IsPickup)
                (GetComponent<SkatePickupPresentation>() ?? gameObject.AddComponent<SkatePickupPresentation>()).Configure();
            var body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = !IsServer || kind != SkateItemKind.Bomb;
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = IsServer;
                if (IsPickup || kind == SkateItemKind.Mine) col.isTrigger = true;
                if (attacker != null) foreach (var own in attacker.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, own);
            }
        }
        public override void OnNetworkDespawn() { ActiveItems.Remove(this); }
        public static SkateCombat NearestEligible(Vector3 position, MysteryBoxReward prize, float reach)
        {
            SkateCombat nearest = null; float best = reach * reach;
            foreach (var player in SkateCombat.Players)
            {
                if (player == null || !player.IsSpawned || !player.CanCollect(prize)) continue;
                float distance = (player.Position - position).sqrMagnitude;
                if (distance > best || distance == best && nearest != null && player.NetworkObjectId >= nearest.NetworkObjectId) continue;
                nearest = player; best = distance;
            }
            return nearest;
        }
        void EnsureVisualPrefab()
        {
            if (visualPrefab == null || transform.Find("Mine Visual") != null) return;
            var visual = Instantiate(visualPrefab, transform, false);
            visual.name = "Mine Visual";
            visual.transform.localPosition = new Vector3(0, .15f, 0);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }
        void Update()
        {
            if (!IsSpawned || !IsServer || consumed) return;
            double age = NetworkManager.ServerTime.Time - born;
            if (!(MatchConnection.Instance?.ActiveRules is SkateMatchRules rules) || !rules.Running) return;
            if (kind == SkateItemKind.Bomb)
            {
                if (age >= 4 || NetworkManager.ServerTime.Time >= explodeAt) Explode();
                return;
            }
            if (NetworkManager.ServerTime.Time < nextScan) return;
            nextScan = NetworkManager.ServerTime.Time + .06;
            if (IsPickup)
            {
                var player = NearestEligible(transform.position, reward, 2f);
                if (player != null && player.CollectOnServer(reward))
                {
                    consumed = true;
                    EffectRpc(transform.position+Vector3.up*.9f);
                    pickupSource?.PickupCollected(pickupSlot, this); NetworkObject.Despawn();
                }
                return;
            }
            if (age < .75) return;
            foreach (var player in SkateCombat.Players)
                if (player != null && player != attacker && player.CanAct && (player.Position - transform.position).sqrMagnitude <= 1.5625f)
                { Explode(); return; }
            if (age > 15) { consumed = true; NetworkObject.Despawn(); }
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
            float blastRadius = kind == SkateItemKind.Bomb ? radius * 1.5f : radius;
            foreach (var player in SkateCombat.Players.ToArray())
                if (player != null && player != attacker && Vector3.Distance(player.Position, transform.position) <= blastRadius && SkateCombat.ClearPath(transform.position, player.Position))
                    player.DamageOnServer(damage, attacker, transform.position, force, lift, downTime);
            EffectRpc(transform.position); NetworkObject.Despawn();
        }
        [Rpc(SendTo.Everyone)]
        void EffectRpc(Vector3 position)
        {
            if(IsPickup){SkateCombatEffects.SpawnPickupPop(position);return;}
            SkateCombatEffects.SpawnExplosion(position, kind == SkateItemKind.Mine);
            if (explosionEffect != null) Destroy(Instantiate(explosionEffect, position, Quaternion.identity), 4);
        }
    }
}
