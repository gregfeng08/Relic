# 05 — Effect Grammar (shared contract)

Status: **v1 — lead-owned contract** · Code: `Assets/Relic/Core/Logic/Effects/` · Last updated 2026-10-13

Everything that *does something* in combat is an **EffectProgram**: relic enchantments, weapon skills, drawn spells, enemy attacks, boss patterns and elite modifiers. Generators produce programs. The validator checks them. One runtime (built later, at integration) executes them.

```
EffectProgram
 └─ Rules[≤4]
     ├─ Trigger      Active · Passive · OnHit · OnKill · OnTakeDamage · OnDash
     │               OnCloseCall · OnLowHealth(threshold) · Periodic(interval) · OnStatusApplied(status)
     ├─ Conditions   TargetHasStatus · SelfHasStatus · Self/TargetHealthBelow · TargetIsBoss
     │               DistanceToTargetBelow/Above · IsNight · IsRaining · InRegionTag   (each can Negate)
     ├─ Chance, Cooldown, CostResource + CostAmount
     └─ Actions[≤4]  Damage · ApplyStatus · RemoveStatus · Heal · RestoreResource · Knockback · Pull
                     ModifyStat · Emit(emitter → payload) · CreateArea(area → payload) · CreateBarrier(wall)
                     payload nesting ≤ 3 deep; Emit/CreateArea are the only actions with payloads
```

## Units
- **Distance:** meters. **Time:** seconds. **Speed:** m/s.
- **Damage and heal:** absolute points, where **10 = one starter-weapon hit at tier 0**.
- Status durations are at most 15 s, stacks 1–5.

## JSON (what the LLM writes, what saves store)
camelCase fields, enums as strings, unknown fields are an **error** (malformed LLM output is rejected, never guessed at).

```json
{
  "version": 1,
  "rules": [{
    "name": "Undertow",
    "trigger": { "kind": "OnHit" },
    "conditions": [{ "kind": "TargetHasStatus", "status": "Soaked" }],
    "actions": [
      { "kind": "Pull", "amount": 3 },
      { "kind": "ApplyStatus", "status": "Rooted", "duration": 1.5 }
    ],
    "cooldown": 2
  }]
}
```

## Power budget (`PowerBudget`)
- Programs are priced in **power points (pp)**: 1 pp ≈ one baseline hit every ~4 s.
- `cost = Σ(action costs) × trigger frequency × chance × condition factor × resource-cost relief`
- **Drawbacks have negative cost** (self-damage, debuffing yourself, buffing enemies), so cursed relics can trade a downside for more power.
- **Budget** = `rarityBudget × (1 + 0.5 × tier)`, with tier clamped to 0–10. Rarity budgets: Common 3, Uncommon 6, Rare 10, Epic 16, Legendary 25. At tier 10 this is about 6× tier 0, matching the power bands in 03-combat.
- All coefficients are first-pass guesses, to be tuned in playtests. **Only the lead edits them.**

## Validation (`EffectValidator`)
1. `Validate(program, budget)` returns structural errors plus cost versus budget.
2. A structural error rejects the program; the generator retries or falls back.
3. If it's only over budget, `ScaleToFit(program, budget)` shrinks the beneficial magnitudes (damage, duration, count) and leaves drawbacks alone.

Hard limits: ≤ 4 rules, ≤ 4 actions per list, payload depth ≤ 3, ≤ 24 projectiles (count × (1 + splits)), speed 2–40 m/s, range ≤ 40 m, area radius ≤ 12 m, area duration ≤ 30 s, active cooldown ≥ 0.2 s. Passive rules may only use ModifyStat or CreateArea (auras).

## Readability rules for enemy programs (pillar 7 + combat spec)
Enemy and boss programs must also:
- have `emitter.delay` or `area.delay` ≥ 0.4 s on anything that deals damage (a physical tell plays during it);
- leave a gap: Ring and Cone patterns at tier ≤ 3 must leave at least one opening of ≥ 2× the player's body width;
- mark `airborne: true` for lobbed projectiles (they cast real shadows).
These are checked by the boss generator (stream I), not by the core validator.

## Changing the contract
Streams **request** grammar changes (a new trigger, condition or action) on the board or in their branch notes. The lead adds them to Core with tests, bumps `version` if the JSON changes incompatibly, and announces it. Streams never edit `Assets/Relic/Core`.
