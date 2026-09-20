using System;

namespace HitBoss.Multiplayer
{
    [Serializable]
    public sealed class RoomInvitation
    {
        public string kind = "hitboss.room.v1";
        public string code;
        public string senderName;
        public long expires;
        [NonSerialized] public string senderId;
        public bool Expired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expires;
    }
}
