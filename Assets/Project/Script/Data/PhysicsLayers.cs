using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Single source of truth for layer indices. Mirrors ProjectSettings/TagManager.asset.
    /// <see cref="Office.Editor"/> validates that these stay in sync; never hardcode a layer
    /// index or a LayerMask in a component.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int UI = 5;

        public const int Player = 8;
        public const int Enemy = 9;
        public const int Interactable = 10;
        public const int LevelGeometry = 11;
        public const int Projectile = 12;
        public const int Prop = 13;
        public const int VoiceEmitter = 14;
        public const int ViewModel = 15;

        /// <summary>Names in declaration order, indexed by the constants above. Used by editor validation.</summary>
        public static readonly (int Index, string Name)[] Expected =
        {
            (Player, "Player"),
            (Enemy, "Enemy"),
            (Interactable, "Interactable"),
            (LevelGeometry, "LevelGeometry"),
            (Projectile, "Projectile"),
            (Prop, "Prop"),
            (VoiceEmitter, "VoiceEmitter"),
            (ViewModel, "ViewModel")
        };

        /// <summary>What a player's movement collider is allowed to stand on and bump into.</summary>
        public static LayerMask WalkableMask =>
            (1 << Default) | (1 << LevelGeometry) | (1 << Prop);

        /// <summary>What the interaction raycast may hit. Includes geometry so a wall blocks the ray.</summary>
        public static LayerMask InteractionMask =>
            (1 << Interactable) | (1 << LevelGeometry) | (1 << Prop) | (1 << Default);

        /// <summary>What blocks line of sight for perception and voice occlusion.</summary>
        public static LayerMask OcclusionMask =>
            (1 << LevelGeometry) | (1 << Default);
    }
}
