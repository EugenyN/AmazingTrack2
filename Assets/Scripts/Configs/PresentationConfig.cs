using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Presentation tuning: prefab references, audio clips, block color range,
    /// background, camera framing, fall presentation and game-over delay, and
    /// view/label animation timing. No simulation rules here - those live in
    /// <see cref="GameplayConfig"/>. Asset lives at Assets/Configs.
    /// </summary>
    [CreateAssetMenu(fileName = "PresentationConfig", menuName = "AmazingTrack/Presentation Config")]
    public class PresentationConfig : ScriptableObject
    {
        [Header("Prefabs")]
        [Tooltip("Track block prefab (unit cube 1 x blockHeight x 1, Layer=Block)")]
        public GameObject blockPrefab;

        [Tooltip("Ball prefab (sphere, Rigidbody, Layer=Ball)")]
        public GameObject ballPrefab;

        [Tooltip("Crystal prefab (change 2)")]
        public GameObject crystalPrefab;

        [Tooltip("Golden crystal prefab (floating variant, jump-only pickup; own view pool)")]
        public GameObject goldenCrystalPrefab;

        [Tooltip("One-shot particle effect spawned at the crystal position on pickup; pooled, released by the game after playing")]
        public GameObject crystalPickupFxPrefab;

        [Header("Audio")]
        [Tooltip("Direction toggle")]
        public AudioClip turnClip;

        [Tooltip("Session start")]
        public AudioClip gameStartClip;

        [Tooltip("Level up")]
        public AudioClip nextLevelClip;

        [Tooltip("Ball fall")]
        public AudioClip fallClip;

        [Tooltip("Jump sound")]
        public AudioClip jumpClip;

        [Tooltip("Crystal pickup sound")]
        public AudioClip crystalClip;

        [Header("Block fall")]
        [Tooltip("Delay before a passed block falls, sec")]
        public float blockFallDelay = 0.4f;

        [Tooltip("Gravity acceleration of a falling block after the delay, units/sec^2. Speed grows from zero up to blockFallMaxSpeed.")]
        public float blockFallGravity = 25f;

        [Tooltip("Maximum fall speed of a block, units/sec")]
        public float blockFallMaxSpeed = 20f;

        [Tooltip("Y threshold at which a block view is destroyed")]
        public float blockFallYThreshold = -10f;

        [Header("Fall / Game Over")]
        [Tooltip("Horizontal speed a falling ball keeps at the physics hand-over, units/sec. Fixed: the forward flight length is the same on every level and does not grow with the ball's track speed")]
        public float ballFallHorizontalSpeed = 3f;

        [Tooltip("Gravity multiplier of a falling ball: > 1 makes the fall arc steeper and shortens the forward glide; 1 = default physics gravity")]
        public float ballFallGravityScale = 2f;

        [Tooltip("Delay before the Game Over screen after falling starts, sec")]
        public float gameOverDelay = 1.5f;

        [Header("Crystal view")]
        [Tooltip("Crystal bob amplitude above the seated height, units")]
        public float crystalBobAmplitude = 0.04f;

        [Tooltip("Crystal fall speed factor relative to the host block's fall (gravity and max speed multiplier)")]
        public float crystalFallSpeedFactor = 0.6f;

        [Header("UI labels")]
        [Tooltip("Combo label float-up distance, UI units (anchoredPosition)")]
        public float comboFloatDistance = 50f;

        [Tooltip("Combo label float-up duration, sec")]
        [Min(0.01f)]
        public float comboFloatDuration = 0.3f;

        [Tooltip("Time after the last combo event before the label hides, sec")]
        [Min(0.1f)]
        public float comboLabelLifetime = 1.5f;

        [Tooltip("Time the level-up label stays visible before hiding, sec")]
        [Min(0.1f)]
        public float levelUpFloatLifetime = 2.5f;

        [Header("Block color (pastel HSV range)")]
        [Tooltip("Saturation range for random block color")]
        public Vector2 colorSaturation = new Vector2(0.5f, 0.75f);

        [Tooltip("Value (brightness) range for random block color")]
        public Vector2 colorValue = new Vector2(0.8f, 0.95f);

        [Header("Background")]
        [Tooltip("Zenith (top of the gradient) saturation of the level background")]
        [Range(0f, 1f)]
        public float backgroundZenithSaturation = 0.45f;

        [Tooltip("Zenith (top of the gradient) brightness of the level background")]
        [Range(0f, 1f)]
        public float backgroundZenithValue = 0.45f;

        [Tooltip("Horizon (bottom of the gradient) saturation of the level background")]
        [Range(0f, 1f)]
        public float backgroundHorizonSaturation = 0.30f;

        [Tooltip("Horizon (bottom of the gradient) brightness of the level background")]
        [Range(0f, 1f)]
        public float backgroundHorizonValue = 0.90f;

        [Tooltip("Duration of the background color transition after a level change, sec")]
        [Min(0.01f)]
        public float backgroundTransitionDuration = 1.2f;

        [Tooltip("Background breathing hue drift amplitude (fraction of the hue wheel; 0 disables breathing)")]
        [Range(0f, 0.5f)]
        public float backgroundBreatheAmplitude = 0.05f;

        [Tooltip("Background breathing period, sec")]
        [Min(0.1f)]
        public float backgroundBreathePeriod = 10f;

        [Tooltip("Zenith (top of the gradient) color shown while the ball falls and on the game over screen")]
        public Color backgroundDefeatZenith = new Color(0.10f, 0.00f, 0.00f, 1f);

        [Tooltip("Horizon (bottom of the gradient) color shown while the ball falls and on the game over screen")]
        public Color backgroundDefeatHorizon = new Color(0.45f, 0.05f, 0.05f, 1f);

        [Header("Camera")]
        [Tooltip("Camera smoothing rate (1/sec, exponential follow). Framing (offset + rotation) is not configured here: it is captured from the scene camera transform at startup.")]
        public float cameraLerpRate = 10f;

        [Tooltip("Orthographic size on wide screens (aspect >= landscape threshold)")]
        public float cameraOrthoSizeLandscape = 6f;

        [Tooltip("Orthographic size on narrow screens (aspect <= portrait threshold)")]
        public float cameraOrthoSizePortrait = 8f;

        [Tooltip("Aspect ratio (width/height) at and above which the landscape size is used")]
        public float cameraAspectLandscape = 1.0f;

        [Tooltip("Aspect ratio (width/height) at and below which the portrait size is used")]
        public float cameraAspectPortrait = 0.55f;
    }
}
