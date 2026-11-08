using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Every movement and stamina constant for the M1 player in one place. Stats (VIT, AGI)
    /// will scale these later. Tune in the inspector on Assets/Relic/World/Settings/PlayerConfig.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Relic/World/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Ground")]
        public float RunSpeed = 6.0f;
        public float SprintSpeed = 9.5f;
        [Tooltip("Legacy linear acceleration (m/s²); now only caps the smoothed acceleration.")]
        public float GroundAcceleration = 60f;
        public float GroundDeceleration = 45f;
        [Tooltip("Critically damped speed response when starting to move (s; ~90% of top speed after 2x this).")]
        public float AccelTime = 0.1f;
        [Tooltip("Speed response when letting go of the stick (s).")]
        public float StopTime = 0.07f;
        [Tooltip("Speed response when the input points against the current velocity (s).")]
        public float ReverseTime = 0.09f;
        [Tooltip("Maximum degrees per second the body turns toward the move direction.")]
        public float TurnSpeed = 900f;
        [Tooltip("Smoothing time (s) of the body's facing: lower = snappier turns.")]
        public float TurnSmoothTime = 0.07f;
        [Tooltip("Snap down onto ground up to this far below the feet (m) instead of going airborne on slopes and steps.")]
        public float GroundSnapDistance = 0.5f;
        [Tooltip("Falls faster than this (m/s) slow you briefly on landing.")]
        public float HardLandingSpeed = 13f;
        [Tooltip("Walkable slope limit in degrees; steeper ground slides and can be climbed.")]
        public float SlopeLimit = 48f;
        public float SlideSpeed = 9f;

        [Header("Air")]
        public float Gravity = 24f;
        public float JumpHeight = 1.5f;
        public float AirAcceleration = 14f;
        public float MaxFallSpeed = 45f;
        [Tooltip("Grace time after leaving a ledge during which jump still works.")]
        public float CoyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing still fires.")]
        public float JumpBuffer = 0.12f;

        [Header("Dash (no invulnerability)")]
        public float DashDistance = 4f;
        [Tooltip("Seconds. The dash eases out from a fast start into run or sprint speed.")]
        public float DashDuration = 0.24f;
        public float DashStamina = 18f;
        public float DashCooldown = 0.3f;

        [Header("Stamina")]
        public float MaxStamina = 240f;
        public float StaminaRegen = 30f;
        public float StaminaRegenDelay = 0.9f;
        public float SprintDrain = 16f;

        [Header("Climb")]
        public float ClimbSpeed = 2.6f;
        public float ClimbDrain = 9f;
        public float ClimbJumpStamina = 22f;
        public float ClimbJumpHeight = 1.4f;
        [Tooltip("Speed of the vault when topping out a climb.")]
        public float VaultUpSpeed = 6.5f;
        [Tooltip("Surfaces must be at least this steep (degrees) to be grabbed as a wall.")]
        public float ClimbMinAngle = 65f;
        [Tooltip("Seconds of pushing into a wall on foot before grabbing it (airborne grabs are immediate).")]
        public float ClimbGrabDelay = 0.2f;


        [Header("Swim")]
        public float SwimSpeed = 3.6f;
        [Tooltip("Depth of the body's center below the water surface while swimming.")]
        public float SwimDepth = 1.15f;

        [Header("Dive (Ctrl/C down, Space up)")]
        public float DiveSpeed = 3.2f;
        public float DiveVerticalSpeed = 2.4f;
        [Tooltip("Smoothing time (s) of underwater velocity: high = floaty.")]
        public float DiveSmoothTime = 0.38f;
        [Tooltip("Slow upward drift (m/s) while diving without vertical input.")]
        public float DiveBuoyancy = 0.35f;
        [Tooltip("Seconds of breath underwater.")]
        public float BreathMax = 25f;
        [Tooltip("Breath regained per second at the surface.")]
        public float BreathRegen = 10f;
        [Tooltip("Stamina drained per second while out of breath (forced to surface).")]
        public float DrownStaminaDrain = 30f;

        [Header("Camera")]
        public float MouseSensitivity = 0.12f;
        [Tooltip("Smoothing (s) of mouse yaw/pitch. 0 = raw.")]
        public float CameraLookSmooth = 0.03f;
        [Tooltip("Smoothing (s) of the camera's horizontal follow.")]
        public float CameraFollowTime = 0.05f;
        [Tooltip("Smoothing (s) of the camera's vertical follow on the ground (steps, landings).")]
        public float CameraVerticalTime = 0.11f;
        [Tooltip("Smoothing (s) of the camera's vertical follow in the air (keeps jumps calm).")]
        public float CameraAirVerticalTime = 0.24f;
        public float CameraDistance = 5.5f;
        public float CameraMinDistance = 1.5f;
        public float CameraMaxDistance = 12f;
        public float CameraPitchMin = -40f;
        public float CameraPitchMax = 75f;
        public float CameraFov = 55f;
        public Vector3 CameraPivotOffset = new Vector3(0f, 1.55f, 0f);
        [Tooltip("Sideways shoulder offset, meters.")]
        public float CameraShoulder = 0.35f;
    }
}
