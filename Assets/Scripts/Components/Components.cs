using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Components of the session singleton entity: state, mode, timers.
    /// </summary>
    public struct GameStateComponent
    {
        public GameState State;
        public GameMode Mode;
        public float FallTimer;
    }

    /// <summary>Score of the current session + high score.</summary>
    public struct ScoreComponent
    {
        public int Value;
        public int HighScore;
        public bool NewRecord;
    }

    /// <summary>Current level (ladder thresholds: scorePerLevel * n^2 / 4).</summary>
    public struct LevelComponent
    {
        public int Value;
    }

    /// <summary>
    /// Crystal pickup streak of the current session (consecutive crystals
    /// collected without a miss). Lives on the session entity next to
    /// Score/Level; reset by GameFlowSystem progress reset and by the break
    /// path when a missed crystal starts falling. Max is the best streak
    /// reached this session: grown with Value, never lowered by a break,
    /// reset together with Value by the session progress reset.
    /// </summary>
    public struct ComboComponent
    {
        public int Value;
        public int Max;
    }

    /// <summary>
    /// Track block entity: a composite slab of 1..3 unit parts laid along
    /// -Right from the cursor cell (part 0 at the head cell). Pos is the
    /// center of all parts; Forward is the cursor step axis (the ball
    /// crosses the slab along it); Right is the positive perpendicular
    /// axis (parts extend along -Right, toward the built track). Order is
    /// a monotonic generation counter (no grid). HolePart: -1 = no hole;
    /// otherwise the index (0..Parts-1) of the missing part (any mode,
    /// end parts only).
    /// </summary>
    public struct BlockComponent
    {
        public Vector3 Pos;
        public Vector3 Forward;
        public Vector3 Right;
        public int Parts;
        public int Order;

        /// <summary>Missing part index along Right: -1 none, else 0..Parts-1.</summary>
        public int HolePart;
    }

    /// <summary>
    /// Ball entity. Pos is the logical position; WorldDir is the current
    /// movement direction among the fixed world axes (+Z / +X); while the
    /// ball waits for the first tap it carries BallWaitingTag and WorldDir
    /// is unused. Ground is the packed entity of the block under the ball;
    /// PrevGround is the ground of the previous frame (used to mark the
    /// block the ball just left as falling).
    /// </summary>
    public struct BallComponent
    {
        public Vector3 Pos;
        public Vector3 WorldDir;
        public EcsPackedEntity Ground;
        public EcsPackedEntity PrevGround;
    }

    /// <summary>Link from any entity to its view instance.</summary>
    public struct ViewLinkComponent
    {
        public GameObject View;
        public Transform Transform;
    }

    /// <summary>
    /// Typed link from a block entity to its pooled BlockView instance
    /// (alongside ViewLinkComponent): lets release paths hand the instance
    /// back to the typed block pool without per-release component searches.
    /// </summary>
    public struct BlockViewComponent
    {
        public Views.BlockView View;
    }

    /// <summary>
    /// Typed link from the ball entity to its Rigidbody (alongside
    /// ViewLinkComponent): the body is fetched once at view spawn, so the
    /// per-frame path never calls GetComponent.
    /// </summary>
    public struct BallViewComponent
    {
        public Rigidbody Body;
    }

    /// <summary>Ball is falling (physics on, input off).</summary>
    public struct BallFallingTag { }

    /// <summary>
    /// Ball is airborne in a jump: movement, ground tracking, the fall
    /// check, direction input and crystal pickup are suppressed (systems
    /// exclude jumping balls via Exc filters); BallJumpSystem integrates
    /// the flight and removes the tag on landing.
    /// </summary>
    public struct BallJumpingTag { }

    /// <summary>
    /// Jump flight state of the ball: Timer counts up to Duration while
    /// BallJumpingTag is set; the view adds the arc height on top of the
    /// logical position (height 0 at takeoff and landing).
    /// </summary>
    public struct BallJumpComponent
    {
        public float Timer;
        public float Duration;

        /// <summary>
        /// Arc height above the logic plane at the current timer: 0 at
        /// takeoff and landing, jumpHeight at mid-flight. Single source of
        /// truth shared by the view (rendering) and the golden-crystal
        /// pickup check, so the pickup geometry always matches the rendered
        /// arc.
        /// </summary>
        public float GetHeight(float jumpHeight)
        {
            float t = Mathf.Clamp01(Timer / Duration);
            return jumpHeight * Mathf.Sin(Mathf.PI * t);
        }
    }

    /// <summary>
    /// Ball is the standing menu preview: added by BallSpawnSystem on Menu
    /// spawns only. Removed by GameFlowSystem (StartMenuBall) when Play
    /// starts the session, so the ball rolls immediately - never by
    /// BallTurnSystem (taps are discarded outside Playing).
    /// </summary>
    public struct BallWaitingTag { }

    /// <summary>Block is marked to fall: wait Delay, then sink and die.</summary>
    public struct FallingEventComponent
    {
        public float Delay;
        public float Timer;
    }

    /// <summary>
    /// Pickup crystal lying on a block. Block is the packed host block
    /// entity; Pos is the crystal's world position (on top of the block).
    /// SeatedY is the view's base height (pivot y) computed at view spawn
    /// from the rendered bounds: at the bob's lowest point the view's
    /// lowest point rests on the block surface. Falling/FallTimer drive the
    /// crystal's own fall (slower than the host block's) once the host
    /// block actually starts sinking; the crystal survives the host's death.
    /// </summary>
    public struct CrystalComponent
    {
        public EcsPackedEntity Block;
        public Vector3 Pos;
        public float SeatedY;
        public bool Falling;
        public float FallTimer;
    }

    /// <summary>
    /// Marks a golden crystal variant: floats goldenCrystalHeight above the
    /// block surface and can only be picked up by a jumping ball, via the
    /// full 3D distance between the ball's actual position (logical position
    /// plus jump arc height) and the crystal. Falls, dies and is cleaned up
    /// exactly like a regular crystal, but its view lives in the dedicated
    /// golden pool.
    /// </summary>
    public struct GoldenCrystalTag { }
}
