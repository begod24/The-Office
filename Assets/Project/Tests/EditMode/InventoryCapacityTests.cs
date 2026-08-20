using NUnit.Framework;
using Office.Data;
using Office.Gameplay;
using Office.UI;

namespace Office.Tests.EditMode
{
    public sealed class InventoryCapacityTests
    {
        private const int Columns = InventoryGrid.Columns;

        [Test]
        public void TheHandIsFourSlots()
        {
            Assert.AreEqual(4, GameplayConstants.HotbarSlots,
                "GDD §7.1 gives the player four hotbar slots, and §7.2's soft roles depend on " +
                "that scarcity. Change the design document before this number.");
        }

        [Test]
        public void TheHandFitsInsideTheInventory()
        {
            Assert.LessOrEqual(GameplayConstants.HotbarSlots, GameplayConstants.InventorySlots,
                "The hotbar would address slots the inventory list never creates.");
        }

        [Test]
        public void TheGridHasNoRaggedLastRow()
        {
            Assert.AreEqual(0, GameplayConstants.InventorySlots % Columns,
                "The inventory grid is four wide; a capacity that is not a multiple of four " +
                "leaves a short last row for the cursor to fall out of.");
        }

        [Test]
        public void TheGridDrawsAtLeastEverySlotThePlayerHas()
        {
            Assert.GreaterOrEqual(InventoryGrid.Cells, GameplayConstants.InventorySlots,
                $"The inventory grid draws {InventoryGrid.Cells} cells but a player has " +
                $"{GameplayConstants.InventorySlots} slots. Slots past the end would be " +
                "unreachable and nothing would say so — the hotbar would still address them " +
                "by number. Add a row to InventoryGrid.Rows.");
        }

        [Test]
        public void TheHandIsAWholeNumberOfRows()
        {
            Assert.AreEqual(0, GameplayConstants.HotbarSlots % Columns,
                "The hand must end on a row boundary, or the inventory grid draws a row that " +
                "is half holdable and half not with nothing to say so.");
        }

        [Test]
        public void APickupFillsTheHandBeforeTheBackpack()
        {
            var slots = new ItemStack[GameplayConstants.InventorySlots];

            var remainder = ItemStacking.Distribute(slots, new ItemStack(1, 1), 1);

            Assert.IsTrue(remainder.IsEmpty);
            Assert.IsFalse(slots[0].IsEmpty, "The first hand slot was skipped.");

            for (var i = GameplayConstants.HotbarSlots; i < slots.Length; i++)
                Assert.IsTrue(slots[i].IsEmpty,
                    $"Slot {i} is in the backpack and was filled while the hand had room.");
        }
    }
}
