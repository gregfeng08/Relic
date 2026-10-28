using NUnit.Framework;
using Relic.Items;
using UnityEditor;
using UnityEngine;

namespace Relic.Character.EditTests
{
    /// <summary>
    /// Edit-mode smoke tests for the procedural hero: random kits build without errors and stay
    /// inside the triangle budget, every pose runs, and a floating-origin jump doesn't fling the
    /// hair/coat chains. (Folder is EditTests, not Tests, so the dotnet harness skips it.)
    /// </summary>
    public sealed class HeroBuildTests
    {
        public const int TriangleBudget = 15000;
        GameObject _go;
        HeroAvatar _hero;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Hero Under Test");
            _hero = _go.AddComponent<HeroAvatar>();
            _hero.ToonMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Relic/World/Materials/ToonCharacter.mat");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void PlainHeroBuilds()
        {
            _hero.Kit = new HeroKit();
            _hero.Rebuild();
            Assert.IsNotNull(_hero.Renderer);
            Assert.Greater(_hero.TriangleCount, 3000);
            Assert.LessOrEqual(_hero.TriangleCount, TriangleBudget);
        }

        [Test]
        public void RandomKitsBuildWithinBudget()
        {
            var roller = new KitRoller(2024);
            for (int roll = 0; roll < 12; roll++)
            {
                _hero.Kit = roller.Roll(roll % 11, roll);
                _hero.WeaponDrawn = roll % 4 == 3;
                _hero.Rebuild();
                Mesh m = _hero.Renderer.sharedMesh;
                Assert.AreEqual(m.vertexCount, m.boneWeights.Length);
                Assert.AreEqual(_hero.Rig.Bones.Count, m.bindposes.Length);
                Assert.LessOrEqual(_hero.TriangleCount, TriangleBudget, "roll " + roll + ": " + _hero.Breakdown);
            }
        }

        [Test]
        public void EveryArmorWeightAndWeaponFitsTheBudget()
        {
            var roller = new KitRoller(7);
            BaseType[] weapons = { BaseType.Sword, BaseType.Greatsword, BaseType.Bow, BaseType.Staff, BaseType.Tome };
            int roll = 0;
            foreach (BaseType w in weapons)
                foreach (ArmorWeight a in new[] { ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy })
                {
                    _hero.Kit = roller.Roll(5, roll++, w, BaseType.HeavyArmor);
                    _hero.ArmorOverride = a;
                    _hero.Rebuild();
                    Assert.AreEqual(a, _hero.Look.Armor.Weight);
                    Assert.LessOrEqual(_hero.TriangleCount, TriangleBudget, w + "/" + a + ": " + _hero.Breakdown);
                }
        }

        [Test]
        public void AllPosesAnimateWithoutNaNs()
        {
            _hero.Kit = new KitRoller(3).Roll(2, 1);
            _hero.Rebuild();
            foreach (HeroMotion motion in System.Enum.GetValues(typeof(HeroMotion)))
            {
                _hero.Input = new HeroAnimInput { Motion = motion, Speed = 7f, Sprint = true, VerticalSpeed = -3f, ClimbSpeed = 2f };
                for (int f = 0; f < 40; f++) _hero.Animate(f % 2 == 0 ? 1f / 144f : 1f / 20f, new Vector3(0f, 0f, 7f));
                foreach (Transform t in _hero.Rig.Bones)
                    Assert.IsFalse(float.IsNaN(t.position.x) || float.IsNaN(t.rotation.w), motion + " " + t.name);
            }
        }

        [Test]
        public void OriginShiftDoesNotFlingChains()
        {
            _hero.Kit = new KitRoller(3).Roll(2, 1);
            _hero.Rebuild();
            for (int f = 0; f < 30; f++) _hero.Animate(1f / 60f, Vector3.zero);
            // Floating origin recenters: the whole player jumps 4 km in one frame.
            _go.transform.position += new Vector3(-4096f, 0f, 4096f);
            _hero.Animate(1f / 60f, Vector3.zero);
            Vector3 root = _go.transform.position;
            foreach (Transform t in _hero.Rig.Bones)
                Assert.Less((t.position - root).magnitude, 2.5f, t.name + " was left behind");
        }
    }
}
