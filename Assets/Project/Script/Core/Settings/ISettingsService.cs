using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    public interface ISettingsService
    {
        GameSettings Current { get; }

        IReadOnlyList<Vector2Int> Resolutions { get; }

        bool DisplayChangesTakeEffect { get; }

        void SetMasterVolume(float slider);

        void SetMusicVolume(float slider);

        void ApplyDisplay(Vector2Int resolution, FullScreenMode mode);
    }
}
