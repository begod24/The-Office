using System;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// The display modes the settings screen offers, in the order it steps through them.
    /// </summary>
    /// <remarks>
    /// Borderless fullscreen rather than exclusive: exclusive fullscreen does not exist on
    /// macOS and silently becomes this same mode there, so offering both would put two
    /// entries that behave identically into the picker on half the machines that run the game.
    /// <para>
    /// A mode arriving from outside this list — Unity's own persistence, a platform default,
    /// a maximised window — still has to land on an entry, so <see cref="IndexOf"/> answers
    /// for every value of the enum rather than only for the two offered.
    /// </para>
    /// </remarks>
    public static class DisplayModes
    {
        public static readonly FullScreenMode[] Ordered =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed
        };

        public static string Label(FullScreenMode mode) => mode switch
        {
            FullScreenMode.ExclusiveFullScreen => "Fullscreen",
            FullScreenMode.FullScreenWindow => "Fullscreen",
            FullScreenMode.MaximizedWindow => "Windowed",
            _ => "Windowed"
        };

        public static int IndexOf(FullScreenMode mode)
        {
            var exact = Array.IndexOf(Ordered, mode);
            if (exact >= 0) return exact;

            var windowed = mode is FullScreenMode.Windowed or FullScreenMode.MaximizedWindow;
            var fallback = Array.IndexOf(Ordered,
                windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);

            return Mathf.Max(0, fallback);
        }
    }
}
