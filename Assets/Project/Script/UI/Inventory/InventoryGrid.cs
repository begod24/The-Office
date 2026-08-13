using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// Moving a cursor around a row-major grid of inventory cells.
    /// </summary>
    /// <remarks>
    /// Pure and static for the same reason <c>ItemStacking</c> is: every awkward case here is
    /// arithmetic — a last row that is not full, a grid one row tall, a grid that is empty
    /// between runs — and every one of them ends with a cursor sitting on a cell that does not
    /// exist. Tested without a scene.
    /// <para>
    /// <paramref name="count"/> is the number of <em>live</em> cells, not the number drawn. The
    /// screen lays out a fixed grid and draws the cells past the player's capacity as locked;
    /// the cursor must never reach one of those.
    /// </para>
    /// </remarks>
    public static class InventoryGrid
    {
        /// <summary>Nothing to point at. What an empty grid answers.</summary>
        public const int None = -1;

        /// <summary>
        /// Cells per row. Four, matching the hotbar, so a slot keeps the number the player
        /// learned — and with <c>GameplayConstants.HotbarSlots</c> also at four, the top row
        /// <em>is</em> the hand and everything under it is the backpack.
        /// </summary>
        public const int Columns = 4;

        /// <summary>Rows drawn, whether or not the player has slots for all of them.</summary>
        public const int Rows = 2;

        /// <summary>
        /// Cells the screen draws. Anything past the player's capacity is drawn locked and the
        /// cursor never reaches it, so this may exceed the capacity but must never fall short.
        /// </summary>
        /// <remarks>
        /// Here rather than in the builder that lays the grid out, because the invariant it has
        /// to satisfy — at least <c>GameplayConstants.InventorySlots</c> — cannot be checked
        /// where it was. Both numbers are compile-time constants, so a runtime guard in the
        /// builder is code the compiler can prove will never run: it warns, and it protects
        /// nothing. <c>InventoryCapacityTests</c> asserts it instead, which fails when the
        /// suite runs rather than when someone happens to click the menu item.
        /// </remarks>
        public const int Cells = Columns * Rows;

        /// <summary>The nearest live index, or <see cref="None"/> when there are none.</summary>
        public static int Clamp(int index, int count) =>
            count <= 0 ? None : Mathf.Clamp(index, 0, count - 1);

        /// <summary>
        /// Steps left or right, wrapping inside the row the cursor is already on.
        /// </summary>
        /// <remarks>
        /// Wrapping rather than stopping dead, for the reason <c>PlayerInventory.Step</c> gives:
        /// a cursor that refuses to move makes the player look for the reason instead of the
        /// item.
        /// </remarks>
        public static int StepColumn(int index, int count, int columns, int delta)
        {
            if (count <= 0) return None;

            columns = Mathf.Max(1, columns);
            index = Clamp(index, count);

            if (delta == 0) return index;

            var rowStart = index / columns * columns;

            // The last row is short whenever capacity is not a multiple of the width.
            var width = Mathf.Min(columns, count - rowStart);
            var column = Wrap(index - rowStart + delta, width);

            return rowStart + column;
        }

        /// <summary>
        /// Steps up or down, wrapping inside the column. A short last row leaves holes; the
        /// cursor keeps going the same way until it finds a cell rather than refusing to move.
        /// </summary>
        public static int StepRow(int index, int count, int columns, int delta)
        {
            if (count <= 0) return None;

            columns = Mathf.Max(1, columns);
            index = Clamp(index, count);

            if (delta == 0) return index;

            var rows = (count + columns - 1) / columns;
            var column = index % columns;
            var row = Wrap(index / columns + delta, rows);
            var step = delta > 0 ? 1 : -1;

            for (var attempt = 0; attempt < rows; attempt++)
            {
                var candidate = row * columns + column;
                if (candidate < count) return candidate;

                row = Wrap(row + step, rows);
            }

            return index;
        }

        private static int Wrap(int value, int length)
        {
            if (length <= 0) return 0;

            value %= length;
            return value < 0 ? value + length : value;
        }
    }
}
