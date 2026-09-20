using Unity.Netcode.Components;

namespace HitBoss.Multiplayer
{
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
