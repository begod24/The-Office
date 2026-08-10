using System;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Every setting the player owns, as one immutable value.
    /// </summary>
    /// <remarks>
    /// One struct rather than a field per setting, for the same reason <c>VitalsState</c>
    /// travels as one variable: the change event carries a complete picture, so no listener
    /// can observe a new volume next to a resolution that has not landed yet.
    /// <para>
    /// Volumes are <b>slider positions</b> in 0..1, never gain — see <see cref="VolumeCurve"/>
    /// for why those are not the same number. A zero resolution means "nothing stored yet",
    /// which is the state a fresh install is in and is not the same as a resolution of zero.
    /// </para>
    /// </remarks>
    public readonly struct GameSettings : IEquatable<GameSettings>
    {
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = 0.7f;

        public readonly int Width;
        public readonly int Height;
        public readonly FullScreenMode DisplayMode;
        public readonly float MasterVolume;
        public readonly float MusicVolume;

        public GameSettings(Vector2Int resolution, FullScreenMode displayMode,
            float masterVolume, float musicVolume)
        {
            Width = Mathf.Max(0, resolution.x);
            Height = Mathf.Max(0, resolution.y);
            DisplayMode = displayMode;
            MasterVolume = Mathf.Clamp01(masterVolume);
            MusicVolume = Mathf.Clamp01(musicVolume);
        }

        public Vector2Int Resolution => new(Width, Height);

        /// <summary>False until the player has applied a resolution at least once.</summary>
        public bool HasResolution => Width > 0 && Height > 0;

        public GameSettings WithResolution(Vector2Int resolution) =>
            new(resolution, DisplayMode, MasterVolume, MusicVolume);

        public GameSettings WithDisplayMode(FullScreenMode displayMode) =>
            new(Resolution, displayMode, MasterVolume, MusicVolume);

        public GameSettings WithMasterVolume(float slider) =>
            new(Resolution, DisplayMode, slider, MusicVolume);

        public GameSettings WithMusicVolume(float slider) =>
            new(Resolution, DisplayMode, MasterVolume, slider);

        // Hand-written so that "did anything actually change" costs no boxing: the settings
        // service asks it on every slider frame, and ValueType.Equals answers by reflection.
        public bool Equals(GameSettings other) =>
            Width == other.Width &&
            Height == other.Height &&
            DisplayMode == other.DisplayMode &&
            Mathf.Approximately(MasterVolume, other.MasterVolume) &&
            Mathf.Approximately(MusicVolume, other.MusicVolume);

        public override bool Equals(object obj) => obj is GameSettings other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Width, Height, (int)DisplayMode, MasterVolume, MusicVolume);

        public static bool operator ==(GameSettings a, GameSettings b) => a.Equals(b);

        public static bool operator !=(GameSettings a, GameSettings b) => !a.Equals(b);

        public override string ToString() =>
            $"{Width}x{Height} {DisplayMode}, master {MasterVolume:0.00}, music {MusicVolume:0.00}";
    }
}
