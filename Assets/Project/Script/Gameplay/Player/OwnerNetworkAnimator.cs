using Unity.Netcode.Components;

namespace Office.Gameplay
{
    /// <summary>
    /// NetworkAnimator whose owning client is authoritative, matching the
    /// owner-authoritative NetworkTransform and movement on the player prefab.
    /// </summary>
    public sealed class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
