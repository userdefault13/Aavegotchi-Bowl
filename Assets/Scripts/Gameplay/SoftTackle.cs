using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Managers;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Retro Bowl–style wrap resolve when mash ContactBattle / TackleBattle QTEs are off:
    /// probabilistic tackle, occasional whiff, optional one-tap stiff-arm.
    /// Also hosts Tecmo NES “popcorn” (HP gap ≥50 → auto flatten).
    /// </summary>
    public static class SoftTackle
    {
        public enum PopcornResult
        {
            None,
            /// <summary>Carrier bowls over the tackler and keeps the ball.</summary>
            CarrierPopcornsTackler,
            /// <summary>Tackler flattens the carrier (instant tackle).</summary>
            TacklerPopcornsCarrier
        }

        /// <summary>
        /// Hitting Power proxy — roster Strength 1–99 (Tecmo HP on a similar scale).
        /// </summary>
        public static int HittingPowerOf(Transform unit)
        {
            if (unit == null) return 50;
            var s = UnitRuntimeStats.Of(unit);
            if (s != null)
                return Mathf.Clamp(s.strength, 1, 99);

            var ai = unit.GetComponent<DefenderAI>();
            if (ai != null)
                return Mathf.Clamp(ai.tackling, 1, 99);

            return 50;
        }

        /// <summary>
        /// NES Tecmo popcorn: |HP_carrier − HP_tackler| ≥ <see cref="GameRules.TecmoPopcornHpGap"/>
        /// → stronger player auto-wins (no mash / RNG wrap).
        /// </summary>
        public static PopcornResult EvaluatePopcorn(Transform carrier, Transform tackler)
        {
            if (carrier == null || tackler == null) return PopcornResult.None;
            return EvaluatePopcorn(carrier, HittingPowerOf(tackler));
        }

        /// <summary>
        /// Popcorn vs a stacked tackler HP pile (multi-defender wraps).
        /// </summary>
        public static PopcornResult EvaluatePopcorn(Transform carrier, int stackedTacklerHp)
        {
            if (!GameRules.EnableTecmoPopcorn) return PopcornResult.None;
            if (carrier == null) return PopcornResult.None;

            int gap = HittingPowerOf(carrier) - Mathf.Max(1, stackedTacklerHp);
            if (gap >= GameRules.TecmoPopcornHpGap)
                return PopcornResult.CarrierPopcornsTackler;
            if (gap <= -GameRules.TecmoPopcornHpGap)
                return PopcornResult.TacklerPopcornsCarrier;
            return PopcornResult.None;
        }

        /// <summary>Carrier popcorns defender — stun + banner. Returns false (not tackled).</summary>
        public static void ApplyCarrierPopcorn(Transform carrier, Transform tackler)
        {
            if (tackler != null)
            {
                Vector3 away = tackler.position - (carrier != null ? carrier.position : tackler.position);
                away.z = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.left;
                PlayerStun.GetOrAdd(tackler.gameObject)?.Stun(0.75f, away.normalized * 1.15f);
            }

            PlayBanner.Show("POPCORN!", 0.7f, BannerTone.Positive);
            MatchPresentation.Shake(0.18f, 0.14f);
        }

        /// <summary>Defender popcorns carrier — presentation only; caller finishes the tackle.</summary>
        public static void ApplyTacklerPopcorn(Transform carrier, Transform tackler)
        {
            PlayBanner.Show("POPCORN!", 0.7f, BannerTone.Turnover);
            MatchPresentation.Tackle();
            MatchPresentation.Shake(0.2f, 0.16f);
        }

        /// <summary>
        /// After a successful wrap, chance the ball pops loose (not every tackle).
        /// Dive / hard hits strip often; pocket sacks rarely; low morale bumps risk.
        /// </summary>
        public static bool RollFumble(
            Transform carrier,
            Transform tackler,
            bool hardHit,
            bool isPocketSack)
        {
            if (carrier == null) return false;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return false;
            if (FieldManager.Instance != null && (FieldManager.Instance.JustScoredTouchdown
                || FieldManager.Instance.JustScoredSafety))
                return false;

            float chance = hardHit ? 0.52f : 0.10f;
            if (isPocketSack && !hardHit)
                chance = 0.055f;

            float strip = TackleSkillOf(tackler);
            chance += Mathf.Lerp(0f, hardHit ? 0.22f : 0.07f, strip);
            chance -= StatBridge.CarrierFumbleResist(carrier);

            // Career tutorial: low condition / morale → more fumbles.
            if (SeasonManager.Instance != null)
            {
                float morale = SeasonManager.Instance.teamMorale;
                if (morale < 35f) chance += 0.14f;
                else if (morale < 50f) chance += 0.07f;
            }

            return Random.value < Mathf.Clamp01(chance);
        }

        /// <summary>Knock direction from tackler into carrier (for loose-ball skitter).</summary>
        public static Vector3 FumbleKnockDir(Transform carrier, Transform tackler)
        {
            Vector3 knock = Vector3.left;
            if (carrier != null && tackler != null)
            {
                knock = carrier.position - tackler.position;
                knock.z = 0f;
                if (knock.sqrMagnitude < 0.01f)
                    knock = Vector3.left;
            }
            return knock.normalized;
        }

        /// <summary>K / E stiff-arm when free; in contact, J also via BattleDown.</summary>
        public static bool StiffArmPressedThisFrame()
        {
            return TecmoInput.BattleDown()
                   || Input.GetKeyDown(KeyCode.F);
        }

        public static bool StiffArmHeld()
        {
            return Input.GetKey(KeyCode.K)
                   || Input.GetKey(KeyCode.E)
                   || Input.GetKey(KeyCode.F);
        }

        /// <summary>
        /// Resolve a wrap. Returns true if the ball-carrier was tackled,
        /// false if they broke free / defender whiffed.
        /// </summary>
        public static bool ResolveWrap(
            Transform carrier,
            Transform tackler,
            bool isPocketSack,
            bool playerCarrier,
            bool hardHit = false)
        {
            // Tecmo popcorn — auto flatten when Strength gap ≥ 50 (skip RNG / mash).
            // Applies on wraps and dive connects (NES).
            {
                var popcorn = EvaluatePopcorn(carrier, tackler);
                if (popcorn == PopcornResult.CarrierPopcornsTackler)
                {
                    ApplyCarrierPopcorn(carrier, tackler);
                    return false;
                }

                if (popcorn == PopcornResult.TacklerPopcornsCarrier)
                {
                    ApplyTacklerPopcorn(carrier, tackler);
                    return true;
                }
            }

            float tackleSkill = TackleSkillOf(tackler);

            // Base wrap chance — high enough that pressure ends plays, with Retro whiffs.
            float wrapChance = Mathf.Lerp(0.55f, 0.92f, tackleSkill);
            if (isPocketSack)
                wrapChance = Mathf.Lerp(0.72f, 0.97f, tackleSkill); // pocket is less forgiving

            // Dive / hard hit — harder to break.
            if (hardHit)
                wrapChance = Mathf.Clamp01(wrapChance + 0.12f);

            // Kickoff gunners — slightly stickier wraps so cover tackles read clearly.
            if (IsKickoffGunner(tackler))
                wrapChance = Mathf.Clamp01(wrapChance + 0.08f);

            float breakChance = 1f - wrapChance;
            breakChance += StatBridge.CarrierBreakBonus(carrier);

            // Moving with speed helps break (can't stiff-arm while stationary as easily).
            if (carrier != null)
            {
                var rb = carrier.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    float spd = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y).magnitude;
                    if (spd > 2.5f)
                        breakChance += 0.08f;
                }

                // Arcade move velocity (kinematic carriers).
                var rc = carrier.GetComponent<ReceiverController>();
                if (rc != null && rc.PlanarSpeed > 2.5f)
                    breakChance += 0.08f;
            }

            bool stiff = playerCarrier && (StiffArmPressedThisFrame() || StiffArmHeld());
            if (stiff)
            {
                float strengthBoost = 0f;
                var us = UnitRuntimeStats.Of(carrier);
                if (us != null)
                    strengthBoost = Mathf.Lerp(0.05f, 0.2f, us.Strength01);
                breakChance += (isPocketSack ? 0.18f : 0.32f) + strengthBoost;
                PlayBanner.Show("STIFF ARM!", 0.55f);
            }

            breakChance = Mathf.Clamp01(breakChance);

            if (Random.value < breakChance)
            {
                // Whiff / broken tackle — shove defender off.
                if (tackler != null)
                {
                    Vector3 away = tackler.position - (carrier != null ? carrier.position : tackler.position);
                    away.z = 0f;
                    if (away.sqrMagnitude < 0.01f) away = Vector3.left;
                    PlayerStun.GetOrAdd(tackler.gameObject)?.Stun(0.45f, away.normalized * 0.6f);
                }

                if (!stiff)
                    PlayBanner.Show(playerCarrier ? "BROKE TACKLE!" : "MISSED TACKLE!", 0.55f);
                else
                    MatchPresentation.Shake(0.14f, 0.12f);

                return false;
            }

            MatchPresentation.Tackle();
            return true;
        }

        /// <summary>0–1 tackle skill for soft wraps (DefenderAI, or kickoff gunner defaults).</summary>
        public static float TackleSkillOf(Transform tackler)
        {
            return StatBridge.TacklerSkill01(tackler);
        }

        static bool IsKickoffGunner(Transform t)
        {
            if (t == null) return false;
            var rc = t.GetComponent<ReceiverController>();
            if (rc != null && rc.IsKickoffGunnerControlled) return true;
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsKickoffCoverage
                && PlayerDefenseController.Instance.ControlledUnit == t)
                return true;

            // AI cover units while the player-kicked return is live.
            if (GameManager.Instance != null
                && GameManager.Instance.isKickoffReturn
                && FieldManager.Instance != null
                && !FieldManager.Instance.KickoffReceiverIsPlayer)
            {
                try
                {
                    return t.CompareTag("Receiver")
                           || t.CompareTag("Lineman")
                           || t.CompareTag("Player");
                }
                catch (UnityException) { }
            }

            return false;
        }
    }
}
