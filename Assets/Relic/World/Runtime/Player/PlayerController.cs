using UnityEngine;

namespace Relic.World.Runtime
{
    public enum MoveState { Grounded, Airborne, Climbing, Swimming, Flying, Diving }

    /// <summary>Asks the world for the terrain and water height at a scene position (pure height function, no colliders needed).</summary>
    public delegate void TerrainProbe(Vector3 scenePosition, out float groundHeight, out float waterLevel);

    /// <summary>
    /// M1 third-person movement on a CharacterController: run, sprint (stamina), dash (~4 m in
    /// ~0.24 s, stamina, NO invulnerability: it only moves the body), jump, climb real walls,
    /// swim and dive. Movement is relative to the camera's yaw, Genshin style.
    /// Shift: press = dash, keep holding = sprint (Genshin). Space: jump, climb-jump. No glider.
    ///
    /// Feel: speed follows a critically damped response (eases in and out, no velocity steps),
    /// the body turns with a smooth-damped yaw and the run direction follows the body, so turns
    /// arc instead of cutting corners. Ground is snapped onto across slopes and small drops, so
    /// the state doesn't flicker. Split into partial files: Ground (snap, landing, facing),
    /// Climb, Water (swim, dive, breath).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PlayerController : MonoBehaviour
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
        Vector3 _accelRef;        // SmoothDamp state of the horizontal velocity
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
            _yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            if (Config == null) Config = ScriptableObject.CreateInstance<PlayerConfig>();
            Stamina = Config.MaxStamina;
            Breath = Config.BreathMax;
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
            _accelRef = Vector3.zero;
            _yaw = transform.eulerAngles.y;
            _yawVel = 0f;
            VisualYOffset = 0f;
            SetState(MoveState.Airborne);
        }

        public void ToggleFly()
        {
            SetState(State == MoveState.Flying ? MoveState.Airborne : MoveState.Flying);
            _velocity = Vector3.zero;
            _accelRef = Vector3.zero;
        }

        void Update()
        {
            if (Frozen || Config == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
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
                case MoveState.Swimming:
                case MoveState.Diving: UpdateWater(dt, input, moveWorld); break;
                case MoveState.Flying: UpdateFly(dt, input, moveWorld); break;
            }

            UpdateStamina(dt);
            UpdateBreath(dt);
            UpdateFeel(dt);
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
                _dashDir = moveWorld.sqrMagnitude > 0.01f ? moveWorld.normalized : Forward;
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
                FaceTowards(_dashDir, Config.TurnSmoothTime * 0.5f, dt);
                _accelRef = Vector3.zero; // hand over to the run smoothly when the dash ends
            }
            else
            {
                bool moving = moveWorld.sqrMagnitude > 0.01f;
                IsSprinting = input.SprintHeld && moving && grounded && !Exhausted && Stamina > 0f;
                float speed = (IsSprinting ? Config.SprintSpeed : Config.RunSpeed) * LandingSpeedFactor;
                Vector3 target = Vector3.zero;
                if (moving)
                {
                    FaceTowards(moveWorld, Config.TurnSmoothTime, dt);
                    target = RunDirection(moveWorld, horizontal.magnitude) * (speed * Mathf.Clamp01(moveWorld.magnitude));
                }
                if (grounded)
                {
                    float response = !moving ? Config.StopTime
                        : Vector3.Dot(horizontal, target) < 0f ? Config.ReverseTime : Config.AccelTime;
                    float maxAccel = Mathf.Max(Config.GroundAcceleration, Config.GroundDeceleration) * 1.5f;
                    horizontal = Vector3.SmoothDamp(horizontal, target, ref _accelRef, response, maxAccel, dt);
                }
                else
                {
                    // Keep momentum after a dash or sprint in the air; only steer.
                    Vector3 airTarget = moving ? target : horizontal;
                    if (moving && horizontal.magnitude > target.magnitude) airTarget = target.normalized * horizontal.magnitude;
                    horizontal = Vector3.MoveTowards(horizontal, airTarget, Config.AirAcceleration * dt);
                    _accelRef = Vector3.zero;
                }
                if (IsSprinting) UseStamina(Config.SprintDrain * dt);
            }

            // Steep ground: slide down, can't walk up.
            if (steep)
            {
                Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                horizontal += new Vector3(downSlope.x, 0f, downSlope.z) * Config.SlideSpeed * dt * 4f;
                Vector3 into = new Vector3(-_groundNormal.x, 0f, -_groundNormal.z).normalized;
                float push = Vector3.Dot(horizontal, into);
                if (push > 0f) horizontal -= into * push;
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
            else if (grounded && !steep)
            {
                vy = SlopeFollowY(horizontal); // run along the slope instead of stepping off it
            }
            else
            {
                vy = Mathf.Max(vy - Config.Gravity * dt, -Config.MaxFallSpeed);
            }

            _velocity = new Vector3(horizontal.x, vy, horizontal.z);
            float fallSpeed = -vy;
            float yBefore = transform.position.y;
            Vector3 move = _velocity * dt;
            if (grounded && !jumped && !steep) move.y -= 0.02f; // keep contact so isGrounded stays true
            CollisionFlags flags = _cc.Move(move);
            bool nowGrounded = (flags & CollisionFlags.Below) != 0 || (_cc.isGrounded && vy <= 0f);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;

            // Lost contact on a slope or a small drop: snap back down instead of a micro-fall.
            if (!nowGrounded && grounded && !jumped && TrySnapToGround()) nowGrounded = true;
            if (grounded && nowGrounded && !jumped) AbsorbStep(transform.position.y - yBefore - move.y);

            if (nowGrounded && !jumped)
            {
                if (State == MoveState.Airborne) OnLanded(fallSpeed);
                SetState(MoveState.Grounded);
            }
            else if (State == MoveState.Grounded && !nowGrounded) SetState(MoveState.Airborne);

            if (TryEnterSwim()) return;
            if (moveWorld.sqrMagnitude > 0.01f && _dashTimeLeft <= 0f && TryStartClimb(moveWorld, dt)) return;
        }

        /// <summary>
        /// The direction to run in: the input direction from a standstill, the body's (smoothed)
        /// facing once moving, so turns and reversals arc around at speed instead of cutting
        /// through a stop. Blended by speed, so there's no switch.
        /// </summary>
        Vector3 RunDirection(Vector3 moveWorld, float speed)
        {
            float inputYaw = Mathf.Atan2(moveWorld.x, moveWorld.z) * Mathf.Rad2Deg;
            float follow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 3.5f, speed));
            float yaw = inputYaw + Mathf.DeltaAngle(inputYaw, _yaw) * follow;
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        // --- Debug fly and benchmark -----------------------------------------------------

        void UpdateFly(float dt, PlayerInputState input, Vector3 moveWorld)
        {
            float speed = input.SprintHeld ? 120f : 35f;
            float vertical = (input.JumpHeld ? 1f : 0f) - (input.DescendHeld ? 1f : 0f);
            _velocity = (moveWorld + Vector3.up * vertical) * speed;
            if (moveWorld.sqrMagnitude > 0.01f) FaceTowards(moveWorld, Config.TurnSmoothTime, dt);
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
            FaceTowards(dir, Config.TurnSmoothTime, dt);
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
            bool draining = State == MoveState.Climbing || IsSprinting || IsDashing || OutOfBreath;
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
