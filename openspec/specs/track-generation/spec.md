# track-generation Specification

## Purpose
Endless procedural generation of a track from blocks with random turns, which always keeps a sufficient margin of blocks ahead of the ball and removes passed blocks. The generator places crystals on blocks; crystal rules (appearance, seating, falling, pickup) are split out into the `crystals` capability.

## Requirements

### Requirement: Track margin ahead of the ball

The track SHALL be continuously extended so that there are always at least 25 blocks ahead of the ball, and SHALL be extended in batches (not block by block) when the minimum is violated.

#### Scenario: Margin is replenished

- **WHEN** the ball passes blocks and the margin ahead drops below 25
- **THEN** the generator extends the track to the target length (more than 25 blocks) in a single batch step

### Requirement: Block part count per mode

Each block SHALL be composite: one entity made of N unit parts, where N is determined by the game mode: Easy — 3, Normal — 2, Hard — 1. Parts of one block SHALL share one color and spawn and fall together. All blocks of a session SHALL have the same part count.

#### Scenario: Easy mode

- **WHEN** the player picks the Easy mode and a session starts
- **THEN** each block consists of 3 parts

#### Scenario: Normal mode

- **WHEN** the player picks the Normal mode and a session starts
- **THEN** each block consists of 2 parts

#### Scenario: Hard mode

- **WHEN** the player picks the Hard mode and a session starts
- **THEN** each block consists of 1 part

### Requirement: Random block color

Each block SHALL get a random color (HSV: random hue, saturation and value in the pastel range) applied to the block mesh via a MaterialPropertyBlock.

#### Scenario: Colors are distinguishable

- **WHEN** a new block is generated
- **THEN** its color is random (distinguishable pastel shades)

### Requirement: Whole-block fall
A block the ball has fully left SHALL fall down (fall animation) and SHALL be removed after falling. All parts of a composite block SHALL fall and be removed together. Additionally, when the next block starts falling, all blocks behind it along the track (in generation order) SHALL start falling together with it - regardless of whether the ball traveled over them. Blocks ahead of the ball SHALL not fall. The delay before the fall SHALL come from the config. After the delay, the block SHALL accelerate downward from zero speed up to a cap defined in the config, instead of moving at a constant speed.

#### Scenario: Block falls in trail

- **WHEN** the ball rolls off the next block forward
- **THEN** the block left behind falls as a whole (all parts) and is then removed from the scene

#### Scenario: Tail falls behind the ball

- **WHEN** the ball rolls off some block
- **THEN** all blocks behind that block along the track also start falling, including blocks the ball never traveled over (for example, ones left aside after a turn)

#### Scenario: Block lives until the ball fully leaves it

- **WHEN** the ball travels over a block via any of its parts and rolls onto the next one
- **THEN** the block starts falling only after the ball has fully left it

#### Scenario: Blocks ahead of the ball do not fall

- **WHEN** the ball moves along the track and the tail behind it falls
- **THEN** all blocks from the one under the ball and ahead stay in place

#### Scenario: Fall acceleration

- **WHEN** the delay before a block's fall has expired
- **THEN** the block starts descending from zero speed and accelerates downward to the speed cap from the configuration

#### Scenario: Block does not fall under a standing ball

- **WHEN** the projection of the ball's center is within the block's footprint and the ball has not left it
- **THEN** the block is not marked as falling (including when the physics query sphere touches neighboring blocks)

### Requirement: Missing block parts

After the starting segment, the generator SHALL place holes (missing parts) on some blocks in any mode; each mode SHALL have its own configurable hole chance. A hole SHALL occupy one extreme part of a multi-part block (the first or the last along the laying axis, never a middle one); in a single-part mode, a hole means the whole block is absent.

#### Scenario: Hole occupies one extreme part

- **WHEN** the generator creates a multi-part block after the starting segment and the hole chance triggers
- **THEN** one extreme part of the block is absent (no mesh and no collider)

#### Scenario: Middle part is always intact

- **WHEN** the generator creates a holed multi-part block
- **THEN** the middle part of the block is present, a path through the block exists

#### Scenario: Hole in a single-part mode

- **WHEN** the generator creates a single-part block (Hard) and the hole chance triggers
- **THEN** the whole block is absent - a one-unit gap between intact blocks

#### Scenario: Holes in any mode

- **WHEN** a session runs in any mode (Easy, Normal, Hard) after the starting segment
- **THEN** holes may appear with the chance configured for that mode

#### Scenario: No holes in the starting segment

- **WHEN** the generator builds the blocks of the starting segment in any mode
- **THEN** all parts of the starting blocks are present

#### Scenario: Hole chance per mode

- **WHEN** hole frequency is tuned
- **THEN** each mode (Easy, Normal, Hard) has its own hole chance, set by a constant in GameplayConfig without code changes

### Requirement: Hole passability

A hole SHALL NOT appear on two consecutive blocks: the block following a holed one SHALL be intact. In a single-part mode, the block following a holed one SHALL be laid along the same step axis as the holed one: there SHALL be no turn immediately after the holed block, so that the landing of a straight jump over the gap lands on the next intact block. Any gap SHALL be jumpable.

#### Scenario: No holes on two consecutive blocks

- **WHEN** the previously built block got a hole
- **THEN** the next block is built whole (without a hole); two holed blocks never come in a row

#### Scenario: No turn after a hole in the single-part mode

- **WHEN** in a single-part mode (Hard) the previous block got a hole
- **THEN** the next block is laid along the same step axis as the holed one (a turn immediately after the gap is excluded), and a straight jump lands on it

#### Scenario: Turn after a hole in multi-part modes

- **WHEN** in a multi-part mode (Easy, Normal) the block after a holed one turns
- **THEN** this is allowed: the wide block leaves room to go around the hole via the neighboring part or to jump from its intact part

#### Scenario: Any gap is jumpable

- **WHEN** the ball jumps from a holed block (or the block before it) over the gap
- **THEN** the jump covers the gap and lands on the next intact block

### Requirement: Orientation-based block placement

The generator SHALL maintain a cursor - a cell stepping one unit along the world axis +X or +Z; the step axis SHALL be chosen randomly for each block. A block SHALL be laid in parts along the axis perpendicular to the step: part 0 - in the cursor's cell, each next one - a step back along the perpendicular axis (toward what is built). Step monotonicity SHALL exclude track self-intersection and block overlap by construction.

#### Scenario: Random step axis choice

- **WHEN** the generator places the next block outside the starting segment
- **THEN** the cursor step (+X or +Z) is chosen randomly, independent of previous steps

#### Scenario: Block part laying

- **WHEN** the cursor steps along axis S into cell C
- **THEN** the block occupies cells C, C−P, ..., C−(N−1)P, where P is the axis perpendicular to S, N is the mode's part count

#### Scenario: Overlaps are excluded by construction

- **WHEN** the generator places any block by the orientation rule
- **THEN** no block part intersects already existing blocks, without any overlap checks

### Requirement: Starting segment

The first startSegmentMin..startSegmentMax steps of a session SHALL run along the fixed axis +Z (soft start). The first block (the ball's spawn block) SHALL have no holes. After the starting segment, axis choice SHALL be purely random with no restrictions on the sequence.

#### Scenario: Soft start

- **WHEN** a game session starts in any mode
- **THEN** the first 5-7 blocks are laid along the +Z axis, and there is a margin of blocks ahead of the ball up to the target length

#### Scenario: Pure randomness after the start

- **WHEN** the starting segment is exhausted
- **THEN** the axis of each next step is chosen randomly, including any number of axis switches in a row
