using UnityEngine;

namespace Office.Gameplay
{
    public static class ItemStacking
    {
        public static ItemStack Distribute(ItemStack[] slots, ItemStack incoming, int maxStack)
        {
            if (slots == null || incoming.IsEmpty) return incoming;

            maxStack = Mathf.Max(1, maxStack);

            for (var i = 0; i < slots.Length && incoming.Count > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.DefinitionId != incoming.DefinitionId) continue;

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

        public static bool Move(ItemStack[] slots, int from, int to, int maxStack)
        {
            if (slots == null || from == to) return false;
            if (from < 0 || from >= slots.Length || to < 0 || to >= slots.Length) return false;

            var source = slots[from];

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

                slots[from] = source.Count > 0 ? source : ItemStack.Empty;
                return true;
            }

            slots[from] = target;
            slots[to] = source;
            return true;
        }
    }
}
