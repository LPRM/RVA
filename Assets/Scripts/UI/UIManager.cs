using TMPro;
using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Shows the right panel for the game state and keeps the HUD texts up to date.
    /// Buttons call GameManager, MazeGravity and CheckpointManager directly through their On Click;
    /// this script only adds what On Click cannot do on its own (choosing the mode, switching menu pages).
    /// Every reference is optional: leave a field empty and that part is simply skipped.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("References (found automatically when empty)")]
        [SerializeField] GameManager gameManager;

        [Header("Panels")]
        [Tooltip("First menu page: Normal, Time Trial, Quit.")]
        [SerializeField] GameObject menuPanel;
        [Tooltip("Second menu page: Level 1, 2, 3, Back.")]
        [SerializeField] GameObject levelSelectPanel;
        [SerializeField] GameObject hudPanel;
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject winPanel;
        [SerializeField] GameObject timeUpPanel;

        [Header("Texts")]
        [Tooltip("On the level select page: shows the chosen mode.")]
        [SerializeField] TMP_Text modeText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text levelText;
        [Tooltip("Filled in later by the weather (Phase 3).")]
        [SerializeField] TMP_Text weatherText;
        [Tooltip("On the win panel: the final time.")]
        [SerializeField] TMP_Text resultText;

        void Awake()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null)
                Debug.LogError("[UIManager] No GameManager in the scene.", this);
        }

        void OnEnable()
        {
            if (gameManager != null)
                gameManager.StateChanged += ShowState;
        }

        void OnDisable()
        {
            if (gameManager != null)
                gameManager.StateChanged -= ShowState;
        }

        void Start()
        {
            // StateChanged only fires on a change, and the game starts already in Menu,
            // so show the first screen by hand.
            if (gameManager != null)
                ShowState(gameManager.State);
        }

        void Update()
        {
            if (gameManager == null)
                return;

            if (timerText != null)
            {
                float seconds = gameManager.Mode == GameMode.TimeTrial ? gameManager.Remaining : gameManager.Elapsed;
                timerText.text = GameManager.FormatTime(seconds);
            }

            if (levelText != null)
                levelText.text = gameManager.CurrentLevelName;
        }

        // ------------------------------------------------------------- buttons (On Click)

        /// <summary>Menu button "Normal": chooses Normal mode and opens the level page.</summary>
        public void SetNormalMode()
        {
            ChooseMode(GameMode.Normal);
        }

        /// <summary>Menu button "Time Trial": chooses Time Trial and opens the level page.</summary>
        public void SetTimeTrialMode()
        {
            ChooseMode(GameMode.TimeTrial);
        }

        /// <summary>Level page button "Back": returns to the first menu page.</summary>
        public void ShowMainMenu()
        {
            SetActive(menuPanel, true);
            SetActive(levelSelectPanel, false);
        }

        // ------------------------------------------------------------- internals

        void ChooseMode(GameMode mode)
        {
            if (gameManager != null)
                gameManager.SetMode(mode);

            if (modeText != null)
                modeText.text = mode == GameMode.TimeTrial ? "Time Trial Mode" : "Normal Mode";

            SetActive(menuPanel, false);
            SetActive(levelSelectPanel, true);
        }

        void ShowState(GameState state)
        {
            // Every state change starts the menu on its first page.
            SetActive(menuPanel, state == GameState.Menu);
            SetActive(levelSelectPanel, false);

            SetActive(hudPanel, state == GameState.Playing || state == GameState.Paused);
            SetActive(pausePanel, state == GameState.Paused);
            SetActive(winPanel, state == GameState.Won);
            SetActive(timeUpPanel, state == GameState.TimeUp);

            if (state == GameState.Won && resultText != null && gameManager != null)
            {
                string label = gameManager.Mode == GameMode.TimeTrial ? "Time Left: " : "Time";
                resultText.text = $"{label}: {GameManager.FormatTime(gameManager.LastResultSeconds)}";
            }
        }

        static void SetActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
                panel.SetActive(active);
        }
    }
}
