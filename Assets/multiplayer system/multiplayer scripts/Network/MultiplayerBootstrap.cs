using UnityEngine;

namespace HitBoss.Multiplayer
{
    [DefaultExecutionOrder(-80)]
    public sealed class MultiplayerBootstrap : MonoBehaviour
    {
        public RoomManager managerPrefab;
        void Awake() { if (RoomManager.Instance == null) Instantiate(managerPrefab); }
    }
}
