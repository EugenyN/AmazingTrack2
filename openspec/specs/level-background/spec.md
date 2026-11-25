# level-background Specification

## Purpose

Background visuals driven by progression: the sky renders as a vertical
gradient whose colors are derived from the current level and glide to the
new scheme on every level change, with the level-1 scheme in the menu and
after a session reset.

## Requirements

### Requirement: Gradient background
The game background SHALL be a vertical two-color gradient (a darker zenith
above a lighter horizon) rendered as the skybox. The gradient colors SHALL
be derived from the current level: hue = frac(level * 0.618034), the zenith
darker and more saturated, the horizon lighter and less saturated, with
saturation and value taken from configuration.

#### Scenario: Level 1 colors
- **WHEN** a session starts and the level is 1
- **THEN** the background shows the level-1 gradient (hue frac(0.618034))
  with zenith darker/more saturated than the horizon per configured
  saturation and value

### Requirement: Scene-wired gradient material
The gradient material SHALL live in the scene's Lighting settings
(`RenderSettings.skybox`) as the single source of truth, wired in the
Editor. The background system SHALL only animate that material's colors,
without creating materials or mutating scene state at runtime.

#### Scenario: Non-gradient skybox is rejected
- **WHEN** the scene's skybox material does not provide the gradient color
  properties at session start
- **THEN** the system logs an error naming the missing setup, the scene
  keeps its own skybox, and no colors are animated

### Requirement: Smooth transition on level change
When the current level value changes, the background gradient colors SHALL
transition smoothly to the new level's colors over a configured duration of
about one second, without flashing or pulsing. The transition SHALL be a
simple interpolation between the previous colors and the target colors.

#### Scenario: Level up glide
- **WHEN** the score crosses a level threshold and the level value increases
- **THEN** the background gradient glides to the new level's colors over
  the configured duration (no abrupt jump, no flash)

#### Scenario: Multiple level changes in quick succession
- **WHEN** the level changes again before a previous transition finishes
- **THEN** the background continues gliding from its current colors toward
  the newest level's colors without snapping

### Requirement: Level 1 scheme in menu and after reset
The menu state SHALL show the level-1 gradient. A session reset (restart or
return to menu) resets the level to 1, and the background SHALL therefore
return to the level-1 scheme through the same smooth transition.

#### Scenario: Menu shows level-1 scheme
- **WHEN** the game is on the mode selection menu
- **THEN** the background shows the level-1 gradient

#### Scenario: Restart returns to level 1 colors
- **WHEN** the player restarts a session or returns to the menu from
  game over
- **THEN** the background glides back to the level-1 scheme

### Requirement: Idle breathing
While the session is not paused, the background SHALL breathe: a slow,
subtle oscillation of its hue - a small hue shift around the target
scheme's colors, with amplitude and period taken from configuration - so
the background is never perfectly static. The breathing SHALL compose
with level-change transitions and with the defeat scheme without
flashing. While paused, the breathing SHALL freeze together with the
colors.

#### Scenario: Breathing is visible over a few seconds
- **WHEN** a session is playing and no level change occurs
- **THEN** the background hue slowly oscillates within a small configured
  amplitude around the level scheme and never stays perfectly constant

#### Scenario: Pause freezes the breathing
- **WHEN** the session is paused
- **THEN** the background hue and colors stay exactly as they were at the
  pause moment until the session resumes

### Requirement: Red-black background on defeat
When the session enters the Falling or GameOver state, the background
SHALL transition through the same smooth glide to a red-black defeat
scheme taken from configuration: a near-black red zenith above a dark red
horizon. On session restart or return to the menu the background SHALL
transition back to the level scheme through the same glide.

#### Scenario: Ball falls -> background turns red-black
- **WHEN** the ball loses ground and the session enters Falling
- **THEN** the background glides to the configured red-black scheme over
  the configured transition duration, without a flash

#### Scenario: Restart returns from red-black
- **WHEN** the player restarts the session or returns to the menu after a
  defeat
- **THEN** the background glides back to the current level scheme

### Requirement: Paused game keeps the background steady
While the session is paused, the background SHALL remain unchanged: no
transition, no drift.

#### Scenario: Pause freezes the background
- **WHEN** the session is paused mid-transition or mid-game
- **THEN** the background stays exactly as it was until the session resumes
