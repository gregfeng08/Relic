using UnityEngine;

namespace Relic.Character
{
    public enum HeroMotion { Idle, Run, Air, Climb, Swim, Fly, Dive }

    /// <summary>What the animator needs to know each frame (from PlayerController, or set by tools).</summary>
    public struct HeroAnimInput
    {
        public HeroMotion Motion;
        public float Speed;          // horizontal m/s
        public bool Sprint;
        public bool Dash;
        public float VerticalSpeed;
        public float ClimbSpeed;     // m/s along the wall
        public bool WeaponDrawn;
    }

    /// <summary>
    /// Procedural animation, no clips: idle breathing and weight shift, run/sprint cycle with a
    /// forward lean, a low dash lunge, jump/fall, climb and swim. Targets are Euler angles per
    /// bone (rest rotations are identity), eased toward with frame-rate independent smoothing.
    /// Bone conventions: +X pitch tips a down-pointing limb backward and leans the torso forward;
    /// +Z swings a down-pointing limb toward +X (the character's right).
    /// </summary>
    public sealed class HeroAnimator
    {
        const int N = 18;
        public const float RunSpeed = 6f, SprintSpeed = 9.5f;

        readonly HeroRig _rig;
        readonly Vector3[] _target = new Vector3[N];
        readonly Quaternion[] _current = new Quaternion[N];
        float _phase, _time, _dash, _run, _air, _climbPhase, _swimPhase, _lean;
        Vector3 _hipsOffset;

        public HeroAnimator(HeroRig rig)
        {
            _rig = rig;
            for (int i = 0; i < N; i++) _current[i] = Quaternion.identity;
        }

        public void Apply(HeroAnimInput a, float dt)
        {
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _time += dt;
            for (int i = 0; i < N; i++) _target[i] = Vector3.zero;

            float runTarget = a.Motion == HeroMotion.Run ? Mathf.Clamp01(a.Speed / RunSpeed) : 0f;
            _run = Ease(_run, runTarget, 10f, dt);
            _dash = Ease(_dash, a.Dash ? 1f : 0f, a.Dash ? 22f : 8f, dt);
            _air = Ease(_air, a.Motion == HeroMotion.Air ? 1f : 0f, 9f, dt);
            float sprint = a.Sprint ? 1f : 0f;
            Vector3 hips = Vector3.zero;

            // Idle: breathing and a slow weight shift (always underneath, faded by motion).
            float idle = (1f - _run) * (1f - _air) * (a.Motion == HeroMotion.Idle || a.Motion == HeroMotion.Run ? 1f : 0f);
            float breath = Mathf.Sin(_time * 1.7f);
            float shift = Mathf.Sin(_time * 0.45f);
            Add(HeroRig.Chest, -1.6f * breath * idle, 0f, 0f);
            Add(HeroRig.Neck, 1.2f * breath * idle, 0f, 0f);
            Add(HeroRig.Head, 2f * idle, 3f * shift * idle, -2f * shift * idle);
            Add(HeroRig.Hips, 0f, 0f, 2.2f * shift * idle);
            Add(HeroRig.Spine, 0f, 0f, -2.6f * shift * idle);
            hips += new Vector3(0.012f * shift, -0.006f * Mathf.Abs(shift), 0f) * idle;
            float relaxedKnee = 7f * idle;
            Add(HeroRig.ThighL, -3f * idle * Mathf.Max(0f, shift), 0f, -2f * idle);
            Add(HeroRig.ShinL, relaxedKnee * Mathf.Max(0f, shift) + 2f * idle, 0f, 0f);
            Add(HeroRig.ThighR, -3f * idle * Mathf.Max(0f, -shift), 0f, 2f * idle);
            Add(HeroRig.ShinR, relaxedKnee * Mathf.Max(0f, -shift) + 2f * idle, 0f, 0f);
            // Arms hang a little out from the coat, elbows soft, hands relaxed.
            Add(HeroRig.UpperArmL, 3f, 0f, -9f - breath * 0.6f);
            Add(HeroRig.UpperArmR, 3f, 0f, 9f + breath * 0.6f);
            Add(HeroRig.ForeArmL, -10f, 0f, 0f);
            Add(HeroRig.ForeArmR, -10f, 0f, 0f);
            Add(HeroRig.HandL, -5f, 0f, 4f);
            Add(HeroRig.HandR, -5f, 0f, -4f);

            // Run / sprint cycle.
            if (_run > 0.001f)
            {
                float stride = Mathf.Lerp(1.5f, 2.3f, sprint);
                _phase += a.Speed * dt / stride * Mathf.PI * 2f;
                float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);
                float legA = Mathf.Lerp(32f, 46f, sprint) * _run;
                Add(HeroRig.ThighL, -s * legA, 0f, 0f);
                Add(HeroRig.ThighR, s * legA, 0f, 0f);
                Add(HeroRig.ShinL, (12f + Mathf.Max(0f, c) * Mathf.Lerp(55f, 85f, sprint)) * _run, 0f, 0f);
                Add(HeroRig.ShinR, (12f + Mathf.Max(0f, -c) * Mathf.Lerp(55f, 85f, sprint)) * _run, 0f, 0f);
                Add(HeroRig.FootL, -s * 12f * _run, 0f, 0f);
                Add(HeroRig.FootR, s * 12f * _run, 0f, 0f);
                float armA = Mathf.Lerp(30f, 48f, sprint) * _run;
                Add(HeroRig.UpperArmL, s * armA, 0f, -4f * _run);
                Add(HeroRig.UpperArmR, -s * armA, 0f, 4f * _run);
                Add(HeroRig.ForeArmL, -Mathf.Lerp(30f, 70f, sprint) * _run, 0f, 0f);
                Add(HeroRig.ForeArmR, -Mathf.Lerp(30f, 70f, sprint) * _run, 0f, 0f);
                Add(HeroRig.Spine, 0f, -s * 7f * _run, 0f);
                Add(HeroRig.Chest, 0f, -s * 6f * _run, 0f);
                Add(HeroRig.Hips, 0f, s * 8f * _run, 0f);
                hips.y += (Mathf.Abs(c) - 0.6f) * 0.045f * _run;
            }
            float leanTarget = _run * Mathf.Lerp(7f, 18f, sprint);

            // Dash: a low lunge, left foot forward, arms swept back (the coat flares from the wind).
            if (_dash > 0.001f)
            {
                float d = _dash;
                leanTarget = Mathf.Lerp(leanTarget, 30f, d);
                hips.y -= 0.13f * d;
                Add(HeroRig.ThighL, -55f * d, 0f, -4f * d);
                Add(HeroRig.ShinL, 55f * d, 0f, 0f);
                Add(HeroRig.ThighR, 30f * d, 0f, 4f * d);
                Add(HeroRig.ShinR, 40f * d, 0f, 0f);
                Add(HeroRig.FootR, 25f * d, 0f, 0f);
                Add(HeroRig.UpperArmL, 45f * d, 0f, -25f * d);
                Add(HeroRig.UpperArmR, 45f * d, 0f, 25f * d);
                Add(HeroRig.Head, -20f * d, 0f, 0f);
            }

            // Jump / fall: knees tucked rising, legs and arms reaching out falling.
            if (_air > 0.001f)
            {
                float rising = Mathf.Clamp01(a.VerticalSpeed / 4f), falling = Mathf.Clamp01(-a.VerticalSpeed / 8f);
                float k = _air;
                Add(HeroRig.ThighL, (-45f * rising - 10f * falling) * k, 0f, -5f * k);
                Add(HeroRig.ShinL, (70f * rising + 15f * falling) * k, 0f, 0f);
                Add(HeroRig.ThighR, (-10f * rising + 12f * falling) * k, 0f, 6f * k);
                Add(HeroRig.ShinR, (30f + 10f * falling) * k, 0f, 0f);
                Add(HeroRig.UpperArmL, (-20f * rising - 15f * falling) * k, 0f, -(25f + 35f * falling) * k);
                Add(HeroRig.UpperArmR, (25f * rising - 15f * falling) * k, 0f, (25f + 35f * falling) * k);
                leanTarget += (6f * rising - 4f * falling) * k;
            }

            if (a.Motion == HeroMotion.Climb)
            {
                _climbPhase += a.ClimbSpeed * dt * 3.2f;
                float s = Mathf.Sin(_climbPhase);
                Add(HeroRig.UpperArmL, -150f + s * 25f, 0f, -12f);
                Add(HeroRig.UpperArmR, -150f - s * 25f, 0f, 12f);
                Add(HeroRig.ForeArmL, -25f - Mathf.Max(0f, s) * 40f, 0f, 0f);
                Add(HeroRig.ForeArmR, -25f - Mathf.Max(0f, -s) * 40f, 0f, 0f);
                Add(HeroRig.ThighL, -40f - s * 20f, 0f, -10f);
                Add(HeroRig.ThighR, -40f + s * 20f, 0f, 10f);
                Add(HeroRig.ShinL, 70f, 0f, 0f);
                Add(HeroRig.ShinR, 70f, 0f, 0f);
                Add(HeroRig.Head, -15f, 0f, 0f);
                leanTarget = 6f;
            }

            if (a.Motion == HeroMotion.Swim || a.Motion == HeroMotion.Dive)
            {
                bool dive = a.Motion == HeroMotion.Dive;
                _swimPhase += dt * (dive ? 1.4f + a.Speed * 0.5f : 2.2f + a.Speed * 0.8f);
                float s = Mathf.Sin(_swimPhase), c = Mathf.Cos(_swimPhase);
                leanTarget = dive ? 82f : a.Speed > 0.5f ? 65f : 20f;
                Add(HeroRig.Head, -leanTarget * 0.7f, 0f, 0f);
                Add(HeroRig.UpperArmL, -100f + s * 60f, 0f, -30f - c * 25f);
                Add(HeroRig.UpperArmR, -100f + s * 60f, 0f, 30f + c * 25f);
                Add(HeroRig.ForeArmL, -30f, 0f, 0f);
                Add(HeroRig.ForeArmR, -30f, 0f, 0f);
                Add(HeroRig.ThighL, s * 22f, 0f, -6f);
                Add(HeroRig.ThighR, -s * 22f, 0f, 6f);
                Add(HeroRig.ShinL, 15f + Mathf.Max(0f, c) * 25f, 0f, 0f);
                Add(HeroRig.ShinR, 15f + Mathf.Max(0f, -c) * 25f, 0f, 0f);
                hips.y += 0.25f * (leanTarget / 65f);
            }

            if (a.Motion == HeroMotion.Fly)
            {
                leanTarget = 25f;
                Add(HeroRig.UpperArmL, 20f, 0f, -60f);
                Add(HeroRig.UpperArmR, 20f, 0f, 60f);
            }

            if (a.WeaponDrawn)
            {
                // Right hand forward holding the weapon at the ready.
                _target[HeroRig.UpperArmR] = Vector3.Lerp(_target[HeroRig.UpperArmR], new Vector3(-25f, 0f, 14f), 0.8f);
                _target[HeroRig.ForeArmR] = Vector3.Lerp(_target[HeroRig.ForeArmR], new Vector3(-55f, -10f, 0f), 0.8f);
                _target[HeroRig.HandR] = new Vector3(10f, 0f, 0f);
            }

            _lean = Ease(_lean, leanTarget, 9f, dt);
            Add(HeroRig.Hips, _lean * 0.55f, 0f, 0f);
            Add(HeroRig.Spine, _lean * 0.3f, 0f, 0f);
            Add(HeroRig.Chest, _lean * 0.15f, 0f, 0f);
            Add(HeroRig.Neck, -_lean * 0.35f, 0f, 0f);
            Add(HeroRig.Head, -_lean * 0.25f, 0f, 0f);
            // Legs stay under the body as the hips pitch forward.
            Add(HeroRig.ThighL, -_lean * 0.5f, 0f, 0f);
            Add(HeroRig.ThighR, -_lean * 0.5f, 0f, 0f);

            float k2 = 1f - Mathf.Exp(-18f * dt);
            for (int i = 1; i < N; i++)
            {
                _current[i] = Quaternion.Slerp(_current[i], Quaternion.Euler(_target[i]), k2);
                _rig.Bones[i].localRotation = _current[i];
            }
            _hipsOffset = Vector3.Lerp(_hipsOffset, hips, 1f - Mathf.Exp(-14f * dt));
            _rig.Bones[HeroRig.Hips].localPosition = _rig.Rest[HeroRig.Hips] + _hipsOffset;
        }

        /// <summary>Jumps straight to the pose for the input (captures, first frame).</summary>
        public void Snap(HeroAnimInput a)
        {
            for (int i = 0; i < 30; i++) Apply(a, 1f / 30f);
        }

        void Add(int bone, float x, float y, float z) => _target[bone] += new Vector3(x, y, z);

        static float Ease(float v, float target, float rate, float dt) => Mathf.Lerp(v, target, 1f - Mathf.Exp(-rate * dt));
    }
}
