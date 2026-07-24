using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;

namespace RetroBowl.UI
{
    public class GameHUD : MonoBehaviour
    {
        [Header("Score Display")]
        public TextMeshProUGUI playerScoreText;
        public TextMeshProUGUI opponentScoreText;

        [Header("Game Info")]
        public TextMeshProUGUI quarterText;
        public TextMeshProUGUI timeText;
        public TextMeshProUGUI downAndDistanceText;
        public TextMeshProUGUI yardLineText;

        [Header("Team Names")]
        public TextMeshProUGUI playerTeamNameText;
        public TextMeshProUGUI opponentTeamNameText;

        void Update()
        {
            UpdateHUD();
        }

        void UpdateHUD()
        {
            if (GameManager.Instance == null) return;

            if (playerScoreText != null)
            {
                playerScoreText.text = GameManager.Instance.playerScore.ToString();
            }

            if (opponentScoreText != null)
            {
                opponentScoreText.text = GameManager.Instance.opponentScore.ToString();
            }

            if (quarterText != null)
            {
                quarterText.text = $"Q{GameManager.Instance.currentQuarter}";
            }

            if (timeText != null)
            {
                timeText.text = GameManager.Instance.GetFormattedTime();
            }

            if (FieldManager.Instance != null)
            {
                if (downAndDistanceText != null)
                {
                    downAndDistanceText.text = FieldManager.Instance.GetDownAndDistance();
                }

                if (yardLineText != null)
                {
                    yardLineText.text = $"Ball on {FieldManager.Instance.currentYardLine}";
                }
            }
        }

        public void SetTeamNames(string playerTeam, string opponentTeam)
        {
            if (playerTeamNameText != null)
            {
                playerTeamNameText.text = playerTeam;
            }

            if (opponentTeamNameText != null)
            {
                opponentTeamNameText.text = opponentTeam;
            }
        }
    }
}
