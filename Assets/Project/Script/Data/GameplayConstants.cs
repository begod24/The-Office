namespace Office.Data
{
    public static class GameplayConstants
    {
        public const float MaxPlayerHealth = 100f;

        /// <summary>
        /// Inventory slots per player. The HUD hotbar is generated with exactly this many
        /// cells — the two must never drift, so both read this constant.
        /// </summary>
        public const int InventorySlots = 4;

        /// <summary>
        /// Seconds a downed player has before dying. GDD §15: a teammate can revive within
        /// sixty.
        /// </summary>
        public const float BleedOutSeconds = 60f;

        /// <summary>
        /// Health a revived player stands up with. Deliberately low — surviving a down is
        /// meant to leave the group weaker, not reset the fight.
        /// </summary>
        public const float ReviveHealth = 30f;
    }
}
