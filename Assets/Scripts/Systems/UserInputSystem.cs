using Leopotam.EcsLite;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AmazingTrack
{
    /// <summary>
    /// Android back gesture only: ESC in the Menu state emits
    /// QuitAppEventComponent (there is no session to lose). In Playing and
    /// Paused the back gesture (delivered as ESC) pauses/resumes through the
    /// InputSystem_Actions Pause action (PlayerActionsInputSystem) - the same
    /// keypress, so polling it here as well would double-fire the toggle and
    /// cancel it out; in Menu the toggle event the Pause action emits is
    /// ignored by GameFlowSystem, and the quit request is emitted here.
    /// On non-Android platforms this system does nothing: ESC pause comes
    /// from the Pause action binding too (and ESC never quits per spec).
    /// </summary>
    public class UserInputSystem : IEcsInitSystem, IEcsRunSystem
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private EcsWorld _world;
        private EcsFilter _sessions;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<QuitAppEventComponent> _quitPool;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _sessions = _world.Filter<GameStateComponent>().End();
            _statePool = _world.GetPool<GameStateComponent>();
            _quitPool = _world.GetPool<QuitAppEventComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            // Back gesture: quit only from the menu (no session to lose).
            // In a session the same ESC keypress pauses via the Pause action;
            // in Falling and GameOver both paths ignore it.
            if (EscapePressed()
                && _sessions.GetSingletonComponent(_statePool).State == GameState.Menu)
            {
                _quitPool.Add(_world.NewEntity());
            }
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
#else
        public void Init(IEcsSystems systems)
        {
        }

        public void Run(IEcsSystems systems)
        {
        }
#endif
    }
}
