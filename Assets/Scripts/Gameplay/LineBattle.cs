using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Deprecated OL-only bull-rush shim. All engagements use <see cref="ContactBattle"/>.
    /// Kept so old scene references / EnsureExists calls remain harmless.
    /// </summary>
    public class LineBattle : MonoBehaviour
    {
        public static LineBattle Instance { get; private set; }

        public bool IsActive => ContactBattle.Instance != null && ContactBattle.Instance.IsActive;
        public Transform Offense => ContactBattle.Instance != null ? ContactBattle.Instance.Offense : null;
        public Transform Defense => ContactBattle.Instance != null ? ContactBattle.Instance.Defense : null;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureExists() => ContactBattle.EnsureExists();

        public static bool Begin(Transform offense, Transform defense)
            => ContactBattle.Begin(offense, defense);

        public static bool IsCombatant(Transform t)
            => ContactBattle.IsCombatant(t);

        public void ForceCancel()
        {
            if (ContactBattle.Instance != null)
                ContactBattle.Instance.ForceCancel();
        }
    }
}
