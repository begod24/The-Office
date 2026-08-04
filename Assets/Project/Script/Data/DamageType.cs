using System;

namespace Office.Data
{
    /// <summary>
    /// Elemental damage channels from GDD §8.3. Flags so a weapon can carry several
    /// and an enemy can declare vulnerabilities and immunities as masks.
    /// The damage resolver reads these; nothing in the codebase branches on a concrete enemy type.
    /// </summary>
    [Flags]
    public enum DamageType
    {
        None = 0,
        Blunt = 1 << 0,
        Cutting = 1 << 1,
        Water = 1 << 2,
        Electric = 1 << 3,
        Adhesive = 1 << 4,
        Light = 1 << 5
    }
}
