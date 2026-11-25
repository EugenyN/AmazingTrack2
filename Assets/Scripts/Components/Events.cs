using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// One-frame event components. A consumer system removes the component;
    /// the entity dies automatically when its last component is removed.
    /// </summary>
    public static class Events
    {
        public enum SoundId
        {
            Turn,
            GameStart,
            NextLevel,
            Fall,
            Crystal,
            Jump
        }
    }

    /// <summary>Raw tap/click anywhere on the screen.</summary>
    public struct TapEventComponent { }

    /// <summary>Jump request (right mouse button or the on-screen Jump button).</summary>
    public struct JumpEventComponent { }

    /// <summary>
    /// One-frame ESC toggle (or UI Resume click): Playing -> Paused,
    /// Paused -> Playing. Consumed by GameFlowSystem.
    /// </summary>
    public struct PauseToggleEventComponent { }

    /// <summary>
    /// UI request: select a menu mode (Easy/Normal/Hard). Only the menu
    /// preview changes; no session starts. Consumed by GameFlowSystem.
    /// </summary>
    public struct SelectModeEventComponent
    {
        public GameMode Mode;
    }

    /// <summary>
    /// UI request: start a session on the already-built menu preview track
    /// (no track regeneration). Consumed by GameFlowSystem.
    /// </summary>
    public struct PlayEventComponent { }

    /// <summary>UI request: return to the menu.</summary>
    public struct GoMenuEventComponent { }

    /// <summary>
    /// UI request: restart the finished session (same mode) from the Game
    /// Over screen's Play Again button. Full reset, fresh track. Consumed
    /// by GameFlowSystem.
    /// </summary>
    public struct PlayAgainEventComponent { }

    /// <summary>
    /// Quit request (Android back gesture in the menu): the application
    /// should exit. Emitted by UserInputSystem, consumed by GameFlowSystem.
    /// </summary>
    public struct QuitAppEventComponent { }

    /// <summary>Request to play a sound.</summary>
    public struct SoundEventComponent
    {
        public Events.SoundId Id;
    }

    /// <summary>Score value changed (UI sync).</summary>
    public struct ScoreChangedEventComponent { }

    /// <summary>Level value changed (UI sync).</summary>
    public struct LevelChangedEventComponent { }

    /// <summary>
    /// Combo changed (UI sync): a collected crystal grew the streak to
    /// Value (Break = false; the label shows "{Value} combo"), or a missed
    /// crystal broke a streak of Value >= 2 (Break = true; the label shows
    /// "Combo Break"). One-frame event, consumed by UiSyncSystem.
    /// </summary>
    public struct ComboChangedEventComponent
    {
        public int Value;
        public bool Break;
    }

    /// <summary>Session state changed (UI sync).</summary>
    public struct StateChangedEventComponent { }

    /// <summary>All blocks were destroyed; the generator must restart from origin.</summary>
    public struct TrackResetEventComponent { }

    /// <summary>
    /// One-shot camera snap request: the track cursor restarted from the
    /// origin (init, mode switch, session start, menu return), so the
    /// camera must place the new framing instantly instead of lerping
    /// across the track. Emitted by TrackGenSystem.ResetCursor, consumed
    /// by CameraSystem; kept until a focus exists so a focusless frame
    /// does not drop it.
    /// </summary>
    public struct CameraSnapEventComponent { }
}
