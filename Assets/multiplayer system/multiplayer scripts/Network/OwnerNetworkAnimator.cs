using Unity.Netcode.Components;

namespace HitBoss.Multiplayer
{
    public sealed class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
