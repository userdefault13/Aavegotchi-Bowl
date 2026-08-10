using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.App;
using RetroBowl.Core;
using RetroBowl.UI.Career;

namespace RetroBowl.UI
{
    public class MenuManager : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject mainMenuPanel;
        public GameObject pauseMenuPanel;
        public GameObject gameOverPanel;
        public GameObject quarterBreakPanel;

        [Header("Main Menu")]
        public Button playButton;
        public Button quitButton;

        [Header("Pause Menu")]
        public Button resumeButton;
        public Button restartButton;
        public Button mainMenuButton;

        [Header("Game Over")]
        public TextMeshProUGUI gameOverTitleText;
        public TextMeshProUGUI finalScoreText;
        public Button playAgainButton;
        public Button exitButton;

        [Header("Quarter Break")]
        public TextMeshProUGUI quarterBreakText;
        public Button continueButton;

        GameState? panelsState;
        readonly MenuCursor menuCursor = new MenuCursor();

        void Awake()
        {
            EnsureFonts(GetComponentsInChildren<TextMeshProUGUI>(true));
        }

        void Start()
        {
            SetupButtons();
            panelsState = null;
            // Match load may already be Playing (SceneFlow) — don't force a Menu panel state
            // that fights play-calling for a frame.
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
            {
                HideAllPanels();
                panelsState = GameState.Playing;
            }
            else
            {
                ShowMainMenu();
                panelsState = GameState.Menu;
            }
        }

        static void EnsureFonts(TextMeshProUGUI[] texts)
        {
            foreach (var tmp in texts)
                GameFonts.Apply(tmp);
        }

        void Update()
        {
            UpdatePanels();
            if (menuCursor.IsActive)
                menuCursor.Tick();
        }

        void SetupButtons()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
        }

        void BindPanelCursor(GameObject panel, Button prefer = null)
        {
            menuCursor.SetActive(false);
            if (panel == null || !panel.activeInHierarchy)
                return;

            var btns = panel.GetComponentsInChildren<Button>(true);
            int start = 0;
            if (prefer != null)
            {
                for (int i = 0; i < btns.Length; i++)
                {
                    if (btns[i] != prefer) continue;
                    start = i;
                    break;
                }
            }

            menuCursor.BindButtons(btns, 1, start);
            menuCursor.SetActive(true);
        }

        void UpdatePanels()
        {
            if (GameManager.Instance == null) return;

            // Only rebuild panels on state change. Re-activating the quarter-break
            // panel every frame cancels in-progress Continue button clicks.
            var state = GameManager.Instance.currentState;
            if (panelsState.HasValue && panelsState.Value == state)
                return;
            panelsState = state;

            switch (state)
            {
                case GameState.Menu:
                    ShowMainMenu();
                    break;
                case GameState.Playing:
                    menuCursor.SetActive(false);
                    HideAllPanels();
                    break;
                case GameState.Paused:
                    ShowPauseMenu();
                    break;
                case GameState.QuarterBreak:
                    ShowQuarterBreak();
                    break;
                case GameState.GameOver:
                    ShowGameOver();
                    break;
            }
        }

        void ShowMainMenu()
        {
            HideAllPanels();
            // Hybrid: career lives in CareerScene — never overlay CareerHub on Match.
            CareerHub.Instance?.Hide();
            if (SceneFlow.Instance != null)
            {
                // Match has no main menu. Do not auto-bounce here — SceneFlow sets
                // Playing after load; EndGame / pause buttons own Career returns.
                menuCursor.SetActive(false);
                return;
            }

            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
            BindPanelCursor(mainMenuPanel, playButton);
        }

        void ShowPauseMenu()
        {
            HideAllPanels();
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
            BindPanelCursor(pauseMenuPanel, resumeButton);
        }

        void ShowGameOver()
        {
            HideAllPanels();
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                
                if (GameManager.Instance.playerScore > GameManager.Instance.opponentScore)
                {
                    if (gameOverTitleText != null)
                        gameOverTitleText.text = "YOU WIN!";
                }
                else if (GameManager.Instance.playerScore < GameManager.Instance.opponentScore)
                {
                    if (gameOverTitleText != null)
                        gameOverTitleText.text = "YOU LOSE";
                }
                else
                {
                    if (gameOverTitleText != null)
                        gameOverTitleText.text = "TIE GAME";
                }

                if (finalScoreText != null)
                {
                    finalScoreText.text = $"{GameManager.Instance.playerScore} - {GameManager.Instance.opponentScore}";
                }
            }

            // Hybrid: GameManager.EndGame loads Career PostMatch via SceneFlow.
            BindPanelCursor(gameOverPanel, playAgainButton);
        }

        void ShowQuarterBreak()
        {
            HideAllPanels();
            CareerHub.Instance?.Hide();
            if (quarterBreakPanel != null)
            {
                quarterBreakPanel.SetActive(true);
                // Sit above HUD / play-calling so their Images cannot steal the Continue click.
                quarterBreakPanel.transform.SetAsLastSibling();

                if (quarterBreakText != null && GameManager.Instance != null)
                {
                    if (GameManager.Instance.isOvertime && GameManager.Instance.currentQuarter > GameManager.Instance.quartersPerGame)
                        quarterBreakText.text = "OVERTIME — NEXT SCORE WINS";
                    else
                        quarterBreakText.text = $"End of Quarter {GameManager.Instance.currentQuarter - 1}";
                }

                if (continueButton != null)
                {
                    continueButton.interactable = true;
                    continueButton.transform.SetAsLastSibling();
                }
            }

            BindPanelCursor(quarterBreakPanel, continueButton);
        }

        void HideAllPanels()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            if (quarterBreakPanel != null)
                quarterBreakPanel.SetActive(false);
        }

        void OnPlayClicked()
        {
            if (SceneFlow.Instance != null)
                SceneFlow.Instance.GoToCareer(CareerScreen.PreMatch);
            else
                GameManager.Instance.StartNewGame();
        }

        void OnQuitClicked()
        {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }

        void OnResumeClicked()
        {
            GameManager.Instance.TogglePause();
        }

        void OnRestartClicked()
        {
            GameManager.Instance.RestartGame();
        }

        void OnMainMenuClicked()
        {
            if (SceneFlow.Instance != null)
                SceneFlow.Instance.ReturnToCareer(CareerScreen.Home);
            else
                GameManager.Instance.QuitToMenu();
        }

        void OnPlayAgainClicked()
        {
            GameManager.Instance.RestartGame();
        }

        void OnExitClicked()
        {
            if (SceneFlow.Instance != null)
                SceneFlow.Instance.ReturnToCareer(CareerScreen.PostMatch);
            else
                GameManager.Instance.QuitToMenu();
        }

        void OnContinueClicked()
        {
            if (GameManager.Instance == null) return;

            // Disable immediately so a double-click cannot re-enter reset.
            if (continueButton != null)
                continueButton.interactable = false;

            // Always invoke GM recovery — ContinueFromQuarterBreak is null-safe / state-tolerant.
            GameManager.Instance.ContinueFromQuarterBreak();
        }
    }
}
