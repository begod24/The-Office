using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Turns whatever the platform reports into the list the resolution picker steps through.
    /// </summary>
    /// <remarks>
    /// Pure and free of <c>Screen</c>, so the awkward cases can be pinned down without a
    /// display attached: an editor that reports no modes at all, a desktop that reports the
    /// same size once per refresh rate, and a stored resolution the monitor stopped offering
    /// after the player unplugged it. Each of those otherwise shows up as a picker that is
    /// empty, full of duplicates, or starts on the wrong entry — none of which look like a
    /// bug in the list that produced them.
    /// </remarks>
    public static class ResolutionCatalogue
    {
        /// <summary>
        /// Offered when the platform reports nothing. The editor does exactly that, so
        /// without this the picker in play mode would have a single entry.
        /// </summary>
        public static readonly Vector2Int[] Fallback =
        {
            new(1280, 720),
            new(1366, 768),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160)
        };

        /// <param name="reported">What the platform supports. May be null or empty.</param>
        /// <param name="alsoInclude">
        /// Sizes that must appear whatever the platform said — the size on screen right now,
        /// and the one the player stored. A stored resolution missing from its own picker is
        /// how a settings screen ends up unable to show what it is already using.
        /// </param>
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

        /// <summary>
        /// Where <paramref name="target"/> sits in the list, or the closest entry to it.
        /// Returns -1 only for an empty list.
        /// </summary>
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
