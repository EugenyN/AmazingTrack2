using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack.Tests.PlayMode
{
    /// <summary>
    /// Autopilot bot for the end-to-end gameplay test: plays the session
    /// like a human would - one decision per frame from the track and
    /// crystal state ahead of the ball, all input through the game's
    /// official one-frame events (TapEventComponent / JumpEventComponent),
    /// no direct system or component manipulation. Direction changes are
    /// rate-limited to a human-plausible interval.
    /// </summary>
    public class GameplayBot
    {
        // Decision windows (world units ahead of the ball).
        private const float JumpLookAheadMin = 0.9f;
        private const float GoldenCrystalLookAhead = 1.1f;

        /// <summary>
        /// Minimum interval between bot taps, seconds: a human cannot toggle
        /// the direction every frame, so neither can the bot. MUST stay
        /// below one cell of travel (1.0 / speed): with the boundary-aligned
        /// turn gate, a longer interval skips every turn window on short
        /// corridors (at 0.2 s and 5+ u/s the dead time exceeds the 1.0-unit
        /// boundary spacing and 1-2-cell corridors become impassable).
        /// </summary>
        private const float MinTurnInterval = 0.2f;

        /// <summary>Per-decision trace for diagnosing a failed run (off by default).</summary>
        private const bool _debugTrace = false;

        /// <summary>
        /// Distance the turn trigger probes ahead, units: must stay at 2 so
        /// the tap fires a full cell before the edge (see Update).
        /// </summary>
        private const float TurnLookAhead = 2.0f;

        /// <summary>
        /// Cell-boundary window for turns, units: a tap is only issued when
        /// the movement-axis coordinate is within this distance of an integer
        /// (a cell boundary). A mid-cell turn freezes the lateral coordinate
        /// at an arbitrary offset and the ball then drifts along the seam of
        /// two cell columns - fatal in Hard mode where 1x1 slabs forgive no
        /// lateral drift. Turning at cell boundaries keeps the ball on the
        /// corridor's centerline in every mode.
        /// </summary>
        private const float TurnCellBoundary = 0.35f;

        private readonly EcsWorld _world;
        private readonly EcsPool<BallComponent> _ballPool;
        private readonly EcsPool<BlockComponent> _blockPool;
        private readonly EcsPool<CrystalComponent> _crystalPool;
        private readonly EcsPool<GoldenCrystalTag> _goldenPool;
        private readonly EcsPool<BallWaitingTag> _waitingPool;
        private readonly EcsFilter _balls;
        private readonly EcsFilter _blocks;
        private readonly EcsFilter _crystals;

        private float _lastTapTime = float.NegativeInfinity;

        /// <summary>Resolves the bot's pools and filters from the live world.</summary>
        public GameplayBot(EcsWorld world)
        {
            _world = world;
            _ballPool = world.GetPool<BallComponent>();
            _blockPool = world.GetPool<BlockComponent>();
            _crystalPool = world.GetPool<CrystalComponent>();
            _goldenPool = world.GetPool<GoldenCrystalTag>();
            _waitingPool = world.GetPool<BallWaitingTag>();
            _balls = world.Filter<BallComponent>().End();
            _blocks = world.Filter<BlockComponent>().End();
            _crystals = world.Filter<CrystalComponent>().End();
        }

        /// <summary>The single ball entity, or -1 when absent.</summary>
        public int GetSingleBall()
        {
            foreach (int entity in _balls)
                return entity;
            return -1;
        }

        /// <summary>The ball's position, or zero when the ball is absent.</summary>
        public Vector3 GetBallPos()
        {
            int ballEntity = GetSingleBall();
            return ballEntity >= 0 ? _ballPool.Get(ballEntity).Pos : Vector3.zero;
        }

        /// <summary>Whether the ball is in the menu's standing (waiting) state.</summary>
        public bool IsWaitingForTap()
        {
            int ballEntity = GetSingleBall();
            return ballEntity >= 0 && _waitingPool.Has(ballEntity);
        }

        /// <summary>
        /// Emits a tap event through the official input channel and records
        /// its time for the turn rate limit.
        /// </summary>
        public void Tap()
        {
            _lastTapTime = Time.time;
            _world.GetPool<TapEventComponent>().Add(_world.NewEntity());
        }

        /// <summary>
        /// One bot decision per frame from the track and crystal state ahead
        /// of the ball: jump over a hole/edge or for a golden crystal, turn
        /// when the current axis is empty. All input goes through the
        /// official one-frame events.
        /// </summary>
        public void Update()
        {
            int ballEntity = GetSingleBall();
            if (ballEntity < 0)
                return;

            ref var ball = ref _ballPool.Get(ballEntity);
            if (_waitingPool.Has(ballEntity))
                return; // menu preview ball: no bot decisions in Menu
            if (_world.GetPool<BallJumpingTag>().Has(ballEntity))
                return; // in flight: input is suppressed anyway

                        Vector3 dir = ball.WorldDir;
            Vector3 pos = ball.Pos;
            Vector3 sideDir = dir == Vector3.right ? Vector3.forward : Vector3.right;

            // The side corridor is enterable when its first cell is solid,
            // or its first cell is a JUMPABLE hole: solid at 1.0, hole at
            // 1.0 with solid at 2.0 (the turn is followed by an immediate
            // jump). Without the hole case the bot gets trapped on a
            // corridor whose only continuation is a hole sideways (the
            // track bends right past a gap): no solid side cell to tap
            // into, no forward landing to jump to.
            bool sideEnterable = HasSolidCell(pos + sideDir * 1.0f)
                                 || (HasSolidCell(pos + sideDir * 2f)
                                     && !HasSolidCell(pos + sideDir * 1.0f));

            // Jump gate: the landing point is exactly jumpDistance (2 units)
            // ahead along the CURRENT position (BallJumpSystem covers exactly
            // jumpDistance from the takeoff, the duration adapts to speed),
            // the landing cell must be solid, AND it must have an exit: a
            // solid cell straight ahead of it or beside it (where the next
            // tap would turn). Without the exit check the bot jumps onto the
            // last cell of a dead-end corridor (e.g. chasing a golden
            // crystal from a late takeoff) and falls off it 0.5 units later.
            // The exit probes start at the LANDING cell, not at the ball:
            // side cells of the takeoff point are irrelevant to surviving
            // the landing.
            Vector3 landing = pos + dir * 2f;
            bool landingSolid = HasSolidCell(landing);
            bool landingHasExit = HasSolidCell(landing + dir)
                                  || HasSolidCell(landing + sideDir);

            // Jump over a hole or track edge: obstacle in the window ahead
            // and a safe landing.
            if (HasObstacleAhead(pos, dir) && landingSolid && landingHasExit)
            {
                Trace("JUMP", pos, dir);
                _world.GetPool<JumpEventComponent>().Add(_world.NewEntity());
                return;
            }

            // Jump for a golden crystal roughly one unit ahead (the arc's
            // pickup window is 0.5..1.5 units from takeoff).
            if (GoldenCrystalAhead(pos, dir) && landingSolid && landingHasExit)
            {
                Trace("GOLD", pos, dir);
                _world.GetPool<JumpEventComponent>().Add(_world.NewEntity());
                return;
            }

            // Boundary gate for BOTH turn branches, before any of them: a
            // turn is only issued when the movement-axis coordinate is
            // within TurnCellBoundary of an integer (a cell boundary). A
            // mid-cell turn freezes the lateral coordinate at an arbitrary
            // offset and the ball then rides the seam of two columns -
            // survivable on 2-wide Normal slabs, fatal on Hard 1x1 slabs
            // (drift 0.5+ leaves the slab band). Detection windows (0.9/2.0
            // units) are far wider than the worst-case wait to a boundary,
            // so no obstacle is missed while waiting.
            if (Time.time - _lastTapTime >= MinTurnInterval)
            {
                float alongMovement = Mathf.Abs(Vector3.Dot(pos, dir));
                float toBoundary = Mathf.Abs(alongMovement - Mathf.Round(alongMovement));
                bool atBoundary = toBoundary <= TurnCellBoundary;

                // Emergency turn: an obstacle whose landing is a dead end
                // (solid but no exit) must not be jumped, and the regular
                // turn trigger is not armed while the landing cell is
                // solid. Turn into an enterable side corridor now instead
                // of rolling into the trap.
                if (atBoundary
                    && HasObstacleAhead(pos, dir) && landingSolid && !landingHasExit
                    && sideEnterable)
                {
                    Trace("TAP", pos, dir);
                    Tap();
                    return;
                }

                // Regular turn: the corridor ahead ends within jump reach
                // (the cell two units ahead is empty: a straight jump would
                // land in the void) and the side corridor is enterable. The
                // trigger MUST stay at 2.0 units, not 1.0: it has to fire
                // while the ball is still one full cell before the edge,
                // where the side corridor is solid - at the edge itself the
                // side cell is often the next slab's hole and the ball
                // would be stuck (no jump landing, no legal tap).
                if (atBoundary
                    && !HasSolidCell(pos + dir * TurnLookAhead)
                    && sideEnterable)
                {
                    // A tap toggles between the fixed axes +Z / +X.
                    Trace("TAP", pos, dir);
                    Tap();
                }
            }
        }

        /// <summary>
        /// ASCII map of the cells around a position for diagnosing a failed
        /// run: a 0.5-step grid centered on the position, 'o' = solid ground,
        /// '.' = void. The half-step resolves both track lattices (Hard
        /// slabs sit on integer coordinates, Normal centerlines on
        /// half-integer ones), so the map is readable in every mode.
        /// </summary>
        public string DumpBlocksNear(Vector3 pos)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Track cells around ({pos.x:0.00}, {pos.z:0.00}), 0.5-step grid:");
            for (float z = Mathf.Floor(pos.z * 2f) / 2f + 2.5f; z >= pos.z - 2.5f; z -= 0.5f)
            {
                var row = new System.Text.StringBuilder();
                for (float x = Mathf.Floor(pos.x * 2f) / 2f - 2.5f; x <= pos.x + 2.5f; x += 0.5f)
                    row.Append(HasSolidCell(new Vector3(x, 0f, z)) ? 'o' : '.');
                sb.AppendLine($"  z={z,5:0.#} {row}");
            }
            return sb.ToString();
        }

        /// <summary>Per-decision trace line (compiled out when off).</summary>
        private static void Trace(string kind, Vector3 pos, Vector3 dir)
        {
            if (_debugTrace)
            {
                // _debugTrace is a compile-time constant; the body below is
                // deliberately unreachable (and stripped by the JIT) when
                // tracing is disabled - hence the suppressed CS0162.
#pragma warning disable CS0162 // Unreachable code detected
                Debug.Log($"[GameplayBotE2E] {kind} t={Time.time:F2} pos={pos:0.00} dir={dir}");
#pragma warning restore CS0162
            }
        }

        /// <summary>
        /// True when a hole or the track edge lies in the jump decision
        /// window ahead of the ball along its direction: the near cell is
        /// empty, or the mid cell is empty (a hole whose takeoff must start
        /// right away). Cells are probed from the ball's continuous position
        /// rounded AWAY from the movement axis: at 5+ units/sec the ball can
        /// be deep inside the last cell when the edge arrives, and a
        /// rounded-back snap would probe the cell the ball already left.
        /// </summary>
        private bool HasObstacleAhead(Vector3 pos, Vector3 dir)
        {
            bool nearEmpty = !HasSolidCell(pos + dir * JumpLookAheadMin);
            bool midSolid = HasSolidCell(pos + dir * 1.0f);
            return nearEmpty || !midSolid;
        }

        /// <summary>
        /// True when a golden crystal lies roughly one unit ahead of the
        /// ball along its direction (XZ distance within the window, any
        /// height: the jump arc covers the floating height).
        /// </summary>
        private bool GoldenCrystalAhead(Vector3 pos, Vector3 dir)
        {
            foreach (int crystalEntity in _crystals)
            {
                if (!_goldenPool.Has(crystalEntity))
                    continue;
                Vector3 crystalPos = _crystalPool.Get(crystalEntity).Pos;
                Vector3 toCrystal = crystalPos - pos;
                float along = Vector3.Dot(toCrystal, dir);
                float lateral = Mathf.Abs(Vector3.Dot(toCrystal, new Vector3(dir.z, 0f, dir.x)));
                if (along > 0f && along <= GoldenCrystalLookAhead && lateral <= 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Whether the point would be grounded if the ball stood there.
        /// Delegates to the runtime ground test (BlockFootprintHelper.
        /// Contains) over live blocks, so the bot never diverges from the
        /// game's own footprint semantics. Probing the part index instead
        /// diverges from the runtime at cell edges (0.47-0.53 off center),
        /// which blinded the bot in Hard mode where 1x1 slabs leave no
        /// slack - hence the grounded-point test, not a cell lookup.
        /// </summary>
        private bool HasSolidCell(Vector3 point)
        {
            foreach (int blockEntity in _blocks)
            {
                ref var block = ref _blockPool.Get(blockEntity);
                if (BlockFootprintHelper.Contains(ref block, point, 1f))
                    return true;
            }
            return false;
        }
    }
}
