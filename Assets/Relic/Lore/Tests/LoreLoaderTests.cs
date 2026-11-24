using System.Collections.Generic;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>The loader is strict: bad data is refused with a message, never guessed at.</summary>
    public class LoreLoaderTests
    {
        static LoreBible Load(Dictionary<string, string> raw) => LoreBible.FromJson(raw);

        [Test]
        public void RawDocumentsLoadTheSameAsTheDirectory()
        {
            var b = Load(LoreTestData.Raw());
            Assert.AreEqual(LoreJson.Serialize(LoreTestData.Bible.CultureTemplates), LoreJson.Serialize(b.CultureTemplates));
            Assert.AreEqual(LoreJson.Serialize(LoreTestData.Bible.ForWorld(5).Cultures), LoreJson.Serialize(b.ForWorld(5).Cultures));
        }

        [Test]
        public void TheConstitutionHoldsNoStory()
        {
            var b = LoreTestData.Bible;
            Assert.IsFalse(b.IsWorld);
            Assert.IsEmpty(b.Cultures, "cultures are generated per world");
            Assert.IsEmpty(b.Eras, "eras are generated per world");
            Assert.IsEmpty(b.Legends, "legends are generated per world");
        }

        [Test]
        public void MissingDocumentIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw.Remove(LoreBible.FormsFile);
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains(LoreBible.FormsFile, ex.Message);
        }

        [Test]
        public void UnknownFieldIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.StyleFile] = raw[LoreBible.StyleFile].Replace("\"maxSentenceWords\"", "\"maxSentenceWordz\"");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains("style.json", ex.Message);
        }

        [Test]
        public void UnknownEnumIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.MotifsFile] = raw[LoreBible.MotifsFile].Replace("\"Soaked\"", "\"Drenched\"");
            Assert.Throws<LoreDataException>(() => Load(raw));
        }

        [Test]
        public void UnknownRegionFamilyIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.CultureTemplatesFile] = raw[LoreBible.CultureTemplatesFile].Replace("\"families\": [\"Meadow\"", "\"families\": [\"Volcano\", \"Meadow\"");
            Assert.Throws<LoreDataException>(() => Load(raw));
        }

        [Test]
        public void DuplicateIdIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.CultureTemplatesFile] = raw[LoreBible.CultureTemplatesFile].Replace("\"id\": \"grove-tenders\"", "\"id\": \"meadow-growers\"");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains("duplicate", ex.Message);
        }

        [Test]
        public void DanglingReferenceIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.EventsFile] = raw[LoreBible.EventsFile].Replace("\"eras\": [\"strife\"]", "\"eras\": [\"no-such-era\"]");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains("no-such-era", ex.Message);
        }

        [Test]
        public void UntaggedFormIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.CultureTemplatesFile] = raw[LoreBible.CultureTemplatesFile].Replace("\"pie-tin buckler\"", "\"pie-tin hat\"");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains("pie-tin hat", ex.Message);
        }

        [Test]
        public void TemplateWithoutMetalsIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.CultureTemplatesFile] = raw[LoreBible.CultureTemplatesFile].Replace("\"metals\": [\"brass\", \"pewter\", \"iron\"]", "\"metals\": []");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
            StringAssert.Contains("metals", ex.Message);
        }

        [Test]
        public void UndeclaredTemplateSlotIsRejected()
        {
            var raw = LoreTestData.Raw();
            raw[LoreBible.EventsFile] = raw[LoreBible.EventsFile].Replace("The Founding of {place}", "The Founding of {town}");
            var ex = Assert.Throws<LoreDataException>(() => Load(raw));
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
