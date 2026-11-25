using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Spawns the ball entity when a track exists and no ball does:
    /// in Menu (standing preview ball on the first block, BallWaitingTag)
    /// and in Playing (ball starts rolling immediately, no waiting tag).
    /// Position at the first block, direction = block forward.
    /// </summary>
    public class BallSpawnSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private EcsFilter _balls;
        private EcsFilter _blocks;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<BallWaitingTag> _waitingPool;
        private EcsFilter _sessions;

        /// <summary>
        /// One-shot set by GameFlowSystem when Play was pressed without a
        /// live menu ball: the next spawn in Playing starts rolling at once
        /// (no BallWaitingTag), keeping the "Play rolls immediately"
        /// contract even in that rare frame race.
        /// </summary>
        public bool SpawnWithoutWaiting;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _balls = _world.Filter<BallComponent>().End();
            _blocks = _world.Filter<BlockComponent>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _blockPool = _world.GetPool<BlockComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _waitingPool = _world.GetPool<BallWaitingTag>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            var state = _sessions.GetSingletonComponent(_statePool).State;
            if (state != GameState.Menu && state != GameState.Playing)
                return;

            if (_balls.GetEntitiesCount() > 0)
                return;

            // Find the first block (minimal Order).
            int firstEntity = -1;
            int firstOrder = int.MaxValue;
            foreach (int blockEntity in _blocks)
            {
                ref var block = ref _blockPool.Get(blockEntity);
                if (block.Order < firstOrder)
                {
                    firstOrder = block.Order;
                    firstEntity = blockEntity;
                }
            }
            if (firstEntity < 0)
                return;

            ref var first = ref _blockPool.Get(firstEntity);
            int ballEntity = _world.NewEntity();
            ref var ball = ref _ballPool.Add(ballEntity);
            ball.Pos = first.Pos;
            ball.WorldDir = Vector3.forward; // start-segment axis (+Z)
            ball.Ground = _world.PackEntity(firstEntity);
            // Menu preview ball stands until the session starts; a Playing
            // spawn (session start, restart) rolls immediately.
            if (state == GameState.Menu && !SpawnWithoutWaiting)
                _waitingPool.Add(ballEntity);
            SpawnWithoutWaiting = false;
        }
    }
}
