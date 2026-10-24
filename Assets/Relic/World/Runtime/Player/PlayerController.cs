using UnityEngine;

namespace Relic.World.Runtime
{
    public enum MoveState { Grounded, Airborne, Climbing, Swimming, Flying }

    /// <summary>Asks the world for the terrain and water height at a scene position (pure height function, no colliders needed).</summary>
    public delegate void TerrainProbe(Vector3 scenePosition, out float groundHeight, out float waterLevel);

    /// <summary>
    /// M1 third-person movement on a CharacterController: run, sprint (stamina), dash (~4 m in
    /// ~0.18 s, stamina, NO invulnerability: it only moves the body), jump, climb steep surfaces,
    /// glide from heights, swim. Movement is relative to the camera's yaw, Genshin style.
    /// Shift: press = dash, keep holding = sprint (Genshin). Space: jump, climb-jump. No glider.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerConfig Config;
        public OrbitCamera OrbitCam;
        public TerrainProbe Probe;
        /// <summary>When true the player ignores input and physics (spawn wait).</summary>
        public bool Frozen = true;

        public MoveState State { get; private set; } = MoveState.Airborne;
        public float Stamina { get; private set; }
        public bool Exhausted { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDashing => _dashTimeLeft > 0f;
        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
        public Vector3 WallNormal => _wallNormal;
        public int DashCount { get; private set; }

        /// <summary>Benchmark: moved along a straight line at a fixed speed, hugging the terrain.</summary>
        public bool AutoRun;
        public Vector3 AutoRunDirection = Vector3.right;
        public float AutoRunSpeed = 9.5f;

        public const int TerrainMask = 1 << 0; // terrain chunks live on the Default layer

        CharacterController _cc;
        Vector3 _velocity;
        float _lastGroundedTime = -10f;
        float _jumpPressedTime = -10f;
        float _staminaUsedTime = -10f;
        float _dashTimeLeft;
        float _dashReadyTime;
        Vector3 _dashDir;
        float _climbPushTime;
        Vector3 _groundNormal = Vector3.up;
        Vector3 _wallNormal = Vector3.forward;
        float _climbJumpLeft;
        float _stateTime;

        float SlopeCos => Mathf.Cos(Config.SlopeLimit * Mathf.Deg2Rad);

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            if (Config == null) Config = ScriptableObject.CreateInstance<PlayerConfig>();
            Stamina = Config.MaxStamina;
            _cc.slopeLimit = Config.SlopeLimit + 2f;
            _cc.stepOffset = 0.35f;
            _cc.skinWidth = 0.04f;
            _cc.minMoveDistance = 0f;
        }

        /// <summary>Places the player (used at spawn and by tools); clears velocity.</summary>
        public void Teleport(Vector3 scenePosition)
        {
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.position = scenePosition;
            _cc.enabled = was;
            _velocity = Vector3.zero;
            SetState(MoveState.Airborne);
        }

        public void ToggleFly()
        {
            SetState(State == MoveState.Flying ? MoveState.Airborne : MoveState.Flying);
            _velocity = Vector3.zero;
        }

        void Update()
        {
            if (Frozen || Config == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            PlayerInputState input = PlayerInput.Read();
            if (input.JumpPressed) _jumpPressedTime = Time.time;
            _stateTime += dt;

            if (AutoRun) { UpdateAutoRun(dt); return; }

            Vector3 moveWorld = CameraRelative(input.Move);

            switch (State)
            {
                case MoveState.Grounded:
                case MoveState.Airborne: UpdateLocomotion(dt, input, moveWorld); break;
                case MoveState.Climbing: UpdateClimb(dt, input); break;
                case MoveState.Swimming: UpdateSwim(dt, input, moveWorld); break;
                case MoveState.Flying: UpdateFly(dt, input, moveWorld); break;
            }

            UpdateStamina(dt);
            SafetyNet();
        }

        // --- Locomotion ------------------------------------------------------------------

        void UpdateLocomotion(float dt, PlayerInputState input, Vector3 moveWorld)
        {
            bool grounded = State == MoveState.Grounded;
            if (grounded) _lastGroundedTime = Time.time;
            ProbeGround();
            bool steep = grounded && _groundNormal.y < SlopeCos;

            // Dash: a burst of movement. It never grants invulnerability.
            if (input.SprintPressed && grounded && !steep && !Exhausted && Time.time >= _dashReadyTime && Stamina > 0f)
            {
                _dashDir = moveWorld.sqrMagnitude > 0.01f ? moveWorld.normalized : transform.forward;
                _dashTimeLeft = Config.DashDuration;
                _dashReadyTime = Time.time + Config.DashDuration + Config.DashCooldown;
                UseStamina(Config.DashStamina);
                DashCount++;
            }

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            IsSprinting = false;
            if (_dashTimeLeft > 0f)
            {
                // Genshin-style: a fast start that eases out into the speed you'll keep, so
                // a held Shift flows straight into sprint and a tap settles into a run. The
                // linear ramp from v0 down to vExit covers DashDistance exactly.
                float vExit = input.SprintHeld && !Exhausted ? Config.SprintSpeed : Config.RunSpeed;
                float v0 = Mathf.Max(vExit, 2f * Config.DashDistance / Config.DashDuration - vExit);
                float t01 = 1f - Mathf.Clamp01(_dashTimeLeft / Config.DashDuration);
                _dashTimeLeft -= dt;
                horizontal = _dashDir * Mathf.Lerp(v0, vExit, t01);
                FaceDirection(_dashDir, dt * 3f);
            }
            else
            {
                bool moving = moveWorld.sqrMagnitude > 0.01f;
                IsSprinting = input.SprintHeld && moving && grounded && !Exhausted && Stamina > 0f;
                float speed = IsSprinting ? Config.SprintSpeed : Config.RunSpeed;
                Vector3 target = moveWorld * speed;
                float accel = grounded ? (moving ? Config.GroundAcceleration : Config.GroundDeceleration) : Config.AirAcceleration;
                // Keep momentum after a dash/sprint in the air; only steer.
                horizontal = Vector3.MoveTowards(horizontal, target, accel * dt);
                if (moving) FaceDirection(moveWorld, dt);
                if (IsSprinting) UseStamina(Config.SprintDrain * dt);
            }

            // Steep ground: slide down, can't walk up.
            if (steep)
            {
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                horizontal += new Vector3(downSlope.x, 0f, downSlope.z) * Config.SlideSpeed * dt * 4f;
                float into = Vector3.Dot(horizontal, new Vector3(-_groundNormal.x, 0f, -_groundNormal.z).normalized);
                if (into > 0f) horizontal -= new Vector3(-_groundNormal.x, 0f, -_groundNormal.z).normalized * into;
            }

            float vy = _velocity.y;
            bool canJump = (grounded && !steep) || (Time.time - _lastGroundedTime <= Config.CoyoteTime && vy <= 0.5f);
            bool jumped = false;
            if (Time.time - _jumpPressedTime <= Config.JumpBuffer && canJump)
            {
                vy = Mathf.Sqrt(2f * Config.Gravity * Config.JumpHeight);
                _jumpPressedTime = -10f;
                _lastGroundedTime = -10f;
                jumped = true;
            }
            else if (grounded && vy <= 0f)
            {
                vy = -3f; // stick to slopes and steps
            }
            else
            {
                vy = Mathf.Max(vy - Config.Gravity * dt, -Config.MaxFallSpeed);
            }

            _velocity = new Vector3(horizontal.x, vy, horizontal.z);
            CollisionFlags flags = _cc.Move(_velocity * dt);
            bool nowGrounded = (flags & CollisionFlags.Below) != 0 || (_cc.isGrounded && vy <= 0f);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;

            if (nowGrounded && !jumped) SetState(MoveState.Grounded);
            else if (State == MoveState.Grounded && !nowGrounded) SetState(MoveState.Airborne);

            if (TryEnterSwim()) return;
            if (moveWorld.sqrMagnitude > 0.01f && _dashTimeLeft <= 0f && TryStartClimb(moveWorld, dt)) return;
        }

        // --- Climb -----------------------------------------------------------------------

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
            bool airborne = State == MoveState.Airborne || State == MoveState.Swimming;
            if (!immediate && !airborne)
            {
                _climbPushTime += dt;
                if (_climbPushTime < Config.ClimbGrabDelay) return false;
            }
            _climbPushTime = 0f;
            _wallNormal = hit.normal;
            _velocity = Vector3.zero;
            _climbJumpLeft = 0f;
            SetState(MoveState.Climbing);
            FaceDirection(-FlatNormal(hit.normal), 1f);
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
            FaceDirection(-FlatNormal(n), dt * 2f);

            if (input.JumpPressed)
            {
                _jumpPressedTime = -10f;
                if (input.Move.y < -0.5f)
                {
                    // Leap away from the wall.
                    _velocity = FlatNormal(n) * 4.5f + Vector3.up * 3.5f;
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
            }
            else
            {
                v = (up * input.Move.y + right * input.Move.x) * Config.ClimbSpeed;
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

        // --- Swim ------------------------------------------------------------------------

        bool TryEnterSwim()
        {
            if (Probe == null) return false;
            Probe(transform.position, out float ground, out float water);
            if (water - ground < Config.SwimDepth + 0.1f) return false;
            if (transform.position.y > water - Config.SwimDepth) return false;
            SetState(MoveState.Swimming);
            _velocity.y = Mathf.Max(_velocity.y, -2f);
            return true;
        }

        void UpdateSwim(float dt, PlayerInputState input, Vector3 moveWorld)
        {
            float ground = transform.position.y - 10f, water = transform.position.y;
            if (Probe != null) Probe(transform.position, out ground, out water);
            float targetY = water - Config.SwimDepth;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), moveWorld * Config.SwimSpeed, 10f * dt);
            float vy = Mathf.Clamp((targetY - transform.position.y) * 4f, -6f, 3f);
            _velocity = new Vector3(horizontal.x, vy, horizontal.z);
            if (moveWorld.sqrMagnitude > 0.01f) FaceDirection(moveWorld, dt);
            _cc.Move(_velocity * dt);

            if (water - ground < Config.SwimDepth)
            {
                SetState(MoveState.Grounded);
                return;
            }
            if (moveWorld.sqrMagnitude > 0.01f) TryStartClimb(moveWorld, dt, immediate: true);
        }

        // --- Debug fly and benchmark -----------------------------------------------------

        void UpdateFly(float dt, PlayerInputState input, Vector3 moveWorld)
        {
            float speed = input.SprintHeld ? 120f : 35f;
            float vertical = (input.JumpHeld ? 1f : 0f) - (input.DescendHeld ? 1f : 0f);
            _velocity = (moveWorld + Vector3.up * vertical) * speed;
            if (moveWorld.sqrMagnitude > 0.01f) FaceDirection(moveWorld, dt);
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.position += _velocity * dt;
            _cc.enabled = was;
            if (Probe != null)
            {
                Probe(transform.position, out float ground, out _);
                if (transform.position.y < ground + 0.5f) transform.position = new Vector3(transform.position.x, ground + 0.5f, transform.position.z);
            }
        }

        void UpdateAutoRun(float dt)
        {
            Vector3 dir = AutoRunDirection.normalized;
            _velocity = dir * AutoRunSpeed;
            IsSprinting = true;
            FaceDirection(dir, dt);
            Vector3 p = transform.position + _velocity * dt;
            if (Probe != null)
            {
                Probe(p, out float ground, out float water);
                p.y = Mathf.Max(ground, water - Config.SwimDepth) + 0.05f;
            }
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.position = p;
            _cc.enabled = was;
            State = MoveState.Grounded;
        }

        // --- Helpers -----------------------------------------------------------------------

        void UpdateStamina(float dt)
        {
            bool draining = State == MoveState.Climbing || IsSprinting || IsDashing;
            if (!draining && Time.time - _staminaUsedTime >= Config.StaminaRegenDelay)
                Stamina = Mathf.Min(Config.MaxStamina, Stamina + Config.StaminaRegen * dt);
            if (Stamina <= 0f) Exhausted = true;
            if (Exhausted && Stamina >= Config.MaxStamina * 0.25f) Exhausted = false;
        }

        void UseStamina(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            _staminaUsedTime = Time.time;
        }

        void ProbeGround()
        {
            Vector3 origin = transform.position + Vector3.up * (_cc.radius + 0.1f);
            if (Physics.SphereCast(origin, _cc.radius * 0.9f, Vector3.down, out RaycastHit hit, 0.6f, TerrainMask, QueryTriggerInteraction.Ignore))
                _groundNormal = hit.normal;
            else
                _groundNormal = Vector3.up;
        }

        float HeightAboveGround()
        {
            Vector3 p = transform.position;
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 60f, TerrainMask, QueryTriggerInteraction.Ignore))
                return hit.distance - 0.1f;
            if (Probe != null)
            {
                Probe(p, out float ground, out float water);
                return p.y - Mathf.Max(ground, water);
            }
            return 0f;
        }

        /// <summary>If the player ever ends up under the terrain (collider not ready yet), put them back on top.</summary>
        void SafetyNet()
        {
            if (Probe == null || State == MoveState.Flying) return;
            Probe(transform.position, out float ground, out _);
            if (transform.position.y < ground - 2.5f)
            {
                Teleport(new Vector3(transform.position.x, ground + 0.5f, transform.position.z));
            }
        }

        Vector3 CameraRelative(Vector2 move)
        {
            float yaw = OrbitCam != null ? OrbitCam.Yaw : transform.eulerAngles.y;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 v = q * new Vector3(move.x, 0f, move.y);
            return Vector3.ClampMagnitude(v, 1f);
        }

        void FaceDirection(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, Config.TurnSpeed * dt);
        }

        static Vector3 FlatNormal(Vector3 n)
        {
            var f = new Vector3(n.x, 0f, n.z);
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        void SetState(MoveState s)
        {
            if (s == State) return;
            State = s;
            _stateTime = 0f;
            if (s != MoveState.Grounded) IsSprinting = false;
        }
    }
}
