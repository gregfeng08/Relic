using System;
using System.Collections.Generic;
using System.Linq;
using Relic.Core.Effects;

namespace Relic.Items.Bosses
{
    /// <summary>
    /// Readability rules for boss patterns (03-combat, 05-effect-grammar, 08):
    /// physical wind-ups, gaps in rings and cones, flagged airborne shots, and
    /// no unavoidable damage. Player numbers below are assumptions until the
    /// player controller (stream W) and combat runtime (lead) pin them down.
    /// </summary>
    public static class Readability
    {
        /// <summary>Torso width: the hitbox (03-combat: the body is the hitbox).</summary>
        public const float BodyWidth = 0.6f;
        /// <summary>Assumed run speed without sprinting (m/s).</summary>
        public const float RunSpeed = 6f;
        /// <summary>03-combat: dash ≈ 4 m in 0.18 s.</summary>
        public const float DashDistance = 4f;
        /// <summary>Distance from the source at which gaps are measured (a typical mid-range position).</summary>
        public const float ReferenceDistance = 6f;
        public const float MinWindUp = 0.4f;
        /// <summary>Rings/cones at tier ≤ 3 leave openings ≥ 2 × body width (08).</summary>
        public const int LowTierMax = 3;
        public const float LowTierGapFactor = 2f;
        /// <summary>Beyond tier 3 gaps may tighten, but never below 1.25 × body width (still passable).</summary>
        public const float HighTierGapFactor = 1.25f;
        /// <summary>Seeking shots must be outrun: at most this fraction of run speed.</summary>
        public const float MaxSeekingSpeedFactor = 0.8f;
        /// <summary>Recovery after each pattern in a rotation.</summary>
        public const float MinRecover = 0.5f;
        /// <summary>Emitters released within this window count as one combined wave for the gap rule.</summary>
        public const float SimultaneousWindow = 0.3f;

        static float Rad(float deg) => deg * (float)Math.PI / 180f;

        public static float RequiredGap(int tier) => (tier <= LowTierMax ? LowTierGapFactor : HighTierGapFactor) * BodyWidth;

        /// <summary>Opening between neighbouring projectiles of a ring at the reference distance.</summary>
        public static float RingGap(int count, float size) => (float)(2 * Math.PI * ReferenceDistance / Math.Max(1, count)) - 2f * size;

        /// <summary>Opening between neighbouring projectiles of a cone at the reference distance.</summary>
        public static float ConeGap(int count, float spreadDegrees, float size) =>
            count <= 1 ? float.MaxValue : ReferenceDistance * Rad(spreadDegrees) / (count - 1) - 2f * size;

        public static int MaxRingCount(int tier, float size) =>
            Math.Max(1, (int)Math.Floor(2 * Math.PI * ReferenceDistance / (RequiredGap(tier) + 2f * size)));

        public static int MaxConeCount(int tier, float spreadDegrees, float size) =>
            1 + Math.Max(0, (int)Math.Floor(ReferenceDistance * Rad(spreadDegrees) / (RequiredGap(tier) + 2f * size)));

        /// <summary>How far a player at the reference distance must move sideways to leave a fan of this spread.</summary>
        public static float SidestepNeeded(float spreadDegrees, float size) =>
            ReferenceDistance * (float)Math.Tan(Rad(Math.Min(spreadDegrees * 0.5f, 80f))) + size + BodyWidth * 0.5f;

        /// <summary>How far they can move before the shots arrive (wind-up + travel, running).</summary>
        public static float SidestepAvailable(float delay, float speed) =>
            RunSpeed * (delay + ReferenceDistance / Math.Max(0.1f, speed));

        /// <summary>Distance to leave an area that appears under/around the player.</summary>
        public static float AreaEscapeNeeded(AreaSpec a)
        {
            switch (a.Shape)
            {
                case AreaShape.Circle: return a.Radius + BodyWidth * 0.5f;
                case AreaShape.Cone: return a.Radius * (float)Math.Sin(Rad(Math.Min(a.Angle * 0.5f, 90f))) + BodyWidth * 0.5f;
                case AreaShape.Line: return a.Width * 0.5f + BodyWidth * 0.5f;
                default: return 0f;
            }
        }

        public static float AreaEscapeAvailable(float tell) => RunSpeed * tell + DashDistance;

        public static bool ContainsDamage(List<ActionSpec> actions)
        {
            if (actions == null) return false;
            foreach (var a in actions)
            {
                if (a == null) continue;
                if (a.Kind == ActionKind.Damage && a.Target != TargetKind.Self) return true;
                if (ContainsDamage(a.Payload)) return true;
            }
            return false;
        }

        public static bool IsLockdown(StatusId s) => s == StatusId.Rooted || s == StatusId.Staggered;

        /// <summary>Structure of a pattern without numbers: two patterns with the same signature differ only in tuning.</summary>
        public static string Signature(EffectProgram p)
        {
            var parts = new List<string>();
            foreach (var r in p.Rules) Sig(r.Actions, parts, "");
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        static void Sig(List<ActionSpec> actions, List<string> parts, string prefix)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                string s = prefix + a.Kind;
                if (a.Emitter != null) s += ":" + a.Emitter.Shape + (a.Emitter.Returning ? "+ret" : "") + (a.Emitter.SplitCount > 0 ? "+split" : "") + (a.Emitter.Airborne ? "+air" : "") + (a.Emitter.Seeking ? "+seek" : "");
                if (a.Area != null) s += ":" + a.Area.Shape + (a.Area.FollowsSelf ? "+self" : "");
                if (a.Kind == ActionKind.Damage || a.Kind == ActionKind.ApplyStatus) continue; // payload contents don't change the shape
                parts.Add(s);
                Sig(a.Payload, parts, s + ">");
            }
        }
    }

    public sealed class ReadabilityReport
    {
        public readonly List<string> Errors = new List<string>();
        /// <summary>Patterns with airborne (lobbed) shots: the runtime must give them real shadows.</summary>
        public readonly List<string> AirborneFlags = new List<string>();
        public int DamagingPatterns;
        public float SmallestGap = float.MaxValue;
        public float ShortestWindUp = float.MaxValue;
        public bool Passed => Errors.Count == 0;
        public override string ToString() => Passed ? "readable" : string.Join("; ", Errors);
    }

    public static class ReadabilityChecker
    {
        public static ReadabilityReport Check(BossDefinition boss)
        {
            var rep = new ReadabilityReport();
            if (boss == null) { rep.Errors.Add("no boss"); return rep; }
            CheckStructure(boss, rep);
            var seen = new Dictionary<string, BossPattern>();
            foreach (var phase in boss.Phases)
                foreach (var p in phase.Patterns)
                {
                    seen[p.Name] = p;
                    CheckPattern(p, boss.Tier, rep);
                }

            // Later phases remix earlier patterns: structure must change, not just numbers.
            for (int i = 1; i < boss.Phases.Count; i++)
            {
                var remixes = boss.Phases[i].Patterns.Where(p => p.Remix != null).ToList();
                if (remixes.Count == 0) rep.Errors.Add($"phase {i + 1} has no remix of an earlier pattern");
                foreach (var r in remixes)
                {
                    if (r.RemixOf == null || !seen.TryGetValue(r.RemixOf, out var src)) { rep.Errors.Add($"{r.Name}: remix source \"{r.RemixOf}\" not found"); continue; }
                    if (Readability.Signature(src.Program) == Readability.Signature(r.Program))
                        rep.Errors.Add($"{r.Name}: remix of {src.Name} only changes numbers (same structure)");
                }
            }
            return rep;
        }

        static void CheckStructure(BossDefinition b, ReadabilityReport rep)
        {
            if (b.Phases.Count < 2 || b.Phases.Count > 4) rep.Errors.Add($"{b.Phases.Count} phases (need 2–4)");
            float last = 1.01f;
            for (int i = 0; i < b.Phases.Count; i++)
            {
                var ph = b.Phases[i];
                if (i == 0 && Math.Abs(ph.StartsAtHealth - 1f) > 1e-4f) rep.Errors.Add("phase 1 must start at full health");
                if (ph.StartsAtHealth >= last || ph.StartsAtHealth <= 0f) rep.Errors.Add($"phase {i + 1}: health thresholds must fall strictly and stay above 0");
                last = ph.StartsAtHealth;
                if (ph.Patterns.Count < 2 || ph.Patterns.Count > 4) rep.Errors.Add($"phase {i + 1}: {ph.Patterns.Count} patterns (need 2–4)");
                if (ph.Rotation.Count == 0) rep.Errors.Add($"phase {i + 1}: empty rotation");
                var used = new HashSet<int>();
                foreach (var step in ph.Rotation)
                {
                    if (step.Pattern < 0 || step.Pattern >= ph.Patterns.Count) { rep.Errors.Add($"phase {i + 1}: rotation index {step.Pattern} out of range"); continue; }
                    used.Add(step.Pattern);
                    if (step.Recover < Readability.MinRecover) rep.Errors.Add($"phase {i + 1}: recovery {step.Recover:0.##} s after {ph.Patterns[step.Pattern].Name} is under {Readability.MinRecover} s");
                }
                if (used.Count != ph.Patterns.Count) rep.Errors.Add($"phase {i + 1}: some patterns never appear in the rotation");
            }
        }

        static void CheckPattern(BossPattern p, int tier, ReadabilityReport rep)
        {
            var v = EffectValidator.Validate(p.Program, float.MaxValue);
            foreach (var e in v.Errors) rep.Errors.Add($"{p.Name}: {e}");
            bool damaging = false, airborne = false;
            foreach (var rule in p.Program.Rules)
            {
                if (rule.Trigger == null || rule.Trigger.Kind != TriggerKind.Active)
                    rep.Errors.Add($"{p.Name}: boss patterns must be Active rules fired by the rotation");
                if (Readability.ContainsDamage(rule.Actions)) damaging = true;

                // Ring-family waves released together count as one combined ring.
                var waves = new List<(float delay, int count, float size)>();
                foreach (var a in rule.Actions)
                    CheckTopLevel(a, p, tier, rep, waves, ref airborne);
                waves.Sort((x, y) => x.delay.CompareTo(y.delay));
                for (int i = 0; i < waves.Count; i++)
                {
                    int count = waves[i].count; float size = waves[i].size;
                    for (int j = i + 1; j < waves.Count && waves[j].delay - waves[i].delay <= Readability.SimultaneousWindow; j++)
                    {
                        count += waves[j].count; size = Math.Max(size, waves[j].size);
                    }
                    float gap = Readability.RingGap(count, size);
                    rep.SmallestGap = Math.Min(rep.SmallestGap, gap);
                    if (gap < Readability.RequiredGap(tier))
                        rep.Errors.Add($"{p.Name}: ring of {count} leaves {gap:0.00} m gaps at {Readability.ReferenceDistance} m (needs {Readability.RequiredGap(tier):0.00} m at tier {tier})");
                }
            }
            if (damaging) rep.DamagingPatterns++;
            if (airborne != p.Airborne) rep.Errors.Add($"{p.Name}: airborne flag ({p.Airborne}) doesn't match its emitters ({airborne})");
            if (airborne) rep.AirborneFlags.Add(p.Name);
            bool lobbedBlock = p.Blocks.Contains(PatternBlock.Lobbed);
            if (lobbedBlock && !airborne) rep.Errors.Add($"{p.Name}: lobbed shots must be marked airborne (they cast shadows)");
        }

        static void CheckTopLevel(ActionSpec a, BossPattern p, int tier, ReadabilityReport rep, List<(float, int, float)> waves, ref bool airborne)
        {
            switch (a.Kind)
            {
                case ActionKind.Damage:
                case ActionKind.ApplyStatus:
                case ActionKind.Pull:
                case ActionKind.Knockback:
                case ActionKind.ModifyStat:
                    if (a.Target != TargetKind.Self) rep.Errors.Add($"{p.Name}: {a.Kind} applied directly to a player can't be dodged; deliver it with a projectile or an area");
                    return;
                case ActionKind.Emit:
                    CheckEmit(a, p, tier, rep, waves, 0f, ref airborne);
                    return;
                case ActionKind.CreateArea:
                    CheckArea(a, p, tier, rep, 0f);
                    return;
            }
        }

        static void CheckPayload(List<ActionSpec> payload, BossPattern p, ReadabilityReport rep)
        {
            if (payload == null) return;
            foreach (var a in payload)
            {
                if (a.Kind == ActionKind.ApplyStatus && a.Target != TargetKind.Self && Readability.IsLockdown(a.Status))
                    rep.Errors.Add($"{p.Name}: inflicts {a.Status}, which would make the next attack unavoidable");
                if (a.Kind == ActionKind.Pull && a.Target != TargetKind.Self)
                    rep.Errors.Add($"{p.Name}: pulls players, which can drag them into unavoidable hits");
            }
        }

        static void CheckEmit(ActionSpec a, BossPattern p, int tier, ReadabilityReport rep, List<(float, int, float)> waves, float extraTell, ref bool airborne)
        {
            var e = a.Emitter;
            if (e == null) return;
            bool dmg = Readability.ContainsDamage(a.Payload);
            CheckPayload(a.Payload, p, rep);
            if (e.Airborne) airborne = true;
            if (dmg)
            {
                float tell = e.Delay + extraTell;
                rep.ShortestWindUp = Math.Min(rep.ShortestWindUp, tell);
                if (tell < Readability.MinWindUp) rep.Errors.Add($"{p.Name}: {e.Shape} wind-up {tell:0.##} s is under {Readability.MinWindUp} s");
                if (e.Seeking && e.Speed > Readability.MaxSeekingSpeedFactor * Readability.RunSpeed)
                    rep.Errors.Add($"{p.Name}: seeking shots at {e.Speed} m/s can't be outrun (max {Readability.MaxSeekingSpeedFactor * Readability.RunSpeed:0.#})");
                switch (e.Shape)
                {
                    case EmitShape.Ring:
                    case EmitShape.Spiral:
                        waves.Add((e.Delay + extraTell, e.Count, e.Size));
                        break;
                    case EmitShape.Cone:
                    {
                        float gap = Readability.ConeGap(e.Count, e.SpreadDegrees, e.Size);
                        bool gapOk = gap >= Readability.RequiredGap(tier);
                        bool sidestep = Readability.SidestepNeeded(e.SpreadDegrees, e.Size) <= Readability.SidestepAvailable(tell, e.Speed);
                        if (gapOk) rep.SmallestGap = Math.Min(rep.SmallestGap, gap);
                        if (tier <= Readability.LowTierMax ? !gapOk : !(gapOk || sidestep))
                            rep.Errors.Add($"{p.Name}: cone of {e.Count} over {e.SpreadDegrees}° leaves {gap:0.00} m gaps (needs {Readability.RequiredGap(tier):0.00} m{(tier <= Readability.LowTierMax ? "" : ", or room to step out of the fan")})");
                        break;
                    }
                    case EmitShape.Orbit:
                        rep.Errors.Add($"{p.Name}: boss orbit patterns aren't supported (no readable gap rule yet)");
                        break;
                    default: // Single, Line, Burst: aimed; dodge by stepping sideways
                    {
                        float spread = e.Shape == EmitShape.Burst ? e.SpreadDegrees : 0f;
                        if (!e.Seeking && Readability.SidestepNeeded(spread, e.Size) > Readability.SidestepAvailable(tell, e.Speed))
                            rep.Errors.Add($"{p.Name}: aimed {e.Shape} can't be sidestepped in time");
                        break;
                    }
                }
            }
            if (a.Payload != null)
                foreach (var inner in a.Payload)
                {
                    // A puddle left where a shot lands: its tell includes the flight (the shadow shows where).
                    if (inner.Kind == ActionKind.CreateArea) CheckArea(inner, p, tier, rep, e.Delay + extraTell + e.Range / Math.Max(0.1f, e.Speed));
                    if (inner.Kind == ActionKind.Emit) CheckEmit(inner, p, tier, rep, waves, e.Delay + extraTell + e.Range / Math.Max(0.1f, e.Speed), ref airborne);
                }
        }

        static void CheckArea(ActionSpec a, BossPattern p, int tier, ReadabilityReport rep, float extraTell)
        {
            var ar = a.Area;
            if (ar == null) return;
            CheckPayload(a.Payload, p, rep);
            if (!Readability.ContainsDamage(a.Payload)) return;
            float tell = ar.Delay;
            rep.ShortestWindUp = Math.Min(rep.ShortestWindUp, tell + extraTell);
            if (ar.Delay < Readability.MinWindUp) rep.Errors.Add($"{p.Name}: {ar.Shape} area tell {ar.Delay:0.##} s is under {Readability.MinWindUp} s");
            if (Readability.AreaEscapeNeeded(ar) > Readability.AreaEscapeAvailable(tell + extraTell))
                rep.Errors.Add($"{p.Name}: {ar.Shape} area can't be escaped during its tell");
            if (ar.Shape == AreaShape.Cone)
            {
                float opening = Readability.ReferenceDistance * (float)((360f - ar.Angle) * Math.PI / 180f);
                if (opening < Readability.RequiredGap(tier)) rep.Errors.Add($"{p.Name}: cone area leaves no opening");
            }
            if (a.Payload != null)
                foreach (var inner in a.Payload)
                    if (inner.Kind == ActionKind.CreateArea || inner.Kind == ActionKind.Emit)
                        rep.Errors.Add($"{p.Name}: nested emitters inside areas aren't checked; keep boss areas simple");
        }
    }
}
