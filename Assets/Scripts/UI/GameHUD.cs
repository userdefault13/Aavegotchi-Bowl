using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;

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

        [Header("Root")]
        public GameObject hudRoot;

        bool teamNamesBound;

        void Awake()
        {
            if (hudRoot == null)
            {
                var t = transform.Find("HUDPanel");
                if (t != null) hudRoot = t.gameObject;
            }

            // Full-screen HUD Image must never steal clicks from QuarterBreak Continue.
            if (hudRoot != null)
            {
                var img = hudRoot.GetComponent<Image>();
                if (img != null)
                    img.raycastTarget = false;
            }
        }

        void Start()
        {
            BindTeamNamesFromCareer();
        }

        void Update()
        {
            if (GameManager.Instance == null) return;

            // Keep score/clock visible during break, but do not sit above modal panels.
            bool show = GameManager.Instance.currentState == GameState.Playing
                        || GameManager.Instance.currentState == GameState.Paused
                        || GameManager.Instance.currentState == GameState.QuarterBreak;

            if (hudRoot != null && hudRoot.activeSelf != show)
                hudRoot.SetActive(show);

            if (show)
            {
                if (!teamNamesBound)
                    BindTeamNamesFromCareer();
                UpdateHUD();
            }
        }

        void BindTeamNamesFromCareer()
        {
            if (TeamManager.Instance == null) return;
            // HUD uses short abbreviations (Retro Bowl style: NYG 0  PHI 0).
            SetTeamNames(
                TeamManager.Instance.PlayerTeamAbbrev(),
                TeamManager.Instance.OpponentTeamAbbrev());
            teamNamesBound = true;
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
                if (GameManager.Instance.isPracticeMode)
                    quarterText.text = "PRAC";
                else if (GameManager.Instance.isOvertime)
                    quarterText.text = "OT";
                else
                    quarterText.text = QuarterLabel(GameManager.Instance.currentQuarter);
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
                    string weather = "";
                    if (WeatherSystem.Instance != null)
                        weather = $"  ·  {WeatherSystem.Instance.Label}";
                    yardLineText.text = $"Ball on {FieldManager.Instance.OwnYardLine}{weather}";
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

            teamNamesBound = true;
        }

        static string QuarterLabel(int quarter) => quarter switch
        {
            1 => "1st Qtr",
            2 => "2nd Qtr",
            3 => "3rd Qtr",
            _ => "4th Qtr"
        };
    }
}
