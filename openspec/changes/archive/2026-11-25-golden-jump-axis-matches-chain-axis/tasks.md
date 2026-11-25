# Tasks

## 1. Golden axis restriction in the distributor

- [x] 1.1 In `Assets/Scripts/Systems/CrystalDistributor.cs` `ResolvePending`, chain branch (`pending.ChainId >= 0`): replace the either-axis pair check (`forwardPairFor || rightPairFor`) with a single-axis check along the chain axis — `pending.InDir` when non-zero, else `pending.OutDir`; when both are zero the link is not golden-eligible. Keep the chain-turn guard, the at-most-one-golden guard, and the null-prefab fallback unchanged. Verify: project compiles in Unity Editor with no errors.
- [x] 1.2 Update the XML doc comments on `ResolvePending` (and the summary on the class if needed) to state the chain-axis rule in English; add a private helper (e.g. `pairAlongAxis`) next to `forwardPairFor`/`rightPairFor` instead of duplicating the takeoff/landing test inline. Verify: comments read correctly, no other behavior touched.

## 2. Verification

- [x] 2.1 Automated rule check in Play Mode: run the game with the existing bot/session harness over several track-rebuild cycles and assert every golden crystal on a chain link has its jump pair (solid takeoff and landing cells) along that chain's in-step axis (out-step for a chain's first link); assert lone-crystal goldens may still satisfy either axis; assert no golden over a hole/void and at most one golden per chain. Verify: 0 violations across the run, console clean.
- [x] 2.2 Play Mode smoke pass by the user: chains with a golden crystal are collectible end-to-end — drive the chain, jump at the golden one along the chain direction, land back on the chain and collect the next link; confirm golden crystals remain visibly present (tune `goldenCrystalChance` in `GameplayConfig` if too rare); combo does not break from an unavoidable golden miss.
