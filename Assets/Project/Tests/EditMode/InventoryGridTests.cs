using NUnit.Framework;
using Office.UI;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Pins down where the inventory cursor lands.
    /// </summary>
    /// <remarks>
    /// The grid is drawn at a fixed size and only the first
    /// <c>GameplayConstants.InventorySlots</c> cells are real, so the cursor is always one
    /// arithmetic slip away from sitting on a cell the player does not own — which reads as a
    /// screen that will not respond rather than as an out-of-range index.
    /// </remarks>
    public sealed class InventoryGridTests
    {
        private const int Columns = 4;

        /// <summary>Four live cells drawn in a grid that has room for eight — today's layout.</summary>
        private const int Live = 4;

        [Test]
        public void StepColumn_WrapsAtTheEndOfTheRow()
        {
            Assert.AreEqual(0, InventoryGrid.StepColumn(3, Live, Columns, 1));
        }

        [Test]
        public void StepColumn_WrapsBackwardsFromTheFirstCell()
        {
            Assert.AreEqual(3, InventoryGrid.StepColumn(0, Live, Columns, -1));
        }

        [Test]
        public void StepColumn_StaysInsideItsOwnRow()
        {
            // Eight live cells, two full rows: the second row must not spill into the first.
            Assert.AreEqual(4, InventoryGrid.StepColumn(7, 8, Columns, 1));
        }

        [Test]
        public void StepColumn_WrapsInsideAShortLastRow()
        {
            // Six cells: the last row holds two, so stepping right from cell 5 wraps to 4.
            Assert.AreEqual(4, InventoryGrid.StepColumn(5, 6, Columns, 1));
        }

        [Test]
        public void StepRow_MovesStraightDownTheColumn()
        {
            Assert.AreEqual(4, InventoryGrid.StepRow(0, 8, Columns, 1));
        }

        [Test]
        public void StepRow_WrapsBackToTheTop()
        {
            Assert.AreEqual(0, InventoryGrid.StepRow(4, 8, Columns, 1));
        }

        [Test]
        public void StepRow_StaysPutWhenTheCellBelowDoesNotExist()
        {
            // Six cells: nothing sits under column 2 of the second row, and wrapping around
            // lands back where it started rather than on a cell the player does not have.
            Assert.AreEqual(2, InventoryGrid.StepRow(2, 6, Columns, 1));
        }

        [Test]
        public void StepRow_DoesNothingInAGridOneRowTall()
        {
            Assert.AreEqual(1, InventoryGrid.StepRow(1, Live, Columns, 1));
        }

        [Test]
        public void Step_AnswersNoneWhileTheGridIsEmpty()
        {
            Assert.AreEqual(InventoryGrid.None, InventoryGrid.StepColumn(0, 0, Columns, 1));
            Assert.AreEqual(InventoryGrid.None, InventoryGrid.StepRow(0, 0, Columns, 1));
            Assert.AreEqual(InventoryGrid.None, InventoryGrid.Clamp(0, 0));
        }

        [Test]
        public void Clamp_PullsACursorBackInsideAShrunkGrid()
        {
            // What a run ending under an open screen looks like: the capacity is gone and the
            // cursor is still pointing at the last cell it saw.
            Assert.AreEqual(Live - 1, InventoryGrid.Clamp(7, Live));
        }

        [Test]
        public void StepColumn_SurvivesAColumnCountOfZero()
        {
            // Nothing authored can produce this, but a serialised field can be dragged to it.
            // One column means every row holds one cell, so sideways is nowhere — not a
            // division by zero.
            Assert.AreEqual(0, InventoryGrid.StepColumn(0, Live, 0, 1));
        }
    }
}
