using UnityEngine;
using RetroBowl.App;
using RetroBowl.Core;
using RetroBowl.Gameplay;

namespace RetroBowl.UI
{
    /// <summary>
    /// MatchScene menu behavior — pause / quarter break / handoff to Career PostMatch.
    /// Lives beside legacy MenuManager panels.
    /// </summary>
    public class MatchMenuController : MonoBehaviour
    {
        MenuManager menus;

        void Awake()
        {
            menus = GetComponent<MenuManager>();
        }

        void Start()
        {
            // Career overlays must not appear over the field.
            CareerHub.Instance?.Hide();
            if (GameManager.Instance != null
                && SceneFlow.Instance != null
                && GameManager.Instance.currentState == GameState.Menu)
            {
                // Match loaded — should already be Playing via SceneFlow.
            }
        }

        public void ExitMatchToCareer()
        {
            if (GameManager.Instance != null && GameManager.Instance.isPracticeMode)
            {
                PracticeMode.ExitToMenu();
                return;
            }

            if (SceneFlow.Instance != null)
                SceneFlow.Instance.ReturnToCareer(CareerScreen.PostMatch);
            else
                GameManager.Instance?.QuitToMenu();
        }
    }
}
