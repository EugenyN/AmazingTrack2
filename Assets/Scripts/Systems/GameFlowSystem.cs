using Leopotam.EcsLite;
using UnityEngine;
using System.Collections.Generic;

namespace AmazingTrack
{
    /// <summary>
    /// Session state machine: Menu -> (Play event) -> Playing <->
    /// Paused (ESC/Resume toggle) -> Playing -> (no ground) -> Falling ->
    /// (delay) -> GameOver -> (Play Again button) -> Playing.
    /// Consumes UI request events, pause toggles and taps; owns the session
    /// singleton. Menu mode buttons only select the mode (the menu preview
    /// track rebuilds); Play starts a session on the already-built preview
    /// track. While Paused, in Menu, while Falling or on the Game Over
    /// screen, taps are deleted so they never leak into BallTurnSystem or
    /// the restart path (a tap during the fall must not restart the session
    /// the frame the results screen appears; on the results screen a tap
    /// must not restart either - restart lives on the Play Again button).
    /// </summary>
    public class GameFlowSystem : IEcsInitSystem, IEcsRunSystem
    {
        private EcsWorld _world;
        private SharedData _shared;
        private EcsFilter _selectModeEvents;
        private EcsFilter _playEvents;
        private EcsFilter _goMenuEvents;
        private EcsFilter _playAgainEvents;
        private EcsFilter _pauseToggles;
#if UNITY_ANDROID && !UNITY_EDITOR
        private EcsFilter _quitEvents;
#endif
        private EcsFilter _taps;
        private EcsFilter _balls;
        private EcsFilter _blocks;
        private EcsFilter _crystals;
        private EcsPool<SelectModeEventComponent> _selectModePool;
        private EcsPool<PlayEventComponent> _playPool;
        private EcsPool<GoMenuEventComponent> _goMenuPool;
        private EcsPool<PlayAgainEventComponent> _playAgainPool;
        private EcsPool<PauseToggleEventComponent> _pauseTogglePool;
#if UNITY_ANDROID && !UNITY_EDITOR
        private EcsPool<QuitAppEventComponent> _quitPool;
#endif
        private EcsPool<TapEventComponent> _tapPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsPool<LevelComponent> _levelPool;
        private EcsPool<ComboComponent> _comboPool;
        private EcsPool<SoundEventComponent> _soundPool;
        private EcsPool<StateChangedEventComponent> _stateChangedPool;
        private EcsPool<BallFallingTag> _ballFallingPool;
        private EcsPool<TrackResetEventComponent> _trackResetPool;
        private BallSpawnSystem _ballSpawn;
        private int _session;

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();
            _ballSpawn = _shared.GetSystem<BallSpawnSystem>();

            _selectModeEvents = _world.Filter<SelectModeEventComponent>().End();
            _playEvents = _world.Filter<PlayEventComponent>().End();
            _goMenuEvents = _world.Filter<GoMenuEventComponent>().End();
            _playAgainEvents = _world.Filter<PlayAgainEventComponent>().End();
            _pauseToggles = _world.Filter<PauseToggleEventComponent>().End();
#if UNITY_ANDROID && !UNITY_EDITOR
            _quitEvents = _world.Filter<QuitAppEventComponent>().End();
#endif
            _taps = _world.Filter<TapEventComponent>().End();
            _balls = _world.Filter<BallComponent>().End();
            _blocks = _world.Filter<BlockComponent>().End();
            _crystals = _world.Filter<CrystalComponent>().End();
            _selectModePool = _world.GetPool<SelectModeEventComponent>();
            _playPool = _world.GetPool<PlayEventComponent>();
            _goMenuPool = _world.GetPool<GoMenuEventComponent>();
            _playAgainPool = _world.GetPool<PlayAgainEventComponent>();
            _pauseTogglePool = _world.GetPool<PauseToggleEventComponent>();
#if UNITY_ANDROID && !UNITY_EDITOR
            _quitPool = _world.GetPool<QuitAppEventComponent>();
#endif
            _tapPool = _world.GetPool<TapEventComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _scorePool = _world.GetPool<ScoreComponent>();
            _levelPool = _world.GetPool<LevelComponent>();
            _comboPool = _world.GetPool<ComboComponent>();
            _soundPool = _world.GetPool<SoundEventComponent>();
            _stateChangedPool = _world.GetPool<StateChangedEventComponent>();
            _ballFallingPool = _world.GetPool<BallFallingTag>();
            _trackResetPool = _world.GetPool<TrackResetEventComponent>();

            // Session singleton: state + score + level + combo on one entity.
            _session = _world.NewEntity();
            _statePool.Add(_session);
            _scorePool.Add(_session);
            _levelPool.Add(_session);
            _comboPool.Add(_session);
            ref var state = ref _statePool.Get(_session);
            state.State = GameState.Menu;
            state.Mode = GameMode.Normal;
            ref var score = ref _scorePool.Get(_session);
            score.HighScore = PlayerPrefs.GetInt(PrefsHelper.HighScoreKey(GameMode.Normal), 0);
            _levelPool.Get(_session).Value = 1;
        }

        public void Run(IEcsSystems systems)
        {
            // Menu mode selection: switch the preview track's mode, no
            // session start. A click on the already-selected mode is a no-op.
            foreach (int e in _selectModeEvents)
            {
                var mode = _selectModePool.Get(e).Mode;
                _selectModePool.Del(e);
                if (_statePool.Get(_session).State != GameState.Menu)
                    continue;
                ref var sessionState = ref _statePool.Get(_session);
                if (sessionState.Mode == mode)
                    continue;
                sessionState.Mode = mode;

                // Records are per mode: switch the menu to the newly
                // selected mode's record (spec scoring).
                _scorePool.Get(_session).HighScore = PlayerPrefs.GetInt(PrefsHelper.HighScoreKey(mode), 0);

                ResetProgress();
                // The standing preview ball is re-placed on the new track's
                // first block: destroy it, BallSpawnSystem (Menu) respawns
                // it once the generator rebuilt the track.
                DestroyBall();
                ClearTrack();
                ClearCrystals();
                _trackResetPool.Add(_world.NewEntity());
            }

            // Play: start a session on the already-built preview track (no
            // track cleanup, no generator reset).
            foreach (int e in _playEvents)
            {
                _playPool.Del(e);
                if (_statePool.Get(_session).State == GameState.Menu)
                    PlayFromMenu();
            }

            foreach (int e in _goMenuEvents)
            {
                _goMenuPool.Del(e);
                if (_statePool.Get(_session).State != GameState.Menu)
                    GoToMenu();
            }

            // Pause toggle (the Pause action - ESC/P/gamepad Start - or the
            // Continue button): Playing <-> Paused without touching any
            // session component (score/level/ball/track stay exactly as they
            // were).
            foreach (int e in _pauseToggles)
            {
                _pauseTogglePool.Del(e);
                ref var s = ref _statePool.Get(_session);
                if (s.State == GameState.Playing)
                    SetState(GameState.Paused);
                else if (s.State == GameState.Paused)
                    SetState(GameState.Playing);
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Quit request (Android back gesture in the menu): UserInputSystem
            // emits it only in the Menu state, so no session state is touched.
            foreach (int e in _quitEvents)
            {
                _quitPool.Del(e);
                Application.Quit();
            }
#endif

            // While Paused, in Menu, while Falling or on the Game Over
            // screen every tap is discarded: it must not turn the ball
            // (BallTurnSystem), restart the session (restart lives on the
            // Play Again button now) nor accumulate until a session starts.
            // Falling matters especially: taps made during the fall must not
            // sit in the world and fire an instant restart the frame the
            // state becomes GameOver - that would skip the results screen.
            // GameOver matters too: the results screen must stay open until
            // the player presses Play Again.
            if (_statePool.Get(_session).State == GameState.Paused
                || _statePool.Get(_session).State == GameState.Menu
                || _statePool.Get(_session).State == GameState.Falling
                || _statePool.Get(_session).State == GameState.GameOver)
            {
                foreach (int tapEntity in _taps)
                    _tapPool.Del(tapEntity);
            }

            // Tap in GameOver no longer restarts the session: restart lives
            // on the Play Again button (PlayAgainEventComponent below).
            // NOTE: taps are consumed here ONLY in GameOver; in Playing the
            // tap is left for BallTurnSystem (later in the pipeline).
            foreach (int e in _playAgainEvents)
            {
                _playAgainPool.Del(e);
                if (_statePool.Get(_session).State == GameState.GameOver)
                    StartSession(_statePool.Get(_session).Mode);
            }

            // Falling: wait out the delay, then GameOver.
            ref var st = ref _statePool.Get(_session);
            if (st.State == GameState.Falling)
            {
                st.FallTimer += Time.deltaTime;
                if (st.FallTimer >= _shared.PresentationConfig.gameOverDelay)
                    SetState(GameState.GameOver);
            }
        }

        /// <summary>
        /// Starts a session from the menu on the already-built preview track:
        /// no track cleanup and no generator reset - generation continues
        /// from the current cursor. The menu's standing ball starts rolling
        /// immediately: its BallWaitingTag is removed (or, when no live ball
        /// exists yet - rare frame race -, the next BallSpawnSystem spawn in
        /// Playing is one-shotted to spawn without the waiting tag).
        /// </summary>
        private void PlayFromMenu()
        {
            ResetProgress();
            StartMenuBall();
            ConsumeTaps();
            SetState(GameState.Playing);
            _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.GameStart;
        }

        /// <summary>
        /// Hands the menu ball over to the session: removes BallWaitingTag
        /// so BallMoveSystem integrates it the same frame. When no ball
        /// exists (Play clicked before the first spawn), arms the spawn
        /// system's one-shot so the upcoming Playing spawn rolls at once.
        /// </summary>
        private void StartMenuBall()
        {
            var waitingPool = _world.GetPool<BallWaitingTag>();
            if (_balls.GetEntitiesCount() > 0)
            {
                foreach (int ballEntity in _balls)
                    waitingPool.Del(ballEntity);
            }
            else
            {
                _ballSpawn.SpawnWithoutWaiting = true;
            }
        }

        /// <summary>Starts a new session in the given mode (full reset).</summary>
        private void StartSession(GameMode mode)
        {
            ref var st = ref _statePool.Get(_session);
            st.Mode = mode;

            ResetProgress();
            DestroyBall();
            ClearTrack();
            ClearCrystals();

            // Reset the generator cursor: next block is the session start.
            _trackResetPool.Add(_world.NewEntity());

            ConsumeTaps();
            SetState(GameState.Playing);
            _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.GameStart;
        }

        /// <summary>Ball fell (called by FallCheckSystem via BeginFall).</summary>
        public void BeginFall()
        {
            ref var st = ref _statePool.Get(_session);
            if (st.State != GameState.Playing)
                return;

            SetState(GameState.Falling);
            st.FallTimer = 0f;

            // Physics takes the ball: mark it falling. The unfiltered ball
            // filter is iterated: BeginFall runs once per fall, so a ball
            // already carrying the tag (impossible in Playing) would throw
            // on Add - the guard below keeps the Add safe regardless.
            foreach (int ballEntity in _balls)
            {
                if (_ballFallingPool.Has(ballEntity))
                    continue;
                _ballFallingPool.Add(ballEntity);
            }

            // High score is saved per mode on fall (spec scoring).
            ref var score = ref _scorePool.Get(_session);
            if (score.Value > score.HighScore)
            {
                score.HighScore = score.Value;
                score.NewRecord = true;
                PlayerPrefs.SetInt(PrefsHelper.HighScoreKey(st.Mode), score.HighScore);
                PlayerPrefs.Save();
            }
            _soundPool.Add(_world.NewEntity()).Id = Events.SoundId.Fall;
        }

        private void GoToMenu()
        {
            ResetProgress();
            // The standing preview ball is re-placed on the menu track after
            // the rebuild: destroy it, BallSpawnSystem (Menu) respawns it.
            DestroyBall();
            ClearTrack();
            ClearCrystals();

            _trackResetPool.Add(_world.NewEntity());

            SetState(GameState.Menu);
        }

        /// <summary>Resets the session progress: score, level, fall timer, combo.</summary>
        private void ResetProgress()
        {
            ref var st = ref _statePool.Get(_session);
            st.FallTimer = 0f;
            ref var score = ref _scorePool.Get(_session);
            score.Value = 0;
            score.NewRecord = false;
            _levelPool.Get(_session).Value = 1;
            ref var combo = ref _comboPool.Get(_session);
            combo.Value = 0;
            combo.Max = 0;
        }

        /// <summary>
        /// Drops the ball entity and destroys its view (the ball view is not
        /// pooled: one per session).
        /// </summary>
        private void DestroyBall()
        {
            var viewPool = _world.GetPool<ViewLinkComponent>();
            foreach (int ballEntity in _balls)
            {
                if (viewPool.Has(ballEntity))
                {
                    var view = viewPool.Get(ballEntity).View;
                    if (view != null)
                        Object.Destroy(view);
                    viewPool.Del(ballEntity);
                }
                _world.DelEntity(ballEntity);
            }
        }

        /// <summary>
        /// Releases all block views to the pool and deletes block entities.
        /// Collects first: deleting entities while iterating a live filter
        /// can skip entries.
        /// </summary>
        private void ClearTrack()
        {
            var viewPool = _world.GetPool<ViewLinkComponent>();
            var doomed = new List<int>();
            foreach (int blockEntity in _blocks)
                doomed.Add(blockEntity);
            foreach (int blockEntity in doomed)
            {
                if (viewPool.Has(blockEntity) && viewPool.Get(blockEntity).View != null)
                    ReleaseBlockView(blockEntity);
                _world.DelEntity(blockEntity);
            }
        }

        /// <summary>
        /// Releases all crystal views immediately (including falling ones):
        /// a falling crystal's host block is already gone, so nothing else
        /// would remove it from the new session or the menu. Each view is
        /// released to its own variant's pool by tag (regular vs golden).
        /// </summary>
        private void ClearCrystals()
        {
            var viewPool = _world.GetPool<ViewLinkComponent>();
            var goldenPool = _world.GetPool<GoldenCrystalTag>();
            var crystals = new List<int>();
            foreach (int crystalEntity in _crystals)
                crystals.Add(crystalEntity);
            foreach (int crystalEntity in crystals)
            {
                if (viewPool.Has(crystalEntity) && viewPool.Get(crystalEntity).View != null)
                {
                    var view = viewPool.Get(crystalEntity).View;
                    if (goldenPool.Has(crystalEntity))
                        _shared.GoldenCrystalPool.Release(view);
                    else
                        _shared.CrystalPool.Release(view);
                }
                _world.DelEntity(crystalEntity);
            }
        }

        /// <summary>
        /// Consumes stale taps (e.g. from a menu button click): the ball
        /// must stand until a dedicated gameplay tap.
        /// </summary>
        private void ConsumeTaps()
        {
            foreach (int tapEntity in _taps)
                _tapPool.Del(tapEntity);
        }

        private void SetState(GameState state)
        {
            ref var st = ref _statePool.Get(_session);
            st.State = state;
            _stateChangedPool.Add(_world.NewEntity());
        }

        /// <summary>
        /// Releases a block entity's view to the typed block pool via the
        /// cached BlockViewComponent (the component is added by
        /// BlockViewSystem on every view spawn, so the typed path always
        /// hits; the fallback below only guards a view whose entity
        /// somehow lacks the component).
        /// </summary>
        private void ReleaseBlockView(int blockEntity)
        {
            var blockViewPool = _world.GetPool<BlockViewComponent>();
            if (blockViewPool.Has(blockEntity))
            {
                _shared.BlockPool.Release(blockViewPool.Get(blockEntity).View);
                return;
            }
            var link = _world.GetPool<ViewLinkComponent>().Get(blockEntity).View;
            var fallback = link != null ? link.GetComponent<Views.BlockView>() : null;
            if (fallback != null)
                _shared.BlockPool.Release(fallback);
        }
    }
}