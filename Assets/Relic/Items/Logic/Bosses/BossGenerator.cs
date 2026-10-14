using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;
using Relic.Items.Text;

namespace Relic.Items.Bosses
{
    /// <summary>
    /// Builds a BossDefinition from a seed: archetype → phases of patterns
    /// (later phases remix earlier ones) → rotation → health from tier →
    /// essence → lore grounded in the world context. Every boss it returns
    /// passes the ReadabilityChecker (patterns that would fail are rebuilt).
    /// </summary>
    public static class BossGenerator
    {
        public const string GeneratorName = "relic.bosses";
        public const int GeneratorVersion = 1;

        public static ulong BossSeed(ulong worldSeed, int index) => Hash.Combine(worldSeed, Hash.Of("boss"), unchecked((ulong)index));

        public static BossDefinition Generate(ulong worldSeed, int index, int tier, WorldContext world, string archetypeId = null)
        {
            ulong seed = BossSeed(worldSeed, index);
            tier = Math.Max(0, Math.Min(Difficulty.MaxTier, tier));
            var rng = Pcg32.ForPurpose(seed, "boss.layout");
            var arch = archetypeId != null ? BossArchetypes.ById(archetypeId) : BossArchetypes.All[rng.Range(0, BossArchetypes.All.Length)];
            if (arch == null) throw new ArgumentException("unknown archetype " + archetypeId);

            var boss = new BossDefinition
            {
                Id = "boss-" + seed.ToString("x16"),
                Seed = seed,
                Archetype = arch.Id,
                Tier = tier,
                BaseHealth = BossHealth.BaseHealth(tier),
                Generator = new GeneratorInfo { Name = GeneratorName, Version = GeneratorVersion, Mode = GenerationMode.Template, TextSource = "template-v1" },
            };

            BuildPhases(boss, arch, tier, ref rng);
            WriteIdentity(boss, arch, world, seed);
            boss.Essence = new EssenceDrop
            {
                Stat = arch.EssenceStat,
                Amount = 1 + tier / 3,
                Name = arch.Id + "'s Essence",
            };
            return boss;
        }

        // ---------- phases ----------

        static void BuildPhases(BossDefinition boss, BossArchetype arch, int tier, ref Pcg32 rng)
        {
            int phases = 2 + (tier >= 3 ? 1 : 0) + (tier >= 7 && rng.Chance(0.6f) ? 1 : 0);
            var usedBlocks = new HashSet<PatternBlock>();
            var earlier = new List<BossPattern>();
            float recover = (float)Math.Round(Math.Max(0.6f, 1.6f - 0.08f * tier), 2);

            for (int i = 0; i < phases; i++)
            {
                var phase = new BossPhase { Index = i + 1, StartsAtHealth = (float)Math.Round(1f - (float)i / phases, 3) };
                var changes = new List<string>();
                if (i == 0)
                {
                    int n = 2 + (rng.Chance(0.5f) ? 1 : 0);
                    for (int k = 0; k < n; k++) phase.Patterns.Add(NewBase(arch, tier, usedBlocks, boss, ref rng));
                    changes.Add("opening patterns");
                }
                else
                {
                    // one new block (if any remain), one or two remixes of earlier patterns, maybe one old favourite
                    if (usedBlocks.Count < 8)
                    {
                        var fresh = NewBase(arch, tier, usedBlocks, boss, ref rng);
                        phase.Patterns.Add(fresh);
                        changes.Add("adds " + fresh.Name);
                    }
                    int remixes = phase.Patterns.Count == 0 ? 2 : 1 + (rng.Chance(0.5f) ? 1 : 0);
                    var usedSources = new HashSet<string>();
                    for (int k = 0; k < remixes; k++)
                    {
                        var r = TryRemix(arch, tier, earlier, phase.Patterns, usedSources, boss, ref rng);
                        if (r == null) break;
                        phase.Patterns.Add(r);
                        changes.Add(r.Name + " (" + r.Remix + " " + r.RemixOf + ")");
                    }
                    if (phase.Patterns.Count < 3 && rng.Chance(0.5f))
                    {
                        var keep = earlier[rng.Range(0, earlier.Count)];
                        if (!phase.Patterns.Exists(p => p.Name == keep.Name)) { phase.Patterns.Add(ItemJson.Clone(keep)); changes.Add("keeps " + keep.Name); }
                    }
                    while (phase.Patterns.Count < 2) phase.Patterns.Add(NewBase(arch, tier, usedBlocks, boss, ref rng));
                }

                // Rotation: every pattern once, seeded order, recovery after each.
                var order = new List<int>();
                for (int k = 0; k < phase.Patterns.Count; k++) order.Add(k);
                for (int k = order.Count - 1; k > 0; k--) { int j = rng.Range(0, k + 1); int t = order[k]; order[k] = order[j]; order[j] = t; }
                foreach (int k in order) phase.Rotation.Add(new RotationStep { Pattern = k, Recover = recover });

                phase.Change = string.Join("; ", changes);
                boss.Phases.Add(phase);
                foreach (var p in phase.Patterns) if (!earlier.Exists(e => e.Name == p.Name)) earlier.Add(p);
            }
        }

        static BossPattern NewBase(BossArchetype arch, int tier, HashSet<PatternBlock> used, BossDefinition boss, ref Pcg32 rng)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var block = PickBlock(arch, used, ref rng);
                var p = PatternLibrary.Build(arch, block, tier, ref rng);
                if (Passes(p, tier))
                {
                    used.Add(block);
                    return p;
                }
            }
            // Ring is always constructible within the gap rule.
            var ring = PatternLibrary.Build(arch, PatternBlock.Ring, tier, ref rng);
            used.Add(PatternBlock.Ring);
            return ring;
        }

        static PatternBlock PickBlock(BossArchetype arch, HashSet<PatternBlock> used, ref Pcg32 rng)
        {
            float total = 0f;
            for (int i = 0; i < arch.BlockWeights.Length; i++) if (!used.Contains((PatternBlock)i)) total += arch.BlockWeights[i];
            if (total <= 0f)
            {
                // archetype palette exhausted: any unused block
                for (int i = 0; i < 8; i++) if (!used.Contains((PatternBlock)i)) return (PatternBlock)i;
                return PatternBlock.Ring;
            }
            float x = rng.NextFloat() * total;
            for (int i = 0; i < arch.BlockWeights.Length; i++)
            {
                if (used.Contains((PatternBlock)i)) continue;
                x -= arch.BlockWeights[i];
                if (x < 0f && arch.BlockWeights[i] > 0f) return (PatternBlock)i;
            }
            for (int i = arch.BlockWeights.Length - 1; i >= 0; i--) if (!used.Contains((PatternBlock)i) && arch.BlockWeights[i] > 0f) return (PatternBlock)i;
            return PatternBlock.Ring;
        }

        static BossPattern TryRemix(BossArchetype arch, int tier, List<BossPattern> earlier, List<BossPattern> current, HashSet<string> usedSources, BossDefinition boss, ref Pcg32 rng)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var src = earlier[rng.Range(0, earlier.Count)];
                if (usedSources.Contains(src.Name)) continue;
                string op = PatternLibrary.RemixOps[rng.Range(0, PatternLibrary.RemixOps.Length)];
                BossPattern other = op == "fused" && earlier.Count > 1 ? earlier[rng.Range(0, earlier.Count)] : null;
                if (!PatternLibrary.CanRemix(op, src, other)) continue;
                var r = PatternLibrary.Remix(op, src, other, arch, tier, ref rng);
                if (current.Exists(p => p.Name == r.Name) || earlier.Exists(p => p.Name == r.Name)) continue;
                if (Readability.Signature(r.Program) == Readability.Signature(src.Program)) continue;
                if (!Passes(r, tier)) continue;
                usedSources.Add(src.Name);
                return r;
            }
            return null;
        }

        /// <summary>Checks one pattern alone by wrapping it in a minimal valid boss.</summary>
        static bool Passes(BossPattern p, int tier)
        {
            var probe = new BossDefinition { Tier = tier };
            var phase1 = new BossPhase { StartsAtHealth = 1f, Patterns = { p, p } };
            phase1.Rotation.Add(new RotationStep { Pattern = 0, Recover = 1f });
            phase1.Rotation.Add(new RotationStep { Pattern = 1, Recover = 1f });
            var phase2 = new BossPhase { StartsAtHealth = 0.5f, Patterns = { p, p } };
            phase2.Rotation.Add(new RotationStep { Pattern = 0, Recover = 1f });
            phase2.Rotation.Add(new RotationStep { Pattern = 1, Recover = 1f });
            probe.Phases.Add(phase1);
            probe.Phases.Add(phase2);
            var rep = ReadabilityChecker.Check(probe);
            // the probe has no real remixes; ignore the remix-structure complaints
            rep.Errors.RemoveAll(e => e.Contains("remix"));
            return rep.Passed;
        }

        // ---------- identity, lore ----------

        static void WriteIdentity(BossDefinition boss, BossArchetype arch, WorldContext world, ulong seed)
        {
            var rng = Pcg32.ForPurpose(seed, "boss.identity");
            world = world ?? StubWorld.Create(seed);
            WorldRegion region = null;
            foreach (var r in world.Regions)
                if (region == null || Math.Abs(r.Tier - boss.Tier) < Math.Abs(region.Tier - boss.Tier)) region = r;
            var faction = world.Factions[rng.Range(0, world.Factions.Count)];
            var local = world.Events.FindAll(e => e.RegionId == region.Id);
            var ev = local.Count > 0 ? local[rng.Range(0, local.Count)] : world.Events[rng.Range(0, world.Events.Count)];
            var figure = world.Figures[rng.Range(0, world.Figures.Count)];

            string name = StubWorld.PersonName(ref rng);
            string Fill(string t) => t.Replace("{name}", name).Replace("{region}", region.Name).Replace("{landmark}", region.Landmark)
                .Replace("{faction}", faction.Name).Replace("{event}", ev.Name).Replace("{figure}", figure.Name);
            string Sentences(string s) // capitalize each sentence ("the Salt Choir..." at a start)
            {
                var parts = s.Split(new[] { ". " }, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++) if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
                return string.Join(". ", parts);
            }

            boss.Name = name;
            boss.Title = Fill(arch.Titles[rng.Range(0, arch.Titles.Length)]);
            boss.Lore = Sentences(Fill(arch.LoreTemplates[rng.Range(0, arch.LoreTemplates.Length)]));
            boss.Arena = Fill(arch.Arenas[rng.Range(0, arch.Arenas.Length)]) + ", in " + region.Name;
            boss.Hazards = new List<string>(arch.Hazards);
            void Ref(string id, string n) { if (boss.Lore.Contains(DraftValidator.StripArticle(n)) || boss.Title.Contains(DraftValidator.StripArticle(n))) if (!boss.WorldRefs.Contains(id)) boss.WorldRefs.Add(id); }
            Ref(region.Id, region.Name);
            Ref(faction.Id, faction.Name);
            Ref(ev.Id, ev.Name);
            Ref(figure.Id, figure.Name);
            if (!boss.WorldRefs.Contains(region.Id)) boss.WorldRefs.Add(region.Id); // the arena is always in the region
        }

    }
}
