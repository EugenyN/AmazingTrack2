using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// +1 point when the ball's ground block changes to a new block (the
    /// spawn block of a fresh session scores too). Runs only in Playing
    /// (no scoring in Menu or while Paused). Jumping balls are excluded:
    /// blocks flown over (not touched) do not score. The last-scored
    /// order resets on every session (re)start: the track restarts from
    /// Order 0.
    /// </summary>
    public class ScoreSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private EcsFilter _balls;
        private EcsFilter _sessions;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsPool<ScoreChangedEventComponent> _scoreChangedPool;
        private EcsPool<GameStateComponent> _statePool;
        private int _lastScoredOrder = -1;

        /// <summary>Session state of the previous frame: detects session (re)starts.</summary>
        private GameState _lastSeenState = (GameState)(-1);

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallJumpingTag>().End();
            _sessions = _world.Filter<GameStateComponent>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _blockPool = _world.GetPool<BlockComponent>();
            _scorePool = _world.GetPool<ScoreComponent>();
            _scoreChangedPool = _world.GetPool<ScoreChangedEventComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            var state = _sessions.GetSingletonComponent(_statePool).State;

            // Session (re)start (Play from the menu, Play Again from the
            // results screen): the track restarts from Order 0, so the
            // last-scored order must reset with it - a stale order would
            // award one phantom point on the spawn block and skip scoring
            // the block with that very order mid-run. Resuming from Paused
            // must not reset: the ball is still on the same block.
            if (state == GameState.Playing
                && (_lastSeenState == GameState.Menu
                    || _lastSeenState == GameState.GameOver))
                _lastScoredOrder = -1;
            _lastSeenState = state;

            if (state != GameState.Playing)
                return;

            foreach (int ballEntity in _balls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                if (!ball.Ground.Unpack(_world, out int groundEntity)
                    || !_blockPool.Has(groundEntity))
                    continue;

                int order = _blockPool.Get(groundEntity).Order;
                if (order == _lastScoredOrder)
                    continue; // same block: no re-score

                _lastScoredOrder = order;
                _sessions.GetSingletonComponent(_scorePool).Value++;
                _scoreChangedPool.Add(_world.NewEntity());
            }
        }
    }
}
