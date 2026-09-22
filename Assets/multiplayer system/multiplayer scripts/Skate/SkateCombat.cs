using System.Collections.Generic;
using System.Linq;
using HitBoss.Social;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitBoss.Multiplayer.Skate
{
    // Clients send intentions only. The host owns damage, inventory, cooldowns and scores.
    public sealed class SkateCombat : NetworkBehaviour
    {
        public static readonly List<SkateCombat> Players = new List<SkateCombat>();
        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(3);
        public readonly NetworkVariable<int> Stamina = new NetworkVariable<int>(3);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Item = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ItemCount = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> Downed = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> Finished = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> InputPaused = new NetworkVariable<bool>();
        public readonly NetworkVariable<double> EndsAt = new NetworkVariable<double>();
        public NetworkPlayer Player { get; private set; }
        public bool Active { get; private set; }
        public bool CanAct => Active && Health.Value > 0 && !Downed.Value && !Finished.Value && !InputPaused.Value;
        public Vector3 Position => Player.movement.transform.position;
        public int MaxHealth => stats.maxHealth;
        public int MaxStamina => stats.maxStamina;
        CombatStats stats;
        CombatController tuning;
        RagdollController ragdoll;
        Rigidbody body;
        double nextAttack, recoverAt, nextStamina, invulnerableUntil;
        byte pendingAttack;
        double hitAt;
        bool hitPending;
        Vector3 recoveryPosition;
        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Player = GetComponent<NetworkPlayer>();
            Active = MatchConnection.Instance?.ActiveRules is SkateMatchRules;
            if (!Active) { enabled = false; return; }
            stats = Player.movement.GetComponent<CombatStats>();
            tuning = Player.movement.GetComponent<CombatController>();
            ragdoll = Player.movement.GetComponent<RagdollController>();
            body = Player.movement.GetComponent<Rigidbody>();
            Players.Add(this);
            if (IsServer) { Health.Value = MaxHealth; Stamina.Value = MaxStamina; nextStamina = Now + 3; invulnerableUntil = Now + 1; }
            Downed.OnValueChanged += StateChanged; Finished.OnValueChanged += StateChanged; InputPaused.OnValueChanged += StateChanged;
            Kills.OnValueChanged += KillsChanged; Deaths.OnValueChanged += DeathsChanged;
            ApplyState();
        }
        public override void OnNetworkDespawn()
        {
            Players.Remove(this); Active = false;
            Downed.OnValueChanged -= StateChanged; Finished.OnValueChanged -= StateChanged; InputPaused.OnValueChanged -= StateChanged;
            Kills.OnValueChanged -= KillsChanged; Deaths.OnValueChanged -= DeathsChanged;
        }
        void KillsChanged(int oldValue, int value)
        {
            Player.participant.kills = value;
            if (IsOwner && !Player.IsBot.Value && value > oldValue) OnlineManager.Instance?.Profiles?.RecordProgress(value - oldValue, 0, (value - oldValue) * (OnlineManager.Instance?.xpPerKill ?? 25));
        }
        void DeathsChanged(int oldValue, int value)
        {
            Player.participant.deaths = value;
            if (IsOwner && !Player.IsBot.Value && value > oldValue) OnlineManager.Instance?.Profiles?.RecordProgress(0, value - oldValue, 0);
        }
        void StateChanged(bool before, bool after) => ApplyState();
        void ApplyState()
        {
            if (!Active || Player == null) return;
            if (Downed.Value && !ragdoll.IsRagdolled)
            {
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                ragdoll.EnterRagdoll(999, true);
            }
            else if (!Downed.Value && ragdoll.IsRagdolled)
            {
                ragdoll.PrepareForRespawn();
                body.isKinematic = !(IsOwner || Player.IsBot.Value && IsServer);
                Player.movement.GetComponent<CapsuleCollider>().enabled = IsOwner || IsServer;
                Player.movement.SetSkateMode(Player.Skating.Value);
            }
            if (!CanAct && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            Player.RefreshInput();
            if (Finished.Value && IsOwner && !Player.IsBot.Value) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void Update()
        {
            if (!Active || !IsSpawned) return;
            if (IsOwner && !Player.IsBot.Value && !Player.IsPaused && CanAct && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) RequestAttackRpc(0);
                if (Mouse.current.rightButton.wasPressedThisFrame) RequestAttackRpc(1);
                if (Mouse.current.middleButton.wasPressedThisFrame) RequestAttackRpc(2);
            }
            if (!IsServer || Finished.Value) return;
            if (hitPending && Now >= hitAt)
            {
                hitPending = false;
                if (CanAct) { if (pendingAttack == 2) ThrowOnServer(); else MeleeOnServer(pendingAttack); }
            }
            if (Downed.Value && Now >= recoverAt) RecoverOnServer();
            if (!Downed.Value && Now >= nextStamina) { Stamina.Value = Mathf.Min(MaxStamina, Stamina.Value + 1); nextStamina = Now + 3; }
            if (!Downed.Value && Position.y < -25) DamageOnServer(MaxHealth, null, Position, 0, 0, 2);
        }
        public void SetPaused(bool value)
        {
            if (Active && IsSpawned && IsOwner && !Player.IsBot.Value) PauseRpc(value);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void PauseRpc(bool value) { InputPaused.Value = value; }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAttackRpc(byte action) { if (!Player.IsBot.Value) AttackOnServer(action); }
        public bool AttackOnServer(byte action)
        {
            if (!IsServer || !CanAct || EndsAt.Value <= Now || Now < nextAttack || action > 2) return false;
            int cost = action == 0 ? tuning.slideStaminaCost : action == 1 ? tuning.punchStaminaCost : 0;
            if (Stamina.Value < cost || action == 2 && ItemCount.Value <= 0) return false;
            Stamina.Value -= cost; nextStamina = Now + 3;
            nextAttack = Now + Mathf.Max(.8f, Player.movement.attackDuration);
            pendingAttack = action; hitAt = Now + .2; hitPending = true;
            AnimateRpc(action);
            return true;
        }
        [Rpc(SendTo.Everyone)]
        void AnimateRpc(byte action) { Player.movement.PlayCombatAnimation(action == 0 ? "slide" : action == 1 ? "punch" : "throw"); }
        void MeleeOnServer(byte action)
        {
            float range = action == 0 ? 2.6f : 2.2f;
            foreach (var other in Players.ToArray())
            {
                if (other == this || !other.Active) continue;
                var delta = other.Position - Position;
                if (delta.magnitude > range || Vector3.Dot(Player.movement.transform.forward, delta.normalized) < .15f || !ClearPath(Position, other.Position)) continue;
                other.DamageOnServer(action == 0 ? tuning.slideDamage : tuning.punchDamage, this, Position,
                    action == 0 ? tuning.slideForce : tuning.punchForce, action == 0 ? tuning.slideUpwardForce : tuning.punchUpwardForce,
                    action == 0 ? tuning.slideRagdollTime : tuning.punchRagdollTime);
            }
        }
        public static bool ClearPath(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            foreach (var hit in Physics.RaycastAll(from + Vector3.up * .6f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<NetworkPlayer>() == null && hit.collider.GetComponentInParent<SkateNetworkItem>() == null) return false;
            return true;
        }
        public void DamageOnServer(int damage, SkateCombat attacker, Vector3 origin, float force, float lift, float duration)
        {
            if (!IsServer || !Active || Finished.Value || Downed.Value || Health.Value <= 0 || Now < invulnerableUntil || damage <= 0) return;
            Health.Value = Mathf.Max(0, Health.Value - damage);
            recoveryPosition = Position; recoverAt = Now + Mathf.Max(duration, 2);
            hitPending = false;
            if (Health.Value == 0)
            {
                Deaths.Value++;
                if (attacker != null && attacker != this && attacker.IsSpawned) attacker.Kills.Value++;
                Item.Value = 0; ItemCount.Value = 0;
                recoverAt = Now + ((SkateMatchRules)MatchConnection.Instance.ActiveRules).respawnDelay;
            }
            Downed.Value = true;
            KnockbackRpc(origin, force, lift);
        }
        [Rpc(SendTo.Everyone)]
        void KnockbackRpc(Vector3 origin, float force, float lift)
        {
            // Ragdoll bone motion is presentation; health and recovery remain host-owned.
            if (!ragdoll.IsRagdolled) ragdoll.EnterRagdoll(999, true);
            ragdoll.ApplyForceFromPoint(origin, force, lift);
        }
        void RecoverOnServer()
        {
            var rules = (SkateMatchRules)MatchConnection.Instance.ActiveRules;
            var pose = Health.Value == 0 ? rules.SpawnPose(Random.Range(0, 100)) : new Pose(recoveryPosition, Player.movement.transform.rotation);
            if (Health.Value == 0) { Health.Value = MaxHealth; Stamina.Value = MaxStamina; }
            Downed.Value = false; invulnerableUntil = Now + 1;
            TeleportRpc(pose.position, pose.rotation);
        }
        [Rpc(SendTo.Everyone)]
        void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            if (ragdoll.IsRagdolled) ragdoll.PrepareForRespawn();
            body.isKinematic = !(IsOwner || Player.IsBot.Value && IsServer);
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.position = position; body.rotation = rotation;
            Player.movement.transform.SetPositionAndRotation(position, rotation);
            if (IsOwner) Player.movement.GetComponent<OwnerNetworkTransform>().Teleport(position, rotation, Player.movement.transform.localScale);
            Player.RefreshInput();
        }
        public bool CollectOnServer(SkateItemKind kind, int amount)
        {
            if (!IsServer || !CanAct) return false;
            if (kind == SkateItemKind.Stamina)
            {
                if (Stamina.Value >= MaxStamina) return false;
                Stamina.Value = Mathf.Min(MaxStamina, Stamina.Value + amount); return true;
            }
            if (ItemCount.Value > 0) return false;
            Item.Value = kind == SkateItemKind.BombPickup ? 1 : 2;
            ItemCount.Value = Item.Value == 1 ? 1 : 3; return true;
        }
        void ThrowOnServer()
        {
            if (ItemCount.Value <= 0) return;
            var rules = (SkateMatchRules)MatchConnection.Instance.ActiveRules;
            rules.SpawnProjectile(this, Item.Value == 1);
            if (--ItemCount.Value <= 0) Item.Value = 0;
        }
        public bool BotTarget(out Vector3 target)
        {
            target = Position;
            if (!CanAct) return false;
            var enemy = Players.Where(p => p != this && p.Active && !p.Downed.Value && p.Health.Value > 0).OrderBy(p => (p.Position - Position).sqrMagnitude).FirstOrDefault();
            if (enemy == null) return false;
            target = enemy.Position;
            float distance = Vector3.Distance(target, Position);
            if (distance < 2) AttackOnServer(Stamina.Value >= tuning.punchStaminaCost ? (byte)1 : (byte)0);
            else if (distance < 9 && ItemCount.Value > 0) AttackOnServer(2);
            return true;
        }
    }
}
