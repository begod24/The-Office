using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    /// <summary>The real screen.</summary>
    public sealed class ScreenDisplayDevice : IDisplayDevice
    {
        public Vector2Int CurrentResolution => new(Screen.width, Screen.height);

        public FullScreenMode CurrentMode => Screen.fullScreenMode;

        public IReadOnlyList<Vector2Int> Supported
        {
            get
            {
                var modes = Screen.resolutions;
                var sizes = new List<Vector2Int>(modes.Length);

                // Desktops report one entry per refresh rate, so the same size arrives several
                // times. Deduplicating is the catalogue's job — this only strips the field it
                // does not model.
                foreach (var mode in modes) sizes.Add(new Vector2Int(mode.width, mode.height));

                return sizes;
            }
        }

        // The editor accepts Screen.SetResolution and does nothing with it: the Game view owns
        // the size in play mode. Saying so out loud is the difference between a settings screen
        // that looks broken and one that explains itself.
        public bool ChangesTakeEffect => !Application.isEditor;

        public void Apply(Vector2Int resolution, FullScreenMode mode) =>
            Screen.SetResolution(resolution.x, resolution.y, mode);
    }
}
