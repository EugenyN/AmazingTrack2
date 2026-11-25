using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Ball movement: integrates the logical position along WorldDir at
    /// speed base + level * perLevel (skipped while the ball carries
    /// BallWaitingTag - the standing menu preview). When the ball leaves
    /// the current ground block (ahead or
    /// to the right), that block is marked to fall. When the ground block
    /// changes, the previously grounded block (still alive, not falling and
    /// outside the ball's footprint) is marked to fall too - the same frame.
    /// In both cases the whole tail behind the block in track order (all
    /// blocks with a smaller Order, including blocks the ball never drove
    /// over, e.g. left behind after a turn) starts falling with it: same
    /// delay, timer 0. Jumping balls are excluded: the takeoff block is
    /// marked falling only after landing (leave-ground logic), so a jump
    /// landing back on the same block does not put it under a falling
    /// block.
    /// </summary>
    public class BallMoveSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _balls;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<LevelComponent> _levelPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<FallingEventComponent> _fallingPool;
        private EcsFilter _sessions;
        private EcsFilter _blocks;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallWaitingTag>().Exc<BallJumpingTag>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _blockPool = _world.GetPool<BlockComponent>();
            _levelPool = _world.GetPool<LevelComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _fallingPool = _world.GetPool<FallingEventComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
            _blocks = _world.Filter<BlockComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return;

            int level = _sessions.GetSingletonComponent(_levelPool).Value;

            var cfg = _shared.GameplayConfig;
            float speed = cfg.baseSpeed + level * cfg.speedPerLevel;
            // Clamp dt: editor pauses/frame spikes must not teleport the ball.
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            foreach (int entity in _balls)
            {
                ref var ball = ref _ballPool.Get(entity);

                // Remember the previous frame's ground: when the ground
                // changes, the block the ball actually left must be marked
                // falling in the same frame (guards against "ghost" blocks
                // behind the ball, e.g. stolen-ground frames).
                EcsPackedEntity prevGround = ball.PrevGround;

                ball.Pos += ball.WorldDir * (speed * dt);

                // Mark the ground block as passed when the ball leaves it:
                // ahead along forward, or to the right beyond its width.
                if (ball.Ground.Unpack(_world, out int groundEntity)
                    && _blockPool.Has(groundEntity)
                    && !_fallingPool.Has(groundEntity))
                {
                    ref var ground = ref _blockPool.Get(groundEntity);

                    float along = Vector3.Dot(ball.Pos - ground.Pos, ground.Forward);

                    // The ground slab spans Parts unit cells along -Right
                    // from the head cell; the ball must stay within them.
                    float halfSpan = ground.Parts * 0.5f;
                    float lateral = Vector3.Dot(ball.Pos - ground.Pos, ground.Right);

                    if (along > cfg.blockLength * 0.5f || lateral > halfSpan || lateral < -halfSpan)
                    {
                        MarkFalling(groundEntity);
                    }
                }

                // Ground changed: the previously grounded block, if it is
                // alive, not yet falling and the ball is now outside its
                // footprint (minus the hole cell), was left behind - mark it
                // falling too.
                if (prevGround.Unpack(_world, out int prevGroundEntity)
                    && (!ball.Ground.Unpack(_world, out int currentGroundEntity)
                        || currentGroundEntity != prevGroundEntity)
                    && _blockPool.Has(prevGroundEntity)
                    && !_fallingPool.Has(prevGroundEntity))
                {
                    ref var prevBlock = ref _blockPool.Get(prevGroundEntity);
                    if (!BlockFootprintHelper.Contains(ref prevBlock, ball.Pos, cfg.blockLength))
                        MarkFalling(prevGroundEntity);
                }

                ball.PrevGround = ball.Ground;

                void MarkFalling(int blockEntity)
                {
                    // Block collapse animation timing lives in the
                    // presentation config (the ball has already left).
                    float delay = _shared.PresentationConfig.blockFallDelay;

                    ref var falling = ref _fallingPool.Add(blockEntity);
                    falling.Delay = delay;
                    falling.Timer = 0f;

                    // Tail collapse: every block generated before the falling
                    // block ("behind the ball" in track order) starts falling
                    // with it, even if the ball never drove over it (e.g. the
                    // straight continuation left behind after a turn). Blocks
                    // not yet falling only.
                    foreach (int blockTailEntity in _blocks)
                    {
                        if (_fallingPool.Has(blockTailEntity))
                            continue;
                        ref var block = ref _blockPool.Get(blockTailEntity);
                        if (block.Order >= _blockPool.Get(blockEntity).Order)
                            continue;
                        ref var tailFalling = ref _fallingPool.Add(blockTailEntity);
                        tailFalling.Delay = delay;
                        tailFalling.Timer = 0f;
                    }
                }
            }
        }
    }
}
