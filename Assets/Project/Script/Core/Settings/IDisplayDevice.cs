using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    public interface IDisplayDevice
    {
        Vector2Int CurrentResolution { get; }

        FullScreenMode CurrentMode { get; }

        IReadOnlyList<Vector2Int> Supported { get; }

        bool ChangesTakeEffect { get; }

        void Apply(Vector2Int resolution, FullScreenMode mode);
    }
}
