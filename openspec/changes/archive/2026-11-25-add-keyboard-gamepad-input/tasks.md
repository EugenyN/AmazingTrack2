# Tasks

## 1. PlayerActionsInputSystem

- [x] 1.1 Create `Assets/Scripts/Systems/PlayerActionsInputSystem.cs`: constructor takes `InputActionAsset`; `Init` caches pools (`TapEventComponent`, `JumpEventComponent`), caches the `Turn`/`Move`/`Previous`/`Next`/`Jump` actions from the `Player` map, enables the map, calls `EnhancedTouchSupport.Enable()` (touch position for the UI guard); implement `IEcsDestroySystem` to disable the map. Verify: project compiles in Unity Editor with no console errors.
- [x] 1.2 Implement jump input: poll the `Jump` action with `WasPressedThisFrame()`; when the press comes from the mouse pointer (RMB binding), skip events over an interactable `Selectable` (reused `PointerEventData` + raycast list, no allocations); add `JumpEventComponent` (one event per press, no repeat while held). Verify in Play Mode: Space / gamepad South / RMB starts a jump during Playing; holding does not chain jumps; no jump in Menu/Paused/GameOver; RMB over a HUD button does not jump.
- [x] 1.3 Implement turn input from buttons: poll `Turn`, `Previous`, `Next` with `WasPressedThisFrame()`; for `Turn` presses from touch/LMB, apply the same Selectable guard by pointer position; add `TapEventComponent`. Verify in Play Mode: tap, LMB, Enter, gamepad West each toggle direction exactly once per press while Playing; tapping a HUD button does not toggle.
- [x] 1.4 Implement turn input from `Move` (any deviation, per design): read the `Vector2` value each frame; emit one `TapEventComponent` on the neutral → deviated transition (threshold 0.5), reset the latch below 0.1 (hysteresis). Verify in Play Mode: any WASD/arrow/stick deviation toggles direction once; holding it does not repeat; releasing and re-deviating toggles again.
- [ ] 1.5 Verify no GC allocations in the hot path: poll actions, create event entities, no collections or string ops in `Run`. Verify: Profile a 30-second Play Mode session (Profiler GC alloc column) — no per-frame allocations.

## 2. UserInputSystem trim and Bootstrap wiring

- [x] 2.1 Remove the tap path (LMB + Enhanced Touch polling, `ConsumeRawTap`), the RMB jump path (`RightMousePressed`/`RightMousePosition`) and the Selectable guard from `UserInputSystem`; keep ESC/back pause-quit logic and its pools. Verify: compiles; in Play Mode ESC pauses/resumes; taps/LMB/RMB no longer create events from this system.
- [x] 2.2 Add `public InputActionAsset InputActions;` to `Bootstrap`, remove `EnhancedTouchSupport.Enable()` from `Start` (moved into the new system's `Init`), register `new PlayerActionsInputSystem(InputActions)` immediately after `UserInputSystem` in the chain; skip creating the system when the field is null. Verify: compiles; scene without the assigned asset plays with no console errors, ESC pause works, gameplay input disabled.
- [x] 2.3 Assign `Assets/InputSystem_Actions.inputactions` to the new Bootstrap field in the scene (manual Editor step, do not hand-edit the `.unity` file). Verify: Inspector shows the asset reference on the scene's Bootstrap.

## 3. Integration verification (Play Mode)

- [ ] 3.1 Single-event check: exactly one direction toggle per physical press — LMB, tap, RMB jump, Space jump (old polling removed, actions active). Verify by exercising the feature in Play Mode (one click = one turn; one RMB = one jump).
- [ ] 3.2 Keyboard path: during Playing, WASD/arrows toggle once per deviation; Space jumps; `1`/`2` toggle; ESC pauses; LMB/RMB work through actions. Verify in Play Mode.
- [ ] 3.3 Gamepad path: left stick deviation and dpad left/right toggle once per press; button South jumps; button West turns. Verify with the Input System debugger or a connected gamepad in Play Mode.
- [ ] 3.4 Suppression and UI rules hold: turn input ignored while jumping/falling, jump input ignored outside Playing, on-screen Jump button unchanged, no raw tap from actions. Verify in Play Mode (jump over a hole, try turning mid-air, open pause with ESC, click Play Again without a turn).

## 4. Pause action

- [x] 4.1 Add `Pause` action handling (`P` / gamepad Start) to `PlayerActionsInputSystem`: poll with `WasPressedThisFrame()` and emit `PauseToggleEventComponent` — the same toggle event the HUD Pause button emits. Verify: compiles; in Play Mode `P` and gamepad Start toggle Playing ↔ Paused exactly like ESC and the HUD Pause button; held key does not re-toggle; ignored in Menu/Falling/GameOver.
- [x] 4.2 Remove the ESC pause path from `UserInputSystem` (ESC is now a `Pause` action binding; polling it too would double-fire the toggle and cancel it out); keep only the Android back-gesture quit in Menu. Verify: compiles; in Play Mode ESC, `P` and gamepad Start each toggle pause exactly once per press; Android back in Menu still quits (code inspection on desktop).
