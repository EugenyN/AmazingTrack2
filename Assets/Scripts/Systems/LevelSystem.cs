using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Level from score: level n is reached at ScoreForLevel(n) points
    /// (scorePerLevel * n^2 / 4, ladder steps growing every two levels).
    /// A level change raises a sound event and a UI event.
    /// </summary>
    public class LevelSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _sessions;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsPool<LevelComponent> _levelPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<SoundEventComponent> _soundPool;
        private EcsPool<LevelChangedEventComponent> _levelChangedPool;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _sessions = _world.Filter<GameStateComponent>().End();
            _scorePool = _world.GetPool<ScoreComponent>();
            _levelPool = _world.GetPool<LevelComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _soundPool = _world.GetPool<SoundEventComponent>();
            _levelChangedPool = _world.GetPool<LevelChangedEventComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            if (_sessions.GetSingletonComponent(_statePool).State != GameState.Playing)
                return;

            ref var score = ref _sessions.GetSingletonComponent(_scorePool);
            ref var levelComp = ref _sessions.GetSingletonComponent(_levelPool);
            // Bounded ladder walk from the current level: exact integer
            // thresholds, usually 0-1 iterations per frame, no allocations.
            int level = levelComp.Value;
            while (score.Value >= _shared.GameplayConfig.ScoreForLevel(level + 1))
                level++;
            if (level != levelComp.Value)
            {
                levelComp.Value = level;
                _levelChangedPool.Add(_world.NewEntity());
                _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.NextLevel;
            }
        }
    }
}
