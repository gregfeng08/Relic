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

## Power budget (`PowerBudget`), calibration v2 (2026-10-13)
- Programs are priced in **power points (pp)**: 1 pp ≈ one baseline hit (10 damage) every ~4 s. A starter sword's plain swings are about 5–6 pp.
- `cost = Σ(action costs) × trigger frequency × chance × condition factor`. **Resource costs (mana, resonance) don't discount power.** Cooldown already prices frequency, and mana is a separate, in-game limit.
- **Trigger frequency** is relative to an Active ability on a 4 s cooldown (= 1). **OnHit is 6** (about 1.5 basic attacks per second, measured), Periodic is `4 / interval`, and a cooldown caps any trigger at `4 / cooldown`.
- **Drawbacks have negative cost** (self-damage, debuffing yourself, buffing enemies), so cursed relics can trade a downside for more power. Self-damage drawbacks must stay survivable: about 2% of a tier-appropriate HP pool per proc.
- **Budgets:**
  - `TierFactor(tier) = 1 + 0.2·tier` (3× at tier 10). It's shared with weapon base damage.
  - `For(rarity, tier)` is the **bonus** budget: Common 0.5, Uncommon 0.9, Rare 1.4, Epic 2.0, Legendary 3.0, each × TierFactor.
  - **Kit allowances** on top of that: `ActiveSkill(tier)` = 1.6 × TierFactor for a weapon's skill (E); `Ultimate(tier)` = 2.0 × TierFactor for a relic's ultimate (Q); `Spell(tier)` = 14 × TierFactor for a drawn spell circle (weapon-scale, since a tome's pages are its attack; mana holds back the top end).
  - Items validate against `ItemGenerator.Budget` = bonus + their slot's kit allowance.
- **Verified end to end** by `Relic.Game.Tests.PowerBandTests`: items from the real generator, fought through `Relic.Combat` with `BalanceSim`. A tier-10 kit (Epic sword, Legendary relic, two Epic trinkets, Epic armor, +12 trained STR/INT) kills a boss **≈ 7× faster** than a Common starter sword (target 6×, accepted 4.5–8×). A stock character still kills anything, just slowly.
- **Only the lead edits these coefficients.** Re-run PowerBandTests after any change.

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
