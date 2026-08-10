using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// The screen, as much of it as the settings service needs.
    /// </summary>
    /// <remarks>
    /// Wrapped rather than called directly for two reasons. Tests can drive a fake monitor —
    /// one that reports no modes, one that refuses the size it was given — and none of that
    /// is reachable through the real <c>Screen</c>. And <see cref="ChangesTakeEffect"/> has
    /// somewhere honest to live: <c>Screen.SetResolution</c> does nothing in the editor,
    /// where the Game view owns the size, which is exactly why a working resolution picker
    /// looks broken to whoever is testing it in play mode.
    /// </remarks>
    public interface IDisplayDevice
    {
        Vector2Int CurrentResolution { get; }

        FullScreenMode CurrentMode { get; }

        /// <summary>Sizes the platform reports. Empty in the editor and on some platforms.</summary>
        IReadOnlyList<Vector2Int> Supported { get; }

        /// <summary>False where a resolution change is accepted and then ignored.</summary>
        bool ChangesTakeEffect { get; }

        void Apply(Vector2Int resolution, FullScreenMode mode);
    }
}
