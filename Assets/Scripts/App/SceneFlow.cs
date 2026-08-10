using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI;

namespace RetroBowl.App
{
    /// <summary>Boot → Career ↔ Match scene orchestration.</summary>
    public class SceneFlow : MonoBehaviour
    {
        public static SceneFlow Instance { get; private set; }

        public const string BootSceneName = "BootScene";
        public const string CareerSceneName = "CareerScene";
        public const string MatchSceneName = "MatchScene";

        public bool OwnsLifecycle { get; private set; } = true;

        MatchLaunchArgs pendingLaunch;
        CareerScreen returnScreen = CareerScreen.Home;
        public int LastPlayerScore { get; private set; }
        public int LastOpponentScore { get; private set; }
        public bool LastWasPractice { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public void GoToCareer(CareerScreen screen)
        {
            returnScreen = screen;
            StartCoroutine(LoadCareerRoutine(screen));
        }

        public void StartMatch(MatchLaunchArgs args)
        {
            pendingLaunch = args;
            StartCoroutine(LoadMatchRoutine());
        }

        public void RecordResult(int playerScore, int opponentScore, bool practice)
        {
            LastPlayerScore = playerScore;
            LastOpponentScore = opponentScore;
            LastWasPractice = practice;
        }

        public void ReturnToCareer(CareerScreen screen)
        {
            if (GameManager.Instance != null)
                RecordResult(
                    GameManager.Instance.playerScore,
                    GameManager.Instance.opponentScore,
                    GameManager.Instance.isPracticeMode);
            GoToCareer(screen);
        }

        public void QuitApp()
        {
            SaveService.Instance?.SaveCareer();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        IEnumerator LoadCareerRoutine(CareerScreen screen)
        {
            // Don't auto-save over slots when opening Save Select / onboarding rooms.
            if (screen != CareerScreen.SaveSelect
                && screen != CareerScreen.Welcome
                && screen != CareerScreen.NewCareer
                && SaveService.Instance != null
                && SaveService.Instance.HasActiveSlot
                && SaveService.Instance.HasChosenTeam)
            {
                SaveService.Instance.SaveCareer();
            }

            var op = SceneManager.LoadSceneAsync(CareerSceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isPracticeMode = false;
                GameManager.Instance.SetGameState(GameState.Menu);
            }

            CareerNav.EnsureExists();
            CareerNav.Instance?.Show(screen);
        }

        IEnumerator LoadMatchRoutine()
        {
            CareerNav.Instance?.HideAll();

            var op = SceneManager.LoadSceneAsync(MatchSceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;

            yield return null; // wait one frame for scene Awakes

            // Re-cull after Awakes — deferred Destroy from MatchBootstrap may still be pending.
            MatchBootstrap.CullDuplicateManagers();

            if (GameManager.Instance != null)
                MatchBootstrap.EnsureMatchSystems(GameManager.Instance.gameObject);

            // Prefer MatchMenuController on any MenuManager in the match scene.
            foreach (var m in Object.FindObjectsByType<MenuManager>())
            {
                if (m.GetComponent<MatchMenuController>() == null)
                    m.gameObject.AddComponent<MatchMenuController>();
            }

            // Career franchise lives on DDOL TeamManager — never regenerate player roster here.
            if (TeamManager.Instance != null)
                TeamManager.Instance.EnsureDefaultTeamsIfNeeded();

            bool practice = pendingLaunch.mode == MatchMode.Practice;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isPracticeMode = practice;
                if (practice)
                    PracticeMode.ConfigureAfterSceneLoad(pendingLaunch);
                else
                {
                    // Prefer opponent already rolled on Week / PreMatch so the matchup matches the UI.
                    if (TeamManager.Instance != null
                        && (TeamManager.Instance.opponentTeam == null
                            || string.IsNullOrEmpty(TeamManager.Instance.opponentTeam.teamName)))
                        TeamManager.Instance.GenerateNewOpponent();
                    WeatherSystem.EnsureExists();
                    if (pendingLaunch.rollWeather)
                        WeatherSystem.Instance?.RollForMatch();
                    GameManager.Instance.StartNewGame();
                    // Week games open with GET READY! / Receive → kickoff (not play-call).
                    PostScoreFlow.BeginOpeningKickoff();
                }
            }
        }
    }
}
