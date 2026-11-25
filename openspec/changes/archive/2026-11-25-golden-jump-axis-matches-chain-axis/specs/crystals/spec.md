# Spec Delta

## MODIFIED Requirements

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
