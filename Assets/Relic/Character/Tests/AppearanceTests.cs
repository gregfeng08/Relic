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
        public void UnwrittenIsTheStartingBody()
        {
            HeroAppearance u = AppearancePresets.Unwritten();
            Assert.AreEqual("Unwritten", AppearancePresets.All()[0].Name, "F11 starts from the unwritten body");
            Assert.AreEqual(FaceStyle.Hint, u.Face.Style);
            Assert.AreEqual(CoatStyle.Cloak, u.Outfit.Coat.Style);
            Assert.IsTrue(u.Outfit.Coat.HoodUp);
            Assert.Greater(u.Body.HeadSize, 1f, "a bigger, younger head");
            Assert.IsFalse(u.Outfit.Pack);
            Assert.IsFalse(u.Outfit.Scarf.On);
            Assert.AreEqual(AccessoryKind.Bookmark, u.Accessories[0].Kind);
            Assert.AreEqual("Unwritten", AppearancePresets.ByName(" unwritten ").Name);
            Assert.AreEqual("Mira", AppearancePresets.ByName("MIRA").Name);
            Assert.IsNull(AppearancePresets.ByName("Nobody"));
            Assert.IsNull(AppearancePresets.ByName(null));
            // Older documents (no style fields) still read as the written defaults.
            HeroAppearance old = AppearanceJson.Deserialize("{\"name\":\"Old\",\"outfit\":{\"coat\":{\"length\":0.5}}}");
            Assert.AreEqual(CoatStyle.Trench, old.Outfit.Coat.Style);
            Assert.IsFalse(old.Outfit.Coat.HoodUp);
            Assert.IsTrue(old.Outfit.Pack);
            Assert.AreEqual(FaceStyle.Full, old.Face.Style);
            Assert.IsFalse(AppearanceJson.TryDeserialize("{\"face\":{\"style\":\"Blank\"}}", out _, out _), "unknown face style");
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
            a.Body.HeadSize = 2f;
            a.Face.EyeSize = float.NaN;
            a.Hair.Length = 7f;
            a.Hair.Color = "white";
            a.Outfit.Coat.Length = -0.5f;
            a.Hair.Style = (HairStyle)99;
            for (int i = 0; i < 6; i++) a.Accessories.Add(new AccessorySpec());
            var fixes = AppearanceRules.Sanitize(a);
            Assert.AreEqual(AppearanceRules.MaxHeight, a.Body.Height);
            Assert.AreEqual(0f, a.Body.Build);
            Assert.AreEqual(AppearanceRules.MaxHeadSize, a.Body.HeadSize);
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
