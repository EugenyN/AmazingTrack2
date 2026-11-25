namespace AmazingTrack
{
    /// <summary>
    /// Game modes. The number of unit parts per composite block comes from
    /// <see cref="GameModeExtensions.PartsCount"/> (3/2/1) - do NOT encode
    /// it in the enum values: duplicate values would merge the modes.
    /// </summary>
    public enum GameMode
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public static class GameModeExtensions
    {
        /// <summary>Number of unit parts per composite block in this mode.</summary>
        public static int PartsCount(this GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Easy:
                    return 3;
                case GameMode.Normal:
                    return 2;
                case GameMode.Hard:
                    return 1;
                default:
                    return 1;
            }
        }

        /// <summary>Missing part (hole) chance of this mode, from the config.</summary>
        public static float HoleChance(this GameMode mode, GameplayConfig cfg)
        {
            switch (mode)
            {
                case GameMode.Easy:
                    return cfg.holeChanceEasy;
                case GameMode.Hard:
                    return cfg.holeChanceHard;
                case GameMode.Normal:
                default:
                    return cfg.holeChanceNormal;
            }
        }
    }
}
