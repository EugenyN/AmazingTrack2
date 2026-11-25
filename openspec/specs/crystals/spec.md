# crystals Specification

## Purpose

Crystal pickup: the appearance (an octahedron mesh), pose, size and seating are defined by the prefab and the view's actual bounds; placement on blocks by the generator, falling after the host block, disappearance on pickup, and cleanup on session reset. Crystal view pooling is described in the `view-pooling` capability; pickup points/sound/effect - in the `scoring` capability.

## Requirements

### Requirement: Crystal octahedron mesh

The crystal SHALL use its own octahedron mesh asset (`Assets/Meshes/Octahedron.asset`) instead of Unity's built-in library mesh. The mesh SHALL have a 1×1×1 bounding box (like Unity's built-in cube) and a faceted look: flat normals per face (24 vertices, 8 triangular faces), no smoothing between faces.

#### Scenario: The mesh is a project asset

- **WHEN** the Crystal prefab is opened or the asset is inspected
- **THEN** the prefab's MeshFilter references `Assets/Meshes/Octahedron.asset`, not a library mesh (`fileID 10202`)

#### Scenario: Crystal size is defined by the prefab

- **WHEN** a crystal appears on a block
- **THEN** its visible size is defined by the prefab's scale at the mesh bbox of 1×1×1 (bounds-based seating adjusts automatically)

### Requirement: Crystal pose - vertex up

The crystal view's pose and size SHALL be defined by the prefab. For the octahedron the prefab SHALL set an identity rotation (the octahedron's vertex points up along the world Y axis); the scale is set in the prefab with no specific value in the spec. The view's spin around the vertical axis SHALL keep the vertex pointing up, without horizontal displacement of the pivot point.

#### Scenario: Octahedron vertex up

- **WHEN** the crystal view appears on a block
- **THEN** its rotation is identity and the octahedron's vertex points up, the scale equals the prefab's scale

#### Scenario: Spin keeps the orientation

- **WHEN** the crystal spins around the vertical axis
- **THEN** the vertex keeps pointing up, the pivot point does not shift horizontally

### Requirement: Crystal seating by actual bounds

The code SHALL seat the view vertically so that at the lowest point of the bobbing the view's bottom (the minimum Y over the actual renderer bounds) touches the host block's surface. The seating SHALL be computed from the view's actual bounds and therefore be correct for any pose, scale and mesh shape from the prefab, including the octahedron; the vertical seating offset is derived once from the actual bounds of the prefab and is not re-measured on every spawn (see `view-pooling`).

#### Scenario: Crystal touches the block

- **WHEN** the crystal view is seated above the block and bobs
- **THEN** at the lowest point of the bob the view's bottom (the octahedron's lower vertex) stays at the block's surface level, not sinking into it, at any rotation and scale set in the prefab

### Requirement: Crystal placement on blocks

The generator SHALL place crystals on some blocks according to a structured distribution (see "Structured crystal distribution over blocks") regardless of the block's orientation. A crystal SHALL stand on a random non-hole part of the block (distribution over parts SHALL be uniform); a crystal SHALL not appear on a hole part, and if the block has no non-hole parts (a single-part block that is a hole), no crystal SHALL appear on that block at all. Above each block (and above each cell) there SHALL be at most one crystal. If a crystal is spawned, a separate configurable chance SHALL decide whether it is golden; otherwise it SHALL be a regular crystal on the block's surface.

#### Scenario: Crystal on any block

- **WHEN** the generator creates a block (of any orientation) and the distribution decided to spawn a crystal on it
- **THEN** a crystal appears on that block on a random non-hole part

#### Scenario: No crystal on a hole part

- **WHEN** the generator creates a block with a hole and the distribution decided to spawn a crystal on it
- **THEN** the crystal is placed on one of the non-hole parts with uniform distribution

#### Scenario: No crystal on an absent block

- **WHEN** the generator creates a single-part block that is a hole
- **THEN** no crystal spawns on that block, and it does not participate in the distribution as eligible

#### Scenario: Variant decided by a separate chance

- **WHEN** a crystal spawn triggered
- **THEN** a separate `goldenCrystalChance` roll decides whether it is a golden crystal (hovering above the block) or a regular one (on the block's surface); the chance is rolled only for spawned crystals and does not affect crystal appearance frequency in general

#### Scenario: At most one crystal above a block

- **WHEN** the generator places crystals
- **THEN** above each block and above each cell there is at most one crystal (a regular and a golden one cannot occupy the same spot)

### Requirement: Structured crystal distribution over blocks

The generator SHALL decide which blocks get crystals with a structured distribution of configurable parameters, not an independent roll per block. Eligible for a crystal is any block capable of placing one (not a single-part block that is entirely a hole).

#### Scenario: Delay at the start of a session

- **WHEN** the generator builds blocks with an ordinal number below `crystalStartDelayBlocks` (numbered from zero from the session start)
- **THEN** no crystals spawn on them, and the gap and chain counters do not advance; the distribution count starts after the delay

#### Scenario: Chain of consecutive crystals

- **WHEN** after the minimal gap the `crystalChance` chance starts a chain
- **THEN** the chain length is chosen randomly in the `crystalChainMinBlocks..crystalChainMaxBlocks` range; the chain's crystals appear on consecutive eligible blocks, giving the player collectible combo streaks

#### Scenario: Chain crystals on neighboring cells

- **WHEN** the chain places its next crystal
- **THEN** it stands on a solid free cell neighboring the previous crystal of the chain (1 unit distance), at the same seating height as any regular crystal; the chain's first crystal stands on a random non-hole part of its block

#### Scenario: Collecting a chain crystal

- **WHEN** the ball rolls onto a chain crystal (or any crystal)
- **THEN** the pickup checks the block under the ball against the crystal's actual cell owner (the block whose solid part occupies the crystal's cell), not against the block on which the crystal decision was made; there is no pickup delay due to a "foreign" block reference

#### Scenario: The chain does not turn left

- **WHEN** the chain makes its next step to the next crystal
- **THEN** the step is made only along +Forward (the next block) or +Right (the next part of the same block) - never to the left (−Right), so the chain always goes along the ball's legal movement directions and the player can drive the whole chain

#### Scenario: A candidate over a hole or void is rerolled

- **WHEN** a neighbor candidate lands on a holed part, an empty cell, or a cell already holding a pending crystal of the same chain
- **THEN** it is rerolled; if both legal neighbors are invalid (a dead end at a hole), the link is skipped and the chain ends on the previous crystal - a crystal never hangs over a hole or void

#### Scenario: Chain length is skewed toward shorter

- **WHEN** the generator starts a new chain
- **THEN** the length is chosen in the `crystalChainMinBlocks..crystalChainMaxBlocks` range with 1/length weights - chains of fewer crystals appear more often than longer ones (for example, for the range 2..5: 39% for 2, 26% for 3, 20% for 4, 16% for 5)

#### Scenario: At most one golden per chain

- **WHEN** the generator decides the variant of the next chain link
- **THEN** the `goldenCrystalChance` roll is made only if no previous link of this chain has become golden yet; after the first golden one, all subsequent links of the chain spawn regular (outside chains, the chance's behavior is unchanged)

#### Scenario: Minimal gap between chains

- **WHEN** a chain has ended (the next link did not continue or the chain maximum is reached)
- **THEN** the next `crystalMinGapBlocks` eligible blocks stay without crystals, and a new chain cannot start before they expire

#### Scenario: Forced spawn at the maximal gap

- **WHEN** `crystalMaxGapBlocks` eligible blocks in a row have passed without a crystal
- **THEN** the next eligible block spawns a crystal regardless of the chance rolls - the drought is never longer than the maximal gap

#### Scenario: Ineligible blocks do not break the distribution

- **WHEN** during the minimal gap, a chain, or the maximal-gap countdown a block incapable of placing a crystal is encountered (a single-part block that is entirely a hole)
- **THEN** that block is skipped without consuming the gap and chain length counters; the next chain link appears on the next eligible block

#### Scenario: Distribution reset on session restart

- **WHEN** a session restarts
- **THEN** the distribution starts over: the start delay is counted from zero, the gap and chain counters are reset, with no "tails" from the previous session

### Requirement: Golden crystal

The game SHALL support a second crystal variant - the golden crystal: a separate prefab (golden material) spawned by the generator under the same conditions as the regular one, but hovering above the host block's surface at a configurable height. The golden crystal SHALL be picked up only by a jumping ball, and pickup detection SHALL be geometric in 3D: the full distance between the ball's actual position (the logical position plus the jump-arc height) and the crystal SHALL be compared against a configurable golden crystal pickup radius; the block under the ball is not considered. A grounded ball SHALL pass under it without picking it up: at the default setting (hover height equal to the jump peak), a standing ball's center is farther from the crystal than the pickup radius. In all other respects (falling after the host block, disappearance on pickup, cleanup on session reset) the golden crystal SHALL behave like the regular one.

#### Scenario: Golden crystal hovers above the block

- **WHEN** the generator places a golden crystal on a non-hole part of a block
- **THEN** its seating point is above the block's surface by the configurable height (`goldenCrystalHeight`, by default equal to the jump peak height), and the bobbing happens around that height

#### Scenario: A grounded ball passes under the golden crystal

- **WHEN** the ball rolls on the ground and enters the golden crystal's XZ radius
- **THEN** the crystal is not picked up, the ball passes under it, with no points, sound or effect (at the default setting the 3D distance to the crystal is greater than the pickup radius)

#### Scenario: A jump picks up the golden crystal

- **WHEN** the ball in a jump enters the golden crystal's pickup radius by the full 3D distance between the ball's actual position (logical position + jump-arc height) and the crystal
- **THEN** the crystal is picked up: it disappears, the golden crystal points are awarded, the shared pickup sound and a one-shot particle effect play at the crystal's position (in the air), tinted with the golden crystal's color

#### Scenario: Golden crystal collected on any part of the arc

- **WHEN** a golden crystal is on the takeoff block, under the middle of the jump arc, or on the block beyond a hole, and the ball's actual in-flight position enters the pickup radius
- **THEN** the crystal is picked up (golden pickup is not tied to the block under the ball, since in flight the ground block is frozen at the takeoff block)

#### Scenario: Missing the arc - missing the crystal

- **WHEN** a jump is made too early or too late, and the ball's actual position does not enter the golden crystal's 3D pickup radius
- **THEN** the crystal is not picked up (the flight geometry matters, not the fact of the jump)

#### Scenario: A regular crystal is not collected in flight

- **WHEN** the ball in a jump flies within the pickup radius above a regular crystal
- **THEN** the regular crystal is not picked up (in flight only golden ones are collected)

#### Scenario: Golden falls and disappears like a regular one

- **WHEN** a golden crystal's host block started falling, or the session is reset
- **THEN** the golden crystal falls slower than the block (the same multiplier), outlives the host block's death, is removed at its own depth threshold, and is removed instantly on session reset - like a regular crystal

### Requirement: Golden crystal spawns only above a jump-completable cell

A golden crystal SHALL spawn only above a solid (non-hole) cell through which a jump can be completed from the takeoff point to the landing. A jump is a straight flight of fixed length along the ball's current direction (+Forward or +Right; the track is never built to the left); the arc enters the crystal's pickup zone only on the segment 0.5..1.5 units after the takeoff point, so the ball must take off from the cell directly behind the crystal along the jump axis and land on the cell directly in front of it. For a golden crystal above cell C, a pair of solid cells (C−axis and C+axis) along the jump axis SHALL be required, where the jump axis SHALL be determined as: for a crystal spawned outside a chain - either of the two axes (the host block's +Forward or +Right); for a chain link - only the chain's axis (the link's in-step; for the chain's first link - the out-step). The cells "behind" the crystal are already built at decision time (the track is monotonic); the cells "ahead" can only appear together with the following slabs, so the variant decision SHALL be made after the slabs following the host block are built. If the required pair is not collected, the crystal SHALL spawn regular.

#### Scenario: Golden only above a cell with a takeoff/landing pair

- **WHEN** the generator decides the crystal variant above cell C
- **THEN** golden spawns only if both cells of the pair (C−axis and C+axis) along the jump axis defined by this requirement are occupied by solid blocks (the takeoff cell behind and the landing cell ahead); otherwise the crystal spawns regular

#### Scenario: A chain link requires a pair along the chain axis

- **WHEN** the generator decides the variant of a chain link that has an in-step
- **THEN** the takeoff/landing pair is checked only along the in-step direction (the chain axis); a pair along the perpendicular axis does not make the link eligible for the golden variant, even if both its cells are solid

#### Scenario: The chain's first link requires a pair along the out-step

- **WHEN** the generator decides the variant of the chain's first link (no in-step, the out-step toward the next link)
- **THEN** the takeoff/landing pair is checked only along the out-step direction (the chain axis); a pair along the perpendicular axis does not make the link eligible for the golden variant

#### Scenario: A lone crystal allows any axis

- **WHEN** the generator decides the variant of a crystal spawned outside a chain
- **THEN** the takeoff/landing pair is checked along either of the two axes (the host block's +Forward or +Right); having at least one pair makes the crystal eligible for the golden variant

#### Scenario: A crystal spawns no earlier than the following slabs are known

- **WHEN** a block with a deferred crystal is built
- **THEN** the crystal entity is created after the following slabs are built (within the same generation batch); for the player this is indistinguishable - the crystal appears at the far edge of the lookahead

### Requirement: Golden crystal does not spawn on a chain turn

The generator SHALL not assign the golden variant to a crystal-chain link on which the chain turns: a link with different in-step and out-step directions (an "L" bend) spawns regular, without a `goldenCrystalChance` roll. Straight links and chain edges remain eligible for the golden variant on general grounds - subject to the jump-axis requirement: a chain link is checked only along the chain axis (the in-step; for the first link - the out-step). Directions SHALL be compared as world step vectors.

#### Scenario: A chain-turn link spawns regular

- **WHEN** the chain arrived at a link along one axis and left it along a perpendicular one (the in-step and the out-step are world vectors differing by axis)
- **THEN** this link's crystal spawns regular, no `goldenCrystalChance` roll is made for it, regardless of the presence of jump pairs around the cell

#### Scenario: A straight chain link can be golden

- **WHEN** the chain passes a link in a straight line (the in-step and the out-step are the same world vector), and a takeoff/landing pair exists along the chain axis (the in-step)
- **THEN** a regular `goldenCrystalChance` roll is made for the link; the crystal may become golden

#### Scenario: A chain edge can be golden

- **WHEN** a link has no neighbor on one of the sides (the chain's first or last link), and a takeoff/landing pair exists along the chain axis (the in-step; for the first link - the out-step)
- **THEN** a regular `goldenCrystalChance` roll is made for the link; the crystal may become golden, the turn rule does not apply to edges

#### Scenario: A lone crystal is unaffected

- **WHEN** a crystal is spawned outside a chain
- **THEN** the variant decision is unchanged: a `goldenCrystalChance` roll when a takeoff/landing pair exists along either of the two axes

#### Scenario: The turn is detected in world coordinates

- **WHEN** the chain turns at the junction of blocks of different orientations (the blocks' local Forward/Right differ as world axes)
- **THEN** the in-step and out-step directions are compared as world step vectors, so the bend is detected regardless of the host blocks' orientations of the links

### Requirement: Pickup effect color by crystal variant

The one-shot pickup particle effect SHALL be tinted with the picked variant's color: the golden crystal's material color for the golden one, the regular one's for the regular. A separate FX prefab SHALL not be created for this: the tinting is done by swapping the material on the effect view when it is spawned from the pool.

#### Scenario: Golden burst - golden

- **WHEN** a golden crystal is picked up
- **THEN** the particle effect at the crystal's position plays with the golden crystal's material (golden color)

#### Scenario: Regular burst - the crystal's color

- **WHEN** a regular crystal is picked up
- **THEN** the particle effect plays with the regular crystal's material (as before this change)

### Requirement: Crystal falls after its host block

When a crystal's host block starts falling (after its own delay), an uncollected crystal SHALL start falling after it: slower than the block (a configurable speed multiplier), keeping its spin, without bobbing. The crystal SHALL outlive the host block's death and be removed at its own depth threshold. On session reset, crystals SHALL be removed instantly together with the blocks.

#### Scenario: Crystal falls after the block

- **WHEN** a block with a crystal starts falling (its fall delay has expired)
- **THEN** the uncollected crystal on that block starts descending from zero speed, accelerating slower than the block (the multiplier from the config), the crystal keeps spinning, the bobbing stops

#### Scenario: The crystal stands while the block stands

- **WHEN** a block is marked falling but its delay has not expired yet
- **THEN** the crystal keeps standing on the block and bobbing as usual

#### Scenario: The crystal outlives the host block

- **WHEN** the host block fell beyond the depth threshold and was removed, and the crystal is still above the threshold
- **THEN** the crystal keeps falling on its own timer and is removed when it itself descends below the depth threshold

#### Scenario: Crystals are removed on session reset

- **WHEN** a session restarts or the player exits to the menu
- **THEN** all crystals (including falling ones) are removed instantly together with the blocks, with nothing left hanging in the new session

### Requirement: Crystal disappears on pickup

When the ball picks up a crystal, the crystal SHALL disappear from the block and SHALL not be available for a repeated pickup; the crystal view is returned to the pool (see `view-pooling`).

#### Scenario: Crystal disappears after pickup

- **WHEN** a crystal is picked up
- **THEN** it disappears from the block and cannot be picked up again
