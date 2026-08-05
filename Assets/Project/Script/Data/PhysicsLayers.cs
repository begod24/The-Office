using UnityEngine;

namespace Office.Data
{
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

        public static LayerMask WalkableMask =>
            (1 << Default) | (1 << LevelGeometry) | (1 << Prop);

        public static LayerMask InteractionMask =>
            (1 << Interactable) | (1 << LevelGeometry) | (1 << Prop) | (1 << Default);

        public static LayerMask OcclusionMask =>
            (1 << LevelGeometry) | (1 << Default);
    }
}
