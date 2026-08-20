using UnityEngine;

namespace Office.Data
{
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
