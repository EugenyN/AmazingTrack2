# Proposal

## Why

The game is currently controlled by manual device polling in `UserInputSystem` (tap = LMB/touch, jump = RMB); keyboard and gamepad do not work for gameplay. The `Assets/InputSystem_Actions.inputactions` asset now defines a `Player` map with the actions `Turn` (includes touch and left mouse click), `Jump` (includes right mouse click), `Move`, `Previous`, `Next` - all gameplay input needs to be moved onto these actions, gaining keyboard and gamepad support without duplicating input paths.

## What Changes

- A new ECS system `PlayerActionsInputSystem`, reading the `Player` map of the `InputSystem_Actions` asset:
  - `Turn` (touch, LMB, Enter, the gamepad West button, the joystick trigger) → a turn event (`TapEventComponent`).
  - `Move` (WASD / arrows / left stick): any deviation from neutral → one turn; a repeated one - after returning to neutral.
  - `Previous` (key `1`, d-pad left) and `Next` (key `2`, d-pad right): a press → one turn.
  - `Jump` (Space, the gamepad South button, RMB) → `JumpEventComponent`.
- `UserInputSystem` loses the tap paths (LMB + Enhanced Touch) and the RMB jump: the `Turn` action includes touch and LMB, `Jump` - RMB, so the old manual polling alongside active actions would duplicate events. Only ESC/back (pause/exit) remains in `UserInputSystem` - those actions are not in the asset.
- Enhanced Touch is no longer used by anyone - the `EnhancedTouchSupport.Enable()` call is removed.
- The guard "a press over an interactive UI button does not count as gameplay input" is preserved for pointer sources (touch/LMB/RMB) via the active device's position.
- Events reuse the existing one-frame components; validation and suppression (only `Playing`, ignored while falling/in a jump) stay in `BallTurnSystem`/`BallJumpSystem`.
- `Bootstrap` gets a serialized field referencing the asset (assigned in the Inspector, a manual Editor step).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `ball`: the turn and jump input sources are moved onto the `Turn`/`Jump` actions of the `Player` map (touch and LMB now via `Turn`, RMB via `Jump`), keyboard/gamepad sources are added (`Move` on deviation, `Previous`, `Next`, Space, the South button). The observable rules (one toggle per press, suppression in the air/while falling, outside `Playing`, exclusivity with UI buttons) are preserved.

## Impact

- New file: `Assets/Scripts/Systems/PlayerActionsInputSystem.cs`.
- Change: `Assets/Scripts/Bootstrap.cs` - a serialized `InputActionAsset` field, system registration after `UserInputSystem`, removal of `EnhancedTouchSupport.Enable()`.
- Change: `Assets/Scripts/Systems/UserInputSystem.cs` - the tap and RMB-jump paths are removed (ESC/back stays).
- Scene: assign the `Assets/InputSystem_Actions.inputactions` asset to the Bootstrap field (a manual Editor step).
- Not affected: `BallTurnSystem`, `BallJumpSystem`, UI bridges, configs, the test bot (creates events directly).
