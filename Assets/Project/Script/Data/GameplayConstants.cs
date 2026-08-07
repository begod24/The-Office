namespace Office.Data
{
    public static class GameplayConstants
    {
        public const float MaxPlayerHealth = 100f;

        /// <summary>
        /// Inventory slots per player. The HUD hotbar is generated with exactly this many
        /// cells — the two must never drift, so both read this constant.
        /// </summary>
        public const int InventorySlots = 5;
    }
}
