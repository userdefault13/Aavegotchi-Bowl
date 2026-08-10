using System.Collections.Generic;
using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Data;
using RetroBowl.Managers;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Maps career <see cref="TeamData.roster"/> onto field GameObjects and pushes
    /// Retro Bowl–style scalars into controllers (speed, stamina, catch, tackle, throw).
    /// </summary>
    public static class StatBridge
    {
        public static float Norm(int rating01to99)
            => Mathf.Clamp01((Mathf.Clamp(rating01to99, 1, 99) - 1) / 98f);

        /// <summary>Map overall 1–99 → Retro Bowl–ish 0.5–5 stars.</summary>
        public static float StarsFromOverall(int overall)
            => Mathf.Clamp(Mathf.Round((overall / 99f) * 9f) * 0.5f, 0.5f, 5f);

        /// <summary>
        /// Kickoff: receiving roster → returner + receive wall; kicking roster → cover + kicker.
        /// </summary>
        public static void ApplyKickoffSides(bool receiverIsPlayer)
        {
            if (TeamManager.Instance == null) return;
            TeamManager.Instance.EnsureDefaultTeamsIfNeeded();

            var receive = receiverIsPlayer
                ? TeamManager.Instance.playerTeam
                : TeamManager.Instance.opponentTeam;
            var cover = receiverIsPlayer
                ? TeamManager.Instance.opponentTeam
                : TeamManager.Instance.playerTeam;

            if (receiverIsPlayer)
            {
                ApplyOffenseRoster(receive);
                ApplyDefenseRoster(cover);
            }
            else
            {
                // Player kicks: cover gunners are offense-named units from player team;
                // returner S + receive wall are defense-named from opponent.
                ApplyOffenseRoster(cover);
                ApplyDefenseRoster(receive);
            }
        }

        /// <summary>
        /// Apply player + opponent roster stats to the current offense/defense sides.
        /// Call after every <see cref="FormationRoster.PlaceOnly"/> / kickoff place.
        /// </summary>
        public static void ApplyMatchSides()
        {
            if (TeamManager.Instance == null) return;

            TeamManager.Instance.EnsureDefaultTeamsIfNeeded();

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            var offense = playerOffense
                ? TeamManager.Instance.playerTeam
                : TeamManager.Instance.opponentTeam;
            var defense = playerOffense
                ? TeamManager.Instance.opponentTeam
                : TeamManager.Instance.playerTeam;

            ApplyOffenseRoster(offense);
            ApplyDefenseRoster(defense);
        }

        static void ApplyOffenseRoster(TeamData team)
        {
            if (team?.roster == null) return;

            ApplyToUnit("Quarterback", FirstOf(team, PlayerPosition.Quarterback));
            ApplyToUnit("RB", FirstOf(team, PlayerPosition.RunningBack));

            var wrs = AllOf(team, PlayerPosition.WideReceiver);
            ApplyToUnit("WR_Top", wrs.Count > 0 ? wrs[0] : null);
            ApplyToUnit("WR_Bot", wrs.Count > 1 ? wrs[1] : (wrs.Count > 0 ? wrs[0] : null));
            ApplyToUnit("TE", FirstOf(team, PlayerPosition.TightEnd));

            var ols = AllOf(team, PlayerPosition.OffensiveLine);
            string[] olNames = FormationRoster.OffensiveLine;
            for (int i = 0; i < olNames.Length; i++)
                ApplyToUnit(olNames[i], i < ols.Count ? ols[i] : null);
        }

        static void ApplyDefenseRoster(TeamData team)
        {
            if (team?.roster == null) return;

            var dls = AllOf(team, PlayerPosition.DefensiveLine);
            string[] dlNames = FormationRoster.DefensiveLine;
            for (int i = 0; i < dlNames.Length; i++)
                ApplyToUnit(dlNames[i], i < dls.Count ? dls[i] : null);

            var lbs = AllOf(team, PlayerPosition.Linebacker);
            string[] lbNames = FormationRoster.Linebackers;
            for (int i = 0; i < lbNames.Length; i++)
                ApplyToUnit(lbNames[i], i < lbs.Count ? lbs[i] : null);

            var cbs = AllOf(team, PlayerPosition.Cornerback);
            ApplyToUnit("CB_Top", cbs.Count > 0 ? cbs[0] : null);
            ApplyToUnit("CB_Bot", cbs.Count > 1 ? cbs[1] : (cbs.Count > 0 ? cbs[0] : null));
            ApplyToUnit("S", FirstOf(team, PlayerPosition.Safety));
        }

        static void ApplyToUnit(string unitName, PlayerData data)
        {
            var go = GameObject.Find(unitName);
            if (go == null)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                {
                    if (t != null && t.name == unitName)
                    {
                        go = t.gameObject;
                        break;
                    }
                }
            }
            if (go == null) return;

            var stats = UnitRuntimeStats.GetOrAdd(go);
            if (data != null)
                stats.ApplyFrom(data);
            PushToControllers(go, stats);
        }

        /// <summary>World move speed from 1–99 rating (wide enough to feel on-field).</summary>
        public static float MoveFromSpeed01(float speed01)
            => Mathf.Lerp(2.0f, 4.5f, Mathf.Clamp01(speed01));

        static void PushToControllers(GameObject go, UnitRuntimeStats s)
        {
            if (go == null || s == null) return;

            float move = MoveFromSpeed01(s.Speed01);
            float sprint = Mathf.Lerp(move * 1.18f, move * 1.42f, s.Stamina01);

            var rc = go.GetComponent<ReceiverController>();
            if (rc != null)
            {
                rc.moveSpeed = move;
                rc.sprintSpeed = sprint;
                rc.catchChance = Mathf.Lerp(0.55f, 0.96f, s.Catching01);
                rc.catchRadius = Mathf.Lerp(0.9f, 1.25f, s.Catching01);
                rc.autoCatchRadius = Mathf.Lerp(0.35f, 0.55f, s.Catching01);
            }

            var pc = go.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.moveSpeed = move * 0.92f;
                pc.sprintSpeed = sprint * 0.95f;
            }

            var qbc = go.GetComponent<QuarterbackController>();
            if (qbc != null)
            {
                // Arm strength → throw power; accuracy → tighter aim click / less tip risk later.
                qbc.throwPower = Mathf.Lerp(14f, 26f, s.Throwing01);
                qbc.aimClickRadius = Mathf.Lerp(3.2f, 1.8f, s.Throwing01);
            }

            var ai = go.GetComponent<DefenderAI>();
            if (ai != null)
            {
                float roleMul = DefenseRoleSpeedMul(go.name, s.position);
                ai.moveSpeed = move * roleMul;
                ai.rushSpeed = Mathf.Lerp(1.9f, 3.6f, s.Speed01) * Mathf.Min(1f, roleMul + 0.05f);
                ai.speed = s.speed;
                ai.awareness = s.awareness;
                ai.tackling = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(45f, 95f, s.Skill01)), 1, 99);
            }

            var stam = go.GetComponent<StaminaSprint>();
            if (stam == null && (rc != null || pc != null || ai != null))
                stam = go.AddComponent<StaminaSprint>();
            if (stam != null)
            {
                stam.maxStamina = Mathf.Lerp(55f, 145f, s.Stamina01);
                stam.drainPerSecond = Mathf.Lerp(28f, 14f, s.Stamina01);
                stam.drainPerTap = Mathf.Lerp(14f, 7f, s.Stamina01);
                stam.refillPerSecond = Mathf.Lerp(18f, 32f, s.Stamina01);
                stam.ResetStamina();
            }
        }

        /// <summary>Carrier strength / catching for SoftTackle &amp; TecmoContact.</summary>
        public static float CarrierBreakBonus(Transform carrier)
        {
            var s = UnitRuntimeStats.Of(carrier);
            if (s == null) return 0f;
            return Mathf.Lerp(0f, 0.18f, s.Strength01);
        }

        public static float CarrierFumbleResist(Transform carrier)
        {
            var s = UnitRuntimeStats.Of(carrier);
            if (s == null) return 0f;
            return Mathf.Lerp(0f, 0.12f, s.Catching01);
        }

        public static float TacklerSkill01(Transform tackler)
        {
            var s = UnitRuntimeStats.Of(tackler);
            if (s != null)
            {
                // Skill positions wrap with strength; dedicated D uses key tackle skill.
                switch (s.position)
                {
                    case PlayerPosition.WideReceiver:
                    case PlayerPosition.RunningBack:
                    case PlayerPosition.TightEnd:
                    case PlayerPosition.Quarterback:
                        return s.Strength01;
                    default:
                        return Mathf.Max(s.Skill01, s.Strength01 * 0.85f);
                }
            }

            var ai = tackler != null ? tackler.GetComponent<DefenderAI>() : null;
            if (ai != null)
                return Mathf.Clamp01(DefenseGuessBlitz.TackleStatOr(ai.tackling) / 100f);
            return 0.65f;
        }

        public static float MoveSpeedOf(Transform unit, float fallback)
        {
            var s = UnitRuntimeStats.Of(unit);
            if (s == null) return fallback;
            float move = MoveFromSpeed01(s.Speed01);
            var ai = unit != null ? unit.GetComponent<DefenderAI>() : null;
            if (ai != null)
                move *= DefenseRoleSpeedMul(unit.name, s.position);
            return move;
        }

        public static float SprintSpeedOf(Transform unit, float fallback)
        {
            var s = UnitRuntimeStats.Of(unit);
            if (s == null) return fallback;
            float move = MoveSpeedOf(unit, MoveFromSpeed01(s.Speed01));
            return Mathf.Lerp(move * 1.18f, move * 1.42f, s.Stamina01);
        }

        /// <summary>Keep DL slower / CB faster at the same rating so positions feel distinct.</summary>
        static float DefenseRoleSpeedMul(string unitName, PlayerPosition pos)
        {
            if (!string.IsNullOrEmpty(unitName))
            {
                if (unitName.StartsWith("DL_")) return 0.88f;
                if (unitName.StartsWith("LB_")) return 0.96f;
                if (unitName.StartsWith("CB_")) return 1.08f;
                if (unitName.StartsWith("S") || unitName.Contains("Safety")) return 1.02f;
            }

            switch (pos)
            {
                case PlayerPosition.DefensiveLine: return 0.88f;
                case PlayerPosition.Linebacker: return 0.96f;
                case PlayerPosition.Cornerback: return 1.08f;
                case PlayerPosition.Safety: return 1.02f;
                default: return 1f;
            }
        }

        static PlayerData FirstOf(TeamData team, PlayerPosition pos)
        {
            if (team?.roster == null) return null;
            foreach (var p in team.roster)
            {
                if (p != null && p.position == pos)
                    return p;
            }
            return null;
        }

        static List<PlayerData> AllOf(TeamData team, PlayerPosition pos)
        {
            var list = new List<PlayerData>();
            if (team?.roster == null) return list;
            foreach (var p in team.roster)
            {
                if (p != null && p.position == pos)
                    list.Add(p);
            }
            return list;
        }
    }
}
