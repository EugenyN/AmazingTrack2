# Mechanics Tests Specification

## Purpose

Automated Play Mode verification of real gameplay: a seeded autopilot bot session that plays the actual GameScene through the real menu flow, survives a fixed duration with a minimum score and no errors, backed by test seams that make the run deterministic.

## Requirements

### Requirement: Seeded track generation test seam

The track generator SHALL accept an optional seed that makes its random decisions reproducible; without a seed the behavior SHALL be unchanged. A test hook SHALL force a seed before the game scene loads, applying it from the very start of the session scene (the menu preview track included, since Play reuses it). The hook SHALL be consumed on use.

#### Scenario: Seeded run builds the same track twice

- **WHEN** two sessions start with the same forced seed and the bot drives them identically
- **THEN** the generated track block sequence is the same in both sessions

#### Scenario: No seed keeps normal behavior

- **WHEN** a session starts without a forced seed (normal play or a test that did not set the hook)
- **THEN** track generation behaves exactly as before this change

#### Scenario: Forced seed applies before the menu preview

- **WHEN** a test forces a seed, then loads the game scene
- **THEN** the menu preview track is already generated with that seed, so pressing Play runs on the seeded track without regeneration

#### Scenario: Hook does not leak between sessions

- **WHEN** a test forces a seed and the scene starts, then a later scene load happens without forcing a seed
- **THEN** the later load does not use the earlier test's seed

### Requirement: World access seam for tests

The scene entry point SHALL expose the live ECS world and shared data to code outside the scene (tests) via a static scene-entry reference established at scene start and cleared at teardown, plus read-only accessors for the world and shared data. Gameplay systems SHALL NOT be modified for this seam.

#### Scenario: Test reaches the live world

- **WHEN** the game scene has loaded and initialized
- **THEN** a test can obtain the live ECS world and shared data through the static scene-entry reference

#### Scenario: Reference cleared at teardown

- **WHEN** the scene unloads or the entry object is destroyed
- **THEN** the static scene-entry reference is cleared (null), and a stale access is detectable

### Requirement: End-to-end gameplay bot run

A Play Mode test SHALL load the game scene asynchronously, start a session through the real menu flow, let an autopilot bot play for 60 seconds at normal time scale, and pass only if the session is still playing at the end and the score reached a configured minimum. The bot SHALL steer exclusively through the game's official input events added into the ECS world, deciding per frame from the track and crystal state ahead of the ball. Any error or exception during the run SHALL fail the test.

#### Scenario: Full run survives 60 seconds

- **WHEN** the bot plays a seeded session for 60 seconds at normal time scale
- **THEN** the session is still in the Playing state at the end of the run

#### Scenario: Minimum score reached

- **WHEN** the 60-second run completes with the session still playing
- **THEN** the session score is greater than or equal to the configured minimum score constant

#### Scenario: Entry through the real menu flow

- **WHEN** the test starts a session
- **THEN** it happens through the actual menu: the menu screen is shown with the test's game mode selected through the real mode buttons, the HUD is hidden, and clicking the Play button starts the session

#### Scenario: Bot steers via official input events only

- **WHEN** the bot decides to turn or jump
- **THEN** it does so by adding the game's tap/jump one-frame event entities into the ECS world, not by simulating raw mouse/keyboard input or calling gameplay systems directly

#### Scenario: Bot decisions from track state

- **WHEN** the bot evaluates the cells ahead of the ball each frame
- **THEN** it turns when the cells ahead are empty, jumps over holes and to collect golden crystals, and collects regular crystals by driving through them

#### Scenario: Ball fall fails the test

- **WHEN** at any moment of the run the session state leaves Playing (the ball fell)
- **THEN** the test fails immediately and reports the reached score and the ball's position

#### Scenario: Errors during the run fail the test

- **WHEN** any exception or error log occurs during the run
- **THEN** the test fails (Unity Test Framework fails it automatically; the run also counts logged errors and includes them in the failure message)
