using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroBowl.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        public GameState currentState = GameState.Menu;
        public int currentQuarter = 1;
        public float quarterTimeRemaining = 120f;
        public const float QUARTER_DURATION = 120f;

        [Header("Score")]
        public int playerScore = 0;
        public int opponentScore = 0;

        [Header("Game Settings")]
        public int quartersPerGame = 4;
        public bool isPlayoffs = false;

        private bool isPaused = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            SetGameState(GameState.Menu);
        }

        void Update()
        {
            if (currentState == GameState.Playing)
            {
                UpdateGameTime();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        void UpdateGameTime()
        {
            if (!isPaused && currentState == GameState.Playing)
            {
                quarterTimeRemaining -= Time.deltaTime;

                if (quarterTimeRemaining <= 0)
                {
                    EndQuarter();
                }
            }
        }

        void EndQuarter()
        {
            currentQuarter++;
            
            if (currentQuarter > quartersPerGame)
            {
                EndGame();
            }
            else
            {
                quarterTimeRemaining = QUARTER_DURATION;
                SetGameState(GameState.QuarterBreak);
            }
        }

        void EndGame()
        {
            SetGameState(GameState.GameOver);
            
            if (playerScore > opponentScore)
            {
                Debug.Log("Player Wins!");
            }
            else if (opponentScore > playerScore)
            {
                Debug.Log("Opponent Wins!");
            }
            else
            {
                Debug.Log("It's a Tie!");
            }
        }

        public void SetGameState(GameState newState)
        {
            currentState = newState;
            Debug.Log($"Game State: {newState}");

            switch (newState)
            {
                case GameState.Menu:
                    Time.timeScale = 1f;
                    break;
                case GameState.Playing:
                    Time.timeScale = 1f;
                    isPaused = false;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    isPaused = true;
                    break;
                case GameState.QuarterBreak:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
            }
        }

        public void StartNewGame()
        {
            playerScore = 0;
            opponentScore = 0;
            currentQuarter = 1;
            quarterTimeRemaining = QUARTER_DURATION;
            SetGameState(GameState.Playing);
        }

        public void AddScore(bool isPlayer, int points)
        {
            if (isPlayer)
            {
                playerScore += points;
            }
            else
            {
                opponentScore += points;
            }

            Debug.Log($"Score - Player: {playerScore} | Opponent: {opponentScore}");
        }

        public void TogglePause()
        {
            if (currentState == GameState.Playing)
            {
                SetGameState(GameState.Paused);
            }
            else if (currentState == GameState.Paused)
            {
                SetGameState(GameState.Playing);
            }
        }

        public void ContinueFromQuarterBreak()
        {
            SetGameState(GameState.Playing);
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            StartNewGame();
        }

        public void QuitToMenu()
        {
            SetGameState(GameState.Menu);
        }

        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(quarterTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(quarterTimeRemaining % 60);
            return $"{minutes:00}:{seconds:00}";
        }
    }

    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        QuarterBreak,
        GameOver
    }
}
