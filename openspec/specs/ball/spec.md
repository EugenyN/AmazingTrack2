# ball Specification

## Purpose

Ball movement along the track and direction control: a tap toggles "forward ↔ sideways", where "sideways" is toward the track's nearest turn; the ball falls when its center leaves the track. Input comes via the actions of the Player map of the InputSystem_Actions asset: Turn/Move/Previous/Next - turn (touch, mouse, keyboard, gamepad), Jump - jump, Pause - pause. Jump: a kinematic arc of fixed length along the current direction, input and ground-check suppression in the air, return to the normal pipeline on landing.

## Requirements

### Requirement: Smooth movement along the track

The ball SHALL move along the track smoothly (continuously, not cell by cell) at a constant speed equal to the base speed plus the current level's bonus. The ball visually rolls (rotates around the axis perpendicular to the movement).

#### Scenario: Constant speed

- **WHEN** the ball rolls along the track and the level does not change
- **THEN** the ball's speed magnitude is constant and equal to the base speed plus the level bonus

#### Scenario: Visual rolling

- **WHEN** the ball moves
- **THEN** the ball rotates so that its rolling is consistent with the movement direction and speed

### Requirement: Fall when the center leaves the track

The ball SHALL start falling when a physics SphereCast downward from the ball's center (over the blocks layer) finds no block under the center. The fall SHALL be physical (gravity, tumbling), track generation SHALL stop, input SHALL be ignored. On starting the fall, a moving ball SHALL receive a horizontal speed equal to a fixed value from the configuration (independent of the current track speed): the ball's forward flight length SHALL be the same at all levels, including under equal fall conditions; a ball standing before the first tap SHALL start falling without horizontal speed. The vertical acceleration of a falling ball SHALL be set by the gravity multiplier from the configuration (applied to standard gravity, the multiplier defaults to 1): a multiplier greater than 1 makes the fall arc steeper and reduces the forward drift. The transition from controlled movement to physics SHALL be visually continuous: in the frame physics takes over, the view position of the ball SHALL not snap back and SHALL not freeze for even one frame (the dead frame between the logical stop and the physics start is covered by extrapolating the view position one frame ahead, using the same fixed horizontal speed).

#### Scenario: Leaving the edge

- **WHEN** the ball's center leaves the track edge and the SphereCast from the ball's center finds no block
- **THEN** the ball transitions to the falling state with physics and is no longer controlled

#### Scenario: Control disabled while falling

- **WHEN** the ball is falling and the player taps
- **THEN** the movement direction does not change, the input is ignored

#### Scenario: Fixed horizontal speed on losing ground

- **WHEN** a moving ball loses ground and is handed to physics
- **THEN** its physics body receives a horizontal speed equal to the fixed value from the configuration, and the fall continues along an arc rather than straight down

#### Scenario: Inertia preserved on losing ground

- **WHEN** a moving ball loses ground and is handed to physics
- **THEN** the physics body starts with a non-zero horizontal speed (keeps the movement inertia) and the fall continues along an arc in the movement direction, rather than straight down

#### Scenario: Forward flight does not grow with track speed

- **WHEN** two ground losses happen at the same track point at different levels (different track speeds)
- **THEN** the ball's horizontal flight distance on falling is the same in both cases: the forward flight length does not depend on the level or the ball's speed

#### Scenario: A standing ball losing ground

- **WHEN** a ball standing before the first tap loses ground
- **THEN** its physics body receives no horizontal speed, the fall is straight down

#### Scenario: Continuity of the physics handover frame

- **WHEN** the ball transitions from controlled movement to physical falling
- **THEN** in the handover frame the view position of the ball moves forward (one-frame extrapolation) without snapping back and without a freeze frame, and the physics body starts from that same visible point

#### Scenario: The gravity multiplier tunes the fall steepness

- **WHEN** the fall gravity multiplier in the configuration is greater than 1
- **THEN** the falling ball descends faster than under standard gravity (a steeper arc, less forward drift)

#### Scenario: The default multiplier does not change behavior

- **WHEN** the fall gravity multiplier is 1 (the default value)
- **THEN** the fall happens under standard gravity, as before the setting was introduced

### Requirement: The ball's rim may overhang the edge without falling

While the projection of the ball's center is over a block, the ball SHALL stay on the track even if its rim overhangs the block's edge. A block SHALL be considered under the ball's center only when the projection of the ball's center lies within the block's footprint: physics query candidates whose footprint does not contain the projection of the ball's center SHALL not be considered ground. While the current ground is alive, not falling, and its footprint contains the projection of the ball's center, the ground SHALL stay the same (hysteresis): neighboring blocks SHALL not take over the ground role while the ball's center is over the current block.

#### Scenario: Rim over a chasm

- **WHEN** part of the ball overhangs the block's edge but the center is still over the block
- **THEN** the ball continues controlled movement and does not fall

#### Scenario: A neighboring block does not steal ground

- **WHEN** the physics query sphere touches a neighboring block whose footprint does not contain the projection of the ball's center, while the current block's footprint does
- **THEN** the ground stays the current block and no block is marked as falling

#### Scenario: Crossing a seam without a false fall

- **WHEN** the ball's center crosses a seam and in some frame the physics query finds no valid candidate (the center on the boundary of footprints)
- **THEN** the ground stays the same in that frame (hysteresis), the ball does not start falling, and when the center enters the next block's footprint the ground switches to it

### Requirement: Movement directions: fixed world axes

The ball SHALL move only along one of two fixed world directions: +Z or +X. A tap/click anywhere on the screen SHALL toggle the direction between +Z and +X, starting from the session's very first tap: the ball SHALL start moving right at session start (on pressing Play) along the starting segment's axis (+Z); no separate "starting" tap exists. The direction SHALL be stored as a world vector, independent of the block under the ball; when crossing block seams, movement SHALL be seamless. A tap/click SHALL be ignored while the ball is not controllable: falling or in a jump (in the air). A ball standing in the menu on the preview track SHALL not move and SHALL not react to taps (taps in the menu are discarded). One physical tap (a finger or mouse button press) SHALL produce exactly one direction toggle: holding the finger on the screen after the press SHALL NOT repeat the toggle in subsequent frames; the next toggle happens only after releasing and pressing again.

#### Scenario: Toggling between fixed axes

- **WHEN** the ball moves along one of the axes (+Z or +X) and the player taps/clicks
- **THEN** the ball's direction toggles to the other axis

#### Scenario: Session start without a starting tap

- **WHEN** a session starts via the Play button and the ball is spawned
- **THEN** the ball immediately moves along +Z (the starting segment's axis), the camera and HUD work

#### Scenario: The first tap toggles the direction

- **WHEN** the player makes the session's first tap/click while the ball is moving
- **THEN** the ball's direction toggles to the other axis (+X); no separate "launching" tap exists

#### Scenario: Holding the finger does not repeat the toggle

- **WHEN** the player puts a finger on the screen and holds it still for several seconds (many frames)
- **THEN** the direction toggles exactly once - in the press frame - and stays unchanged until the finger is released; after releasing and pressing again the direction toggles again exactly once

#### Scenario: Seamless seam

- **WHEN** the ball crosses a seam of blocks of any orientation
- **THEN** movement continues without a vector change and without an instant turn

#### Scenario: Tap in a jump is ignored

- **WHEN** the ball is in a jump (in the air) and the player taps/clicks
- **THEN** the direction does not change: as when falling, the input is ignored while the ball is not controllable

#### Scenario: Standing until the first tap

- **WHEN** the game is in the menu and the ball is spawned on the preview track
- **THEN** the ball is motionless on the spawn block, the camera and HUD work; in a session (Playing) there is no first-tap wait anymore

#### Scenario: The first tap launches along the starting axis

- **WHEN** the player makes the session's first tap/click
- **THEN** the ball is already moving (launched by the Play button), the tap toggles the direction from +Z to +X

#### Scenario: The ball in the menu is motionless

- **WHEN** the game is in the Menu state and the ball stands on the preview track
- **THEN** the ball does not move, taps neither turn it nor launch it

### Requirement: Falling into a hole

The ball SHALL start falling when the horizontal projection of its center is over a hole, following the same fall-state transition rules as when leaving the track edge. The hole cell SHALL be excluded from the host block's footprint when determining ground: neither the direct query (SphereCast) nor the current-ground hysteresis SHALL keep the block as ground while the ball's center is over the hole cell. The hole cell SHALL have the same rim tolerance (seam tolerance) as the track edge: while the ball's center remains on the solid part (within the tolerance to the hole cell's boundary), the ball does not fall; when the center crosses the hole cell's boundary beyond the tolerance, the ground is cleared and the ball starts falling via the common "no block under the center" path.

#### Scenario: Center over a hole

- **WHEN** the ball's center passes over a hole
- **THEN** the ball starts a physical fall, as when leaving the track

#### Scenario: Rim over a hole

- **WHEN** only part of the ball overhangs a hole but the center remains over the block's solid part
- **THEN** the ball does not fall and continues controlled movement

#### Scenario: Entering a hole from the same block's solid part

- **WHEN** the ball is already standing on the holed block as ground and reaches the hole cell (moving along the part-laying axis), and the ball's center crosses the hole cell's boundary beyond the rim tolerance
- **THEN** the hysteresis stops holding this block as ground, the ground is cleared and the ball starts a physical fall in the same frame

#### Scenario: The query sphere touches the solid part over a hole

- **WHEN** the physics query sphere touches the solid part of a holed block (or a neighboring block) while the ball's center is over the hole cell and outside its footprint tolerance
- **THEN** such a candidate is not considered ground (the footprint filter rejects it), and the ball starts falling

#### Scenario: Holes do not change hole-free block behavior

- **WHEN** a block has no hole
- **THEN** ground determination, hysteresis and the leave-block check work as before, without false falls at seams

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

#### Scenario: One physical fast tap on mobile web produces one event

- **WHEN** the Web build runs in a touch-capable browser and the player makes a fast tap, which the browser delivers as the native touchscreen press and then as a synthesized mouse press+release batched into a later frame
- **THEN** the batched press produces no gameplay event (when polled it has no active control): exactly one turn per physical tap; a long press delivers a single touchscreen press and also produces exactly one turn

#### Scenario: Desktop web browsers keep mouse input

- **WHEN** the Web build runs in a browser without a touchscreen device and the player clicks LMB
- **THEN** the direction toggles as before: no synthetic-press suppression without a touchscreen

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

### Requirement: Jump arc

During gameplay and on the ground (not falling, not in a jump), a jump SHALL move the ball forward by exactly the configured jump distance (2 world units) along the current direction, raising the ball to the configured height at the middle of the flight. The arc SHALL be purely kinematic (visual height only); the ball's Rigidbody SHALL NOT participate.

#### Scenario: A jump covers exactly 2 units

- **WHEN** the ball jumps while moving along an axis
- **THEN** the horizontal displacement between takeoff and landing equals the configured jump distance (2 units) along the direction the ball had at takeoff

#### Scenario: Smooth arc

- **WHEN** the ball jumps
- **THEN** its visible height rises from the track surface to the configured peak at the middle of the flight and returns to the surface on landing, with no jumps or freeze frames in the view position

### Requirement: Flight length is level-independent

The horizontal flight speed SHALL equal the ball's current track speed (the in-plane speed does not change during a jump): the flight duration adapts (duration = distance / track speed), so the flight length is the same at every level, and the jump is faster at higher speeds.

#### Scenario: Flight length is the same at different levels

- **WHEN** jumps are made at different levels (different track speeds) from equal conditions
- **THEN** the horizontal flight length is the same in both cases

#### Scenario: Higher speed - faster jump

- **WHEN** jumps are made at a higher level (higher track speed)
- **THEN** the flight duration is shorter, and the covered distance stays the same

### Requirement: Input and ground suppression in the air

While the ball is in the air (jumping), direction-change input SHALL be ignored (the direction stays as at takeoff), the ground block SHALL stay the takeoff block (no new ground is acquired and none is lost), and the fall check for missing ground SHALL NOT run. After landing, the normal pipeline SHALL resume in the same frame: ground tracking, the fall check, movement point scoring, and direction input work again; a ball landing without a valid block under the center SHALL start a normal fall, continuing the jump arc's momentum (the arc's horizontal and descending speed are handed to the physical fall without a kink).

#### Scenario: Tap in flight is ignored

- **WHEN** the ball is in the air and the player taps/clicks
- **THEN** the direction does not change

#### Scenario: A jump in flight is ignored

- **WHEN** the ball is in the air and the player presses jump input again
- **THEN** no second jump starts

#### Scenario: No false fall at takeoff

- **WHEN** the ball takes off (for example, over a hole)
- **THEN** the missing-ground fall check does not trigger while the ball is in the air, even if the takeoff block is already marked falling or there is a hole under the block

#### Scenario: Landing on the track resumes the game

- **WHEN** the ball lands with a valid block under the center
- **THEN** ground tracking and direction input resume in the landing frame, movement continues along the takeoff direction

#### Scenario: Landing off the track - a fall

- **WHEN** the ball lands without a valid block under the center (for example, the track turned away during the flight)
- **THEN** in the landing frame the normal missing-ground fall starts, continuing the arc's descending part without a bounce or kink

#### Scenario: The takeoff block falls in trail

- **WHEN** the ball lands away from the block it took off from
- **THEN** the takeoff block is marked falling by the existing leave-ground logic (the whole tail together with it), as if the ball had rolled off it

### Requirement: The on-screen Jump button

The HUD SHALL show the on-screen "Jump" button in the Playing and Falling states on mobile platforms (`Application.isMobilePlatform`), in the editor (`Application.isEditor`), and on Web builds running in a touch-capable browser (the Input System's Touchscreen device is created there at startup, before the first touch); on builds without a touchscreen the button SHALL be hidden. The button SHALL have an English caption ("Jump") and be bound to the jump input event. The jump SHALL trigger on press (pointer down); one press SHALL produce exactly one jump input. Pressing the button SHALL NOT count as a raw tap.

#### Scenario: The button is visible during gameplay

- **WHEN** a session is in the Playing state on a mobile device, in the editor, or on the Web in a touch-capable browser
- **THEN** the "Jump" button is visible on the HUD

#### Scenario: The button is hidden outside gameplay

- **WHEN** a session is in the Menu, GameOver or Paused state
- **THEN** the "Jump" button is not visible

#### Scenario: The button is hidden on builds without a touchscreen

- **WHEN** the application runs as a build without a touchscreen device (for example, a desktop build or a desktop browser)
- **THEN** the "Jump" button is not visible in any session state; the jump is available on the right mouse button

#### Scenario: The jump triggers on press

- **WHEN** the player puts a finger on the "Jump" button (pointer down) without releasing it
- **THEN** the ball starts the jump at the press moment, without waiting for release

#### Scenario: One press - one jump

- **WHEN** the player presses and releases the "Jump" button once
- **THEN** exactly one jump input is created (the release does not create a second one)

#### Scenario: The Jump button press is not a raw tap

- **WHEN** the player presses the "Jump" button
- **THEN** no raw tap event is created (the existing Selectable guard applies to jump input the same way as to taps)
