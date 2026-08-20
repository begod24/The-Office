using NUnit.Framework;
using Office.Data;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    public sealed class ItemWearTests
    {
        private const int Stapler = 1;
        private const int BrokenStapler = 2;

        private const int MaxUses = 10;

        private const int Everlasting = 0;

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

        [Test]
        public void Spend_ConsumesOneOfAStackAndTheNextIsPristine()
        {
            var after = ItemWear.Spend(new ItemStack(Stapler, 3, MaxUses - 1), MaxUses, 1, ContentNoId);

            Assert.AreEqual(new ItemStack(Stapler, 2, ItemWear.Pristine), after);
        }

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
