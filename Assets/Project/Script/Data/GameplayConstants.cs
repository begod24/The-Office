namespace Office.Data
{
    public static class GameplayConstants
    {
        public const float MaxPlayerHealth = 100f;

        /// <summary>
        /// Slots the hand can reach: the number keys, the scroll step and the HUD hotbar all
        /// address exactly these, and only something in one of them can be held or swung.
        /// </summary>
        /// <remarks>
        /// Four is GDD §7.1, and the scarcity is the point (§7.2): four players times four
        /// hand slots cannot carry everything, which is what forces the soft roles. The
        /// backpack below does not loosen that — reaching it means opening the inventory
        /// screen, standing still in front of everyone, so it stores options rather than
        /// answers. The first <see cref="HotbarSlots"/> indices of the one slot list are the
        /// hand; there is no second list to keep in sync.
        /// </remarks>
        public const int HotbarSlots = 4;

        /// <summary>
        /// All slots per player: the hand plus the backpack. The inventory grid draws exactly
        /// this many live cells, and the slot list is created at this size.
        /// </summary>
        /// <remarks>
        /// The inventory grid is four wide, so this stays a multiple of four to avoid a
        /// ragged last row — and it can only grow past the drawn grid together with a row in
        /// <c>InventoryBuilder</c>, which refuses to build otherwise.
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
