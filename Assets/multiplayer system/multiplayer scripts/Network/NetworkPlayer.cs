using HitBoss.Social;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public PlayerController movement;
        public CoopPlayerController coopMovement;
        public PlayerCamera playerCamera;
        public MatchParticipant participant;
        public PlayerAppearance appearance;
        public readonly NetworkVariable<bool> IsBot = new NetworkVariable<bool>();
        public readonly NetworkVariable<NetworkLoadout> Loadout = new NetworkVariable<NetworkLoadout>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<FixedString128Bytes> Username = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Skating = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        Rigidbody body;
        bool paused;
        public bool IsPaused => paused;
        public void RefreshInput()
        {
            if (!IsSpawned) return;
            var combat = GetComponent<HitBoss.Multiplayer.Skate.SkateCombat>();
            bool allowed = IsOwner && !IsBot.Value && !paused && (combat == null || !combat.Active || combat.CanAct);
            movement.enabled = allowed && !mode.useCoopMovement;
            if (coopMovement != null) coopMovement.enabled = allowed && mode.useCoopMovement;
            if (playerCamera != null) playerCamera.enabled = IsOwner && !IsBot.Value && !paused && !(combat != null && combat.Active && combat.Finished.Value);
        }
        MultiplayerMode mode;
        int pendingBotNumber;
        public void PrepareBot(int number) { pendingBotNumber = number; }

        void Awake()
        {
            body = movement.GetComponent<Rigidbody>();
            SetLocalControl(false);
        }
        void SetLocalControl(bool local)
        {
            movement.enabled = local && !paused && (mode == null || !mode.useCoopMovement);
            if (coopMovement != null) coopMovement.enabled = local && !paused && mode != null && mode.useCoopMovement;
            if (playerCamera != null) playerCamera.enabled = local && !paused;
            foreach (var camera in GetComponentsInChildren<Camera>(true)) camera.enabled = local;
            foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = local;
            foreach (var rigidbody in GetComponentsInChildren<Rigidbody>(true)) rigidbody.isKinematic = true;
            body.isKinematic = !local;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            if (local) movement.GetComponent<CapsuleCollider>().enabled = true;
        }
        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingBotNumber > 0)
            {
                IsBot.Value = true;
                Username.Value = new FixedString128Bytes("Bot " + pendingBotNumber);
            }
            mode = RoomManager.Instance.Mode;
            SetLocalControl(IsOwner && !IsBot.Value);
            if (IsBot.Value && IsServer)
            {
                body.isKinematic = false;
                movement.GetComponent<CapsuleCollider>().enabled = true;
                var brain = GetComponent<NetworkBotBrain>() ?? gameObject.AddComponent<NetworkBotBrain>();
                brain.Initialize(this);
            }
            if (MatchConnection.Instance.ActiveRules != null) MatchConnection.Instance.ActiveRules.ConfigurePlayer(this, mode);
            Loadout.OnValueChanged += LoadoutChanged;
            if (IsOwner) Loadout.Value = MatchConnection.Instance.CurrentLoadout;
            LoadoutChanged(default, Loadout.Value);
            movement.SetSkateMode(mode.skating);
            if (IsOwner)
            {
                var name = IsBot.Value ? Username.Value.ToString() : OnlineManager.Instance?.Profiles?.Current?.username ?? "Player";
                Username.Value = new FixedString128Bytes(name.Length > 40 ? name.Substring(0, 40) : name);
                if (!IsBot.Value) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            Username.OnValueChanged += NameChanged;
            Skating.OnValueChanged += SkateChanged;
            if (IsOwner) Skating.Value = mode.skating;
            else SkateChanged(false, Skating.Value);
            NameChanged(default, Username.Value);
        }
        void LoadoutChanged(NetworkLoadout previous, NetworkLoadout next) { if (appearance != null) appearance.Apply(next); }
        public void SetPaused(bool value)
        {
            if (!IsOwner || IsBot.Value) return;
            if (value) { if (movement.enabled) movement.StopImmediately(); if (coopMovement != null && coopMovement.enabled) coopMovement.StopImmediately(); }
            paused = value;
            GetComponent<HitBoss.Multiplayer.Skate.SkateCombat>()?.SetPaused(value);
            RefreshInput();
        }
        void LateUpdate()
        {
            if (IsSpawned && IsOwner && Skating.Value != movement.IsSkateMode) Skating.Value = movement.IsSkateMode;
        }
        void SkateChanged(bool previous, bool next) { if (!IsOwner) movement.SetSkateMode(next); }
        void NameChanged(FixedString128Bytes previous, FixedString128Bytes next)
        {
            participant.playerName = next.ToString(); participant.RefreshName();
        }
        public override void OnNetworkDespawn() { Username.OnValueChanged -= NameChanged; Skating.OnValueChanged -= SkateChanged; Loadout.OnValueChanged -= LoadoutChanged; }
    }
}
