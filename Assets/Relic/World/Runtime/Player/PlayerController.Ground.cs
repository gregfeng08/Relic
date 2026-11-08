using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>Ground contact, landing and facing: the parts that make movement feel smooth.</summary>
    public sealed partial class PlayerController
    {
        /// <summary>
        /// Visual-only vertical offset (m) for the avatar: absorbs step-ups and ground snaps so the
        /// body glides over them instead of popping. Decays to 0 in about 0.1 s. Avatars add it
        /// to their root's local position.
        /// </summary>
        public float VisualYOffset { get; private set; }

        /// <summary>Increments on every landing from the air (avatars trigger a squash on change).</summary>
        public int LandCount { get; private set; }

        /// <summary>Downward speed (m/s) at the last landing.</summary>
        public float LastLandSpeed { get; private set; }

        /// <summary>0..1 landing squash, 1 right after a hard landing, springing back to 0.</summary>
        public float LandingSquash { get; private set; }

        /// <summary>The body's smoothed facing yaw in degrees.</summary>
        public float FacingYaw => _yaw;

        /// <summary>What the feet stand on: terrain plus prop colliders (boulders). The camera and climbing use TerrainMask only.</summary>
        public const int GroundMask = TerrainMask | (1 << PropColliders.PropLayer);

        float _yaw, _yawVel;
        float _visualYVel;
        float _squashVel;
        float _landSlowTime;
        float _landSlowDuration = 1f;

        Vector3 Forward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

        /// <summary>Speed multiplier right after a hard landing (eases from 0.55 back to 1).</summary>
        float LandingSpeedFactor => _landSlowTime > 0f ? Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0f, 1f, _landSlowTime / _landSlowDuration)) : 1f;

        /// <summary>Turns the body toward dir with a critically damped yaw (no fixed-rate snapping).</summary>
        void FaceTowards(Vector3 dir, float smoothTime, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _yaw = Mathf.SmoothDampAngle(_yaw, target, ref _yawVel, Mathf.Max(0.001f, smoothTime), Config.TurnSpeed, dt);
            _yaw = Mathf.Repeat(_yaw, 360f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void ProbeGround()
        {
            Vector3 origin = transform.position + Vector3.up * (_cc.radius + 0.1f);
            if (Physics.SphereCast(origin, _cc.radius * 0.9f, Vector3.down, out RaycastHit hit, 0.6f, GroundMask, QueryTriggerInteraction.Ignore))
                _groundNormal = hit.normal;
            else
                _groundNormal = Vector3.up;
        }

        /// <summary>Vertical speed that keeps a horizontal move on the ground plane (so running downhill hugs the slope).</summary>
        float SlopeFollowY(Vector3 horizontal)
        {
            Vector3 n = _groundNormal;
            if (n.y < 0.2f) return 0f;
            return -(n.x * horizontal.x + n.z * horizontal.z) / n.y;
        }

        /// <summary>
        /// After losing contact while grounded (crest of a slope, a small drop): if walkable
        /// ground is within GroundSnapDistance below, move onto it and stay grounded.
        /// </summary>
        bool TrySnapToGround()
        {
            float r = _cc.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (_cc.radius + 0.05f);
            float maxDist = Config.GroundSnapDistance + 0.05f;
            if (!Physics.SphereCast(origin, r, Vector3.down, out RaycastHit hit, maxDist, GroundMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < SlopeCos) return false;
            // The capsule stops on contact, so overshooting a little is safe.
            float drop = hit.distance - 0.05f - (_cc.radius - r);
            _cc.Move(Vector3.down * (Mathf.Max(0f, drop) + _cc.skinWidth + 0.02f));
            _groundNormal = hit.normal;
            if (_velocity.y > 0f) _velocity.y = 0f;
            return true;
        }

        /// <summary>Unplanned vertical jumps while grounded (step-ups, snaps) go into the visual offset.</summary>
        void AbsorbStep(float unplannedDy)
        {
            if (Mathf.Abs(unplannedDy) < 0.03f) return;
            VisualYOffset = Mathf.Clamp(VisualYOffset - unplannedDy, -0.6f, 0.6f);
        }

        void OnLanded(float fallSpeed)
        {
            LandCount++;
            LastLandSpeed = Mathf.Max(0f, fallSpeed);
            float impact = Mathf.Clamp01((LastLandSpeed - 3f) / 15f);
            _squashVel -= impact * 9f; // the squash spring kicks down, then recovers
            if (LastLandSpeed > Config.HardLandingSpeed)
            {
                _landSlowDuration = Mathf.Lerp(0.12f, 0.3f, Mathf.InverseLerp(Config.HardLandingSpeed, Config.HardLandingSpeed * 2f, LastLandSpeed));
                _landSlowTime = _landSlowDuration;
            }
        }

        /// <summary>Decays the visual offsets and the landing squash spring.</summary>
        void UpdateFeel(float dt)
        {
            VisualYOffset = Mathf.SmoothDamp(VisualYOffset, 0f, ref _visualYVel, 0.06f, Mathf.Infinity, dt);
            // Critically damped spring (omega 14) on the squash amount, kicked by landings.
            const float omega = 14f;
            float s = -LandingSquash; // spring position (negative = squashed)
            float a = -omega * omega * s - 2f * omega * _squashVel;
            _squashVel += a * dt;
            s += _squashVel * dt;
            LandingSquash = Mathf.Clamp01(-s);
            if (_landSlowTime > 0f) _landSlowTime = Mathf.Max(0f, _landSlowTime - dt);
        }
    }
}
