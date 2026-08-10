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
                    speed -= 10;
                    break;
                case PlayerPosition.DefensiveLine:
                    strength += 20;
                    speed -= 10;
                    break;
                case PlayerPosition.Linebacker:
                    awareness += 10;
                    speed += 5;
                    break;
                case PlayerPosition.Cornerback:
                    speed += 20;
                    awareness += 10;
                    break;
                case PlayerPosition.Safety:
                    speed += 12;
                    awareness += 15;
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

        /// <summary>
        /// Retro Bowl stamina bar — career UI historically mapped this to <see cref="agility"/>.
        /// </summary>
        public int StaminaStat => agility;

        /// <summary>Position key skill (RB "skill" bar): throw / catch / tackle / block.</summary>
        public int KeySkill(PlayerPosition position)
        {
            switch (position)
            {
                case PlayerPosition.Quarterback:
                    return throwing;
                case PlayerPosition.WideReceiver:
                case PlayerPosition.RunningBack:
                case PlayerPosition.TightEnd:
                    return catching;
                case PlayerPosition.OffensiveLine:
                    return Mathf.Max(strength, awareness);
                case PlayerPosition.DefensiveLine:
                case PlayerPosition.Linebacker:
                case PlayerPosition.Cornerback:
                case PlayerPosition.Safety:
                    return Mathf.Max(strength, awareness);
                default:
                    return awareness;
            }
        }

        /// <summary>Retro Bowl–ish half-star rating from overall 1–99.</summary>
        public float StarRating => Mathf.Clamp(
            Mathf.Round((GetOverallRating() / 99f) * 9f) * 0.5f, 0.5f, 5f);
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
