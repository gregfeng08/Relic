using System.Collections.Generic;

namespace Relic.Magic
{
    /// <summary>
    /// Example pages, as found in tomes or taught at the desk. Recipes, not
    /// programs: the drawing and the program both come from the glyphs.
    /// </summary>
    public static class PrewrittenSpells
    {
        public sealed class Page
        {
            public readonly string Title;
            public readonly string Recipe;
            public readonly string Note;

            public Page(string title, string recipe, string note)
            {
                Title = title;
                Recipe = recipe;
                Note = note;
            }
        }

        public static readonly IReadOnlyList<Page> All = new List<Page>
        {
            new Page("Tide Seekers", "water | split seek | line", "Three soaking bolts that curve toward their targets (the spellcraft example)."),
            new Page("Ember Nova", "flame | more | ring", "A ring of burning bolts in every direction."),
            new Page("Stone Rampart", "stone | lasting | line wall", "A standing wall that staggers whatever touches it."),
            new Page("Gale Fan", "wind | far | cone", "A wide fan of gusts that blow enemies back."),
            new Page("Dawn Halo", "light | lasting | orbit", "Dazzling motes that circle you."),
            new Page("Patient Bramble", "growth | heavy | delay line return", "A slow, heavy bolt that waits, flies out, roots, and comes back."),
            new Page("Quick Drizzle", "water | less swift | line", "A cheap, fast little bolt."),
            new Page("Cinder Storm", "flame | split | ring delay line", "A burst of flame, then three bolts a moment later."),
        };
    }
}
