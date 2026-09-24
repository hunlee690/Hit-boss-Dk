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
        public readonly NetworkVariable<NetworkLoadout> Loadout = new NetworkVariable<NetworkLoadout>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<FixedString128Bytes> Username = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Skating = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        Rigidbody body;
        MultiplayerMode mode;
        bool paused;
        int pendingBotNumber;

        public bool IsPaused => paused;

        static readonly string[] BotNames =
        {
            "Nova", "Blaze", "Ryder", "Echo", "Viper", "Luna",
            "Axel", "Jett", "Pixel", "Storm", "Raven", "Bolt"
        };

        public static string BotName(int number)
        {
            int index = Mathf.Max(0, number - 1);
            string name = BotNames[index % BotNames.Length];
            return index < BotNames.Length
                ? name
                : name + " " + (index / BotNames.Length + 1);
        }

        public void PrepareBot(int number)
        {
            pendingBotNumber = number;
        }

        void Awake()
        {
            if (movement != null)
                body = movement.GetComponent<Rigidbody>();

            SetLocalControl(false);
        }

        public void RefreshInput()
        {
            if (!IsSpawned || movement == null)
                return;

            var combat = GetComponent<HitBoss.Multiplayer.Skate.SkateCombat>();

            bool allowed =
                IsOwner &&
                !IsBot.Value &&
                !paused &&
                (combat == null || !combat.Active || combat.CanAct);

            movement.enabled =
                allowed &&
                (mode == null || !mode.useCoopMovement);

            if (coopMovement != null)
                coopMovement.enabled =
                    allowed &&
                    mode != null &&
                    mode.useCoopMovement;

            if (playerCamera != null)
                playerCamera.enabled =
                    IsOwner &&
                    !IsBot.Value &&
                    !paused;
        }

        void SetLocalControl(bool local)
        {
            if (movement != null)
                movement.enabled =
                    local &&
                    !paused &&
                    (mode == null || !mode.useCoopMovement);

            if (coopMovement != null)
                coopMovement.enabled =
                    local &&
                    !paused &&
                    mode != null &&
                    mode.useCoopMovement;

            if (playerCamera != null)
                playerCamera.enabled = local && !paused;

            foreach (var camera in GetComponentsInChildren<Camera>(true))
                camera.enabled = local;

            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = local;

            foreach (var rigidbody in GetComponentsInChildren<Rigidbody>(true))
                rigidbody.isKinematic = true;

            if (body != null)
                body.isKinematic = !local;

            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            if (movement != null)
            {
                var capsule = movement.GetComponent<CapsuleCollider>();

                if (capsule != null)
                    capsule.enabled = local || IsServer;
            }

            foreach (var combat in GetComponentsInChildren<CombatController>(true))
                combat.enabled = false;

            foreach (var inventory in GetComponentsInChildren<ThrowableInventory>(true))
                inventory.enabled = local || IsServer;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingBotNumber > 0)
            {
                IsBot.Value = true;
                Username.Value = new FixedString128Bytes(BotName(pendingBotNumber));

                if (appearance != null)
                    Loadout.Value = appearance.RandomLoadout();
            }

            mode = RoomManager.Instance != null
                ? RoomManager.Instance.Mode
                : null;

            SetLocalControl(IsOwner && !IsBot.Value);

            if (IsBot.Value && IsServer)
            {
                if (body != null)
                    body.isKinematic = false;

                if (movement != null)
                {
                    var capsule = movement.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                        capsule.enabled = true;
                }

                var brain =
                    GetComponent<NetworkBotBrain>() ??
                    gameObject.AddComponent<NetworkBotBrain>();

                brain.Initialize(this);
            }

            if (MatchConnection.Instance != null &&
                MatchConnection.Instance.ActiveRules != null)
            {
                MatchConnection.Instance.ActiveRules.ConfigurePlayer(this, mode);
            }

            Loadout.OnValueChanged += LoadoutChanged;

            if (IsOwner && !IsBot.Value && MatchConnection.Instance != null)
                Loadout.Value = MatchConnection.Instance.CurrentLoadout;

            LoadoutChanged(default, Loadout.Value);

            if (movement != null)
                movement.SetSkateMode(mode != null && mode.skating);

            if (IsOwner)
            {
                string name =
                    IsBot.Value
                        ? Username.Value.ToString()
                        : OnlineManager.Instance?.Profiles?.Current?.username ?? "Player";

                Username.Value = new FixedString128Bytes(
                    name.Length > 40
                        ? name.Substring(0, 40)
                        : name
                );

                if (!IsBot.Value)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            Username.OnValueChanged += NameChanged;
            Skating.OnValueChanged += SkateChanged;

            if (IsOwner)
                Skating.Value = mode != null && mode.skating;
            else
                SkateChanged(false, Skating.Value);

            NameChanged(default, Username.Value);
        }

        void LoadoutChanged(NetworkLoadout previous, NetworkLoadout next)
        {
            if (appearance != null)
                appearance.Apply(next);
        }

        public void SetPaused(bool value)
        {
            if (!IsOwner || IsBot.Value)
                return;

            if (value)
            {
                if (movement != null && movement.enabled)
                    movement.StopImmediately();

                if (coopMovement != null && coopMovement.enabled)
                    coopMovement.StopImmediately();
            }

            paused = value;
            GetComponent<HitBoss.Multiplayer.Skate.SkateCombat>()?.SetPaused(value);
            RefreshInput();
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || movement == null)
                return;

            if (Skating.Value != movement.IsSkateMode)
                Skating.Value = movement.IsSkateMode;
        }

        void SkateChanged(bool previous, bool next)
        {
            if (!IsOwner && movement != null)
                movement.SetSkateMode(next);
        }

        void NameChanged(FixedString128Bytes previous, FixedString128Bytes next)
        {
            if (participant == null)
                return;

            participant.playerName = next.ToString();
            participant.RefreshName();
        }

        public override void OnNetworkDespawn()
        {
            Username.OnValueChanged -= NameChanged;
            Skating.OnValueChanged -= SkateChanged;
            Loadout.OnValueChanged -= LoadoutChanged;
        }
    }
}
