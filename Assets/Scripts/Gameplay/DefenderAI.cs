using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public enum DefenderRole
    {
        Rush,   // DL — crash the pocket
        Cover,  // CB / S — trail receivers, break on ball
        Blitz   // LB — read, then fill / delayed rush
    }

    public enum DefenderState
    {
        Patrolling,
        Rushing,
        Pursuing,
        Covering,
        Filling
    }

    /// <summary>
    /// Retro Bowl–style defense AI (behavior mimic, not a code port):
    /// DL rush QB with rising pressure → CBs trail WRs with over-top shade →
    /// LBs delay then fill → everyone swarms the BallCarrier after the catch.
    /// </summary>
    public class DefenderAI : MonoBehaviour
    {
        [Header("Role")]
        public DefenderRole role = DefenderRole.Cover;

        [Header("Speeds")]
        public float moveSpeed = 3.0f;
        public float rushSpeed = 2.6f;
        public float pursueSpeedMul = 1.4f;
        public float coverSpeedMul = 1.05f;
        public float fillSpeedMul = 1.2f;

        [Header("Contact")]
        public float detectionRadius = 16f;
        public float tackleRadius = 0.9f;
        public float blockEngageRadius = 0.55f;
        public float blockedSpeedMul = 0.4f;
        public float shedLateral = 1.15f;
        [Tooltip("Min seconds between wrap attempts after a whiff.")]
        public float wrapRetryCooldown = 0.45f;

        [Header("Retro Bowl timings")]
        [Tooltip("LB wait before crashing / filling.")]
        public float lbReadSeconds = 0.65f;
        [Tooltip("Extra rush speed after this many seconds in pocket.")]
        public float pressureRampStart = 1.1f;
        public float pressureRampMax = 1.55f;
        [Tooltip("CB/S break on thrown ball within this base radius (scaled by awareness).")]
        public float ballBreakRadius = 7.5f;
        [Tooltip("Shade over the top of the WR (world +X).")]
        public float coverShadeX = 1.6f;

        [Header("Stats (0–100)")]
        public int speed = 70;
        public int awareness = 60;
        public int tackling = 65;

        [Header("AI State")]
        public DefenderState currentState = DefenderState.Patrolling;
        public bool IsPlayerControlled;
        public Transform ballCarrier;
        public Transform ballTarget;

        Transform currentTarget;
        Transform assignedReceiver;
        Vector3 patrolPosition;
        Rigidbody rb;
        float nextAttemptLogAt;
        float rushLaneBias;
        float playStartedAt = -1f;
        float coverLaneSign = 1f; // +1 prefer top WR, -1 bottom

        // Brief get-off pause so OL can engage before free rush (was 0.06 — near-instant).
        const float RushWindup = 0.22f;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            ArcadeMove.ConfigureKinematicBody(rb);
            AutoAssignRole();
            rushLaneBias = Random.Range(-0.85f, 0.85f);
            SetRandomPatrolPosition();
            PlayerStun.GetOrAdd(gameObject);
        }

        void AutoAssignRole()
        {
            // Role / cover geometry only — moveSpeed comes from StatBridge roster stats.
            // (Do not overwrite speeds here: Start() runs after ApplyMatchSides.)
            string n = gameObject.name;
            if (n.StartsWith("DL_"))
            {
                role = DefenderRole.Rush;
            }
            else if (n.StartsWith("LB_"))
            {
                role = DefenderRole.Blitz;
            }
            else if (n.StartsWith("S") || n == "S" || n.Contains("Safety"))
            {
                role = DefenderRole.Cover;
                coverShadeX = 2.4f; // deeper shade
            }
            else
            {
                // CB_Top / CB_Bot
                role = DefenderRole.Cover;
                coverLaneSign = n.Contains("Bot") || n.Contains("Bottom") ? -1f : 1f;
            }
        }

        void Update()
        {
            bool liveReturn = GameManager.Instance != null && GameManager.Instance.IsLiveReturn;
            bool looseKickoff = FootballBehavior.TryGetLooseKickoff(out _);
            bool kickoffChasePhase = IsKickoffReturnerChasePhase();
            // Live return + loose KO: coverage/gunners run. Aim / flight stay frozen.
            bool specialTeamsLive = liveReturn || looseKickoff;
            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || (!specialTeamsLive && (GameManager.Instance.isPreSnap || GameManager.Instance.isKicking))
                || GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.pendingQuarterEnd
                || (FieldManager.Instance != null
                    && (FieldManager.Instance.JustScoredTouchdown
                        || FieldManager.Instance.JustScoredSafety)))
            {
                // AI kickoff returner (S) is driven by KickingController chase — do not freeze them.
                if (kickoffChasePhase && IsDesignatedKickoffReturner())
                    return;

                playStartedAt = -1f;
                currentTarget = null;
                assignedReceiver = null;
                StopMotion();
                return;
            }

            if (PlayerStun.IsUnitStunned(this))
                return;

            // Interceptor is driven by InterceptionReturner — do not swarm self / teammates.
            if (InterceptionReturner.IsUnitReturning(this))
            {
                StopMotion();
                return;
            }

            if (IsPlayerControlled)
                return;

            // Mash lock freezes only this defender — teammates keep engaging.
            if (ContactBattle.IsCombatant(transform) || TecmoContact.IsInvolved(transform))
            {
                StopMotion();
                return;
            }

            if (playStartedAt < 0f)
                playStartedAt = Time.time;

            if (GameManager.Instance.isInterceptionReturn)
            {
                RunIntReturnSupport();
                return;
            }

            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;

            // Designated returner is driven by ReceiverController / kick chase — never wall-block.
            if (IsDesignatedKickoffReturner())
                return;

            // Loose kickoff / kickoff return: role depends on who is receiving.
            // Player receive → DefenderAI = coverage (chase). Player kick → DefenderAI = receive wall (block).
            if (FootballBehavior.TryGetLooseKickoff(out var koBall) && koBall != null)
            {
                if (playerReceives) PursueLooseKickoff(koBall);
                else RunKickoffReceiveWallBlock();
                return;
            }

            if (GameManager.Instance.isKickoffReturn && !playerReceives)
            {
                RunKickoffReceiveWallBlock();
                return;
            }

            // Loose ball scramble — dive toward the fumble before normal coverage.
            if (FootballBehavior.TryGetLooseFumble(out var loose) && loose != null)
            {
                PursueLooseFumble(loose);
                return;
            }

            UpdateAI();
        }

        /// <summary>
        /// Kickoff approach / loft / bounce — KickingController drives the returner to land.
        /// </summary>
        static bool IsKickoffReturnerChasePhase()
        {
            if (GameManager.Instance == null || !GameManager.Instance.isKicking)
                return false;
            var kick = KickingController.Instance;
            if (kick == null) return false;
            return kick.IsKickerApproaching || kick.IsBallInFlight;
        }

        bool IsDesignatedKickoffReturner()
        {
            try
            {
                if (CompareTag("BallCarrier")) return true;
            }
            catch (UnityException) { }

            var rc = GetComponent<ReceiverController>();
            if (rc != null && rc.hasBall) return true;

            // Name match during loft/chase/return (KickoffReceiverIsPlayer persists after).
            bool koLive = GameManager.Instance != null
                          && (GameManager.Instance.isKickoffReturn
                              || FootballBehavior.TryGetLooseKickoff(out _)
                              || IsKickoffReturnerChasePhase());
            if (!koLive) return false;

            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            return gameObject.name == (playerReceives ? "RB" : "S");
        }

        void PursueLooseKickoff(FootballBehavior ball)
        {
            if (ball == null)
            {
                StopMotion();
                return;
            }

            Vector3 aim = ball.PlayPlanePosition;
            // Bias toward deep returner if present so gunners don't all stack on the ball.
            var ret = GameObject.Find("RB");
            if (ret == null || !ret.activeInHierarchy)
                ret = GameObject.Find("S");
            if (ret != null && ret.activeInHierarchy)
                aim = Vector3.Lerp(aim, ret.transform.position, 0.45f);

            float blockSlow = OffensiveBlocker.GetBlockSlowFor(transform);
            var engaged = OffensiveBlocker.FindNearestEngagedTo(
                transform, blockEngageRadius * 1.4f, out float blockDist);
            if (engaged != null && blockDist < blockEngageRadius * 1.15f)
            {
                Vector3 lateral = rushLaneBias < 0f ? Vector3.down : Vector3.up;
                Vector3 pushPoint = transform.position
                                   + (aim - transform.position).normalized * 0.2f
                                   + lateral * shedLateral;
                pushPoint.z = 0f;
                MoveTowards(pushPoint, Mathf.Min(blockedSpeedMul, blockSlow) * 1.15f);
                return;
            }

            float mul = blockSlow < 1f ? Mathf.Lerp(0.55f, 1.15f, blockSlow) : 1.2f;
            MoveTowards(aim, mul);
        }

        /// <summary>
        /// When the opponent is returning, Defender-tagged units are the receive wall —
        /// peel to the nearest offense gunner instead of chasing our own returner.
        /// </summary>
        void RunKickoffReceiveWallBlock()
        {
            Transform threat = FindNearestKickoffCoverThreat();
            if (threat == null)
            {
                StopMotion();
                return;
            }

            Vector3 returnerPos = ResolveKickoffReturnerPos();
            Vector3 toRet = returnerPos - threat.position;
            toRet.z = 0f;
            if (toRet.sqrMagnitude < 0.01f) toRet = Vector3.right;
            Vector3 engage = threat.position + toRet.normalized * 0.55f;
            engage.z = 0f;

            float d = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(threat.position.x, threat.position.y));
            MoveTowards(engage, d < blockEngageRadius * 1.3f ? 0.85f : 1.15f);

            if (d < blockEngageRadius * 1.1f)
            {
                Vector3 away = threat.position - returnerPos;
                away.z = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.left;
                var rrb = threat.GetComponent<Rigidbody>();
                if (rrb != null)
                    ArcadeMove.Apply(rrb, away.normalized * 2.2f);
            }
        }

        static Vector3 ResolveKickoffReturnerPos()
        {
            try
            {
                var bc = GameObject.FindGameObjectWithTag("BallCarrier");
                if (bc != null && bc.activeInHierarchy)
                    return bc.transform.position;
            }
            catch (UnityException) { }

            var s = GameObject.Find("S");
            if (s != null && s.activeInHierarchy) return s.transform.position;
            var rb = GameObject.Find("RB");
            if (rb != null && rb.activeInHierarchy) return rb.transform.position;
            return Vector3.zero;
        }

        Transform FindNearestKickoffCoverThreat()
        {
            Transform best = null;
            float bestDist = 10f;
            Vector2 my = new Vector2(transform.position.x, transform.position.y);

            void Consider(GameObject go)
            {
                if (go == null || !go.activeInHierarchy || go == gameObject) return;
                if (PlayerStun.IsUnitStunned(go)) return;
                try
                {
                    if (go.CompareTag("BallCarrier")) return;
                    if (go.CompareTag("Defender")) return;
                }
                catch (UnityException) { }
                float d = Vector2.Distance(my, new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }

            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Receiver"))
                    Consider(go);
            }
            catch (UnityException) { }

            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Lineman"))
                    Consider(go);
            }
            catch (UnityException) { }

            Consider(GameObject.Find("Quarterback"));
            return best;
        }

        void PursueLooseFumble(FootballBehavior ball)
        {
            if (ball == null)
            {
                StopMotion();
                return;
            }

            MoveTowards(ball.PlayPlanePosition, 1.28f);
        }

        /// <summary>
        /// Teammates of the interceptor: lead a soft convoy / bump offensive pursuers.
        /// </summary>
        void RunIntReturnSupport()
        {
            if (!InterceptionReturner.TryGetActive(out var ret) || ret == null)
            {
                StopMotion();
                return;
            }

            Transform threat = FindNearestIntReturnThreat(ret.transform.position);
            if (threat != null)
            {
                float d = Vector3.Distance(transform.position, threat.position);
                MoveTowards(threat.position, 1.05f);
                if (d < blockEngageRadius * 1.15f)
                {
                    Vector3 away = threat.position - transform.position;
                    away.z = 0f;
                    if (away.sqrMagnitude < 0.01f)
                        away = Vector3.up;
                    PlayerStun.GetOrAdd(threat.gameObject)?.Stun(0.32f, away.normalized * 0.42f);
                }
                return;
            }

            float leadX = ret.TowardLowEndzone ? -2.6f : 2.6f;
            Vector3 lead = ret.transform.position + Vector3.right * leadX;
            lead.y += rushLaneBias * 0.85f;
            MoveTowards(lead, 0.92f);
        }

        static Transform FindNearestIntReturnThreat(Vector3 from)
        {
            Transform best = null;
            float bestDist = 7.5f;
            Vector2 p = new Vector2(from.x, from.y);

            void Consider(GameObject go)
            {
                if (go == null || !go.activeInHierarchy) return;
                if (PlayerStun.IsUnitStunned(go)) return;
                // Skip defensive teammates.
                if (go.CompareTag("Defender") || go.CompareTag("BallCarrier")) return;
                float d = Vector2.Distance(p, new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }

            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Receiver"))
                    Consider(go);
            }
            catch (UnityException) { }

            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Lineman"))
                    Consider(go);
            }
            catch (UnityException) { }

            var qb = GameObject.Find("Quarterback");
            Consider(qb);

            return best;
        }

        void StopMotion() => ArcadeMove.Apply(rb, Vector3.zero);

        void UpdateAI()
        {
            FindTarget();

            switch (currentState)
            {
                case DefenderState.Patrolling:
                    Patrol();
                    break;
                case DefenderState.Rushing:
                    RushPocket();
                    break;
                case DefenderState.Pursuing:
                    Pursue();
                    break;
                case DefenderState.Covering:
                    CoverReceiver();
                    break;
                case DefenderState.Filling:
                    FillToBall();
                    break;
            }
        }

        // ─── Targeting (Retro Bowl priority) ─────────────────────────────

        void FindTarget()
        {
            // 1) Live ball carrier — everyone swarms (not during INT return — handled above).
            if (TryGetBallCarrier(out Transform carrier))
            {
                // Never pursue our own interceptor.
                if (InterceptionReturner.IsUnitReturning(carrier))
                {
                    ballCarrier = null;
                }
                else
                {
                    ballCarrier = carrier;
                    currentTarget = carrier;
                    currentState = DefenderState.Pursuing;
                    return;
                }
            }

            ballCarrier = null;

            // 2) Ball in the air — break according to role / awareness.
            if (TryGetLivePass(out Transform ball, out FootballBehavior fb))
            {
                ballTarget = ball;
                if (ShouldBreakOnBall(ball, fb))
                {
                    currentTarget = ball;
                    currentState = DefenderState.Pursuing;
                    return;
                }
            }
            else
            {
                ballTarget = null;
            }

            // 3) Pocket pressure while QB still holds (guess-blitz: everyone rushes).
            // After handoff / throw, BallCarrier / ball targeting above handles pursuit —
            // do NOT force every CB onto the RB here (that felt like permanent max-blitz on runs).
            if (ShouldRushQuarterback())
            {
                var qb = FindHoldQb();
                if (qb != null)
                {
                    currentTarget = qb;
                    currentState = DefenderState.Rushing;
                    return;
                }
            }

            // 4) LBs fill to expected run / short middle after read.
            if (role == DefenderRole.Blitz && PlayAge() >= lbReadSeconds)
            {
                var fill = FindFillAim();
                if (fill != null)
                {
                    currentTarget = fill;
                    currentState = DefenderState.Filling;
                    return;
                }
            }

            // 5) Secondary coverage.
            if (role == DefenderRole.Cover || role == DefenderRole.Blitz)
            {
                var wr = AssignCoverageReceiver();
                if (wr != null)
                {
                    currentTarget = wr;
                    currentState = DefenderState.Covering;
                    return;
                }
            }

            currentState = DefenderState.Patrolling;
        }

        static bool TryGetBallCarrier(out Transform carrier)
        {
            carrier = null;
            try
            {
                var go = GameObject.FindGameObjectWithTag("BallCarrier");
                if (go != null && go.activeInHierarchy)
                {
                    carrier = go.transform;
                    return true;
                }
            }
            catch (UnityException) { }

            // Catch / handoff runner — swarm. Pocket QB is NOT a BallCarrier.
            var receivers = GameObject.FindGameObjectsWithTag("Receiver");
            foreach (var r in receivers)
            {
                if (r == null) continue;
                var rc = r.GetComponent<ReceiverController>();
                if (rc != null && rc.hasBall)
                {
                    carrier = r.transform;
                    return true;
                }
            }

            // Scramble only: ConvertToScrambleRun tags BallCarrier, but keep an
            // isScrambling fallback. Do NOT treat "QB hasBall pre-throw" as a
            // carrier — that made every defender Pursue through the OL on snap.
            var qb = GameObject.Find("Quarterback");
            if (qb != null)
            {
                var qbc = qb.GetComponent<QuarterbackController>();
                if (qbc != null && qbc.isScrambling)
                {
                    carrier = qb.transform;
                    return true;
                }
            }

            return false;
        }

        static bool TryGetLivePass(out Transform ball, out FootballBehavior fb)
        {
            ball = null;
            fb = null;
            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go == null) return false;
                fb = go.GetComponent<FootballBehavior>();
                if (fb == null || !fb.isInAir || fb.isCaught) return false;
                ball = go.transform;
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        bool ShouldBreakOnBall(Transform ball, FootballBehavior fb)
        {
            if (ball == null || fb == null) return false;

            float aware = Mathf.Clamp01(awareness / 100f);
            float radius = ballBreakRadius * Mathf.Lerp(0.65f, 1.35f, aware);

            // DL rarely leave the rush for a deep ball.
            if (role == DefenderRole.Rush)
                return Vector3.Distance(transform.position, ball.position) < radius * 0.45f;

            // Safeties / CBs: break if near the flight path or landing.
            Vector3 land = fb.ThrowTarget;
            float toBall = Vector3.Distance(transform.position, ball.position);
            float toLand = Vector3.Distance(transform.position, land);
            if (toBall <= radius || toLand <= radius * 1.15f)
                return true;

            // Late catch window — commit so tip / INT contests actually happen.
            if (fb.IsInCatchWindow() && toLand <= radius * 1.45f)
                return true;

            // High-awareness LBs jump short throws.
            if (role == DefenderRole.Blitz && toLand <= radius * 0.85f)
                return true;

            return false;
        }

        bool ShouldRushQuarterback()
        {
            if (DefenseGuessBlitz.Active)
                return true;

            if (role == DefenderRole.Rush)
                return true;

            // LB delayed blitz if QB is still in the pocket.
            if (role == DefenderRole.Blitz && PlayAge() >= lbReadSeconds)
            {
                var qb = FindHoldQb();
                return qb != null;
            }

            return false;
        }

        static Transform FindHoldQb()
        {
            var qb = GameObject.Find("Quarterback");
            if (qb == null) return null;
            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc == null) return qb.transform;
            if (qbc.hasThrown && !qbc.isScrambling) return null;
            if (qbc.isScrambling) return null; // handled as BallCarrier
            var pc = qb.GetComponent<PlayerController>();
            if (pc != null && !pc.hasBall) return null;
            return qb.transform;
        }

        Transform AssignCoverageReceiver()
        {
            // Sticky assignment — don't ping-pong every frame.
            if (assignedReceiver != null
                && assignedReceiver.gameObject.activeInHierarchy)
            {
                var rc = assignedReceiver.GetComponent<ReceiverController>();
                if (rc != null && !rc.hasBall)
                    return assignedReceiver;
            }

            GameObject[] receivers;
            try { receivers = GameObject.FindGameObjectsWithTag("Receiver"); }
            catch { return null; }

            Transform best = null;
            float bestScore = float.MaxValue;
            Vector2 self = new Vector2(transform.position.x, transform.position.y);

            foreach (var go in receivers)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name.StartsWith("RB") && role == DefenderRole.Cover) continue; // CBs prefer WRs
                var rc = go.GetComponent<ReceiverController>();
                if (rc != null && rc.hasBall) continue;

                Vector2 p = new Vector2(go.transform.position.x, go.transform.position.y);
                float dist = Vector2.Distance(self, p);
                // Prefer same side of field (top/bot CB).
                float lanePenalty = 0f;
                if (role == DefenderRole.Cover)
                {
                    float laneAlign = Mathf.Sign(p.y == 0f ? coverLaneSign : p.y) * coverLaneSign;
                    if (laneAlign < 0f) lanePenalty = 4.5f;
                }

                float score = dist + lanePenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = go.transform;
                }
            }

            assignedReceiver = best;
            return best;
        }

        Transform FindFillAim()
        {
            // Prefer short middle / RB / QB if still holding; else nearest skill.
            var rbGo = GameObject.Find("RB");
            if (rbGo != null && rbGo.activeInHierarchy)
                return rbGo.transform;

            var qb = FindHoldQb();
            if (qb != null) return qb;

            return AssignCoverageReceiver();
        }

        float PlayAge() => playStartedAt < 0f ? 0f : Time.time - playStartedAt;

        float SpeedFactor()
        {
            float n = Mathf.Clamp(speed / 70f, 0.75f, 1.35f);
            return DefenseGuessBlitz.SpeedFactorOr(n);
        }

        float EffectiveTackleRadius => tackleRadius * DefenseGuessBlitz.TackleRadiusMul;

        // ─── Behaviors ───────────────────────────────────────────────────

        void RushPocket()
        {
            if (currentTarget == null)
            {
                currentState = DefenderState.Patrolling;
                return;
            }

            if (!DefenseGuessBlitz.Active && PlayAge() < RushWindup)
            {
                StopMotion();
                return;
            }

            Vector3 aim = currentTarget.position;
            aim.y += rushLaneBias;
            aim.z = 0f;

            // Rising pressure the longer the QB holds (Retro Bowl sack clock feel).
            float pressure = 1f;
            if (PlayAge() > pressureRampStart)
            {
                float t = Mathf.InverseLerp(pressureRampStart, pressureRampStart + 2.2f, PlayAge());
                pressure = Mathf.Lerp(1f, pressureRampMax, t);
            }

            float blockSlow = OffensiveBlocker.GetBlockSlowFor(transform);
            var engaged = OffensiveBlocker.FindNearestEngagedTo(transform, blockEngageRadius * 1.4f, out float blockDist);

            if (engaged != null && blockDist < blockEngageRadius * 1.15f)
            {
                if ((GameRules.EnableContactBattle || GameRules.EnableLineEngageBattles)
                    && blockDist <= 0.95f)
                {
                    ContactBattle.Begin(engaged.transform, transform);
                    StopMotion();
                    return;
                }

                // Already locked in a contact fight — do not crawl through.
                if (ContactBattle.IsCombatant(transform)
                    || ContactBattle.IsCombatant(engaged.transform))
                {
                    StopMotion();
                    return;
                }

                Vector3 lateral = rushLaneBias < 0f ? Vector3.down : Vector3.up;
                Vector3 pushPoint = transform.position
                                   + (aim - transform.position).normalized * 0.25f
                                   + lateral * shedLateral;
                pushPoint.z = 0f;
                MoveTowards(pushPoint, Mathf.Min(blockedSpeedMul, blockSlow) * pressure);
                TrySackQuarterback();
                return;
            }

            if (TryGetNearestBlocker(out _, out float dist) && dist < blockEngageRadius)
            {
                Vector3 lateral = rushLaneBias < 0f ? Vector3.down : Vector3.up;
                Vector3 pushPoint = transform.position
                                   + (aim - transform.position).normalized * 0.35f
                                   + lateral * shedLateral;
                pushPoint.z = 0f;
                MoveTowards(pushPoint, blockedSpeedMul * blockSlow * pressure);
                TrySackQuarterback();
                return;
            }

            float freeMul = blockSlow < 1f ? Mathf.Lerp(0.6f, 1.15f, blockSlow) : 1.2f;
            MoveTowards(aim, freeMul * pressure);
            TrySackQuarterback();
            TryBattleNearestOffense();
        }

        void CoverReceiver()
        {
            if (currentTarget == null)
            {
                currentState = DefenderState.Patrolling;
                return;
            }

            // Trail the WR with shade over the top (toward attack endzone).
            float driveDir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            Vector3 cover = currentTarget.position + Vector3.right * (coverShadeX * driveDir);
            // Slight inside leverage toward hashes.
            cover.y = Mathf.Lerp(cover.y, currentTarget.position.y * 0.85f, 0.35f);
            cover.z = 0f;

            float mul = coverSpeedMul;
            // Close the gap if WR is separating downfield.
            float sep = (currentTarget.position.x - transform.position.x) * driveDir;
            if (sep > 1.5f)
                mul *= 1.15f;

            MoveTowards(cover, mul);
            TryBattleNearestOffense();
        }

        void FillToBall()
        {
            if (currentTarget == null)
            {
                currentState = DefenderState.Patrolling;
                return;
            }

            MoveTowards(currentTarget.position, fillSpeedMul);
            TrySackQuarterback();
            TryBattleNearestOffense();
        }

        void Pursue()
        {
            if (currentTarget == null)
            {
                currentState = DefenderState.Patrolling;
                return;
            }

            // Lead the runner slightly downfield so angles feel Retro Bowl–aggressive.
            Vector3 aim = currentTarget.position;
            var rc = currentTarget.GetComponent<ReceiverController>();
            var qbc = currentTarget.GetComponent<QuarterbackController>();
            bool kickReturn = GameManager.Instance != null && GameManager.Instance.isKickoffReturn;
            if (rc != null || (qbc != null && qbc.isScrambling))
            {
                float lead = 0.8f;
                if (FieldManager.Instance != null)
                    lead = kickReturn
                        ? FieldManager.Instance.ReturnDirX * 0.8f
                        : FieldManager.Instance.DriveDirX * 0.8f;
                else if (kickReturn)
                    lead = -0.8f;
                aim += Vector3.right * lead;
            }

            float blockSlow = OffensiveBlocker.GetBlockSlowFor(transform);
            var engaged = OffensiveBlocker.FindNearestEngagedTo(
                transform, blockEngageRadius * 1.4f, out float blockDist);

            if (engaged != null && blockDist < blockEngageRadius * 1.15f)
            {
                if ((GameRules.EnableContactBattle || GameRules.EnableLineEngageBattles)
                    && blockDist <= 0.95f)
                {
                    ContactBattle.Begin(engaged.transform, transform);
                    StopMotion();
                    return;
                }

                if (ContactBattle.IsCombatant(transform)
                    || ContactBattle.IsCombatant(engaged.transform))
                {
                    StopMotion();
                    return;
                }

                // Soft-held by receive wall — shed laterally instead of ghosting through.
                Vector3 lateral = rushLaneBias < 0f ? Vector3.down : Vector3.up;
                Vector3 pushPoint = transform.position
                                   + (aim - transform.position).normalized * 0.2f
                                   + lateral * shedLateral;
                pushPoint.z = 0f;
                MoveTowards(pushPoint, Mathf.Min(blockedSpeedMul, blockSlow) * pursueSpeedMul);
            }
            else if (TryGetNearestBlocker(out _, out float dist) && dist < blockEngageRadius)
            {
                Vector3 lateral = rushLaneBias < 0f ? Vector3.down : Vector3.up;
                Vector3 pushPoint = transform.position
                                   + (aim - transform.position).normalized * 0.3f
                                   + lateral * shedLateral;
                pushPoint.z = 0f;
                MoveTowards(pushPoint, blockedSpeedMul * blockSlow * pursueSpeedMul);
            }
            else
            {
                float freeMul = blockSlow < 1f
                    ? Mathf.Lerp(0.55f, 1f, blockSlow) * pursueSpeedMul
                    : pursueSpeedMul;
                MoveTowards(aim, freeMul);
            }

            float distance = Vector3.Distance(transform.position, currentTarget.position);
            if (distance < EffectiveTackleRadius)
            {
                // Soft-held by a blocker — don't free-tackle through the wall.
                if (engaged != null && blockDist < blockEngageRadius * 0.9f)
                    return;

                if (qbc != null)
                {
                    int tack = DefenseGuessBlitz.TackleStatOr(tackling);
                    float stripChance = Mathf.Lerp(0.05f, 0.14f, Mathf.Clamp01(tack / 100f));
                    bool hardHit = Random.value < stripChance;
                    qbc.ApplySackOrTackle(gameObject.name, hardHit: hardHit, tackler: transform);
                }
                else
                    AttemptTackle();
            }

            TryBattleNearestOffense();
        }

        void Patrol()
        {
            MoveTowards(patrolPosition, 0.65f);
            if (Vector3.Distance(transform.position, patrolPosition) < 2f)
                SetRandomPatrolPosition();
        }

        void TrySackQuarterback()
        {
            if (currentTarget == null) return;
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            if (distance > EffectiveTackleRadius) return;

            var qbc = currentTarget.GetComponent<QuarterbackController>();
            if (qbc != null)
                qbc.ApplySackOrTackle(gameObject.name, tackler: transform);
        }

        /// <summary>
        /// Any nearby offense unit (OL / WR / TE / QB without ball) → ContactBattle.
        /// Carrier wraps stay on ApplySackOrTackle / TecmoContact.
        /// </summary>
        void TryBattleNearestOffense()
        {
            if (!GameRules.EnableContactBattle && !GameRules.EnableLineEngageBattles)
                return;
            if (ContactBattle.IsCombatant(transform) || TecmoContact.IsInvolved(transform))
                return;

            Transform best = null;
            float bestDist = blockEngageRadius * 1.05f;
            TryNearestTagged("Lineman", ref best, ref bestDist);
            TryNearestTagged("Receiver", ref best, ref bestDist);
            TryNearestTagged("Player", ref best, ref bestDist);

            if (best == null) return;
            if (ContactBattle.TryOpposingTouch(best, transform))
                StopMotion();
        }

        void TryNearestTagged(string tag, ref Transform best, ref float bestDist)
        {
            GameObject[] units;
            try { units = GameObject.FindGameObjectsWithTag(tag); }
            catch { return; }

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            foreach (var u in units)
            {
                if (u == null || !u.activeInHierarchy) continue;
                if (PlayerStun.IsUnitStunned(u)) continue;
                if (ContactBattle.IsBallCarrier(u.transform)) continue;
                float d = Vector2.Distance(pos, new Vector2(u.transform.position.x, u.transform.position.y));
                if (d >= bestDist) continue;
                bestDist = d;
                best = u.transform;
            }
        }

        void AttemptTackle()
        {
            if (GameManager.Instance != null
                && (GameManager.Instance.waitingForNextPlay
                    || GameManager.Instance.currentState != GameState.Playing
                    || GameManager.Instance.pendingQuarterEnd))
                return;
            if (FieldManager.Instance != null
                && (FieldManager.Instance.JustScoredTouchdown
                    || FieldManager.Instance.JustScoredSafety))
                return;
            if (currentTarget == null) return;
            if (Time.time < nextAttemptLogAt) return;
            nextAttemptLogAt = Time.time + wrapRetryCooldown;

            var rc = currentTarget.GetComponent<ReceiverController>();
            if (rc == null || !rc.hasBall) return;

            // Occasional strip-angle "big hit" (RB feel) even when player defense is off.
            int tack = DefenseGuessBlitz.TackleStatOr(tackling);
            float stripChance = Mathf.Lerp(0.06f, 0.16f, Mathf.Clamp01(tack / 100f));
            bool hardHit = Random.value < stripChance;

            // SoftTackle inside ForceTackleFromDefender handles whiff / stiff-arm / wrap / fumble.
            rc.ForceTackleFromDefender(gameObject.name, hardHit, transform);
        }

        void MoveTowards(Vector3 targetPosition, float speedMul)
        {
            Vector3 delta = targetPosition - transform.position;
            delta.z = 0f;
            Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;

            float adjustedSpeed = moveSpeed * SpeedFactor() * speedMul;
            ArcadeMove.Apply(rb, direction * adjustedSpeed);

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && Mathf.Abs(direction.x) > 0.05f)
                sr.flipX = direction.x < 0f;
        }

        bool TryGetNearestBlocker(out Transform blocker, out float dist)
        {
            blocker = null;
            dist = float.MaxValue;
            Vector2 pos = new Vector2(transform.position.x, transform.position.y);

            GameObject[] line;
            try { line = GameObject.FindGameObjectsWithTag("Lineman"); }
            catch { line = System.Array.Empty<GameObject>(); }

            foreach (var ol in line)
            {
                if (ol == null || !ol.activeInHierarchy) continue;
                float d = Vector2.Distance(pos, new Vector2(ol.transform.position.x, ol.transform.position.y));
                if (d < dist)
                {
                    dist = d;
                    blocker = ol.transform;
                }
            }

            var blockers = Object.FindObjectsByType<OffensiveBlocker>(FindObjectsInactive.Exclude);
            foreach (var b in blockers)
            {
                if (b == null || !b.IsPassProActive) continue;
                float d = Vector2.Distance(pos, new Vector2(b.transform.position.x, b.transform.position.y));
                if (d < dist)
                {
                    dist = d;
                    blocker = b.transform;
                }
            }

            return blocker != null;
        }

        void SetRandomPatrolPosition()
        {
            float band = RetroLookApplier.PlayBandHalf;
            patrolPosition = new Vector3(Random.Range(-20f, 20f), Random.Range(-band, band), 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, tackleRadius);
        }
    }
}
