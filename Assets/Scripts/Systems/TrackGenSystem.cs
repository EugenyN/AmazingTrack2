using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Endless track generation by orientation (reference model): the cursor
    /// STEPS first into a fresh world cell along +X or +Z (random per block,
    /// after a forced +Z start segment), then a composite 1..3-part slab is
    /// laid from that head cell backwards along -Right. Stepping before
    /// laying keeps every slab in the fresh column/row the head just entered:
    /// monotone stepping excludes self-intersection and overlap by
    /// construction (with Parts &gt; 1, laying from the pre-step cursor would
    /// overlap the previous slab on every turn), so no overlap checks exist.
    /// Keeps a lookahead ahead. Crystal placement (distribution, chains,
    /// deferred spawn, variants) lives in <see cref="CrystalDistributor"/>,
    /// cell-occupancy queries in <see cref="TrackQueriesHelper"/>.
    /// </summary>
    public class TrackGenSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _blocks;
        private EcsFilter _balls;
        private EcsFilter _resets;
        private EcsFilter _sessions;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<TrackResetEventComponent> _resetPool;
        private EcsPool<CameraSnapEventComponent> _cameraSnapPool;

        private System.Random _rng;
        private CrystalDistributor _crystals;

        /// <summary>
        /// Creates the generator. When a seed is given, all random decisions
        /// (step axis, holes, crystals) are reproducible; without one the
        /// behavior is unchanged (non-deterministic). Crystal decisions use
        /// two dedicated streams derived from the same seed, so crystal
        /// tuning never perturbs the track shape (axis/holes) itself.
        /// </summary>
        public TrackGenSystem(int? seed = null)
        {
            if (seed.HasValue)
            {
                _rng = new System.Random(seed.Value);
                // Distinct derivations of the master seed: identical seeds
                // for both streams would correlate their first rolls.
                _crystals = new CrystalDistributor(
                    new System.Random(seed.Value ^ 0x5DEECE66),
                    new System.Random(seed.Value ^ 0x2545F491));
            }
            else
            {
                _rng = new System.Random();
                _crystals = new CrystalDistributor(
                    new System.Random(),
                    new System.Random());
            }
        }

        private int _nextOrder;
        private Vector3 _cursorPos;
        private int _straightLeft;   // forced +Z steps of the start segment
        private bool _lastBlockHadHole; // consecutive-hole rule: the block right after a hole block is solid
        private Vector3 _lastForward; // previous step axis: after a hole block in a 1-part mode the next block steps along the same axis (no turn behind a jumpable gap)

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();

            _blocks = _world.Filter<BlockComponent>().End();
            _balls = _world.Filter<BallComponent>().Exc<BallFallingTag>().End();
            _resets = _world.Filter<TrackResetEventComponent>().End();
            _blockPool = _world.GetPool<BlockComponent>();
            _ballPool = _world.GetPool<BallComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _resetPool = _world.GetPool<TrackResetEventComponent>();
            _cameraSnapPool = _world.GetPool<CameraSnapEventComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();

            ResetCursor();
        }

        /// <summary>
        /// Cursor restarts at the origin on session reset (fresh track).
        /// The crystal distributor resets with it (deferred crystals from
        /// the old track are dropped: their host blocks are gone, and the
        /// new track's layout has nothing to do with them). Also emits a
        /// camera snap event: the whole track is rebuilt around the origin,
        /// so the follow focus teleports to the new first block and the
        /// camera must not lerp across the scene (mode switch, session
        /// start, menu return).
        /// </summary>
        private void ResetCursor()
        {
            _cameraSnapPool.Add(_world.NewEntity());
            _nextOrder = 0;
            _cursorPos = Vector3.zero;
            _straightLeft = _shared.GameplayConfig.startSegmentMin
                            + _rng.Next(_shared.GameplayConfig.startSegmentMax - _shared.GameplayConfig.startSegmentMin);
            _lastBlockHadHole = false;
            _lastForward = Vector3.forward;
            _crystals.Reset(_shared.GameplayConfig);
        }

        public void Run(IEcsSystems systems)
        {
            // Session reset: restart the cursor from the origin.
            foreach (int resetEntity in _resets)
            {
                _resetPool.Del(resetEntity);
                ResetCursor();
            }

            var state = _sessions.GetSingletonComponent(_statePool);
            bool playing = state.State == GameState.Playing;
            bool paused = state.State == GameState.Paused;

            // Paused: do nothing at all (the menu demo-build branch below
            // must not run during a frozen session).
            if (paused)
                return;

            // Menu: keep a demo track of the lookahead size from the origin.
            if (!playing)
            {
                if (_blocks.GetEntitiesCount() < _shared.GameplayConfig.lookaheadBlocks)
                    BuildBatch();
                return;
            }

            // Playing: rebuild when ahead of the ball drops below the target.
            if (CountAhead() < _shared.GameplayConfig.lookaheadBlocks)
                BuildBatch();
        }

        private void BuildBatch()
        {
            int target = _shared.GameplayConfig.lookaheadBlocks + _shared.GameplayConfig.buildBatch;
            int guard = 16;
            while (guard-- > 0)
            {
                if (CountAhead() >= target)
                    break;
                BuildNextBlock();
            }
        }

        private int CountAhead()
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return _blocks.GetEntitiesCount();

            foreach (int ballEntity in _balls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                if (ball.Ground.Unpack(_world, out int groundEntity)
                    && _blockPool.Has(groundEntity))
                    return (_nextOrder - 1) - _blockPool.Get(groundEntity).Order;
            }
            return _nextOrder;
        }

        /// <summary>Steps the cursor into a fresh cell and lays one composite slab from it.</summary>
        private void BuildNextBlock()
        {
            var cfg = _shared.GameplayConfig;
            var mode = _sessions.GetSingletonComponent(_statePool).Mode;
            int parts = mode.PartsCount();

            // Step axis: forced +Z during the start segment; coin flip after,
            // EXCEPT right after a hole block in a 1-part mode (Hard): there
            // the next block must step along the same axis, otherwise the
            // straight jump over the gap would land in the void.
            Vector3 forward;
            if (_straightLeft > 0)
            {
                forward = Vector3.forward;
                _straightLeft--;
            }
            else if (_lastBlockHadHole && parts == 1)
            {
                forward = _lastForward;
            }
            else
            {
                forward = _rng.Next(2) == 0 ? Vector3.forward : Vector3.right;
            }
            _lastForward = forward;

            // Step FIRST (reference semantics): the cursor enters a fresh
            // cell, then the slab is laid from that head cell backwards along
            // -Right. Right pairs the step axis with the perpendicular: for a
            // +Z step the slab extends -X, for a +X step it extends -Z (the
            // reference layout). Every part lies in the fresh column/row the
            // head just entered - overlap-free by construction. (Laying from
            // the pre-step cursor, or Cross(up, forward) for Right, would
            // overlap the previous slab on every turn when Parts > 1.)
            _cursorPos += forward;

            Vector3 right = new Vector3(forward.z, 0f, forward.x);
            Vector3 center = _cursorPos - right * ((parts - 1) * 0.5f);

            int entity = _world.NewEntity();
            ref var block = ref _blockPool.Add(entity);
            block.Pos = center;
            block.Forward = forward;
            block.Right = right;
            block.Parts = parts;
            block.Order = _nextOrder;

            // Holes (any mode, after the start segment): one missing END part
            // (0 or Parts-1), never the middle; in a 1-part mode (Hard) the
            // whole block is missing. Chance is per-mode (HoleChance).
            // Never on two consecutive blocks: the block right after a hole
            // block is always solid, so every hole is jumpable.
            int holePart = -1;
            if (_straightLeft == 0 && !_lastBlockHadHole
                && _rng.NextDouble() < mode.HoleChance(cfg))
            {
                holePart = _rng.Next(2) == 0 ? 0 : parts - 1;
            }
            block.HolePart = holePart;
            _lastBlockHadHole = holePart >= 0;

            // Crystal distribution for the fresh block: resolves the older
            // deferred decisions (golden needs the two following slabs'
            // layouts), then advances the structured distribution machine.
            _crystals.OnBlockBuilt(entity, ref block, _nextOrder, _world, _shared);

            _nextOrder++;
        }
    }
}
