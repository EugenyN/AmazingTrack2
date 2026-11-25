using Leopotam.EcsLite;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AmazingTrack
{
    /// <summary>
    /// Gameplay input from the Player action map of the InputSystem_Actions
    /// asset: Turn (touch, left mouse, Enter, gamepad West, joystick
    /// trigger), Move (WASD, arrows, left stick - any deviation), Previous
    /// (key 1, dpad left), Next (key 2, dpad right) create the same
    /// TapEventComponent the old raw tap produced; Jump (Space, gamepad
    /// South, right mouse) creates JumpEventComponent; Pause (key P, gamepad
    /// Start) creates PauseToggleEventComponent - the same toggle the HUD
    /// Pause button and ESC emit (Playing &lt;-&gt; Paused, validated by
    /// GameFlowSystem). The old manual device
    /// polling for taps and jumps was removed from UserInputSystem: Turn
    /// includes touch and left mouse, Jump includes right mouse, so polling
    /// alongside actions would double-fire every press.
    /// Validation stays in the consumers (BallTurnSystem, BallJumpSystem):
    /// this system only translates actions into event components.
    /// Pointer-originated presses (touch, left/right mouse) are dropped when
    /// they land on an interactable Selectable: the button handles the press
    /// itself, so HUD clicks (e.g. the Game Over screen's Play Again) never
    /// double-fire as gameplay input. Keyboard and gamepad bindings have no
    /// screen position and never take the guard (checked via the active
    /// control's device: Mouse or Touchscreen only).
    /// Touch position for the guard is read through the Enhanced Touch API
    /// (Touch.activeTouches): the Turn action's touchscreen tap binding does
    /// not expose it. Requires EnhancedTouchSupport.Enable() (done in Init).
    /// Web (UNITY_WEBGL): mobile browsers deliver a fast tap twice - the
    /// native touchscreen press, then a synthesized mouse press+release
    /// batched into a later frame. A batched press leaves the action with
    /// no active control when polled, so such presses are dropped
    /// (IsSyntheticPress): one tap - one event. Desktop browsers have no
    /// touchscreen device and keep mouse input unchanged; keyboard/gamepad
    /// bindings are unaffected.
    /// All polling is allocation-free (hot path rule): reused
    /// PointerEventData and raycast list, no per-frame collections.
    /// </summary>
    public class PlayerActionsInputSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        /// <summary>Stick/Move magnitude above which the action counts as deviated.</summary>
        private const float DeviationThreshold = 0.5f;

        /// <summary>Stick/Move magnitude below which the action counts as back to neutral.</summary>
        private const float NeutralThreshold = 0.1f;

        private readonly InputActionAsset _asset;
        private EcsWorld _world;
        private EcsPool<TapEventComponent> _tapPool;
        private EcsPool<JumpEventComponent> _jumpPool;
        private EcsPool<PauseToggleEventComponent> _pauseTogglePool;
        private InputActionMap _playerMap;
        private InputAction _turn;
        private InputAction _move;
        private InputAction _previous;
        private InputAction _next;
        private InputAction _jump;
        private InputAction _pause;

        // Latch for the Move action: turn fires on the neutral -> deviated
        // transition only; re-armed when the value returns below the neutral
        // threshold (hysteresis against stick jitter near neutral).
        private bool _moveDeviated;

        // Reused per press: no allocations in the guard frame (hot path rule).
        private readonly PointerEventData _pointerData;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(8);

        public PlayerActionsInputSystem(InputActionAsset asset)
        {
            _asset = asset;
            _pointerData = new PointerEventData(EventSystem.current);
        }

#if UNITY_WEBGL
        /// <summary>
        /// True when the press is a browser-synthesized duplicate of a
        /// touch. Mobile browsers deliver a fast tap as the native touch
        /// press first, then as a synthesized mouse press+release batched
        /// into a later frame; when such a batched press is polled, the
        /// action has no active control anymore. Real presses (touch,
        /// keyboard, gamepad) always carry an active control while pressed.
        /// Gated on a touchscreen device being present, so desktop browsers
        /// (mouse only) are unaffected. Confirmed by on-device diagnostics:
        /// fast tap - Touchscreen press at frame N, activeControl=null
        /// press at frame N+2; long press - a single Touchscreen press.
        /// </summary>
        private static bool IsSyntheticPress(InputAction action)
            => Touchscreen.current != null && action.activeControl == null;
#endif

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _tapPool = _world.GetPool<TapEventComponent>();
            _jumpPool = _world.GetPool<JumpEventComponent>();
            _pauseTogglePool = _world.GetPool<PauseToggleEventComponent>();

            _playerMap = _asset.FindActionMap("Player", throwIfNotFound: true);
            _turn = _playerMap.FindAction("Turn", throwIfNotFound: true);
            _move = _playerMap.FindAction("Move", throwIfNotFound: true);
            _previous = _playerMap.FindAction("Previous", throwIfNotFound: true);
            _next = _playerMap.FindAction("Next", throwIfNotFound: true);
            _jump = _playerMap.FindAction("Jump", throwIfNotFound: true);
            _pause = _playerMap.FindAction("Pause", throwIfNotFound: true);

            // Enhanced Touch must be enabled before Touch.activeTouches is
            // read (touch position for the UI guard); the old Bootstrap call
            // moved here because this system is its only consumer now.
            EnhancedTouchSupport.Enable();

            _playerMap.Enable();
        }

        public void Run(IEcsSystems systems)
        {
            if (_turn.WasPressedThisFrame()
                && !(PointerOriginated(_turn) && PointerOverSelectable(TouchOrMousePosition()))
#if UNITY_WEBGL
                && !IsSyntheticPress(_turn)
#endif
                )
            {
                _tapPool.Add(_world.NewEntity());
            }

            if (_previous.WasPressedThisFrame())
            {
                _tapPool.Add(_world.NewEntity());
            }

            if (_next.WasPressedThisFrame())
            {
                _tapPool.Add(_world.NewEntity());
            }

            var move = _move.ReadValue<Vector2>();
            if (move.sqrMagnitude > DeviationThreshold * DeviationThreshold)
            {
                if (!_moveDeviated)
                {
                    _moveDeviated = true;
                    _tapPool.Add(_world.NewEntity());
                }
            }
            else if (move.sqrMagnitude < NeutralThreshold * NeutralThreshold)
            {
                _moveDeviated = false;
            }

            if (_jump.WasPressedThisFrame()
                && !(PointerOriginated(_jump) && PointerOverSelectable(TouchOrMousePosition()))
#if UNITY_WEBGL
                && !IsSyntheticPress(_jump)
#endif
                )
            {
                _jumpPool.Add(_world.NewEntity());
            }

            // Pause (P, gamepad Start): the same toggle event the HUD Pause
            // button and ESC emit (Playing <-> Paused). Emitted regardless of
            // the session state; GameFlowSystem validates it (state must be
            // Playing or Paused), so Menu/Falling/GameOver ignore it. No
            // Selectable guard: keyboard/gamepad bindings have no screen
            // position, and the HUD Pause button itself emits the same event.
            if (_pause.WasPressedThisFrame())
            {
                _pauseTogglePool.Add(_world.NewEntity());
            }
        }

        public void Destroy(IEcsSystems systems)
        {
            _playerMap.Disable();
        }

        /// <summary>
        /// True when the action's press this frame came from a
        /// pointer-positioned binding (mouse or touchscreen): only those
        /// presses take the Selectable guard. Keyboard, gamepad, joystick
        /// and XR bindings have no screen position and never take it, so
        /// Space pressed with the cursor over a HUD button still jumps.
        /// activeControl is valid in the frame a polled action changed.
        /// </summary>
        private static bool PointerOriginated(InputAction action)
        {
            var control = action.activeControl;
            return control != null
                   && (control.device is Mouse || control.device is Touchscreen);
        }

        /// <summary>
        /// Screen position of the active pointer for the Selectable guard:
        /// the touch position when a finger is on the screen, otherwise the
        /// mouse position. Keyboard/gamepad presses read the same position
        /// but never trigger this path - they only reach the guard when a
        /// pointer-driven binding (touch/LMB/RMB) fired the action.
        /// </summary>
        private static Vector2 TouchOrMousePosition()
        {
            var activeTouches = Touch.activeTouches;
            for (int i = 0; i < activeTouches.Count; i++)
            {
                if (activeTouches[i].phase != TouchPhase.Ended
                    && activeTouches[i].phase != TouchPhase.Canceled)
                {
                    return activeTouches[i].screenPosition;
                }
            }

            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : default;
        }

        /// <summary>
        /// True when the press position raycasts onto an interactable
        /// Selectable (button, including presses on its child labels). Uses
        /// RaycastAll directly instead of EventSystem.current
        /// .IsPointerOverGameObject to avoid the touch-frame timing pitfall
        /// of the latter.
        /// </summary>
        private bool PointerOverSelectable(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            _pointerData.position = screenPosition;
            _raycastResults.Clear();
            eventSystem.RaycastAll(_pointerData, _raycastResults);
            for (int i = 0; i < _raycastResults.Count; i++)
            {
                var hit = _raycastResults[i].gameObject;
                var selectable = hit != null
                    ? hit.GetComponentInParent<Selectable>()
                    : null;
                if (selectable != null && selectable.interactable)
                    return true;
            }
            return false;
        }
    }
}
