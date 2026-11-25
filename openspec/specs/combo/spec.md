# combo Specification

## Purpose

Combo streak for crystals: a counter of consecutively collected crystals, a score multiplier by streak tiers, streak break on a missed crystal, and a HUD feedback label with a float-up animation; the round's maximum streak value shown in the HUD and on the results screen.

## Requirements

### Requirement: Combo counter

The game SHALL maintain a combo counter within a session: picking up a crystal (regular or golden) SHALL increase the counter by 1; any uncollected crystal that started falling together with its host block SHALL reset the counter to 0. The counter SHALL be zeroed on session start, on exit to the menu, and on session end by the ball's fall.

#### Scenario: Streak grows with pickups

- **WHEN** the ball picks up crystals one after another without misses
- **THEN** the combo counter equals the number of crystals collected in a row since the last reset

#### Scenario: Golden crystal continues the streak

- **WHEN** the streak was 2 and the jumping ball picks up a golden crystal
- **THEN** the combo counter becomes 3

#### Scenario: A miss resets the streak

- **WHEN** a crystal stays uncollected and its host block started falling (the crystal went into its fall)
- **THEN** the combo counter is reset to 0

#### Scenario: A new session starts from zero

- **WHEN** a session restarts or the player exits to the menu
- **THEN** the combo counter is 0

### Requirement: Score multiplier by combo tiers

Crystal points SHALL be multiplied by the multiplier determined by the combo counter value after picking up that crystal: ×1 at streak 1, ×2 starting at streak 2, ×3 starting at streak 5, ×4 starting at streak 10. Tier thresholds SHALL be configurable. The crystal that brought the streak to a tier SHALL receive that tier's multiplier.

#### Scenario: Second crystal doubles

- **WHEN** the combo counter was 1 and the ball picks up the next crystal
- **THEN** the crystal awards base points ×2

#### Scenario: Fifth crystal triples

- **WHEN** the combo counter was 4 and the ball picks up the next crystal
- **THEN** the crystal awards base points ×3

#### Scenario: Tenth crystal quadruples

- **WHEN** the combo counter was 9 and the ball picks up the next crystal
- **THEN** the crystal awards base points ×4

#### Scenario: First crystal without a multiplier

- **WHEN** the combo counter was 0 and the ball picks up a crystal
- **THEN** the crystal awards base points without a multiplier

### Requirement: Combo break

When an uncollected crystal (regular or golden) starts falling after its host block, the game SHALL consider the streak broken and SHALL reset the counter to 0. If the broken streak was 2 or more, the game SHALL show a red "Combo Break" label; if the streak was 1, the reset SHALL happen without a label. While the ball is falling and on the results screen, the break label SHALL not be shown.

#### Scenario: Break after a streak

- **WHEN** the streak was 2 or more and an uncollected crystal started falling
- **THEN** a red "Combo Break" label appears, the counter resets to 0

#### Scenario: Silent reset of a single crystal

- **WHEN** the streak was 1 and an uncollected crystal started falling
- **THEN** the counter resets to 0 without any label

#### Scenario: No break outside gameplay

- **WHEN** a session ends with the ball's fall, and uncollected crystals are removed on session reset
- **THEN** the "Combo Break" label is not shown

### Requirement: Combo label in the HUD

On picking up a crystal that brought the streak to 2 or more, the game SHALL show a yellow label of the form "{N} combo" in the HUD, where N is the counter value after the pickup. A pickup that brought the streak to 1 SHALL not show a label. The label SHALL hide after a configurable time (default 2 seconds) after the last combo event, if no new events occurred.

#### Scenario: Label from the second crystal

- **WHEN** the ball picks up the second crystal in a row
- **THEN** a yellow "2 combo" label appears in the HUD

#### Scenario: Number grows with the streak

- **WHEN** the streak keeps growing without breaks
- **THEN** each event shows the current number: "3 combo", "4 combo", "5 combo" and so on

#### Scenario: First crystal without a label

- **WHEN** the ball picks up the streak's first crystal (the counter became 1)
- **THEN** no combo label appears

#### Scenario: Label hides on a timer

- **WHEN** 2 seconds have passed since the last combo event and no new events occurred
- **THEN** the label hides

### Requirement: Label appearance animation

On each new combo event the label SHALL become visible at the original position and SHALL float up by a configurable distance (default 86 UI units) over a configurable time (default 1 second). If a new event arrives while the label is visible, the animation SHALL restart: the label returns to the original position and floats up again with the new text. While paused, the animation SHALL freeze and resume after unpause.

#### Scenario: Float-up

- **WHEN** a combo label appears
- **THEN** it becomes visible at the original position and rises up by 86 units over 1 second

#### Scenario: Restart on a new event

- **WHEN** a new combo event arrives while the label is still visible and floating
- **THEN** the label instantly returns to the original position and floats up again, showing the new text

#### Scenario: Pause freezes the animation

- **WHEN** the game is paused while the label is visible
- **THEN** the label freezes in place and resumes its animation after unpause

#### Scenario: Label hides outside gameplay

- **WHEN** a session ends (the ball's death, game over, exit to menu) or a new session starts
- **THEN** the combo label hides and does not appear outside active gameplay

### Requirement: Maximum combo per round

The game SHALL track the maximum combo streak value reached during the current session. The maximum SHALL update on each crystal pickup that brought the streak to a new maximum, and SHALL not decrease on a streak break. The maximum SHALL reset on session start, restart and exit to the menu. The maximum SHALL be shown in the HUD (the label is hidden while the maximum is 0, and visible from the first combo) and on the results screen as "Max Combo: N".

#### Scenario: Maximum grows with the streak

- **WHEN** a combo streak reaches a value greater than the current maximum
- **THEN** the maximum becomes that streak's value

#### Scenario: A break does not reset the maximum

- **WHEN** a streak of 5 breaks and a new streak starts
- **THEN** the maximum stays 5 until the new streak exceeds it

#### Scenario: Label hidden before the first combo

- **WHEN** no crystal has been picked up in the session yet (the maximum is 0)
- **THEN** the max-combo label in the HUD is hidden

#### Scenario: Label appears with the first combo

- **WHEN** the ball picks up the session's first crystal
- **THEN** the HUD label shows "Max Combo: 1" and keeps updating on each new maximum

#### Scenario: Maximum on the results screen

- **WHEN** a session ends with the ball's fall
- **THEN** the results screen shows "Max Combo: N", where N is the round's best

#### Scenario: Reset on a new session

- **WHEN** a session restarts or the player exits to the menu
- **THEN** the maximum resets to 0 and the HUD label hides again
