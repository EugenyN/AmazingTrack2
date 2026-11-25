# Spec Delta

## ADDED Requirements

### Requirement: Gameplay input via the Player map actions

Gameplay input (turn and jump) SHALL come from the actions of the `Player` map of the `InputSystem_Actions` asset: `Turn`, `Move`, `Previous`, `Next` - turn; `Jump` - jump. Manual polling of touch and the left/right mouse buttons for gameplay SHALL be removed: the same physical presses come through the `Turn` (touch, LMB) and `Jump` (RMB) bindings, and each physical press SHALL produce exactly one event. The `Jump` action SHALL activate a jump on Space, the gamepad South button, and RMB. The `Previous` (key `1`, d-pad left) and `Next` (key `2`, d-pad right) actions SHALL produce exactly one turn per press. Any deviation of `Move` (WASD, arrows, the gamepad left stick) from neutral SHALL produce one turn; a repeated turn from `Move` SHALL happen only after the value returns to neutral. Each turn source SHALL produce the same "forward ↔ sideways" toggle turn as the previous tap: all suppression rules (only in `Playing`, ignored while falling and in a jump) apply in full. The input asset SHALL be connected to the scene as the source of these actions; without it, turns and jumps from devices SHALL be unavailable (the ESC/back pause is preserved), with no console errors.

#### Scenario: Turn toggles the direction

- **WHEN** the ball is controllable (Playing, on the ground) and the player taps the screen, clicks LMB, presses Enter, the gamepad West button, or the joystick trigger
- **THEN** the ball's direction toggles between +Z and +X, as with the previous tap

#### Scenario: Holding Turn does not repeat the turn

- **WHEN** the player holds a finger on the screen, holds LMB, or holds the West button for several seconds (many frames)
- **THEN** the direction toggles exactly once - at the press moment; a repeated turn happens only after releasing and pressing again

#### Scenario: Move deviation turns

- **WHEN** the ball is controllable and the player deviates any WASD key, arrow, or the gamepad left stick from neutral
- **THEN** the ball's direction toggles between +Z and +X

#### Scenario: Holding Move does not repeat the turn

- **WHEN** the player holds a WASD key/arrow or the stick deviated for several seconds
- **THEN** the direction toggles exactly once - at the deviation moment; a repeated turn only after the `Move` value returns to neutral and deviates again

#### Scenario: Previous and Next turn

- **WHEN** the ball is controllable and the player presses key `1` (or d-pad left) or key `2` (or d-pad right)
- **THEN** the ball's direction toggles exactly once per press for each action

#### Scenario: Turns suppressed in the air and while falling

- **WHEN** the ball is in a jump or falling, and the player uses `Turn`, `Move`, `Previous` or `Next`
- **THEN** the direction does not change: turn input is processed by the same suppression rules as the previous tap

#### Scenario: The game works without a connected input asset

- **WHEN** the `InputSystem_Actions` asset is not assigned in the scene
- **THEN** turns and jumps from any devices are unavailable, the ESC pause (and back on Android) works as before, with no console errors

### Requirement: Pointer input exclusivity with UI buttons

A `Turn` or `Jump` press from a pointer source (touch, LMB, RMB) located over an interactive UI button (`Selectable`) SHALL NOT produce a gameplay event: the button handles the press itself. Gamepad and keyboard sources (the West/South buttons, d-pad, Space, `1`, `2`, `Move`) SHALL produce events regardless of the pointer position: they have no screen position.

#### Scenario: A tap on a HUD button does not turn

- **WHEN** the player taps or clicks LMB on an interactive UI button (for example, Play Again on the Game Over screen)
- **THEN** no turn event is created, the button handles the click

#### Scenario: RMB on a HUD button does not jump

- **WHEN** the player clicks RMB over an interactive UI button during gameplay
- **THEN** no jump event is created

#### Scenario: Keyboard and gamepad do not depend on the pointer

- **WHEN** the mouse cursor is over a UI button and the player presses Space, the South or West button, the d-pad, `1`, `2`, or deviates `Move`
- **THEN** the gameplay event is created regardless of the cursor position

### Requirement: Pause via the Pause action

Along with the HUD Pause button, the `Pause` action of the `Player` map of the `InputSystem_Actions` asset (the `P` key, the ESC key, the gamepad Start button) SHALL pause and unpause the session with the same mechanism: the same toggle event consumed by the game flow system is emitted (Playing → Paused, Paused → Playing without a session reset). Manual ESC polling for pause SHALL be removed: ESC comes through the `Pause` action binding, and each physical press SHALL produce exactly one toggle. The `Pause` action SHALL be handled in the Playing and Paused states; in Menu, Falling and GameOver the toggle SHALL be ignored by the consumer, and on an Android build ESC in Menu SHALL still close the application (the back gesture). One press SHALL produce exactly one toggle; holding the key or button SHALL NOT toggle the pause repeatedly.

#### Scenario: ESC pauses and unpauses

- **WHEN** the session is in the Playing state and the player presses ESC; then the session is in Paused and the player presses ESC again
- **THEN** the session transitions to Paused, then back to Playing - exactly one toggle per press (a double toggle from polling and the action is excluded)

#### Scenario: P pauses and unpauses

- **WHEN** the session is in the Playing state and the player presses the `P` key; then the session is in Paused and the player presses `P` again
- **THEN** the session transitions to Paused, then back to Playing - the same mechanism as ESC

#### Scenario: Start on the gamepad pauses

- **WHEN** the session is in the Playing state and the player presses the gamepad Start button
- **THEN** the session transitions to Paused, as with the HUD Pause button

#### Scenario: Holding Pause does not re-toggle

- **WHEN** the player holds ESC, `P` or Start for several seconds
- **THEN** the toggle event is created exactly once - in the press frame; a repeated toggle only after releasing and pressing again

#### Scenario: Pause outside gameplay is ignored

- **WHEN** the session is in the Menu, Falling or GameOver state, and the player presses ESC, `P` or Start
- **THEN** the session state does not change; on an Android build ESC in Menu still closes the application (the back gesture)

## MODIFIED Requirements

### Requirement: Jump input

The ball SHALL jump on the `Jump` action of the `Player` map of the `InputSystem_Actions` asset (the Space key, the gamepad South button, the right mouse button) or the on-screen "Jump" button (touch and any platforms). The previous "jump on the right mouse button via manual polling" path SHALL be removed: RMB comes through the `Jump` action binding. The on-screen "Jump" button press and the `Jump` action SHALL NOT count as a raw tap (SHALL NOT toggle the direction). Jump input SHALL be ignored while the session is not in the Playing state. One press of the `Jump` action SHALL produce exactly one jump input; holding the key or button SHALL NOT repeat the jump in subsequent frames.

#### Scenario: The right mouse button jumps

- **WHEN** the player presses the right mouse button during gameplay
- **THEN** the ball starts a jump (the event comes through the `Jump` action)

#### Scenario: Space jumps

- **WHEN** the player presses the Space key during gameplay
- **THEN** the ball starts a jump, the direction is not toggled

#### Scenario: The gamepad South button jumps

- **WHEN** the player presses the gamepad's bottom face button (South) during gameplay
- **THEN** the ball starts a jump, the direction is not toggled

#### Scenario: Holding Jump does not repeat the jump

- **WHEN** the player holds Space, South or RMB for several seconds
- **THEN** the jump input is created exactly once - in the press frame; the next jump is possible only after releasing and pressing again

#### Scenario: Jump outside gameplay is ignored

- **WHEN** the session is in the Menu, Paused or GameOver state, and the player presses Space, South or RMB
- **THEN** no jump starts (state validation stays in the event consumer)

#### Scenario: The on-screen Jump button jumps

- **WHEN** the player presses the on-screen "Jump" button during gameplay
- **THEN** the ball starts a jump, and the press does not toggle the direction

#### Scenario: The Jump button press is not a raw tap

- **WHEN** the player presses the on-screen "Jump" button
- **THEN** no raw tap event is created
