# game-flow Specification

## Purpose
Screens and the session loop: mode selection with a preview track, start via the Play button, gameplay with a HUD, pause, Game Over with results, restart of the same mode, and return to the menu.

## Requirements

### Requirement: Mode selection screen

On game start, the menu SHALL open with mode selection: Easy (width 3), Normal (width 2), Hard (width 1). The menu SHALL contain three mode buttons and a separate Play button. Normal SHALL be selected by default. Mode selection (the Easy/Normal/Hard buttons) SHALL only switch the selected mode and regenerate the menu preview track - no game session is started. The menu preview track SHALL have a ball on it (on the track's first block); when the preview track is regenerated, the ball SHALL be moved to the first block of the new track. The Play button SHALL start a game session on the current preview track without regenerating it: the existing menu ball SHALL start rolling immediately, without waiting for the first tap. The selected mode button SHALL be visually highlighted.

#### Scenario: Menu at app start

- **WHEN** the application starts
- **THEN** the menu is shown with mode buttons and Play, the Normal mode is selected, a preview track of Normal width is built, and a ball stands on the track's first block

#### Scenario: Selecting Easy

- **WHEN** the player presses Easy in the menu
- **THEN** the selected mode becomes Easy (no session starts), the preview track is regenerated with 3 parts, and the ball is moved to the first block of the new track

#### Scenario: Selecting Normal

- **WHEN** the player presses Normal in the menu
- **THEN** the selected mode becomes Normal (no session starts), the preview track is regenerated with 2 parts, and the ball is moved to the first block of the new track

#### Scenario: Selecting Hard

- **WHEN** the player presses Hard in the menu
- **THEN** the selected mode becomes Hard (no session starts), the preview track is regenerated with 1 part, and the ball is moved to the first block of the new track

#### Scenario: Re-selecting the same mode

- **WHEN** the player presses the button of the already selected mode in the menu
- **THEN** the mode does not change and the preview track is not regenerated

#### Scenario: Selected mode highlight

- **WHEN** the player selects one of the modes in the menu
- **THEN** the selected mode's button is visually highlighted, the other modes' buttons are in the normal state

#### Scenario: Play starts a session without track regeneration

- **WHEN** the player selects a mode in the menu and presses Play
- **THEN** a session of the selected mode starts on the already built preview track (the track is not reset and not regenerated), and the ball starts rolling immediately without waiting for the first tap

#### Scenario: Play without a prior mode selection

- **WHEN** the player presses Play in the menu right after the app starts
- **THEN** a session of the Normal mode (the default) starts on the already built preview track, and the ball starts rolling immediately

#### Scenario: Track continues after Play

- **WHEN** a session was started with the Play button and the ball moves along the track
- **THEN** the track keeps being extended from the last block without resetting the generator cursor

#### Scenario: Tap from the Play button does not turn the ball

- **WHEN** the player presses Play in the menu
- **THEN** the input produced by this click does not turn the ball; the first tap after the session starts switches the direction

### Requirement: HUD during gameplay

During a game session the HUD SHALL display the current score (as `Score: N`), the selected mode's high score (as `High Score: N`) and the current level; the HUD SHALL be hidden outside a session (in the menu and on the results screen only the text belonging to them is visible). The displayed high score SHALL be fixed at session start and SHALL not be updated during the session when the current score exceeds it.

#### Scenario: Score on the HUD

- **WHEN** the ball passes another block
- **THEN** the score value on the HUD increases by 1

#### Scenario: Score format on the HUD

- **WHEN** the HUD displays the current score
- **THEN** the score is shown as `Score: N` (for example, `Score: 123`)

#### Scenario: Level on the HUD

- **WHEN** the score crosses the next 300 threshold
- **THEN** the level number on the HUD increases by 1

#### Scenario: High score on the HUD

- **WHEN** a session starts in the selected mode
- **THEN** the HUD shows that mode's high score as `High Score: N`, and this value does not change until the end of the session, even if the current score exceeds it

#### Scenario: HUD hidden in the menu

- **WHEN** the game is in the menu or on the results screen
- **THEN** the game HUD is hidden

### Requirement: Game Over and the results screen

When the ball starts falling, the game SHALL end the session and, after a short delay, show the results screen: session score, high score, a "new record" marker when it was updated, and the Play Again and "Menu" buttons. Taps made in the Falling state (before the results screen appears) SHALL be ignored: they SHALL not accumulate and SHALL not trigger a restart the moment the results screen appears. A restart SHALL only work via the Play Again button once the results screen is shown (the GameOver state).

#### Scenario: Results screen

- **WHEN** the ball falls off the track and is falling
- **THEN** after a short delay (about 1 second) the results screen appears with the session score, the high score, and the Play Again and "Menu" buttons

#### Scenario: New record marker

- **WHEN** the session score exceeded the stored high score
- **THEN** the results screen has a visible new-record marker

#### Scenario: Tap while falling is ignored

- **WHEN** the ball is falling (the Falling state) and the player taps/clicks the play field
- **THEN** the tap has no gameplay effect and is not remembered: the results screen appears after the delay and stays open

#### Scenario: The results screen is not skipped by accumulated taps

- **WHEN** taps were made while falling and the delay has expired (the state became GameOver)
- **THEN** the results screen is shown and stays open; no restart happens until the player presses Play Again after the screen appeared

#### Scenario: Restart after the screen appears

- **WHEN** the results screen is open (the GameOver state) and the player presses the Play Again button
- **THEN** the session restarts in the same mode

### Requirement: Restart and exit to menu

On the results screen, a session restart in the same mode SHALL only happen via the Play Again button (PlayAgainButton); a tap/click anywhere else on the screen SHALL NOT restart the session. A separate "Menu" button SHALL return to the mode selection screen. A full session reset (restart or exit to menu) SHALL also lift the pause if the session was paused.

#### Scenario: Restart via the Play Again button

- **WHEN** the results screen is open (the GameOver state) and the player presses the Play Again button
- **THEN** the current session is fully reset and a new session of the same mode starts

#### Scenario: Tap on empty space does not restart

- **WHEN** the results screen is open (the GameOver state) and the player taps/clicks outside the results screen's buttons
- **THEN** the session is not restarted; the results screen stays open

#### Scenario: Restart of the same mode

- **WHEN** the results screen is open and the player presses the Play Again button
- **THEN** the current session is fully reset and a new session of the same mode starts

#### Scenario: Return to menu

- **WHEN** on the results screen the player presses the "Menu" button
- **THEN** the session is reset and the mode selection screen opens

#### Scenario: Full state reset

- **WHEN** a restart or exit to menu happens
- **THEN** the track, ball, score, level and effects are reset to a new session's state, and the pause is not preserved

#### Scenario: Exit to menu from pause

- **WHEN** the session is paused and the player presses Exit to Menu on the pause screen
- **THEN** the session is fully reset and the mode selection screen opens

### Requirement: Pause via ESC

During a game session (the Playing state), pressing the ESC key SHALL pause the session: the session transitions to the Paused state. Pressing ESC again while paused SHALL lift the pause and return the session to Playing.

#### Scenario: ESC pauses

- **WHEN** a session is running (Playing) and the player presses ESC
- **THEN** the session transitions to the Paused state and the pause screen appears

#### Scenario: ESC unpauses

- **WHEN** the session is paused and the player presses ESC
- **THEN** the pause is lifted, the session returns to Playing, the pause screen is hidden and the game continues from the same place

### Requirement: Pause via the HUD button

The HUD SHALL have a pause button (PauseButton) emulating the ESC key: during a game session (the Playing state), pressing the button SHALL pause the session with the same mechanism as ESC (toggling Playing <-> Paused). The button SHALL be part of the HUD and therefore visible only while the HUD is visible; unpausing stays on Continue and ESC. Pressing the button SHALL NOT count as a gameplay tap (it does not turn the ball, does not restart the session).

#### Scenario: Button pauses

- **WHEN** a session is running (Playing) and the player presses the HUD pause button
- **THEN** the session transitions to the Paused state and the pause screen appears, as with ESC

#### Scenario: Button not visible while paused

- **WHEN** the session is paused
- **THEN** the HUD (together with the pause button) is hidden; the pause can be lifted only via Continue or ESC

#### Scenario: Button outside Playing does not pause

- **WHEN** the session is in the Menu, Falling or GameOver state
- **THEN** the pause button is unavailable (the HUD is hidden or the press has no effect), and the session state does not change

#### Scenario: Pressing is not a gameplay tap

- **WHEN** the player presses the HUD pause button
- **THEN** the press does not turn the ball and does not restart the session; only the pause is toggled

### Requirement: App exit via the Android back gesture in the menu (Android)

On an Android build, the back swipe/button (delivered as ESC) SHALL close the application when the game is in the main menu (the Menu state). In the Playing and Paused states, "back" SHALL still toggle the pause (the existing ESC behavior); in the Falling and GameOver states, "back" SHALL be ignored. On platforms other than Android (desktop, editor), ESC SHALL NOT close the application.

#### Scenario: Back in the menu closes the app

- **WHEN** the game is in the main menu (the Menu state) on an Android build and the player performs the "back" swipe
- **THEN** the application exits

#### Scenario: Back in gameplay pauses as before

- **WHEN** a session is running (the Playing state) on an Android build and the player performs the "back" swipe
- **THEN** the session transitions to the Paused state (the pause screen appears), the application does not exit

#### Scenario: Back while paused unpauses as before

- **WHEN** the session is paused (the Paused state) on an Android build and the player performs the "back" swipe
- **THEN** the pause is lifted, the session returns to Playing, the application does not exit

#### Scenario: Back while falling and on the results screen is ignored

- **WHEN** the session is in the Falling or GameOver state on an Android build and the player performs the "back" swipe
- **THEN** the session state does not change, the application does not exit

#### Scenario: ESC on desktop does not close the app

- **WHEN** the game is in the main menu (the Menu state) on desktop or in the editor and the player presses ESC
- **THEN** the application keeps running, the session state does not change

### Requirement: Simulation freeze while paused

While the session is paused, the game SHALL not advance the simulation: the ball does not move or turn, track generation is not extended, block falling (timers and animation) stops, crystals do not spin and are not picked up, no points or levels are awarded. The world state SHALL be preserved exactly at the pause moment.

#### Scenario: Ball freezes

- **WHEN** the player pauses while the ball is moving
- **THEN** the ball stops at its current position and does not move until the pause is lifted

#### Scenario: Block falling freezes

- **WHEN** the pause is applied while blocks are falling
- **THEN** the fall animation and delay timers stop and resume only after the pause is lifted

#### Scenario: Taps while paused are ignored

- **WHEN** the session is paused and the player taps/clicks the play field
- **THEN** the ball's direction does not change, no restart is triggered, the tap has no gameplay effect

### Requirement: Pause screen

On entering pause, a pause screen with Continue and Exit to Menu buttons SHALL be shown. The Continue button SHALL lift the pause (transition to Playing without a session reset). The Exit to Menu button SHALL abort the session and return the game to the menu (a full reset, as when exiting to the menu from the results screen). The pause screen SHALL be hidden on unpause and on exit to the menu.

#### Scenario: Continue

- **WHEN** the session is paused and the player presses Continue
- **THEN** the pause is lifted and the session continues from the same place without resetting the score, track and ball

### Requirement: Pause does not distort session completion

The pause SHALL only be applied in the Playing state. From the Falling, GameOver and Menu states, ESC SHALL not pause. A full session reset (restart after Game Over or exit to menu) SHALL lift the pause / not leave the session in the Paused state.

#### Scenario: ESC outside Playing does not pause

- **WHEN** the session is in the Falling, GameOver or Menu state and the player presses ESC
- **THEN** the session state does not change, the pause screen does not appear

#### Scenario: Restart resets the pause

- **WHEN** the session is paused and a full reset starts (restart or exit to menu)
- **THEN** the new session starts in the Playing state without a pause screen

### Requirement: Isometric camera

The camera SHALL be orthographic and SHALL use a fixed isometric framing taken from the current scene camera transform at startup: the scene camera's position and rotation are the single source of the framing, with no duplication in the config. In the menu the camera SHALL stand in the session-start framing: the focus is the ball spawn point (the first block of the preview track), the position = focus + the original offset, the rotation the original one; when the mode changes in the menu, the focus SHALL follow the regenerated preview track. During gameplay the camera SHALL follow the ball with the same offset and rotation. Movement SHALL be smoothed (lerp with the speed from the config). When the ball entity does not exist in an active session (the ball fell and was destroyed), the camera SHALL hold its current position and framing until a restart or exit to the menu.

#### Scenario: Menu framing

- **WHEN** the game is in the menu
- **THEN** the camera stands at the position equal to the framing right after pressing Play: focus = the first block of the preview track (the ball spawn point), position = focus + the original offset (currently (-10, 10, -10)), rotation the original one (currently (30, 45, 0))

#### Scenario: Camera follows the ball with a fixed framing

- **WHEN** the ball moves along the track during a session
- **THEN** the camera smoothly catches up to the point "ball + (original camera position − menu focus)" (currently "ball + (-10, 10, -10)"), keeping the original camera rotation (currently (30, 45, 0)) unchanged

#### Scenario: Camera rotation does not depend on the track

- **WHEN** the ball changes direction (+Z / +X) or the track turns
- **THEN** the camera does not rotate after the ball and does not change its tilt: the offset and rotation are constant

#### Scenario: Mode change moves the menu framing

- **WHEN** the player selects another mode in the menu (the preview track is regenerated)
- **THEN** the camera smoothly (lerp) moves to the start framing of the new preview track: focus = the first block of the new track, the offset and rotation unchanged; pressing Play afterwards does not move the camera

#### Scenario: Camera while paused

- **WHEN** the session is paused and unpaused
- **THEN** the camera framing does not change: the ball stays at the same screen point, the offset and rotation unchanged

#### Scenario: Camera holds position after a fall

- **WHEN** the ball entity is destroyed on falling (the session is in the Falling or Game Over state)
- **THEN** the camera stays at its current position and does not move to origin or to the menu start position until the session is restarted or the player exits to the menu

#### Scenario: Camera on restart after a fall

- **WHEN** on the results screen the player restarts the session with the Play Again button
- **THEN** the camera smoothly follows the new ball with the standard lerp, without teleporting
