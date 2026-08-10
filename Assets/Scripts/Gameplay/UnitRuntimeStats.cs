using UnityEngine;
using RetroBowl.Data;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Retro Bowl–style runtime stats on a field unit (applied from career
    /// <see cref="PlayerData"/>). Controllers read these instead of only inspector defaults.
    /// Scale: source roster is 1–99; normalized 0–1 drives gameplay scalars.
    /// </summary>
    public class UnitRuntimeStats : MonoBehaviour
    {
        public string playerName;
        public int jerseyNumber;
        public PlayerPosition position;

        [Range(1, 99)] public int speed = 70;
        [Range(1, 99)] public int strength = 70;
        /// <summary>Retro Bowl stamina — stored as PlayerStats.agility in career data.</summary>
        [Range(1, 99)] public int stamina = 70;
        [Range(1, 99)] public int throwing = 70;
        [Range(1, 99)] public int catching = 70;
        [Range(1, 99)] public int awareness = 70;
        /// <summary>Position key skill (tackling for D, blocking for OL, etc.).</summary>
        [Range(1, 99)] public int skill = 70;

        public float Speed01 => StatBridge.Norm(speed);
        public float Strength01 => StatBridge.Norm(strength);
        public float Stamina01 => StatBridge.Norm(stamina);
        public float Throwing01 => StatBridge.Norm(throwing);
        public float Catching01 => StatBridge.Norm(catching);
        public float Awareness01 => StatBridge.Norm(awareness);
        public float Skill01 => StatBridge.Norm(skill);

        /// <summary>Retro Bowl–ish 0.5–5 star display from overall.</summary>
        public float StarRating => StatBridge.StarsFromOverall(
            (speed + strength + stamina + skill) / 4);

        public void ApplyFrom(PlayerData data)
        {
            if (data == null) return;
            playerName = data.playerName;
            jerseyNumber = data.jerseyNumber;
            position = data.position;
            var s = data.stats ?? new PlayerStats(data.position);
            speed = Mathf.Clamp(s.speed, 1, 99);
            strength = Mathf.Clamp(s.strength, 1, 99);
            stamina = Mathf.Clamp(s.StaminaStat, 1, 99);
            throwing = Mathf.Clamp(s.throwing, 1, 99);
            catching = Mathf.Clamp(s.catching, 1, 99);
            awareness = Mathf.Clamp(s.awareness, 1, 99);
            skill = Mathf.Clamp(s.KeySkill(data.position), 1, 99);
        }

        public static UnitRuntimeStats GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            var u = go.GetComponent<UnitRuntimeStats>();
            if (u == null) u = go.AddComponent<UnitRuntimeStats>();
            return u;
        }

        public static UnitRuntimeStats Of(Component c)
            => c != null ? c.GetComponent<UnitRuntimeStats>() : null;

        public static UnitRuntimeStats Of(Transform t)
            => t != null ? t.GetComponent<UnitRuntimeStats>() : null;
    }
}
