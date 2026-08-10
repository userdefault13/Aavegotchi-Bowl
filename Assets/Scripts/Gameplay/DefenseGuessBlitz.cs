using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Tecmo-style play guess: if defense picks the same play id as offense,
    /// all defenders get max defensive stats and force-blitz for that one play.
    /// </summary>
    public static class DefenseGuessBlitz
    {
        public static bool Active { get; private set; }

        /// <summary>Max SpeedFactor clamp used by DefenderAI.</summary>
        public const float MaxSpeedFactor = 1.35f;

        public static void EvaluateOnSnap()
        {
            // Always reset first so a prior down cannot leak max-stat mode.
            Active = false;

            if (!Playbook.DefenseGuessMatchesOffense)
            {
                if (Playbook.DefenseGuess != null && Playbook.Selected != null)
                {
                    Debug.Log(
                        $"DefenseGuessBlitz: miss (guess '{Playbook.DefenseGuess.Id}' vs '{Playbook.Selected.Id}')");
                }
                return;
            }

            Active = true;
            PlayBanner.Show("GUESS!  BLITZ!", 1.6f);
            Debug.Log(
                $"DefenseGuessBlitz: MATCH — {Playbook.Selected.DisplayName} ({Playbook.Selected.Id}) → max D boost + blitz");
        }

        public static void Clear()
        {
            Active = false;
        }

        public static float SpeedFactorOr(float normal)
            => Active ? MaxSpeedFactor : normal;

        public static int TackleStatOr(int tackling)
            => Active ? 100 : tackling;

        public static float TackleRadiusMul => Active ? 1.3f : 1f;
    }
}
