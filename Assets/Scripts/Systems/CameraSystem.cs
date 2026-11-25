using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Camera: fixed isometric framing captured from the scene camera
    /// transform at startup (position, rotation, follow offset). In Menu
    /// the camera frames the session-start view: focus = the ball spawn
    /// point (first block of the preview track), position = focus + offset;
    /// a mode switch regenerates the preview and the camera re-frames with
    /// the same lerp. During play it follows the ball with the same offset
    /// and rotation. Position is lerped; rotation is set fixed every frame
    /// (no LookAt). A track rebuild from the origin (mode switch, session
    /// start, menu return) moves the follow focus to the new track start:
    /// the camera snaps there instantly instead of lerping across the
    /// whole track (one-shot CameraSnapEventComponent, emitted by
    /// TrackGenSystem.ResetCursor).
    /// The orthographic size is recomputed every frame from
    /// the screen aspect ratio (config-driven: landscape size on wide
    /// screens, portrait size on narrow ones, interpolated between) so the
    /// track stays visible in both orientations, including runtime
    /// orientation changes. While the ball entity does not exist in an
    /// active session (it was destroyed after falling), the camera holds
    /// the last focus position; the spawn-point focus applies only in Menu
    /// (and at startup, before the first track exists).
    /// </summary>
    public class CameraSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _balls;
        private EcsFilter _blocks;
        private EcsFilter _sessions;
        private EcsPool<BallComponent> _ballPool;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<GameStateComponent> _statePool;
        private EcsFilter _cameraSnaps;
        private EcsPool<CameraSnapEventComponent> _cameraSnapPool;

        private Vector3 _target;
        private Vector3 _menuPosition;
        private Quaternion _fixedRotation;
        private Vector3 _followOffset;

        public void Init(IEcsSystems systems)
        {
            _shared = systems.GetShared<SharedData>();
            _world = systems.GetWorld();
            _balls = _world.Filter<BallComponent>().End();
            _blocks = _world.Filter<BlockComponent>().End();
            _sessions = _world.Filter<GameStateComponent>().End();
            _ballPool = _world.GetPool<BallComponent>();
            _blockPool = _world.GetPool<BlockComponent>();
            _statePool = _world.GetPool<GameStateComponent>();
            _cameraSnaps = _world.Filter<CameraSnapEventComponent>().End();
            _cameraSnapPool = _world.GetPool<CameraSnapEventComponent>();

            // The scene camera transform is the single source of the framing:
            // menu position, fixed rotation and the follow offset relative to
            // the menu focus (origin). At startup no session exists yet, so
            // the focus defaults to the origin.
            _target = TryGetFocus(out var focus) ? focus : Vector3.zero;
            if (_shared.Camera != null)
            {
                _menuPosition = _shared.Camera.transform.position;
                _fixedRotation = _shared.Camera.transform.rotation;
                _followOffset = _menuPosition - _target;
            }
            else
            {
                _menuPosition = new Vector3(-10f, 10f, -10f);
                _fixedRotation = Quaternion.Euler(30f, 45f, 0f);
                _followOffset = _menuPosition;
            }
            Apply(1f);
        }

        public void Run(IEcsSystems systems)
        {
            // Keep the last focus while no focus source exists (ball entity
            // destroyed after falling, session not in Menu): the camera must
            // hold its position until restart or menu return.
            // One-shot snap: a track rebuild from the origin (mode switch,
            // session start, menu return) teleports the focus to the fresh
            // track start - lerp would fly the camera across the whole
            // track, so place it at the new framing instantly. Normal focus
            // motion (ball rolling, menu re-framing on mode switch) is
            // continuous and stays lerped. The snap event is consumed only
            // once a focus exists, so a focusless frame does not drop it.
            if (TryGetFocus(out var focus))
            {
                bool snap = false;
                foreach (int snapEntity in _cameraSnaps)
                {
                    _cameraSnapPool.Del(snapEntity);
                    snap = true;
                }
                _target = focus;
                Apply(snap ? 1f : _shared.PresentationConfig.cameraLerpRate * Time.deltaTime);
            }
            ApplyOrthoSize();
        }

        /// <summary>
        /// Aspect-driven orthographic size: recomputed every frame from the
        /// screen dimensions so runtime orientation changes are handled
        /// without any session restart. Landscape and portrait sizes and the
        /// aspect thresholds come from the config; sizes interpolate
        /// continuously between the thresholds. Pure math, no allocations.
        /// </summary>
        private void ApplyOrthoSize()
        {
            if (_shared.Camera == null)
                return;
            float aspect = (float)Screen.width / Screen.height;
            float t = Mathf.InverseLerp(_shared.PresentationConfig.cameraAspectPortrait, _shared.PresentationConfig.cameraAspectLandscape, aspect);
            _shared.Camera.orthographicSize = Mathf.Lerp(_shared.PresentationConfig.cameraOrthoSizePortrait, _shared.PresentationConfig.cameraOrthoSizeLandscape, t);
        }

        private void Apply(float t)
        {
            if (_shared.Camera == null)
                return;
            var desired = _target + _followOffset;
            _shared.Camera.transform.position = Vector3.Lerp(_shared.Camera.transform.position, desired, t);
            _shared.Camera.transform.rotation = _fixedRotation;
        }

        private bool TryGetFocus(out Vector3 focus)
        {
            if (_sessions.GetSingletonComponent(_statePool).State == GameState.Menu)
            {
                // Menu preview framing equals the session-start framing: the
                // focus is the ball spawn point (first track block). The
                // track is regenerated per selected mode, so switching modes
                // re-frames the menu camera smoothly (lerp).
                focus = GetFirstBlockPos();
                return true;
            }

            foreach (int ballEntity in _balls)
            {
                ref var ball = ref _ballPool.Get(ballEntity);
                focus = ball.Pos;
                return true;
            }

            // No ball and not in Menu: no focus source this frame.
            focus = default;
            return false;
        }

        /// <summary>
        /// Position of the first track block (minimal Order) - the ball spawn
        /// point; the origin while the track is empty (initialization frame,
        /// between mode-switch cleanup and the generator rebuild).
        /// </summary>
        private Vector3 GetFirstBlockPos()
        {
            int firstEntity = -1;
            int firstOrder = int.MaxValue;
            foreach (int blockEntity in _blocks)
            {
                ref var block = ref _blockPool.Get(blockEntity);
                if (block.Order < firstOrder)
                {
                    firstOrder = block.Order;
                    firstEntity = blockEntity;
                }
            }
            return firstEntity >= 0 ? _blockPool.Get(firstEntity).Pos : Vector3.zero;
        }
    }
}
