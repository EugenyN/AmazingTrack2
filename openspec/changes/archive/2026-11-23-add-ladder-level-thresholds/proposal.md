# Proposal

## Why

Level-ups currently arrive at a constant pace: every `scorePerLevel` points,
forever. Since ball speed grows with level, late levels keep arriving at the
same rate and the difficulty curve flattens into a steady speed climb. Making
each step of the ladder wider than the last slows late progression while
keeping early levels exactly as they play today.

## What Changes

- Level thresholds become a ladder: the score step from level L to L+1 is
  `scorePerLevel × ⌈L/2⌉` — 500, 500, 1000, 1000, 1500, 1500, … The step grows
  by `scorePerLevel` every two levels.
- Equivalently, the entry threshold of level n is `scorePerLevel × n² / 4`
  (integer division): level 2 at 500, level 3 at 1000, level 4 at 2000 (would
  be 1500 linear), level 5 at 3000 (was 2000), level 6 at 4500 (was 2500).
- Levels 1–3 thresholds are unchanged (500, 1000), so early-game behavior and
  the E2E bot test (MinScore 550 → level 2) are unaffected.
- Ball speed formula is untouched: baseSpeed + level × speedPerLevel.
- No new serialized config fields and no scene/prefab edits: the ladder is
  fully determined by the existing `scorePerLevel`.

## Capabilities

### Modified Capabilities

- `scoring`: the "Levels and speed growth" requirement changes — level
  thresholds follow the ladder formula above instead of a fixed
  `scorePerLevel`-point step.

## Impact

- `Assets/Scripts/Config/ScoringExtensions.cs` — new: `ScoreForLevel(int level)`
  helper over `GameplayConfig` (beside `ComboMultiplier`, same file).
- `Assets/Scripts/Ecs/LevelSystem.cs` — replace the division formula with a
  threshold loop.
- `Assets/Scripts/Ecs/Components.cs` — `LevelComponent` doc comment update.
- Specs: `openspec/specs/scoring/spec.md` (modified requirement) after archive.
