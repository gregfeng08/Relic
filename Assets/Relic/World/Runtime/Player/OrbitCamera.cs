using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Genshin-style third-person orbit camera: mouse yaw/pitch around a pivot above the player's
    /// head, slight shoulder offset, scroll zoom, sphere-cast collision against terrain, and it
    /// never dips under the height field even where colliders aren't loaded yet.
    ///
    /// Smoothness: runs in LateUpdate after the controller (which moves in Update). Look,
    /// follow, collision distance and the ground clamp are all critically damped (SmoothDamp),
    /// so nothing snaps: jumps and steps are followed calmly, a blocked view pulls in fast
    /// but smoothly and recovers slowly. Floating-origin shifts move every stored position.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public PlayerConfig Config;
        public Transform Target;
        public TerrainProbe Probe;

        /// <summary>Smoothed yaw the camera shows (degrees). Movement is relative to it.</summary>
        public float Yaw { get; private set; }
        /// <summary>Smoothed pitch (degrees, positive looks down).</summary>
        public float Pitch { get; private set; } = 12f;
        /// <summary>True when the camera itself is below the local water surface.</summary>
        public bool Underwater { get; private set; }
        /// <summary>Meters of water above the camera (0 when above the surface).</summary>
        public float CameraWaterDepth { get; private set; }

        Camera _cam;
        PlayerController _player;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _targetYaw, _targetPitch = 12f;
        float _yawVel, _pitchVel;
        float _distance;
        float _currentDistance;
        float _distanceVel;
        float _lift, _liftVel;
        bool _initialized;

        public Camera Cam => _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void Snap(float yaw)
        {
            Yaw = _targetYaw = yaw;
            _yawVel = 0f;
            _initialized = false;
        }

        /// <summary>Called by the floating origin after a shift so smoothing doesn't swing across the world.</summary>
        public void OnOriginShifted(Vector3 delta)
        {
            _pivot -= delta;
        }

        void LateUpdate()
        {
            if (Target == null || Config == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            if (_player == null) _player = Target.GetComponent<PlayerController>();
            PlayerInputState input = PlayerInput.Read();

            _targetYaw += input.Look.x * Config.MouseSensitivity;
            _targetPitch = Mathf.Clamp(_targetPitch - input.Look.y * Config.MouseSensitivity, Config.CameraPitchMin, Config.CameraPitchMax);
            if (Config.CameraLookSmooth > 0f)
            {
                Yaw = Mathf.SmoothDampAngle(Yaw, _targetYaw, ref _yawVel, Config.CameraLookSmooth, Mathf.Infinity, dt);
                Pitch = Mathf.SmoothDamp(Pitch, _targetPitch, ref _pitchVel, Config.CameraLookSmooth, Mathf.Infinity, dt);
            }
            else
            {
                Yaw = _targetYaw;
                Pitch = _targetPitch;
            }

            Vector3 wanted = Target.position + Config.CameraPivotOffset;
            if (!_initialized)
            {
                _distance = Config.CameraDistance;
                _currentDistance = _distance;
                _pivot = wanted;
                _pivotVelocity = Vector3.zero;
                _initialized = true;
            }
            _distance = Mathf.Clamp(_distance - input.Zoom * 0.8f, Config.CameraMinDistance, Config.CameraMaxDistance);

            FollowPivot(wanted, dt);

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 shoulder = rot * new Vector3(Config.CameraShoulder, 0f, 0f);
            Vector3 origin = _pivot + shoulder;
            Vector3 back = rot * Vector3.back;

            // Collision: pull in fast (but smoothly) when blocked, ease back out when clear.
            float allowed = _distance;
            if (Physics.SphereCast(origin, 0.3f, back, out RaycastHit hit, _distance, PlayerController.TerrainMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.4f, hit.distance - 0.1f);
            float response = allowed < _currentDistance ? 0.05f : 0.35f;
            _currentDistance = Mathf.SmoothDamp(_currentDistance, allowed, ref _distanceVel, response, Mathf.Infinity, dt);
            // Never more than a little past the obstacle while catching up.
            if (_currentDistance > allowed + 0.4f) _currentDistance = allowed + 0.4f;

            Vector3 camPos = origin + back * _currentDistance;
            float water = float.MinValue;
            if (Probe != null)
            {
                Probe(camPos, out float ground, out water);
                // Ground clamp, smoothed: lift over bumps rather than snapping to them.
                float need = Mathf.Max(0f, ground + 0.45f - camPos.y);
                _lift = Mathf.SmoothDamp(_lift, need, ref _liftVel, 0.06f, Mathf.Infinity, dt);
                camPos.y += Mathf.Max(_lift, need - 0.25f);
            }
            CameraWaterDepth = Mathf.Max(0f, water - camPos.y);
            Underwater = CameraWaterDepth > 0.02f;

            transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(_pivot + shoulder * 0.5f - camPos, Vector3.up));
            if (_cam != null && !Mathf.Approximately(_cam.fieldOfView, Config.CameraFov)) _cam.fieldOfView = Config.CameraFov;
        }

        /// <summary>
        /// Critically damped follow: quick horizontally, calmer vertically (more so in the air, so a
        /// jump doesn't bob the view), catching up faster the further it lags so it never drifts away.
        /// </summary>
        void FollowPivot(Vector3 wanted, float dt)
        {
            Vector3 p = _pivot;
            if ((p - wanted).sqrMagnitude > 64f) // teleports
            {
                _pivot = wanted;
                _pivotVelocity = Vector3.zero;
                return;
            }
            bool air = _player != null && (_player.State == MoveState.Airborne || _player.State == MoveState.Climbing);
            float vTime = air ? Config.CameraAirVerticalTime : Config.CameraVerticalTime;
            float lag = Mathf.Abs(wanted.y - p.y);
            vTime *= Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(1f, 3f, lag));
            p.x = Mathf.SmoothDamp(p.x, wanted.x, ref _pivotVelocity.x, Config.CameraFollowTime, Mathf.Infinity, dt);
            p.z = Mathf.SmoothDamp(p.z, wanted.z, ref _pivotVelocity.z, Config.CameraFollowTime, Mathf.Infinity, dt);
            p.y = Mathf.SmoothDamp(p.y, wanted.y, ref _pivotVelocity.y, vTime, Mathf.Infinity, dt);
            _pivot = p;
        }
    }
}
