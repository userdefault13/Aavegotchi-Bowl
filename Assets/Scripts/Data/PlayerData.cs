using UnityEngine;

namespace RetroBowl.Data
{
    [System.Serializable]
    public class PlayerData
    {
        public string playerName;
        public int jerseyNumber;
        public PlayerPosition position;
        public PlayerStats stats;

        public PlayerData(string name, int number, PlayerPosition pos)
        {
            playerName = name;
            jerseyNumber = number;
            position = pos;
            stats = new PlayerStats(pos);
        }
    }

    [System.Serializable]
    public class PlayerStats
    {
        public int speed = 50;
        public int strength = 50;
        public int agility = 50;
        public int throwing = 50;
        public int catching = 50;
        public int awareness = 50;

        public PlayerStats(PlayerPosition position)
        {
            speed = Random.Range(40, 90);
            strength = Random.Range(40, 90);
            agility = Random.Range(40, 90);
            throwing = Random.Range(40, 90);
            catching = Random.Range(40, 90);
            awareness = Random.Range(40, 90);

            switch (position)
            {
                case PlayerPosition.Quarterback:
                    throwing += 20;
                    awareness += 15;
                    break;
                case PlayerPosition.RunningBack:
                    speed += 15;
                    agility += 15;
                    break;
                case PlayerPosition.WideReceiver:
                    speed += 20;
                    catching += 15;
                    break;
                case PlayerPosition.TightEnd:
                    strength += 15;
                    catching += 10;
                    break;
                case PlayerPosition.OffensiveLine:
                    strength += 20;
                    awareness += 10;
                    break;
            }

            speed = Mathf.Clamp(speed, 1, 99);
            strength = Mathf.Clamp(strength, 1, 99);
            agility = Mathf.Clamp(agility, 1, 99);
            throwing = Mathf.Clamp(throwing, 1, 99);
            catching = Mathf.Clamp(catching, 1, 99);
            awareness = Mathf.Clamp(awareness, 1, 99);
        }

        public int GetOverallRating()
        {
            return (speed + strength + agility + throwing + catching + awareness) / 6;
        }
    }

    public enum PlayerPosition
    {
        Quarterback,
        RunningBack,
        WideReceiver,
        TightEnd,
        OffensiveLine,
        DefensiveLine,
        Linebacker,
        Cornerback,
        Safety
    }
}
