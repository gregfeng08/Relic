using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Relic.Core.Random;

namespace Relic.Lore.Tests
{
    public class NameGeneratorTests
    {
        static LoreBible B => LoreTestData.Bible;
        static List<CultureDef> Cultures => _cultures ?? (_cultures = LoreTestData.SweepCultures());
        static List<CultureDef> _cultures;
        static readonly Regex Letters = new Regex("^[A-Za-z]+$");

        static NameGenerator Gen(CultureDef c) => new NameGenerator(c, B.Style);

        [Test]
        public void NamesAreDeterministicPerSeed()
        {
            foreach (var c in Cultures)
            {
                var a = Gen(c);
                var b = Gen(c);
                for (ulong seed = 0; seed < 200; seed++)
                {
                    Assert.AreEqual(a.Person(seed), b.Person(seed), c.Id + " person " + seed);
                    Assert.AreEqual(a.Place(seed), b.Place(seed), c.Id + " place " + seed);
                }
                var r1 = new Pcg32(77);
                var r2 = new Pcg32(77);
                for (int i = 0; i < 50; i++) Assert.AreEqual(a.Person(ref r1), b.Person(ref r2));
            }
        }

        [Test]
        public void CulturesSoundDifferentFromEachOther()
        {
            foreach (var world in LoreTestData.Seeds)
            foreach (var c in LoreTestData.World(world).Cultures)
            {
                var others = LoreTestData.World(world).Cultures.FindAll(o => o.Id != c.Id);
                int same = 0;
                for (ulong seed = 0; seed < 100; seed++)
                    foreach (var o in others) if (Gen(c).Person(seed) == Gen(o).Person(seed)) same++;
                Assert.Less(same, 3, c.Id + " shares too many names with other cultures");
            }
        }

        [Test]
        public void NamesVary()
        {
            foreach (var c in Cultures)
            {
                var g = Gen(c);
                var people = new HashSet<string>();
                var places = new HashSet<string>();
                for (ulong seed = 0; seed < 300; seed++) { people.Add(g.Person(seed)); places.Add(g.Place(seed)); }
                Assert.Greater(people.Count, 200, c.Id + " people");
                Assert.Greater(places.Count, 200, c.Id + " places");
            }
        }

        [Test]
        public void NamesArePlausible()
        {
            foreach (var c in Cultures)
            {
                var g = Gen(c);
                var p = c.Phonology;
                for (ulong seed = 0; seed < 2000; seed++)
                {
                    string person = g.Person(seed);
                    var words = person.Split(' ');
                    Assert.LessOrEqual(words.Length, 2, person);
                    foreach (var w in words)
                    {
                        Assert.IsTrue(Letters.IsMatch(w), c.Id + ": '" + person + "' has non-letters");
                        Assert.That(w.Length, Is.InRange(p.MinLength, p.MaxLength), c.Id + ": '" + w + "' length");
                        Assert.IsTrue(char.IsUpper(w[0]), person);
                        Assert.IsFalse(Regex.IsMatch(w.ToLowerInvariant(), @"(.)\1\1"), person + " has a tripled letter");
                    }
                    AssertClean(c, person);

                    string place = g.Place(seed);
                    foreach (var w in place.Split(' '))
                    {
                        Assert.IsTrue(Letters.IsMatch(w), c.Id + ": place '" + place + "' has non-letters");
                        Assert.That(w.Length, Is.InRange(p.MinLength, p.MaxLength + NameGenerator.PlaceExtraLength), c.Id + ": place '" + place + "' length");
                    }
                    foreach (var w in place.Split(' '))
                        if (!p.Places.Words.Contains(w)) AssertClean(c, w); // fixed words like "Pass" are curated, not generated
                }
            }
        }

        static void AssertClean(CultureDef c, string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (var bad in B.Style.BannedNameSubstrings)
                Assert.IsFalse(lower.Contains(bad), c.Id + ": '" + name + "' contains banned '" + bad + "'");
        }

        [Test]
        public void FallbackNamesArePlausible()
        {
            foreach (var c in Cultures)
            {
                var g = Gen(c);
                Assert.IsTrue(g.IsPlausible(g.Fallback()), c.Id + " fallback '" + g.Fallback() + "'");
            }
        }

        [Test]
        public void PhonologyFamiliesSoundDistinct()
        {
            // Whole families: across 300 seeds, two families almost never produce the same name.
            var fams = B.Phonologies;
            Assert.That(fams.Count, Is.GreaterThanOrEqualTo(5), "phonology families");
            var sets = new List<HashSet<string>>();
            foreach (var f in fams)
            {
                var g = Gen(new CultureDef { Id = "fam-" + f.Id, Phonology = f.Phonology });
                var set = new HashSet<string>();
                for (ulong seed = 0; seed < 300; seed++) set.Add(g.Person(seed).Split(' ')[0]);
                Assert.Greater(set.Count, 150, f.Id + " names vary");
                foreach (var n in set) Assert.IsTrue(g.IsPlausible(n), f.Id + ": " + n);
                sets.Add(set);
            }
            for (int i = 0; i < sets.Count; i++)
                for (int j = i + 1; j < sets.Count; j++)
                {
                    var shared = new HashSet<string>(sets[i]);
                    shared.IntersectWith(sets[j]);
                    Assert.Less(shared.Count, sets[i].Count / 10, fams[i].Id + " and " + fams[j].Id + " share too many names");
                }
        }

        [Test]
        public void SeamVowelsMerge()
        {
            var c = new CultureDef { Id = "test" };
            c.Phonology.Starts.Add("Ama");
            c.Phonology.Ends.Add("ano");
            c.Phonology.Patterns.Add(new SyllablePattern { Shape = "SE", Weight = 1 });
            c.Phonology.Places.Words.Add("Cove");
            var g = new NameGenerator(c, new StyleRules());
            Assert.AreEqual("Amano", g.Person(1));
            Assert.AreEqual("Amano Cove", g.Place(1));
        }
    }
}
