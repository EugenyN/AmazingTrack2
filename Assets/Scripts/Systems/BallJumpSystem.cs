using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Ball jump: consumes JumpEventComponent (right mouse button / the
    /// on-screen Jump button) and starts a fixed-distance kinematic flight
    /// along the current direction; integrates the flight and removes the
    /// jumping tag on landing.
    /// Start conditions: state Playing, ball present, not falling, not the
    /// standing menu preview (BallWaitingTag), not already jumping. The flight keeps the
    /// ball's current track speed in the horizontal plane (the speed does
    /// not change during the jump); the flight duration adapts so the
    /// covered distance is always jumpDistance (2 units): duration =
    /// jumpDistance / track speed (higher speed = faster jump, same
    /// distance). The direction is frozen (BallTurnSystem is suppressed):
    /// WorldDir at takeoff IS the takeoff direction. Landing (Timer &ge;
    /// Duration) removes BallJumpingTag; BallGroundSystem, running later in
    /// the same frame, re-acquires ground by the ordinary cast (no ground:
    /// FallCheckSystem starts the regular fall). Runs only in Playing
    /// (events are validated here at consumption time).
    /// </summary>
    public class BallJumpSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _jumps;
        private EcsFilter _balls;
        private EcsFilter _sessions;
        private EcsFilter _jumpingBalls;
        private EcsPool<JumpEventComponent> _jumpEventPool;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BallJumpingTag> _jumpingPool;
        private EcsPool<BallJumpComponent> _jumpPool;
        private EcsPool<BallFallingTag> _fallingPool;
        private EcsPool<BallWaitingTag> _waitingPool;
        private EcsPool<LevelComponent> _levelPool;
        private EcsPool<SoundEventComponent> _soundPool;
        private EcsPool<GameStateComponent> _statePool;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _jumps = _world.Filter<JumpEventComponent>().End();
            _balls = _world.Filter<BallComponent>().End();
            _sessions = _world.Filter<GameStateComponent>().End();
            _jumpingBalls = _world.Filter<BallComponent>().Inc<BallJumpingTag>().End();
            _jumpEventPool = _world.GetPool<JumpEventComponent>();
            _ballPool = _world.GetPool<BallComponent>();
            _jumpingPool = _world.GetPool<BallJumpingTag>();
            _jumpPool = _world.GetPool<BallJumpComponent>();
            _fallingPool = _world.GetPool<BallFallingTag>();
            _waitingPool = _world.GetPool<BallWaitingTag>();
            _levelPool = _world.GetPool<LevelComponent>();
            _soundPool = _world.GetPool<SoundEventComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
            {
                // Events are dropped outside Playing (Menu/Falling/GameOver):
                // consumers validate at consumption time, same as taps.
                foreach (int eventEntity in _jumps)
                    _jumpEventPool.Del(eventEntity);
                return;
            }

            var cfg = _shared.GameplayConfig;

            // Jump requests: start a flight when the ball is controllable.
            // Duration = distance / current track speed: the plane speed is
            // unchanged by the jump, the covered distance is fixed.
            foreach (int eventEntity in _jumps)
            {
                _jumpEventPool.Del(eventEntity);

                foreach (int ballEntity in _balls)
                {
                    if (_fallingPool.Has(ballEntity)
                        || _waitingPool.Has(ballEntity)
                        || _jumpingPool.Has(ballEntity))
                        continue;

                    float trackSpeed = cfg.baseSpeed
                                       + _sessions.GetSingletonComponent(_levelPool).Value
                                       * cfg.speedPerLevel;

                    _jumpingPool.Add(ballEntity);
                    ref var jump = ref _jumpPool.Add(ballEntity);
                    jump.Timer = 0f;
                    jump.Duration = cfg.jumpDistance / trackSpeed;
                    _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.Jump;
                }
            }

            // Flight integration: advance the timer, move the logical
            // position at the unchanged track speed along the frozen
            // direction, land when the timer is up. dt is clamped like in
            // BallMoveSystem: frame spikes must not teleport the ball.
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            foreach (int ballEntity in _jumpingBalls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                ref var jump = ref _jumpPool.Get(ballEntity);

                // Horizontal flight speed: the takeoff track speed (the
                // plane speed is preserved during the jump), derived from
                // the component: distance / duration.
                float flightSpeed = cfg.jumpDistance / jump.Duration;

                jump.Timer += dt;
                if (jump.Timer >= jump.Duration)
                {
                    // Land exactly at the configured distance: the final
                    // step covers only the remaining fraction (at high
                    // track speeds a full step is up to a third of a
                    // block - an undershoot would land the ball short).
                    // The tag is removed, so the ordinary pipeline (ground
                    // cast, fall check, input) resumes this frame. The
                    // component is kept at full timer for one frame: the
                    // view renders zero arc height from it, and a fall
                    // that starts this same frame (landing over a void)
                    // reads it to continue the arc's momentum instead of
                    // the roll-off fixed speed.
                    ball.Pos += ball.WorldDir * (flightSpeed * (jump.Duration - (jump.Timer - dt)));
                    jump.Timer = jump.Duration;
                    _jumpingPool.Del(ballEntity);
                    continue;
                }

                ball.Pos += ball.WorldDir * (flightSpeed * dt);
            }
        }
    }
}
