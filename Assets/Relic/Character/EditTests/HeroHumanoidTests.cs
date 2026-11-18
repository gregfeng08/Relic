using System.Collections.Generic;
using NUnit.Framework;
using Relic.Character.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Relic.Character.EditTests
{
    /// <summary>
    /// The Humanoid + Animator hero (RLC-059): every preset gets a valid human avatar and an
    /// Animator with the baked controller, the baked clips are humanoid loops, they reproduce the
    /// procedural poses they were baked from, and the controller has every state the driver uses.
    /// </summary>
    public sealed class HeroHumanoidTests
    {
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
        public void EveryPresetIsAValidHumanoidWithTheController()
        {
            foreach (HeroAppearance look in AppearancePresets.All())
            {
                _hero.Appearance = look;
                _hero.Rebuild();
                Assert.IsNotNull(_hero.HumanAvatar, look.Name);
                Assert.IsTrue(_hero.HumanAvatar.isValid, look.Name + " avatar valid");
                Assert.IsTrue(_hero.HumanAvatar.isHuman, look.Name + " avatar human");
                Assert.IsNotNull(_hero.Mecanim, look.Name + " has an Animator");
                Assert.AreSame(HeroAvatar.DefaultController(), _hero.Mecanim.runtimeAnimatorController);
                Assert.IsTrue(_hero.Mecanim.isHuman, look.Name);
                Assert.AreEqual(1, _hero.Renderer.sharedMaterials.Length, "one material");
                Assert.LessOrEqual(_hero.TriangleCount, HeroBuildTests.TriangleBudget, look.Name + ": " + _hero.Breakdown);
                foreach (HeroHumanoid.Pair p in HeroHumanoid.Map)
                    Assert.AreSame(_hero.Rig.Bones[p.Bone], _hero.Mecanim.GetBoneTransform(p.Human), p.Human.ToString());
            }
        }

        [Test]
        public void UnwrittenLooksUnwritten()
        {
            _hero.Appearance = AppearancePresets.Unwritten();
            _hero.Kit = new HeroKit();
            _hero.Rebuild();
            StringAssert.Contains("Bookmark", string.Join(",", BoneNames()), "the bookmark ribbon hangs on a spring chain");
            Assert.IsFalse(BoneNames().Exists(n => n.StartsWith("ScarfTail") || n.StartsWith("SideLock") || n.StartsWith("Coat")), "no scarf, side lock or trench coat");
            int unwrittenBones = _hero.Rig.Bones.Count;
            _hero.Appearance = AppearancePresets.Default();
            _hero.Rebuild();
            Assert.Less(unwrittenBones, _hero.Rig.Bones.Count, "no back hair chains under the hood");
            _hero.Appearance = AppearancePresets.Unwritten();
            _hero.Rebuild();
            Assert.That(_hero.Chains.ChainCount, Is.GreaterThan(4), "cloak panels and the bookmark swing");
            // A rounder, smaller body than the default look, still Genshin-medium proportions.
            Assert.That(_hero.Shape.HeadsTall, Is.InRange(5.9f, 6.6f));
        }

        List<string> BoneNames()
        {
            var names = new List<string>();
            foreach (Transform t in _hero.Rig.Bones) names.Add(t.name);
            return names;
        }

        [Test]
        public void ControllerHasEveryState()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroAnimatorBuilder.ControllerPath);
            Assert.IsNotNull(ctrl, "bake the controller: Relic > Character > Bake Hero Animator");
            Assert.AreSame(ctrl, HeroAvatar.DefaultController());
            var states = new HashSet<string>();
            foreach (ChildAnimatorState s in ctrl.layers[0].stateMachine.states) states.Add(s.state.name);
            foreach (string name in new[] { HeroMecanim.SIdle, HeroMecanim.SMove, HeroMecanim.SDash, HeroMecanim.SJump, HeroMecanim.SFall, HeroMecanim.SLand, HeroMecanim.SClimb, HeroMecanim.SSwim, HeroMecanim.SDive, HeroMecanim.SFly })
                Assert.IsTrue(states.Contains(name), name);
            Assert.AreEqual("Weapon", ctrl.layers[HeroMecanim.WeaponLayer].name);
            Assert.IsNotNull(ctrl.layers[HeroMecanim.WeaponLayer].avatarMask);
        }

        [Test]
        public void BakedClipsAreHumanoidLoops()
        {
            foreach (HeroClipBaker.ClipSpec s in HeroClipBaker.Specs)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroAnimatorBuilder.ClipPath(s.Name));
                Assert.IsNotNull(clip, s.Name);
                Assert.IsTrue(clip.humanMotion, s.Name + " is a humanoid clip");
                Assert.IsTrue(clip.isLooping, s.Name + " loops");
                if (!s.Static) Assert.AreEqual(s.Period, clip.length, 1e-3f, s.Name + " is one cycle long");
            }
        }

        /// <summary>
        /// Poses a bare rig procedurally (the bake's first frame), then poses it again from the
        /// clip's curves through the humanoid muscle space: hands, feet, head and hips land within
        /// a few centimeters. Catches clamped muscle limits, bad T-pose axes or a misplaced body.
        /// </summary>
        [Test]
        public void BakedClipsReproduceTheProceduralPoses()
        {
            HeroRig rig = HeroClipBaker.NewRig(out GameObject go);
            Avatar avatar = HeroHumanoid.Build(rig);
            var handler = new HumanPoseHandler(avatar, rig.RootTransform);
            int[] probes = { HeroRig.Hips, HeroRig.Head, HeroRig.HandL, HeroRig.HandR, HeroRig.FootL, HeroRig.FootR, HeroRig.ForeArmL, HeroRig.ShinR };
            try
            {
                foreach (HeroClipBaker.ClipSpec s in HeroClipBaker.Specs)
                {
                    HeroClipBaker.WarmUp(rig, s, out _, out _);
                    var expected = new Vector3[probes.Length];
                    for (int i = 0; i < probes.Length; i++) expected[i] = rig.RootTransform.InverseTransformPoint(rig.Bones[probes[i]].position);

                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroAnimatorBuilder.ClipPath(s.Name));
                    rig.ResetPose();
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    float F(string attr) => AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), attr)).Evaluate(0f);
                    pose.bodyPosition = new Vector3(F("RootT.x"), F("RootT.y"), F("RootT.z"));
                    pose.bodyRotation = new Quaternion(F("RootQ.x"), F("RootQ.y"), F("RootQ.z"), F("RootQ.w")).normalized;
                    for (int m = 0; m < HumanTrait.MuscleCount; m++)
                        pose.muscles[m] = HeroHumanoid.UsesMuscle(m) ? F(HumanTrait.MuscleName[m]) : 0f;
                    handler.SetHumanPose(ref pose);
                    for (int i = 0; i < probes.Length; i++)
                    {
                        Vector3 got = rig.RootTransform.InverseTransformPoint(rig.Bones[probes[i]].position);
                        Assert.Less((got - expected[i]).magnitude, 0.05f, s.Name + " " + rig.Bones[probes[i]].name + ": expected " + expected[i].ToString("F3") + " got " + got.ToString("F3"));
                    }
                }
            }
            finally
            {
                handler.Dispose();
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AnimatorIdleStandsLikeTheProceduralIdle()
        {
            _hero.Appearance = AppearancePresets.Unwritten();
            _hero.Rebuild();
            for (int f = 0; f < 60; f++) _hero.Animate(1f / 60f, Vector3.zero);
            Vector3 head = _hero.Rig.Bones[HeroRig.Head].position, foot = _hero.Rig.Bones[HeroRig.FootL].position;

            _hero.ProceduralOnly = true;
            _hero.Rebuild();
            Assert.IsNull(_hero.Mecanim);
            for (int f = 0; f < 60; f++) _hero.Animate(1f / 60f, Vector3.zero);
            Assert.Less((_hero.Rig.Bones[HeroRig.Head].position - head).magnitude, 0.06f, "head");
            Assert.Less((_hero.Rig.Bones[HeroRig.FootL].position - foot).magnitude, 0.06f, "foot");
            Assert.That(foot.y, Is.InRange(0f, 0.2f), "feet on the ground");
        }
    }
}
