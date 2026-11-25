# camera-framing Specification

## Purpose

Defines how the camera adapts its orthographic size to the screen aspect ratio so the generated track stays visible across screen orientations, on top of the fixed isometric follow framing.

## Requirements

### Requirement: Aspect-driven orthographic size
The camera SHALL compute its orthographic size every frame from the screen aspect ratio (screen width / screen height) so that the visible track area adapts to the current orientation. All parameters (landscape size, portrait size, aspect thresholds) SHALL come from the game configuration.

#### Scenario: Landscape screen uses landscape size
- **WHEN** the screen aspect ratio is greater than or equal to the landscape aspect threshold
- **THEN** the camera orthographic size equals the configured landscape size (default 6)

#### Scenario: Portrait screen uses portrait size
- **WHEN** the screen aspect ratio is less than or equal to the portrait aspect threshold
- **THEN** the camera orthographic size equals the configured portrait size (default 8)

#### Scenario: Intermediate aspect interpolates
- **WHEN** the screen aspect ratio lies strictly between the portrait and landscape thresholds
- **THEN** the camera orthographic size is a continuous interpolation between the portrait and landscape sizes

### Requirement: Runtime orientation changes
The camera SHALL update its orthographic size during play when the screen dimensions change (device rotation or window resize), without restarting the session or reloading the scene.

#### Scenario: Device rotates mid-session
- **WHEN** the screen orientation changes while a session is active
- **THEN** the camera orthographic size reflects the new aspect ratio within the next frame

### Requirement: Allocation-free size update
The per-frame orthographic size computation SHALL NOT allocate memory (no collections, no boxing, no string operations).

#### Scenario: Extended play with frequent rotation
- **WHEN** the game runs with orientation switches repeated during play
- **THEN** no garbage collection spikes are caused by the camera size update
