using UnityEngine;
using RetroBowl.Data;

namespace RetroBowl.Managers
{
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        [Header("Teams")]
        public TeamData playerTeam;
        public TeamData opponentTeam;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            InitializeTeams();
        }

        void InitializeTeams()
        {
            playerTeam = new TeamData("New York", "Thunder", new Color(0.1f, 0.3f, 0.8f), new Color(1f, 1f, 1f));
            playerTeam.GenerateRoster();

            opponentTeam = new TeamData("Los Angeles", "Raptors", new Color(0.8f, 0.1f, 0.1f), new Color(0.2f, 0.2f, 0.2f));
            opponentTeam.GenerateRoster();

            Debug.Log($"Player Team: {playerTeam.cityName} {playerTeam.teamName} - Rating: {playerTeam.GetTeamRating()}");
            Debug.Log($"Opponent Team: {opponentTeam.cityName} {opponentTeam.teamName} - Rating: {opponentTeam.GetTeamRating()}");
        }

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

        public PlayerData GetPlayerTeamQuarterback()
        {
            return playerTeam.GetPlayerByPosition(PlayerPosition.Quarterback);
        }

        public PlayerData GetOpponentTeamQuarterback()
        {
            return opponentTeam.GetPlayerByPosition(PlayerPosition.Quarterback);
        }
    }
}
