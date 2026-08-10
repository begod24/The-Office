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

                // Wear has to match, or a half-broken stapler would merge into a fresh one and
                // one of the two histories would silently win. Items that wear are authored
                // with a max stack of one, so in practice this never rejects anything; it is
                // here so that authoring one wrong degrades into separate slots rather than
                // into free repairs.
                if (slot.Wear != incoming.Wear) continue;

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

                slots[i] = new ItemStack(incoming.DefinitionId, moved, incoming.Wear);
                incoming.Count -= moved;
            }

            return incoming.Count > 0 ? incoming : ItemStack.Empty;
        }

        /// <summary>
        /// Moves one slot's contents onto another and reports whether anything actually moved.
        /// What the player is doing when they drag a cell across the inventory.
        /// </summary>
        /// <remarks>
        /// Two readings of a drop, and the slot decides which: onto the same item it merges,
        /// onto anything else it swaps. Swapping rather than refusing is what makes an
        /// occupied target work at all — the item already there has to go somewhere, and the
        /// slot the player just emptied is the only place that does not invent capacity.
        /// <para>
        /// Wear is part of identity here for the reason <see cref="Distribute"/> gives: merging
        /// a half-broken stapler into a fresh one silently picks a winner between two
        /// histories. Mismatched wear falls through to the swap, which keeps both.
        /// </para>
        /// </remarks>
        public static bool Move(ItemStack[] slots, int from, int to, int maxStack)
        {
            if (slots == null || from == to) return false;
            if (from < 0 || from >= slots.Length || to < 0 || to >= slots.Length) return false;

            var source = slots[from];

            // Dragging an empty slot is a gesture, not an edit.
            if (source.IsEmpty) return false;

            maxStack = Mathf.Max(1, maxStack);

            var target = slots[to];

            if (!target.IsEmpty &&
                target.DefinitionId == source.DefinitionId &&
                target.Wear == source.Wear &&
                target.Count < maxStack)
            {
                var moved = Mathf.Min(maxStack - target.Count, source.Count);

                target.Count += moved;
                source.Count -= moved;

                slots[to] = target;

                // Whatever did not fit stays where it was rather than evaporating.
                slots[from] = source.Count > 0 ? source : ItemStack.Empty;
                return true;
            }

            slots[from] = target;
            slots[to] = source;
            return true;
        }
    }
}
