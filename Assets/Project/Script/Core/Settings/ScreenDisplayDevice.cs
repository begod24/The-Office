using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
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

                foreach (var mode in modes) sizes.Add(new Vector2Int(mode.width, mode.height));

                return sizes;
            }
        }

        public bool ChangesTakeEffect => !Application.isEditor;

        public void Apply(Vector2Int resolution, FullScreenMode mode) =>
            Screen.SetResolution(resolution.x, resolution.y, mode);
    }
}
