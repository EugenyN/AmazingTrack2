# Spec Delta

## MODIFIED Requirements

### Requirement: Levels and speed growth

The game SHALL raise the level by 1 when the session score reaches the next level's threshold. The entry threshold of level n SHALL be computed as `scorePerLevel × n² / 4` (integer division; `scorePerLevel` defaults to 500). The score step from level L to level L+1 SHALL equal `scorePerLevel × ⌈L/2⌉`: level thresholds form the sequence 500, 1000, 2000, 3000, 4500, … (the step grows by `scorePerLevel` every two levels). Ball speed SHALL increase by `speedPerLevel` (default 1) per level: speed = base speed (4) + level × speedPerLevel. The level SHALL be displayed in the HUD and grow without a cap.

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
