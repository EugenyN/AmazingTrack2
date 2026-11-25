using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Off-track check: when the ball has no ground block under its center
    /// (BallGroundSystem found none), the ball starts falling. Jumping
    /// balls are excluded: takeoff over a hole must not trigger the fall.
    /// </summary>
    public class FallCheckSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private EcsFilter _balls;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _sessions;
        private SharedData _shared;
        private GameFlowSystem _gameFlow;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _gameFlow = _shared.GetSystem<GameFlowSystem>();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallJumpingTag>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return;

            foreach (int entity in _balls)
            {
                ref var ball = ref _ballPool.Get(entity);
                if (!ball.Ground.Unpack(_world, out _))
                    _gameFlow.BeginFall();
            }
        }
    }
}
