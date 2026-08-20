using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    public static class ResolutionCatalogue
    {
        public static readonly Vector2Int[] Fallback =
        {
            new(1280, 720),
            new(1366, 768),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160)
        };

        public static List<Vector2Int> Build(IEnumerable<Vector2Int> reported,
            params Vector2Int[] alsoInclude)
        {
            var options = new List<Vector2Int>(16);

            if (reported != null)
                foreach (var size in reported)
                    Add(options, size);

            if (options.Count == 0)
                foreach (var size in Fallback)
                    Add(options, size);

            if (alsoInclude != null)
                foreach (var size in alsoInclude)
                    Add(options, size);

            options.Sort(Compare);
            return options;
        }

        public static int NearestIndex(IReadOnlyList<Vector2Int> options, Vector2Int target)
        {
            if (options == null || options.Count == 0) return -1;

            var best = 0;
            var bestDistance = long.MaxValue;

            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] == target) return i;

                var distance = (long)Math.Abs(options[i].x - target.x) +
                               Math.Abs(options[i].y - target.y);

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        private static void Add(ICollection<Vector2Int> options, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0) return;
            if (options.Contains(size)) return;

            options.Add(size);
        }

        private static int Compare(Vector2Int a, Vector2Int b) =>
            a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
    }
}
