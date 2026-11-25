using System.Collections.Generic;
using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Updates the ball's ground block with a physics query: SphereCast down
    /// from the ball center along the Block layer; the hit collider's
    /// EntityLinkView maps back to the block entity. Candidates are filtered by
    /// a footprint test (the XZ projection of the ball center must lie
    /// within the block's footprint, minus the hole cell), and
    /// the previous ground is kept while it still contains the center
    /// (hysteresis): neighbors cannot steal the ground role. Jumping balls
    /// are excluded: the ground stays frozen at the takeoff block during
    /// the flight. Does not run while Paused.
    /// </summary>
    public class BallGroundSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _balls;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<FallingEventComponent> _fallingPool;
        private EcsFilter _sessions;

        // Large buffer: sinking blocks near the cast path also register hits;
        // a small NonAlloc buffer can overflow and lose the live block.
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        // Collider -> EntityLinkView cache: GetComponentInParent runs once
        // per pooled collider per session (cache miss), then a dictionary
        // lookup per hit. Pooled block views are never destroyed during
        // play - only parked by the pool - so entries stay valid until this
        // system dies; the link's entity binding is refreshed by
        // BlockViewSystem on every acquire and validated per hit below.
        private readonly Dictionary<Collider, Views.EntityLinkView> _linkByCollider
            = new Dictionary<Collider, Views.EntityLinkView>(64);

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallJumpingTag>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _blockPool = _world.GetPool<BlockComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _fallingPool = _world.GetPool<FallingEventComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            // Paused: the ground must not change while the session is frozen.
            ref var sessionComponent = ref _sessions.GetSingletonComponent(_statePool);
            if (sessionComponent.State == GameState.Paused)
                return;

            var cfg = _shared.GameplayConfig;

            foreach (int entity in _balls)
            {
                ref var ball = ref _ballPool.Get(entity);

                // Start the cast well above the block (a cast that starts
                // already penetrating returns no hit in Unity PhysX).
                float blockTop = cfg.blockHeight; // block center at y=0, half height up
                Vector3 origin = ball.Pos + Vector3.up * (blockTop + cfg.ballRadius + 0.1f);
                float castDistance = (blockTop + cfg.ballRadius + 0.1f) + 0.2f;
                int count = Physics.SphereCastNonAlloc(origin, cfg.ballRadius * 0.9f, Vector3.down,
                    _hits, castDistance, _shared.BlockMask, QueryTriggerInteraction.Ignore);

                int? groundEntity = null;
                float bestDistance = float.MaxValue;
                bool bestFalling = true;
                for (int i = 0; i < count; i++)
                {
                    var collider = _hits[i].collider;
                    if (!_linkByCollider.TryGetValue(collider, out var link))
                    {
                        // Cache miss: once per pooled collider (init-time
                        // cost, not a per-frame search). Null results are
                        // not cached: a foreign collider (no link) must stay
                        // resolvable in case it gains a link later.
                        link = collider.GetComponentInParent<Views.EntityLinkView>();
                        if (link != null)
                            _linkByCollider.Add(collider, link);
                    }
                    if (link == null || link.World != _world || !link.IsAlive())
                        continue;
                    // The view's collider may outlive its entity for a frame
                    // (destroyed at session reset): IsAlive catches that.
                    if (!_blockPool.Has(link.Entity))
                        continue;

                    // Footprint filter: the candidate counts as ground only
                    // when the XZ projection of the ball's center lies within
                    // the block's footprint (with a small seam margin), minus
                    // the hole cell. A wide SphereCast sweep
                    // (wider than the contact) must not let a diagonal
                    // neighbor steal the ground while the ball's center is
                    // still over the current block, and must not let a hole
                    // block stand in as ground while the center is over the
                    // hole cell.
                    ref var block = ref _blockPool.Get(link.Entity);
                    if (!BlockFootprintHelper.Contains(ref block, ball.Pos, cfg.blockLength))
                        continue;

                    // Prefer non-falling blocks: at a seam both neighbors hit
                    // at the same distance; the passed (falling) one must not
                    // hold the ground role, or the ball overshoots the seam.
                    bool falling = _fallingPool.Has(link.Entity);
                    bool better = groundEntity == null
                                  || (bestFalling && !falling)
                                  || (bestFalling == falling && _hits[i].distance < bestDistance);
                    if (!better)
                        continue;
                    bestDistance = _hits[i].distance;
                    bestFalling = falling;
                    groundEntity = link.Entity;
                }

                // Hysteresis: keep the previous ground while it is alive, not
                // falling and its footprint (minus the hole cell) still
                // contains the ball's center. Neighbors must not grab the
                // ground role in the frame the cast grazes them, and the
                // block must not hold the ground role once the center is
                // over its hole cell.
                if (ball.Ground.Unpack(_world, out int prevGroundEntity)
                    && _blockPool.Has(prevGroundEntity)
                    && !_fallingPool.Has(prevGroundEntity))
                {
                    ref var prevBlock = ref _blockPool.Get(prevGroundEntity);
                    if (BlockFootprintHelper.Contains(ref prevBlock, ball.Pos, cfg.blockLength))
                        groundEntity = prevGroundEntity;
                }

                ball.Ground = groundEntity.HasValue
                    ? _world.PackEntity(groundEntity.Value)
                    : default; // no ground: FallCheckSystem triggers the fall
            }
        }
    }
}
