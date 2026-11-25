using Leopotam.EcsLite;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using AmazingTrack.UI;

namespace AmazingTrack
{
    /// <summary>
    /// Scene entry point: creates the ECS world, shared data and systems.
    /// Thin MonoBehaviour - all game logic lives in ECS systems.
    /// </summary>
    [AutoStaticsCleanup]
    public partial class Bootstrap : MonoBehaviour
    {
        /// <summary>
        /// Test-only: when set before the game scene loads, the track
        /// generator is seeded with it (reproducible track). Consumed once
        /// in Start and reset to null so it never leaks into later loads.
        /// </summary>
        public static int? ForcedSeed;

        /// <summary>
        /// Live entry point of the loaded scene (tests read the ECS world
        /// through it). Set in Start, cleared in OnDestroy.
        /// </summary>
        public static Bootstrap Instance { get; private set; }

        public GameplayConfig GameplayConfig;
        public PresentationConfig PresentationConfig;
        public UiRoot Ui;

        /// <summary>
        /// Player action map source (InputSystem_Actions asset): Turn/Move/
        /// Previous/Next/Jump actions drive gameplay input through
        /// PlayerActionsInputSystem. Unassigned - gameplay input is disabled
        /// (ESC pause still works); touch/mouse presses reach gameplay only
        /// through the asset's Turn/Jump bindings.
        /// </summary>
        public InputActionAsset InputActions;

        private EcsWorld _world;
        private IEcsSystems _systems;
        private SharedData _shared;

        /// <summary>The live ECS world (tests observe gameplay state).</summary>
        public EcsWorld World => _world;

        /// <summary>The shared data passed to all systems (tests read config, pools).</summary>
        public SharedData Shared => _shared;

        private void Start()
        {
            Instance = this;

            // Fail loudly on a misconfigured composition root: both config
            // assets must be assigned before the ECS world is built.
            if (GameplayConfig == null || PresentationConfig == null)
                throw new System.InvalidOperationException(
                    "Bootstrap requires both GameplayConfig and PresentationConfig assets assigned in the Inspector.");

            // Enhanced Touch is enabled by PlayerActionsInputSystem.Init (its
            // only consumer); nothing to enable here when the asset is
            // unassigned - gameplay input is disabled in that case.

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android ignores vSyncCount and defaults to 30 fps; target the
            // native display refresh rate instead (60/90/120 Hz).
            var refreshRate = Screen.currentResolution.refreshRateRatio;
            Application.targetFrameRate =
                Mathf.RoundToInt(refreshRate.numerator / (float)refreshRate.denominator);
#else
            // Hardware vsync kills tearing on desktop.
            QualitySettings.vSyncCount = 1;
#endif

            _shared = new SharedData
            {
                GameplayConfig = GameplayConfig,
                PresentationConfig = PresentationConfig,
                Camera = Camera.main,
                Audio = FindAnyObjectByType<AudioSource>(),
                Ui = Ui != null ? Ui : FindAnyObjectByType<UiRoot>(),
                BallPrefab = PresentationConfig.ballPrefab
            };
            _shared.ViewRoot = new GameObject("ViewRoot").transform;
            _shared.PoolRoot = new GameObject("PoolRoot").transform;
            _shared.PoolRoot.SetParent(_shared.ViewRoot, false);
            _shared.PoolRoot.gameObject.SetActive(false);
            _shared.CreatePools();
            _shared.CacheCrystalMaterials();
            _shared.BlockMask = LayerMask.GetMask("Block");

            _world = new EcsWorld();
            _systems = new EcsSystems(_world, _shared);
            
            _systems
                .Add(new UserInputSystem())
                .Add(new PlayerActionsInputSystem(InputActions))
                .Add(new GameFlowSystem())
                .Add(new TrackGenSystem(ForcedSeed))
                .Add(new BlockViewSystem())
                .Add(new BallSpawnSystem())
                .Add(new BallJumpSystem())
                .Add(new BallGroundSystem())
                .Add(new CrystalSystem())
                .Add(new FallCheckSystem())
                .Add(new BallTurnSystem())
                .Add(new BallMoveSystem())
                .Add(new BallViewSystem())
                .Add(new CrystalViewSystem())
                .Add(new ScoreSystem())
                .Add(new LevelSystem())
                .Add(new BackgroundSystem())
                .Add(new BlockFallViewSystem())
                .Add(new CameraSystem())
                .Add(new AudioSystem())
                .Add(new UiSyncSystem());

            // Cross-reference registration: systems resolve each other by
            // type through this (SharedData.GetSystem<T>) during Init.
            _shared.Systems = _systems;

            ForcedSeed = null;
            
            // Inject the world into UI bridges before the first frame.
            if (_shared.Ui != null)
                _shared.Ui.Bind(_world);

            _systems.Init();
        }

        private void Update()
        {
            _systems?.Run();
        }

        private void OnDestroy()
        {
            Instance = null;

            if (_systems != null)
            {
                _systems.Destroy();
                _systems = null;
            }
            if (_world != null)
            {
                _world.Destroy();
                _world = null;
            }
        }
    }
}
