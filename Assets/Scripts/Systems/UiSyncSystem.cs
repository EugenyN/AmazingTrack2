using Leopotam.EcsLite;
using UnityEngine;
using AmazingTrack.UI;
using TMPro;

namespace AmazingTrack
{
    /// <summary>
    /// UI sync: switches screens by session state (menu, HUD, game over,
    /// pause), updates score/level/record texts by change events
    /// (presentation cache is kept here, not in model).
    /// </summary>
    public class UiSyncSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _sessions;
        private EcsFilter _scoreChanged;
        private EcsFilter _levelChanged;
        private EcsFilter _stateChanged;
        private EcsFilter _comboChanged;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsPool<LevelComponent> _levelPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<ComboComponent> _comboPool;
        private EcsPool<ScoreChangedEventComponent> _scoreChangedPool;
        private EcsPool<LevelChangedEventComponent> _levelChangedPool;
        private EcsPool<StateChangedEventComponent> _stateChangedPool;
        private EcsPool<ComboChangedEventComponent> _comboChangedPool;

        private GameState _lastState = (GameState)(-1);
        private GameMode _lastMode = (GameMode)(-1);
        private int _lastScore = -1;
        private int _lastLevel = -1;
        private int _lastHighScore = -1;
        private int _lastMaxCombo = -1;

        /// <summary>Yellow tint of the combo streak label.</summary>
        private static readonly Color ComboColor = new Color(1f, 0.85f, 0f);

        /// <summary>Red tint of the combo break label.</summary>
        private static readonly Color ComboBreakColor = new Color(1f, 0.2f, 0.2f);

        /// <summary>Text shown on the shared float label for a level-up.</summary>
        private const string LevelUpText = "Level up!";

        /// <summary>The shared float label is currently shown (animating or holding).</summary>
        private bool _floatVisible;

        /// <summary>Seconds since the last float-label event (drives float and hide).</summary>
        private float _floatAge;

        /// <summary>Start anchored position of the float label, cached on first use.</summary>
        private Vector2 _floatBasePos;

        /// <summary>Whether the float label start position was cached.</summary>
        private bool _floatBaseCached;

        /// <summary>Lifetime of the current show (level-up vs combo config value).</summary>
        private float _floatLifetime;

        public void Init(IEcsSystems systems)
        {
            _shared = systems.GetShared<SharedData>();
            _world = systems.GetWorld();
            _sessions = _world.Filter<GameStateComponent>().End();
            _scoreChanged = _world.Filter<ScoreChangedEventComponent>().End();
            _levelChanged = _world.Filter<LevelChangedEventComponent>().End();
            _stateChanged = _world.Filter<StateChangedEventComponent>().End();
            _comboChanged = _world.Filter<ComboChangedEventComponent>().End();
            _scorePool = _world.GetPool<ScoreComponent>();
            _levelPool = _world.GetPool<LevelComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _comboPool = _world.GetPool<ComboComponent>();
            _scoreChangedPool = _world.GetPool<ScoreChangedEventComponent>();
            _levelChangedPool = _world.GetPool<LevelChangedEventComponent>();
            _stateChangedPool = _world.GetPool<StateChangedEventComponent>();
            _comboChangedPool = _world.GetPool<ComboChangedEventComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            var ui = _shared.Ui;
            if (ui == null)
                return;

            // Consume change events and refresh texts.
            bool scoreChanged = false, levelChanged = false, stateChanged = false;
            int comboValue = 0;
            bool comboBreak = false;
            bool comboEvent = false;
            foreach (int e in _scoreChanged) { _scoreChangedPool.Del(e); scoreChanged = true; }
            foreach (int e in _levelChanged) { _levelChangedPool.Del(e); levelChanged = true; }
            foreach (int e in _stateChanged) { _stateChangedPool.Del(e); stateChanged = true; }
            foreach (int e in _comboChanged)
            {
                ref var evt = ref _comboChangedPool.Get(e);
                comboValue = evt.Value;
                comboBreak = evt.Break;
                comboEvent = true;
                _comboChangedPool.Del(e);
            }

            ref var score = ref _sessions.GetSingletonComponent(_scorePool);
            ref var level = ref _sessions.GetSingletonComponent(_levelPool);
            ref var state = ref _sessions.GetSingletonComponent(_statePool);
            ref var combo = ref _sessions.GetSingletonComponent(_comboPool);

            if (scoreChanged || score.Value != _lastScore)
            {
                _lastScore = score.Value;
                if (ui.ScoreText != null)
                    ui.ScoreText.text = $"Score: {score.Value}";
            }

            // High score of the selected mode: refresh menu and HUD labels
            // only when the value changes (mode switch, fall save); building
            // the string every frame would allocate.
            if (score.HighScore != _lastHighScore)
            {
                _lastHighScore = score.HighScore;
                if (ui.MenuHighScoreText != null)
                    ui.MenuHighScoreText.text = $"High Score: {score.HighScore}";
                if (ui.HudHighScoreText != null)
                    ui.HudHighScoreText.text = $"High Score: {score.HighScore}";
            }
            if (levelChanged || level.Value != _lastLevel)
            {
                _lastLevel = level.Value;
                if (ui.LevelText != null)
                    ui.LevelText.text = $"Level: {level.Value}";
            }

            // Max combo of the session: hidden while the streak never reached
            // 1 (no crystals yet), shown from the first combo. String built
            // only on change, like the score above.
            if (combo.Max != _lastMaxCombo)
            {
                _lastMaxCombo = combo.Max;
                if (ui.MaxComboText != null)
                {
                    bool visible = combo.Max > 0;
                    ui.MaxComboText.gameObject.SetActive(visible);
                    if (visible)
                        ui.MaxComboText.text = $"Max Combo: {combo.Max}";
                }
            }

            if (stateChanged || state.State != _lastState)
            {
                _lastState = state.State;
                bool menu = state.State == GameState.Menu;
                bool over = state.State == GameState.GameOver;
                bool paused = state.State == GameState.Paused;
                bool playing = state.State == GameState.Playing
                               || state.State == GameState.Falling;
                ui.MenuScreen.SetActive(menu);
                ui.HudScreen.SetActive(playing);
                ui.GameOverScreen.SetActive(over);
                if (ui.PauseScreen != null)
                    ui.PauseScreen.SetActive(paused);
                // The combo label never survives a state change: hidden on
                // leaving play (menu/game over) and on entering it (the
                // scene may hold an active placeholder).
                ResetFloatLabel();
                if (over)
                {
                    if (ui.FinalScoreText != null)
                        ui.FinalScoreText.text = $"Score: {score.Value}";
                    if (ui.FinalHighScoreText != null)
                        ui.FinalHighScoreText.text = $"Record: {score.HighScore}";
                    if (ui.FinalComboText != null)
                        ui.FinalComboText.text = $"Max Combo: {combo.Max}";
                    if (ui.NewRecordText != null)
                        ui.NewRecordText.gameObject.SetActive(score.NewRecord);
                }
            }

            // Selected mode button highlight: refresh on mode change only
            // (and on the first menu frame). No allocations.
            if (state.Mode != _lastMode)
            {
                _lastMode = state.Mode;
                if (state.State == GameState.Menu)
                    ui.SetSelectedMode(state.Mode);
            }

            // Shared float label (FloatText): on an event (re)start the
            // presentation - text, color, snap to the cached start
            // position, visible; then per frame float up linearly and hide
            // after the lifetime. The update is skipped while Paused
            // (animation and hide timer freeze) and outside play the label
            // is hidden on state change. Combo and level-up events share
            // the label; the last event wins.
            var cfg = _shared.PresentationConfig;
            if (levelChanged)
                ShowFloatLabel(ui, LevelUpText, Color.white, cfg.levelUpFloatLifetime);
            if (comboEvent)
                ShowFloatLabel(ui, comboBreak ? "Combo break" : $"{comboValue} combo",
                    comboBreak ? ComboBreakColor : ComboColor, cfg.comboLabelLifetime);

            if (_floatVisible && state.State != GameState.Paused)
            {
                _floatAge += Time.deltaTime;
                var text = ui.FloatText;
                if (text != null)
                {
                    float t = Mathf.Clamp01(_floatAge / cfg.comboFloatDuration);
                    var pos = _floatBasePos + Vector2.up * (cfg.comboFloatDistance * t);
                    text.rectTransform.anchoredPosition = pos;
                }
                if (_floatAge >= _floatLifetime)
                    HideFloatLabel();
            }
        }

        /// <summary>
        /// (Re)starts the shared float label presentation: the given text
        /// and color, snapped to the cached start position and visible.
        /// The base position is cached once on first use (HUD layout is
        /// static during play). A new event while visible restarts the
        /// show from the base.
        /// </summary>
        private void ShowFloatLabel(UiRoot ui, string text, Color color, float lifetime)
        {
            var label = ui.FloatText;
            if (label == null)
                return;
            if (!_floatBaseCached)
            {
                _floatBasePos = label.rectTransform.anchoredPosition;
                _floatBaseCached = true;
            }
            label.text = text;
            label.color = color;
            label.rectTransform.anchoredPosition = _floatBasePos;
            label.gameObject.SetActive(true);
            _floatVisible = true;
            _floatAge = 0f;
            _floatLifetime = lifetime;
        }

        /// <summary>Hides the float label once its lifetime has elapsed.</summary>
        private void HideFloatLabel()
        {
            _floatVisible = false;
            if (_shared.Ui != null && _shared.Ui.FloatText != null)
                _shared.Ui.FloatText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Hides the float label and resets its presentation state (used on
        /// session state changes: the label never survives outside play).
        /// </summary>
        private void ResetFloatLabel()
        {
            HideFloatLabel();
            _floatAge = 0f;
        }
    }
}
