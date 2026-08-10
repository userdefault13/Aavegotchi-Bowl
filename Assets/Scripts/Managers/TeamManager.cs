using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Data;

namespace RetroBowl.Managers
{
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        [Header("Teams")]
        public TeamData playerTeam;
        public TeamData opponentTeam;
        /// <summary>Player franchise is the home team (USDC). Opponent is the visitor (UNI).</summary>
        public bool playerIsHome = true;

        void Awake()
        {
            bool onAppRoot = GameManager.Instance != null
                             && gameObject == GameManager.Instance.gameObject;

            if (Instance == null || Instance == this)
            {
                Instance = this;
                if (transform.parent == null)
                    DontDestroyOnLoad(gameObject);
                return;
            }

            // Prefer the DDOL AppRoot franchise host over a MatchScene leftover.
            bool instanceOnAppRoot = GameManager.Instance != null
                                     && Instance.gameObject == GameManager.Instance.gameObject;
            if (onAppRoot && !instanceOnAppRoot)
            {
                Destroy(Instance.gameObject);
                Instance = this;
                return;
            }

            // Never Destroy(gameObject) on AppRoot — it hosts GameManager / SceneFlow.
            Destroy(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Only seed defaults once — never overwrite a franchise already chosen / loaded.
            EnsureDefaultTeamsIfNeeded();
        }

        /// <summary>
        /// First-run defaults: USDC (home / player) vs UNI (visitor / opponent).
        /// Preserves in-memory career roster across Career↔Match
        /// and after SaveService.LoadCareer / ChooseTeam.
        /// </summary>
        public void EnsureDefaultTeamsIfNeeded()
        {
            // Upgrade old seed franchises so Editor / unsaved runs pick up USDC home + UNI visitor.
            if (playerTeam != null
                && playerTeam.cityName == "New York"
                && playerTeam.teamName == "Thunder")
            {
                playerTeam = null;
            }
            if (opponentTeam != null
                && opponentTeam.cityName == "Los Angeles"
                && opponentTeam.teamName == "Raptors")
            {
                opponentTeam = null;
            }

            if (playerTeam == null || string.IsNullOrEmpty(playerTeam.teamName))
            {
                // maUSDC skin — home side.
                playerTeam = new TeamData("Aavegotchi", "USDC",
                    new Color(0.15f, 0.55f, 0.95f), new Color(1f, 1f, 1f), "USDC");
                playerTeam.GenerateRoster();
            }
            else if (playerTeam.roster == null || playerTeam.roster.Count == 0)
            {
                playerTeam.GenerateRoster();
            }

            if (opponentTeam == null || string.IsNullOrEmpty(opponentTeam.teamName))
            {
                // maUNI skin — visiting side.
                opponentTeam = new TeamData("Aavegotchi", "UNI",
                    new Color(0.85f, 0.25f, 0.75f), new Color(0.15f, 0.1f, 0.2f), "UNI");
                opponentTeam.GenerateRoster();
            }
            else if (opponentTeam.roster == null || opponentTeam.roster.Count == 0)
            {
                opponentTeam.GenerateRoster();
            }

            if (string.IsNullOrEmpty(playerTeam.abbreviation)
                && playerTeam.teamName == "USDC")
                playerTeam.abbreviation = "USDC";
            if (opponentTeam != null
                && string.IsNullOrEmpty(opponentTeam.abbreviation)
                && opponentTeam.teamName == "UNI")
                opponentTeam.abbreviation = "UNI";

            // Logged once — EnsureDefaultTeamsIfNeeded runs from formation/LateUpdate often.
            if (!_loggedDefaultTeams)
            {
                _loggedDefaultTeams = true;
                Debug.Log($"Home (player): {playerTeam.cityName} {playerTeam.teamName} [{playerTeam.GetAbbreviation()}]");
                if (opponentTeam != null)
                    Debug.Log($"Visitor (opp): {opponentTeam.cityName} {opponentTeam.teamName} [{opponentTeam.GetAbbreviation()}]");
            }
        }

        bool _loggedDefaultTeams;

        public void GenerateNewOpponent()
        {
            string[] cities = { "Boston", "Chicago", "Dallas", "Miami", "Seattle", "Denver", "Phoenix", "Detroit", "Atlanta", "Houston" };
            string[] names = { "Eagles", "Bears", "Sharks", "Warriors", "Knights", "Titans", "Dragons", "Legends", "Storm", "Blaze" };

            string city = cities[Random.Range(0, cities.Length)];
            string name = names[Random.Range(0, names.Length)];
            Color primary = new Color(Random.value, Random.value, Random.value);
            Color secondary = new Color(Random.value, Random.value, Random.value);

            opponentTeam = new TeamData(city, name, primary, secondary);
            opponentTeam.GenerateRoster();

            Debug.Log($"New Opponent: {opponentTeam.cityName} {opponentTeam.teamName} - Rating: {opponentTeam.GetTeamRating()}");
        }

        public string PlayerTeamLabel()
            => playerTeam != null ? $"{playerTeam.cityName} {playerTeam.teamName}" : "HOME";

        public string OpponentTeamLabel()
            => opponentTeam != null ? $"{opponentTeam.cityName} {opponentTeam.teamName}" : "AWAY";

        public string PlayerTeamAbbrev()
            => playerTeam != null ? playerTeam.GetAbbreviation() : "USDC";

        public string OpponentTeamAbbrev()
            => opponentTeam != null ? opponentTeam.GetAbbreviation() : "UNI";

        public string HomeTeamAbbrev()
            => playerIsHome ? PlayerTeamAbbrev() : OpponentTeamAbbrev();

        public string VisitorTeamAbbrev()
            => playerIsHome ? OpponentTeamAbbrev() : PlayerTeamAbbrev();

        public PlayerData GetPlayerTeamQuarterback()
        {
            return playerTeam?.GetPlayerByPosition(PlayerPosition.Quarterback);
        }

        public PlayerData GetOpponentTeamQuarterback()
        {
            return opponentTeam?.GetPlayerByPosition(PlayerPosition.Quarterback);
        }
    }
}
