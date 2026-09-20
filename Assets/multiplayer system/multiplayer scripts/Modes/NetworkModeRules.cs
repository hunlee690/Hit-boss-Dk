using UnityEngine;

namespace HitBoss.Multiplayer
{
    // A new mode supplies its own rules without changing rooms, invitations or transport.
    public abstract class NetworkModeRules : MonoBehaviour
    {
        public abstract Pose SpawnPose(int playerIndex);
        public abstract void ConfigurePlayer(NetworkPlayer player, MultiplayerMode mode);
        public virtual void BeginServerMatch() { }
        public virtual void EndServerMatch() { }
    }
}
