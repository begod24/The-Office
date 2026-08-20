using Unity.Netcode.Components;

namespace Office.Gameplay
{
    public sealed class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
