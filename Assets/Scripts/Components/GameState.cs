namespace AmazingTrack
{
    /// <summary>
    /// Session states (spec game-flow, game-pause):
    /// Menu -> Playing -> Paused (ESC toggle) -> Playing -> ... ->
    /// Falling -> GameOver -> Playing (restart) / Menu.
    /// </summary>
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Falling,
        GameOver
    }
}