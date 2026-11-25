# Tasks

## 1. Config: threshold helper

- [x] 1.1 In `ScoringExtensions` (new file, beside `ComboMultiplier`): add `public static int ScoreForLevel(this GameplayConfig cfg, int level) => cfg.scorePerLevel * level * level / 4;` (XML doc: entry threshold of the given level, ladder steps grow by scorePerLevel every two levels), and update the `scorePerLevel` tooltip in `GameplayConfig` (step grows by scorePerLevel every two levels). Verify the project compiles in Unity Editor (no Console errors) and the config asset needs no re-serialization.

## 2. Level formula

- [x] 2.1 In `LevelSystem.Run`: replace `score / scorePerLevel + 1` with the loop starting from the current level value: `while (score >= ScoreForLevel(level + 1)) level++;`, keeping the existing change event and sound emission. Update the system's summary doc comment; update `LevelComponent`'s doc comment in `Components.cs`. Verify the project compiles.

## 3. Play Mode verification

- [ ] 3.1 Verify thresholds in Play Mode: level 2 at score 500, level 3 at 1000, level 4 at 2000, level 5 at 3000 (temporarily lower `scorePerLevel` for a faster check if needed, then restore 500); level-up sound plays and HUD "Level N" updates at each threshold; speed still jumps by speedPerLevel per level.
- [ ] 3.2 Verify integration: background glides to the new hue on the level-4 transition (score 2000); restart/menu returns level to 1 and the level-1 background; pause freezes everything mid-play; no Console errors and no steady-state GC allocations.
- [ ] 3.3 Run the PlayMode test suite (mechanics-tests bot E2E, MinScore 550): passes unchanged.
