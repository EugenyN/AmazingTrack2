# Design

## Context

`LevelSystem` derives level from score per frame
(`score / scorePerLevel + 1`), level lives in `LevelComponent` on the session
entity, and every reset path goes through `GameFlowSystem.ResetProgress` (sets
level 1). Consumers of the level value: speed (`BallMoveSystem`,
`BallJumpSystem`), background hue (`BackgroundSystem`), HUD (`UiSyncSystem`),
level-up sound. Config was split since the original plan: scoring tuning now
lives in `GameplayConfig` (`Assets/Configs/GameplayConfig.asset`,
`scorePerLevel: 500`), not `GameConfig`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Ladder thresholds with integer-only math, zero allocations, no new state.
- Levels 1–3 behave exactly as today; only level ≥ 4 thresholds move.
- Single source of truth for the formula (one helper), covered by the spec.

**Non-Goals:**
- No HUD progress display for the next threshold (declined for this change).
- No speed-curve changes, no per-level config tables.

## Decisions

### Threshold formula: `scoreForLevel(n) = scorePerLevel × n² / 4`
Integer-only closed form of the ladder rule (step `scorePerLevel × ⌈L/2⌉`).
Checks with scorePerLevel 500: n=2 → 500, n=3 → 1000, n=4 → 2000, n=5 → 3000,
n=6 → 4500, n=8 → 8000. Alternatives rejected: pure linear (current), step =
L × scorePerLevel (user asked for a slower ramp), accumulation counter in
`LevelComponent` (more state, same behavior).

### Inverse via a bounded loop, not sqrt
`LevelSystem` starts from the current level and advances
`while (score >= ScoreForLevel(level + 1)) level++;`. Exact integer math (a
float `sqrt` inverse risks off-by-one at exact thresholds); usually 0–1
iterations per frame; monotonic score during play means no regressions; all
reset paths already restore level 1.

### Helper in `ScoringExtensions`, no serialized changes
`public static int ScoreForLevel(this GameplayConfig cfg, int level) =>
cfg.scorePerLevel * level * level / 4;` beside `ComboMultiplier` in
`Assets/Scripts/Config/ScoringExtensions.cs` — both derived-scoring methods
live off the ScriptableObject type (the `GameModeExtensions` precedent),
tuning fields stay on `GameplayConfig`. Methods are not serialized, so
`Assets/Configs/GameplayConfig.asset` needs no editor work. Tooltip of
`scorePerLevel` updated to describe the ladder step.

### Doc-comment updates only elsewhere
`LevelComponent` ("score / scorePerLevel + 1") and `LevelSystem`'s summary get
the new formula wording. No consumer code changes: speed, background, HUD and
sound all read `LevelComponent.Value` and stay valid.

## Risks / Trade-offs

- [Rarer late level-ups] Background hue changes and level-up sounds become
  less frequent at high levels — that is the intended effect of the request.
- [E2E bot test] `MinScore = 550` maps to level 2 under both the old and new
  scales (first changed threshold is level 4 at 2000); the test must still
  pass.
- [Integer overflow] Only meaningful for level > ~46 000 (n² overflow);
  unreachable in practice.
