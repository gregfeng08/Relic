using System;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Swimming on the surface and diving below it, with a grounded breath meter: breath runs
    /// out underwater, running out forces you up and drains stamina. Ctrl/C dives and descends,
    /// Space ascends; underwater, forward follows the camera's pitch.
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>Seconds of breath left (Config.BreathMax when full).</summary>
        public float Breath { get; private set; }
        public float BreathFraction => Config != null && Config.BreathMax > 0f ? Breath / Config.BreathMax : 1f;
        public bool IsDiving => State == MoveState.Diving;
        /// <summary>The head is below the water surface.</summary>
        public bool HeadUnderwater { get; private set; }
        /// <summary>Breath ran out: you're forced up and stamina drains until you've caught your breath.</summary>
        public bool OutOfBreath { get; private set; }
        /// <summary>Seconds spent out of breath underwater in total (for the future HP hook and tests).</summary>
        public float SuffocationSeconds { get; private set; }
        /// <summary>Body pitch while diving, degrees (positive = head down), for avatars.</summary>
        public float DivePitch { get; private set; }

        /// <summary>
        /// Raised every frame you're out of breath underwater, with that frame's seconds.
        /// TODO(HP): HP doesn't exist yet. Once it does, the game layer subscribes here and
        /// applies drowning damage (stamina already drains in UpdateBreath).
        /// </summary>
        public event Action<float> Suffocating;

        const float HeadHeight = 1.55f;
        Vector3 _diveAccel;

        bool TryEnterSwim()
        {
            if (Probe == null) return false;
            Probe(transform.position, out float ground, out float water);
            float depth = water - ground;
            if (depth < Config.SwimDepth + 0.1f) return false;
            if (transform.position.y > water - Config.SwimDepth) return false;
            // Plunging in from a height carries you under: a dive, not a belly flop.
            bool plunge = _velocity.y < -9f && depth > 3.5f && !OutOfBreath;
            SetState(plunge ? MoveState.Diving : MoveState.Swimming);
            _velocity.y = plunge ? Mathf.Max(_velocity.y * 0.35f, -5f) : Mathf.Max(_velocity.y, -2f);
            _accelRef = Vector3.zero;
            _diveAccel = Vector3.zero;
            return true;
        }

        void UpdateWater(float dt, PlayerInputState input, Vector3 moveWorld)
        {
            float ground = transform.position.y - 10f, water = transform.position.y + Config.SwimDepth;
            if (Probe != null) Probe(transform.position, out ground, out water);
            float depth = water - ground;

            if (State == MoveState.Swimming && input.DescendHeld && !OutOfBreath && depth > Config.SwimDepth + 2.2f)
            {
                SetState(MoveState.Diving);
                _velocity.y = Mathf.Min(_velocity.y, -1.2f);
                _diveAccel = Vector3.zero;
            }

            if (State == MoveState.Diving) UpdateDive(dt, input, ground, water);
            else UpdateSurfaceSwim(dt, moveWorld, ground, water);
        }

        void UpdateSurfaceSwim(float dt, Vector3 moveWorld, float ground, float water)
        {
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            bool moving = moveWorld.sqrMagnitude > 0.01f;
            horizontal = Vector3.SmoothDamp(horizontal, moveWorld * Config.SwimSpeed, ref _accelRef, moving ? 0.22f : 0.35f, Mathf.Infinity, dt);

            // Critically damped buoyancy toward floating height; in the shallows the target rides
            // up with the ground, so wading out of the water is one smooth motion.
            float targetY = Mathf.Max(water - Config.SwimDepth, ground + 0.02f);
            const float omega = 4.5f;
            float vy = _velocity.y;
            vy += ((targetY - transform.position.y) * omega * omega - 2f * omega * vy) * dt;
            vy = Mathf.Clamp(vy, -8f, 4f);
            _velocity = new Vector3(horizontal.x, vy, horizontal.z);
            DivePitch = Mathf.Lerp(DivePitch, 0f, 1f - Mathf.Exp(-8f * dt));
            if (moving) FaceTowards(moveWorld, Config.TurnSmoothTime * 1.6f, dt);
            _cc.Move(_velocity * dt);

            if (water - ground < Config.SwimDepth)
            {
                SetState(MoveState.Grounded);
                return;
            }
            if (moving) TryStartClimb(moveWorld, dt, immediate: true);
        }

        void UpdateDive(float dt, PlayerInputState input, float ground, float water)
        {
            float camYaw = OrbitCam != null ? OrbitCam.Yaw : _yaw;
            float camPitch = OrbitCam != null ? OrbitCam.Pitch : 0f;
            // Forward follows where the camera looks (look down to swim down); strafing stays level.
            Vector3 look = Quaternion.Euler(Mathf.Clamp(camPitch, -80f, 80f), camYaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
            Vector3 dir = Vector3.ClampMagnitude(look * input.Move.y + right * input.Move.x, 1f);
            float vertical = (input.JumpHeld ? 1f : 0f) - (input.DescendHeld ? 1f : 0f);

            Vector3 target = dir * Config.DiveSpeed + Vector3.up * (vertical * Config.DiveVerticalSpeed);
            if (Mathf.Approximately(vertical, 0f) && dir.sqrMagnitude < 0.01f) target.y += Config.DiveBuoyancy;
            if (OutOfBreath) target.y = Mathf.Max(target.y, 2.8f); // forced up, can't fight it
            _velocity = Vector3.SmoothDamp(_velocity, target, ref _diveAccel, Config.DiveSmoothTime, Mathf.Infinity, dt);

            Vector3 flat = new Vector3(_velocity.x, 0f, _velocity.z);
            if (dir.sqrMagnitude > 0.01f) FaceTowards(new Vector3(dir.x, 0f, dir.z).sqrMagnitude > 0.01f ? new Vector3(dir.x, 0f, dir.z) : Forward, Config.TurnSmoothTime * 2f, dt);
            float pitchTarget = Mathf.Atan2(-_velocity.y, Mathf.Max(flat.magnitude, 0.6f)) * Mathf.Rad2Deg;
            DivePitch = Mathf.Lerp(DivePitch, Mathf.Clamp(pitchTarget, -60f, 70f), 1f - Mathf.Exp(-5f * dt));

            _cc.Move(_velocity * dt);

            float surfaceY = water - Config.SwimDepth;
            bool atSurface = transform.position.y >= surfaceY - 0.05f && _velocity.y > -0.2f && (!input.DescendHeld || OutOfBreath);
            bool shallow = water - ground < Config.SwimDepth + 0.6f;
            if (atSurface || shallow)
            {
                _velocity.y = Mathf.Min(_velocity.y, 1.2f);
                _accelRef = Vector3.zero;
                SetState(MoveState.Swimming);
            }
        }

        void UpdateBreath(float dt)
        {
            if (Config == null) return;
            float water = float.MinValue;
            if (Probe != null && (State == MoveState.Diving || State == MoveState.Swimming))
                Probe(transform.position, out _, out water);
            HeadUnderwater = State == MoveState.Diving && transform.position.y + HeadHeight < water - 0.05f;

            if (HeadUnderwater)
            {
                Breath = Mathf.Max(0f, Breath - dt);
                if (Breath <= 0f) OutOfBreath = true;
                if (OutOfBreath)
                {
                    UseStamina(Config.DrownStaminaDrain * dt);
                    SuffocationSeconds += dt;
                    Suffocating?.Invoke(dt);
                }
            }
            else
            {
                Breath = Mathf.Min(Config.BreathMax, Breath + Config.BreathRegen * dt);
                if (OutOfBreath && Breath >= Config.BreathMax * 0.3f) OutOfBreath = false;
            }
        }
    }
}
