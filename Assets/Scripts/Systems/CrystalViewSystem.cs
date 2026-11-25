using System.Collections.Generic;
using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Crystal views: acquires the Crystal prefab view from the shared pool
    /// for every new CrystalComponent entity - regular crystals from the
    /// regular pool, golden ones (GoldenCrystalTag) from the golden pool.
    /// The prefab is the source of truth for the pose (rotation) and size
    /// (scale); this system only seats the view vertically, using the seat
    /// offset measured ONCE at init from that variant's prefab's rendered
    /// bounds, so that at the bob's lowest point the view's lowest point
    /// touches its seat point (the block surface for regular crystals, the
    /// floating height for golden ones). Spins it around the vertical axis
    /// and bobs it. When the host block actually starts sinking (fall delay
    /// expired), an uncollected crystal starts falling too - slower than the
    /// block (crystalFallSpeedFactor), keeping its spin, without bob - and
    /// survives the host block's death, dying on its own y-threshold.
    /// Crystals whose host vanishes without a falling phase (and
    /// non-falling crystals on session reset, done by GameFlowSystem) are
    /// released to their own variant's pool. Spin/bob/fall freeze while
    /// Paused. Destroying the entity is done by consumers (CrystalSystem,
    /// session reset); host-gone cleanup runs here.
    /// </summary>
    public class CrystalViewSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _newCrystals;
        private EcsFilter _crystals;
        private EcsFilter _sessions;
        private EcsPool<CrystalComponent> _crystalPool;
        private EcsPool<GoldenCrystalTag> _goldenPool;
        private EcsPool<ViewLinkComponent> _viewPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<ComboComponent> _comboPool;
        private EcsPool<ComboChangedEventComponent> _comboChangedPool;
        private EcsPool<FallingEventComponent> _fallingPool;
        private EcsPool<BlockComponent> _blockPool;

        /// <summary>Vertical offset (view lowest point - pivot) from prefab bounds, measured once.</summary>
        private float _seatOffset;

        /// <summary>Same offset for the golden variant's prefab, measured once.</summary>
        private float _goldenSeatOffset;

        /// <summary>Reusable dead list: cleared per frame, never reallocated.</summary>
        private readonly List<int> _dead = new List<int>(16);

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _newCrystals = _world.Filter<CrystalComponent>().Exc<ViewLinkComponent>().End();
            _crystals = _world.Filter<CrystalComponent>().End();
            _sessions = _world.Filter<GameStateComponent>().End();
            _crystalPool = _world.GetPool<CrystalComponent>();
            _goldenPool = _world.GetPool<GoldenCrystalTag>();
            _viewPool = _world.GetPool<ViewLinkComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _comboPool = _world.GetPool<ComboComponent>();
            _comboChangedPool = _world.GetPool<ComboChangedEventComponent>();
            _fallingPool = _world.GetPool<FallingEventComponent>();
            _blockPool = _world.GetPool<BlockComponent>();

            _seatOffset = MeasureSeatOffset(_shared.PresentationConfig.crystalPrefab);
            _goldenSeatOffset = MeasureSeatOffset(_shared.PresentationConfig.goldenCrystalPrefab);
        }

        /// <summary>
        /// Measures the vertical offset between the view's lowest rendered
        /// point and its pivot from the prefab's actual bounds. Init-time
        /// instantiate + destroy is allowed (not a play-time allocation).
        /// </summary>
        private static float MeasureSeatOffset(GameObject prefab)
        {
            if (prefab == null)
                return 0f;
            var probe = Object.Instantiate(prefab, Vector3.zero, prefab.transform.rotation);
            var renderers = probe.GetComponentsInChildren<Renderer>();
            float lowest = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r.bounds.min.y < lowest)
                    lowest = r.bounds.min.y;
            }
            float offset = lowest > float.MaxValue - 1f
                ? 0f // no renderers: keep the pivot where it is
                : lowest - probe.transform.position.y;
            Object.Destroy(probe);
            return offset;
        }

        public void Run(IEcsSystems systems)
        {
            var cfg = _shared.PresentationConfig;

            // Paused: spin/bob/fall freeze. Host-gone cleanup still runs so
            // a crystal whose block died before the pause cannot linger.
            bool paused = _sessions.GetSingletonComponent(_statePool).State == GameState.Paused;

            // Spawn views for new crystals: the prefab carries rotation and
            // scale; this system only seats the view vertically using the
            // offset measured once at init. The variant's tag picks the
            // pool (regular vs golden) and the seat offset.
            foreach (int entity in _newCrystals)
            {
                ref var crystal = ref _crystalPool.Get(entity);
                bool golden = _goldenPool.Has(entity);
                var go = golden
                    ? _shared.GoldenCrystalPool.Get()
                    : _shared.CrystalPool.Get();
                go.transform.SetParent(_shared.ViewRoot, false);
                var sourcePrefab = golden ? cfg.goldenCrystalPrefab : cfg.crystalPrefab;
                go.transform.rotation = sourcePrefab != null
                    ? sourcePrefab.transform.rotation
                    : Quaternion.identity;

                ref var view = ref _viewPool.Add(entity);
                view.View = go;
                view.Transform = go.transform;

                var seatOffset = golden ? _goldenSeatOffset : _seatOffset;
                crystal.SeatedY = crystal.Pos.y - seatOffset + cfg.crystalBobAmplitude;
                go.transform.position = new Vector3(crystal.Pos.x, crystal.SeatedY, crystal.Pos.z);
            }

            // Spin/bob; drop into falling with the host block; clean up orphans.
            float t = Time.time;
            _dead.Clear();

            foreach (int entity in _crystals)
            {
                ref var crystal = ref _crystalPool.Get(entity);

                // Host sinking (fall delay expired) -> the crystal goes with
                // it, slower than the block. A falling crystal keeps falling
                // even after its host block entity dies (its own timer).
                // An uncollected crystal starting to fall breaks the streak:
                // reset the counter, and if the broken streak was 2+ (and we
                // are still Playing - session reset never emits), show the
                // break label. A streak of 1 resets silently.
                if (!crystal.Falling && crystal.Block.Unpack(_world, out int blockEntity)
                                     && _blockPool.Has(blockEntity)
                                     && _fallingPool.Has(blockEntity))
                {
                    ref var falling = ref _fallingPool.Get(blockEntity);
                    if (falling.Timer >= falling.Delay)
                    {
                        crystal.Falling = true;
                        crystal.FallTimer = 0f;
                        BreakCombo();
                    }
                }

                // Host gone entirely without a falling phase (instant destroy
                // paths other than the session reset) -> delete the crystal.
                bool hostGone = !crystal.Block.Unpack(_world, out blockEntity)
                                || !_blockPool.Has(blockEntity);
                if (hostGone && !crystal.Falling)
                {
                    _dead.Add(entity);
                    continue;
                }

                if (paused || !_viewPool.Has(entity))
                    continue;
                ref var view = ref _viewPool.Get(entity);
                if (view.Transform == null)
                    continue;

                // Spin around the world-up axis through the pivot (the
                // prefab's pose is pivot-centered): rotates in place.
                view.Transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

                if (crystal.Falling)
                {
                    // Accelerating fall, slower than the block: the block's
                    // gravity curve scaled by crystalFallSpeedFactor. No bob.
                    crystal.FallTimer += Time.deltaTime;
                    float speed = Mathf.Min(
                        crystal.FallTimer * cfg.blockFallGravity * cfg.crystalFallSpeedFactor,
                        cfg.blockFallMaxSpeed * cfg.crystalFallSpeedFactor);
                    var p = view.Transform.position;
                    p.y -= speed * Time.deltaTime;
                    view.Transform.position = p;

                    if (p.y <= cfg.blockFallYThreshold)
                        _dead.Add(entity);
                    continue;
                }

                // Bob slightly around the seated base; at the bob's lowest
                // point the view's lowest point stays at the block surface.
                var bp = view.Transform.position;
                bp.y = crystal.SeatedY + Mathf.Sin(t * 2.5f) * cfg.crystalBobAmplitude;
                view.Transform.position = bp;
            }

            foreach (int entity in _dead)
            {
                if (_viewPool.Has(entity) && _viewPool.Get(entity).View != null)
                    ReleaseView(entity, _viewPool.Get(entity).View);
                if (_viewPool.Has(entity))
                    _viewPool.Del(entity);
                _world.DelEntity(entity);
            }
        }

        /// <summary>
        /// Breaks the combo streak: resets the session counter to 0 and,
        /// when the broken streak was 2 or more and the session is Playing
        /// (session reset deletes crystals in bulk and must never emit),
        /// raises a one-frame break event for the UI label. A streak of 1
        /// resets silently. Runs once per missed crystal (guarded by the
        /// Falling transition above), so several missed crystals in one
        /// frame emit the break only for the first one.
        /// </summary>
        private void BreakCombo()
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
            {
                _comboPool.Get(_sessions.GetSingletonEntity()).Value = 0;
                return;
            }
            ref var session = ref _sessions.GetSingletonComponent(_comboPool);
            int broken = session.Value;
            session.Value = 0;
            if (broken >= 2)
                _comboChangedPool.Add(_world.NewEntity()).Break = true;
        }

        /// <summary>
        /// Releases a crystal view to its own variant's pool by tag: golden
        /// views go to the golden pool, regular ones to the regular pool.
        /// Every release path must go through this (a view released into the
        /// wrong pool would corrupt that pool's identity).
        /// </summary>
        private void ReleaseView(int entity, GameObject view)
        {
            if (_goldenPool.Has(entity))
                _shared.GoldenCrystalPool.Release(view);
            else
                _shared.CrystalPool.Release(view);
        }
    }
}
