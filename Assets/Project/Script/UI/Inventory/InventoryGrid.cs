using UnityEngine;

namespace Office.UI
{
    public static class InventoryGrid
    {
        public const int None = -1;

        public const int Columns = 4;

        public const int Rows = 2;

        public const int Cells = Columns * Rows;

        public static int Clamp(int index, int count) =>
            count <= 0 ? None : Mathf.Clamp(index, 0, count - 1);

        public static int StepColumn(int index, int count, int columns, int delta)
        {
            if (count <= 0) return None;

            columns = Mathf.Max(1, columns);
            index = Clamp(index, count);

            if (delta == 0) return index;

            var rowStart = index / columns * columns;

            var width = Mathf.Min(columns, count - rowStart);
            var column = Wrap(index - rowStart + delta, width);

            return rowStart + column;
        }

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
