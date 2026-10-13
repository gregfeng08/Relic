using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Genshin-style third-person orbit camera: mouse yaw/pitch around a pivot above the player's
    /// head, slight shoulder offset, scroll zoom, sphere-cast collision against terrain, and it
    /// never dips under the height field even where colliders aren't loaded yet.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public PlayerConfig Config;
        public Transform Target;
        public TerrainProbe Probe;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 12f;

        Camera _cam;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _distance;
        float _currentDistance;
        bool _initialized;

        public Camera Cam => _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void Snap(float yaw)
        {
            Yaw = yaw;
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
            PlayerInputState input = PlayerInput.Read();

            Yaw += input.Look.x * Config.MouseSensitivity;
            Pitch = Mathf.Clamp(Pitch - input.Look.y * Config.MouseSensitivity, Config.CameraPitchMin, Config.CameraPitchMax);
            if (!_initialized)
            {
                _distance = Config.CameraDistance;
                _currentDistance = _distance;
                _pivot = Target.position + Config.CameraPivotOffset;
                _initialized = true;
            }
            _distance = Mathf.Clamp(_distance - input.Zoom * 0.8f, Config.CameraMinDistance, Config.CameraMaxDistance);

            // Follow: snappy horizontally, softer vertically so jumps and steps don't jolt the view.
            Vector3 wanted = Target.position + Config.CameraPivotOffset;
            Vector3 p = _pivot;
            p.x = Mathf.SmoothDamp(p.x, wanted.x, ref _pivotVelocity.x, 0.03f, Mathf.Infinity, dt);
            p.z = Mathf.SmoothDamp(p.z, wanted.z, ref _pivotVelocity.z, 0.03f, Mathf.Infinity, dt);
            p.y = Mathf.SmoothDamp(p.y, wanted.y, ref _pivotVelocity.y, 0.09f, Mathf.Infinity, dt);
            if ((p - wanted).sqrMagnitude > 25f) p = wanted; // teleports
            _pivot = p;

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 shoulder = rot * new Vector3(Config.CameraShoulder, 0f, 0f);
            Vector3 origin = _pivot + shoulder;
            Vector3 back = rot * Vector3.back;

            float allowed = _distance;
            if (Physics.SphereCast(origin, 0.25f, back, out RaycastHit hit, _distance, PlayerController.TerrainMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.3f, hit.distance - 0.05f);
            // Pull in instantly when blocked, ease back out when clear.
            _currentDistance = allowed < _currentDistance ? allowed : Mathf.MoveTowards(_currentDistance, allowed, 6f * dt);

            Vector3 camPos = origin + back * _currentDistance;
            if (Probe != null)
            {
                Probe(camPos, out float ground, out _);
                if (camPos.y < ground + 0.35f) camPos.y = ground + 0.35f;
            }
            transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(_pivot + shoulder * 0.5f - camPos, Vector3.up));
            if (_cam != null && !Mathf.Approximately(_cam.fieldOfView, Config.CameraFov)) _cam.fieldOfView = Config.CameraFov;
        }
    }
}
