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
        public readonly NetworkVariable<FixedString128Bytes> Username = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        Rigidbody body;

        void Awake()
        {
            body = movement.GetComponent<Rigidbody>();
            SetLocalControl(false);
        }
        void SetLocalControl(bool local)
        {
            movement.enabled = local;
            if (coopMovement != null) coopMovement.enabled = false;
            if (playerCamera != null) playerCamera.enabled = local;
            foreach (var camera in GetComponentsInChildren<Camera>(true)) camera.enabled = local;
            foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = local;
            foreach (var rigidbody in GetComponentsInChildren<Rigidbody>(true)) rigidbody.isKinematic = true;
            body.isKinematic = !local;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            if (local) movement.GetComponent<CapsuleCollider>().enabled = true;
            // Local combat cannot be shared safely until a mode supplies authoritative combat rules.
            foreach (var combat in GetComponentsInChildren<CombatController>(true)) combat.enabled = false;
            foreach (var inventory in GetComponentsInChildren<ThrowableInventory>(true)) inventory.enabled = false;
        }
        public override void OnNetworkSpawn()
        {
            SetLocalControl(IsOwner);
            var mode = RoomManager.Instance.Mode;
            if (MatchConnection.Instance.ActiveRules != null) MatchConnection.Instance.ActiveRules.ConfigurePlayer(this, mode);
            if (IsOwner)
            {
                var name = OnlineManager.Instance?.Profiles?.Current?.username ?? "Player";
                Username.Value = new FixedString128Bytes(name.Length > 40 ? name.Substring(0, 40) : name);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            Username.OnValueChanged += NameChanged;
            NameChanged(default, Username.Value);
        }
        void NameChanged(FixedString128Bytes previous, FixedString128Bytes next)
        {
            participant.playerName = next.ToString(); participant.RefreshName();
        }
        public override void OnNetworkDespawn() { Username.OnValueChanged -= NameChanged; }
    }
}
