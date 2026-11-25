using Leopotam.EcsLite;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace AmazingTrack.UI
{
    /// <summary>
    /// References to the UI screens. Buttons are bound by Bootstrap after the
    /// ECS world exists: bridges get the world and emit event entities.
    /// All texts are in English per project rules.
    /// </summary>
    public class UiRoot : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject MenuScreen;
        public GameObject HudScreen;
        public GameObject GameOverScreen;
        public GameObject PauseScreen;

        [Header("Menu")]
        public UnityEngine.UI.Button EasyButton;
        public UnityEngine.UI.Button NormalButton;
        public UnityEngine.UI.Button HardButton;
        public UnityEngine.UI.Button PlayButton;
        public TMP_Text MenuHighScoreText;

        [Header("Menu Tints")]
        /// <summary>Image tint of the selected mode button.</summary>
        [SerializeField] private Color selectedModeTint = new Color32(91, 211, 98, 144);
        /// <summary>Image tint of non-selected mode buttons.</summary>
        [SerializeField] private Color normalModeTint = Color.white;

        [Header("Pause")]
        public UnityEngine.UI.Button ContinueButton;
        public UnityEngine.UI.Button MenuButton;

        [Header("HUD")]
        public TMP_Text ScoreText;
        public TMP_Text LevelText;
        public TMP_Text HudHighScoreText;
        public TMP_Text FloatText;
        /// <summary>Best combo streak of the session; hidden until the first combo.</summary>
        public TMP_Text MaxComboText;
        public UnityEngine.UI.Button JumpButton;
        public UnityEngine.UI.Button PauseButton;

        [Header("Game Over")]
        public TMP_Text FinalScoreText;
        public TMP_Text FinalHighScoreText;
        /// <summary>Best combo streak of the finished session on the results screen.</summary>
        public TMP_Text FinalComboText;
        public TMP_Text NewRecordText;
        /// <summary>
        /// Restarts the finished session (same mode) on click. Null-tolerant:
        /// when unassigned, Bind falls back to the child named
        /// "PlayAgainButton" of GameOverScreen.
        /// </summary>
        public UnityEngine.UI.Button PlayAgainButton;

        /// <summary>
        /// Binds all buttons to the ECS world (called once by Bootstrap).
        /// </summary>
        public void Bind(EcsWorld world)
        {
            BindModeButton(EasyButton, UiButtonBridge.Action.SelectEasy, world);
            BindModeButton(NormalButton, UiButtonBridge.Action.SelectNormal, world);
            BindModeButton(HardButton, UiButtonBridge.Action.SelectHard, world);
            BindModeButton(PlayButton, UiButtonBridge.Action.Play, world);
            if (GameOverScreen != null)
            {
                var menuButton = GameOverScreen.transform.Find("MenuButton")
                    ?.GetComponent<UnityEngine.UI.Button>();
                BindModeButton(menuButton, UiButtonBridge.Action.GoMenu, world);

                // Restart lives on an explicit button: the serialized
                // reference wins, the name lookup only fills a gap. The
                // bind itself is null-tolerant (a missing button must not
                // break the bind of the rest of the UI).
                if (PlayAgainButton == null)
                {
                    PlayAgainButton = GameOverScreen.transform.Find("PlayAgainButton")
                        ?.GetComponent<UnityEngine.UI.Button>();
                }
                BindModeButton(PlayAgainButton, UiButtonBridge.Action.PlayAgain, world);
            }
            BindModeButton(ContinueButton, UiButtonBridge.Action.Resume, world);
            BindModeButton(MenuButton, UiButtonBridge.Action.GoMenu, world);
            BindModeButton(JumpButton, UiButtonBridge.Action.Jump, world);
            // HUD pause button: toggles pause exactly like ESC (the event is
            // validated by GameFlowSystem, so this is inert outside
            // Playing/Paused). Null-tolerant: a missing reference or the
            // button being absent from a scene must not break the bind.
            BindModeButton(PauseButton, UiButtonBridge.Action.Pause, world);

            // The on-screen Jump button is a touch affordance: show it on
            // mobile devices, in the Editor (for testing) and on web builds
            // running in a touch-capable browser (the Touchscreen device is
            // created at startup there, before the first touch). On
            // non-touch builds the right mouse button covers jumping, so
            // the button would only clutter the HUD. Deactivated once at
            // bind time; UiSyncSystem only toggles the whole HudScreen, so
            // this child state persists.
            if (JumpButton != null
                && !(Application.isMobilePlatform
                     || Application.isEditor
                     || (IsWebBuild() && Touchscreen.current != null)))
            {
                JumpButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// True on Web platform builds (UNITY_WEBGL). Separate helper so
        /// the visibility rule stays readable without #if in method body.
        /// </summary>
        private static bool IsWebBuild()
        {
#if UNITY_WEBGL
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Highlights the selected mode button and resets the others to
        /// their normal color. Tints are configured in the Inspector.
        /// Allocation-free.
        /// </summary>
        public void SetSelectedMode(GameMode mode)
        {
            SetModeTint(EasyButton, mode == GameMode.Easy);
            SetModeTint(NormalButton, mode == GameMode.Normal);
            SetModeTint(HardButton, mode == GameMode.Hard);
        }

        /// <summary>
        /// The menu button that selects the given mode, or null when absent.
        /// </summary>
        public UnityEngine.UI.Button GetModeButton(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Easy:
                    return EasyButton;
                case GameMode.Hard:
                    return HardButton;
                case GameMode.Normal:
                default:
                    return NormalButton;
            }
        }

        private void SetModeTint(UnityEngine.UI.Button button, bool selected)
        {
            if (button == null || button.image == null)
                return;
            button.image.color = selected ? selectedModeTint : normalModeTint;
        }

        private static void BindModeButton(UnityEngine.UI.Button button, UiButtonBridge.Action action,
            EcsWorld world)
        {
            if (button == null)
                return;
            var bridge = button.GetComponent<UiButtonBridge>()
                         ?? button.gameObject.AddComponent<UiButtonBridge>();
            bridge.ButtonAction = action;
            bridge.World = world;
            button.onClick.AddListener(bridge.OnClick);
        }
    }
}
