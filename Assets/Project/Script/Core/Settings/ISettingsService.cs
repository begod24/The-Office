using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// The one owner of what the player chose, and the one place it is applied and stored.
    /// </summary>
    /// <remarks>
    /// Two settings screens exist — the main menu's and the pause menu's — and there will be
    /// more consumers than screens: the music reads a volume, a future SFX bus reads another,
    /// the loading screen may read a resolution. Every one of them asks here. A screen that
    /// kept its own copy would be a second answer to a question this already answers, and the
    /// two would disagree the first time the other screen changed something.
    /// <para>
    /// Changes are announced as <see cref="SettingsChanged"/> on the event bus, so nothing
    /// that reacts to a setting has to know which screen moved it.
    /// </para>
    /// </remarks>
    public interface ISettingsService
    {
        GameSettings Current { get; }

        /// <summary>
        /// What the resolution picker steps through: deduplicated, sorted, and guaranteed to
        /// contain both the size on screen and the stored one.
        /// </summary>
        IReadOnlyList<Vector2Int> Resolutions { get; }

        /// <summary>
        /// False where the platform ignores a resolution change — the editor. The screen says
        /// so rather than letting the player conclude the picker is broken.
        /// </summary>
        bool DisplayChangesTakeEffect { get; }

        void SetMasterVolume(float slider);

        void SetMusicVolume(float slider);

        /// <summary>Applies immediately, then stores. Ignores a non-positive size.</summary>
        void ApplyDisplay(Vector2Int resolution, FullScreenMode mode);
    }
}
