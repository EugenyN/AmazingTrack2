# Design

## Context

Input is currently manual: `UserInputSystem` polls devices directly (`Mouse.current`, `Keyboard.current`, Enhanced Touch) and creates the one-frame events `TapEventComponent` / `JumpEventComponent` / `PauseToggleEventComponent`. The user modified the `Assets/InputSystem_Actions.inputactions` asset: the `Player` map now contains `Turn` (bindings: touch `<Touchscreen>/primaryTouch/tap`, LMB, Enter, buttonWest, the joystick trigger, XR), `Jump` (Space, buttonSouth, **RMB**, XR), `Move` (WASD+arrows+left stick, Value/Vector2), `Previous` (`1`, dpad-left), `Next` (`2`, dpad-right). The `Look`/`Attack`/`Interact`/`Crouch`/`Sprint` actions were removed. Wrapper generation is off (`generateWrapperCode: 0`), `PlayerInput` is not used. The consumers (`BallTurnSystem`, `BallJumpSystem`) validate events by the session state and ball tags; the event source is irrelevant to them.

A key consequence of the bindings: `Turn` includes touch and LMB, `Jump` - RMB. If the old manual polling is kept alongside the actions, every press will produce two events (polling + action). The input path for tap/jump must become a single one.

## Goals / Non-Goals

**Goals:**

- A single gameplay input path via the `Player` map actions: `Turn`/`Move`/`Previous`/`Next` → turn, `Jump` → jump; keyboard and gamepad work "for free" from the bindings.
- Preserving all observable rules: one event per press, suppression in the air/while falling, outside `Playing`, exclusivity with UI buttons for pointer sources.
- ESC/back (pause/exit) stays in `UserInputSystem` - those actions are not in the asset.

**Non-Goals:**

- Moving pause/exit onto actions (there are no actions for them in the asset).
- Rebinding bindings, UI rebinding, new control schemes, using the asset's UI map.
- Gamepad vibration, support of several simultaneous gamepads.

## Decisions

**1. All gameplay input logic moves into a new `PlayerActionsInputSystem`; `UserInputSystem` loses the tap and RMB-jump paths.**
`Turn` includes touch and LMB, `Jump` - RMB, so a parallel manual polling would duplicate events. `UserInputSystem` remains only for ESC/back (pause/exit) - the only non-gameplay paths. `ConsumeRawTap`, `RightMousePressed`/`RightMousePosition`, Enhanced Touch polling and the raycast guard are removed from it (the guard moves into the new system). The alternative - suppressing duplicates via an "actions are active" flag - is rejected: two active sources of one event is hidden state that will inevitably desynchronize.

**2. Registration: `UserInputSystem` (ESC) → `PlayerActionsInputSystem` → the rest.**
All event sources stand before the consumers (`BallTurnSystem`, `BallJumpSystem`); the order between sources does not matter - they write into the shared event pool within one frame.

**3. The input asset - via a serialized `Bootstrap` field, like `GameplayConfig`/`PresentationConfig`.**
A `public InputActionAsset InputActions;` is added; the system is created only if the field is assigned, otherwise gameplay input is disabled without errors (ESC keeps working). A manual Editor step: assign the asset to the Bootstrap field. `Resources.Load` was rejected: a hidden dependency, the asset is not in `Resources/`.

**4. Enabling the `Player` map in `Init`, disabling in `Destroy` (IEcsDestroySystem); polling, not callbacks.**
Button actions (`Turn`, `Jump`, `Previous`, `Next`) - `WasPressedThisFrame()`; `Move` - `ReadValue<Vector2>()`. Polling is consistent with the ECS pipeline (events are created inside `Run`, not in callbacks outside the system order) and does not allocate. For `WasPressedThisFrame()` on buttons, `initialStateCheck: false` is enough (the asset's default).

**5. `Move` - a turn on the neutral → deviated transition with hysteresis (the user's choice: "any deviation").**
The system keeps a `bool _moveDeviated`. Deviation is `magnitude > 0.5`; the reset is `magnitude < 0.1` (hysteresis against stick jitter near neutral). A turn is created on the transition from "not deviated" to "deviated".

**6. The UI guard for pointer bindings - by the active pointer's position.**
Touch/LMB/RMB have no position in the action itself (`Turn` is a Button), so when `Turn`/`Jump` fires, the system checks the position of the pointing device that caused the firing: for the mouse - `Mouse.current.position`; for touch - the last active touch point (Enhanced Touch is kept only for the position, `Touch.activeTouches`). If the position is over a `Selectable` (`EventSystem.RaycastAll`, reused `PointerEventData`/`List<RaycastResult>` - no allocations) - no event is created. Keyboard/gamepad bindings pass no checks: they have no position. A practical simplification: check the mouse position on any firing from mouse/touch; a tap on a HUD button is handled by the button anyway.
Note: a touch-tap on a button passes the raycast into UI events earlier, but the guard is needed for symmetry with the old behavior (a click on Play Again must not turn the ball).

**7. Enhanced Touch Support.**
`EnhancedTouchSupport.Enable()` in `Bootstrap.Start` is removed: its only consumer was the `UserInputSystem` tap path, which is removed. The new system uses the `Turn` action's `<Touchscreen>/primaryTouch/tap` binding - the Input System handles that itself. The touch position for the guard is read from `Touch.activeTouches` (which requires Enhanced Touch to be enabled - see the task: the system calls `EnhancedTouchSupport.Enable()` in `Init` when the asset is present).

## Risks / Trade-offs

- [`WasPressedThisFrame()` and low FPS: a press and release between frames] → The Input System keeps presses within the polling frame; on a skipped frame the event may be lost (a general limitation of the polling model; the old polling behaved the same).
- [A touch-tap on a HUD button turns the ball] → The guard by touch position covers the case; buttons process the pointer earlier and are interactive, the raycast cuts it off.
- [The asset is not assigned] → Gameplay input is unavailable, ESC works, no errors. A manual step in tasks.md.
- [A double turn in one frame from `Turn` and `Move`] → The probability is negligible (a simultaneous LMB press and stick movement); the consequence is one extra toggle. Not buffered.
- [Window focus loss with a held `Move` key] → The Input System resets devices on focus loss; the neutral hysteresis returns the system to its initial state without a turn.

## Migration Plan

1. Add the system code, change `UserInputSystem` and `Bootstrap` (compilation is safe: the `InputActions` field defaults to `null`, gameplay input is temporarily unavailable until the asset is assigned).
2. In the Unity Editor, assign the `InputSystem_Actions.inputactions` asset to the Bootstrap field (a manual step; we do not hand-edit scenes).
3. Rollback: restore the previous version of `UserInputSystem`/`Bootstrap` from git; the touch path is restored entirely.

## Open Questions

(none - the `Move` logic choice was made by the user earlier: "any deviation"; the new bindings are defined by the asset.)
