# Design

## Context

`CrystalDistributor.ResolvePending` decides the golden variant when a deferred crystal reaches a two-block age. For a chain link it currently accepts a jump pair (solid takeoff cell behind + solid landing cell ahead) along *either* the host block's Forward or Right axis (`forwardPairFor(ref pending, world) || rightPairFor(ref pending, world)`), then applies the chain-turn guard and the at-most-one-golden guard. `PendingCrystal` already stores world-space step directions (`InDir`, `OutDir`) and the host block axes (`Forward`, `Right`), so the chain's travel axis is available at decision time. See proposal.md for the motivation.

## Goals / Non-Goals

**Goals:**
- A player driving along a crystal chain can always collect a golden crystal on that chain without leaving the chain's path: the required jump axis coincides with the direction of travel.
- Keep the lone-crystal behavior unchanged (either axis qualifies).

**Non-Goals:**
- Changing `goldenCrystalChance`, pickup geometry, scoring, or any view/pooling behavior.
- Requiring a solid cell beyond the landing cell (post-jump survival stays the player's challenge, as with hole jumps).
- Moving the golden decision to chain start (pre-granting a golden link number); the resolve-time decision architecture stays.

## Decisions

### 1. Restrict the chain-link pair check to the chain axis, chosen from existing step fields

In `ResolvePending`, the chain branch (`pending.ChainId >= 0`) replaces the either-axis check with a single-axis check:

- axis = `pending.InDir` when it is non-zero (any link with a previous neighbor);
- axis = `pending.OutDir` when `InDir` is zero (chain start: first link; the next link's in-step, backfilled into this entry, is the chain's direction);
- if both are zero (a degenerate single-entry chain, e.g. the only link whose continuation died) → not eligible for golden: no chain axis exists to guarantee a reachable jump.

The pair check itself is the existing takeoff/landing test with the chosen axis: `HasSolidCell(cell - axis) && HasSolidCell(cell + axis)`. `forwardPairFor`/`rightPairFor` stay for the lone-crystal branch, which keeps `forwardPairFor || rightPairFor`.

*Why the in-step (not out-step) as the canonical axis:* the player approaches a link along the direction the chain came from; the in-step is that direction for every link except the first. Using the out-step only for the first link (where no in-step exists) keeps the whole chain on one consistent rule: the axis the player actually drives.

*Why not extend the rule to lone crystals:* a lone crystal has no guaranteed approach direction, so either axis is an honest chance; restricting it would cut the golden rate without fixing any guaranteed-break problem. The trap the change fixes is specifically "inside a collectible series".

*Alternative considered:* pre-selecting the golden link index at chain start (decision 4 in the discussion). Rejected: same outcome, larger structural change to the distributor; the axis restriction achieves it with a one-branch edit.

### 2. Interaction with the existing guards stays as-is

The chain-turn guard (`InDir != OutDir` across axes → never golden) is unchanged and remains evaluated before the roll; a straight link's `InDir == OutDir` means both directions describe the same axis, so the axis choice is unambiguous. The at-most-one-golden-per-chain guard and the null-prefab fallback are untouched.

### 3. No allocation or structural changes

The decision uses already-stored `Vector3` fields and the existing `TrackQueriesHelper.HasSolidCell` queries. No new state, no buffers, no per-frame allocations.

## Risks / Trade-offs

- [Golden crystals become rarer: chain edges and perpendicular-only links now fall back to regular] → Accepted as the point of the fix; if playtesting finds the rate too low, raise `goldenCrystalChance` in `GameplayConfig` (no code change).
- [The first chain link rolls its axis from `OutDir`, which is backfilled by the *next* link's step] → The backfill already exists and completes before resolve (the entry is queued while the next link is deferred; resolve happens two blocks later). A chain that dies immediately after its first link leaves `OutDir` zero → the link is simply not golden-eligible; safe default.
- [Chain links adjacent to a hole behind the crystal along the chain axis lose golden eligibility even though a perpendicular jump exists] → Intended: the perpendicular jump forces leaving the chain, which is the guaranteed-break trap.
