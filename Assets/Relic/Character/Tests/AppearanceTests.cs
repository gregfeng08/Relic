using NUnit.Framework;
using Newtonsoft.Json;

namespace Relic.Character.Tests
{
    public sealed class AppearanceTests
    {
        [Test]
        public void PresetsRoundTripThroughJson()
        {
            foreach (HeroAppearance a in AppearancePresets.All())
            {
                string json = AppearanceJson.Serialize(a);
                HeroAppearance b = AppearanceJson.Deserialize(json);
                Assert.AreEqual(json, AppearanceJson.Serialize(b), a.Name);
                Assert.AreEqual(0, AppearanceRules.Sanitize(b).Count, a.Name + " preset should already be valid");
            }
        }

        [Test]
        public void JsonIsCamelCaseWithStringEnums()
        {
            string json = AppearanceJson.Serialize(AppearancePresets.Mira());
            StringAssert.Contains("\"style\":\"TwinTails\"", json);
            StringAssert.Contains("\"eyeColor\":", json);
        }

        [Test]
        public void OutOfRangeValuesAreClamped()
        {
            var a = new HeroAppearance();
            a.Body.Height = 3f;
            a.Body.Build = -1f;
            a.Face.EyeSize = float.NaN;
            a.Hair.Length = 7f;
            a.Hair.Color = "white";
            a.Outfit.Coat.Length = -0.5f;
            a.Hair.Style = (HairStyle)99;
            for (int i = 0; i < 6; i++) a.Accessories.Add(new AccessorySpec());
            var fixes = AppearanceRules.Sanitize(a);
            Assert.AreEqual(AppearanceRules.MaxHeight, a.Body.Height);
            Assert.AreEqual(0f, a.Body.Build);
            Assert.That(a.Face.EyeSize, Is.InRange(AppearanceRules.MinEyeSize, AppearanceRules.MaxEyeSize));
            Assert.AreEqual(1f, a.Hair.Length);
            Assert.AreEqual(new HairSpec().Color, a.Hair.Color);
            Assert.AreEqual(0f, a.Outfit.Coat.Length);
            Assert.AreEqual(HairStyle.LowTail, a.Hair.Style);
            Assert.AreEqual(AppearanceRules.MaxAccessories, a.Accessories.Count);
            Assert.Greater(fixes.Count, 6);
        }

        [Test]
        public void MissingSectionsAreFilledAndBadDocumentsRejected()
        {
            HeroAppearance a = AppearanceJson.Deserialize("{\"name\":\"Kit\",\"hair\":{\"style\":\"Bob\",\"color\":\"#112233\"}}");
            Assert.AreEqual(HairStyle.Bob, a.Hair.Style);
            Assert.AreEqual("#112233", a.Hair.Color);
            Assert.IsNotNull(a.Outfit.Coat);
            Assert.IsFalse(AppearanceJson.TryDeserialize("{\"hair\":{\"style\":\"Mohawk\"}}", out _, out string e1), "unknown enum");
            Assert.IsNotNull(e1);
            Assert.IsFalse(AppearanceJson.TryDeserialize("{\"wings\":true}", out _, out _), "unknown field");
            Assert.Throws<JsonSerializationException>(() => AppearanceJson.Deserialize("null"));
        }

        [Test]
        public void ExplicitNullsAreRepaired()
        {
            HeroAppearance a = AppearanceJson.Deserialize("{\"body\":null,\"outfit\":{\"coat\":null},\"accessories\":[null]}");
            Assert.IsNotNull(a.Body);
            Assert.IsNotNull(a.Outfit.Coat);
            Assert.AreEqual(0, a.Accessories.Count);
        }
    }
}
