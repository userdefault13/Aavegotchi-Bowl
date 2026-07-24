using UnityEngine;

namespace RetroBowl.Core
{
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }

        [Header("Field Dimensions")]
        public float fieldLength = 100f;
        public float fieldWidth = 53.3f;
        public float yardLength = 1f;

        [Header("Ball Position")]
        public Transform ballTransform;
        public int currentYardLine = 20;
        public bool isPlayerPossession = true;
        public int down = 1;
        public int yardsToGo = 10;
        private int firstDownMarker = 30;

        [Header("Drive Management")]
        public int startingYardLine = 20;

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

        void Start()
        {
            InitializeDrive(true);
        }

        public void InitializeDrive(bool playerHasBall)
        {
            isPlayerPossession = playerHasBall;
            currentYardLine = startingYardLine;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo;
            
            UpdateBallPosition();
        }

        public void AdvanceBall(int yards)
        {
            currentYardLine += yards;

            currentYardLine = Mathf.Clamp(currentYardLine, 0, 100);

            if (currentYardLine >= 100)
            {
                Touchdown();
                return;
            }

            if (currentYardLine >= firstDownMarker)
            {
                FirstDown();
            }
            else
            {
                down++;
                if (down > 4)
                {
                    Turnover();
                }
            }

            UpdateBallPosition();
        }

        void FirstDown()
        {
            down = 1;
            yardsToGo = Mathf.Min(10, 100 - currentYardLine);
            firstDownMarker = currentYardLine + yardsToGo;
            Debug.Log("First Down!");
        }

        void Touchdown()
        {
            Debug.Log($"{(isPlayerPossession ? "Player" : "Opponent")} Touchdown!");
            GameManager.Instance.AddScore(isPlayerPossession, 6);
            
            InitializeDrive(!isPlayerPossession);
        }

        void Turnover()
        {
            Debug.Log("Turnover on Downs!");
            isPlayerPossession = !isPlayerPossession;
            currentYardLine = 100 - currentYardLine;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo;
            
            UpdateBallPosition();
        }

        public void FieldGoalAttempt()
        {
            int distance = 100 - currentYardLine + 17;
            
            float successChance = Mathf.Clamp(1f - (distance / 70f), 0.3f, 0.95f);
            bool success = Random.value < successChance;

            if (success)
            {
                Debug.Log("Field Goal Good!");
                GameManager.Instance.AddScore(isPlayerPossession, 3);
            }
            else
            {
                Debug.Log("Field Goal Missed!");
            }

            InitializeDrive(!isPlayerPossession);
        }

        public void Punt()
        {
            int puntDistance = Random.Range(35, 55);
            int newYardLine = 100 - Mathf.Min(100, currentYardLine + puntDistance);
            
            Debug.Log($"Punt! Ball at opponent {newYardLine} yard line");
            
            isPlayerPossession = !isPlayerPossession;
            currentYardLine = newYardLine;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo;
            
            UpdateBallPosition();
        }

        void UpdateBallPosition()
        {
            if (ballTransform != null)
            {
                float xPosition = (currentYardLine * yardLength) - (fieldLength / 2f);
                ballTransform.position = new Vector3(xPosition, 0.5f, 0);
            }
        }

        public string GetDownAndDistance()
        {
            if (yardsToGo >= 10)
            {
                return $"{down}st & {yardsToGo}";
            }
            else
            {
                return $"{down}st & {yardsToGo}";
            }
        }

        public int GetYardsToEndzone()
        {
            return 100 - currentYardLine;
        }
    }
}
