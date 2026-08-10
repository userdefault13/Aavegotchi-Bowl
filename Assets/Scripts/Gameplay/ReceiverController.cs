using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>WR / TE / RB — runs routes, catches passes, then becomes the controlled ball-carrier.</summary>
    public class ReceiverController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 3.2f;
        public float sprintSpeed = 4.4f;
        public float acceleration = 10f;
        Vector3 velocity;

        [Header("Route")]
        public bool isRunningRoute;
        public Vector3[] routePoints;
        int currentRoutePoint;
        public float waypointThreshold = 1f;

        [Header("Adjust To Ball")]
        [Tooltip("Primary adjuster may break from this far to the landing spot.")]
        public float maxBreakRadius = 12f;
        [Tooltip("Non-primary receivers only soft-adjust within this radius.")]
        public float secondaryBreakRadius = 5.5f;
        [Tooltip("Reach ETA must be ≤ remaining flight × this slack.")]
        public float reachTimeSlack = 1.3f;
        [Tooltip("Sprint toward landing when farther than this.")]
        public float adjustSprintDistance = 2.8f;
        /// <summary>True while breaking off the route toward the throw landing.</summary>
        public bool isAdjustingToBall;

        [Header("Catching")]
        public float catchRadius = 1.05f;
        public float catchChance = 0.78f;
        /// <summary>Inside this distance the catch is automatic.</summary>
        public float autoCatchRadius = 0.45f;
        public float jumpPoseHeight = 0.35f;
        public bool hasBall;
        public float tackleRadius = 0.85f;
        public float breakImmunity = 0.85f;

        Rigidbody rb;
        SpriteRenderer spriteRenderer;
        DefenderAI defenseHost;
        bool isPlayerControlled;
        /// <summary>AI offense ball-carrier (player is on defense).</summary>
        bool aiBallCarrier;
        int losYardAtCatch;
        float immuneUntil;
        float jumpPoseUntil;
        Vector3 jumpPoseRestLocal;
        bool jumpPoseActive;
        bool tacklePending;
        bool ballViaHandoff;
        /// <summary>Lateral weave bias for AI / initial handoff lead (−1..1 across).</summary>
        float runLeadLateral;
        float runLeadDownfield = 1f;
        bool kickoffReturnActive;
        bool kickoffKneelPending;
        /// <summary>Player-steered cover gunner after a player kick (PDC owns motion).</summary>
        bool kickoffGunnerControlled;
        int kickoffCatchYard;
        readonly OffenseDiveSlide diveSlide = new OffenseDiveSlide();
        Camera mainCamera;

        /// <summary>True during Tecmo dive burst or get-up.</summary>
        public bool IsDiveBusy => diveSlide.IsBusy;

        /// <summary>True when this component is hosted on a DefenderAI unit (e.g. AI KO returner S).</summary>
        bool IsDefenseHosted => defenseHost != null;

        // Shared primary election so the whole squad doesn't converge on every ball.
        static int s_adjustElectFrame = -1;
        static FootballBehavior s_adjustBall;
        static ReceiverController s_primaryAdjuster;

        void Awake()
        {
            defenseHost = GetComponent<DefenderAI>();
        }

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            ArcadeMove.ConfigureKinematicBody(rb);
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            mainCamera = Camera.main;
            if (defenseHost == null)
                defenseHost = GetComponent<DefenderAI>();

            diveSlide.BindHost(transform);
            GenerateRoute();
            isRunningRoute = false;
            ClearPassAdjust();
            velocity = Vector3.zero;
            diveSlide.Reset();
        }

        bool IsGameplayFrozen()
        {
            if (GameManager.Instance == null) return true;
            if (GameManager.Instance.currentState != GameState.Playing) return true;
            if (GameManager.Instance.waitingForNextPlay) return true;
            // Kickoff / INT return are live play phases — ignore pre-snap / kick gates.
            if (GameManager.Instance.IsLiveReturn) return false;
            // Player fielding a kickoff — steer under the ball while it flies.
            if (IsPlayerKickoffFielding())
                return false;
            return GameManager.Instance.isPreSnap || GameManager.Instance.isKicking;
        }

        /// <summary>Player-receive kickoff: controlling RB before the catch.</summary>
        bool IsPlayerKickoffFielding()
        {
            if (!isPlayerControlled || hasBall) return false;
            if (GameManager.Instance == null || !GameManager.Instance.isKicking)
                return false;
            return FieldManager.Instance != null
                   && FieldManager.Instance.KickoffReceiverIsPlayer;
        }

        /// <summary>
        /// Player-receive kickoff — ring/camera on the returner during flight.
        /// Motion to the land spot is driven by <see cref="KickingController"/> until catch.
        /// </summary>
        public void ArmKickoffFielding()
        {
            if (defenseHost == null)
                defenseHost = GetComponent<DefenderAI>();
            // Never arm the AI returner (S) — player kick path watches only.
            if (IsDefenseHosted)
            {
                isPlayerControlled = false;
                aiBallCarrier = false;
                FollowCameraOnly();
                return;
            }

            hasBall = false;
            ballViaHandoff = false;
            kickoffReturnActive = false;
            kickoffKneelPending = false;
            isRunningRoute = false;
            ClearPassAdjust();
            tacklePending = false;
            velocity = Vector3.zero;

            // Mark as the fielding unit (camera/ring) — WASD waits until after the catch.
            isPlayerControlled = true;
            aiBallCarrier = false;
            SilenceKickerControl();
            FollowCameraAndRing();
            Debug.Log($"{gameObject.name} armed for kickoff fielding (auto to land, then player return)");
        }

        bool YieldMotionToBlocker()
        {
            if (hasBall || isPlayerControlled) return false;
            var blocker = GetComponent<OffensiveBlocker>();
            if (blocker == null) return false;
            // Pass pro, receive-wall peel, or cover-gunner chase during kickoff.
            return blocker.IsPassProActive || OffensiveBlocker.IsKickoffWallPhase();
        }

        /// <summary>PlayerDefenseController steers this gunner on a player kickoff.</summary>
        public void SetKickoffGunnerControl(bool on)
        {
            kickoffGunnerControlled = on;
            if (on)
            {
                isPlayerControlled = false;
                aiBallCarrier = false;
                isRunningRoute = false;
                ClearPassAdjust();
                velocity = Vector3.zero;
            }
        }

        public bool IsKickoffGunnerControlled => kickoffGunnerControlled;

        void Update()
        {
            TickJumpCatchPose();

            // Player kickoff gunner — PlayerDefenseController owns Rigidbody / dive / tackle.
            if (kickoffGunnerControlled && !hasBall)
            {
                ClearPassAdjust();
                isRunningRoute = false;
                velocity = Vector3.zero;
                return;
            }

            // Kickoff wall / pass-pro TE — OffensiveBlocker owns Rigidbody.
            if (YieldMotionToBlocker())
            {
                ClearPassAdjust();
                isRunningRoute = false;
                velocity = Vector3.zero;
                return;
            }

            if (IsGameplayFrozen())
            {
                // Plane already broken — still award even if waitingForNextPlay raced ahead.
                if (hasBall && TryFinishTouchdown())
                    return;

                ClearPassAdjust();
                velocity = Vector3.zero;
                return;
            }

            // Goal-line: award TD even while mash / tackle-pending freezes motion.
            if (hasBall && TryFinishTouchdown())
                return;

            if (tacklePending
                || ContactBattle.IsCombatant(transform)
                || TecmoContact.IsInvolved(transform))
            {
                ClearPassAdjust();
                velocity = Vector3.zero;
                return;
            }

            // DefenderAI hosts this for AI kickoff returns only — yield when not carrying.
            if (IsDefenseHosted && !hasBall)
            {
                ClearPassAdjust();
                isRunningRoute = false;
                velocity = Vector3.zero;
                return;
            }

            // After an INT / fumble recovery return, original offense pursues the returner.
            if (!hasBall
                && GameManager.Instance != null
                && GameManager.Instance.isInterceptionReturn)
            {
                ClearPassAdjust();
                isRunningRoute = false;
                PursueInterceptionReturner();
                return;
            }

            // Player kicked — AI gunners chase; player-controlled gunner handled above.
            if (!hasBall
                && !kickoffGunnerControlled
                && GameManager.Instance != null
                && GameManager.Instance.isKickoffReturn
                && FieldManager.Instance != null
                && !FieldManager.Instance.KickoffReceiverIsPlayer)
            {
                ClearPassAdjust();
                isRunningRoute = false;
                PursueKickoffReturnerAsCover();
                return;
            }

            // Loose ball — race to recover (both teams).
            if (!hasBall && FootballBehavior.TryGetLooseFumble(out _))
            {
                ClearPassAdjust();
                isRunningRoute = false;
                PursueLooseFumble();
                return;
            }

            // AI kneeling for kickoff touchback — hold still until Invoke fires.
            if (kickoffKneelPending)
            {
                ClearPassAdjust();
                velocity = Vector3.zero;
                return;
            }

            if (isPlayerControlled)
            {
                ClearPassAdjust();
                // Kickoff receive: KickingController auto-runs us under the land spot.
                if (IsPlayerKickoffFielding())
                {
                    velocity = Vector3.zero;
                    return;
                }

                // Already across the goal line — score even during mash lock.
                if (hasBall && TryFinishTouchdown())
                    return;

                // Tecmo contact mash — freeze run; Z is consumed by TecmoContact.
                if (TecmoContact.IsInvolved(transform))
                {
                    velocity = Vector3.zero;
                    return;
                }

                HandlePlayerInput();
                if (hasBall)
                {
                    if (TryFinishTouchdown())
                        return;
                    if (IsOutOfBounds())
                    {
                        FinishOutOfBounds();
                        return;
                    }
                    // Dive / get-up: any touch = auto tackle. Otherwise normal wraps after immune.
                    if (Time.time >= immuneUntil)
                    {
                        if (diveSlide.IsBusy)
                            CheckDiveContactTackle();
                        else
                            CheckTackleProximity();
                    }
                }
            }
            else if (aiBallCarrier && hasBall)
            {
                ClearPassAdjust();
                RunAiBallCarrier();
                if (TryFinishTouchdown())
                    return;
                if (IsOutOfBounds())
                {
                    FinishOutOfBounds();
                    return;
                }
                if (Time.time >= immuneUntil)
                    CheckTackleProximity();
            }
            else if (TryAdjustToPass())
            {
                // Breaking toward the throw landing — route waypoints paused.
            }
            else if (isRunningRoute)
                RunRoute();
            else
                velocity = Vector3.zero;

            // Lead blockers / free WRs can peel a defender off a live wrap.
            if (!hasBall)
                TecmoContact.TryTeammatePopAssist(transform);
        }

        void FixedUpdate()
        {
            if (rb == null) return;

            // PlayerDefenseController owns the kickoff gunner Rigidbody.
            if (kickoffGunnerControlled && !hasBall)
                return;

            // Leave Rigidbody to OffensiveBlocker (pass pro / kickoff wall).
            if (YieldMotionToBlocker())
                return;

            // DefenderAI owns motion unless this unit is the live return carrier.
            if (IsDefenseHosted && !hasBall)
                return;

            if (IsGameplayFrozen()
                || tacklePending
                || ContactBattle.IsCombatant(transform)
                || TecmoContact.IsInvolved(transform))
            {
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                return;
            }

            ArcadeMove.Apply(rb, velocity);
        }

        public void GenerateRoute()
        {
            int routeType = Random.Range(0, 3);
            Vector3 startPos = transform.position;
            float cut = RetroLookApplier.PlayBandHalf * 0.7f;
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;

            switch (routeType)
            {
                case 0:
                    routePoints = new[]
                    {
                        startPos + Vector3.right * (8f * dir),
                        startPos + Vector3.right * (8f * dir) + Vector3.up * cut
                    };
                    break;
                case 1:
                    routePoints = new[] { startPos + Vector3.right * (12f * dir) };
                    break;
                default:
                    routePoints = new[]
                    {
                        startPos + Vector3.right * (7f * dir),
                        startPos + Vector3.right * (7f * dir) + Vector3.down * cut
                    };
                    break;
            }

            currentRoutePoint = 0;
        }

        /// <summary>Assign absolute world-space waypoints (from playbook / preview).</summary>
        public void SetRoute(Vector3[] points)
        {
            routePoints = points;
            currentRoutePoint = 0;
            isRunningRoute = false;
        }

        /// <summary>Assign relative offsets (X downfield, Y across) from current snap spot.</summary>
        public void SetRelativeRoute(Vector2[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
            {
                GenerateRoute();
                return;
            }

            Vector3 start = transform.position;
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            var pts = new Vector3[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                pts[i] = start + new Vector3(offsets[i].x * dir, offsets[i].y, 0f);
            SetRoute(pts);
        }

        public void StartRoute()
        {
            if (routePoints == null || routePoints.Length == 0)
                GenerateRoute();
            isRunningRoute = true;
            ClearPassAdjust();
            currentRoutePoint = 0;
            isPlayerControlled = false;
            hasBall = false;
            ballViaHandoff = false;
            aiBallCarrier = false;
            diveSlide.Reset();
        }

        /// <summary>Immediate handoff — RB becomes the controlled ball-carrier (or AI runner on defense).</summary>
        public void ReceiveHandoff()
        {
            if (hasBall) return;

            AttachParkedBall();
            hasBall = true;
            ballViaHandoff = true;
            kickoffReturnActive = false;
            isRunningRoute = false;
            ClearPassAdjust();
            ApplyRunConceptLead(out Vector3 leadVel);
            velocity = leadVel;

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            isPlayerControlled = playerOffense;
            aiBallCarrier = !playerOffense;

            losYardAtCatch = GameManager.Instance != null
                ? GameManager.Instance.playLosYard
                : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20);

            HandOffControlFromQb();
            if (playerOffense)
                FollowCameraAndRing();

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            immuneUntil = Time.time + 0.55f;
            diveSlide.Reset();

            // Don't clobber a live guess-blitz callout on handoff.
            if (!DefenseGuessBlitz.Active)
            {
                string playName = Playbook.Selected != null ? Playbook.Selected.DisplayName : "RUN";
                PlayBanner.Show(playName, 1.2f);
            }
            Debug.Log($"{gameObject.name} took the handoff — {(playerOffense ? "player" : "AI")} controlled.");
        }

        void ApplyRunConceptLead(out Vector3 leadVel)
        {
            float dirX = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            runLeadDownfield = 1f;
            runLeadLateral = 0f;

            var concept = Playbook.Selected != null ? Playbook.Selected.Run : RunConcept.None;
            switch (concept)
            {
                case RunConcept.Dive:
                    runLeadDownfield = 1.05f;
                    runLeadLateral = 0.05f;
                    break;
                case RunConcept.InsideZone:
                    runLeadDownfield = 0.95f;
                    runLeadLateral = 0.35f;
                    break;
                case RunConcept.OutsideZone:
                    runLeadDownfield = 0.75f;
                    runLeadLateral = -0.85f;
                    break;
                case RunConcept.Sweep:
                    runLeadDownfield = 0.55f;
                    runLeadLateral = -1.1f;
                    break;
                default:
                    // Prefer first RB route lateral if present.
                    if (Playbook.Selected?.Rb != null && Playbook.Selected.Rb.Length > 0)
                        runLeadLateral = Mathf.Clamp(Playbook.Selected.Rb[0].y / 3f, -1.2f, 1.2f);
                    break;
            }

            leadVel = new Vector3(moveSpeed * 0.85f * dirX * runLeadDownfield, moveSpeed * 0.55f * runLeadLateral, 0f);
            if (leadVel.sqrMagnitude > 0.01f)
                leadVel = leadVel.normalized * moveSpeed * 0.85f;
        }

        /// <summary>
        /// Kickoff catch → player (or AI) controls the returner toward the kicking endzone
        /// (opposite <see cref="FieldManager.kickDirection"/>).
        /// Player kick → AI returner only (watch camera, no player identity handoff).
        /// </summary>
        public void BeginKickoffReturn(int catchYard)
        {
            if (defenseHost == null)
                defenseHost = GetComponent<DefenderAI>();

            AttachParkedBall();
            hasBall = true;
            ballViaHandoff = false;
            kickoffReturnActive = true;
            kickoffCatchYard = catchYard;
            losYardAtCatch = catchYard;
            isRunningRoute = false;
            ClearPassAdjust();
            velocity = Vector3.zero;
            tacklePending = false;

            // Explicit flag only — never default to "player receives" when FieldManager is missing.
            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            // S (DefenderAI host) after a player kick must stay AI — do not hand player control over.
            if (IsDefenseHosted)
                playerReceives = false;

            isPlayerControlled = playerReceives;
            aiBallCarrier = !playerReceives;

            if (playerReceives)
            {
                // Player receive — handoff identity to RB (camera + selection ring).
                HandOffControlFromQb();
                FollowCameraAndRing();
            }
            else
            {
                // Player kicked — AI returner. Gunner coverage keeps camera/ring if active.
                SilenceKickerControl();
                if (PlayerDefenseController.Instance == null
                    || !PlayerDefenseController.Instance.IsKickoffCoverage)
                    FollowCameraOnly();
            }

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            if (defenseHost != null)
                defenseHost.IsPlayerControlled = false;

            immuneUntil = Time.time + 0.65f;
            diveSlide.Reset();

            // AI in the receive endzone: kneel for touchback (own 35) or run it out.
            bool inReceiveEz = FieldManager.Instance != null
                               && FieldManager.Instance.IsInKickReceiveEndzone(transform.position.x);
            kickoffKneelPending = false;
            if (!playerReceives && inReceiveEz && AiShouldKneelTouchback())
            {
                kickoffKneelPending = true;
                aiBallCarrier = false;
                isPlayerControlled = false;
                velocity = Vector3.zero;
                PlayBanner.Show("TOUCHBACK", 0.85f, BannerTone.Neutral);
                Invoke(nameof(FinishKickoffTouchback), 0.55f);
                Debug.Log($"{gameObject.name} kneels for touchback in the endzone");
                return;
            }

            PlayBanner.Show(inReceiveEz && !playerReceives ? "RETURNING!" : "RETURN!", 1.0f);
            Debug.Log(
                $"{gameObject.name} kickoff return from the {catchYard} — "
                + (playerReceives ? "player" : "AI") + " controlled");
        }

        /// <summary>
        /// AI endzone decision — kneel when coverage is tight / deep; otherwise try to return.
        /// </summary>
        bool AiShouldKneelTouchback()
        {
            if (FieldManager.Instance == null) return true;

            // Deeper into the EZ → more likely to kneel.
            float absYard = FieldManager.Instance.WorldXToYardUnclamped(transform.position.x);
            float depth = FieldManager.Instance.kickDirection > 0
                ? absYard - 100f
                : -absYard;
            float kneelChance = Mathf.Lerp(0.35f, 0.85f, Mathf.Clamp01((depth + 2f) / 10f));

            // Nearby cover (offense-tagged gunners when player kicked) bumps kneel odds.
            int closeGunners = 0;
            void Count(string tag)
            {
                try
                {
                    foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                    {
                        if (go == null || !go.activeInHierarchy || go == gameObject) continue;
                        float d = Vector2.Distance(
                            new Vector2(transform.position.x, transform.position.y),
                            new Vector2(go.transform.position.x, go.transform.position.y));
                        if (d < 6.5f) closeGunners++;
                    }
                }
                catch (UnityException) { }
            }

            if (FieldManager.Instance.KickoffReceiverIsPlayer)
                Count("Defender");
            else
            {
                Count("Receiver");
                Count("Lineman");
                Count("Player");
            }

            kneelChance += Mathf.Clamp(closeGunners * 0.12f, 0f, 0.4f);
            return Random.value < Mathf.Clamp01(kneelChance);
        }

        void RunAiBallCarrier()
        {
            float dirX = 1f;
            if (FieldManager.Instance != null)
                dirX = kickoffReturnActive
                    ? FieldManager.Instance.ReturnDirX
                    : FieldManager.Instance.DriveDirX;
            else if (kickoffReturnActive)
                dirX = -1f;
            float weave = Mathf.Sin(Time.time * 3.1f) * 2.4f;
            // Handoff concepts bias AI cut; kickoff returns keep neutral weave.
            float lateral = kickoffReturnActive
                ? weave
                : weave * 0.45f + runLeadLateral * moveSpeed * 0.55f;
            float forward = kickoffReturnActive ? 0.92f : (0.88f * Mathf.Max(0.5f, runLeadDownfield));
            Vector3 targetVel = new Vector3(moveSpeed * forward * dirX, lateral, 0f);
            if (targetVel.sqrMagnitude > 0.01f)
                targetVel = targetVel.normalized * moveSpeed * 0.92f;
            velocity = Vector3.Lerp(velocity, targetVel, acceleration * Time.deltaTime);

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        void AttachParkedBall()
        {
            FootballBehavior ball = null;
            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null) ball = go.GetComponent<FootballBehavior>();
            }
            catch { /* tag missing */ }

            if (ball == null)
            {
                var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
                foreach (var fb in all)
                {
                    if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                    if (fb.isInAir || fb.isCaught) continue;
                    ball = fb;
                    break;
                }
            }

            if (ball != null)
                ball.Catch(transform);
        }

        void RunRoute()
        {
            if (routePoints == null || currentRoutePoint >= routePoints.Length)
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 targetPoint = routePoints[currentRoutePoint];
            Vector3 direction = (targetPoint - transform.position);
            direction.z = 0f;
            velocity = direction.sqrMagnitude > 0.01f
                ? direction.normalized * moveSpeed
                : Vector3.zero;

            if (Vector3.Distance(transform.position, targetPoint) < waypointThreshold)
            {
                currentRoutePoint++;
                if (currentRoutePoint >= routePoints.Length)
                    velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// While the pass is in the air, break toward the landing target when reachable
        /// (bail underthrows / catch in stride). One primary adjuster; others soft-adjust only if close.
        /// </summary>
        bool TryAdjustToPass()
        {
            if (hasBall)
            {
                ClearPassAdjust();
                return false;
            }

            if (!IsEligiblePassAdjuster())
            {
                ClearPassAdjust();
                return false;
            }

            FootballBehavior ball = FindLivePassBall();
            if (ball == null || !ball.isInAir || ball.isCaught || ball.isBouncing)
            {
                ClearPassAdjust();
                return false;
            }

            ElectPrimaryAdjuster(ball);

            Vector3 land = ball.ThrowTarget;
            land.z = 0f;
            if (FieldManager.Instance != null)
                land = FieldManager.Instance.ClampInBounds(land);

            Vector2 pos2 = new Vector2(transform.position.x, transform.position.y);
            Vector2 land2 = new Vector2(land.x, land.y);
            float distLand = Vector2.Distance(pos2, land2);

            bool isPrimary = s_primaryAdjuster == this;
            float breakRadius = isPrimary ? maxBreakRadius : secondaryBreakRadius;

            if (distLand > breakRadius || !CanReachLanding(distLand, ball, isPrimary))
            {
                ClearPassAdjust();
                return false;
            }

            // Primary always breaks; secondary only soft-adjusts when already near the window.
            if (!isPrimary && distLand > secondaryBreakRadius * 0.85f)
            {
                ClearPassAdjust();
                return false;
            }

            isAdjustingToBall = true;
            // Pause waypoint progress while adjusting — resume route if pass sails past.
            // (isRunningRoute stays true so TE pass-pro release logic is unchanged.)

            Vector3 aim = ComputeCatchLeadAim(ball, land);
            if (FieldManager.Instance != null)
                aim = FieldManager.Instance.ClampInBounds(aim);

            Vector3 toAim = aim - transform.position;
            toAim.z = 0f;
            float distAim = toAim.magnitude;
            if (distAim < 0.12f)
            {
                velocity = Vector3.zero;
                return true;
            }

            float speed = moveSpeed;
            if (isPrimary && distLand >= adjustSprintDistance)
                speed = sprintSpeed;
            else if (!isPrimary)
                speed = moveSpeed * 0.85f;

            // Ease into the landing window so they don't skate past the ball.
            if (distAim < 1.1f)
                speed *= Mathf.Lerp(0.45f, 1f, distAim / 1.1f);

            velocity = toAim.normalized * speed;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;

            return true;
        }

        Vector3 ComputeCatchLeadAim(FootballBehavior ball, Vector3 landing)
        {
            // Early flight: blend toward current play-plane so they don't camp under a ball
            // still rising. Late flight: lock onto the landing window.
            float remainFrac = 1f - Mathf.Clamp01(ball.FlightT);
            Vector3 plane = ball.PlayPlanePosition;
            plane.z = 0f;
            float earlyPull = Mathf.Clamp01(remainFrac * 0.4f);
            return Vector3.Lerp(landing, plane, earlyPull);
        }

        bool CanReachLanding(float distLand, FootballBehavior ball, bool isPrimary)
        {
            float speed = isPrimary ? Mathf.Max(moveSpeed, sprintSpeed) : moveSpeed;
            float eta = distLand / Mathf.Max(0.01f, speed);
            float remain = ball.RemainingFlightTime;
            float slack = isPrimary ? reachTimeSlack : reachTimeSlack * 0.85f;
            // Still allow a short break when flight time is tiny (screen / tip drill).
            float budget = Mathf.Max(0.18f, remain * slack);
            return eta <= budget;
        }

        static void ElectPrimaryAdjuster(FootballBehavior ball)
        {
            if (ball == null) return;
            if (Time.frameCount == s_adjustElectFrame && s_adjustBall == ball)
                return;

            s_adjustElectFrame = Time.frameCount;
            s_adjustBall = ball;
            s_primaryAdjuster = null;

            Vector2 land = new Vector2(ball.ThrowTarget.x, ball.ThrowTarget.y);
            float bestScore = float.MaxValue;

            GameObject[] receivers;
            try { receivers = GameObject.FindGameObjectsWithTag("Receiver"); }
            catch { return; }

            foreach (var go in receivers)
            {
                if (go == null || !go.activeInHierarchy) continue;

                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || !rc.IsEligiblePassAdjuster()) continue;

                Vector2 rp = new Vector2(go.transform.position.x, go.transform.position.y);
                float dist = Vector2.Distance(rp, land);
                if (dist > rc.maxBreakRadius) continue;
                if (!rc.CanReachLanding(dist, ball, true)) continue;

                // Prefer closest to landing; slight bias to whoever is already running a route.
                float score = dist - (rc.isRunningRoute || rc.isAdjustingToBall ? 0.35f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    s_primaryAdjuster = rc;
                }
            }
        }

        static FootballBehavior FindLivePassBall()
        {
            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null)
                {
                    var fb = go.GetComponent<FootballBehavior>();
                    if (fb != null && fb.isInAir && !fb.isCaught)
                        return fb;
                }
            }
            catch { /* tag missing */ }

            var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (fb.isInAir && !fb.isCaught && !fb.isBouncing)
                    return fb;
            }
            return null;
        }

        void ClearPassAdjust()
        {
            isAdjustingToBall = false;
            if (s_primaryAdjuster == this)
                s_primaryAdjuster = null;
        }

        /// <summary>Clear route-break state between plays.</summary>
        public void ResetPassAdjust() => ClearPassAdjust();

        bool IsEligiblePassAdjuster()
        {
            if (hasBall || isPlayerControlled) return false;
            // TE still chipping — don't yank them off the block for the ball.
            var blocker = GetComponent<OffensiveBlocker>();
            if (blocker != null && blocker.IsPassProActive)
                return false;
            return true;
        }

        /// <summary>Brief hop so jump-ball / haul-ins read clearly.</summary>
        public void PlayJumpCatchPose()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null && !jumpPoseActive)
            {
                jumpPoseRestLocal = spriteRenderer.transform.localPosition;
                jumpPoseActive = true;
                spriteRenderer.transform.localPosition = new Vector3(
                    jumpPoseRestLocal.x,
                    jumpPoseRestLocal.y + jumpPoseHeight,
                    jumpPoseRestLocal.z);
            }
            jumpPoseUntil = Time.time + 0.28f;
        }

        void TickJumpCatchPose()
        {
            if (!jumpPoseActive || Time.time < jumpPoseUntil) return;
            jumpPoseActive = false;
            if (spriteRenderer != null)
                spriteRenderer.transform.localPosition = jumpPoseRestLocal;
        }

        /// <summary>Called by the ball when within catch range. Returns true if caught.</summary>
        public bool AttemptCatch(FootballBehavior ball)
        {
            if (hasBall || ball == null || !ball.isInAir || ball.isCaught)
                return false;

            // Normal throw window, or live tipped loft at gotchi height.
            if (!ball.IsInCatchWindow() && !ball.IsInTipCatchWindow())
                return false;

            Vector3 plane = ball.PlayPlanePosition;
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(plane.x, plane.y));

            float reach = ball.IsTippedFlight
                ? (TecmoDive.IsOffenseDiveBusy(transform) ? ball.tipDiveCatchRadius : ball.tipCatchRadius)
                : catchRadius;

            if (distance > reach)
                return false;

            PlayJumpCatchPose();

            // Tip haul-ins that are in the height band are reliable; normal throws keep drop chance.
            bool success = ball.IsTippedFlight
                || distance <= autoCatchRadius
                || Random.value < catchChance;
            PassCollisionGate.NotifyCatchAttemptResolved();
            if (!success)
            {
                Debug.Log($"{gameObject.name} dropped the pass!");
                return false;
            }

            CompleteCatch(ball);
            return true;
        }

        /// <summary>Haul in a tipped ball after diving / reaching the tip landing.</summary>
        public void CompleteTipCatch(FootballBehavior ball)
        {
            if (hasBall || ball == null) return;
            PlayJumpCatchPose();
            PassCollisionGate.NotifyCatchAttemptResolved();
            CompleteCatch(ball);
            PlayBanner.Show("TIP CATCH!", 0.9f, BannerTone.Positive);
            Debug.Log($"{gameObject.name} hauled in the tip");
        }

        void CompleteCatch(FootballBehavior ball)
        {
            ball.Catch(transform);
            MatchPresentation.Catch();
            hasBall = true;
            ballViaHandoff = false;
            isRunningRoute = false;
            ClearPassAdjust();
            velocity = Vector3.zero;

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            isPlayerControlled = playerOffense;
            aiBallCarrier = !playerOffense;

            losYardAtCatch = FieldManager.Instance != null
                ? FieldManager.Instance.currentYardLine
                : 20;

            HandOffControlFromQb();
            if (playerOffense)
                FollowCameraAndRing();

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { /* tag may be missing in rare setups */ }

            // Brief grace so dive/slide can fire before an instant wrap tackle.
            immuneUntil = Time.time + 0.55f;
            diveSlide.Reset();

            Debug.Log($"{gameObject.name} caught the ball — {(playerOffense ? "player" : "AI")} controlled.");
        }

        void HandOffControlFromQb()
        {
            var qb = GameObject.FindGameObjectWithTag("Player");
            if (qb == null) return;

            var pc = qb.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RemoveBall();
                pc.SetControlled(false);
            }

            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc != null)
                qbc.NotifyPassCaught();
        }

        /// <summary>
        /// Player kickoff — freeze the kicker/QB without a catch/handoff identity transfer.
        /// </summary>
        void SilenceKickerControl()
        {
            var qb = GameObject.Find("Quarterback");
            if (qb == null)
            {
                try { qb = GameObject.FindGameObjectWithTag("Player"); }
                catch (UnityException) { }
            }
            if (qb == null) return;

            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc != null)
            {
                qbc.FreezeForKickoff();
                return;
            }

            var pc = qb.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RemoveBall();
                pc.hasBall = false;
                pc.SetControlled(false);
            }
        }

        void FollowCameraAndRing()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null) cc.SetTarget(transform);
            }

            var ring = GameObject.Find("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(transform);
            }
        }

        /// <summary>Watch an AI returner — camera only, never the selection ring.</summary>
        void FollowCameraOnly()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null) cc.SetTarget(transform);
            }

            var ring = GameObject.Find("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(null);
            }
        }

        public void SetPlayerControlled(bool controlled) => isPlayerControlled = controlled;
        public bool IsPlayerControlled => isPlayerControlled;
        /// <summary>Planar move speed for soft-tackle break chance (kinematic ArcadeMove).</summary>
        public float PlanarSpeed => new Vector2(velocity.x, velocity.y).magnitude;

        public void ClearTackleState()
        {
            CancelInvoke(nameof(FinishKickoffTouchback));
            tacklePending = false;
            immuneUntil = 0f;
            ballViaHandoff = false;
            kickoffGunnerControlled = false;
            aiBallCarrier = false;
            kickoffReturnActive = false;
            kickoffKneelPending = false;
            diveSlide.Reset();
        }

        /// <summary>Player-controlled defender initiates a wrap or dive tackle.</summary>
        public void ForceTackleFromDefender(string tacklerName, bool hardHit, Transform tackler = null)
        {
            if (!hasBall) return;
            if (tackler == null && !string.IsNullOrEmpty(tacklerName))
            {
                var go = GameObject.Find(tacklerName);
                if (go != null) tackler = go.transform;
            }

            // Second+ defender during mash lock — stack HP (before tacklePending gate).
            if (TecmoContact.IsActiveForCarrier(transform) && tackler != null)
            {
                TecmoContact.TryJoinTackler(tackler);
                return;
            }

            if (tacklePending) return;
            // Tecmo: contact during dive / get-up is a tackle (not a free safe slide).
            if (diveSlide.IsBusy)
                diveSlide.Reset();
            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive) return;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi) return;
            if (Time.time < immuneUntil) return;
            if (TryFinishTouchdown()) return;

            // Dive / hard hit → instant tackle (Tecmo dive connect).
            if (hardHit)
            {
                tacklePending = true;
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                TecmoContact.ResolveInstantTackle(
                    transform,
                    tackler,
                    onTackled: FinishTackledPlay,
                    onBroken: () =>
                    {
                        tacklePending = false;
                        OnTackleBroken();
                    },
                    hardHit: true);
                // Fumble path clears carrier state itself; ensure pending isn't stuck.
                if (!hasBall)
                    tacklePending = false;
                return;
            }

            // Non-dive touch → Tecmo mash lock (Z to break / wrap).
            if (GameRules.EnableTecmoContact && tackler != null)
            {
                tacklePending = true;
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                if (TecmoContact.Begin(
                        transform,
                        tackler,
                        onBroken: OnTackleBroken,
                        onTackled: FinishTackledPlay))
                    return;
                tacklePending = false;
            }

            if (!GameRules.EnableContactBattle)
            {
                ResolveSoftWrap(tackler, hardHit: false);
                return;
            }

            tacklePending = true;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);

            if (tackler == null)
            {
                tacklePending = false;
                FinishTackledPlay();
                return;
            }

            bool started = ContactBattle.Begin(
                transform,
                tackler,
                onOffenseWon: OnTackleBroken,
                onDefenseWon: FinishTackledPlay);

            if (!started)
            {
                tacklePending = false;
                ResolveSoftWrap(tackler, hardHit: false);
            }
        }

        /// <summary>
        /// Strip the ball — dive hit (forced) or wrap strip (after SoftTackle.RollFumble).
        /// </summary>
        public void ForceFumbleFromDive(string tacklerName, Vector3 knockDir)
        {
            if (!hasBall) return;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay) return;
            // Already across the goal line — score, don't strip.
            if (TryFinishTouchdown()) return;

            int refYard = losYardAtCatch;
            bool wasHandoff = ballViaHandoff;

            // Clear carrier state before the ball goes live.
            hasBall = false;
            isPlayerControlled = false;
            aiBallCarrier = false;
            tacklePending = false;
            isRunningRoute = false;
            ClearPassAdjust();
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);

            RestoreRosterTag();

            FootballBehavior.ForceFumbleAt(
                transform.position,
                knockDir,
                tacklerName,
                refYard,
                wasRunPlay: wasHandoff);

            ballViaHandoff = false;
            Debug.Log($"{gameObject.name} fumbled from dive by {tacklerName}");
        }

        void HandlePlayerInput()
        {
            // Tecmo: Z (A) in the receive endzone = kneel for touchback (not dive).
            if (hasBall
                && !tacklePending
                && kickoffReturnActive
                && !kickoffKneelPending
                && FieldManager.Instance != null
                && FieldManager.Instance.IsInKickReceiveEndzone(transform.position.x)
                && TecmoInput.ADown())
            {
                kickoffKneelPending = true;
                velocity = Vector3.zero;
                PlayBanner.Show("TOUCHBACK", 0.85f, BannerTone.Neutral);
                Invoke(nameof(FinishKickoffTouchback), 0.45f);
                return;
            }

            // Tecmo dive: burst yards → get up (2s) → run again; touch = tackled.
            if (hasBall && !tacklePending)
            {
                if (mainCamera == null)
                    mainCamera = Camera.main;

                bool backOnFeet = diveSlide.Tick(
                    canStart: !diveSlide.IsBusy,
                    currentVelocity: velocity,
                    camera: mainCamera,
                    out Vector3 diveVel);

                if (backOnFeet)
                {
                    velocity = Vector3.zero;
                    immuneUntil = Time.time + 0.2f;
                    return;
                }

                if (diveSlide.IsBusy)
                {
                    velocity = diveVel;
                    if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                        spriteRenderer.flipX = velocity.x < 0f;
                    return;
                }
            }

            var stamina = GetComponent<StaminaSprint>();
            bool boosting = stamina != null && stamina.IsBoosting;
            float speed = boosting ? sprintSpeed : moveSpeed;

            Vector2 input = ArcadeMove.ReadDigitalPlanar();
            if (input.sqrMagnitude < 0.01f)
                velocity = Vector3.zero;
            else
                velocity = new Vector3(input.x, input.y, 0f).normalized * speed;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        /// <summary>During dive / get-up, any defender contact ends the play (Tecmo).</summary>
        void CheckDiveContactTackle()
        {
            if (!hasBall || tacklePending) return;

            if (TryDiveContactNearTag("Defender")) return;

            bool playerKicked = kickoffReturnActive
                                && FieldManager.Instance != null
                                && !FieldManager.Instance.KickoffReceiverIsPlayer;
            if (playerKicked && aiBallCarrier)
            {
                if (TryDiveContactNearTag("Receiver")) return;
                if (TryDiveContactNearTag("Lineman")) return;
                TryDiveContactNearTag("Player");
            }
        }

        bool TryDiveContactNearTag(string tag)
        {
            GameObject[] units;
            try { units = GameObject.FindGameObjectsWithTag(tag); }
            catch { return false; }

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            float radius = tackleRadius * 1.15f;
            foreach (var u in units)
            {
                if (u == null || !u.activeInHierarchy || u == gameObject) continue;
                if (PlayerStun.IsUnitStunned(u)) continue;
                float dist = Vector2.Distance(pos, new Vector2(u.transform.position.x, u.transform.position.y));
                if (dist > radius) continue;

                // NES: diving carrier with +50 HP popcorns through the defender.
                var popcorn = SoftTackle.EvaluatePopcorn(transform, u.transform);
                if (popcorn == SoftTackle.PopcornResult.CarrierPopcornsTackler)
                {
                    SoftTackle.ApplyCarrierPopcorn(transform, u.transform);
                    return true;
                }

                diveSlide.Reset();
                velocity = Vector3.zero;
                ForceTackleFromDefender(u.name, hardHit: true, u.transform);
                return true;
            }
            return false;
        }

        void CheckTackleProximity()
        {
            // Kickoff: tacklers are whoever is NOT carrying.
            // Player kicked + AI returner → offense-tagged gunners tackle.
            // Player kicked + gunner scooped fumble → Defenders tackle.
            bool playerKicked = kickoffReturnActive
                                && FieldManager.Instance != null
                                && !FieldManager.Instance.KickoffReceiverIsPlayer;
            if (playerKicked && aiBallCarrier)
            {
                if (TryTackleNearTag("Receiver")) return;
                if (TryTackleNearTag("Lineman")) return;
                TryTackleNearTag("Player");
                return;
            }

            TryTackleNearTag("Defender");
        }

        bool TryTackleNearTag(string tag)
        {
            GameObject[] units;
            try { units = GameObject.FindGameObjectsWithTag(tag); }
            catch { return false; }

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            foreach (var u in units)
            {
                if (u == null || !u.activeInHierarchy || u == gameObject) continue;
                if (PlayerStun.IsUnitStunned(u)) continue;
                float dist = Vector2.Distance(pos, new Vector2(u.transform.position.x, u.transform.position.y));
                if (dist <= tackleRadius)
                {
                    BeginContactBattle(u.transform);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Offense gunners when the player kicked — chase the AI returner.
        /// Tackle resolve stays on the carrier via <see cref="CheckTackleProximity"/>.
        /// </summary>
        void PursueKickoffReturnerAsCover()
        {
            Transform target = null;
            try
            {
                var bc = GameObject.FindGameObjectWithTag("BallCarrier");
                if (bc != null && bc.activeInHierarchy)
                    target = bc.transform;
            }
            catch (UnityException) { }

            if (target == null)
            {
                var s = GameObject.Find("S");
                if (s != null && s.activeInHierarchy)
                    target = s.transform;
            }

            if (target == null || target == transform)
            {
                velocity = Vector3.zero;
                return;
            }

            if (PlayerStun.IsUnitStunned(this))
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 aim = target.position;
            // Cut off toward the return endzone (opposite kick flight).
            float cut = FieldManager.Instance != null ? FieldManager.Instance.ReturnDirX : -1f;
            aim += Vector3.right * (0.75f * cut);
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            velocity = delta.sqrMagnitude > 0.01f
                ? delta.normalized * (moveSpeed * 1.08f)
                : Vector3.zero;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        /// <summary>Chase the interceptor after a live INT and attempt a soft wrap.</summary>
        void PursueInterceptionReturner()
        {
            if (!InterceptionReturner.TryGetActive(out var ret) || ret == null)
            {
                velocity = Vector3.zero;
                return;
            }

            if (PlayerStun.IsUnitStunned(this))
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 aim = ret.transform.position;
            // Lead slightly toward the return endzone so angles cut off the runner.
            aim += Vector3.right * (ret.TowardLowEndzone ? -0.7f : 0.7f);
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            velocity = delta.sqrMagnitude > 0.01f
                ? delta.normalized * (moveSpeed * 1.05f)
                : Vector3.zero;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(ret.transform.position.x, ret.transform.position.y));
            if (dist <= tackleRadius * 1.05f)
                ret.TryAcceptTackle(transform);
        }

        void OnTriggerEnter(Collider other)
        {
            if (diveSlide.IsDiving || other == null) return;
            if (Time.time < immuneUntil) return;

            // Without the ball: any O↔D touch still battles (blocks / jams).
            if (!hasBall)
            {
                if (other.CompareTag("Defender"))
                    ContactBattle.TryOpposingTouch(transform, other.transform);
                return;
            }

            bool playerKicked = kickoffReturnActive
                                && FieldManager.Instance != null
                                && !FieldManager.Instance.KickoffReceiverIsPlayer;
            bool isTackler;
            if (playerKicked && aiBallCarrier)
            {
                isTackler = other.CompareTag("Receiver")
                            || other.CompareTag("Lineman")
                            || other.CompareTag("Player");
            }
            else
            {
                isTackler = other.CompareTag("Defender");
            }

            if (isTackler)
                BeginContactBattle(other.transform);
        }

        /// <summary>Restore WR tag, or Defender when this is the AI kickoff returner (S).</summary>
        void RestoreRosterTag()
        {
            string tag = IsDefenseHosted ? "Defender" : "Receiver";
            try { gameObject.tag = tag; }
            catch (UnityException) { }
        }

        void BeginContactBattle(Transform tackler)
        {
            if (!hasBall) return;
            if (tackler == null) return;
            if (TecmoContact.IsActiveForCarrier(transform))
            {
                TecmoContact.TryJoinTackler(tackler);
                return;
            }
            if (tacklePending) return;
            if (diveSlide.IsDiving) return;
            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive) return;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi) return;
            if (PassCollisionGate.ShouldBlockBattle(transform, tackler)) return;
            if (Time.time < immuneUntil) return;
            if (TryFinishTouchdown()) return;

            // NES Tecmo: mash Z lock on non-dive contact.
            if (GameRules.EnableTecmoContact)
            {
                tacklePending = true;
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                if (TecmoContact.Begin(
                        transform,
                        tackler,
                        onBroken: OnTackleBroken,
                        onTackled: FinishTackledPlay))
                    return;
                tacklePending = false;
            }

            if (!GameRules.EnableContactBattle)
            {
                ResolveSoftWrap(tackler, hardHit: false);
                return;
            }

            tacklePending = true;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);

            bool started = ContactBattle.Begin(
                transform,
                tackler,
                onOffenseWon: OnTackleBroken,
                onDefenseWon: FinishTackledPlay);

            if (!started)
            {
                tacklePending = false;
                ResolveSoftWrap(tackler, hardHit: false);
            }
        }

        void ResolveSoftWrap(Transform tackler, bool hardHit = false)
        {
            if (!hasBall || tacklePending) return;
            if (diveSlide.IsDiving) return;
            if (Time.time < immuneUntil) return;
            if (TryFinishTouchdown()) return;

            bool playerCarrier = isPlayerControlled && !aiBallCarrier;
            bool tackled = SoftTackle.ResolveWrap(
                transform,
                tackler,
                isPocketSack: false,
                playerCarrier: playerCarrier,
                hardHit: hardHit);

            if (!tackled)
            {
                OnTackleBroken();
                return;
            }

            if (SoftTackle.RollFumble(transform, tackler, hardHit, isPocketSack: false))
            {
                string name = tackler != null ? tackler.name : "DEFENSE";
                ForceFumbleFromDive(name, SoftTackle.FumbleKnockDir(transform, tackler));
                return;
            }

            tacklePending = true;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);
            FinishTackledPlay();
        }

        /// <summary>Offense recovers a live fumble — continue as ball-carrier.</summary>
        public void BeginFumbleRecovery(int refYard, bool wasRun)
        {
            hasBall = true;
            ballViaHandoff = wasRun;
            // Keep KO return direction if the fumble happened on a kickoff return.
            kickoffReturnActive = GameManager.Instance != null
                                  && GameManager.Instance.isKickoffReturn;
            isRunningRoute = false;
            ClearPassAdjust();
            tacklePending = false;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);

            // Kickoff fumble: scoop ownership follows roster host (not who originally received).
            // Player kicked + gunner scoop → player; AI returner scoop → AI.
            bool playerOffense;
            if (kickoffReturnActive)
                playerOffense = !IsDefenseHosted;
            else
                playerOffense = FieldManager.Instance == null
                               || FieldManager.Instance.isPlayerPossession;

            isPlayerControlled = playerOffense;
            aiBallCarrier = !playerOffense;
            kickoffGunnerControlled = false;
            losYardAtCatch = refYard;

            HandOffControlFromQb();
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsKickoffCoverage)
                PlayerDefenseController.Instance.Cancel();

            if (playerOffense)
                FollowCameraAndRing();

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            // Possession follows the scoop on a kickoff fumble.
            if (kickoffReturnActive && FieldManager.Instance != null)
                FieldManager.Instance.isPlayerPossession = playerOffense;

            immuneUntil = Time.time + 0.4f;
            diveSlide.Reset();
            Debug.Log($"{gameObject.name} recovered fumble — {(playerOffense ? "player" : "AI")} controlled");
        }

        void PursueLooseFumble()
        {
            if (!FootballBehavior.TryGetLooseFumble(out var ball) || ball == null)
            {
                velocity = Vector3.zero;
                return;
            }

            if (PlayerStun.IsUnitStunned(this))
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 aim = ball.PlayPlanePosition;
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            // Dive-ish scramble — slightly faster than a normal route run.
            velocity = delta.sqrMagnitude > 0.01f
                ? delta.normalized * (moveSpeed * 1.18f)
                : Vector3.zero;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        void OnTackleBroken()
        {
            tacklePending = false;
            hasBall = true;
            // Keep whoever was controlling the carrier.
            if (!aiBallCarrier)
                isPlayerControlled = true;
            immuneUntil = Time.time + breakImmunity;
            float nudgeDir = 1f;
            if (FieldManager.Instance != null)
                nudgeDir = kickoffReturnActive
                    ? FieldManager.Instance.ReturnDirX
                    : FieldManager.Instance.DriveDirX;
            transform.position += Vector3.right * (0.55f * nudgeDir);
            Debug.Log($"{gameObject.name} broke the tackle — keep running!");
        }

        void FinishTackledPlay()
        {
            string header = kickoffReturnActive
                ? "RETURN"
                : (ballViaHandoff ? "RUN" : "COMPLETE PASS");
            FinishCarrierDown(header);
        }

        /// <summary>Legacy safe-spot dive (kickoff kneel / touchback only).</summary>
        void FinishDivePlay()
        {
            diveSlide.Reset();
            if (kickoffReturnActive
                && FieldManager.Instance != null
                && FieldManager.Instance.IsInKickReceiveEndzone(transform.position.x))
            {
                FinishKickoffTouchback();
                return;
            }

            FinishCarrierDown(kickoffReturnActive ? "RETURN" : "DIVE");
        }

        void FinishCarrierDown(string header)
        {
            if (kickoffReturnActive)
            {
                FinishKickoffReturnDown(header);
                return;
            }

            tacklePending = false;
            hasBall = false;
            isPlayerControlled = false;
            aiBallCarrier = false;
            velocity = Vector3.zero;
            diveSlide.Reset();

            // Drop the stuck ball visual now — next play will park a clean LOS ball.
            DetachHeldBall();

            if (ResolveTouchdownAtSpot())
            {
                ballViaHandoff = false;
                RestoreRosterTag();
                return;
            }

            if (ResolveSafetyAtSpot())
            {
                ballViaHandoff = false;
                RestoreRosterTag();
                return;
            }

            int yardsGained = 0;
            string downLine = "";

            if (FieldManager.Instance != null)
            {
                int endYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                yardsGained = FieldManager.Instance.YardsGained(losYardAtCatch, endYard);
                FieldManager.Instance.AdvanceBall(yardsGained);

                if (FieldManager.Instance.JustConvertedTwoPoint
                    || FieldManager.Instance.JustScoredTouchdown)
                {
                    PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                if (FieldManager.Instance.JustScoredSafety)
                {
                    PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                if (FieldManager.Instance.JustFailedTwoPoint)
                {
                    PlayBanner.ShowPlayOver("2-POINT NO GOOD", BannerTone.Neutral);
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                downLine = FieldManager.Instance.GetPlayResultDownLine();
            }

            string yardsText = yardsGained >= 0 ? $"+{yardsGained} YDS" : $"{yardsGained} YDS";
            PlayBanner.ShowPlayOver($"{header}\n{yardsText}\n{downLine}", BannerTone.Positive);
            Debug.Log($"{header} — {yardsText}, {downLine}");
            ballViaHandoff = false;

            RestoreRosterTag();
        }

        void FinishKickoffReturnDown(string header)
        {
            tacklePending = false;
            hasBall = false;
            isPlayerControlled = false;
            aiBallCarrier = false;
            velocity = Vector3.zero;
            diveSlide.Reset();
            DetachHeldBall();

            if (ResolveKickoffReturnTouchdownAtSpot())
            {
                kickoffReturnActive = false;
                ballViaHandoff = false;
                RestoreRosterTag();
                return;
            }

            // Absolute field yard where the return ended.
            int absEndYard = 25;
            int ownYard = 25;
            int returned = 0;
            if (FieldManager.Instance != null)
            {
                absEndYard = Mathf.Clamp(
                    FieldManager.Instance.WorldXToYard(transform.position.x),
                    1,
                    99);
                // Return yards along ReturnDirX (opposite kick flight).
                int kickDir = FieldManager.NormDir(FieldManager.Instance.kickDirection);
                returned = Mathf.Max(0, (kickoffCatchYard - absEndYard) * kickDir);
                // Keep absolute world spot; set driveDirection = −kickDirection.
                FieldManager.Instance.ResolveKickoffReturnSpot(absEndYard);
                ownYard = FieldManager.Instance.OwnYardLine;
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.isKickoffReturn = false;
            }

            kickoffReturnActive = false;
            ballViaHandoff = false;
            string yardsText = returned > 0 ? $"+{returned} YDS" : "NO GAIN";
            // Banner / HUD use offense-own yard (e.g. own 20), not absolute 80.
            PlayBanner.ShowPlayOver(
                $"{header}\n{yardsText}\nBALL AT THE {ownYard}",
                BannerTone.Positive);
            Debug.Log($"Kickoff return — {yardsText}, abs {absEndYard} → own {ownYard}");

            RestoreRosterTag();
        }

        void FinishKickoffTouchback()
        {
            CancelInvoke(nameof(FinishKickoffTouchback));
            tacklePending = false;
            hasBall = false;
            isPlayerControlled = false;
            aiBallCarrier = false;
            kickoffReturnActive = false;
            kickoffKneelPending = false;
            velocity = Vector3.zero;
            diveSlide.Reset();
            DetachHeldBall();

            int tb = FieldManager.KickoffTouchbackOwnYard;
            if (FieldManager.Instance != null)
                FieldManager.Instance.ResolveKickoff(tb, true);
            else if (GameManager.Instance != null)
                GameManager.Instance.isKickoffReturn = false;

            PlayBanner.ShowPlayOver($"TOUCHBACK\nBALL AT THE {tb}", BannerTone.Neutral);
            RestoreRosterTag();
        }

        void FinishOutOfBounds()
        {
            if (!hasBall) return;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return;

            if (FieldManager.Instance != null)
                transform.position = FieldManager.Instance.ClampInBounds(transform.position);

            if (kickoffReturnActive)
            {
                FinishKickoffReturnDown("RETURN — OUT OF BOUNDS");
                return;
            }

            hasBall = false;
            isPlayerControlled = false;
            velocity = Vector3.zero;
            tacklePending = false;
            DetachHeldBall();

            if (ResolveTouchdownAtSpot())
            {
                ballViaHandoff = false;
                RestoreRosterTag();
                return;
            }

            if (ResolveSafetyAtSpot())
            {
                ballViaHandoff = false;
                RestoreRosterTag();
                return;
            }

            int yardsGained = 0;
            string downLine = "";

            if (FieldManager.Instance != null)
            {
                int endYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                yardsGained = FieldManager.Instance.YardsGained(losYardAtCatch, endYard);
                FieldManager.Instance.AdvanceBall(yardsGained);

                if (FieldManager.Instance.JustConvertedTwoPoint
                    || FieldManager.Instance.JustScoredTouchdown)
                {
                    PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                if (FieldManager.Instance.JustScoredSafety)
                {
                    PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                if (FieldManager.Instance.JustFailedTwoPoint)
                {
                    PlayBanner.ShowPlayOver("2-POINT NO GOOD", BannerTone.Neutral);
                    ballViaHandoff = false;
                    RestoreRosterTag();
                    return;
                }

                downLine = FieldManager.Instance.GetPlayResultDownLine();
            }

            string yardsText = yardsGained >= 0 ? $"+{yardsGained} YDS" : $"{yardsGained} YDS";
            string header = ballViaHandoff ? "RUN — OUT OF BOUNDS" : "OUT OF BOUNDS";
            PlayBanner.ShowPlayOver($"{header}\n{yardsText}\n{downLine}", BannerTone.Positive);
            Debug.Log($"Receiver out of bounds — {yardsText}, {downLine}");
            ballViaHandoff = false;

            RestoreRosterTag();
        }

        /// <summary>Live endzone check while the carrier is still running.</summary>
        bool TryFinishTouchdown()
        {
            if (!hasBall) return false;
            if (FieldManager.Instance == null) return false;

            bool scored;
            if (kickoffReturnActive)
            {
                int returnDir = FieldManager.Instance.kickDirection > 0 ? -1 : 1;
                scored = FieldManager.Instance.IsInKickReturnScoringEndzone(
                    FieldManager.Instance.ScoringPlaneWorldX(transform, returnDir));
            }
            else
            {
                scored = FieldManager.Instance.IsCarrierInScoringEndzone(transform);
            }
            if (!scored) return false;

            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive)
                TackleBattle.Instance.CancelForScore();
            if (ContactBattle.Instance != null && ContactBattle.Instance.IsActive)
                ContactBattle.Instance.ForceCancel();
            if (TecmoContact.Instance != null && TecmoContact.Instance.IsActive)
                TecmoContact.Instance.ForceCancel();

            tacklePending = false;
            hasBall = false;
            isPlayerControlled = false;
            aiBallCarrier = false;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);
            DetachHeldBall();

            bool awarded;
            if (kickoffReturnActive)
            {
                int returnDir = FieldManager.Instance.kickDirection > 0 ? -1 : 1;
                float planeX = FieldManager.Instance.ScoringPlaneWorldX(transform, returnDir);
                awarded = FieldManager.Instance.TryScoreKickReturnTouchdown(planeX)
                          || FieldManager.Instance.JustScoredTouchdown;
                if (awarded)
                {
                    PlayBanner.ShowPlayOver("TOUCHDOWN\nKICKOFF RETURN", BannerTone.Positive);
                    Debug.Log($"{gameObject.name} kickoff return touchdown");
                }
            }
            else
            {
                // Plane already validated — award without re-checking pivot/spot.
                awarded = FieldManager.Instance.AwardTouchdown()
                          || FieldManager.Instance.JustScoredTouchdown;
                if (awarded)
                {
                    PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                    Debug.Log($"{gameObject.name} scored a touchdown");
                }
            }

            kickoffReturnActive = false;
            ballViaHandoff = false;

            RestoreRosterTag();
            return awarded || FieldManager.Instance.JustScoredTouchdown;
        }

        /// <summary>Award TD + banner if the spot is in the scoring endzone.</summary>
        bool ResolveTouchdownAtSpot()
        {
            if (FieldManager.Instance == null) return false;
            if (!FieldManager.Instance.TryScoreTouchdown(transform)
                && !FieldManager.Instance.JustScoredTouchdown)
                return false;

            PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
            Debug.Log($"{gameObject.name} scored a touchdown");
            return true;
        }

        /// <summary>Award safety + banner if the offense is downed in their own endzone.</summary>
        bool ResolveSafetyAtSpot()
        {
            if (FieldManager.Instance == null) return false;
            if (!FieldManager.Instance.TryScoreSafety(transform.position.x)
                && !FieldManager.Instance.JustScoredSafety)
                return false;

            PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
            Debug.Log($"{gameObject.name} — SAFETY");
            return true;
        }

        bool ResolveKickoffReturnTouchdownAtSpot()
        {
            if (FieldManager.Instance == null) return false;
            int returnDir = FieldManager.Instance.kickDirection > 0 ? -1 : 1;
            float planeX = FieldManager.Instance.ScoringPlaneWorldX(transform, returnDir);
            if (!FieldManager.Instance.TryScoreKickReturnTouchdown(planeX)
                && !FieldManager.Instance.JustScoredTouchdown)
                return false;

            kickoffReturnActive = false;
            PlayBanner.ShowPlayOver("TOUCHDOWN\nKICKOFF RETURN", BannerTone.Positive);
            Debug.Log($"{gameObject.name} kickoff return touchdown");
            return true;
        }

        bool IsOutOfBounds()
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.IsOutOfBounds(transform.position);
            return Mathf.Abs(transform.position.y) >= RetroLookApplier.SidelineY;
        }

        void DetachHeldBall()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var fb = child.GetComponent<FootballBehavior>();
                bool isBall = child.CompareTag("Football")
                              || fb != null
                              || child.name.Contains("Football");
                if (!isBall) continue;

                // Unparent + leave at the tackle / down spot for the result banner.
                // Do not teleport to LOS here — AdvanceBall used to yank ballTransform
                // via UpdateBallPosition; that is now gated to pre-snap only.
                child.SetParent(null, true);
                if (fb != null)
                {
                    Vector3 spot = transform.position;
                    spot.z = 0f;
                    fb.ResetToParked(spot);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (routePoints != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < routePoints.Length; i++)
                {
                    Gizmos.DrawSphere(routePoints[i], 0.3f);
                    if (i > 0)
                        Gizmos.DrawLine(routePoints[i - 1], routePoints[i]);
                }
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, catchRadius);
        }
    }
}
