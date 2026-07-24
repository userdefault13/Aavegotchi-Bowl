using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public class OpponentAI : MonoBehaviour
    {
        [Header("AI Control")]
        public bool isOpponentPossession = false;
        public float playCallingDelay = 2f;
        private float playTimer = 0f;

        [Header("AI Difficulty")]
        [Range(0f, 1f)]
        public float difficulty = 0.5f;
        public int aiSkillLevel = 50;

        [Header("Play Selection Weights")]
        public float passPlayWeight = 0.5f;
        public float runPlayWeight = 0.3f;
        public float fieldGoalWeight = 0.1f;
        public float puntWeight = 0.1f;

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing)
            {
                return;
            }

            if (FieldManager.Instance != null)
            {
                isOpponentPossession = !FieldManager.Instance.isPlayerPossession;
            }

            if (isOpponentPossession)
            {
                playTimer += Time.deltaTime;

                if (playTimer >= playCallingDelay)
                {
                    ExecuteAIPlay();
                    playTimer = 0f;
                }
            }
        }

        void ExecuteAIPlay()
        {
            if (FieldManager.Instance == null) return;

            int down = FieldManager.Instance.down;
            int yardsToGo = FieldManager.Instance.yardsToGo;
            int yardsToEndzone = FieldManager.Instance.GetYardsToEndzone();

            PlayType selectedPlay = DecidePlay(down, yardsToGo, yardsToEndzone);

            ExecutePlay(selectedPlay);
        }

        PlayType DecidePlay(int down, int yardsToGo, int yardsToEndzone)
        {
            if (down == 4)
            {
                if (yardsToEndzone < 35 && yardsToGo > 5)
                {
                    return PlayType.FieldGoal;
                }
                else if (yardsToGo < 3 && Random.value > 0.7f)
                {
                    return Random.value > 0.5f ? PlayType.Pass : PlayType.Run;
                }
                else
                {
                    return PlayType.Punt;
                }
            }

            if (yardsToEndzone < 10)
            {
                if (yardsToGo <= 3)
                {
                    return PlayType.Run;
                }
                else
                {
                    return Random.value > 0.4f ? PlayType.Pass : PlayType.Run;
                }
            }

            if (yardsToGo > 10)
            {
                return Random.value > 0.3f ? PlayType.Pass : PlayType.Run;
            }
            else if (yardsToGo > 5)
            {
                return Random.value > 0.5f ? PlayType.Pass : PlayType.Run;
            }
            else
            {
                return Random.value > 0.6f ? PlayType.Run : PlayType.Pass;
            }
        }

        void ExecutePlay(PlayType playType)
        {
            switch (playType)
            {
                case PlayType.Pass:
                    ExecutePassPlay();
                    break;
                case PlayType.Run:
                    ExecuteRunPlay();
                    break;
                case PlayType.FieldGoal:
                    ExecuteFieldGoal();
                    break;
                case PlayType.Punt:
                    ExecutePunt();
                    break;
            }
        }

        void ExecutePassPlay()
        {
            Debug.Log("AI: Pass Play");

            float completionChance = 0.6f + (difficulty * 0.2f) + (aiSkillLevel / 200f);
            bool isComplete = Random.value < completionChance;

            if (isComplete)
            {
                int yardsGained = Random.Range(5, 20);
                yardsGained = (int)(yardsGained * (0.8f + difficulty * 0.4f));
                
                Debug.Log($"AI: Complete! Gain {yardsGained} yards");
                FieldManager.Instance.AdvanceBall(yardsGained);
            }
            else
            {
                Debug.Log("AI: Incomplete Pass");
                FieldManager.Instance.AdvanceBall(0);
            }
        }

        void ExecuteRunPlay()
        {
            Debug.Log("AI: Run Play");

            int baseYards = Random.Range(-1, 8);
            int yardsGained = (int)(baseYards * (0.8f + difficulty * 0.4f));
            yardsGained += Random.Range(0, aiSkillLevel / 20);

            Debug.Log($"AI: Run for {yardsGained} yards");
            FieldManager.Instance.AdvanceBall(yardsGained);
        }

        void ExecuteFieldGoal()
        {
            Debug.Log("AI: Field Goal Attempt");
            FieldManager.Instance.FieldGoalAttempt();
        }

        void ExecutePunt()
        {
            Debug.Log("AI: Punt");
            FieldManager.Instance.Punt();
        }

        public void SetDifficulty(float newDifficulty)
        {
            difficulty = Mathf.Clamp01(newDifficulty);
            aiSkillLevel = (int)(difficulty * 100);
        }
    }

    public enum PlayType
    {
        Pass,
        Run,
        FieldGoal,
        Punt
    }
}
