using System.Collections.Generic;
using UnityEngine;

namespace RetroBowl.Data
{
    [System.Serializable]
    public class TeamData
    {
        public string teamName;
        public string cityName;
        public Color primaryColor;
        public Color secondaryColor;
        public List<PlayerData> roster;

        public int wins = 0;
        public int losses = 0;

        public TeamData(string city, string name, Color primary, Color secondary)
        {
            cityName = city;
            teamName = name;
            primaryColor = primary;
            secondaryColor = secondary;
            roster = new List<PlayerData>();
        }

        public void GenerateRoster()
        {
            roster.Clear();

            roster.Add(new PlayerData(GeneratePlayerName(), 12, PlayerPosition.Quarterback));
            roster.Add(new PlayerData(GeneratePlayerName(), 28, PlayerPosition.RunningBack));
            roster.Add(new PlayerData(GeneratePlayerName(), 22, PlayerPosition.RunningBack));
            
            roster.Add(new PlayerData(GeneratePlayerName(), 11, PlayerPosition.WideReceiver));
            roster.Add(new PlayerData(GeneratePlayerName(), 18, PlayerPosition.WideReceiver));
            roster.Add(new PlayerData(GeneratePlayerName(), 85, PlayerPosition.WideReceiver));
            
            roster.Add(new PlayerData(GeneratePlayerName(), 88, PlayerPosition.TightEnd));
            
            for (int i = 0; i < 5; i++)
            {
                roster.Add(new PlayerData(GeneratePlayerName(), 50 + i, PlayerPosition.OffensiveLine));
            }

            for (int i = 0; i < 4; i++)
            {
                roster.Add(new PlayerData(GeneratePlayerName(), 90 + i, PlayerPosition.DefensiveLine));
            }

            for (int i = 0; i < 3; i++)
            {
                roster.Add(new PlayerData(GeneratePlayerName(), 54 + i, PlayerPosition.Linebacker));
            }

            for (int i = 0; i < 3; i++)
            {
                roster.Add(new PlayerData(GeneratePlayerName(), 20 + i, PlayerPosition.Cornerback));
            }

            for (int i = 0; i < 2; i++)
            {
                roster.Add(new PlayerData(GeneratePlayerName(), 30 + i, PlayerPosition.Safety));
            }
        }

        private string GeneratePlayerName()
        {
            string[] firstNames = { "John", "Mike", "Tom", "Jake", "Chris", "Matt", "Josh", "Tyler", "Brandon", "Kevin", "Ryan", "Alex", "Dan", "Eric", "Steve" };
            string[] lastNames = { "Smith", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Garcia" };

            return firstNames[Random.Range(0, firstNames.Length)] + " " + lastNames[Random.Range(0, lastNames.Length)];
        }

        public int GetTeamRating()
        {
            if (roster.Count == 0) return 0;

            int totalRating = 0;
            foreach (var player in roster)
            {
                totalRating += player.stats.GetOverallRating();
            }
            return totalRating / roster.Count;
        }

        public PlayerData GetPlayerByPosition(PlayerPosition position)
        {
            foreach (var player in roster)
            {
                if (player.position == position)
                {
                    return player;
                }
            }
            return null;
        }
    }
}
