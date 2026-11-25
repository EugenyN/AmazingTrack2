using System.Collections;
using Leopotam.EcsLite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AmazingTrack.Tests.PlayMode
{
    /// <summary>
    /// End-to-end gameplay test: loads the real GameScene, starts a session
    /// through the real menu flow (Play button), then an autopilot bot
    /// (<see cref="GameplayBot"/>) plays for the full run duration at normal
    /// time scale by emitting the game's official input events
    /// (TapEventComponent / JumpEventComponent) into the ECS world. The test
    /// passes when the session is still Playing at the end, the score
    /// reached the configured minimum and no errors or exceptions were
    /// logged.
    /// </summary>
    [Timeout(120000)]
    public class GameplayBotEndToEndTests
    {
        /// <summary>Track generator seed: the bot run is reproducible.</summary>
        private const int Seed = 12345;

        /// <summary>
        /// Game mode the bot plays: switch here to test other modes. The
        /// default menu mode is Normal; any other value is selected through
        /// the real mode buttons before Play. Note: MinScore and the bot's
        /// decision tuning are mode-specific (part counts, hole chances and
        /// typical scores differ).
        /// </summary>
        private const GameMode TestMode = GameMode.Normal;

        /// <summary>
        /// Minimum score the bot must reach in the full run: the lowest
        /// seeded-run score (579 across three runs on seed 12345: 579, 579,
        /// 589) minus a safety margin for decision-timing variance.
        /// </summary>
        private const int MinScore = 500;

        /// <summary>Full run duration, seconds of game time.</summary>
        private const float RunDuration = 60f;

        /// <summary>Scene the test loads.</summary>
        private const string SceneName = "GameScene";

        private Bootstrap _bootstrap;
        private EcsWorld _world;
        private SharedData _shared;
        private EcsPool<GameStateComponent> _statePool;
        private EcsPool<ScoreComponent> _scorePool;
        private EcsFilter _sessions;

        private GameplayBot _bot;

        private int _errorCount;
        private string _firstErrorMessage;

        [SetUp]
        public void SetUp()
        {
            // Records must not leak between runs (per-mode keys).
            PlayerPrefs.DeleteKey(PrefsHelper.HighScoreKey(GameMode.Easy));
            PlayerPrefs.DeleteKey(PrefsHelper.HighScoreKey(GameMode.Normal));
            PlayerPrefs.DeleteKey(PrefsHelper.HighScoreKey(GameMode.Hard));
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            Bootstrap.ForcedSeed = null;
        }

        [UnityTest]
        public IEnumerator GameplayBot_Survives60Seconds_WithMinScore()
        {
            // ---- Phase 0: menu entry through the real UI flow. ----
            Bootstrap.ForcedSeed = Seed;
            yield return LoadScene();

            Assert.That(Bootstrap.Instance, Is.Not.Null, "Bootstrap.Instance must be live after scene load.");
            _bootstrap = Bootstrap.Instance;
            Assert.That(_bootstrap.World, Is.Not.Null, "ECS world must be exposed for tests.");
            Assert.That(_bootstrap.Shared, Is.Not.Null, "SharedData must be exposed for tests.");

            _world = _bootstrap.World;
            _shared = _bootstrap.Shared;
            _statePool = _world.GetPool<GameStateComponent>();
            _scorePool = _world.GetPool<ScoreComponent>();
            _sessions = _world.Filter<GameStateComponent>().End();
            _bot = new GameplayBot(_world);

            var ui = _shared.Ui;
            Assert.That(ui, Is.Not.Null, "UiRoot must be present in the scene.");
            Assert.That(ui.MenuScreen, Is.Not.Null, "Menu screen must exist.");
            Assert.That(ui.MenuScreen.activeSelf, Is.True, "Menu screen must be shown on start.");
            Assert.That(ui.HudScreen, Is.Not.Null, "HUD screen must exist.");
            Assert.That(ui.HudScreen.activeSelf, Is.False, "HUD must be hidden in the menu.");

            var gameState = GetSessionState();
            Assert.That(gameState.State, Is.EqualTo(GameState.Menu), "Game must start in the Menu state.");

            // Select the test mode through the real mode button.
            Assert.That(ui.GetModeButton(TestMode), Is.Not.Null,
                $"A mode button for {TestMode} must exist in the menu.");
            ui.GetModeButton(TestMode).onClick.Invoke();
            yield return null;

            gameState = GetSessionState();
            Assert.That(gameState.Mode, Is.EqualTo(TestMode), $"Mode {TestMode} must be selected before Play.");

            Assert.That(Time.timeScale, Is.EqualTo(1f), "Time scale must stay normal for the whole run.");

            _errorCount = 0;
            _firstErrorMessage = null;
            Application.logMessageReceived += OnLogMessage;

            try
            {
                Assert.That(ui.PlayButton, Is.Not.Null, "Play button must exist.");
                ui.PlayButton.onClick.Invoke();

                // Let the flow system consume the Play event and spawn the ball.
                yield return null;
                yield return null;

                gameState = GetSessionState();
                Assert.That(gameState.State, Is.EqualTo(GameState.Playing), "Play button must start the session.");
                Assert.That(ui.MenuScreen, Is.Not.Null, "Menu screen must exist.");
                Assert.That(ui.MenuScreen.activeSelf, Is.False, "Menu screen must hide after Play.");
                Assert.That(ui.HudScreen, Is.Not.Null, "HUD screen must exist.");
                Assert.That(ui.HudScreen.activeSelf, Is.True, "HUD must show during the session.");

                int ballEntity = _bot.GetSingleBall();
                Assert.That(ballEntity, Is.GreaterThanOrEqualTo(0), "Ball must be spawned at session start.");
                Assert.That(_bot.IsWaitingForTap(), Is.False,
                    "Ball must roll immediately after Play (no waiting-for-tap state).");

                // ---- Phase 2: the bot drives the session (the ball already
                // rolls; the bot's taps toggle the direction). ----
                float elapsed = 0f;
                while (elapsed < RunDuration)
                {
                    yield return null;
                    elapsed += Time.deltaTime;

                    gameState = GetSessionState();
                    if (gameState.State != GameState.Playing)
                    {
                        Vector3 pos = _bot.GetBallPos();
                        int reachedScore = _sessions.GetSingletonComponent(_scorePool).Value;
                        string blockDump = _bot.DumpBlocksNear(pos);
                        Assert.Fail(
                            $"Session left Playing at {elapsed:F1}s/{RunDuration:F0}s: state={gameState.State}, " +
                            $"score={reachedScore}, ballPos={pos}.\n{blockDump}");
                    }

                    _bot.Update();
                }

                // ---- Phase 3: final asserts. ----
                gameState = GetSessionState();
                Assert.That(gameState.State, Is.EqualTo(GameState.Playing), "Bot must survive the whole run.");

                int finalScore = _sessions.GetSingletonComponent(_scorePool).Value;
                Debug.Log($"[GameplayBotE2E] Run finished: state={gameState.State}, finalScore={finalScore}, " +
                          $"minScore={MinScore}, loggedErrors={_errorCount}.");
                Assert.That(finalScore, Is.GreaterThanOrEqualTo(MinScore),
                    $"Score {finalScore} must reach the minimum {MinScore}.");

                Assert.That(_errorCount, Is.EqualTo(0),
                    $"No errors expected during the run, got {_errorCount}: {_firstErrorMessage}");
            }
            finally
            {
                Application.logMessageReceived -= OnLogMessage;
            }
        }

        /// <summary>Counts error/exception logs for the run report.</summary>
        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            _errorCount++;
            if (_firstErrorMessage == null)
                _firstErrorMessage = $"{type}: {condition}";
        }

        /// <summary>
        /// The session singleton's state snapshot (by value: safe across
        /// yields in an iterator, unlike a ref into component storage).
        /// </summary>
        private GameStateComponent GetSessionState()
        {
            return _sessions.GetSingletonComponent(_statePool);
        }

        /// <summary>Loads the test scene and waits for activation.</summary>
        private static IEnumerator LoadScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName);
            Assert.That(load, Is.Not.Null, $"Scene '{SceneName}' must be in the build.");
            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;
        }
    }
}
