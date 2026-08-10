namespace Office.Data
{
    public static class GameplayConstants
    {
        public const float MaxPlayerHealth = 100f;

        /// <summary>
        /// Inventory slots per player. The HUD hotbar and the inventory grid are both
        /// generated with exactly this many cells — the three must never drift, so all of
        /// them read this constant.
        /// </summary>
        /// <remarks>
        /// Eight is also the last number the keyboard can address directly: every slot has a
        /// number key, and `PlayerInputReader` resolves one `HotbarN` action per slot. Going
        /// past eight means either a ninth key or a hotbar that steps, and the input asset has
        /// to gain the action either way — a raised number alone gives a slot nothing can
        /// select. The inventory grid is four wide, so this also has to stay a multiple of
        /// four to avoid a ragged last row.
        /// </remarks>
        public const int InventorySlots = 8;

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
