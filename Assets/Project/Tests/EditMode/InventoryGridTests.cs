using NUnit.Framework;
using Office.UI;

namespace Office.Tests.EditMode
{
    public sealed class InventoryGridTests
    {
        private const int Columns = 4;

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
            Assert.AreEqual(4, InventoryGrid.StepColumn(7, 8, Columns, 1));
        }

        [Test]
        public void StepColumn_WrapsInsideAShortLastRow()
        {
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
            Assert.AreEqual(Live - 1, InventoryGrid.Clamp(7, Live));
        }

        [Test]
        public void StepColumn_SurvivesAColumnCountOfZero()
        {
            Assert.AreEqual(0, InventoryGrid.StepColumn(0, Live, 0, 1));
        }
    }
}
