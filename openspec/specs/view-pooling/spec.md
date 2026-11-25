# view-pooling Specification

## Purpose

Pooled lifecycle of block, crystal, and pickup-FX views and the game's memory-allocation budget: these views are reused instead of instantiated/destroyed during play, and system update paths stay free of garbage-collection allocations during play.

## Requirements

### Requirement: Block views are pooled
Block views SHALL be acquired from a pool and released back to it instead of being instantiated and destroyed during play. A pooled block view SHALL be re-initialized on acquire (pose, per-block color, entity link rebound to the live entity); a released block view SHALL be hidden and disabled, not destroyed. The pool SHALL be shared across sessions.

#### Scenario: Block spawns without instantiation
- **WHEN** the generator creates a block entity while the pool holds a released view
- **THEN** the pool's existing GameObject is reused for the view and no new GameObject is created

#### Scenario: Fallen block returns to the pool
- **WHEN** a falling block view passes the depth threshold
- **THEN** the view is deactivated and returned to the pool, and the block entity is deleted as before

#### Scenario: Session reset reuses views
- **WHEN** a session is restarted or the game returns to the menu
- **THEN** all live block views are released to the pool (hidden, not destroyed), and the next session reuses them without GameObject instantiation

### Requirement: Block parts come from the prefab
The block prefab SHALL contain all possible unit part cubes (mesh and collider, Block layer) as children. At spawn the view system SHALL activate exactly the non-hole parts of the block and deactivate the remaining children. Creating part GameObjects at runtime SHALL not happen; parts of one block SHALL move, fall, and disappear together as one root view.

#### Scenario: Three-part block activates all parts
- **WHEN** a 3-part block with no hole spawns
- **THEN** all 3 part cubes are active and share one random pastel color

#### Scenario: Hole part stays deactivated
- **WHEN** a block with a hole part spawns
- **THEN** the hole part's cube is inactive (no mesh, no collider) and the other parts are active

### Requirement: Crystal views are pooled
Crystal views SHALL be acquired from a pool and released back to it instead of being instantiated and destroyed, on spawn, pickup, and orphan cleanup. There SHALL be two separate pools, one per crystal variant: the regular crystal pool (from `crystalPrefab`) and the golden crystal pool (from `goldenCrystalPrefab`). Every release path (pickup, orphan/fall cleanup, session reset) SHALL release each crystal view to the pool of its own variant; releasing a view to the wrong variant's pool SHALL NOT happen. The vertical seating offset of each variant SHALL be derived from that variant's prefab's rendered bounds once at startup, not measured per spawn (the seating behavior itself is specified in the `crystals` capability). A released crystal view SHALL be hidden and disabled, not destroyed.

#### Scenario: Crystal spawns without instantiation or bounds measurement
- **WHEN** a regular crystal entity is created and the regular pool holds a released view
- **THEN** the existing GameObject is reused, seated at the precomputed offset above its block part, and no renderer search runs at spawn time

#### Scenario: Golden crystal spawns from its own pool
- **WHEN** a golden crystal entity is created
- **THEN** its view is acquired from the golden crystal pool (golden prefab) and seated using the seat offset measured from the golden prefab's bounds

#### Scenario: Picked crystal returns to the pool
- **WHEN** the ball picks the crystal up
- **THEN** the pickup effects still play at the crystal position and the crystal view is released to its own variant's pool (hidden, not destroyed)

#### Scenario: Orphan crystal returns to the pool
- **WHEN** a non-falling crystal's host block disappears, or a falling crystal passes its own depth threshold
- **THEN** the crystal view is released to its own variant's pool and the entity is deleted as before

#### Scenario: Session reset releases each variant to its own pool
- **WHEN** a session is restarted or the game returns to the menu with live crystals of both variants
- **THEN** every regular crystal view is released to the regular pool and every golden crystal view to the golden pool (hidden, not destroyed), and no view ends up parked in the wrong pool for the next session

### Requirement: Pickup FX instances are pooled
Pickup-FX instances SHALL be acquired from a pool and released back to it instead of being instantiated and destroyed. The FX prefab SHALL play exactly once per acquisition and return to the pool after finishing; the one-shot effect at the crystal position is unchanged. Re-acquired instances SHALL restart their particle playback from the beginning.

#### Scenario: FX plays once and returns to the pool
- **WHEN** the ball picks a crystal up
- **THEN** a pooled FX instance plays its particle burst once at the crystal position and is returned to the pool after playback ends

#### Scenario: FX does not linger or replay silently
- **WHEN** an FX instance finishes playing and is later reused
- **THEN** it restarts from its beginning at the new position and no stopped instance stays active in the scene

### Requirement: Allocation budget during play
During play the game SHALL not perform garbage-collected allocations more often than once every 0.5 seconds, and per-frame system update paths SHALL not allocate. Rare event-driven allocations (session reset teardown, UI text updates on change events) are permitted. Sleeping pooled objects SHALL not participate in physics queries or rendering.

#### Scenario: Steady-state frame profile is allocation-free
- **WHEN** the ball rolls along a steady track with crystals spawning and falling blocks releasing for several seconds (menu demo track, Playing, and Falling states included)
- **THEN** the profiler reports no garbage-collection allocations in any system update path: no per-frame collection instantiation, no per-spawn component searches (GetComponent family), and no per-spawn component additions on pooled views

#### Scenario: Pooled views are invisible to physics and rendering
- **WHEN** a pool holds sleeping released views
- **THEN** those GameObjects are deactivated and never register in physics queries or render
