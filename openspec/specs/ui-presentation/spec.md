# ui-presentation Specification

## Purpose

Keeps the on-screen UI (HUD, menu, pause, game-over screens) visually consistent across device orientations, aspect ratios, and resolutions: text and controls keep a stable, readable size whether the screen is portrait or landscape, and screen labels (texts outside buttons) stay horizontally centered in their areas.

## Requirements

### Requirement: Unified UI scale in portrait and landscape orientation

UI SHALL scale so that when the screen rotates between portrait (1080×1920) and landscape (1920×1080) orientation at the base resolution of 1080×1920, the effective UI scale stays the same (≈1.0) instead of being driven by screen width alone. Font and button sizes on the HUD, menu, pause, and results screens SHALL stay visually stable across orientations; the layout SHALL stretch correctly over the available width/height in both orientations.

#### Scenario: Scale in portrait

- **WHEN** the game is displayed in portrait orientation 1080×1920
- **THEN** the effective UI scale is 1.0 relative to the reference resolution

#### Scenario: Scale in landscape

- **WHEN** the screen rotates to landscape orientation 1920×1080
- **THEN** the effective UI scale stays ≈1.0 (it does not grow to ≈1.78 as with width-only scaling)

#### Scenario: Stable label size on rotation

- **WHEN** during gameplay or in the menu the device rotates from portrait to landscape
- **THEN** labels (score, level, headings, buttons) keep practically the same visible font size, and their placement stays aligned to their areas

#### Scenario: Label alignment does not regress

- **WHEN** the new UI scale is applied
- **THEN** the alignment from the "Screen label alignment" requirement (centering of menu and results labels) is preserved in both orientations

### Requirement: Screen label alignment

Labels (texts that are not captions inside buttons) on the menu and results screens SHALL be horizontally centered relative to their areas: no label of these screens SHALL be displayed with a significant horizontal offset from the center of its area, regardless of the window's resolution and aspect ratio. The observed rightward shift of most menu and results-screen labels SHALL be eliminated.

#### Scenario: Menu labels centered

- **WHEN** the game is in the menu at common window aspect ratios (16:9, portrait Game view)
- **THEN** the title and the high-score line are horizontally centered on the screen, without a rightward shift

#### Scenario: Results screen labels centered

- **WHEN** the results screen is open
- **THEN** the score line, high-score line, and new-record marker are centered without a rightward shift

#### Scenario: Buttons unaffected

- **WHEN** the labels are fixed
- **THEN** button captions on these screens stay in place and remain clickable
