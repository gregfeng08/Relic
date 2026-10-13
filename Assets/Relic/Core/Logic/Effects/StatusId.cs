namespace Relic.Core.Effects
{
    /// <summary>
    /// Physical status effects (no elemental reactions). See docs/design/03-combat.md.
    /// </summary>
    public enum StatusId
    {
        None,
        Bleeding,
        Burning,
        Chilled,
        Soaked,
        Poisoned,
        Staggered,
        Rooted,
        Exhausted,
        Dazed,
        Silenced,
    }

    public static class StatusRules
    {
        /// <summary>Every status is harmful to whoever has it.</summary>
        public static bool IsDebuff(StatusId s) => s != StatusId.None;

        static readonly StatusId[] NoneCleared = new StatusId[0];
        static readonly StatusId[] SoakedClears = { StatusId.Burning };
        static readonly StatusId[] BurningClears = { StatusId.Soaked, StatusId.Chilled };

        /// <summary>
        /// Physical interactions (not a reaction system): applying a status clears
        /// the ones it physically cancels. Soaked puts out Burning; Burning dries
        /// Soaked and thaws Chilled.
        /// </summary>
        public static StatusId[] Clears(StatusId applied)
        {
            switch (applied)
            {
                case StatusId.Soaked: return SoakedClears;
                case StatusId.Burning: return BurningClears;
                default: return NoneCleared;
            }
        }
    }
}
