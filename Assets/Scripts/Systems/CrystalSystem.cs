using System.Collections.Generic;
using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Crystal pickup, split by ball state into two paths:
    /// - Ground path (grounded ball, existing filter): positional XZ check
    ///   against the ball's ground block, collecting REGULAR crystals only
    ///   (golden ones are skipped - the ball rolls beneath them).
    /// - Air path (jumping ball, BallJumpingTag filter): full 3D distance
    ///   between the ball's actual position (logical position plus the jump
    ///   arc height) and the crystal, collecting GOLDEN crystals only. No
    ///   ground-block binding: in flight the ground is frozen to the takeoff
    ///   block while the logical position travels, so golden crystals on the
    ///   takeoff block, under the arc's peak and beyond a hole are all
    ///   reachable. The geometric check makes a too-early/too-late jump miss.
    /// A picked crystal is consumed: variant points, score and sound events,
    /// plus a one-shot pooled particle effect at the crystal's position (in
    /// the air for golden ones). The entity is deleted, so a crystal cannot
    /// be picked twice.
    /// </summary>
    public class CrystalSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _groundBalls;
        private EcsFilter _airBalls;
        private EcsFilter _crystals;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BallJumpComponent> _jumpPool;
        private EcsPool<CrystalComponent> _crystalPool;
        private EcsPool<GoldenCrystalTag> _goldenPool;
        private EcsPool<ViewLinkComponent> _viewPool;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsPool<ScoreChangedEventComponent> _scoreChangedPool;
        private EcsPool<ComboComponent> _comboPool;
        private EcsPool<ComboChangedEventComponent> _comboChangedPool;
        private EcsPool<SoundEventComponent> _soundPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _sessions;

        /// <summary>Reusable pickup list: cleared per ball, never reallocated.</summary>
        private readonly List<int> _doomed = new List<int>(8);

        /// <summary>Cached FX completion callback: releases the instance back to the pool.</summary>
        private readonly System.Action<Views.PooledFxView> _fxRelease;

        public CrystalSystem()
        {
            _fxRelease = fx => _shared.FxPool.Release(fx);
        }

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            // Ground path: grounded balls pick up regular crystals only.
            _groundBalls = _world.Filter<BallComponent>().Exc<BallFallingTag>().Exc<BallJumpingTag>().End();
            // Air path: jumping balls pick up golden crystals only.
            _airBalls = _world.Filter<BallComponent>().Inc<BallJumpingTag>().Exc<BallFallingTag>().End();
            _crystals = _world.Filter<CrystalComponent>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _jumpPool = _world.GetPool<BallJumpComponent>();
            _crystalPool = _world.GetPool<CrystalComponent>();
            _goldenPool = _world.GetPool<GoldenCrystalTag>();
            _viewPool = _world.GetPool<ViewLinkComponent>();
            _scorePool = _world.GetPool<ScoreComponent>();
            _scoreChangedPool = _world.GetPool<ScoreChangedEventComponent>();
            _comboPool = _world.GetPool<ComboComponent>();
            _comboChangedPool = _world.GetPool<ComboChangedEventComponent>();
            _soundPool = _world.GetPool<SoundEventComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return;

            PickRegularOnGround();
            PickGoldenInAir();
        }

        /// <summary>
        /// Ground path: a grounded ball picks up regular crystals on its
        /// ground block within the XZ pickup radius. Golden crystals are
        /// skipped unconditionally (the ball passes beneath them, whatever
        /// the height/radius tuning).
        /// </summary>
        private void PickRegularOnGround()
        {
            foreach (int ballEntity in _groundBalls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                if (!ball.Ground.Unpack(_world, out int groundEntity))
                    continue;

                // Crystals on the ball's ground block, within pickup radius.
                // Reusable list: cleared per ball, never reallocated.
                _doomed.Clear();
                foreach (int crystalEntity in _crystals)
                {
                    ref var crystal = ref _crystalPool.Get(crystalEntity);
                    if (_goldenPool.Has(crystalEntity))
                        continue; // golden: jump-only pickup, the ball passes beneath
                    if (!crystal.Block.Unpack(_world, out int crystalBlock)
                        || crystalBlock != groundEntity)
                        continue;

                    var dx = ball.Pos.x - crystal.Pos.x;
                    var dz = ball.Pos.z - crystal.Pos.z;
                    float pickupRadius = _shared.GameplayConfig.crystalPickupRadius;
                    if (dx * dx + dz * dz <= pickupRadius * pickupRadius)
                        _doomed.Add(crystalEntity);
                }

                ConsumePicked(_doomed, _shared.GameplayConfig.crystalScore);
            }
        }

        /// <summary>
        /// Air path: a jumping ball picks up golden crystals by the full 3D
        /// distance between its actual position (logical position plus the
        /// jump arc height, same formula the view renders) and the crystal,
        /// within the golden pickup radius. No ground-block binding.
        /// </summary>
        private void PickGoldenInAir()
        {
            var cfg = _shared.GameplayConfig;
            float radius = cfg.goldenCrystalPickupRadius;

            foreach (int ballEntity in _airBalls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                if (!_jumpPool.Has(ballEntity))
                    continue;
                ref var jump = ref _jumpPool.Get(ballEntity);

                // Actual position: logic plane plus the rendered arc height.
                var ballPos = ball.Pos + Vector3.up * jump.GetHeight(cfg.jumpHeight);

                _doomed.Clear();
                foreach (int crystalEntity in _crystals)
                {
                    if (!_goldenPool.Has(crystalEntity))
                        continue; // regular crystals are ground-only pickups
                    ref var crystal = ref _crystalPool.Get(crystalEntity);

                    var dx = ballPos.x - crystal.Pos.x;
                    var dy = ballPos.y - crystal.Pos.y;
                    var dz = ballPos.z - crystal.Pos.z;
                    if (dx * dx + dy * dy + dz * dz <= radius * radius)
                        _doomed.Add(crystalEntity);
                }

                ConsumePicked(_doomed, cfg.goldenCrystalScore);
            }
        }

        /// <summary>
        /// Consumes the picked crystals: one-shot pooled particle burst at
        /// the crystal's position tinted to the variant's material color,
        /// view released to its variant's own pool, entity deleted, variant
        /// points multiplied by the combo multiplier of the grown streak
        /// (the crystal reaching a tier is awarded with that tier's
        /// multiplier), score sync, combo changed and sound events. Shared
        /// by both pickup paths.
        /// </summary>
        private void ConsumePicked(List<int> picked, int points)
        {
            foreach (int crystalEntity in picked)
            {
                // One-shot pooled particle burst at the crystal's position
                // (before removing the entity), tinted to the variant.
                SpawnPickupFx(crystalEntity, ref _crystalPool.Get(crystalEntity));

                // Release the view to its variant's pool, then remove the
                // entity.
                if (_viewPool.Has(crystalEntity))
                {
                    var view = _viewPool.Get(crystalEntity).View;
                    if (view != null)
                        ReleaseView(crystalEntity, view);
                    _viewPool.Del(crystalEntity);
                }
                _world.DelEntity(crystalEntity);

                // Grow the streak, award the variant points scaled by the
                // multiplier of the new streak value (combo is only shown
                // from 2 - the first crystal has no label and scores base).
                ref var combo = ref _sessions.GetSingletonComponent(_comboPool);
                combo.Value++;
                if (combo.Value > combo.Max)
                    combo.Max = combo.Value;
                _sessions.GetSingletonComponent(_scorePool).Value +=
                    points * _shared.GameplayConfig.ComboMultiplier(combo.Value);
                _scoreChangedPool.Add(_world.NewEntity());
                if (combo.Value >= 2)
                    _comboChangedPool.Add(_world.NewEntity()).Value = combo.Value;
                _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.Crystal;
            }
        }

        /// <summary>
        /// Releases a crystal view to its own variant's pool by tag: golden
        /// views go to the golden pool, regular ones to the regular pool.
        /// Every release path must go through this (a view released into the
        /// wrong pool would corrupt that pool's identity).
        /// </summary>
        private void ReleaseView(int crystalEntity, GameObject view)
        {
            if (_goldenPool.Has(crystalEntity))
                _shared.GoldenCrystalPool.Release(view);
            else
                _shared.CrystalPool.Release(view);
        }

        /// <summary>
        /// Spawns the one-shot pickup effect at the crystal's position from
        /// the shared FX pool: plays from the beginning, unparented from the
        /// pool root so a scene teardown cannot kill it mid-play; the
        /// PooledFxView releases it back when playback ends. The burst is
        /// tinted with the picked crystal's variant material (golden for
        /// golden crystals). The pool hands out the typed component, so
        /// there is no per-spawn GetComponent; the completion callback is
        /// cached once per system.
        /// </summary>
        private void SpawnPickupFx(int crystalEntity, ref CrystalComponent crystal)
        {
            if (_shared.PresentationConfig.crystalPickupFxPrefab == null)
                return;
            var fx = _shared.FxPool.Get();
            fx.transform.SetParent(null, false);
            fx.transform.position = crystal.Pos;
            fx.transform.rotation = Quaternion.identity;
            fx.Play(_fxRelease, _goldenPool.Has(crystalEntity)
                ? _shared.GoldenCrystalFxMaterial
                : _shared.CrystalFxMaterial);
        }
    }
}
