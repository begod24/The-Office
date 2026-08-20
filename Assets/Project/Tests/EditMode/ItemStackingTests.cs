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

        [Test]
        public void Distribute_DoesNotMergeStacksAtDifferentWear()
        {
            var slots = EmptySlots(3);
            slots[0] = new ItemStack(Stapler, 1, 12);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Stapler, 1), 4);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.AreEqual(new ItemStack(Stapler, 1, 12), slots[0], "The worn stack was topped up.");
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[1]);
        }

        [Test]
        public void Distribute_MergesStacksAtIdenticalWear()
        {
            var slots = EmptySlots(3);
            slots[0] = new ItemStack(Stapler, 1, 12);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Stapler, 1, 12), 4);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.AreEqual(new ItemStack(Stapler, 2, 12), slots[0]);
            Assert.IsTrue(slots[1].IsEmpty);
        }

        [Test]
        public void Distribute_CarriesWearIntoNewSlots()
        {
            var slots = EmptySlots(2);

            var remainder = ItemStacking.Distribute(slots, new ItemStack(Keycard, 6, 7), 4);

            Assert.AreEqual(new ItemStack(Keycard, 4, 7), slots[0]);
            Assert.AreEqual(new ItemStack(Keycard, 2, 7), slots[1]);
            Assert.IsTrue(remainder.IsEmpty);
        }

        [Test]
        public void Move_FillsAnEmptySlot()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Stapler, 1);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 3, 1));

            Assert.IsTrue(slots[0].IsEmpty);
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[3]);
        }

        [Test]
        public void Move_SwapsTwoDifferentItems()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Stapler, 1);
            slots[1] = new ItemStack(Keycard, 2);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 1, 1));

            Assert.AreEqual(new ItemStack(Keycard, 2), slots[0]);
            Assert.AreEqual(new ItemStack(Stapler, 1), slots[1]);
        }

        [Test]
        public void Move_MergesOntoAMatchingStack()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Keycard, 2);
            slots[1] = new ItemStack(Keycard, 1);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 1, 4));

            Assert.IsTrue(slots[0].IsEmpty);
            Assert.AreEqual(new ItemStack(Keycard, 3), slots[1]);
        }

        [Test]
        public void Move_LeavesWhatDoesNotFitBehind()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Keycard, 3);
            slots[1] = new ItemStack(Keycard, 2);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 1, 4));

            Assert.AreEqual(new ItemStack(Keycard, 1), slots[0]);
            Assert.AreEqual(new ItemStack(Keycard, 4), slots[1]);
        }

        [Test]
        public void Move_SwapsRatherThanMergingWhenTheTargetIsFull()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Keycard, 1);
            slots[1] = new ItemStack(Keycard, 4);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 1, 4));

            Assert.AreEqual(new ItemStack(Keycard, 4), slots[0]);
            Assert.AreEqual(new ItemStack(Keycard, 1), slots[1]);
        }

        [Test]
        public void Move_KeepsItemsWornDifferentlyApart()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Stapler, 1, 12);
            slots[1] = new ItemStack(Stapler, 1, 3);

            Assert.IsTrue(ItemStacking.Move(slots, 0, 1, 4));

            Assert.AreEqual(new ItemStack(Stapler, 1, 3), slots[0]);
            Assert.AreEqual(new ItemStack(Stapler, 1, 12), slots[1]);
        }

        [Test]
        public void Move_DoesNothingWhenTheSourceIsEmpty()
        {
            var slots = EmptySlots(4);
            slots[1] = new ItemStack(Keycard, 2);

            Assert.IsFalse(ItemStacking.Move(slots, 0, 1, 4));

            Assert.IsTrue(slots[0].IsEmpty);
            Assert.AreEqual(new ItemStack(Keycard, 2), slots[1]);
        }

        [Test]
        public void Move_DoesNothingWhenADragEndsWhereItStarted()
        {
            var slots = EmptySlots(4);
            slots[2] = new ItemStack(Stapler, 1);

            Assert.IsFalse(ItemStacking.Move(slots, 2, 2, 1));

            Assert.AreEqual(new ItemStack(Stapler, 1), slots[2]);
        }

        [Test]
        public void Move_RefusesAnIndexOutsideTheInventory()
        {
            var slots = EmptySlots(4);
            slots[0] = new ItemStack(Stapler, 1);

            Assert.IsFalse(ItemStacking.Move(slots, 0, 4, 1));
            Assert.IsFalse(ItemStacking.Move(slots, -1, 0, 1));

            Assert.AreEqual(new ItemStack(Stapler, 1), slots[0]);
        }
    }
}
