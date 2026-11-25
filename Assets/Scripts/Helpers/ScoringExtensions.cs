using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Scoring math derived from <see cref="GameplayConfig"/> tuning fields,
    /// kept off the ScriptableObject type as extension methods (the
    /// <see cref="GameModeExtensions"/> precedent). All methods are pure,
    /// integer-only and allocation-free.
    /// </summary>
    public static class ScoringExtensions
    {
        /// <summary>
        /// Crystal score multiplier for the given combo streak value (the
        /// value AFTER the pickup that reached it): x4 from comboQuadFrom,
        /// x3 from comboTripleFrom, x2 from comboDoubleFrom, else x1.
        /// Allocation-free, called on the pickup path only.
        /// </summary>
        public static int ComboMultiplier(this GameplayConfig cfg, int combo)
        {
            if (combo >= cfg.comboQuadFrom)
                return 4;
            if (combo >= cfg.comboTripleFrom)
                return 3;
            if (combo >= cfg.comboDoubleFrom)
                return 2;
            return 1;
        }

        /// <summary>
        /// Entry score threshold of the given level (levels are numbered
        /// from 1): scorePerLevel * level^2 / 4 with integer division, the
        /// closed form of the ladder rule "the step from level L to L+1 is
        /// scorePerLevel * ceil(L/2)". With the default scorePerLevel of
        /// 500 the thresholds are 500, 1000, 2000, 3000, 4500, ... — the
        /// step grows by scorePerLevel every two levels. Allocation-free.
        /// </summary>
        public static int ScoreForLevel(this GameplayConfig cfg, int level)
        {
            return cfg.scorePerLevel * level * level / 4;
        }
    }
}
