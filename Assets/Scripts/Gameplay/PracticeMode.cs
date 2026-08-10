using UnityEngine;
using RetroBowl.App;
using RetroBowl.Core;
using RetroBowl.Managers;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Sandbox drills: frozen clock, midfield LOS, no season record impact.
    /// </summary>
    public static class PracticeMode
    {
        public static bool IsTrainingFacilityTutorial { get; private set; }
        public static bool IsTrainingFacility { get; private set; }
        public static TrainingOffice PendingOffice { get; private set; }

        /// <summary>Legacy same-scene entry — prefer SceneFlow.StartMatch(Practice).</summary>
        public static void Start()
        {
            if (SceneFlow.Instance != null)
            {
                SceneFlow.Instance.StartMatch(MatchLaunchArgs.Practice());
                return;
            }

            if (GameManager.Instance == null) return;
            GameManager.Instance.isPracticeMode = true;
            ConfigureAfterSceneLoad(MatchLaunchArgs.Practice());
        }

        /// <summary>Called after MatchScene loads for practice / training.</summary>
        public static void ConfigureAfterSceneLoad(MatchLaunchArgs args)
        {
            if (GameManager.Instance == null) return;

            IsTrainingFacilityTutorial = args.trainingTutorial;
            IsTrainingFacility = args.trainingFacility;
            PendingOffice = args.trainingOffice;

            GameManager.Instance.isPracticeMode = true;
            GameManager.Instance.isOvertime = false;
            if (TeamManager.Instance != null)
                TeamManager.Instance.EnsureDefaultTeamsIfNeeded();

            GameManager.Instance.StartNewGame();

            if (FieldManager.Instance != null)
                FieldManager.Instance.SetupPracticeField();

            WeatherSystem.EnsureExists();
            WeatherSystem.Instance?.RollForMatch(forceClear: true);

            GameManager.Instance.SkipPostScoreFlowOnce();
            GameManager.Instance.ReadyNextPlay();

            if (args.trainingFacility)
            {
                RetroLookApplier.RefreshTrainingFacilityField();
                TrainingFacilityHub.EnsureExists(args.trainingOffice);
            }
            else if (args.trainingTutorial)
            {
                RetroLookApplier.RefreshTrainingFacilityField();
                TrainingTutorialOverlay.EnsureForTraining();
            }
            else
            {
                PlayBanner.Show("PRACTICE MODE\nCLOCK OFF · NO RECORD", 2f, BannerTone.Neutral);
            }

            Debug.Log(args.trainingFacility
                ? "Training Facility started"
                : args.trainingTutorial
                    ? "Training Facility tutorial started"
                    : "Practice mode started");
        }

        /// <summary>Back-compat overload.</summary>
        public static void ConfigureAfterSceneLoad(bool trainingTutorial = false)
            => ConfigureAfterSceneLoad(trainingTutorial
                ? MatchLaunchArgs.TrainingFacilityTutorial()
                : MatchLaunchArgs.Practice());

        public static void ExitToMenu()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.isPracticeMode = false;
            IsTrainingFacilityTutorial = false;
            IsTrainingFacility = false;
            PendingOffice = TrainingOffice.None;
            if (TrainingTutorialOverlay.Instance != null)
                TrainingTutorialOverlay.Instance.Teardown();
            if (TrainingFacilityHub.Instance != null)
                TrainingFacilityHub.Instance.Teardown();
            if (SceneFlow.Instance != null)
                SceneFlow.Instance.ReturnToCareer(CareerScreen.Training);
            else
                GameManager.Instance.QuitToMenu();
        }
    }
}
