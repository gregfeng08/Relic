namespace Relic.Core.Stats
{
    /// <summary>Trained character stats. See docs/design/02-core-loop.md.</summary>
    public enum StatId
    {
        STR, // Strength: melee damage, stagger
        AGI, // Agility: move/attack speed, crit, stamina regen
        VIT, // Vitality: max HP, HP regen, stamina pool, status resistance
        INT, // Intelligence: spell power, max mana
        INS, // Insight: mana regen, seeing secrets
        CHR, // Charisma: prices, NPC disposition
        DEF, // Defense: from armor, not trained; flat reduction per hit
    }

    public enum ResourceId
    {
        HP,
        Mana,
        Stamina,
        Resonance, // relic charge for the ultimate (Q)
    }
}
