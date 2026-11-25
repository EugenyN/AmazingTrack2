# scoring Specification

## Purpose
Points for passed blocks, levels with a growing step (a ladder of thresholds from `scorePerLevel`) with ball speed growth, and high-score persistence between sessions.

## Requirements

### Requirement: Points for passed blocks

For each new block the ball lands on, the game SHALL award 1 point. Staying on the same block again SHALL not award points repeatedly.

#### Scenario: First block

- **WHEN** a session starts and the ball occupies the starting block
- **THEN** the score becomes 1

#### Scenario: Points without duplicates

- **WHEN** the ball moves "forward" and "sideways" within the same block
- **THEN** points are awarded only once for that block

### Requirement: Levels and speed growth

The game SHALL raise the level by 1 when the session score reaches the next level's threshold. The entry threshold of level n SHALL be computed as `scorePerLevel × n² / 4` (integer division; `scorePerLevel` defaults to 500). The score step from level L to level L+1 SHALL equal `scorePerLevel × ⌈L/2⌉`: level thresholds form the sequence 500, 1000, 2000, 3000, 4500, … (the step grows by `scorePerLevel` every two levels). Ball speed SHALL increase by `speedPerLevel` (default 1) per level: speed = base speed (4) + level × speedPerLevel. The level SHALL be displayed in the HUD and grow without a cap. On level-up, the game SHALL show a `Level up!` popup label in the HUD on the shared HUD popup label (the same one as combo notifications), following that label's show rules: float distance and duration from the combo settings, show time from its own `levelUpFloatLifetime` setting (default 2.5 s). The level-up text color is white, distinguishing it from the yellow/red combo labels.

#### Scenario: First level

- **WHEN** a session starts with a score of 0
- **THEN** the level is 1 and the speed is 5 (4 + 1)

#### Scenario: First thresholds match the previous scale

- **WHEN** the score reaches 500, then 1000
- **THEN** the level becomes 2, then 3 (the first three levels' thresholds match the previous linear scale)

#### Scenario: Step grows every two levels

- **WHEN** the score reaches 2000, then 3000, then 4500
- **THEN** the level becomes 4, then 5, then 6 (thresholds 500, 1000, 2000, 3000, 4500, …)

#### Scenario: Speed grows with level

- **WHEN** the level is raised
- **THEN** ball speed immediately increases by speedPerLevel

#### Scenario: Level-up popup

- **WHEN** the score crosses a level threshold and the session is in the playing state
- **THEN** the HUD popup label shows white `Level up!` text, starts floating up from its usual position (linear float over the distance and duration from the combo settings) and hides after `levelUpFloatLifetime` (default 2.5 s)

#### Scenario: Repeated level-up during the popup

- **WHEN** the score crosses the next level threshold while the `Level up!` label is still visible
- **THEN** the show restarts: the label returns to its original position and floats up again

#### Scenario: Pause freezes the popup

- **WHEN** the session enters pause while the `Level up!` label is visible
- **THEN** the float and the hide timer freeze and resume after unpause

#### Scenario: Combo event during the popup

- **WHEN** a crystal pickup produces a combo notification while the `Level up!` label is visible
- **THEN** the label switches to the combo text, and the show restarts from the original position (the label is shared; the latest event defines the text)

#### Scenario: Level reset does not pop up

- **WHEN** a session restarts (restart after game over, return to menu, start from menu) and the level returns to 1 without crossing a threshold
- **THEN** the `Level up!` label is not shown

### Requirement: High score in PlayerPrefs

The game SHALL store the high score (the largest session score) separately for each game mode in PlayerPrefs under a mode-dependent key, and SHALL update the mode's high score when it is exceeded at the end of a session (the ball falls). The old high-score key shared by all modes SHALL not be used; no migration of the old value is performed - all modes' high scores start at 0. When the mode is changed in the menu, the game SHALL re-read the selected mode's high score. The high score SHALL be displayed in the menu and in the HUD as `High Score: N`, and on the results screen as `Record: N`.

#### Scenario: High score beaten

- **WHEN** a session ends with the ball falling and the session score is greater than the mode's stored high score
- **THEN** the mode's high score is updated and shown as the new one on the results screen

#### Scenario: High score not beaten

- **WHEN** a session ends and the session score does not exceed the mode's high score
- **THEN** the stored mode high score stays the same and is shown on the results screen

#### Scenario: High score survives a restart

- **WHEN** the player closes and re-launches the game
- **THEN** each mode's stored high scores are available and the selected mode's high score is shown in the menu

#### Scenario: Mode high scores are independent

- **WHEN** the player beats the high score in one mode and then switches to another mode
- **THEN** the other mode's high score stays the same (the modes' high-score values do not affect each other)

#### Scenario: Mode change updates the high score in the menu

- **WHEN** the player in the menu selects another mode with the Easy/Normal/Hard button
- **THEN** the high-score label in the menu shows the newly selected mode's high score

#### Scenario: Old shared key is not used

- **WHEN** PlayerPrefs contains a value under the old shared high-score key and the mode keys are empty
- **THEN** all modes' high scores are 0 (the old value is not carried over and not displayed)

#### Scenario: High score in the HUD

- **WHEN** a game session is running
- **THEN** the HUD shows the selected mode's high score as `High Score: N`; when the high score is exceeded during the session, the HUD keeps showing the starting high-score value until the end of the session

### Requirement: Crystal points

When the ball picks up a crystal, the game SHALL award the variant's base points multiplied by the current combo multiplier (see the `combo` capability): picking a regular crystal - 10 × multiplier; picking a golden crystal (hovering above a block, picked up only by jumping - see the `crystals` capability) - `goldenCrystalScore` × multiplier (default 50 × multiplier). In both cases the game SHALL play the pickup sound and SHALL play a one-shot particle effect at the crystal's position (for the golden one - in the air, at the hover height). The awarded points SHALL go into the total session score and affect levels the same way as block points. The effect's lifecycle (pool, return after playing, no self-destruction by Stop Action) is described in the `view-pooling` capability; the crystal's own disappearance - in the `crystals` capability.

#### Scenario: Crystal pickup

- **WHEN** the ball reaches the block with a regular crystal and picks it up
- **THEN** the score increases by 10 × combo multiplier and the pickup sound plays

#### Scenario: Golden crystal pickup

- **WHEN** the jumping ball picks up a golden crystal
- **THEN** the score increases by `goldenCrystalScore` × combo multiplier (default 50 × multiplier) and the pickup sound plays

#### Scenario: Golden crystals affect levels

- **WHEN** golden crystals are picked up and the session score sum crosses a level threshold
- **THEN** the level is raised as usual (golden crystal points count into the total score)

#### Scenario: No effect outside gameplay

- **WHEN** pickup is impossible (pause, menu, the ball is falling)
- **THEN** no particle effect is created
