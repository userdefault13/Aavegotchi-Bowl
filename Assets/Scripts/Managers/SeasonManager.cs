using System.Collections.Generic;
using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Data;

namespace RetroBowl.Managers
{
    public class SeasonManager : MonoBehaviour
    {
        public static SeasonManager Instance { get; private set; }

        [Header("Season Progress")]
        public int currentWeek = 1;
        public int totalWeeks = 17;
        public int currentSeason = 1;

        [Header("Schedule")]
        public List<Game> schedule = new List<Game>();

        [Header("Standings")]
        public int playerWins = 0;
        public int playerLosses = 0;

        [Header("Meta")]
        [Range(0f, 100f)] public float teamMorale = 70f;

        void Awake()
        {
            bool onAppRoot = GameManager.Instance != null
                             && gameObject == GameManager.Instance.gameObject;

            if (Instance == null || Instance == this)
            {
                Instance = this;
                if (transform.parent == null)
                    DontDestroyOnLoad(gameObject);
                return;
            }

            bool instanceOnAppRoot = GameManager.Instance != null
                                     && Instance.gameObject == GameManager.Instance.gameObject;
            if (onAppRoot && !instanceOnAppRoot)
            {
                Destroy(Instance.gameObject);
                Instance = this;
                return;
            }

            Destroy(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Keep an in-progress season — MatchScene duplicates must not wipe week/record.
            if (schedule == null || schedule.Count == 0)
                GenerateSchedule();
        }

        public void GenerateSchedule()
        {
            schedule.Clear();

            for (int week = 1; week <= totalWeeks; week++)
            {
                Game game = new Game
                {
                    week = week,
                    isPlayed = false,
                    playerScore = 0,
                    opponentScore = 0
                };
                schedule.Add(game);
            }

            Debug.Log($"Generated {totalWeeks}-week schedule");
        }

        public void CompleteGame(int playerScore, int opponentScore)
        {
            if (currentWeek <= totalWeeks && currentWeek > 0)
            {
                Game currentGame = schedule[currentWeek - 1];
                currentGame.isPlayed = true;
                currentGame.playerScore = playerScore;
                currentGame.opponentScore = opponentScore;

                if (playerScore > opponentScore)
                {
                    playerWins++;
                    AdjustMorale(5f);
                    Debug.Log($"Win! Record: {playerWins}-{playerLosses}");
                }
                else if (playerScore < opponentScore)
                {
                    playerLosses++;
                    AdjustMorale(-6f);
                    Debug.Log($"Loss! Record: {playerWins}-{playerLosses}");
                }
                else
                {
                    AdjustMorale(-1f);
                    Debug.Log($"Tie! Record: {playerWins}-{playerLosses}");
                }

                currentWeek++;

                if (currentWeek > totalWeeks)
                {
                    EndSeason();
                }
            }
        }

        public void AdjustMorale(float delta)
        {
            teamMorale = Mathf.Clamp(teamMorale + delta, 0f, 100f);
        }

        void EndSeason()
        {
            Debug.Log($"Season {currentSeason} Complete! Final Record: {playerWins}-{playerLosses}");

            if (playerWins >= totalWeeks / 2)
            {
                Debug.Log("Made the Playoffs!");
            }
            else
            {
                Debug.Log("Missed the Playoffs");
            }
        }

        public void AdvanceToNextSeason()
        {
            currentSeason++;
            currentWeek = 1;
            playerWins = 0;
            playerLosses = 0;
            teamMorale = 70f;
            GenerateSchedule();

            Debug.Log($"Starting Season {currentSeason}");
        }

        public string GetRecord()
        {
            return $"{playerWins}-{playerLosses}";
        }

        public float GetWinPercentage()
        {
            int totalGames = playerWins + playerLosses;
            if (totalGames == 0) return 0f;
            return (float)playerWins / totalGames;
        }
    }

    [System.Serializable]
    public class Game
    {
        public int week;
        public bool isPlayed;
        public int playerScore;
        public int opponentScore;
    }
}
