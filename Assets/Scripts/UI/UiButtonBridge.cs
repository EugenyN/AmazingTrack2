using UnityEngine;
using UnityEngine.EventSystems;
using Leopotam.EcsLite;

namespace AmazingTrack.UI
{
    /// <summary>
    /// Button -> ECS event bridge. Placed on every mode button, the Play
    /// button, the Menu buttons, the pause-screen buttons, the HUD pause
    /// button and the Game Over screen's Play Again button; creates
    /// one-frame event entities consumed by GameFlowSystem.
    /// Mode buttons only select the mode (the menu preview rebuilds; no
    /// session starts); Play emits PlayEventComponent. Resume and Pause emit
    /// PauseToggleEventComponent (the same toggle ESC uses: Paused ->
    /// Playing, Playing -> Paused). Jump is the exception: it implements
    /// IPointerDownHandler and emits JumpEventComponent on pointer press
    /// (not on click/release) so the jump starts instantly. Duplicate
    /// pointer-press events from one physical fast tap (the browser
    /// -synthesized mouse press following the touch press on mobile web)
    /// emit a single jump: a press is ignored while the previous one has
    /// not seen its matching release (IPointerUpHandler clears the state).
    /// </summary>
    public class UiButtonBridge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Action
        {
            SelectEasy,
            SelectNormal,
            SelectHard,
            Play,
            GoMenu,
            PlayAgain,
            Resume,
            Pause,
            Jump
        }

        public Action ButtonAction;

        /// <summary>
        /// Jump press dedup: true from OnPointerDown until the matching
        /// OnPointerUp. A second press while held (the browser-synthesized
        /// mouse press following a touch press on mobile web) is ignored,
        /// so one physical tap emits exactly one jump event.
        /// </summary>
        private bool _jumpPressed;

        /// <summary>
        /// World is injected by UiRoot when binding buttons. Runtime-only:
        /// never serialized (EcsWorld is not a serializable type).
        /// </summary>
        [System.NonSerialized]
        public EcsWorld World;

        public void OnClick()
        {
            if (World == null)
                return;
            switch (ButtonAction)
            {
                case Action.SelectEasy:
                    EmitSelect(GameMode.Easy);
                    break;
                case Action.SelectNormal:
                    EmitSelect(GameMode.Normal);
                    break;
                case Action.SelectHard:
                    EmitSelect(GameMode.Hard);
                    break;
                case Action.Play:
                    World.GetPool<PlayEventComponent>().Add(World.NewEntity());
                    break;
                case Action.GoMenu:
                    World.GetPool<GoMenuEventComponent>().Add(World.NewEntity());
                    break;
                case Action.PlayAgain:
                    World.GetPool<PlayAgainEventComponent>().Add(World.NewEntity());
                    break;
                case Action.Resume:
                case Action.Pause:
                    World.GetPool<PauseToggleEventComponent>().Add(World.NewEntity());
                    break;
            }
        }

        /// <summary>
        /// Jump fires on pointer press (not on the click, which Unity raises
        /// on release) so the jump starts instantly. Click does not emit a
        /// jump: one press emits exactly one jump event. A press arriving
        /// while the button is already held (duplicate pointer-press from
        /// the same physical tap) is ignored until the next OnPointerUp.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (World == null || ButtonAction != Action.Jump || _jumpPressed)
                return;
            _jumpPressed = true;
            World.GetPool<JumpEventComponent>().Add(World.NewEntity());
        }

        /// <summary>
        /// Clears the jump press state so the next physical press emits a
        /// jump again.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            _jumpPressed = false;
        }

        private void EmitSelect(GameMode mode)
        {
            var pool = World.GetPool<SelectModeEventComponent>();
            pool.Add(World.NewEntity()).Mode = mode;
        }
    }
}
