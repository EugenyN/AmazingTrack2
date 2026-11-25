# ecs-singleton-access Specification

## Purpose

The LeoEcsLite singleton-entity access contract: exactly one session entity
exists, and all systems read it through the shared singleton-access helpers
that fail loudly on a missing or duplicated singleton instead of silently
taking the first match.

## Requirements

### Requirement: Session singleton access idiom

All systems SHALL access the session entity and its components
(GameStateComponent, ScoreComponent, LevelComponent) through the shared
singleton-access helpers operating on the cached session filter, instead of
per-system "iterate the filter and take the first entity" loops. A singleton
helper call SHALL return the component of the single matching entity and
SHALL support mutation through the returned reference.

#### Scenario: Systems read session state through the helper

- **WHEN** any system update reads the session state, score or level
- **THEN** it obtains them via the singleton-access helper on the cached
  session filter, and no first-match enumeration loop over the session
  filter remains in that system

#### Scenario: Session filter is cached

- **WHEN** the game runs any number of frames
- **THEN** no system constructs a session filter inside its per-frame update
  path; each system builds its session filter once at init

### Requirement: No per-frame session filter construction

The session filter SHALL be built once at system init. Systems SHALL NOT
construct a filter for the session component inside their per-frame update
paths.

#### Scenario: No inline session filters in update paths

- **WHEN** the game runs any number of frames
- **THEN** no system constructs a session filter inside its per-frame update
  path

### Requirement: Singleton helpers fail explicitly

A singleton-access helper SHALL throw an exception with an error message
naming the component type when the filtered component exists on zero
entities, and SHALL throw when it exists on more than one entity, rather
than returning a sentinel value or silently reading the first match.

#### Scenario: Missing singleton is reported

- **WHEN** a singleton-access helper is called while no entity carries the
  requested component
- **THEN** the call throws with an error message that names the component
  type

#### Scenario: Duplicated singleton is reported

- **WHEN** a second session entity is created while one already exists
- **THEN** the next singleton-access helper call throws with an error
  message that names the component type

### Requirement: Allocation-free singleton access

Singleton-access helper calls from per-frame system update paths SHALL not
allocate garbage: the helpers SHALL reuse the caller's cached filter and
pool references, returning a reference into existing component storage.

#### Scenario: Steady-state frame stays allocation-free

- **WHEN** systems read the session through the helpers on every frame of a
  running session
- **THEN** the profiler reports no garbage-collection allocations from the
  singleton-access path
