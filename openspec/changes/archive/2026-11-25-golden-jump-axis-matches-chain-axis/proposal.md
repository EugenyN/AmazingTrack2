# Proposal

## Why

A golden crystal can currently roll golden on a chain link (or a lone crystal) whose only valid jump pair is perpendicular to the direction the player travels along the chain. Collecting it then requires leaving the chain axis, after which the player cannot return to the next link - the golden crystal or the next link is guaranteed to be missed, and any missed crystal that starts falling breaks the combo (capability `combo`). The placement must guarantee that a player driving the chain can collect the golden crystal without abandoning the chain.

## What Changes

- For chain links, the golden jump-pair check (takeoff cell behind + landing cell ahead along one jump axis) is restricted to the chain's own travel axis: the in-step direction of the link (the out-step direction for the chain's first link). The perpendicular pair no longer qualifies a chain link for the golden variant.
- Lone crystals (outside chains) keep the current check: either axis pair (`forwardPairFor` or `rightPairFor`) still qualifies, because a lone crystal has no chain axis to follow.
- The existing chain-turn rule and the at-most-one-golden-per-chain rule are unchanged; they continue to apply on top of the axis restriction.
- Net effect: golden crystals become slightly rarer (some chain edges and perpendicular-pair-only links fall back to regular); `goldenCrystalChance` tuning is unaffected.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `crystals`: The golden placement requirement changes - a chain link is eligible for the golden variant only when the jump pair exists along the chain's travel axis (in-step, or out-step for the first link), not along any axis; the "golden only over a jump-completable cell" requirement keeps its lone-crystal behavior.

## Impact

- `Assets/Scripts/Systems/CrystalDistributor.cs` - `ResolvePending` golden decision: chain-link branch restricts the pair check to the chain axis (`pending.InDir`, falling back to `pending.OutDir` for the first link); `forwardPairFor`/`rightPairFor` remain for lone crystals and the axis-selected check.
- `openspec/specs/crystals/spec.md` - amended golden placement requirements.
- No config, prefab, view, scoring, or pickup changes; the bot (`GameplayBot`) already jumps along its travel direction, so its behavior stays correct.
