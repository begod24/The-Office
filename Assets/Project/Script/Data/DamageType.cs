using System;

namespace Office.Data
{
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
