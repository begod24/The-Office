using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    public static class ItemWear
    {
        public const ushort Pristine = 0;

        public static bool IsEverlasting(int maxUses) => maxUses <= 0;

        public static int RemainingUses(in ItemStack stack, int maxUses) =>
            IsEverlasting(maxUses) ? int.MaxValue : Mathf.Max(0, maxUses - stack.Wear);

        public static float NormalisedCondition(in ItemStack stack, int maxUses) =>
            IsEverlasting(maxUses) ? 1f : Mathf.Clamp01((float)RemainingUses(stack, maxUses) / maxUses);

        public static ItemStack Spend(in ItemStack stack, int maxUses, int cost, int breaksIntoId)
        {
            if (stack.IsEmpty || cost <= 0 || IsEverlasting(maxUses)) return stack;

            var worn = Mathf.Min(stack.Wear + cost, ushort.MaxValue);

            if (worn < maxUses) return new ItemStack(stack.DefinitionId, stack.Count, (ushort)worn);

            if (stack.Count > 1)
                return new ItemStack(stack.DefinitionId, stack.Count - 1, Pristine);

            return breaksIntoId == ContentDefinition.NoId
                ? ItemStack.Empty
                : new ItemStack(breaksIntoId, 1, Pristine);
        }
    }
}
