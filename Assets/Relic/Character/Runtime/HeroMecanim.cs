using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The hero's AnimatorController contract (built by Relic.Character.Editor.HeroAnimatorBuilder,
    /// saved at Resources/Relic/HeroAnimator.controller) and the bridge that feeds it from
    /// HeroAnimInput each frame. Names are shared with the builder so they can't drift.
    /// </summary>
    public sealed class HeroMecanim
    {
        public const string ResourcePath = "Relic/HeroAnimator";

        // Parameters.
        public const string Speed = "Speed", VerticalSpeed = "VerticalSpeed", Mode = "Mode", Dash = "Dash";
        public const string Rate = "Rate", DivePitch = "DivePitch", Land = "Land";
        // Mode values.
        public const int Ground = 0, Air = 1, Climb = 2, Swim = 3, Fly = 4, Dive = 5;
        // States (base layer) and the weapon layer.
        public const string SIdle = "Idle", SMove = "Move", SDash = "Dash", SJump = "Jump", SFall = "Fall", SLand = "Land";
        public const string SClimb = "Climb", SSwim = "Swim", SDive = "Dive", SFly = "Fly";
        public const int WeaponLayer = 1;

        // The speeds and rates the clips were baked at (HeroClipBaker), for the Rate multiplier.
        public const float WalkSpeed = 1.5f, BakeClimbSpeed = 2f, BakeSwimSpeed = 3.6f, BakeDiveSpeed = 3.2f;
        public const float WalkStride = 1.5f, RunStride = 1.5f, SprintStride = 2.3f;

        static readonly int SpeedId = Animator.StringToHash(Speed), VSpeedId = Animator.StringToHash(VerticalSpeed);
        static readonly int ModeId = Animator.StringToHash(Mode), DashId = Animator.StringToHash(Dash);
        static readonly int RateId = Animator.StringToHash(Rate), PitchId = Animator.StringToHash(DivePitch), LandId = Animator.StringToHash(Land);

        public readonly Animator Animator;
        float _weapon;

        public HeroMecanim(Animator animator) { Animator = animator; }

        public static int ModeOf(HeroMotion m)
        {
            switch (m)
            {
                case HeroMotion.Air: return Air;
                case HeroMotion.Climb: return Climb;
                case HeroMotion.Swim: return Swim;
                case HeroMotion.Fly: return Fly;
                case HeroMotion.Dive: return Dive;
                default: return Ground;
            }
        }

        /// <summary>The base-layer state the input settles in (for snapping after a rebuild).</summary>
        public static string StateFor(HeroAnimInput a)
        {
            switch (ModeOf(a.Motion))
            {
                case Air: return a.Dash ? SDash : a.VerticalSpeed > 0.5f ? SJump : SFall;
                case Climb: return SClimb;
                case Swim: return SSwim;
                case Fly: return SFly;
                case Dive: return SDive;
                default: return a.Dash ? SDash : a.LandingSquash > 0.15f ? SLand : a.Speed > 0.3f ? SMove : SIdle;
            }
        }

        /// <summary>Sets the parameters for this frame (dt smooths the speed and the weapon layer).</summary>
        public void Push(HeroAnimInput a, float dt)
        {
            float speed = a.Motion == HeroMotion.Idle ? Mathf.Min(a.Speed, 0.2f) : a.Speed;
            Animator.SetFloat(SpeedId, speed, 0.06f, Mathf.Max(dt, 1e-4f));
            Animator.SetFloat(VSpeedId, a.VerticalSpeed);
            Animator.SetInteger(ModeId, ModeOf(a.Motion));
            Animator.SetBool(DashId, a.Dash);
            Animator.SetFloat(PitchId, a.DivePitch);
            Animator.SetFloat(LandId, Mathf.Clamp01(a.LandingSquash));
            Animator.SetFloat(RateId, RateFor(a, Animator.GetFloat(SpeedId)));
            _weapon = Mathf.MoveTowards(_weapon, a.WeaponDrawn ? 1f : 0f, dt * 6f);
            if (Animator.layerCount > WeaponLayer) Animator.SetLayerWeight(WeaponLayer, _weapon);
        }

        /// <summary>Jumps straight to the input's state and pose (first frame, rebuilds, captures).</summary>
        public void Snap(HeroAnimInput a)
        {
            _weapon = a.WeaponDrawn ? 1f : 0f;
            Animator.Rebind();
            Push(a, 1f);
            Animator.SetFloat(SpeedId, a.Speed);
            Animator.Play(StateFor(a), 0, 0f);
            if (Animator.layerCount > WeaponLayer) Animator.Play("Ready", WeaponLayer, 0f);
            Animator.Update(0f);
        }

        /// <summary>
        /// Playback rate for the active cycle so the cadence follows the procedural original:
        /// strides per second while moving, the climb speed, and the swim/dive stroke rate.
        /// </summary>
        public static float RateFor(HeroAnimInput a, float speed)
        {
            switch (ModeOf(a.Motion))
            {
                case Climb: return a.ClimbSpeed / BakeClimbSpeed;
                case Swim:
                {
                    // Stroke rate 2.2 + 0.8 v rad/s; the blend (0 → 3.6 m/s) plays its own average duration.
                    float w = Mathf.Clamp01(speed / BakeSwimSpeed);
                    float natural = Mathf.Lerp(Period(2.2f), Period(2.2f + 0.8f * BakeSwimSpeed), w);
                    return natural / Period(2.2f + 0.8f * Mathf.Max(0f, speed));
                }
                case Dive: return (1.4f + 0.5f * Mathf.Max(0f, speed)) / (1.4f + 0.5f * BakeDiveSpeed);
                case Ground:
                {
                    if (speed <= 0.3f) return 1f;
                    // Move blend: Walk (1.5) → Run (6) → Sprint (9.5); one cycle per stride.
                    float wSprint = Mathf.Clamp01((speed - HeroAnimator.RunSpeed) / (HeroAnimator.SprintSpeed - HeroAnimator.RunSpeed));
                    float wRun = speed < HeroAnimator.RunSpeed ? Mathf.Clamp01((speed - WalkSpeed) / (HeroAnimator.RunSpeed - WalkSpeed)) : 1f - wSprint;
                    float wWalk = 1f - wRun - wSprint;
                    float natural = wWalk * (WalkStride / WalkSpeed) + wRun * (RunStride / HeroAnimator.RunSpeed) + wSprint * (SprintStride / HeroAnimator.SprintSpeed);
                    float stride = Mathf.Lerp(RunStride, SprintStride, wSprint);
                    return natural / (stride / speed);
                }
                default: return 1f;
            }
        }

        static float Period(float radPerSecond) => Mathf.PI * 2f / radPerSecond;
    }
}
