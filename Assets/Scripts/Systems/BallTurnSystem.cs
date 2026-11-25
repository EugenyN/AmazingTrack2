using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Tap handling in Playing: every tap toggles the direction between the
    /// fixed world axes +Z and +X, including the very first tap of a session
    /// (the ball already rolls when the session starts - Play removes the
    /// waiting state). Emits a sound event. The ground block is not
    /// consulted. Jumping balls are excluded: a tap in flight does not
    /// change direction.
    /// </summary>
    public class BallTurnSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private EcsFilter _taps;
        private EcsFilter _balls;
        private EcsPool<TapEventComponent> _tapPool;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<SoundEventComponent> _soundPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _sessions;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _taps = _world.Filter<TapEventComponent>().End();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallJumpingTag>().End();
            _tapPool = _world.GetPool<TapEventComponent>();
            _ballPool = _world.GetPool<BallComponent>();
            _soundPool = _world.GetPool<SoundEventComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return;

            bool tapped = false;
            foreach (int tapEntity in _taps)
            {
                _tapPool.Del(tapEntity);
                tapped = true;
            }
            if (!tapped)
                return;

            foreach (int ballEntity in _balls)
            {
                // Toggle between the fixed world axes +Z and +X.
                ref var ball = ref _ballPool.Get(ballEntity);
                ball.WorldDir = ball.WorldDir == Vector3.right
                    ? Vector3.forward
                    : Vector3.right;
                _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.Turn;
            }
        }
    }
}
