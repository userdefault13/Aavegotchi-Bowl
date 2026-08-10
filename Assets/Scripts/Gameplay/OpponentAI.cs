using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Legacy abstract AI play caller. Live opponent drives are handled by
    /// <see cref="DefensePlayDirector"/> so the human can play defense after TOD.
    /// Kept for difficulty settings / future special-teams helpers.
    /// </summary>
    public class OpponentAI : MonoBehaviour
    {
        [Header("AI Control")]
        public bool isOpponentPossession = false;

        [Header("AI Difficulty")]
        [Range(0f, 1f)]
        public float difficulty = 0.5f;
        public int aiSkillLevel = 50;

        void Update()
        {
            if (FieldManager.Instance != null)
                isOpponentPossession = !FieldManager.Instance.isPlayerPossession;

            // Live plays: DefensePlayDirector + SnapCadence.BeginDefenseAutoSnap.
            // Do not AdvanceBall here — that skipped player-controlled defense.
        }

        public void SetDifficulty(float newDifficulty)
        {
            difficulty = Mathf.Clamp01(newDifficulty);
            aiSkillLevel = (int)(difficulty * 100);
        }
    }

    public enum PlayType
    {
        Pass,
        Run,
        FieldGoal,
        Punt
    }
}
