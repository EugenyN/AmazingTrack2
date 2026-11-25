using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Fall animation of blocks: entities with FallingEventComponent +
    /// ViewLinkComponent wait out the delay, then sink with gravity-driven
    /// acceleration (from zero speed up to blockFallMaxSpeed), and are
    /// destroyed (view + component, the entity dies with it) below threshold.
    /// Does not run while Paused (delay timers and sinking freeze).
    /// </summary>
    public class BlockFallViewSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _falling;
        private EcsPool<FallingEventComponent> _fallingPool;
        private EcsPool<ViewLinkComponent> _viewPool;
        private EcsPool<BlockViewComponent> _blockViewPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _sessions;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _falling = _world.Filter<FallingEventComponent>().Inc<ViewLinkComponent>().End();
            _fallingPool = _world.GetPool<FallingEventComponent>();
            _viewPool = _world.GetPool<ViewLinkComponent>();
            _blockViewPool = _world.GetPool<BlockViewComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            // Paused: delay timers and the sink animation freeze.
            if (_sessions.GetSingletonComponent(_statePool).State == GameState.Paused)
                return;

            var cfg = _shared.PresentationConfig;
            float dt = Time.deltaTime;

            foreach (int entity in _falling)
            {
                ref var falling = ref _fallingPool.Get(entity);
                falling.Timer += dt;
                if (falling.Timer < falling.Delay)
                    continue;

                ref var view = ref _viewPool.Get(entity);
                if (view.View == null)
                {
                    // View destroyed externally: drop the components, entity dies.
                    _fallingPool.Del(entity);
                    if (_viewPool.Has(entity))
                        _viewPool.Del(entity);
                    continue;
                }

                // Accelerating fall: after the delay the block starts from
                // zero speed and speeds up (gravity) up to the configured
                // ceiling. The timer already includes the delay, so the
                // velocity is derived from it directly - no extra state.
                float fallTime = falling.Timer - falling.Delay;
                float speed = Mathf.Min(fallTime * cfg.blockFallGravity, cfg.blockFallMaxSpeed);

                var p = view.Transform.position;
                p.y -= speed * dt;
                view.Transform.position = p;

                if (p.y <= cfg.blockFallYThreshold)
                {
                    // Release the view to the pool AND kill the entity:
                    // leaving BlockComponent alive would make
                    // BlockViewSystem respawn the view at the original spot
                    // next frame (zombie collider factory).
                    if (_blockViewPool.Has(entity))
                        _shared.BlockPool.Release(_blockViewPool.Get(entity).View);
                    _world.DelEntity(entity);
                }
            }
        }
    }
}
