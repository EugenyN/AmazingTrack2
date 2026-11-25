using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Structured crystal distribution for the track generator: decides
    /// WHEN a crystal exists (start delay, chains, min/max gap with a pity
    /// force-spawn) and WHERE it sits (random non-hole part for a chain
    /// start, adjacent solid cell for a chain link). Owns the
    /// deferred-spawn queue: a decision on block N spawns its entity only
    /// when block N+2 is built - the golden variant needs the two
    /// following slabs' layouts to check that a jump through the crystal
    /// can complete. The variant roll (regular vs golden) happens at
    /// resolve time; at most one crystal per chain is golden (keyed by
    /// chain id, because resolves can run after the next chain started),
    /// and a chain link is only golden when the jump pair lies along the
    /// chain's travel axis, so the player can collect it without leaving
    /// the chain.
    /// No allocations after Init: the pending queue is a pre-allocated
    /// buffer, and the world queries below iterate live block entities.
    /// </summary>
    public class CrystalDistributor
    {
        /// <summary>A deferred crystal decision awaiting its resolve.</summary>
        private struct PendingCrystal
        {
            public int BlockEntity;
            public int BlockOrder;
            public Vector3 CrystalPos;
            public Vector3 Forward;
            public Vector3 Right;
            public int ChainId;
            // World-space step from the previous chain crystal onto this
            // link's cell. Zero for a chain's first link and for non-chain
            // crystals (no in-step).
            public Vector3 InDir;
            // World-space step from this link's cell to the next chain
            // link's cell, backfilled when the next link is deferred.
            // Zero while unknown (chain's last link or non-chain crystal).
            public Vector3 OutDir;
        }

        // Deferred crystals: decided to exist on block N, spawned only when
        // block N+2 is built - the golden variant needs the two following
        // slabs' layouts to check that a jump through the crystal can
        // complete (the landing cell C+Forward or C+Right may only get its
        // block from the second-next slab, e.g. a right turn after a
        // straight one). Decisions on consecutive blocks (crystal chains)
        // leave up to two entries pending at once, so the single slot of
        // the old roll was replaced by a small pre-allocated ring buffer
        // (no allocations after Init); each build resolves every entry
        // that is two blocks behind.
        private const int PendingCrystalCapacity = 4;
        private readonly PendingCrystal[] _pendingCrystals = new PendingCrystal[PendingCrystalCapacity];
        private int _pendingCrystalCount;

        // Structured distribution state: blocks left before the session's
        // crystal-free opening ends, chain links remaining (crystals on
        // consecutive eligible blocks), and eligible blocks passed since
        // the last crystal (pity counter).
        private int _crystalStartDelayLeft;
        private int _chainBlocksLeft;
        private int _dryEligibleBlocks;

        // XZ cell of the previous chain crystal: the next chain link steps
        // 1 unit from it (adjacent cell). Null outside a chain.
        private Vector3? _lastChainCrystalCell;

        // Monotonic chain id; -1 = the crystal is not a chain continuation.
        private int _nextChainId = -1;
        // Id of the chain the pending links still belong to.
        private int _currentChainId = -1;
        // Whether the chain with id _hasGoldenChainId already placed its
        // golden crystal (at most one per chain). Keyed by chain id, not a
        // bare flag: pending entries resolve two blocks after their host
        // block, so a resolve pass can run after the next chain started -
        // the flag must still answer for the OLD chain.
        private bool _currentChainHasGolden;
        private int _hasGoldenChainId = -1;

        private readonly System.Random _rollRng;
        private readonly System.Random _variantRng;

        /// <summary>
        /// Creates the distributor. <paramref name="rollRng"/> drives the
        /// distribution and cell decisions (the same stream the generator
        /// uses for crystals, so crystal tuning never perturbs the track
        /// shape); <paramref name="variantRng"/> drives the regular-vs-
        /// golden roll at resolve time.
        /// </summary>
        public CrystalDistributor(System.Random rollRng, System.Random variantRng)
        {
            _rollRng = rollRng;
            _variantRng = variantRng;
        }

        /// <summary>
        /// Session reset: drops the deferred queue (their host blocks are
        /// gone) and restarts the distribution counters.
        /// </summary>
        public void Reset(GameplayConfig cfg)
        {
            _pendingCrystalCount = 0;
            _crystalStartDelayLeft = cfg.crystalStartDelayBlocks;
            _chainBlocksLeft = 0;
            _dryEligibleBlocks = 0;
            _lastChainCrystalCell = null;
            _nextChainId = -1;
            _currentChainId = -1;
            _currentChainHasGolden = false;
            _hasGoldenChainId = -1;
        }

        /// <summary>
        /// One build step: resolves every pending decision that is two
        /// blocks behind (spawning its entity), then advances the
        /// distribution machine on the freshly built block. The start
        /// delay is an absolute session-phase guard: it counts every built
        /// block, eligible or not. An ineligible block (fully missing
        /// 1-part block) touches no counters at all.
        /// </summary>
        public void OnBlockBuilt(
            int blockEntity, ref BlockComponent block, int nextOrder,
            EcsWorld world, SharedData shared)
        {
            ResolvePending(nextOrder, world, shared);

            var cfg = shared.GameplayConfig;
            if (_crystalStartDelayLeft > 0)
            {
                _crystalStartDelayLeft--;
            }
            else if (!(block.HolePart >= 0 && block.Parts <= 1))
            {
                AdvanceDistribution(blockEntity, ref block, cfg, world);
            }
        }

        /// <summary>
        /// One structured-distribution decision per eligible block (the
        /// start delay is handled by the caller). Branch order: chain link
        /// (crystal on this block; links were granted at chain start) →
        /// pity force-spawn after the max dry gap → chain-start roll after
        /// the min gap → dry block. A chain start pre-grants its full
        /// rolled length (crystalChainMinBlocks..crystalChainMaxBlocks),
        /// so gap bookkeeping only resumes when the chain runs out.
        /// Ineligible blocks never reach this method and neither consume
        /// links nor advance gaps.
        /// </summary>
        private void AdvanceDistribution(
            int blockEntity, ref BlockComponent block, GameplayConfig cfg,
            EcsWorld world)
        {
            if (_chainBlocksLeft > 0)
            {
                _chainBlocksLeft--;
                DeferCrystal(blockEntity, ref block, cfg, world);
                _dryEligibleBlocks = 0;
                if (_chainBlocksLeft == 0)
                {
                    _lastChainCrystalCell = null; // chain complete
                    _currentChainId = -1;
                }
                return;
            }

            if (_dryEligibleBlocks >= cfg.crystalMaxGapBlocks)
            {
                StartCrystalChain(blockEntity, ref block, cfg, world);
                return;
            }

            if (_dryEligibleBlocks >= cfg.crystalMinGapBlocks
                && _rollRng.NextDouble() < cfg.crystalChance)
            {
                StartCrystalChain(blockEntity, ref block, cfg, world);
                return;
            }

            _dryEligibleBlocks++;
        }

        /// <summary>
        /// Starts a crystal chain on the given block and pre-grants its
        /// remaining links: the length is rolled with 1/length weights over
        /// the min..max range (shorter chains more frequent), and one
        /// crystal is placed right now. Pity force-spawns use the same
        /// path - a forced chain behaves exactly like a rolled one.
        /// </summary>
        private void StartCrystalChain(
            int blockEntity, ref BlockComponent block, GameplayConfig cfg,
            EcsWorld world)
        {
            // Length distribution: shorter chains must be more frequent.
            // Weights 1/length over the min..max range: length 2..5 gives
            // weights 0.5/0.33/0.25/0.2 (sum 1.28) -> 39%/26%/20%/16%.
            int range = cfg.crystalChainMaxBlocks - cfg.crystalChainMinBlocks + 1;
            double roll = _rollRng.NextDouble() * WeightSum(range);
            int length = cfg.crystalChainMinBlocks;
            double acc = 0.0;
            for (int candidate = cfg.crystalChainMinBlocks;
                 candidate <= cfg.crystalChainMaxBlocks;
                 candidate++)
            {
                acc += 1.0 / candidate;
                if (roll < acc)
                {
                    length = candidate;
                    break;
                }
            }

            _chainBlocksLeft = length - 1;
            _lastChainCrystalCell = null; // first link rolls a fresh part
            _currentChainId = ++_nextChainId;
            _currentChainHasGolden = false;
            _hasGoldenChainId = -1;

            DeferCrystal(blockEntity, ref block, cfg, world);
            _dryEligibleBlocks = 0;
        }

        /// <summary>Sum of 1/length weights over the first `range` lengths starting at 1.</summary>
        private static double WeightSum(int range)
        {
            double sum = 0.0;
            for (int i = 1; i <= range; i++)
                sum += 1.0 / i;
            return sum;
        }

        /// <summary>
        /// Rolls whether a crystal exists on the block and decides its cell;
        /// the entity is spawned two blocks later (see OnBlockBuilt).
        /// A chain's first crystal sits on a random non-hole part of its host
        /// block; each next chain crystal sits on a solid cell adjacent
        /// (1 unit) to the previous chain crystal, one step along the host
        /// block's Forward (into the next block) or Right (the next part of
        /// the same slab). Only the roll happens here; the variant (regular
        /// vs golden) is decided at resolve time.
        /// </summary>
        private void DeferCrystal(
            int blockEntity, ref BlockComponent block, GameplayConfig cfg,
            EcsWorld world)
        {
            Vector3 crystalPos;
            Vector3 inDir = Vector3.zero;
            int chainId = _chainBlocksLeft > 0 || _lastChainCrystalCell.HasValue
                ? _currentChainId
                : -1;
            if (_lastChainCrystalCell.HasValue)
            {
                // Chain continuation: step exactly 1 unit from the previous
                // chain crystal onto a solid adjacent cell. The step is only
                // +Forward (next block) or +Right (next part of the same
                // slab) - never -Right (left) - so the chain always runs
                // along the ball's legal travel directions and the player
                // can drive the whole chain. Candidates are re-rolled while
                // they land on a hole part, an empty cell, or a cell taken
                // by an earlier chain link.
                var cell = RollChainCell(ref block, world);
                if (!cell.HasValue)
                {
                    // No legal adjacent cell: this link is skipped and the
                    // chain ends here - forget the last cell so nothing
                    // steps from a cell that never got its crystal.
                    _lastChainCrystalCell = null;
                    return;
                }

                // World-space step onto this link's cell; the previous
                // link's pending entry gets the same vector as its out-step
                // (see the backfill below), so resolve can tell a straight
                // link from a chain turn.
                inDir = cell.Value - _lastChainCrystalCell.Value;
                inDir.y = 0f;

                crystalPos = new Vector3(
                    cell.Value.x, cfg.blockHeight * 0.5f, cell.Value.z);
            }
            else
            {
                // Uniform roll among non-hole parts: raw index over
                // Parts+1 outcomes where a hole collapse re-rolls instead
                // of biasing. A fully-hole block never reaches here (the
                // roll is skipped).
                int part;
                do
                {
                    part = (int)(_rollRng.NextDouble() * block.Parts);
                    if (block.HolePart >= 0 && part == block.HolePart)
                        part = -1; // collapsed onto the hole: re-roll
                } while (part < 0);

                // Part center: head cell minus partIndex steps along Right;
                // the crystal anchor is the point on the block surface where
                // the diamond's bottom vertex rests.
                var partPos = block.Pos + block.Right * ((block.Parts - 1) * 0.5f - part);
                crystalPos = partPos + Vector3.up * (cfg.blockHeight * 0.5f);
            }

            // Remember this chain's last cell so the next link (possibly on
            // the next built block) steps from it; cleared by the
            // distribution machine when the chain ends.
            _lastChainCrystalCell = new Vector3(
                crystalPos.x, 0f, crystalPos.z);

            // Capacity 4 is never exceeded: a resolve pass runs every build
            // before the next decision, and entries resolve at a two-block
            // age, so at most two entries (ages 0 and 1) are ever queued.
            _pendingCrystals[_pendingCrystalCount++] = new PendingCrystal
            {
                BlockEntity = blockEntity,
                BlockOrder = block.Order,
                CrystalPos = crystalPos,
                Forward = block.Forward,
                Right = block.Right,
                ChainId = chainId,
                InDir = inDir,
                OutDir = Vector3.zero,
            };

            if (inDir != Vector3.zero)
            {
                // Backfill the previous chain link's out-step with this
                // step vector. That entry is still queued: it was deferred
                // one build ago and resolves only at a two-block age, so a
                // fixed scan over the few live entries is enough (no
                // allocations).
                for (int i = 0; i < _pendingCrystalCount - 1; i++)
                {
                    if (_pendingCrystals[i].ChainId == chainId
                        && _pendingCrystals[i].OutDir == Vector3.zero)
                    {
                        _pendingCrystals[i].OutDir = inDir;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Picks a solid cell for a chain continuation crystal: re-rolls one
        /// of the two legal neighbors of the previous chain crystal - one
        /// step along the host block's Forward or Right, never -Right
        /// (left) - until the candidate is a free solid cell: not a hole
        /// part, not an empty cell, not a cell already taken by a queued
        /// pending crystal of this chain. With only two candidates a
        /// dead-end corner (both invalid) is possible; the chain then ends
        /// on the previous crystal and this link is skipped - no crystal
        /// ever floats over a hole, and no chain ever bends left.
        /// </summary>
        private Vector3? RollChainCell(ref BlockComponent block, EcsWorld world)
        {
            var prev = _lastChainCrystalCell.Value;
            int guard = 8;
            while (guard-- > 0)
            {
                Vector3 candidate = _rollRng.Next(2) == 0
                    ? prev + block.Forward
                    : prev + block.Right;
                candidate.y = 0f;

                if (TrackQueriesHelper.HasSolidCell(world, candidate) && !IsPendingAt(candidate))
                    return candidate;
            }

            // Both neighbors invalid (hole and/or void): this link has no
            // legal adjacent cell - skip it.
            return null;
        }

        /// <summary>
        /// True when a queued pending crystal (this chain's earlier links,
        /// not yet resolved) already occupies the given XZ cell: keeps two
        /// chain links off one cell.
        /// </summary>
        private bool IsPendingAt(Vector3 cell)
        {
            for (int i = 0; i < _pendingCrystalCount; i++)
            {
                var pos = _pendingCrystals[i].CrystalPos;
                if (Mathf.Abs(pos.x - cell.x) < 0.5f
                    && Mathf.Abs(pos.z - cell.z) < 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Spawns the deferred crystal entities once the two following slabs
        /// are built: the golden variant may only float over a cell from
        /// which a jump can be completed through the crystal. A jump is a
        /// straight 2-unit flight along +Forward or +Right (the track never
        /// goes left); the arc is within pickup reach of the crystal only
        /// 0.5..1.5 units past the takeoff point, so the ball must take off
        /// from the cell directly behind the crystal along the jump axis
        /// and land on the cell directly ahead of it. Falling to regular
        /// needs no check.
        /// The jump axis is constrained by how the player reaches the
        /// crystal: a lone crystal accepts a pair along either the host
        /// block's Forward or Right axis, while a chain link accepts a
        /// pair only along the chain's travel axis (the link's in-step;
        /// the out-step for a chain's first link) - a perpendicular pair
        /// would force the player off the chain to collect the golden
        /// crystal, and they could not return to the next link, breaking
        /// the combo guaranteed. A chain link with no in- or out-step at
        /// all is never golden.
        /// Resolves in queue order: with crystal chains, decisions on
        /// consecutive blocks age together, so every entry reaching a
        /// two-block age spawns in the same pass.
        /// Each spawned crystal's Block link points at the block that owns
        /// the crystal's CELL (re-resolved at spawn time from the cell), not
        /// the block the decision was made on: a chain step may move the
        /// crystal onto a neighboring slab, and the ground pickup path binds
        /// on the block under the ball.
        /// At most one crystal per chain is golden: a chain rolls the
        /// golden variant for its first eligible link; once one link of a
        /// chain spawned golden, all later links of the same chain spawn
        /// regular. A link at a chain turn (in-step and out-step along
        /// different world axes, an "L" corner) is never golden: it spawns
        /// regular without the golden roll, so the golden crystal never
        /// sits in the corner of a bent chain.
        /// </summary>
        private void ResolvePending(int nextOrder, EcsWorld world, SharedData shared)
        {
            if (_pendingCrystalCount == 0)
                return;

            var cfg = shared.GameplayConfig;
            int write = 0;
            for (int read = 0; read < _pendingCrystalCount; read++)
            {
                ref var pending = ref _pendingCrystals[read];
                if (nextOrder - pending.BlockOrder < 2)
                {
                    // Still waiting for its two following slabs: keep it,
                    // compacting the buffer in place (queue order holds).
                    if (write != read)
                        _pendingCrystals[write] = _pendingCrystals[read];
                    write++;
                    continue;
                }

                // Owner of the crystal's cell: the chain step may have moved
                // the crystal onto a neighboring slab, so re-resolve the
                // binding from the cell itself; fall back to the deciding
                // block (still better than a dangling link).
                int ownerEntity = TrackQueriesHelper.BlockEntityAt(world, pending.CrystalPos);
                if (ownerEntity < 0)
                    ownerEntity = pending.BlockEntity;

                bool golden;
                if (shared.PresentationConfig.goldenCrystalPrefab == null)
                {
                    golden = false;
                }
                else if (pending.ChainId >= 0)
                {
                    // A chain link at a chain turn (in-step and out-step
                    // along different world axes) never rolls golden: the
                    // golden crystal must not sit in the corner of an "L"
                    // bend, with the previous chain crystal on one side and
                    // the next one on the perpendicular side. Straight links
                    // (same axis in and out) and chain ends (one of the
                    // steps zero, the other not) pass this guard and are
                    // judged by the chain-axis pair check below.
                    bool atChainTurn = pending.InDir != Vector3.zero
                                       && pending.OutDir != Vector3.zero
                                       && pending.InDir != pending.OutDir;
                    // The jump pair is required along the chain's travel
                    // axis only: the in-step for a link with a previous
                    // neighbor, the out-step for a chain's first link. A
                    // perpendicular pair would force the player off the
                    // chain to collect the golden crystal, and they could
                    // not return to the next link - a guaranteed combo
                    // break. A link with no chain axis at all (both steps
                    // zero) is not golden-eligible.
                    Vector3 chainAxis = pending.InDir != Vector3.zero
                        ? pending.InDir
                        : pending.OutDir;
                    // A chain spawns at most one golden crystal: skip the
                    // roll once one of its links already went golden. Keyed
                    // by the entry's own chain id: the resolve of a chain's
                    // last links can run after the next chain started.
                    golden = !atChainTurn
                             && !(_currentChainHasGolden && _hasGoldenChainId == pending.ChainId)
                             && chainAxis != Vector3.zero
                             && pairAlongAxis(ref pending, world, chainAxis)
                             && _variantRng.NextDouble() < cfg.goldenCrystalChance;
                    if (golden)
                    {
                        _currentChainHasGolden = true;
                        _hasGoldenChainId = pending.ChainId;
                    }
                }
                else
                {
                    golden = (forwardPairFor(ref pending, world) || rightPairFor(ref pending, world))
                             && _variantRng.NextDouble() < cfg.goldenCrystalChance;
                }

                SpawnCrystalEntity(ownerEntity, pending.CrystalPos, golden, cfg, world);
            }

            _pendingCrystalCount = write;
        }

        /// <summary>Golden jump-pair check along the pending's Forward axis.</summary>
        private bool forwardPairFor(ref PendingCrystal pending, EcsWorld world)
        {
            var cell = new Vector3(pending.CrystalPos.x, 0f, pending.CrystalPos.z);
            return TrackQueriesHelper.HasSolidCell(world, cell - pending.Forward)
                   && TrackQueriesHelper.HasSolidCell(world, cell + pending.Forward);
        }

        /// <summary>Golden jump-pair check along the pending's Right axis.</summary>
        private bool rightPairFor(ref PendingCrystal pending, EcsWorld world)
        {
            var cell = new Vector3(pending.CrystalPos.x, 0f, pending.CrystalPos.z);
            return TrackQueriesHelper.HasSolidCell(world, cell - pending.Right)
                   && TrackQueriesHelper.HasSolidCell(world, cell + pending.Right);
        }

        /// <summary>
        /// Golden jump-pair check along an arbitrary world-space axis:
        /// a solid takeoff cell behind the crystal plus a solid landing
        /// cell ahead of it. Used for a chain link, whose axis is the
        /// chain's travel direction (in-step, or out-step for a chain's
        /// first link) - a perpendicular pair would force the player off
        /// the chain and guarantee a combo break.
        /// </summary>
        private bool pairAlongAxis(ref PendingCrystal pending, EcsWorld world, Vector3 axis)
        {
            var cell = new Vector3(pending.CrystalPos.x, 0f, pending.CrystalPos.z);
            return TrackQueriesHelper.HasSolidCell(world, cell - axis)
                   && TrackQueriesHelper.HasSolidCell(world, cell + axis);
        }

        /// <summary>Creates the crystal entity with the decided variant.</summary>
        private void SpawnCrystalEntity(
            int blockEntity, Vector3 pos, bool golden, GameplayConfig cfg,
            EcsWorld world)
        {
            int crystalEntity = world.NewEntity();
            ref var crystal = ref world.GetPool<CrystalComponent>().Add(crystalEntity);
            crystal.Block = world.PackEntity(blockEntity);
            crystal.Pos = pos;
            if (golden)
            {
                world.GetPool<GoldenCrystalTag>().Add(crystalEntity);
                crystal.Pos.y += cfg.goldenCrystalHeight;
            }
        }
    }
}
