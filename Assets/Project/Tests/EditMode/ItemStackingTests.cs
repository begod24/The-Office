using NUnit.Framework;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    public sealed class ItemStackingTests
    {
        private const int Stapler = 1;
        private const int Keycard = 2;

        private static ItemStack[] EmptySlots(int count) => new ItemStack[count];

        [Test]
        public void Distribute_FillsTheFirstEmptySlot()
        {
            var slots = EmptySlots(3);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Stapler, 1), 1);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[0]);
            Assert.IsTrue(slots[1].IsEmpty);
        }

        [Test]
        public void Distribute_TopsUpAMatchingStackBeforeOpeningANewSlot()
        {
            var slots = EmptySlots(3);
            slots[1] = new ItemStack(Keycard, 1);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Keycard, 2), 4);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.AreEqual(new ItemStack(Keycard, 3), slots[1]);
            Assert.IsTrue(slots[0].IsEmpty, "A new slot was opened while an existing stack had room.");
        }

        [Test]
        public void Distribute_SpillsIntoFurtherSlotsOnceAStackIsFull()
        {
            var slots = EmptySlots(3);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Keycard, 9), 4);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.AreEqual(new ItemStack(Keycard, 4), slots[0]);
            Assert.AreEqual(new ItemStack(Keycard, 4), slots[1]);
            Assert.AreEqual(new ItemStack(Keycard, 1), slots[2]);
        }

        [Test]
        public void Distribute_ReturnsWhatDidNotFit()
        {
            var slots = EmptySlots(1);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Keycard, 6), 4);

            Assert.AreEqual(new ItemStack(Keycard, 4), slots[0]);
            Assert.AreEqual(new ItemStack(Keycard, 2), remainder);
        }

        // WorldItem compares the remainder with what it offered to decide whether anything
        // moved. A full inventory must therefore hand the whole stack back untouched, or an
        // item would be deleted from the floor without ever reaching a player.
        [Test]
        public void Distribute_LeavesAFullInventoryUnchanged()
        {
            var slots = new[] { new ItemStack(Stapler, 1) };
            var offered = new ItemStack(Keycard, 3);

            var remainder = ItemStacking.Distribute(slots, offered, 4);

            Assert.AreEqual(offered, remainder);
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[0]);
        }

        [Test]
        public void Distribute_IgnoresAnEmptyOffer()
        {
            var slots = EmptySlots(2);

            var remainder = ItemStacking.Distribute(slots, ItemStack.Empty, 4);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.IsTrue(slots[0].IsEmpty);
        }

        // Zero is "no content"; treating it as a real max stack would loop forever.
        [Test]
        public void Distribute_TreatsAnInvalidMaxStackAsOne()
        {
            var slots = EmptySlots(2);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Stapler, 3), 0);

            Assert.AreEqual(new ItemStack(Stapler, 1), slots[0]);
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[1]);
            Assert.AreEqual(new ItemStack(Stapler, 1), remainder);
        }

        [Test]
        public void EmptyStack_IsWhatDefaultGives()
        {
            Assert.IsTrue(default(ItemStack).IsEmpty);
            Assert.IsTrue(new ItemStack(Stapler, 0).IsEmpty);
            Assert.IsFalse(new ItemStack(Stapler, 1).IsEmpty);
        }
    }
}
