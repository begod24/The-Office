using NUnit.Framework;
using Office.Data;
using Office.Gameplay;
using Office.UI;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Pins down the hand-versus-backpack split.
    /// </summary>
    /// <remarks>
    /// The two constants are read by six places that cannot see each other — the slot list,
    /// the number keys, the HUD hotbar, the inventory grid, the cell numbering and the screen's
    /// equip path — and every way of getting them wrong is silent. A hand larger than the
    /// number keys gives a slot nothing can select; a hand larger than the whole inventory
    /// gives the hotbar cells the list never creates. Neither throws.
    /// <para>
    /// GDD §7.1 fixes the hand at four, and §7.2 builds the soft roles on that scarcity: four
    /// players who cannot carry everything have to divide the tools between them. Raising it
    /// is a design decision, so it should cost a failing test rather than an inspector edit.
    /// </para>
    /// </remarks>
    public sealed class InventoryCapacityTests
    {
        /// <summary>Cells per row in both the inventory grid and the hotbar.</summary>
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

        /// <remarks>
        /// This one used to be a guard inside <c>InventoryBuilder</c>. It could not stay there:
        /// both sides are compile-time constants, so the compiler folded the comparison and the
        /// guard became unreachable code — a standing warning that would never fire. Here it is
        /// checked on every test run, and it fails at the moment someone raises the capacity
        /// rather than the next time they happen to rebuild the screen.
        /// </remarks>
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
            // The screen has no divider between the hand and the bag: the split is legible
            // only because the hand ends exactly where a row does.
            Assert.AreEqual(0, GameplayConstants.HotbarSlots % Columns,
                "The hand must end on a row boundary, or the inventory grid draws a row that " +
                "is half holdable and half not with nothing to say so.");
        }

        [Test]
        public void APickupFillsTheHandBeforeTheBackpack()
        {
            // Distribute walks the slots in order and the hand is the front of the list, so
            // this holds by construction — which is exactly why it is worth pinning: reversing
            // the loop or moving the hand to the back would leave a player who picked an item
            // up unable to swing it, with nothing logged.
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
