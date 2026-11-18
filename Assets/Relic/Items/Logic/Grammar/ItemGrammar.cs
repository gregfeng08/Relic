using System;
using Relic.Core.Effects;
using System.Collections.Generic;
using Relic.Core.Random;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Resolves the item sentence (17-item-grammar) in order: Base → Archetype → Culture/Maker
    /// → Material → Theme → Quality. Each step has its own purpose-salted generator, so adding
    /// a step later never reshuffles the others. Names and lore are written afterwards from
    /// the sentence (TemplateAuthor, or the LLM with the sentence in its brief).
    /// </summary>
    public static class ItemGrammar
    {
        public const int Version = 1;

        static readonly string[] CommonQuality = { "Worn", "Plain", "Dented", "Serviceable" };
        static readonly string[] UncommonQuality = { "Fine", "Sturdy", "Balanced", "Well-Kept" };
        static readonly string[] RareQuality = { "Fine", "Handsome", "True-Made" };
        static readonly string[] HighQuality = { "Masterwork", "Heirloom" };
        static readonly string[] SomberQuality = { "Mended", "Field-Worn" };

        /// <param name="overused">Optional: true for form nouns overused in recent names (NameFreshness); those forms are a third as likely, so one culture's relics don't all come out as the same object.</param>
        public static ItemSentence Resolve(ulong seed, BaseType bt, Rarity rarity, int tier, WorldRegion home, WorldContext world, LoreBible bible, Func<string, bool> overused = null)
        {
            if (bible == null) throw new ArgumentNullException(nameof(bible));
            var s = new ItemSentence { Base = bt, RegionId = home?.Id, Layers = ItemSentence.LayersFor(rarity) };

            // Archetype: the item's purpose, weighted by base.
            s.Archetype = Archetypes.Pick(bt, seed).Id;
            var arch = Archetypes.ById(s.Archetype);

            // Culture: who lives where it was found, favoring cultures that make this kind of thing.
            var crng = Pcg32.ForPurpose(seed, "grammar.culture");
            var culture = PickCulture(bt, home, bible, ref crng);
            s.Culture = culture.Id;
            s.CultureAdjective = culture.Adjective;

            var forms = CultureForms.For(culture, bt);
            var frng = Pcg32.ForPurpose(seed, "grammar.form");
            if (forms.Count > 0 && rarity >= Rarity.Rare) // the culture's own words come with the real name (Rare+)
            {
                var f = PickForm(forms, overused, ref frng);
                s.Form = f.Form;
                s.Kit = f.Kit;
                s.CultureForm = true;
            }
            else if (bt == BaseType.Relic)
            {
                var k = Forms.RelicForms[frng.Range(0, Forms.RelicForms.Length)];
                s.Form = k.Id.Replace('-', ' ');
                s.Kit = k.Id;
            }
            else s.Form = (rarity == Rarity.Common ? Forms.BaseNoun(bt, ref frng) : Archetypes.Noun(arch, bt, ref frng)).ToLowerInvariant();

            // Maker: named by the culture, working in one of the culture's regions.
            var mrng = Pcg32.ForPurpose(seed, "grammar.maker");
            s.Maker = new NameGenerator(culture, bible.Style).Person(ref mrng);
            var homes = world != null ? world.Regions.FindAll(r => r.Culture == culture.Id) : new List<WorldRegion>();
            var makerHome = homes.Count > 0 ? homes[mrng.Range(0, homes.Count)] : home;
            s.MakerHomeId = makerHome?.Id;
            s.MakerHome = makerHome?.Name ?? new NameGenerator(culture, bible.Style).Place(ref mrng);
            var marks = culture.ItemNaming.MakersMarks;
            s.MakersMark = marks.Count > 0 ? marks[mrng.Range(0, marks.Count)] : null;

            // Material: what the culture works with, if it can make this base at this tier.
            var matRng = Pcg32.ForPurpose(seed, "grammar.material");
            var mat = PickMaterial(bt, s.Form, s.Kit, tier, culture, ref matRng, out string accent);
            s.Material = mat.Name;
            s.Accent = accent;
            s.MaterialTag = mat.TagText;
            s.MaterialStat = mat.Tag;

            // Theme: mood at 70/30 bright/somber, then a motif of the culture that suits it.
            var trng = Pcg32.ForPurpose(seed, "grammar.theme");
            var theme = ThemePicker.Pick(bible, culture, ref trng);
            s.Mood = theme.Mood.Id;
            s.Tone = theme.Tone.ToString();
            s.Motif = theme.Motif.Id;

            var qrng = Pcg32.ForPurpose(seed, "grammar.quality");
            string[] q = rarity == Rarity.Common ? CommonQuality : rarity == Rarity.Uncommon ? UncommonQuality
                : theme.Tone == Tone.Somber ? SomberQuality : rarity == Rarity.Rare ? RareQuality : HighQuality;
            s.Quality = q[qrng.Range(0, q.Length)];
            return s;
        }

        static CultureForms.Entry PickForm(List<CultureForms.Entry> forms, Func<string, bool> overused, ref Pcg32 rng)
        {
            var weights = new int[forms.Count];
            int total = 0;
            for (int i = 0; i < forms.Count; i++)
            {
                var words = forms[i].Form.Split(' ');
                weights[i] = overused != null && overused(words[words.Length - 1].ToLowerInvariant()) ? 1 : 3;
                total += weights[i];
            }
            int r = rng.Range(0, total);
            for (int i = 0; i < forms.Count; i++) { r -= weights[i]; if (r < 0) return forms[i]; }
            return forms[0];
        }

        static CultureDef PickCulture(BaseType bt, WorldRegion home, LoreBible bible, ref Pcg32 rng)
        {
            List<CultureDef> pool = null;
            if (home != null && Enum.TryParse(home.Family, out LoreFamily fam)) pool = bible.CulturesOf(fam);
            if (pool == null || pool.Count == 0) pool = bible.Cultures;
            var weights = new int[pool.Count];
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                int w = home != null && pool[i].Id == home.Culture ? 4 : 1;
                if (CultureForms.Makes(pool[i], bt)) w *= 3;
                weights[i] = w;
                total += w;
            }
            int r = rng.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                r -= weights[i];
                if (r < 0) return pool[i];
            }
            return pool[0];
        }

        /// <summary>Kinds of material a base (or a relic's kit form) can be made of.</summary>
        public static MaterialKind[] KindsFor(BaseType bt, string kit)
        {
            if (bt == BaseType.Relic)
            {
                var f = Forms.FormById(kit);
                if (f != null && f.Kinds.Length > 0) return f.Kinds;
            }
            return Materials.KindsFor(bt);
        }

        static MaterialDef PickMaterial(BaseType bt, string form, string kit, int tier, CultureDef culture, ref Pcg32 rng, out string accent)
        {
            var kinds = KindsFor(bt, kit);
            var accents = new List<string>();
            var own = new List<MaterialDef>();
            foreach (var name in culture.Materials)
            {
                var m = Materials.Find(name);
                if (m == null) continue;
                if (m.Kind == MaterialKind.Accent) accents.Add(m.Name);
                else if (Array.IndexOf(kinds, m.Kind) >= 0 && m.TierMin <= tier) own.Add(m);
            }
            accent = accents.Count > 0 && rng.Chance(0.6f) ? accents[rng.Range(0, accents.Count)] : null;

            // A form that names its material ("whalebone bow") is made of it.
            var named = CultureForms.NamedMaterial(form);
            if (named != null && Array.IndexOf(kinds, named.Kind) >= 0) return named;

            if (own.Count > 0 && rng.Chance(0.75f)) return own[rng.Range(0, own.Count)];
            var generic = Materials.Generic(kinds, tier);
            if (generic.Count > 0) return generic[rng.Range(0, generic.Count)];
            return own.Count > 0 ? own[0] : Materials.Find("iron");
        }

        /// <summary>The theme a sentence names (null when the bible doesn't know it).</summary>
        public static Theme ThemeOf(ItemSentence s, LoreBible bible)
        {
            var motif = bible?.Motif(s.Motif);
            var mood = bible?.Mood(s.Mood);
            return motif == null || mood == null ? null : new Theme { Motif = motif, Mood = mood };
        }

        /// <summary>
        /// Wear from quality, material and mood: worn commons look used, masterworks don't,
        /// soft materials show age, and somber things have been through something.
        /// </summary>
        public static float Wear(ItemSentence s, ref Pcg32 rng)
        {
            float lo, hi;
            switch (s.Quality)
            {
                case "Worn": case "Dented": lo = 0.6f; hi = 0.9f; break;
                case "Plain": case "Serviceable": lo = 0.35f; hi = 0.6f; break;
                case "Mended": case "Field-Worn": lo = 0.45f; hi = 0.75f; break;
                case "Masterwork": case "True-Made": lo = 0.05f; hi = 0.2f; break;
                case "Heirloom": lo = 0.25f; hi = 0.45f; break;
                default: lo = 0.1f; hi = 0.35f; break;
            }
            float w = lo + (hi - lo) * rng.NextFloat() + (Materials.Find(s.Material)?.WearBias ?? 0f);
            return (float)Math.Round(Math.Max(0f, Math.Min(1f, w)), 2);
        }
    }
}
