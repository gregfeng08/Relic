using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Relic.Character.PlayTests
{
    /// <summary>
    /// The hero's Animator in play mode (Unity evaluates it every frame, as in the game): movement
    /// input walks it through the state machine the way PlayerController does (idle → run →
    /// sprint, jump → fall → land, swim ↔ dive, climb, dash), and every preset builds as a valid
    /// humanoid with the Animator attached.
    /// </summary>
    public class HeroAnimatorPlayTests
    {
        GameObject _go;
        HeroAvatar _hero;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Animated Hero Under Test");
            _hero = _go.AddComponent<HeroAvatar>();
            _hero.Appearance = AppearancePresets.Unwritten();
            _hero.Rebuild();
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        /// <summary>Feeds the input every frame for a while (game time), like HeroPlayerDriver.</summary>
        IEnumerator Drive(HeroAnimInput a, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                t += dt;
                _hero.SetInput(a, dt);
                _hero.Animate(dt, Vector3.zero);
            }
        }

        bool InState(string name) => _hero.Mecanim.GetCurrentAnimatorStateInfo(0).IsName(name) && !_hero.Mecanim.IsInTransition(0);

        string DominantClip()
        {
            string best = null;
            float w = -1f;
            foreach (AnimatorClipInfo c in _hero.Mecanim.GetCurrentAnimatorClipInfo(0))
                if (c.weight > w) { w = c.weight; best = c.clip.name; }
            return best;
        }

        static HeroAnimInput Ground(float speed, bool sprint = false) =>
            new HeroAnimInput { Motion = speed > 0.25f ? HeroMotion.Run : HeroMotion.Idle, Speed = speed, Sprint = sprint };

        [UnityTest]
        public IEnumerator IdleRunSprintAndBack()
        {
            Assert.IsNotNull(_hero.Mecanim, "the hero is driven by an Animator");
            yield return Drive(Ground(0f), 0.4f);
            Assert.IsTrue(InState(HeroMecanim.SIdle), "starts idle");
            yield return Drive(Ground(6f), 0.6f);
            Assert.IsTrue(InState(HeroMecanim.SMove), "runs");
            Assert.AreEqual("HeroRun", DominantClip());
            Assert.That(_hero.Mecanim.GetFloat(HeroMecanim.Rate), Is.EqualTo(1f).Within(0.05f), "run plays at its baked cadence");
            yield return Drive(Ground(9.5f, true), 0.6f);
            Assert.IsTrue(InState(HeroMecanim.SMove), "sprints in the same blend");
            Assert.AreEqual("HeroSprint", DominantClip());
            yield return Drive(Ground(2.5f), 0.6f);
            Assert.AreEqual("HeroWalk", DominantClip(), "slow moves blend toward the walk");
            yield return Drive(Ground(0f), 0.8f);
            Assert.IsTrue(InState(HeroMecanim.SIdle), "back to idle");
        }

        [UnityTest]
        public IEnumerator JumpFallLand()
        {
            yield return Drive(Ground(0f), 0.3f);
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Air, VerticalSpeed = 6f, Speed = 4f }, 0.3f);
            Assert.IsTrue(InState(HeroMecanim.SJump), "jumps");
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Air, VerticalSpeed = -7f, Speed = 4f }, 0.5f);
            Assert.IsTrue(InState(HeroMecanim.SFall), "falls");
            Assert.AreEqual("HeroFall", DominantClip());
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Idle, LandingSquash = 0.9f }, 0.2f);
            Assert.IsTrue(InState(HeroMecanim.SLand), "lands");
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Idle }, 0.6f);
            Assert.IsTrue(InState(HeroMecanim.SIdle), "recovers to idle");
            // Landing while running goes straight back to the run.
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Air, VerticalSpeed = -5f, Speed = 6f }, 0.4f);
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Run, Speed = 6f, LandingSquash = 0.5f }, 0.5f);
            Assert.IsTrue(InState(HeroMecanim.SMove), "runs on after landing");
        }

        [UnityTest]
        public IEnumerator SwimDiveClimbDash()
        {
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Swim, Speed = 3f }, 0.6f);
            Assert.IsTrue(InState(HeroMecanim.SSwim), "swims");
            Assert.AreEqual("HeroSwim", DominantClip());
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Dive, Speed = 3f, DivePitch = 40f, Underwater = true }, 0.7f);
            Assert.IsTrue(InState(HeroMecanim.SDive), "dives");
            Assert.AreEqual("HeroDiveDown", DominantClip(), "pitched down");
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Swim, Speed = 0f }, 0.7f);
            Assert.IsTrue(InState(HeroMecanim.SSwim), "surfaces");
            Assert.AreEqual("HeroSwimIdle", DominantClip());
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Climb, ClimbSpeed = 2.6f }, 0.5f);
            Assert.IsTrue(InState(HeroMecanim.SClimb), "climbs");
            Assert.That(_hero.Mecanim.GetFloat(HeroMecanim.Rate), Is.EqualTo(1.3f).Within(0.01f), "climb cadence follows the climb speed");
            yield return Drive(Ground(0f), 0.6f);
            Assert.IsTrue(InState(HeroMecanim.SIdle), "tops out to idle");
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Run, Speed = 14f, Dash = true }, 0.2f);
            Assert.IsTrue(InState(HeroMecanim.SDash), "dashes");
            yield return Drive(Ground(6f), 0.5f);
            Assert.IsTrue(InState(HeroMecanim.SMove), "runs out of the dash");
        }

        [UnityTest]
        public IEnumerator WeaponLayerRaisesTheRightArm()
        {
            yield return Drive(Ground(0f), 0.3f);
            Vector3 down = _hero.Rig.Bones[HeroRig.HandR].position;
            yield return Drive(new HeroAnimInput { Motion = HeroMotion.Idle, WeaponDrawn = true }, 0.5f);
            Assert.AreEqual(1f, _hero.Mecanim.GetLayerWeight(HeroMecanim.WeaponLayer), 1e-3f);
            Assert.Greater(_hero.Rig.Bones[HeroRig.HandR].position.z - down.z, 0.1f, "the weapon hand comes forward");
        }

        [UnityTest]
        public IEnumerator EveryPresetBuildsAsAnAnimatedHumanoid()
        {
            foreach (HeroAppearance look in AppearancePresets.All())
            {
                _hero.Appearance = look;
                _hero.Rebuild();
                Assert.IsTrue(_hero.HumanAvatar.isValid && _hero.HumanAvatar.isHuman, look.Name);
                Assert.IsNotNull(_hero.Mecanim, look.Name);
                Assert.LessOrEqual(_hero.TriangleCount, 15000, look.Name);
                yield return Drive(Ground(6f), 0.25f);
                Assert.IsTrue(_hero.Mecanim.GetCurrentAnimatorStateInfo(0).IsName(HeroMecanim.SMove) || _hero.Mecanim.IsInTransition(0), look.Name + " runs");
                foreach (Transform t in _hero.Rig.Bones)
                    Assert.IsFalse(float.IsNaN(t.position.x), look.Name + " " + t.name);
            }
        }
    }
}
