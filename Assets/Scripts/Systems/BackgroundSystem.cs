using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Level-driven background: the sky renders as a vertical two-color
    /// gradient. The scene's RenderSettings.skybox material is the single
    /// source of truth (wired in the Editor); the system only animates its
    /// colors - in the Editor on a temporary copy, so the persistent
    /// material asset is never dirtied. Target colors derive
    /// from the current
    /// level: hue = frac(level * 0.618034), darker saturated zenith above a
    /// lighter pastel horizon (contrast with the random pastel blocks).
    /// On a level change the gradient glides smoothly to the new colors
    /// (duration from config, no flash); a re-target mid-glide continues
    /// from the current colors. Falling/GameOver override the target with
    /// the explicit red-black defeat colors from config through the same
    /// glide; restart/menu returns to the level scheme. The background
    /// breathes: the target hue itself drifts slowly (amplitude/period from
    /// config, amplitude 0 disables) and the colors are rebuilt from the
    /// drifting hue, so the tint lives without flickering brightness.
    /// Paused sessions freeze colors, glide and breathing phase; all other
    /// states keep gliding. The level value itself is the state: the event
    /// is left for UiSyncSystem (no system-order coupling).
    /// </summary>
    public class BackgroundSystem : IEcsInitSystem, IEcsRunSystem
    {
        private const float HueStep = 0.618034f;

        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");

        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _sessions;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<LevelComponent> _levelPool;

        private Material _material;
        private Color _currentTop;
        private Color _currentBottom;
        private Color _fromTop;
        private Color _fromBottom;
        private Color _targetTop;
        private Color _targetBottom;
        private int _lastLevel = -1;
        private bool _lastDefeat;
        private float _transitionTimer;
        private float _transitionDuration = 1f;
        private float _breatheTime;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _sessions = _world.Filter<GameStateComponent>().End();
            _statePool = _world.GetPool<GameStateComponent>();
            _levelPool = _world.GetPool<LevelComponent>();

            // The scene's skybox material (RenderSettings.skybox) is the
            // single source of truth, wired in the Editor; the system only
            // animates its colors. Skip updates (with an error) when the
            // skybox is missing or is not the gradient material. In the
            // Editor the system animates a temporary copy so per-frame
            // SetColor never dirties the persistent asset; Unity restores
            // the scene's original material on play mode exit. In a build
            // the asset only ever lives in memory, so it is used directly.
#if UNITY_EDITOR
            _material = Object.Instantiate(RenderSettings.skybox);
            RenderSettings.skybox = _material;
#else
            _material = RenderSettings.skybox;
#endif
            if (_material != null && !_material.HasProperty(TopColorId))
            {
                Debug.LogError("BackgroundSystem: RenderSettings.skybox material has no _TopColor property - assign the GradientSkybox material in the scene's Lighting settings. Background stays unchanged.");
                _material = null;
            }

            _transitionDuration = Mathf.Max(0.01f, _shared.PresentationConfig.backgroundTransitionDuration);
        }

        public void Run(IEcsSystems systems)
        {
            if (_material == null)
                return;

            var cfg = _shared.PresentationConfig;
            ref var state = ref _sessions.GetSingletonComponent(_statePool);

            // Frozen: no transition, no breathing while paused.
            bool paused = state.State == GameState.Paused;
            // Falling/GameOver turn the background red-black; a restart or
            // menu return resets the level, restoring the level hue.
            bool defeat = state.State == GameState.Falling
                          || state.State == GameState.GameOver;

            // Level (or defeat) change -> start a new glide from the
            // current colors (re-target mid-transition is safe).
            int level = _sessions.GetSingletonComponent(_levelPool).Value;
            if (level != _lastLevel || defeat != _lastDefeat)
            {
                _lastLevel = level;
                _lastDefeat = defeat;
                _fromTop = _currentTop;
                _fromBottom = _currentBottom;
                ComputeTargetColors(level, defeat);
                _transitionTimer = 0f;
            }

            if (!paused)
            {
                // Glide current -> target over the configured duration: a
                // normalized-duration lerp from the colors the glide started
                // at, so the transition completes exactly in duration.
                if (_transitionTimer < _transitionDuration)
                {
                    _transitionTimer = Mathf.Min(_transitionTimer + Time.deltaTime, _transitionDuration);
                    float t = _transitionTimer / _transitionDuration;
                    t = t * t * (3f - 2f * t); // smoothstep easing at the ends
                    _currentTop = Color.LerpUnclamped(_fromTop, _targetTop, t);
                    _currentBottom = Color.LerpUnclamped(_fromBottom, _targetBottom, t);
                }
                else if (!defeat)
                {
                    // Breathing: the target hue itself drifts slowly and the
                    // colors are rebuilt from it, so the tint lives without
                    // brightness flicker. Skipped during a glide (it owns the
                    // colors) and in defeat (fixed mood, no drift). The phase
                    // advances only while not paused.
                    _breatheTime += Time.deltaTime;
                    float hueShift = cfg.backgroundBreatheAmplitude
                                     * Mathf.Sin(2f * Mathf.PI * _breatheTime / Mathf.Max(0.1f, cfg.backgroundBreathePeriod));
                    float hue = Mathf.Repeat(_lastLevel * HueStep + hueShift, 1f);
                    _currentTop = Color.HSVToRGB(hue, cfg.backgroundZenithSaturation, cfg.backgroundZenithValue);
                    _currentBottom = Color.HSVToRGB(hue, cfg.backgroundHorizonSaturation, cfg.backgroundHorizonValue);
                }

                _material.SetColor(TopColorId, _currentTop);
                _material.SetColor(BottomColorId, _currentBottom);
            }
        }

        private void ComputeTargetColors(int level, bool defeat)
        {
            var cfg = _shared.PresentationConfig;
            if (defeat)
            {
                // Explicit red-black defeat mood from config.
                _targetTop = cfg.backgroundDefeatZenith;
                _targetBottom = cfg.backgroundDefeatHorizon;
                return;
            }
            float hue = Mathf.Repeat(level * HueStep, 1f);
            _targetTop = Color.HSVToRGB(hue, cfg.backgroundZenithSaturation, cfg.backgroundZenithValue);
            _targetBottom = Color.HSVToRGB(hue, cfg.backgroundHorizonSaturation, cfg.backgroundHorizonValue);
        }
    }
}
