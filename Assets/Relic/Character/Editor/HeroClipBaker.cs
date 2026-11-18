using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Relic.Character.Editor
{
    /// <summary>
    /// Bakes the procedural poses (HeroAnimator) into Humanoid AnimationClips: a bare rig is posed
    /// frame by frame, HumanPoseHandler reads the pose back as muscles plus body position and
    /// rotation, and those become the clip's curves (RootT/RootQ and one curve per muscle). The
    /// clips are humanoid, so they play on any humanoid avatar, and ours can play other clips.
    /// Cycles are recorded over exactly one period after a warm-up, so they loop seamlessly.
    /// </summary>
    public static class HeroClipBaker
    {
        public sealed class ClipSpec
        {
            public string Name;
            public HeroAnimInput Input;
            /// <summary>Seconds per loop (cycles); ignored for static poses.</summary>
            public float Period;
            /// <summary>Samples per loop; 0 = a static pose (two identical keys a second apart).</summary>
            public int Keys;
            public bool Static => Keys == 0;
        }

        const float Step = 1f / 120f;

        static HeroAnimInput In(HeroMotion m, float speed = 0f) => new HeroAnimInput { Motion = m, Speed = speed };

        static float Stride(float speed, bool sprint) => (sprint ? HeroMecanim.SprintStride : HeroMecanim.RunStride) / speed;
        static float Stroke(float radPerSecond) => Mathf.PI * 2f / radPerSecond;

        public static readonly ClipSpec[] Specs =
        {
            new ClipSpec { Name = "Idle", Input = In(HeroMotion.Idle), Period = HeroAnimator.IdleLoop, Keys = 48 },
            new ClipSpec { Name = "Walk", Input = In(HeroMotion.Run, HeroMecanim.WalkSpeed), Period = HeroMecanim.WalkStride / HeroMecanim.WalkSpeed, Keys = 18 },
            new ClipSpec { Name = "Run", Input = In(HeroMotion.Run, HeroAnimator.RunSpeed), Period = Stride(HeroAnimator.RunSpeed, false), Keys = 16 },
            new ClipSpec { Name = "Sprint", Input = new HeroAnimInput { Motion = HeroMotion.Run, Speed = HeroAnimator.SprintSpeed, Sprint = true }, Period = Stride(HeroAnimator.SprintSpeed, true), Keys = 16 },
            new ClipSpec { Name = "Dash", Input = new HeroAnimInput { Motion = HeroMotion.Run, Speed = 14f, Dash = true }, Period = Stride(14f, false), Keys = 12 },
            new ClipSpec { Name = "Rise", Input = new HeroAnimInput { Motion = HeroMotion.Air, Speed = 5f, VerticalSpeed = 5f } },
            new ClipSpec { Name = "Apex", Input = new HeroAnimInput { Motion = HeroMotion.Air, Speed = 5f, VerticalSpeed = 0f } },
            new ClipSpec { Name = "Fall", Input = new HeroAnimInput { Motion = HeroMotion.Air, Speed = 5f, VerticalSpeed = -8f } },
            new ClipSpec { Name = "Land", Input = new HeroAnimInput { Motion = HeroMotion.Idle, LandingSquash = 1f } },
            new ClipSpec { Name = "Climb", Input = new HeroAnimInput { Motion = HeroMotion.Climb, ClimbSpeed = HeroMecanim.BakeClimbSpeed }, Period = Stroke(HeroMecanim.BakeClimbSpeed * 3.2f), Keys = 18 },
            new ClipSpec { Name = "SwimIdle", Input = In(HeroMotion.Swim, 0f), Period = Stroke(2.2f), Keys = 18 },
            new ClipSpec { Name = "Swim", Input = In(HeroMotion.Swim, HeroMecanim.BakeSwimSpeed), Period = Stroke(2.2f + 0.8f * HeroMecanim.BakeSwimSpeed), Keys = 18 },
            new ClipSpec { Name = "DiveUp", Input = new HeroAnimInput { Motion = HeroMotion.Dive, Speed = HeroMecanim.BakeDiveSpeed, DivePitch = -55f, Underwater = true }, Period = Stroke(1.4f + 0.5f * HeroMecanim.BakeDiveSpeed), Keys = 18 },
            new ClipSpec { Name = "Dive", Input = new HeroAnimInput { Motion = HeroMotion.Dive, Speed = HeroMecanim.BakeDiveSpeed, DivePitch = 0f, Underwater = true }, Period = Stroke(1.4f + 0.5f * HeroMecanim.BakeDiveSpeed), Keys = 18 },
            new ClipSpec { Name = "DiveDown", Input = new HeroAnimInput { Motion = HeroMotion.Dive, Speed = HeroMecanim.BakeDiveSpeed, DivePitch = 45f, Underwater = true }, Period = Stroke(1.4f + 0.5f * HeroMecanim.BakeDiveSpeed), Keys = 18 },
            new ClipSpec { Name = "Fly", Input = In(HeroMotion.Fly, 8f) },
            new ClipSpec { Name = "Ready", Input = new HeroAnimInput { Motion = HeroMotion.Idle, WeaponDrawn = true } },
        };

        public static ClipSpec Spec(string name)
        {
            foreach (ClipSpec s in Specs) if (s.Name == name) return s;
            throw new KeyNotFoundException(name);
        }

        /// <summary>A bare rig (no mesh) at the default proportions, hidden, for baking and tests.</summary>
        public static HeroRig NewRig(out GameObject go)
        {
            go = new GameObject("HeroBake") { hideFlags = HideFlags.HideAndDontSave };
            return new HeroRig(go.transform, new HeroShape(new BodySpec()).Remap);
        }

        /// <summary>Runs the procedural poser into its steady state: the rig ends on the clip's first frame.</summary>
        public static HeroAnimator WarmUp(HeroRig rig, ClipSpec s, out float dt, out int sub)
        {
            rig.ResetPose();
            var anim = new HeroAnimator(rig);
            if (s.Static) { sub = 1; dt = Step; }
            else
            {
                sub = Mathf.Max(1, Mathf.CeilToInt(s.Period / s.Keys / Step));
                dt = s.Period / (s.Keys * sub);
            }
            float warm = s.Static ? 3f : Mathf.Max(3f, s.Period * 2f);
            // Whole loops of warm-up, so a cycle's first key lands on the same phase every bake.
            int steps = s.Static ? Mathf.CeilToInt(warm / dt) : Mathf.CeilToInt(warm / s.Period) * s.Keys * sub;
            for (int i = 0; i < steps; i++) anim.Apply(s.Input, dt);
            return anim;
        }

        /// <summary>Records one clip: muscles and body pose per key.</summary>
        public static AnimationClip Bake(ClipSpec s, HeroRig rig, HumanPoseHandler handler, AnimationClip into = null)
        {
            HeroAnimator anim = WarmUp(rig, s, out float dt, out int sub);
            int keys = s.Static ? 2 : s.Keys + 1;
            float length = s.Static ? 1f : s.Period;
            var poses = new List<HumanPose>();
            var pose = new HumanPose();
            for (int k = 0; k < keys; k++)
            {
                if (k > 0 && !s.Static) for (int i = 0; i < sub; i++) anim.Apply(s.Input, dt);
                handler.GetHumanPose(ref pose);
                var copy = new HumanPose { bodyPosition = pose.bodyPosition, bodyRotation = pose.bodyRotation, muscles = (float[])pose.muscles.Clone() };
                if (poses.Count > 0 && Quaternion.Dot(poses[poses.Count - 1].bodyRotation, copy.bodyRotation) < 0f)
                {
                    Quaternion q = copy.bodyRotation;
                    copy.bodyRotation = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }
                poses.Add(copy);
            }
            if (!s.Static)
            {
                // The last key is the first one again (exactly), so the loop closes without a pop.
                poses[poses.Count - 1] = poses[0];
            }

            AnimationClip clip = into != null ? into : new AnimationClip();
            clip.ClearCurves();
            clip.name = "Hero" + s.Name;
            clip.frameRate = 30f;
            float[] times = new float[keys];
            for (int k = 0; k < keys; k++) times[k] = length * k / (keys - 1);
            void Curve(string property, System.Func<HumanPose, float> value)
            {
                var v = new float[keys];
                for (int k = 0; k < keys; k++) v[k] = value(poses[k]);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), Smooth(times, v, !s.Static));
            }
            Curve("RootT.x", p => p.bodyPosition.x);
            Curve("RootT.y", p => p.bodyPosition.y);
            Curve("RootT.z", p => p.bodyPosition.z);
            Curve("RootQ.x", p => p.bodyRotation.x);
            Curve("RootQ.y", p => p.bodyRotation.y);
            Curve("RootQ.z", p => p.bodyRotation.z);
            Curve("RootQ.w", p => p.bodyRotation.w);
            for (int m = 0; m < HumanTrait.MuscleCount; m++)
            {
                if (!HeroHumanoid.UsesMuscle(m)) continue;
                int mi = m;
                Curve(HumanTrait.MuscleName[m], p => p.muscles[mi]);
            }
            AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
            {
                loopTime = true,
                loopBlend = false,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
                loopBlendOrientation = true,
                loopBlendPositionY = true,
                loopBlendPositionXZ = true,
                heightFromFeet = false,
                mirror = false,
                startTime = 0f,
                stopTime = length,
            });
            return clip;
        }

        /// <summary>
        /// A curve through the samples with smooth (Catmull-Rom) tangents, wrapping for loops.
        /// Constant curves collapse to two keys.
        /// </summary>
        static AnimationCurve Smooth(float[] t, float[] v, bool loop)
        {
            int n = v.Length;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float x in v) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
            if (hi - lo < 2e-4f) return new AnimationCurve(new Keyframe(t[0], v[0], 0f, 0f), new Keyframe(t[n - 1], v[0], 0f, 0f));
            var keys = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                float slope;
                if (i > 0 && i < n - 1) slope = (v[i + 1] - v[i - 1]) / (t[i + 1] - t[i - 1]);
                else if (loop) slope = (v[1] - v[n - 2]) / (t[1] - t[0] + t[n - 1] - t[n - 2]);
                else slope = 0f;
                keys[i] = new Keyframe(t[i], v[i], slope, slope);
            }
            return new AnimationCurve(keys);
        }
    }
}
