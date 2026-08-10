using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// NES Tecmo dive helpers: ghost through blockers, detect dive state.
    /// Dive only resolves vs the ball carrier (defense) or defenders (offense) —
    /// not ContactBattle mash with OL.
    /// </summary>
    public static class TecmoDive
    {
        public static bool IsOffenseDiveBusy(Transform t)
        {
            if (t == null) return false;
            var rc = t.GetComponent<ReceiverController>();
            if (rc != null && rc.IsDiveBusy) return true;
            var qb = t.GetComponent<QuarterbackController>();
            return qb != null && qb.IsDiveBusy;
        }

        public static bool IsDefenseDiving(Transform t)
        {
            var pdc = PlayerDefenseController.Instance;
            return pdc != null
                   && pdc.IsDivingUnit(t);
        }

        /// <summary>True while a unit should phase through blockers.</summary>
        public static bool IsGhostDiving(Transform t)
            => IsOffenseDiveBusy(t) || IsDefenseDiving(t);

        public static void SetColliderGhost(Transform unit, bool ghost)
        {
            if (!GameRules.EnableTecmoDiveGhost || unit == null) return;
            foreach (var col in unit.GetComponents<Collider>())
            {
                if (col != null)
                    col.enabled = !ghost;
            }
        }

        /// <summary>Mash / hold Tecmo A to stretch a dive (NES slide feel).</summary>
        public static bool AExtendPressedOrHeld()
        {
            if (TecmoInput.ADown()) return true;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return true;
            return TecmoInput.AHeld();
        }
    }
}
