using NUnit.Framework;
using Office.Data;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Pins down what happens to an item as it is used up.
    /// </summary>
    /// <remarks>
    /// The arithmetic is small, but every one of these cases is a way an item could quietly
    /// duplicate itself, repair itself, or vanish from a player's hand — and none of them
    /// would look like a bug in a playtest, only like the inventory "acting strange".
    /// </remarks>
    public sealed class ItemWearTests
    {
        private const int Stapler = 1;
        private const int BrokenStapler = 2;

        private const int MaxUses = 10;

        /// <summary>What an item with no DurabilityModule resolves to.</summary>
        private const int Everlasting = 0;

        /// <summary>Nothing is left behind when it breaks.</summary>
        private const int ContentNoId = ContentDefinition.NoId;

        [Test]
        public void Spend_LeavesAnEverlastingItemAlone()
        {
            var stack = new ItemStack(Stapler, 1);

            var after = ItemWear.Spend(stack, Everlasting, 1, ContentNoId);

            Assert.AreEqual(stack, after);
        }

        [Test]
        public void Spend_AccumulatesWear()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 1, 3), MaxUses, 2, ContentNoId);

            Assert.AreEqual(new ItemStack(Stapler, 1, 5), after);
        }

        [Test]
        public void Spend_IgnoresAZeroCost()
        {
            var stack = new ItemStack(Stapler, 1, 4);

            Assert.AreEqual(stack, ItemWear.Spend(stack, MaxUses, 0, ContentNoId));
        }

        // The last use is a use. Reaching the ceiling exactly has to break the item, or every
        // weapon in the game quietly gets one more swing than it was authored with.
        [Test]
        public void Spend_BreaksOnReachingTheCeiling()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 1, MaxUses - 1), MaxUses, 1, ContentNoId);

            Assert.IsTrue(after.IsEmpty);
        }

        [Test]
        public void Spend_LeavesBehindWhatTheItemBreaksInto()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 1, MaxUses - 1), MaxUses, 1,
                BrokenStapler);

            Assert.AreEqual(new ItemStack(BrokenStapler, 1), after);
        }

        // A stack of spares loses one and the next comes up fresh. Not resetting the wear
        // would break the whole stack at once; not consuming one would repair it for free.
        [Test]
        public void Spend_ConsumesOneOfAStackAndTheNextIsPristine()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 3, MaxUses - 1), MaxUses, 1, ContentNoId);

            Assert.AreEqual(new ItemStack(Stapler, 2, ItemWear.Pristine), after);
        }

        // A cost big enough to wrap a ushort would otherwise land back near zero and hand the
        // player a repaired weapon.
        [Test]
        public void Spend_DoesNotWrapOnAnAbsurdCost()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 1), MaxUses, int.MaxValue, ContentNoId);

            Assert.IsTrue(after.IsEmpty, "An overflowing cost repaired the item instead of breaking it.");
        }

        [Test]
        public void Spend_IgnoresAnEmptyStack()
        {
            Assert.IsTrue(ItemWear.Spend(ItemStack.Empty, MaxUses, 1, ContentNoId).IsEmpty);
        }

        [Test]
        public void RemainingUses_CountsDownFromTheCeiling()
        {
            Assert.AreEqual(MaxUses, ItemWear.RemainingUses(new ItemStack(Stapler, 1), MaxUses));
            Assert.AreEqual(4, ItemWear.RemainingUses(new ItemStack(Stapler, 1, 6), MaxUses));
        }

        [Test]
        public void RemainingUses_IsUnboundedForAnEverlastingItem()
        {
            Assert.AreEqual(int.MaxValue,
                ItemWear.RemainingUses(new ItemStack(Stapler, 1, 900), Everlasting));
        }

        // The HUD divides by this. An everlasting item reporting anything but a full bar would
        // draw a coffee mug as if it were about to break.
        [Test]
        public void NormalisedCondition_IsFullForAnEverlastingItem()
        {
            Assert.AreEqual(1f,
                ItemWear.NormalisedCondition(new ItemStack(Stapler, 1, 500), Everlasting));
        }

        [Test]
        public void NormalisedCondition_RunsFromOneToZero()
        {
            Assert.AreEqual(1f, ItemWear.NormalisedCondition(new ItemStack(Stapler, 1), MaxUses));
            Assert.AreEqual(0.5f, ItemWear.NormalisedCondition(new ItemStack(Stapler, 1, 5), MaxUses));
            Assert.AreEqual(0f, ItemWear.NormalisedCondition(new ItemStack(Stapler, 1, MaxUses), MaxUses));
        }

    }
}
