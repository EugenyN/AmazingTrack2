# game-configs Specification

## Purpose
The ScriptableObject configuration contract: which tuning constants live in
GameplayConfig versus PresentationConfig, and the isolation rule that presentation
tuning never affects simulation behavior while gameplay tuning never references
scene assets.

## Requirements

### Requirement: Split configuration assets

The game SHALL be tuned through two ScriptableObject assets instead of a single
GameConfig: a GameplayConfig holding simulation rules and a PresentationConfig
holding presentation. Each tunable constant SHALL live in exactly one of the two
assets.

#### Scenario: Gameplay tuning is separated from presentation tuning

- **WHEN** a gameplay constant (for example ball speed, a hole chance, or track
  geometry) is tuned
- **THEN** it is found in GameplayConfig, and the presentation asset exposes no
  gameplay constants

#### Scenario: Presentation tuning is separated from gameplay tuning

- **WHEN** a presentation setting (for example a prefab reference, an audio clip, a
  background color, a camera size or view animation timing) is tuned
- **THEN** it is found in PresentationConfig, and the gameplay asset exposes no
  prefab or asset references

### Requirement: Gameplay config carries no asset references

GameplayConfig SHALL NOT contain references to Unity assets (prefabs, clips,
materials, scene objects).

#### Scenario: No object references in the gameplay asset

- **WHEN** the GameplayConfig asset is inspected
- **THEN** every field is a numeric or vector value, with no GameObject or
  AudioClip slots

### Requirement: Systems read the matching config

Each system SHALL read its constants from the config that owns them: simulation
systems (track generation, ball movement, jump, ground, scoring, levels) from
GameplayConfig, and view/presentation systems (block and crystal views, block and
crystal fall animation, ball fall presentation, game-over timing, background,
camera, audio, UI labels) from PresentationConfig. No system SHALL depend on the
removed GameConfig type.

#### Scenario: Simulation constants come from GameplayConfig

- **WHEN** a simulation system needs a tuning constant (for example the track
  generator needing a hole chance)
- **THEN** it reads the value from GameplayConfig

#### Scenario: View constants come from PresentationConfig

- **WHEN** a presentation system needs a setting (for example the block view needing
  the color range, or the audio system needing clips)
- **THEN** it reads the value from PresentationConfig

### Requirement: Values migrate 1:1

Migrating the previous GameConfig asset SHALL preserve every tuning value: the
created GameplayConfig and PresentationConfig assets SHALL start with the same
values the GameConfig asset held, so gameplay behavior after the migration is
unchanged.

#### Scenario: Behavior unchanged after migration

- **WHEN** the game is played after the split and both new assets are migrated
- **THEN** ball speed, scoring, crystal spawn, hole chances, fall timing, colors,
  background, camera framing and audio behave exactly as before the split
