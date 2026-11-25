using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Sound: one AudioSource, clips from PresentationConfig. Consumes
    /// SoundEventComponent entities (one-frame events).
    /// </summary>
    public class AudioSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _sounds;
        private EcsPool<SoundEventComponent> _soundPool;

        public void Init(IEcsSystems systems)
        {
            _shared = systems.GetShared<SharedData>();
            _world = systems.GetWorld();
            _sounds = _world.Filter<SoundEventComponent>().End();
            _soundPool = _world.GetPool<SoundEventComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            if (_shared.Audio == null)
                return;
            var cfg = _shared.PresentationConfig;

            foreach (int entity in _sounds)
            {
                switch (_soundPool.Get(entity).Id)
                {
                    case Events.SoundId.Turn:
                        _shared.Audio.PlayOneShot(cfg.turnClip);
                        break;
                    case Events.SoundId.GameStart:
                        _shared.Audio.PlayOneShot(cfg.gameStartClip);
                        break;
                    case Events.SoundId.NextLevel:
                        _shared.Audio.PlayOneShot(cfg.nextLevelClip);
                        break;
                    case Events.SoundId.Fall:
                        _shared.Audio.PlayOneShot(cfg.fallClip);
                        break;
                    case Events.SoundId.Crystal:
                        _shared.Audio.PlayOneShot(cfg.crystalClip);
                        break;
                    case Events.SoundId.Jump:
                        _shared.Audio.PlayOneShot(cfg.jumpClip);
                        break;
                }
                _soundPool.Del(entity);
            }
        }
    }
}
