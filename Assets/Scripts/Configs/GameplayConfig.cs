using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Simulation-rules tuning: ball speed, scoring, jump, crystal spawn and
    /// pickup, combo thresholds, hole chances per mode, track geometry. No
    /// Unity asset references here - those live in
    /// <see cref="PresentationConfig"/>; derived scoring math lives in
    /// <see cref="ScoringExtensions"/>. Asset lives at Assets/Configs.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "AmazingTrack/Gameplay Config")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Ball")]
        [Tooltip("Base ball speed, units/sec (level 1)")]
        public float baseSpeed = 4f;

        [Tooltip("Speed added per level")]
        public float speedPerLevel = 0.25f;

        [Tooltip("Ball radius = the visible sphere's actual radius (drives rolling, SphereCast and ground clearance). Must match the ball prefab's sphere scale.")]
        public float ballRadius = 0.25f;

        [Header("Scoring")]
        [Tooltip("Base size of the level score step. The step from level L to L+1 is scorePerLevel * ceil(L/2): it grows by scorePerLevel every two levels, so the entry threshold of level n is scorePerLevel * n^2 / 4 (500, 1000, 2000, 3000, 4500, ...)")]
        public int scorePerLevel = 500;

        [Tooltip("Points per regular crystal")]
        public int crystalScore = 10;

        [Tooltip("Points per golden crystal (floating, jump-only pickup)")]
        public int goldenCrystalScore = 50;

        [Tooltip("Horizontal distance covered by one jump, world units (2 = two blocks, clears a one-cell hole). The flight duration adapts to the current track speed: duration = jumpDistance / track speed (higher speed = faster jump, same distance)")]
        public float jumpDistance = 2f;

        [Tooltip("Peak height of the jump arc above the track surface, units. Also defines the golden crystal pickup height used by the golden-crystal pickup check.")]
        public float jumpHeight = 1.0f;

        [Header("Crystals")]
        [Tooltip("Chance (0..1) that a new crystal chain starts on an eligible block once the minimum gap since the last chain has passed")]
        [Range(0f, 1f)]
        public float crystalChance = 0.1f;

        [Tooltip("Number of leading blocks of a session (menu preview included) that spawn no crystals")]
        [Min(0)]
        public int crystalStartDelayBlocks = 10;

        [Tooltip("Minimum number of eligible blocks without crystals between two crystal chains")]
        [Min(0)]
        public int crystalMinGapBlocks = 10;

        [Tooltip("Maximum number of eligible blocks in a row without a crystal; the next eligible block then force-spawns one (pity)")]
        [Min(0)]
        public int crystalMaxGapBlocks = 20;

        [Tooltip("Minimum crystal count of one chain (the length is rolled uniformly in the min..max range)")]
        [Min(1)]
        public int crystalChainMinBlocks = 1;

        [Tooltip("Maximum crystal count of one chain; consecutive chain crystals sit on adjacent cells 1 unit apart - neighboring blocks and/or neighboring parts of one block")]
        [Min(1)]
        public int crystalChainMaxBlocks = 5;

        [Tooltip("Pickup radius (XZ distance, units)")]
        public float crystalPickupRadius = 0.6f;

        [Tooltip("Chance (0..1) that a spawned crystal is the golden variant; rolled only for crystals that actually spawn, so it composes with crystalChance")]
        [Range(0f, 1f)]
        public float goldenCrystalChance = 0.2f;

        [Tooltip("Height above the block surface the golden crystal floats at (view-only; default equals the jump peak height so a well-timed jump catches it)")]
        public float goldenCrystalHeight = 1.0f;

        [Tooltip("Full 3D pickup radius of a golden crystal: the distance between the ball's actual position (logical position + jump arc height) and the crystal must be within this radius. Smaller than the ground pickup radius to keep the timing window honest")]
        public float goldenCrystalPickupRadius = 0.6f;

        [Header("Combo")]
        [Tooltip("Combo value from which the crystal score multiplier is 2 (a crystal reaching this streak is already doubled)")]
        public int comboDoubleFrom = 2;

        [Tooltip("Combo value from which the crystal score multiplier is 3")]
        public int comboTripleFrom = 5;

        [Tooltip("Combo value from which the crystal score multiplier is 4")]
        public int comboQuadFrom = 10;

        [Header("Holes")]
        [Tooltip("Missing part (hole) chance for Easy, any block after the start segment")]
        [Range(0f, 1f)]
        public float holeChanceEasy = 0.05f;

        [Tooltip("Missing part (hole) chance for Normal, any block after the start segment")]
        [Range(0f, 1f)]
        public float holeChanceNormal = 0.03f;

        [Tooltip("Missing part (hole) chance for Hard, any block after the start segment (the whole block is missing)")]
        [Range(0f, 1f)]
        public float holeChanceHard = 0.015f;

        [Header("Track")]
        [Tooltip("Block length along the track, world units")]
        public float blockLength = 1f;

        [Tooltip("Block height, world units")]
        public float blockHeight = 0.5f;

        [Tooltip("Straight (forced +Z) steps at session start (min, inclusive)")]
        public int startSegmentMin = 5;

        [Tooltip("Straight (forced +Z) steps at session start (max, exclusive)")]
        public int startSegmentMax = 8;

        [Tooltip("Target number of blocks ahead of the ball")]
        public int lookaheadBlocks = 35;

        [Tooltip("Blocks built per generation batch")]
        public int buildBatch = 4;
    }
}
