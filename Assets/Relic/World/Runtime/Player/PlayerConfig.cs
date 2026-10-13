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
        public float GroundAcceleration = 60f;
        public float GroundDeceleration = 45f;
        [Tooltip("Degrees per second the body turns toward the move direction.")]
        public float TurnSpeed = 900f;
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
        public float DashDuration = 0.18f;
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

        [Header("Glide")]
        public float GlideSpeed = 9f;
        public float GlideFallSpeed = 2.4f;
        public float GlideTurnSpeed = 150f;
        public float GlideDrain = 5f;
        [Tooltip("Minimum height above ground to open the glider.")]
        public float GlideMinHeight = 2.2f;

        [Header("Swim")]
        public float SwimSpeed = 3.6f;
        [Tooltip("Depth of the body's center below the water surface while swimming.")]
        public float SwimDepth = 1.15f;

        [Header("Camera")]
        public float MouseSensitivity = 0.12f;
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
