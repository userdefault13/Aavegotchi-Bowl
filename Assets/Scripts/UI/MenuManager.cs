using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;

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

        void Start()
        {
            SetupButtons();
            ShowMainMenu();
        }

        void Update()
        {
            UpdatePanels();
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

        void UpdatePanels()
        {
            if (GameManager.Instance == null) return;

            switch (GameManager.Instance.currentState)
            {
                case GameState.Menu:
                    ShowMainMenu();
                    break;
                case GameState.Playing:
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
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
        }

        void ShowPauseMenu()
        {
            HideAllPanels();
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
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
        }

        void ShowQuarterBreak()
        {
            HideAllPanels();
            if (quarterBreakPanel != null)
            {
                quarterBreakPanel.SetActive(true);
                
                if (quarterBreakText != null)
                {
                    quarterBreakText.text = $"End of Quarter {GameManager.Instance.currentQuarter - 1}";
                }
            }
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
            GameManager.Instance.QuitToMenu();
        }

        void OnPlayAgainClicked()
        {
            GameManager.Instance.RestartGame();
        }

        void OnExitClicked()
        {
            GameManager.Instance.QuitToMenu();
        }

        void OnContinueClicked()
        {
            GameManager.Instance.ContinueFromQuarterBreak();
        }
    }
}
