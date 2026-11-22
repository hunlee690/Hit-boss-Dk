using System.Collections.Generic;
using System.Linq;
using HitBoss.Social;
using HitBoss.Currency;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitBoss.Multiplayer.Skate
{
    // Clients send intentions only. The host owns damage, inventory, cooldowns and scores.
    public sealed class SkateCombat : NetworkBehaviour
    {
        const float FullThrowChargeSeconds = 1.2f;
        const float BoostSpeedBonus = 10f;
        const float SlideSpeedBonus = 11f;
        const float BoostPickupSeconds = 6f;
        public const float MaxBoostReserve = 12f;
        public static readonly List<SkateCombat> Players = new List<SkateCombat>();
        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(3);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>();
        public readonly NetworkVariable<int> CoinsEarned = new NetworkVariable<int>();
        public readonly NetworkVariable<double> FinishSeconds = new NetworkVariable<double>();
        public readonly NetworkVariable<double> AttackReadyAt = new NetworkVariable<double>();
        public readonly NetworkVariable<float> AttackCooldownSeconds = new NetworkVariable<float>(.9f);
        public readonly NetworkVariable<double> SlideReadyAt = new NetworkVariable<double>();
        public readonly NetworkVariable<double> PunchReadyAt = new NetworkVariable<double>();
        public readonly NetworkVariable<double> ProtectedUntil = new NetworkVariable<double>();
        public readonly NetworkVariable<float> PackAssist = new NetworkVariable<float>();
        public bool IsProtected => IsSpawned && Now < ProtectedUntil.Value;
        public float PackSpeed => CanAct ? PackAssist.Value : 0f;
        public float CooldownRemaining(byte action) => Mathf.Max(0f, (float)(System.Math.Max(AttackReadyAt.Value, action == 0 ? SlideReadyAt.Value : PunchReadyAt.Value) - Now));
        public float CooldownFraction(byte action) => CooldownRemaining(action) / Mathf.Max(.1f, action == 0 ? tuning.slideCooldown : tuning.punchCooldown);
        public float AttackCooldown01 => Mathf.Clamp01((float)(AttackReadyAt.Value - Now) / Mathf.Max(.1f, AttackCooldownSeconds.Value));
        public bool ReleasingMines => mineSequence;
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Item = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ItemCount = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> Downed = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> Finished = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> InputPaused = new NetworkVariable<bool>();
        public readonly NetworkVariable<double> StartsAt = new NetworkVariable<double>();
        public readonly NetworkVariable<int> RacePlace = new NetworkVariable<int>();
        public readonly NetworkVariable<int> FinishPlace = new NetworkVariable<int>();
        public readonly NetworkVariable<int> CompletedLaps = new NetworkVariable<int>();
        public readonly NetworkVariable<double> BoostUntil = new NetworkVariable<double>();
        public readonly NetworkVariable<float> BoostReserve = new NetworkVariable<float>();
        public readonly NetworkVariable<bool> ReserveBoosting = new NetworkVariable<bool>();
        public readonly NetworkVariable<double> SlideUntil = new NetworkVariable<double>();
        public readonly NetworkVariable<double> RaceClosesAt = new NetworkVariable<double>();
        public readonly NetworkVariable<bool> Drafting = new NetworkVariable<bool>();
        public NetworkPlayer Player { get; private set; }
        public bool Active { get; private set; }
        public bool HasFinishedRace => FinishPlace.Value > 0;
        public bool Started => StartsAt.Value > 0 && Now >= StartsAt.Value;
        public bool CanAct => Active && Started && Health.Value > 0 && !Downed.Value && !Finished.Value && !HasFinishedRace && !InputPaused.Value;
        public Vector3 Position => Player.movement.transform.position;
        public int MaxHealth => tuning.maxHealth;
        public float BoostSpeed => CanAct && (Now < BoostUntil.Value || ReserveBoosting.Value) ? BoostSpeedBonus : 0f;
        public float SlideSpeed => CanAct && Now < SlideUntil.Value ? SlideSpeedBonus : 0f;
        public float DraftSpeed => CanAct && Drafting.Value ? 3f : 0f;
        public bool IsChargingThrow => localChargeStart >= 0;
        public float ThrowCharge => Mathf.Clamp01((Time.unscaledTime - localChargeStart) / FullThrowChargeSeconds);
        SkatePlayerSettings tuning;
        RagdollController ragdoll;
        Rigidbody body;
        double nextAttack, recoverAt;
        byte pendingAttack;
        bool mineSequence, botBoostHeld;
        double nextMine, mineSequenceEndsAt;
        int lastRewardedKills;
        double hitAt;
        bool hitPending;
        bool wasStarted, hidden;
        float localChargeStart = -1f;
        bool localReleasePending;
        bool localBoostHeld, serverBoostHeld, wasBoosting;
        double serverChargeStart = -1, lastMotionSample, nextSlideHit;
        readonly HashSet<ulong> slideHits = new HashSet<ulong>();
        Vector3 lastMotionPosition, sampledVelocity;
        Vector3 recoveryPosition;
        Quaternion recoveryRotation;
        SkateMatchRules rules;
        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Player = GetComponent<NetworkPlayer>();
            rules = MatchConnection.Instance?.ActiveRules as SkateMatchRules;
            Active = rules != null;
            if (!Active) { enabled = false; return; }
            tuning = Player.movement.GetComponent<SkatePlayerSettings>();
            ragdoll = Player.movement.GetComponent<RagdollController>();
            body = Player.movement.GetComponent<Rigidbody>();
            Players.Add(this);
            lastMotionPosition = Position;
            lastMotionSample = Now;
            if (IsServer) { Health.Value = MaxHealth; ProtectedUntil.Value = Now + 1.75; }
            Downed.OnValueChanged += StateChanged; Finished.OnValueChanged += StateChanged; InputPaused.OnValueChanged += StateChanged;
            FinishPlace.OnValueChanged += FinishPlaceChanged;
            Kills.OnValueChanged += KillsChanged; Deaths.OnValueChanged += DeathsChanged;
            CoinsEarned.OnValueChanged += CoinsChanged;
            ApplyState();
            SkateCombatEffects.AttachGroundTrail(Player.movement.transform, NetworkObjectId, Player.IsBot.Value);
        }
        public override void OnNetworkDespawn()
        {
            rules?.PlayerLeft(this);
            Players.Remove(this); Active = false;
            Downed.OnValueChanged -= StateChanged; Finished.OnValueChanged -= StateChanged; InputPaused.OnValueChanged -= StateChanged;
            FinishPlace.OnValueChanged -= FinishPlaceChanged;
            Kills.OnValueChanged -= KillsChanged; Deaths.OnValueChanged -= DeathsChanged;
            CoinsEarned.OnValueChanged -= CoinsChanged;
            if (Player != null && Player.movement != null)
            {
                Player.movement.SkateBoostSpeed = 0f;
                Player.movement.SkateSlideSpeed = 0f;
                Player.movement.SkateDraftSpeed = 0f;
                Player.movement.SkatePackSpeed = 0f;
            }
        }
        void KillsChanged(int oldValue, int value)
        {
            Player.participant.kills = value;
            if (IsOwner && !Player.IsBot.Value && value > oldValue)
            {
                int kills = value - oldValue;
                OnlineManager.Instance?.Profiles?.RecordProgress(kills, 0, kills * (OnlineManager.Instance?.xpPerKill ?? 25));

            }
        }
        void CoinsChanged(int before, int value)
        {
            if (!IsOwner || Player.IsBot.Value || value <= before) return;
            OnlineManager.Instance?.Profiles?.RecordCurrency(value - before, 0);
            int kills = Mathf.Max(1, Kills.Value - lastRewardedKills); lastRewardedKills = Kills.Value;
            rules.ShowKillReward(kills, value - before);
        }
        void DeathsChanged(int oldValue, int value)
        {
            Player.participant.deaths = value;
            if (IsOwner && !Player.IsBot.Value && value > oldValue) OnlineManager.Instance?.Profiles?.RecordProgress(0, value - oldValue, 0);
        }
        void StateChanged(bool before, bool after) => ApplyState();
        void FinishPlaceChanged(int before, int after) => ApplyState();
        void ApplyState()
        {
            if (!Active || Player == null) return;
            if (!CanAct)
            {
                localChargeStart = -1f;
                localReleasePending = false;
                serverChargeStart = -1;
                serverBoostHeld = false;
                mineSequence = false;
                Player.movement.SkateBoostSpeed = 0f;
                Player.movement.SkateSlideSpeed = 0f;
                Player.movement.SkateDraftSpeed = 0f;
                Player.movement.SkatePackSpeed = 0f;
            }
            if (HasFinishedRace)
            {
                if (!hidden)
                {
                    hidden = true;
                    if (ragdoll.IsRagdolled) ragdoll.PrepareForRespawn();
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                    body.isKinematic = true;
                    foreach (var collider in Player.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    foreach (var renderer in Player.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    foreach (var physicsBody in Player.GetComponentsInChildren<Rigidbody>(true))
                        physicsBody.isKinematic = true;
                    foreach (var canvas in Player.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                    if (Player.movement.animator != null) Player.movement.animator.enabled = false;
                    var trail = Player.movement.GetComponent<SkateGroundTrail>();
                    if (trail != null) trail.enabled = false;
                    if (Player.participant != null)
                    {
                        if (Player.participant.nameText != null) Player.participant.nameText.enabled = false;
                        Player.participant.enabled = false;
                    }
                }
                Player.RefreshInput();
                if (Finished.Value && IsOwner && !Player.IsBot.Value) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                return;
            }
            if (Downed.Value && !ragdoll.IsRagdolled)
            {
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
            if (Started != wasStarted) { wasStarted = Started; ApplyState(); }
            Player.movement.SkateBoostSpeed = BoostSpeed;
            Player.movement.SkateSlideSpeed = SlideSpeed;
            Player.movement.SkateDraftSpeed = DraftSpeed;
            Player.movement.SkatePackSpeed = PackSpeed;
            bool boosting = BoostSpeed > 0f;
            if (wasBoosting != boosting)
            {
                wasBoosting = boosting;
                var trail = Player.movement.GetComponent<SkateGroundTrail>();
                if (trail != null) trail.SetBoosting(boosting);
                if (boosting) SkateCombatEffects.SpawnBoost(Player.movement.transform, .45f);
            }
            if (IsOwner && !Player.IsBot.Value)
            {
                bool heldNow = CanAct && !Player.IsPaused && Player.Skating.Value && Keyboard.current != null &&
                    (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) &&
                    !Keyboard.current.sKey.isPressed;
                if (localBoostHeld != heldNow)
                {
                    localBoostHeld = heldNow;
                    HoldBoostRpc(heldNow);
                }
            }
            if (IsOwner && !Player.IsBot.Value && !Player.IsPaused && CanAct && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) RequestAttackRpc(0);
                if (Mouse.current.rightButton.wasPressedThisFrame) RequestAttackRpc(1);
                if (Mouse.current.middleButton.wasPressedThisFrame && ItemCount.Value > 0)
                {
                    if (Item.Value == 1) localChargeStart = Time.unscaledTime;
                    ThrowButtonRpc(true, Vector3.zero);
                }
                if (Mouse.current.middleButton.wasReleasedThisFrame && IsChargingThrow)
                {
                    // Release after the camera applies this frame's mouse movement.
                    localReleasePending = true;
                }
            }
            if (!IsServer || Finished.Value || HasFinishedRace || !Started) return;
            if (mineSequence)
            {
                if (!CanAct || Now >= mineSequenceEndsAt || Item.Value != 2 || ItemCount.Value <= 0) mineSequence = false;
                else if (Now >= nextMine)
                {
                    UseThrowableOnServer(false, 0, Vector3.zero);
                    nextMine = Now + .28;
                    if (ItemCount.Value <= 0) mineSequence = false;
                }
            }
            if (hitPending && Now >= hitAt)
            {
                hitPending = false;
                if (CanAct)
                {
                    if (pendingAttack == 2) { mineSequence = true; nextMine = Now; mineSequenceEndsAt = Now + 3.5; }
                    else MeleeOnServer(pendingAttack);
                }
            }
            if (CanAct && Now < SlideUntil.Value && Now >= nextSlideHit)
            {
                nextSlideHit = Now + .035;
                MeleeOnServer(0);
            }
            if (Downed.Value && Now >= recoverAt) RecoverOnServer();
            if (!Downed.Value && rules.FellOff(this))
                KnockDownOnServer(Position, 0, 0, rules.respawnDelay, true);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !Active) return;
            bool requested = serverBoostHeld || (Player.IsBot.Value && botBoostHeld);
            bool usingReserve = requested && CanAct && Player.Skating.Value && Now >= BoostUntil.Value && BoostReserve.Value > 0f;
            ReserveBoosting.Value = usingReserve;
            if (usingReserve) BoostReserve.Value = Mathf.Max(0f, BoostReserve.Value - Time.fixedDeltaTime);
        }
        void LateUpdate()
        {
            if (localReleasePending)
            {
                localReleasePending = false;
                if (IsSpawned && CanAct && IsOwner && !Player.IsBot.Value && !Player.IsPaused)
                    ThrowButtonRpc(false, GrenadeAimDirection());
                localChargeStart = -1f;
            }
            if (!IsSpawned || !Active || !IsServer || !body.isKinematic) return;
            double elapsed = Now - lastMotionSample;
            if (elapsed < .075) return;
            Vector3 displacement = Position - lastMotionPosition;
            sampledVelocity = displacement.sqrMagnitude > 64f || !CanAct
                ? Vector3.zero
                : Vector3.Lerp(sampledVelocity, Vector3.ClampMagnitude(displacement / (float)elapsed, 50f), .65f);
            lastMotionSample = Now;
            lastMotionPosition = Position;
        }

        internal Vector3 ThrowMomentum => body.isKinematic ? sampledVelocity : body.linearVelocity;

        internal bool ApplyBoostOnServer(float duration)
        {
            if (!IsServer || !CanAct || !Player.Skating.Value) return false;
            BoostUntil.Value = System.Math.Max(BoostUntil.Value, Now + duration);
            return true;
        }
        public void SetBotBoost(bool held) { if (IsServer && Player.IsBot.Value) botBoostHeld = held; }
        public void SetPaused(bool value)
        {
            if (Active && IsSpawned && IsOwner && !Player.IsBot.Value) PauseRpc(value);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void PauseRpc(bool value) { InputPaused.Value = value; }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void HoldBoostRpc(bool held) { serverBoostHeld = !Player.IsBot.Value && held && CanAct; }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAttackRpc(byte action) { if (!Player.IsBot.Value && action < 2) AttackOnServer(action); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ThrowButtonRpc(bool pressed, Vector3 aim)
        {
            if (Player.IsBot.Value || !CanAct || ItemCount.Value <= 0)
            {
                serverChargeStart = -1;
                return;
            }
            if (pressed)
            {
                if (Item.Value == 2) AttackOnServer(2);
                else if (Item.Value == 1) serverChargeStart = Now;
                return;
            }
            if (serverChargeStart < 0 || Item.Value != 1) return;
            float charge = Mathf.Clamp01((float)(Now - serverChargeStart) / FullThrowChargeSeconds);
            serverChargeStart = -1;
            AttackOnServer(2, charge, aim);
        }

        public bool AttackOnServer(byte action, float charge = .35f, Vector3 aim = default)
        {
            if (!IsServer || !CanAct || Now < nextAttack || action > 2) return false;
            if (mineSequence || hitPending || action == 2 && ItemCount.Value <= 0) return false;
            if (action == 0 && (Now < SlideReadyAt.Value || !Player.Skating.Value || !Player.movement.SampleSkateSurface(out _))) return false;
            if (action == 1 && Now < PunchReadyAt.Value) return false;
            if (action == 0) SlideReadyAt.Value = Now + tuning.slideCooldown;
            if (action == 1) PunchReadyAt.Value = Now + tuning.punchCooldown;
            AttackCooldownSeconds.Value = Mathf.Max(.65f, Player.movement.attackDuration);
            nextAttack = Now + AttackCooldownSeconds.Value;
            AttackReadyAt.Value = nextAttack;
            if (action == 0)
            {
                slideHits.Clear();
                nextSlideHit = Now + .08;
                SlideUntil.Value = Now + .55;
            }
            else if (action == 2 && Item.Value == 1)
                UseThrowableOnServer(true, charge, aim);
            else
            {
                pendingAttack = action;
                hitAt = Now + .2;
                hitPending = true;
            }
            AnimateRpc(action);
            return true;
        }
        [Rpc(SendTo.Everyone)]
        void AnimateRpc(byte action)
        {
            Player.movement.PlayCombatAnimation(action == 0 ? "slide" : action == 1 ? "punch" : "throw");
            if (action < 2)
                SkateCombatEffects.SpawnMelee(Player.movement.transform, action == 0);
        }
        void MeleeOnServer(byte action)
        {
            float range = action == 0 ? 3.15f : 2.2f;
            foreach (var other in Players)
            {
                if (other == this || !other.Active || other.Finished.Value || other.Downed.Value || other.HasFinishedRace) continue;
                var delta = other.Position - Position;
                if (delta.magnitude > range || Vector3.Dot(Player.movement.transform.forward, delta.normalized) < .15f || !ClearPath(Position, other.Position)) continue;
                if (action == 0 && !slideHits.Add(other.NetworkObjectId)) continue;
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
            if (!IsServer || !Active || !Started || Finished.Value || HasFinishedRace || Downed.Value || Health.Value <= 0 || IsProtected || damage <= 0) return;
            Health.Value = Mathf.Max(0, Health.Value - damage);
            if (Health.Value == 0)
            {
                Deaths.Value++;
                if (attacker != null && attacker != this && attacker.IsSpawned)
                {
                    attacker.Kills.Value++;
                    attacker.CoinsEarned.Value += CurrencyManager.Instance != null ? CurrencyManager.Instance.coinsPerKill : 10;
                }
            }
            if (attacker != null) HitNoticeRpc(attacker.Player.Username.Value.ToString(), Health.Value == 0);
            KnockDownOnServer(origin, force, lift, Mathf.Clamp(duration, rules.respawnDelay, 1.3f), false);
        }
        [Rpc(SendTo.Owner)]
        void HitNoticeRpc(string name, bool knockout)
        {
            if (IsOwner && !Player.IsBot.Value) rules.ShowHitNotice(name, knockout);
        }
        void KnockDownOnServer(Vector3 origin, float force, float lift, float duration, bool fellOff)
        {
            Vector3 velocity = ragdoll.MovementVelocity;
            Vector3 angularVelocity = ragdoll.MovementAngularVelocity;
            var recovery = rules.RecoveryPose(this, fellOff);
            recoveryPosition = recovery.position;
            recoveryRotation = recovery.rotation;
            recoverAt = Now + duration;
            hitPending = false; mineSequence = false;
            BoostUntil.Value = 0;
            ReserveBoosting.Value = false;
            SlideUntil.Value = 0;
            Downed.Value = true;
            KnockbackRpc(origin, force, lift, velocity, angularVelocity);
        }
        [Rpc(SendTo.Everyone)]
        void KnockbackRpc(Vector3 origin, float force, float lift, Vector3 velocity, Vector3 angularVelocity)
        {
            if (HasFinishedRace || Finished.Value) return;
            // Ragdoll bone motion is presentation; health and recovery remain host-owned.
            bool alreadyRagdolled = ragdoll.IsRagdolled;
            if (!alreadyRagdolled) ragdoll.EnterRagdoll(999, true);
            // The owning player's immediate velocity is more current than the host's replicated sample.
            if (!IsOwner || Player.IsBot.Value)
                ragdoll.InheritMomentum(velocity, angularVelocity);
            ragdoll.ApplyForceFromPoint(origin, force, lift);
        }
        void RecoverOnServer()
        {
            if (Health.Value == 0) Health.Value = MaxHealth;
            Downed.Value = false; ProtectedUntil.Value = Now + 1.75;
            TeleportRpc(recoveryPosition, recoveryRotation);
        }
        internal void RepositionStuckBot(Pose pose)
        {
            if (!IsServer || !Player.IsBot.Value || !CanAct) return;
            sampledVelocity = Vector3.zero;
            lastMotionPosition = pose.position;
            lastMotionSample = Now;
            TeleportRpc(pose.position, pose.rotation);
        }
        [Rpc(SendTo.Everyone)]
        void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            if (HasFinishedRace) return;
            if (ragdoll.IsRagdolled) ragdoll.PrepareForRespawn();
            body.isKinematic = !(IsOwner || Player.IsBot.Value && IsServer);
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.position = position; body.rotation = rotation;
            Player.movement.transform.SetPositionAndRotation(position, rotation);
            if (IsOwner) Player.movement.GetComponent<OwnerNetworkTransform>().Teleport(position, rotation, Player.movement.transform.localScale);
            Player.RefreshInput();
        }
        public bool CanCollect(MysteryBoxReward reward) => CanAct && (reward == MysteryBoxReward.Boost
            ? BoostReserve.Value < MaxBoostReserve : ItemCount.Value == 0);
        public bool CollectOnServer(MysteryBoxReward reward)
        {
            if (!IsServer || !CanCollect(reward)) return false;
            if (reward == MysteryBoxReward.Boost)
            {
                if (BoostReserve.Value >= MaxBoostReserve) return false;
                float seconds = BoostPickupSeconds + (rules != null ? rules.TrailingBoostBonus(this) : 0f);
                BoostReserve.Value = Mathf.Min(MaxBoostReserve, BoostReserve.Value + seconds);
                return true;
            }
            if (ItemCount.Value > 0) return false;
            Item.Value = reward == MysteryBoxReward.Bomb ? 1 : 2;
            ItemCount.Value = Item.Value == 1 ? 1 : 3; return true;
        }
        internal Vector3 GrenadeAimDirection()
        {
            var camera = Player.playerCamera;
            return camera != null ? camera.transform.forward : Player.movement.transform.forward;
        }

        void UseThrowableOnServer(bool grenade, float charge, Vector3 aim)
        {
            if (ItemCount.Value <= 0) return;
            if (rules.SpawnProjectile(this, grenade, charge, aim) && --ItemCount.Value <= 0)
                Item.Value = 0;
        }
        public bool TryBotAttack(SkateCombat target)
        {
            if (!IsServer || !CanAct || target == null || target == this || !target.CanAct || target.IsProtected) return false;
            Vector3 delta = target.Position - Position;
            float distance = delta.magnitude;
            float ahead = Vector3.Dot(Player.movement.transform.forward, delta.normalized);
            if (!ClearPath(Position, target.Position)) return false;
            if (Item.Value == 2 && distance > 2.5f && distance < 11f && ahead < -.2f) return AttackOnServer(2);
            if (ahead < .35f) return false;
            if (Item.Value == 1 && distance > 5f && distance < 18f)
                return AttackOnServer(2, .35f, (delta.normalized + Vector3.up * .18f).normalized);
            if (distance < 2.2f && Now >= PunchReadyAt.Value) return AttackOnServer(1);
            return distance > 2f && distance < 5.5f && Now >= SlideReadyAt.Value && AttackOnServer(0);
        }
    }
}
