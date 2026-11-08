using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>Climbing real walls: grab, climb, climb-jump, leap off, vault over the top.</summary>
    public sealed partial class PlayerController
    {
        Vector3 _climbVel; // smoothed climb velocity (eases in and out instead of starting at full speed)
        Vector3 _climbAccel;

        /// <summary>
        /// Grab a wall only when it's really a wall: steep (ClimbMinAngle), at least as tall as
        /// the hero (the head ray hits it too), and either we're airborne/swimming or we've
        /// been pushing into it on foot for ClimbGrabDelay. Small cliff bands and steep bumps
        /// are run over or slid down instead of grabbed.
        /// </summary>
        bool TryStartClimb(Vector3 moveWorld, float dt = 0f, bool immediate = false)
        {
            if (Stamina <= 0f || Exhausted) { _climbPushTime = 0f; return false; }
            Vector3 dir = moveWorld.normalized;
            Vector3 chest = transform.position + Vector3.up * 1.1f;
            float reach = _cc.radius + 0.35f;
            float wallCos = Mathf.Cos(Config.ClimbMinAngle * Mathf.Deg2Rad);
            if (!Physics.SphereCast(chest, 0.2f, dir, out RaycastHit hit, reach, TerrainMask, QueryTriggerInteraction.Ignore)
                || hit.normal.y > wallCos                                             // not steep enough to be a wall
                || Vector3.Dot(dir, -FlatNormal(hit.normal)) < 0.5f                    // not pushing into it
                || !Physics.Raycast(transform.position + Vector3.up * 1.75f, dir, reach + 0.3f, TerrainMask, QueryTriggerInteraction.Ignore)) // shorter than us: step/slide, don't grab
            {
                _climbPushTime = 0f;
                return false;
            }
            bool airborne = State == MoveState.Airborne || State == MoveState.Swimming || State == MoveState.Diving;
            if (!immediate && !airborne)
            {
                _climbPushTime += dt;
                if (_climbPushTime < Config.ClimbGrabDelay) return false;
            }
            _climbPushTime = 0f;
            _wallNormal = hit.normal;
            // Carry a little of the upward momentum into the climb, so a jump-grab flows on.
            _climbVel = Vector3.ProjectOnPlane(new Vector3(0f, Mathf.Clamp(_velocity.y, 0f, 3f), 0f), hit.normal);
            _climbAccel = Vector3.zero;
            _velocity = _climbVel;
            _climbJumpLeft = 0f;
            _accelRef = Vector3.zero;
            SetState(MoveState.Climbing);
            return true;
        }

        void UpdateClimb(float dt, PlayerInputState input)
        {
            Vector3 pos = transform.position;
            Vector3 n = _wallNormal;
            bool chestHit = Physics.Raycast(pos + Vector3.up * 1.2f, -n, out RaycastHit chest, _cc.radius + 0.9f, TerrainMask, QueryTriggerInteraction.Ignore);
            bool feetHit = Physics.Raycast(pos + Vector3.up * 0.25f, -n, out RaycastHit feet, _cc.radius + 0.9f, TerrainMask, QueryTriggerInteraction.Ignore);

            if (!chestHit)
            {
                if (feetHit)
                {
                    // Topped out: vault up and over the edge.
                    _velocity = Vector3.up * Config.VaultUpSpeed - FlatNormal(n) * 3f;
                    SetState(MoveState.Airborne);
                }
                else
                {
                    _velocity = FlatNormal(n) * 1.5f;
                    SetState(MoveState.Airborne);
                }
                return;
            }

            n = chest.normal;
            if (n.y >= SlopeCos + 0.05f)
            {
                SetState(MoveState.Airborne); // the wall flattened out: walk again
                return;
            }
            _wallNormal = n;
            FaceTowards(-FlatNormal(n), 0.06f, dt);

            if (input.JumpPressed)
            {
                _jumpPressedTime = -10f;
                if (input.Move.y < -0.5f)
                {
                    // Leap away from the wall.
                    _velocity = FlatNormal(n) * 4.5f + Vector3.up * 3.5f;
                    FaceTowards(FlatNormal(n), 0.06f, dt);
                    SetState(MoveState.Airborne);
                    return;
                }
                if (Stamina >= Config.ClimbJumpStamina * 0.5f)
                {
                    UseStamina(Config.ClimbJumpStamina);
                    _climbJumpLeft = 0.3f;
                }
            }

            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -FlatNormal(n)).normalized;
            right = Vector3.ProjectOnPlane(right, n).normalized;
            Vector3 v;
            if (_climbJumpLeft > 0f)
            {
                _climbJumpLeft -= dt;
                v = up * (Config.ClimbJumpHeight / 0.3f);
                _climbVel = v;
                _climbAccel = Vector3.zero;
            }
            else
            {
                Vector3 target = (up * input.Move.y + right * input.Move.x) * Config.ClimbSpeed;
                _climbVel = Vector3.SmoothDamp(_climbVel, target, ref _climbAccel, 0.08f, Mathf.Infinity, dt);
                v = _climbVel;
            }
            bool moving = v.sqrMagnitude > 0.01f;
            UseStamina((moving ? Config.ClimbDrain : 1f) * dt);
            _velocity = v;

            CollisionFlags flags = _cc.Move((v - n * 0.6f) * dt);
            if (Stamina <= 0f)
            {
                _velocity = FlatNormal(n) * 1f;
                SetState(MoveState.Airborne);
                return;
            }
            if ((flags & CollisionFlags.Below) != 0 && input.Move.y < -0.1f) SetState(MoveState.Grounded);
        }
    }
}
