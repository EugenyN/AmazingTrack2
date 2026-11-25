using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// PlayerPrefs keys (spec scoring). High scores are stored per game
    /// mode: the legacy shared key is no longer read or written.
    /// </summary>
    public static class PrefsHelper
    {
        public const string HighScoreEasy = "AmazingTrack2.HighScore.Easy";
        public const string HighScoreNormal = "AmazingTrack2.HighScore.Normal";
        public const string HighScoreHard = "AmazingTrack2.HighScore.Hard";

        /// <summary>PlayerPrefs key of the high score of this game mode.</summary>
        public static string HighScoreKey(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Easy:
                    return HighScoreEasy;
                case GameMode.Hard:
                    return HighScoreHard;
                case GameMode.Normal:
                default:
                    return HighScoreNormal;
            }
        }
    }
}
