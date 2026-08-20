using System;
using UnityEngine;

namespace Office.Data
{
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
