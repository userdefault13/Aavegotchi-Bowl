using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;

namespace RetroBowl.UI
{
    public class PlayCallingUI : MonoBehaviour
    {
        [Header("Play Selection Panel")]
        public GameObject playSelectionPanel;
        public Button passPlayButton;
        public Button runPlayButton;
        public Button fieldGoalButton;
        public Button puntButton;

        [Header("Play Info")]
        public TextMeshProUGUI situationText;
        public TextMeshProUGUI recommendationText;

        private bool isPlaySelected = false;

        void Start()
        {
            SetupButtons();
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
            {
                if (!isPlaySelected && FieldManager.Instance != null && FieldManager.Instance.isPlayerPossession)
                {
                    ShowPlaySelection();
                }
            }
        }

        void SetupButtons()
        {
            if (passPlayButton != null)
                passPlayButton.onClick.AddListener(OnPassPlaySelected);
            
            if (runPlayButton != null)
                runPlayButton.onClick.AddListener(OnRunPlaySelected);
            
            if (fieldGoalButton != null)
                fieldGoalButton.onClick.AddListener(OnFieldGoalSelected);
            
            if (puntButton != null)
                puntButton.onClick.AddListener(OnPuntSelected);
        }

        void ShowPlaySelection()
        {
            if (playSelectionPanel != null)
            {
                playSelectionPanel.SetActive(true);
                UpdatePlayInfo();
            }
        }

        void HidePlaySelection()
        {
            if (playSelectionPanel != null)
            {
                playSelectionPanel.SetActive(false);
            }
        }

        void UpdatePlayInfo()
        {
            if (FieldManager.Instance == null) return;

            if (situationText != null)
            {
                situationText.text = $"{FieldManager.Instance.GetDownAndDistance()} at {FieldManager.Instance.currentYardLine}";
            }

            if (recommendationText != null)
            {
                int down = FieldManager.Instance.down;
                int yardsToGo = FieldManager.Instance.yardsToGo;
                int yardsToEndzone = FieldManager.Instance.GetYardsToEndzone();

                if (down == 4)
                {
                    if (yardsToEndzone < 35 && yardsToGo > 5)
                    {
                        recommendationText.text = "Recommended: Field Goal";
                    }
                    else
                    {
                        recommendationText.text = "Recommended: Punt";
                    }
                }
                else if (yardsToGo > 7)
                {
                    recommendationText.text = "Recommended: Pass";
                }
                else if (yardsToGo <= 3)
                {
                    recommendationText.text = "Recommended: Run";
                }
                else
                {
                    recommendationText.text = "Your choice!";
                }
            }

            if (fieldGoalButton != null)
            {
                int yardsToEndzone = FieldManager.Instance.GetYardsToEndzone();
                fieldGoalButton.interactable = yardsToEndzone < 45;
            }
        }

        void OnPassPlaySelected()
        {
            Debug.Log("Pass Play Selected");
            isPlaySelected = true;
            HidePlaySelection();
            Invoke(nameof(ResetPlaySelection), 10f);
        }

        void OnRunPlaySelected()
        {
            Debug.Log("Run Play Selected");
            isPlaySelected = true;
            HidePlaySelection();
            
            int yardsGained = Random.Range(-2, 12);
            FieldManager.Instance.AdvanceBall(yardsGained);
            Debug.Log($"Run for {yardsGained} yards!");
            
            Invoke(nameof(ResetPlaySelection), 3f);
        }

        void OnFieldGoalSelected()
        {
            Debug.Log("Field Goal Attempt");
            isPlaySelected = true;
            HidePlaySelection();
            
            FieldManager.Instance.FieldGoalAttempt();
            
            Invoke(nameof(ResetPlaySelection), 3f);
        }

        void OnPuntSelected()
        {
            Debug.Log("Punt");
            isPlaySelected = true;
            HidePlaySelection();
            
            FieldManager.Instance.Punt();
            
            Invoke(nameof(ResetPlaySelection), 3f);
        }

        void ResetPlaySelection()
        {
            isPlaySelected = false;
        }
    }
}
