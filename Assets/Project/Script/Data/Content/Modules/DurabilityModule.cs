using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Gives an item a finite number of uses.
    /// </summary>
    /// <remarks>
    /// Only the ceiling lives here. How much of it <em>this</em> stapler has left is
    /// per-instance state and travels in <c>ItemStack.Durability</c> — an asset is shared by
    /// every copy of the item, so writing wear into it would wear down all of them at once.
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Modules/Durability", fileName = "MOD_Durability")]
    public sealed class DurabilityModule : ItemModule
    {
        [Tooltip("Uses from new. Capped at ushort range because that is what one slot carries " +
                 "over the wire.")]
        [Range(1, 65535)]
        [SerializeField] private int maxUses = 40;

        [Tooltip("What is left of the item when it breaks. None means it disappears.")]
        [SerializeField] private ItemDefinition breaksInto;

        public int MaxUses => maxUses;

        public ItemDefinition BreaksInto => breaksInto;
    }
}
