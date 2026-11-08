using System.Collections.Generic;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>The loader is strict: bad data is refused with a message, never guessed at.</summary>
    public class LoreLoaderTests
    {
        [Test]
        public void RawDocumentsLoadTheSameAsTheDirectory()
        {
            var b = LoreTestData.Raw().Load();
            Assert.AreEqual(LoreTestData.Bible.Cultures.Count, b.Cultures.Count);
            Assert.AreEqual(LoreJson.Serialize(LoreTestData.Bible.Cultures), LoreJson.Serialize(b.Cultures));
        }

        [Test]
        public void CultureOrderDoesNotDependOnInputOrder()
        {
            var raw = LoreTestData.Raw();
            raw.Cultures.Reverse();
            var b = raw.Load();
            for (int i = 1; i < b.Cultures.Count; i++)
                Assert.Less(string.CompareOrdinal(b.Cultures[i - 1].Id, b.Cultures[i].Id), 0);
        }

        [Test]
        public void UnknownFieldIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Style = raw.Style.Replace("\"maxSentenceWords\"", "\"maxSentenceWordz\"");
            var ex = Assert.Throws<LoreDataException>(() => raw.Load());
            StringAssert.Contains("style.json", ex.Message);
        }

        [Test]
        public void UnknownEnumIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Motifs = raw.Motifs.Replace("\"Soaked\"", "\"Drenched\"");
            Assert.Throws<LoreDataException>(() => raw.Load());
        }

        [Test]
        public void UnknownRegionFamilyIsRejected()
        {
            var raw = LoreTestData.Raw();
            var first = raw.Cultures[0];
            raw.Cultures[0] = new KeyValuePair<string, string>(first.Key, first.Value.Replace("\"families\": [\"", "\"families\": [\"Volcano\", \""));
            Assert.Throws<LoreDataException>(() => raw.Load());
        }

        [Test]
        public void DuplicateIdIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Cultures.Add(raw.Cultures[0]);
            var ex = Assert.Throws<LoreDataException>(() => raw.Load());
            StringAssert.Contains("duplicate", ex.Message);
        }

        [Test]
        public void DanglingReferenceIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Legends = raw.Legends.Replace("\"related\": [\"iron-sentence\"]", "\"related\": [\"no-such-legend\"]");
            var ex = Assert.Throws<LoreDataException>(() => raw.Load());
            StringAssert.Contains("no-such-legend", ex.Message);
        }

        [Test]
        public void UndeclaredTemplateSlotIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Events = raw.Events.Replace("The Founding of {place}", "The Founding of {town}");
            var ex = Assert.Throws<LoreDataException>(() => raw.Load());
            StringAssert.Contains("{town}", ex.Message);
        }

        [Test]
        public void TemplateFillIsStrict()
        {
            var values = new Dictionary<string, string> { ["place"] = "Bramwick" };
            Assert.AreEqual("The Founding of Bramwick", TemplateText.Fill("The Founding of {place}", values));
            Assert.Throws<System.ArgumentException>(() => TemplateText.Fill("{founder} of {place}", values));
            CollectionAssert.AreEqual(new[] { "founder", "place" }, TemplateText.SlotsIn("{founder} of {place}, {founder}"));
            Assert.IsTrue(TemplateText.HasStrayBraces("{ place}"));
        }
    }
}
