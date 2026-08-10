using System.Collections.Generic;
using UnityEngine;

namespace RetroBowl.Data
{
    [System.Serializable]
    public class TeamData
    {
        public string teamName;
        public string cityName;
        /// <summary>Optional HUD code (e.g. USDC / UNI). When empty, derived from city.</summary>
        public string abbreviation;
        public Color primaryColor;
        public Color secondaryColor;
        public List<PlayerData> roster;

        public int wins = 0;
        public int losses = 0;

        public TeamData(string city, string name, Color primary, Color secondary, string abbrev = null)
        {
            cityName = city;
            teamName = name;
            primaryColor = primary;
            secondaryColor = secondary;
            abbreviation = abbrev;
            roster = new List<PlayerData>();
        }

        /// <summary>Retro-style HUD code (USDC / UNI / NYT).</summary>
        public string GetAbbreviation()
        {
            if (!string.IsNullOrEmpty(abbreviation))
                return abbreviation.ToUpperInvariant();

            if (string.IsNullOrEmpty(cityName))
                return "TM";

            var words = cityName.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                char a = char.ToUpperInvariant(words[0][0]);
                char b = char.ToUpperInvariant(words[1][0]);
                char c = !string.IsNullOrEmpty(teamName)
                    ? char.ToUpperInvariant(teamName[0])
                    : 'X';
                return $"{a}{b}{c}";
            }

            string raw = cityName.Replace(" ", "").ToUpperInvariant();
            if (raw.Length >= 3)
                return raw.Substring(0, Mathf.Min(4, raw.Length));

            if (!string.IsNullOrEmpty(teamName))
                raw += teamName.ToUpperInvariant();
            while (raw.Length < 3)
                raw += "X";
            return raw.Substring(0, Mathf.Min(4, raw.Length));
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
