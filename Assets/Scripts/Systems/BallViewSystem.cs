using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Spawns the ball view when a BallComponent entity appears without a
    /// view, keeps the view at the logical position with rolling, adds the
    /// jump arc height while BallJumpComponent is present (height 0 at
    /// takeoff and landing, peak at mid-flight), and hands the ball over to
    /// physics when BallFallingTag is set: a moving ball keeps its track
    /// speed as horizontal velocity (arc fall), a waiting ball drops
    /// vertically.
    /// </summary>
    public class BallViewSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _newBalls;
        private EcsFilter _balls;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<ViewLinkComponent> _viewPool;
        private EcsPool<BallViewComponent> _ballViewPool;
        private EcsPool<BallFallingTag> _fallingPool;
        private EcsPool<BallJumpComponent> _jumpPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _sessions;

        private Vector3 _lastViewPos;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();

            _newBalls = _world.Filter<BallComponent>().Exc<ViewLinkComponent>().End();
            _balls = _world.Filter<BallComponent>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _viewPool = _world.GetPool<ViewLinkComponent>();
            _ballViewPool = _world.GetPool<BallViewComponent>();
            _fallingPool = _world.GetPool<BallFallingTag>();
            _jumpPool = _world.GetPool<BallJumpComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            var gp = _shared.GameplayConfig;
            var pres = _shared.PresentationConfig;

            // Paused: freeze presentation too (no view ticking, no physics
            // hand-over, no sink-based destruction) unless the ball is
            // already physics-driven.
            bool paused = _sessions.GetSingletonComponent(_statePool).State == GameState.Paused;

            // Spawn a view for a new ball (allowed while paused: a fresh
            // session resets the state before the first ball spawn).
            foreach (int entity in _newBalls)
            {
                var go = Object.Instantiate(_shared.BallPrefab, Vector3.zero, Quaternion.identity,
                    _shared.ViewRoot);

                // Rigidbody fetched once at spawn: kinematic setup here, the
                // per-frame path reads it from BallViewComponent - never via
                // GetComponent.
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    // Kinematic poses are written from Update: interpolation
                    // would only replay stale physics-time poses (a one-frame
                    // backward snap at the fall hand-over).
                    rb.interpolation = RigidbodyInterpolation.None;
                }

                ref var view = ref _viewPool.Add(entity);
                view.View = go;
                view.Transform = go.transform;

                ref var ballView = ref _ballViewPool.Add(entity);
                ballView.Body = rb;

                _lastViewPos = go.transform.position;
            }

            if (paused)
                return;

            foreach (int entity in _balls)
            {
                ref var ball = ref _ballPool.Get(entity);
                if (!_viewPool.Has(entity))
                    continue;
                ref var view = ref _viewPool.Get(entity);

                bool falling = _fallingPool.Has(entity);

                var rb = _ballViewPool.Get(entity).Body;
                if (falling)
                {
                    if (rb != null && rb.isKinematic)
                    {
                        bool waiting = _world.GetPool<BallWaitingTag>().Has(entity);

                        // Hand-over velocity. A waiting ball (BallWaitingTag,
                        // WorldDir unused) drops straight down with no
                        // velocity; a moving ball keeps a fixed horizontal
                        // fall speed (config, not track speed: the forward
                        // flight length is identical on every level). A
                        // landing-frame jump fall (landing over a void)
                        // instead inherits the arc's momentum: BallJump-
                        // Component lingers exactly one frame (tag already
                        // removed, timer frozen at Duration); horizontal =
                        // the unchanged plane speed, vertical = the arc's
                        // descent speed at landing (the derivative of
                        // h*sin(pi*t/T): h*pi/T*cos(pi*t/T), negative =
                        // downward at t = 1), so the jump arc continues
                        // into the fall with no kink.
                        var velocity = Vector3.zero;
                        bool extrapolate = true;
                        float verticalExtra = 0f;

                        if (!waiting)
                        {
                            float horizontal = pres.ballFallHorizontalSpeed;

                            if (_jumpPool.Has(entity))
                            {
                                ref var jump = ref _jumpPool.Get(entity);
                                horizontal = gp.jumpDistance / jump.Duration;
                                verticalExtra = gp.jumpHeight * Mathf.PI / jump.Duration
                                                * Mathf.Cos(Mathf.PI * Mathf.Clamp01(jump.Timer / jump.Duration));
                                _jumpPool.Del(entity);
                                extrapolate = false;
                            }

                            velocity = ball.WorldDir * horizontal + Vector3.up * verticalExtra;
                        }

                        // Hand-over frame: the view advances one frame along
                        // the fall velocity and physics starts from the same
                        // visible point (no dead frame, no rollback). The
                        // jump-landing case skips the step: the logical
                        // position moved this frame already (exact landing),
                        // so an extra velocity*dt would double the drop.
                        var handOver = ball.Pos
                                       + Vector3.up * (gp.blockHeight * 0.5f + gp.ballRadius)
                                       + (extrapolate ? velocity * Time.deltaTime : Vector3.zero);
                        view.Transform.position = handOver;
                        rb.position = handOver;

                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.linearVelocity = velocity;

                        var tumble = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 2f;
                        rb.AddTorque(tumble, ForceMode.Impulse);
                    }

                    // Destroy the view after it sinks below the threshold.
                    if (view.Transform.position.y < pres.blockFallYThreshold)
                    {
                        Object.Destroy(view.View);
                        _viewPool.Del(entity);
                        _world.DelEntity(entity);
                    }

                    // Extra gravity while physics-driven: a scale above 1
                    // steepens the fall arc (shorter forward glide) without
                    // touching the hand-over velocity. Mass-independent
                    // acceleration, measured against the actual project 
                    // gravity. No allocations.
                    if (rb != null && !rb.isKinematic && pres.ballFallGravityScale != 1f)
                        rb.AddForce(Vector3.down * (-Physics.gravity.y * (pres.ballFallGravityScale - 1f)),
                            ForceMode.Acceleration);

                    continue; // physics drives the ball now
                }

                // Kinematic: place at the logical position + radius above
                // ground. While jumping, the arc height (0 at takeoff and
                // landing, peak at mid-flight) is added on top - same
                // GetHeight formula the golden-crystal pickup check uses,
                // so the pickup geometry matches the rendered arc. A
                // landing-frame jump component (tag gone, timer at
                // Duration) renders zero height this frame and is then
                // removed: no snap.
                float jumpHeight = 0f;
                if (_jumpPool.Has(entity))
                {
                    ref var jump = ref _jumpPool.Get(entity);
                    jumpHeight = jump.GetHeight(gp.jumpHeight);

                    // Landing frame: the arc is over; drop the component so
                    // the base height applies from the next frame (the fall
                    // hand-over above has already consumed the momentum).
                    float t = Mathf.Clamp01(jump.Timer / jump.Duration);
                    if (t >= 1f)
                        _jumpPool.Del(entity);
                }

                var target = ball.Pos + Vector3.up * (gp.blockHeight * 0.5f + gp.ballRadius + jumpHeight);
                view.Transform.position = target;

                // Rolling: rotate around the axis perpendicular to movement.
                var delta = view.Transform.position - _lastViewPos;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist > 1e-5f)
                {
                    var axis = Vector3.Cross(Vector3.up, delta.normalized);
                    view.Transform.Rotate(axis, dist / Mathf.Max(0.01f, gp.ballRadius) * Mathf.Rad2Deg,
                        Space.World);
                }
                _lastViewPos = view.Transform.position;
            }
        }
    }
}
