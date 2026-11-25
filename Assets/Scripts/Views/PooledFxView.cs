using UnityEngine;

namespace AmazingTrack.Views
{
    /// <summary>
    /// One-shot pooled pickup FX: plays the burst from the beginning at the
    /// spawn position and releases the instance back to the shared FX pool
    /// when the particle system finishes. The material passed to Play tints
    /// the burst to the picked crystal's variant color (the shared
    /// material instance on the renderer is swapped; pooled instances are
    /// never alive at the same position twice). Lives on the pickup FX
    /// prefab; must stay in a file matching the class name (Unity
    /// component rule).
    /// </summary>
    public class PooledFxView : MonoBehaviour
    {
        [HideInInspector] public ParticleSystem Particles;
        [HideInInspector] public Renderer FxRenderer;

        private System.Action<PooledFxView> _onFinished;
        private bool _playing;
        private Material _defaultMaterial;

        /// <summary>
        /// Caches the particle system, the renderer and its default
        /// material, then starts playback from the beginning with the given
        /// material (null keeps the prefab's default).
        /// </summary>
        public void Play(System.Action<PooledFxView> onFinished, Material material = null)
        {
            if (Particles == null)
                Particles = GetComponent<ParticleSystem>();
            if (FxRenderer == null)
                FxRenderer = GetComponentInChildren<Renderer>();
            if (_defaultMaterial == null && FxRenderer != null)
                _defaultMaterial = FxRenderer.sharedMaterial;
            if (FxRenderer != null)
                FxRenderer.sharedMaterial = material != null ? material : _defaultMaterial;
            _onFinished = onFinished;
            Particles.Clear();
            Particles.Play();
            _playing = true;
        }

        private void Update()
        {
            if (!_playing)
                return;
            if (Particles.IsAlive(true))
                return;
            _playing = false;
            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke(this);
        }
    }
}
