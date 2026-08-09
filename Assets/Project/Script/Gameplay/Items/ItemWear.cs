using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The arithmetic behind an item wearing out and breaking, kept free of NGO so it can be
    /// tested without a running session.
    /// </summary>
    /// <remarks>
    /// Same split as <see cref="ItemStacking"/> and <see cref="Vitals"/>: the caller owns
    /// replication and authority, this owns the rules and returns a new stack rather than
    /// mutating one, so nobody can half-apply a break.
    /// <para>
    /// The ceiling lives in the item's <see cref="DurabilityModule"/> and the wear lives in
    /// <see cref="ItemStack.Wear"/>, because a ScriptableObject is shared by every copy of the
    /// item — writing wear into the asset would blunt every stapler in the building at once.
    /// This class is the seam between the two and never sees either: the caller resolves the
    /// module and passes plain numbers.
    /// </para>
    /// </remarks>
    public static class ItemWear
    {
        /// <summary>Factory fresh. Also what <c>default</c> gives.</summary>
        public const ushort Pristine = 0;

        /// <summary>
        /// True for an item that never wears out. Everything without a
        /// <see cref="DurabilityModule"/> answers this, which is most of the game.
        /// </summary>
        public static bool IsEverlasting(int maxUses) => maxUses <= 0;

        /// <summary>
        /// Uses left in the top item of the stack, or <see cref="int.MaxValue"/> for something
        /// that never wears out. For the HUD — nothing decides anything from this.
        /// </summary>
        public static int RemainingUses(in ItemStack stack, int maxUses) =>
            IsEverlasting(maxUses) ? int.MaxValue : Mathf.Max(0, maxUses - stack.Wear);

        /// <summary>
        /// Fraction of this item's life left, 1 when fresh and 0 when it is about to break.
        /// Everlasting items report 1 so a HUD can draw them without a special case.
        /// </summary>
        public static float NormalisedCondition(in ItemStack stack, int maxUses) =>
            IsEverlasting(maxUses) ? 1f : Mathf.Clamp01((float)RemainingUses(stack, maxUses) / maxUses);

        /// <summary>
        /// Spends <paramref name="cost"/> uses and returns what the slot now holds — the same
        /// stack when nothing wore out, a worn one, or whatever the item left behind when it
        /// broke.
        /// </summary>
        /// <remarks>
        /// Breaking consumes exactly one item from the stack. With the intended authoring —
        /// max stack of one on anything durable — that empties the slot or replaces it with
        /// the <c>BreaksInto</c> item. With a stack of several, the next one comes up
        /// <see cref="Pristine"/>, which is the only reading of "a spare" that does not either
        /// destroy the spares or hand out a free repair.
        /// </remarks>
        /// <param name="maxUses">From the item's <see cref="DurabilityModule"/>. Zero or less
        /// means it never wears out and the stack comes back untouched.</param>
        /// <param name="breaksIntoId">What is left behind, or
        /// <see cref="ContentDefinition.NoId"/> for an item that simply ceases to exist.</param>
        public static ItemStack Spend(in ItemStack stack, int maxUses, int cost, int breaksIntoId)
        {
            if (stack.IsEmpty || cost <= 0 || IsEverlasting(maxUses)) return stack;

            // Clamped rather than wrapped: a cost large enough to overflow a ushort has
            // already broken the item several times over, and wrapping would silently repair it.
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
