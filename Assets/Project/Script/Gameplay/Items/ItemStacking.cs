using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The slot arithmetic behind picking things up, kept free of NGO so it can be tested
    /// without a running session.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerInventory"/> copies its NetworkList into a plain array, runs this,
    /// then writes back only the entries that actually moved — an unchanged element still
    /// costs a delta on the wire.
    /// </remarks>
    public static class ItemStacking
    {
        /// <summary>
        /// Fills <paramref name="slots"/> with as much of <paramref name="incoming"/> as fits
        /// and returns the remainder, which is <see cref="ItemStack.Empty"/> when it all went in.
        /// </summary>
        public static ItemStack Distribute(ItemStack[] slots, ItemStack incoming, int maxStack)
        {
            if (slots == null || incoming.IsEmpty) return incoming;

            maxStack = Mathf.Max(1, maxStack);

            // Top up matching stacks before opening a new slot, so a nearly full inventory
            // still absorbs loose items instead of refusing them.
            for (var i = 0; i < slots.Length && incoming.Count > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.DefinitionId != incoming.DefinitionId) continue;
                if (slot.Count >= maxStack) continue;

                var moved = Mathf.Min(maxStack - slot.Count, incoming.Count);

                slot.Count += moved;
                slots[i] = slot;
                incoming.Count -= moved;
            }

            for (var i = 0; i < slots.Length && incoming.Count > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;

                var moved = Mathf.Min(maxStack, incoming.Count);

                slots[i] = new ItemStack(incoming.DefinitionId, moved);
                incoming.Count -= moved;
            }

            return incoming.Count > 0 ? incoming : ItemStack.Empty;
        }
    }
}
