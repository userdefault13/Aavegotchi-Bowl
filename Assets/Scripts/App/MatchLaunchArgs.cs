namespace RetroBowl.App
{
    public enum MatchMode
    {
        WeekGame,
        Practice
    }

    [System.Serializable]
    public struct MatchLaunchArgs
    {
        public MatchMode mode;
        public bool rollWeather;
        public int playerScoreSeed;
        public int opponentScoreSeed;
        /// <summary>Career onboarding Training Facility — passing tip overlays.</summary>
        public bool trainingTutorial;
        /// <summary>Full Training Facility hub (offices → active-8 → drill matchup).</summary>
        public bool trainingFacility;
        public TrainingOffice trainingOffice;

        public static MatchLaunchArgs WeekGame() => new MatchLaunchArgs
        {
            mode = MatchMode.WeekGame,
            rollWeather = true
        };

        public static MatchLaunchArgs Practice() => new MatchLaunchArgs
        {
            mode = MatchMode.Practice,
            rollWeather = true
        };

        /// <summary>Legacy onboarding tip chain on the practice field.</summary>
        public static MatchLaunchArgs TrainingFacilityTutorial() => new MatchLaunchArgs
        {
            mode = MatchMode.Practice,
            rollWeather = false,
            trainingTutorial = true
        };

        /// <summary>Training Facility — practice field + office / play drill hub.</summary>
        public static MatchLaunchArgs TrainingFacility(TrainingOffice office = TrainingOffice.None)
            => new MatchLaunchArgs
            {
                mode = MatchMode.Practice,
                rollWeather = false,
                trainingFacility = true,
                trainingOffice = office
            };
    }
}
