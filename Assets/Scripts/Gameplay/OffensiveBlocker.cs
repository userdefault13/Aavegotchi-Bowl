using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Pass protection for OL + TE. Slides to the nearest loose rusher,
    /// starts a ContactBattle on contact, then adjusts to the next free threat.
    /// True O-line (LT/LG/C/RG/RT) regroup to a QB-relative pocket spot when idle.
    /// Also drives kickoff receive-wall peels (soft hold coverage for the returner).
    /// </summary>
    public class OffensiveBlocker : MonoBehaviour
    {
        [Header("Identity")]
        public bool isTightEnd;
        /// <summary>
        /// Temporary WR/QB wall units — only AI during kickoff wall phase; stripped after return.
        /// </summary>
        public bool kickoffWallOnly;
        public float teBlockDuration = 1.05f;

        [Header("Pass Pro")]
        public float slideSpeed = 3.6f;
        public float engageRadius = 1.7f;
        public float holdRadius = 0.9f;
        public float battleStartRadius = 0.95f;
        [Range(0.05f, 0.5f)] public float holdSpeedMul = 0.16f;
        public float pushForce = 2.8f;
        public float maxSlideFromHome = 3.2f;
        public float retargetInterval = 0.2f;

        [Header("Kickoff wall")]
        public float kickoffSlideSpeed = 5.2f;
        public float kickoffHuntRadius = 9.5f;

        [Header("Regroup (OL only)")]
        public float regroupSpeed = 4.4f;
        public float regroupHuntRadiusMul = 5.5f;

        public bool IsPassProActive { get; private set; }
        /// <summary>True while protecting a run lane (BallCarrier does not end the block).</summary>
        public bool IsRunBlock { get; private set; }
        public Transform EngagedRusher { get; private set; }
        public bool BlocksBeforeRoute => isTightEnd;
        public bool IsInContactBattle => ContactBattle.IsCombatant(transform);
        public bool IsTrueOLine => !isTightEnd;
        public bool IsActivelyBlocking =>
            IsInContactBattle
            || (EngagedRusher != null
                && EngagedRusher.gameObject.activeInHierarchy
                && !PlayerStun.IsUnitStunned(EngagedRusher));

        Rigidbody rb;
        Vector3 homePos;
        Vector3 pocketOffsetFromQb;
        float passProStartedAt = -1f;
        float nextRetargetAt;
        bool releasedToRoute;
        PlayerStun stun;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            ArcadeMove.ConfigureKinematicBody(rb);
            stun = PlayerStun.GetOrAdd(gameObject);

            if (gameObject.name == "TE" || gameObject.name.StartsWith("TE"))
                isTightEnd = true;

            homePos = transform.position;
        }

        public void BeginPassPro()
        {
            IsRunBlock = false;
            BeginBlockInternal();
        }

        /// <summary>Run play — stay engaged after handoff; TE does not release to route.</summary>
        public void BeginRunBlock()
        {
            IsRunBlock = true;
            BeginBlockInternal();
        }

        void BeginBlockInternal()
        {
            IsPassProActive = true;
            releasedToRoute = false;
            EngagedRusher = null;
            passProStartedAt = Time.time;
            nextRetargetAt = 0f;
            homePos = transform.position;
            float back = FieldManager.Instance != null ? -FieldManager.Instance.DriveDirX : -1f;
            pocketOffsetFromQb = homePos - ResolveQbPosition(homePos + Vector3.right * (3f * back));
            if (stun != null) stun.Clear();
        }

        public void EndPassPro()
        {
            IsPassProActive = false;
            IsRunBlock = false;
            EngagedRusher = null;
            ArcadeMove.Apply(rb, Vector3.zero);
        }

        public void NotifyBattleWon(Transform beatenRusher)
        {
            if (EngagedRusher == beatenRusher)
                EngagedRusher = null;
            nextRetargetAt = 0f;
        }

        public void NotifyBattleLost()
        {
            EngagedRusher = null;
            nextRetargetAt = Time.time + 0.15f;
        }

        /// <summary>
        /// Live kickoff return, or loose kickoff ball before the scoop — receive wall peels.
        /// </summary>
        public static bool IsKickoffWallPhase()
        {
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.currentState != GameState.Playing) return false;
            if (GameManager.Instance.waitingForNextPlay) return false;
            if (GameManager.Instance.isKickoffReturn) return true;
            // Coverage may still be gated; wall can still step to lanes before pickup.
            return GameManager.Instance.isKicking
                   && FootballBehavior.TryGetLooseKickoff(out _);
        }

        void Update()
        {
            bool kickoffWall = IsKickoffWallPhase();

            // Kickoff-only WR/QB blockers idle outside special teams.
            if (kickoffWallOnly && !kickoffWall)
            {
                if (IsPassProActive) EndPassPro();
                EngagedRusher = null;
                releasedToRoute = false;
                StopMotion();
                return;
            }

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.waitingForNextPlay
                || (!kickoffWall
                    && (GameManager.Instance.isPreSnap || GameManager.Instance.isKicking)))
            {
                if (GameManager.Instance != null
                    && (GameManager.Instance.isPreSnap || GameManager.Instance.isKicking)
                    && !kickoffWall)
                {
                    IsPassProActive = false;
                    EngagedRusher = null;
                    releasedToRoute = false;
                }
                StopMotion();
                return;
            }

            // Only freeze this unit while it is in a mash — other O-line keep battling.
            if (ContactBattle.IsCombatant(transform) || TecmoContact.IsInvolved(transform))
            {
                StopMotion();
                if (EngagedRusher != null)
                    StopOther(EngagedRusher);
                // Offensive help: peel a defender off a live ball-carrier mash.
                TecmoContact.TryTeammatePopAssist(transform);
                return;
            }

            if (stun != null && stun.IsStunned)
            {
                // Knockback handled by PlayerStun.
                EngagedRusher = null;
                return;
            }

            if (IsInContactBattle)
            {
                StopMotion();
                return;
            }

            // Engaged at battle range — lock feet until ContactBattle resolves.
            if (EngagedRusher != null
                && EngagedRusher.gameObject.activeInHierarchy
                && !PlayerStun.IsUnitStunned(EngagedRusher))
            {
                float engageDist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
                if (engageDist <= battleStartRadius)
                {
                    StopMotion();
                    StopOther(EngagedRusher);
                    TryStartBattle();
                    TecmoContact.TryTeammatePopAssist(transform);
                    return;
                }
            }

            // Kickoff special teams: receive wall peels; cover wall chases the returner.
            if (kickoffWall)
            {
                // Player-steered gunner — PlayerDefenseController owns motion.
                var gunner = GetComponent<ReceiverController>();
                if (gunner != null && gunner.IsKickoffGunnerControlled)
                {
                    StopMotion();
                    return;
                }

                if (IsKickoffReceiveWallUnit())
                    RunKickoffWallBlock();
                else
                    PursueKickoffReturnerAsCoverage();
                return;
            }

            // Live INT / fumble return — OL abandons pass pro and chases the returner.
            if (GameManager.Instance.isInterceptionReturn)
            {
                IsPassProActive = false;
                EngagedRusher = null;
                PursueInterceptionReturner();
                return;
            }

            // Loose fumble — OL joins the pile / race to the ball.
            if (FootballBehavior.TryGetLooseFumble(out var loose) && loose != null)
            {
                IsPassProActive = false;
                EngagedRusher = null;
                PursueLooseFumble(loose);
                return;
            }

            if (!IsPassProActive && !releasedToRoute && passProStartedAt < 0f)
                BeginPassPro();

            if (!IsPassProActive)
            {
                StopMotion();
                return;
            }

            if (BallIsOut())
            {
                EndPassPro();
                return;
            }

            if (!IsRunBlock && isTightEnd && Time.time - passProStartedAt >= teBlockDuration)
            {
                ReleaseTightEndToRoute();
                return;
            }

            // True OL: if idle (no assignment / battle) while QB still has the ball,
            // fall back to pocket and hunt the next loose rusher.
            // Run block: stay downfield — no pocket regroup.
            bool regrouping = !IsRunBlock && ShouldRegroupToPocket();

            UpdateAssignment(regrouping);
            DriveBlock(regrouping);
            TryStartBattle();
        }

        /// <summary>
        /// Receive wall = offense when player receives, defense when player kicks.
        /// Cover (gunners) is the opposite roster — they should chase, not peel.
        /// </summary>
        bool IsKickoffReceiveWallUnit()
        {
            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            bool isDefense = GetComponent<DefenderAI>() != null;
            return playerReceives ? !isDefense : isDefense;
        }

        /// <summary>Covering gunners after a player kickoff — chase the AI returner.</summary>
        void PursueKickoffReturnerAsCoverage()
        {
            if (IsPassProActive)
                EndPassPro();
            EngagedRusher = null;

            Vector3 aim = ResolveReturnerPosition();
            if (aim.sqrMagnitude < 0.01f)
            {
                StopMotion();
                return;
            }

            float cut = FieldManager.Instance != null ? FieldManager.Instance.ReturnDirX : -1f;
            aim += Vector3.right * (0.55f * cut);
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            float spd = isTightEnd ? 5.8f : 5.1f;
            Vector3 vel = delta.sqrMagnitude > 0.01f ? delta.normalized * spd : Vector3.zero;
            ArcadeMove.Apply(rb, vel);
        }

        void RunKickoffWallBlock()
        {
            if (!IsPassProActive)
                BeginPassPro();

            bool needRetarget = Time.time >= nextRetargetAt;
            bool lost =
                EngagedRusher == null
                || !EngagedRusher.gameObject.activeInHierarchy
                || PlayerStun.IsUnitStunned(EngagedRusher)
                || IsRusherClaimedByOther(EngagedRusher);

            if (needRetarget || lost)
            {
                nextRetargetAt = Time.time + retargetInterval;
                EngagedRusher = FindBestKickoffThreat();
            }
            else if (EngagedRusher != null)
            {
                float dist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
                if (dist > engageRadius * 2.2f)
                {
                    nextRetargetAt = Time.time + retargetInterval;
                    EngagedRusher = FindBestKickoffThreat();
                }
            }

            DriveKickoffBlock();
            TryStartBattle();
        }

        Transform FindBestKickoffThreat()
        {
            Vector3 returnerPos = ResolveReturnerPosition();
            Vector2 my = new Vector2(transform.position.x, transform.position.y);
            Vector2 ret = new Vector2(returnerPos.x, returnerPos.y);

            Transform best = null;
            float bestScore = float.MaxValue;

            void Consider(Transform t)
            {
                if (t == null || t == transform) return;
                if (!t.gameObject.activeInHierarchy) return;
                if (PlayerStun.IsUnitStunned(t)) return;
                if (ContactBattle.IsCombatant(t)) return;
                // Never block the returner / ball-carrier.
                try
                {
                    if (t.CompareTag("BallCarrier")) return;
                }
                catch (UnityException) { }
                var rc = t.GetComponent<ReceiverController>();
                if (rc != null && rc.hasBall) return;

                Vector2 tp = new Vector2(t.position.x, t.position.y);
                float dist = Vector2.Distance(my, tp);
                if (dist > kickoffHuntRadius) return;

                // Prefer gunners between this wall unit and the returner (downfield of wall toward −X).
                float toRet = Vector2.Distance(tp, ret);
                float lane = Mathf.Abs(t.position.y - transform.position.y);
                float betweenBonus = 0f;
                // Coverage coming from +X (kick direction) toward returner: sit in the path.
                if (t.position.x > returnerPos.x - 1.5f && t.position.x < transform.position.x + 2.5f)
                    betweenBonus = -1.8f;

                float threat = dist * 0.35f + toRet * 0.4f + lane * 0.35f + betweenBonus;
                if (IsRusherClaimedByOther(t)) threat += 5f;

                if (threat < bestScore)
                {
                    bestScore = threat;
                    best = t;
                }
            }

            // Player receive: coverage is Defender-tagged.
            try
            {
                foreach (var d in GameObject.FindGameObjectsWithTag("Defender"))
                    Consider(d != null ? d.transform : null);
            }
            catch (UnityException) { }

            // Opponent receive: coverage is offense-colored (Receivers / Linemen / QB).
            if (best == null)
            {
                try
                {
                    foreach (var r in GameObject.FindGameObjectsWithTag("Receiver"))
                        Consider(r != null ? r.transform : null);
                }
                catch (UnityException) { }

                try
                {
                    foreach (var l in GameObject.FindGameObjectsWithTag("Lineman"))
                        Consider(l != null ? l.transform : null);
                }
                catch (UnityException) { }

                var qb = GameObject.Find("Quarterback");
                if (qb != null) Consider(qb.transform);
            }

            return best;
        }

        void DriveKickoffBlock()
        {
            Vector3 returnerPos = ResolveReturnerPosition();
            Vector3 target;

            if (EngagedRusher != null)
            {
                Vector3 threat = EngagedRusher.position;
                // Soft engage point: between coverage and returner (peel into the lane).
                Vector3 toRet = returnerPos - threat;
                toRet.z = 0f;
                if (toRet.sqrMagnitude < 0.01f)
                {
                    float rd = FieldManager.Instance != null ? FieldManager.Instance.ReturnDirX : -1f;
                    toRet = Vector3.right * rd;
                }
                target = threat + toRet.normalized * 0.55f;
            }
            else
            {
                // Idle: shade a step toward the returner so lanes open when gunners arrive.
                target = Vector3.Lerp(homePos, returnerPos, 0.12f);
                target.x = Mathf.Lerp(homePos.x, returnerPos.x, 0.08f);
            }

            target.z = 0f;
            MoveTowards(target, kickoffSlideSpeed);
        }

        static Vector3 ResolveReturnerPosition()
        {
            try
            {
                var bc = GameObject.FindGameObjectWithTag("BallCarrier");
                if (bc != null && bc.activeInHierarchy)
                    return bc.transform.position;
            }
            catch (UnityException) { }

            // Pre-scoop: deep returner unit still named RB / S.
            foreach (var name in new[] { "RB", "S" })
            {
                var go = GameObject.Find(name);
                if (go != null && go.activeInHierarchy)
                    return go.transform.position;
            }

            if (FootballBehavior.TryGetLooseKickoff(out var ball) && ball != null)
                return ball.PlayPlanePosition;

            return Vector3.zero;
        }

        void LateUpdate()
        {
            if (!IsPassProActive || EngagedRusher == null) return;
            if (stun != null && stun.IsStunned) return;
            if (IsInContactBattle) return;
            if (ContactBattle.IsCombatant(transform) || ContactBattle.IsCombatant(EngagedRusher))
                return;
            if (TecmoContact.IsInvolved(transform) || TecmoContact.IsInvolved(EngagedRusher))
                return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.isPreSnap && !IsKickoffWallPhase()) return;
            if (PlayerStun.IsUnitStunned(EngagedRusher)) return;

            float back = FieldManager.Instance != null ? -FieldManager.Instance.DriveDirX : -1f;
            Vector3 protectPos = IsKickoffWallPhase()
                ? ResolveReturnerPosition()
                : ResolveQbPosition(homePos + Vector3.right * (3f * back));
            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
            // Inside battle radius — no shove; feet stay planted for ContactBattle.
            if (dist <= battleStartRadius) return;
            if (dist <= holdRadius)
                ApplyHoldPush(EngagedRusher.position, protectPos);
        }

        bool BallIsOut()
        {
            // Run block: keep sealing while the RB has the ball.
            // Pass pro: BallCarrier means the ball left the pocket — release.
            if (!IsRunBlock)
            {
                try
                {
                    if (GameObject.FindGameObjectWithTag("BallCarrier") != null)
                        return true;
                }
                catch (UnityException) { /* tag missing */ }
            }

            var qb = GameObject.Find("Quarterback");
            if (qb == null) return false;
            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc == null) return false;

            // Scramble = QB is the runner — release OL holds (pass look).
            if (!IsRunBlock && qbc.isScrambling) return true;

            // Pass left the pocket.
            if (qbc.hasThrown) return true;

            return false;
        }

        /// <summary>
        /// QB still has the ball in the pocket (not thrown, handed off, sacked,
        /// scrambled past LOS as a runner, or waiting for the next play).
        /// Used for OL pocket regroup — separate from BallIsOut / EndPassPro.
        /// </summary>
        public static bool QbStillHasBall()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.currentState != GameState.Playing) return false;
                if (GameManager.Instance.isPreSnap) return false;
                if (GameManager.Instance.waitingForNextPlay) return false;
            }

            var qb = GameObject.Find("Quarterback");
            if (qb == null) return false;

            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc != null)
            {
                // Scramble past LOS — QB is a runner; stop pocket regroup.
                if (qbc.isScrambling) return false;
                if (qbc.hasThrown) return false;
            }

            var pc = qb.GetComponent<PlayerController>();
            if (pc != null)
                return pc.hasBall;

            // Fallback if no PlayerController: treat as holding until thrown.
            return qbc == null || !qbc.hasThrown;
        }

        bool ShouldRegroupToPocket()
        {
            if (!IsTrueOLine) return false;
            if (!IsPassProActive) return false;
            if (!QbStillHasBall()) return false;
            if (IsInContactBattle) return false;
            if (IsActivelyBlocking) return false;
            return true;
        }

        void ReleaseTightEndToRoute()
        {
            EndPassPro();
            releasedToRoute = true;
            passProStartedAt = -1f;

            var rc = GetComponent<ReceiverController>();
            if (rc != null && !rc.hasBall && !rc.isRunningRoute)
                rc.StartRoute();
        }

        void UpdateAssignment(bool regrouping)
        {
            bool needRetarget = Time.time >= nextRetargetAt;
            bool lost =
                EngagedRusher == null
                || !EngagedRusher.gameObject.activeInHierarchy
                || PlayerStun.IsUnitStunned(EngagedRusher)
                || IsRusherClaimedByOther(EngagedRusher);

            if (!needRetarget && !lost)
            {
                float dist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
                // Stick with assignment while close; otherwise hunt a looser threat.
                if (dist <= engageRadius * 1.6f)
                    return;
            }

            nextRetargetAt = Time.time + retargetInterval;
            float huntMul = regrouping ? regroupHuntRadiusMul : 3.2f;
            EngagedRusher = FindBestLooseRusher(huntMul);
        }

        Transform FindBestLooseRusher(float huntRadiusMul = 3.2f)
        {
            GameObject[] defenders;
            try { defenders = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { return null; }

            Transform qb = null;
            var qbGo = GameObject.Find("Quarterback");
            if (qbGo != null) qb = qbGo.transform;

            Transform best = null;
            float bestScore = float.MaxValue;
            Vector2 my = new Vector2(transform.position.x, transform.position.y);
            Vector2 gapAnchor = my;
            if (qb != null)
            {
                // Prefer threats in this lineman's gap (pocket spot / lane).
                Vector3 pocket = PassProSpot(qb.position);
                gapAnchor = new Vector2(pocket.x, pocket.y);
            }

            float maxHunt = engageRadius * Mathf.Max(1f, huntRadiusMul);

            foreach (var d in defenders)
            {
                if (d == null || !d.activeInHierarchy) continue;
                if (PlayerStun.IsUnitStunned(d)) continue;
                if (ContactBattle.IsCombatant(d.transform)) continue;

                var ai = d.GetComponent<DefenderAI>();
                bool rusher = ai == null
                              || ai.role == DefenderRole.Rush
                              || ai.role == DefenderRole.Blitz
                              || ai.currentState == DefenderState.Rushing
                              || ai.currentState == DefenderState.Pursuing;
                if (!rusher && !isTightEnd) continue;

                // Prefer truly loose rushers (not already locked by another OL).
                bool claimed = IsRusherClaimedByOther(d.transform);
                Vector2 dp = new Vector2(d.transform.position.x, d.transform.position.y);
                float dist = Vector2.Distance(my, dp);
                float gapDist = Vector2.Distance(gapAnchor, dp);
                if (dist > maxHunt && gapDist > maxHunt) continue;

                float threat = dist;
                if (qb != null)
                {
                    float toQb = Vector2.Distance(dp, new Vector2(qb.position.x, qb.position.y));
                    // Weight QB threat highest; gap proximity next; own distance last.
                    threat = toQb * 0.55f + gapDist * 0.25f + dist * 0.2f;
                }

                float laneBias = Mathf.Abs(d.transform.position.y - transform.position.y) * 0.3f;
                threat += laneBias;

                // Strongly prefer unblocked rushers.
                if (claimed) threat += 4.5f;

                if (threat < bestScore)
                {
                    bestScore = threat;
                    best = d.transform;
                }
            }

            return best;
        }

        static Vector3 ResolveQbPosition(Vector3 fallback)
        {
            var qbGo = GameObject.Find("Quarterback");
            return qbGo != null ? qbGo.transform.position : fallback;
        }

        Vector3 PassProSpot(Vector3 qbPos)
        {
            Vector3 spot = qbPos + pocketOffsetFromQb;
            // Don't drift past original snap depth toward the LOS more than a step.
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            float towardLos = (spot.x - homePos.x) * dir;
            if (towardLos > 0.35f)
                spot.x = homePos.x + 0.35f * dir;
            spot.z = 0f;
            return spot;
        }

        bool IsRusherClaimedByOther(Transform rusher)
        {
            if (rusher == null) return false;
            var blockers = UnityEngine.Object.FindObjectsByType<OffensiveBlocker>(FindObjectsInactive.Exclude);
            foreach (var b in blockers)
            {
                if (b == null || b == this) continue;
                if (!b.IsPassProActive) continue;
                if (PlayerStun.IsUnitStunned(b)) continue;
                if (b.EngagedRusher == rusher)
                    return true;
            }
            return false;
        }

        void DriveBlock(bool regrouping)
        {
            float back = FieldManager.Instance != null ? -FieldManager.Instance.DriveDirX : -1f;
            Vector3 qbPos = ResolveQbPosition(homePos + Vector3.right * (3f * back));
            Vector3 pocket = PassProSpot(qbPos);
            Vector3 target = pocket;

            if (EngagedRusher != null)
            {
                Vector3 rusher = EngagedRusher.position;
                Vector3 toQb = qbPos - rusher;
                toQb.z = 0f;
                if (toQb.sqrMagnitude < 0.01f) toQb = Vector3.right * back;
                target = rusher + toQb.normalized * 0.5f;

                // Anchor slide to the live pocket spot so the wall tracks the QB.
                Vector3 fromPocket = target - pocket;
                fromPocket.z = 0f;
                if (fromPocket.magnitude > maxSlideFromHome)
                    target = pocket + fromPocket.normalized * maxSlideFromHome;
            }
            else if (regrouping)
            {
                // Idle OL: return to pocket and await / pick up the next rusher.
                target = pocket;
            }
            else
            {
                target = Vector3.Lerp(homePos, qbPos, 0.12f);
            }

            target.z = 0f;
            float speed = (regrouping && EngagedRusher == null) ? regroupSpeed : slideSpeed;
            MoveTowards(target, speed);
        }

        void TryStartBattle()
        {
            if (!GameRules.EnableLineEngageBattles && !GameRules.EnableContactBattle)
                return;
            if (EngagedRusher == null) return;
            if (PlayerStun.IsUnitStunned(EngagedRusher)) return;

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
            if (dist > battleStartRadius) return;

            ContactBattle.Begin(transform, EngagedRusher);
        }

        void ApplyHoldPush(Vector3 rusher, Vector3 qbPos)
        {
            var rrb = EngagedRusher.GetComponent<Rigidbody>();
            if (rrb == null) return;

            Vector3 awayFromQb = rusher - qbPos;
            awayFromQb.z = 0f;
            if (awayFromQb.sqrMagnitude < 0.01f)
            {
                float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                awayFromQb = Vector3.right * dir;
            }
            awayFromQb.Normalize();

            Vector3 lateral = new Vector3(-awayFromQb.y, awayFromQb.x, 0f);
            if (EngagedRusher.position.y < transform.position.y) lateral = -lateral;

            Vector3 push = (awayFromQb * 0.75f + lateral * 0.35f) * pushForce;
            ArcadeMove.Apply(rrb, push);
        }

        void MoveTowards(Vector3 target, float speed)
        {
            Vector3 delta = target - transform.position;
            delta.z = 0f;
            Vector3 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;
            if (EngagedRusher != null)
            {
                float d = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    new Vector2(EngagedRusher.position.x, EngagedRusher.position.y));
                if (d < engageRadius) speed *= 1.2f;
            }

            ArcadeMove.Apply(rb, dir * speed);
        }

        void PursueInterceptionReturner()
        {
            if (!InterceptionReturner.TryGetActive(out var ret) || ret == null)
            {
                StopMotion();
                return;
            }

            Vector3 aim = ret.transform.position;
            aim += Vector3.right * (ret.TowardLowEndzone ? -0.55f : 0.55f);
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            float spd = isTightEnd ? 5.6f : 4.6f;
            Vector3 vel = delta.sqrMagnitude > 0.01f ? delta.normalized * spd : Vector3.zero;
            ArcadeMove.Apply(rb, vel);

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(ret.transform.position.x, ret.transform.position.y));
            if (dist <= battleStartRadius)
                ret.TryAcceptTackle(transform);
        }

        void PursueLooseFumble(FootballBehavior ball)
        {
            if (ball == null)
            {
                StopMotion();
                return;
            }

            Vector3 delta = ball.PlayPlanePosition - transform.position;
            delta.z = 0f;
            float spd = isTightEnd ? 5.8f : 4.9f;
            Vector3 vel = delta.sqrMagnitude > 0.01f ? delta.normalized * spd : Vector3.zero;
            ArcadeMove.Apply(rb, vel);
        }

        void StopMotion()
        {
            if (rb != null && (stun == null || !stun.IsStunned))
                ArcadeMove.Apply(rb, Vector3.zero);
        }

        static void StopOther(Transform t)
        {
            if (t == null) return;
            if (PlayerStun.IsUnitStunned(t)) return;
            var otherRb = t.GetComponent<Rigidbody>();
            ArcadeMove.Apply(otherRb, Vector3.zero);
        }

        public float GetHoldSpeedMul(Transform rusher)
        {
            if (!IsPassProActive || rusher == null || EngagedRusher != rusher)
                return 1f;
            if (stun != null && stun.IsStunned) return 1f;
            if (IsInContactBattle) return holdSpeedMul * 0f;

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(rusher.position.x, rusher.position.y));
            if (dist > holdRadius * 1.15f) return 1f;
            return holdSpeedMul;
        }

        public static float GetBlockSlowFor(Transform rusher)
        {
            if (rusher == null) return 1f;
            float mul = 1f;
            var blockers = UnityEngine.Object.FindObjectsByType<OffensiveBlocker>(FindObjectsInactive.Exclude);
            foreach (var b in blockers)
            {
                if (b == null || !b.IsPassProActive) continue;
                if (PlayerStun.IsUnitStunned(b)) continue;
                float m = b.GetHoldSpeedMul(rusher);
                if (m < mul) mul = m;
            }
            return mul;
        }

        public static OffensiveBlocker FindNearestEngagedTo(Transform rusher, float maxDist, out float dist)
        {
            dist = float.MaxValue;
            OffensiveBlocker best = null;
            if (rusher == null) return null;

            var blockers = UnityEngine.Object.FindObjectsByType<OffensiveBlocker>(FindObjectsInactive.Exclude);
            Vector2 rp = new Vector2(rusher.position.x, rusher.position.y);
            foreach (var b in blockers)
            {
                if (b == null || !b.IsPassProActive) continue;
                if (PlayerStun.IsUnitStunned(b)) continue;
                float d = Vector2.Distance(rp, new Vector2(b.transform.position.x, b.transform.position.y));
                if (d < dist && d <= maxDist)
                {
                    dist = d;
                    best = b;
                }
            }
            return best;
        }

        public void ResetForNextPlay()
        {
            EndPassPro();
            releasedToRoute = false;
            passProStartedAt = -1f;
            homePos = transform.position;
            pocketOffsetFromQb = Vector3.zero;
            if (stun != null) stun.Clear();
            StopMotion();
        }

        /// <summary>Strip temporary kickoff wall blockers (WR/QB) after the return ends.</summary>
        public static void ClearKickoffWallOnlyBlockers()
        {
            var blockers = UnityEngine.Object.FindObjectsByType<OffensiveBlocker>(FindObjectsInactive.Include);
            foreach (var b in blockers)
            {
                if (b == null || !b.kickoffWallOnly) continue;
                UnityEngine.Object.Destroy(b);
            }
        }
    }
}
