using UnityEngine;
using RetroBowl.Managers;

namespace RetroBowl.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Scoring Values")]
        public int touchdownPoints = 6;
        public int extraPointValue = 1;
        public int twoPointConversionValue = 2;
        public int fieldGoalPoints = 3;
        public int safetyPoints = 2;

        [Header("Stats Tracking")]
        public GameStats playerStats = new GameStats();
        public GameStats opponentStats = new GameStats();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ScoreTouchdown(bool isPlayer)
        {
            GameManager.Instance.AddScore(isPlayer, touchdownPoints);

            if (isPlayer)
            {
                playerStats.touchdowns++;
            }
            else
            {
                opponentStats.touchdowns++;
            }

            Debug.Log($"{(isPlayer ? "Player" : "Opponent")} Touchdown! +{touchdownPoints} points");

            AttemptExtraPoint(isPlayer);
        }

        void AttemptExtraPoint(bool isPlayer)
        {
            bool success = Random.value > 0.05f;

            if (success)
            {
                GameManager.Instance.AddScore(isPlayer, extraPointValue);
                Debug.Log($"Extra Point Good! +{extraPointValue}");
            }
            else
            {
                Debug.Log("Extra Point Missed!");
            }
        }

        public void ScoreFieldGoal(bool isPlayer, int distance)
        {
            GameManager.Instance.AddScore(isPlayer, fieldGoalPoints);

            if (isPlayer)
            {
                playerStats.fieldGoals++;
            }
            else
            {
                opponentStats.fieldGoals++;
            }

            Debug.Log($"{(isPlayer ? "Player" : "Opponent")} Field Goal ({distance} yards)! +{fieldGoalPoints} points");
        }

        public void ScoreSafety(bool isPlayer)
        {
            GameManager.Instance.AddScore(isPlayer, safetyPoints);

            if (isPlayer)
            {
                playerStats.safeties++;
            }
            else
            {
                opponentStats.safeties++;
            }

            Debug.Log($"{(isPlayer ? "Player" : "Opponent")} Safety! +{safetyPoints} points");
        }

        public void RecordPass(bool isPlayer, int yards, bool isComplete)
        {
            if (isPlayer)
            {
                playerStats.passAttempts++;
                if (isComplete)
                {
                    playerStats.passCompletions++;
                    playerStats.passingYards += yards;
                }
            }
            else
            {
                opponentStats.passAttempts++;
                if (isComplete)
                {
                    opponentStats.passCompletions++;
                    opponentStats.passingYards += yards;
                }
            }
        }

        public void RecordRush(bool isPlayer, int yards)
        {
            if (isPlayer)
            {
                playerStats.rushAttempts++;
                playerStats.rushingYards += yards;
            }
            else
            {
                opponentStats.rushAttempts++;
                opponentStats.rushingYards += yards;
            }
        }

        public void RecordTurnover(bool isPlayer)
        {
            if (isPlayer)
            {
                playerStats.turnovers++;
            }
            else
            {
                opponentStats.turnovers++;
            }
        }

        public void ResetStats()
        {
            playerStats = new GameStats();
            opponentStats = new GameStats();
        }

        public void PrintGameStats()
        {
            Debug.Log("=== GAME STATS ===");
            Debug.Log($"Player: {playerStats.GetSummary()}");
            Debug.Log($"Opponent: {opponentStats.GetSummary()}");
        }
    }

    [System.Serializable]
    public class GameStats
    {
        public int touchdowns = 0;
        public int fieldGoals = 0;
        public int safeties = 0;
        
        public int passAttempts = 0;
        public int passCompletions = 0;
        public int passingYards = 0;
        
        public int rushAttempts = 0;
        public int rushingYards = 0;
        
        public int turnovers = 0;

        public float GetCompletionPercentage()
        {
            if (passAttempts == 0) return 0f;
            return (float)passCompletions / passAttempts * 100f;
        }

        public float GetYardsPerPass()
        {
            if (passAttempts == 0) return 0f;
            return (float)passingYards / passAttempts;
        }

        public float GetYardsPerRush()
        {
            if (rushAttempts == 0) return 0f;
            return (float)rushingYards / rushAttempts;
        }

        public string GetSummary()
        {
            return $"TDs: {touchdowns}, FGs: {fieldGoals} | Pass: {passCompletions}/{passAttempts} ({GetCompletionPercentage():F1}%), {passingYards} yds | Rush: {rushAttempts} att, {rushingYards} yds | TOs: {turnovers}";
        }
    }
}
