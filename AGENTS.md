# AGENTS.md

Amazing Track 2 — Unity 6 (6000.7.x) arcade game: ball runs along a generated
track, turns on input, collects crystals. Runtime logic is LeoEcsLite ECS.
All feature work is driven by OpenSpec.

## Workflow: OpenSpec first

- New feature or behavior change -> `/openspec-propose`. Do not implement
  features directly outside an OpenSpec change.
- Implement an approved change -> `/openspec-apply-change`; finalize ->
  `/openspec-archive-change`; open questions -> `/openspec-explore`.
- Game behavior specs live in `openspec/specs/` — read the relevant spec
  before changing behavior; AGENTS.md does not document game behavior.
- Before writing any OpenSpec artifact, read `openspec/config.yaml` —
  its `context` and `rules` are binding.

## Unity rules

- NEVER hand-edit `.unity`, `.prefab`, `.asset` YAML files. Change the scene
  through the live Unity Editor (unity MCP tools / unity CLI pipeline) or give
  the user explicit manual Editor steps.
- Generated, never edit or commit: `Library/`, `Temp/`, `obj/`, `Logs/`.

## Code conventions

- LeoEcsLite: systems iterate entities via EcsFilter + pools
  (`world.Filter<T>().End()`, `world.GetPool<T>()`); no god objects.
- LeoEcsLite filters that are built every frame or frequently (per-frame
  `Run` paths and helpers called from them) are cached in fields built once
  in `Init` and reused. Inline `world.Filter<T>()...End()` is fine for
  rare paths (state transitions, one-off queries).
- Naming: systems `*System`; components `*Component`/`*Tag`; one-frame
  events `*EventComponent`.
- Static helper classes (pure-function utilities, no instance state) end in
  `Helper` (e.g. `BlockFootprintHelper`); extension-method containers keep
  the `*Extensions` convention. Code layout: `Scripts/Systems/`,
  `Scripts/Components/`, `Scripts/Helpers/`, `Scripts/Configs/`; all
  namespaces are flat `AmazingTrack`.
- Pooling: views for prefab-backed entities (Block, Crystal, pickup FX) are
  acquired/released via `UnityEngine.Pool.ObjectPool<T>` pools hosted in
  `SharedData`; never `Instantiate`/`Destroy` these views during play.
- Allocations: no GC allocations during play more often than every 0.5 s;
  nothing allocates inside per-frame `Run(IEcsSystems)` paths — reuse
  pre-allocated buffers/fields, no per-frame collection creation, no
  per-spawn GetComponent/GetComponentsInChildren/AddComponent/new on the
  hot path.
- All in-game texts, code comments, XML docs: English.

## Workflow rules

- Do not commit unless explicitly asked to.
- Do not run Play Mode checks unless explicitly asked to.

## Verification

- No CLI test suite. Verify by compiling in Unity Editor and exercising the
  feature in Play Mode. Finish by listing what to check in Play Mode.
