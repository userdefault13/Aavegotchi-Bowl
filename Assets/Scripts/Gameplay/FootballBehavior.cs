using System;
using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// 2D football with lofted flight. Play-plane XY is the true path;
    /// flight height is separate so balls can clear the DL mid-air.
    /// Held stick runs after GotchiFacingView so the ball tracks the hand wearable slot.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class FootballBehavior : MonoBehaviour
    {
        [Header("Physics")]
        public float throwSpeed = 18f;
        public float arcHeightFactor = 0.28f;
        public float minArcHeight = 1.8f;
        public float maxArcHeight = 11f;

        [Header("Catching / Contests")]
        public float catchCheckRadius = 1.15f;
        /// <summary>Receivers can jump this high to haul it in.</summary>
        public float receiverCatchHeight = 1.25f;
        /// <summary>Only catchable once flight progress reaches this (descending into the spot).</summary>
        [Range(0.4f, 0.9f)] public float minCatchFlightT = 0.68f;
        /// <summary>Ball play-plane must be within this of the aimed landing to be catchable.</summary>
        public float landingWindowRadius = 1.6f;
        /// <summary>DL tip / bat-down ceiling — higher and the ball sails over.</summary>
        public float dlContestHeight = 0.7f;
        /// <summary>DB / LB jump-ball / INT ceiling.</summary>
        public float dbContestHeight = 1.25f;
        /// <summary>Defender must be this close (play-plane) to tip / INT.</summary>
        public float defenderContestRadius = 1.25f;
        /// <summary>Brief hold on INT before cleanup so the pick is readable.</summary>
        public float interceptionHoldSeconds = 0.9f;

        [Header("Tip / INT odds (Retro-style readability)")]
        [Tooltip("Alone in the window: chance the pass is batted incomplete.")]
        [Range(0f, 1f)] public float tipChanceAlone = 0.42f;
        [Tooltip("Alone in the window: chance of interception (remainder can whiff).")]
        [Range(0f, 1f)] public float intChanceAlone = 0.38f;
        [Tooltip("WR + DB jump ball: tip / bat-down.")]
        [Range(0f, 1f)] public float contestedTipChance = 0.28f;
        [Tooltip("WR + DB jump ball: interception.")]
        [Range(0f, 1f)] public float contestedIntChance = 0.22f;
        [Tooltip("DL may tip slightly earlier than the catch window.")]
        [Range(0.2f, 0.8f)] public float dlTipMinFlightT = 0.42f;

        [Header("Tipped Ball")]
        [Tooltip("How far the tip redirects the ball before it can be caught or fall incomplete.")]
        public float tipRedirectYards = 5.5f;
        public float tipFlightSpeed = 9.5f;
        public float tipPeakHeight = 1.35f;
        [Tooltip("Lowest loft that still counts as gotchi-height catchable.")]
        public float tipCatchMinHeight = 0.05f;
        [Tooltip("Highest loft a gotchi can haul in (body / jump band).")]
        public float tipCatchMaxHeight = 1.25f;
        public float tipCatchRadius = 1.35f;
        [Tooltip("Extra planar reach while diving at the tip landing spot.")]
        public float tipDiveCatchRadius = 2.15f;
        public float tipLandingBonusRadius = 0.55f;

        [Header("Elevation Scale")]
        [Tooltip("Peak size multiplier mid-ascent (1 = no pop). Back to 1× by loft peak.")]
        [Range(1f, 2.5f)] public float maxElevationScale = 1.85f;
        [Tooltip("Unused for passes — ascent pop uses flight progress. Kept for bounce hops.")]
        public float elevationScaleRefHeight = 0f;

        [Header("Incomplete / Fumble Bounce")]
        [Tooltip("Initial planar skitter speed after the ball hits the ground.")]
        public float bounceSlideSpeed = 4.2f;
        [Tooltip("Initial upward hop speed (visual loft).")]
        public float bounceHopSpeed = 3.6f;
        public float bounceHopGravity = 22f;
        [Range(0.15f, 0.85f)] public float bounceHopRestitution = 0.4f;
        [Range(0.45f, 0.92f)] public float bounceGroundFriction = 0.7f;
        public int bounceMaxHops = 4;
        public float bounceSettleSpeed = 0.45f;
        [Tooltip("Safety timeout if hops never settle.")]
        public float bounceDuration = 2.2f;
        [Tooltip("Z-spin on the Visual only (degrees/sec at full slide speed).")]
        public float bounceSpinDegrees = 260f;
        // Legacy inspector fields — kept so old scenes don't lose serialized data.
        public float bounceHopHeight = 1.0f;
        public float bounceHopFrequency = 2.0f;

        [Header("Fumble")]
        public float fumbleBounceDuration = 1.85f;
        public float fumbleSlideSpeed = 4.8f;
        public float fumbleRecoverAfter = 0.28f;
        public float fumbleRecoverRadius = 1.55f;

        [Header("Kickoff Live Bounce")]
        [Tooltip("Initial planar speed after first ground contact.")]
        public float kickoffSlideSpeed = 7.5f;
        [Tooltip("Upward speed of the first hop (world Y on the visual).")]
        public float kickoffHopSpeed = 5.2f;
        public float kickoffHopGravity = 18f;
        [Range(0.2f, 0.9f)] public float kickoffHopRestitution = 0.55f;
        [Range(0.5f, 0.95f)] public float kickoffGroundFriction = 0.72f;
        public int kickoffMaxHops = 5;
        public float kickoffSettleSpeed = 0.55f;
        public float kickoffRecoverAfter = 0.22f;
        public float kickoffRecoverRadius = 1.55f;
        [Tooltip("Legacy / incomplete-pass hop scale (kickoff uses hop velocity).")]
        public float kickoffBounceHopHeight = 1.15f;
        public float kickoffBounceDuration = 2.4f;

        [Header("Kick Flight")]
        [Tooltip("End-over-end tumble on Visual during kick loft (degrees/sec).")]
        public float kickSpinDegrees = 720f;

        [Header("State")]
        public bool isInAir;
        public bool isCaught;
        public bool isBouncing;
        /// <summary>Live loose ball after a dive strip — recoverable by either side.</summary>
        public bool isFumbleLoose;
        /// <summary>Live kickoff bounce / grounded ball — returner must recover (not fumble).</summary>
        public bool isKickoffLoose;

        /// <summary>Field path without loft (X downfield, Y across).</summary>
        public Vector3 PlayPlanePosition { get; private set; }
        /// <summary>Current loft altitude (0 on hand / at landing, peak mid-flight).</summary>
        public float FlightHeight { get; private set; }
        /// <summary>0→1 progress along the throw.</summary>
        public float FlightT { get; private set; }
        /// <summary>Aimed play-plane landing spot for the current throw.</summary>
        public Vector3 ThrowTarget => throwTarget;
        /// <summary>Play-plane origin of the current throw.</summary>
        public Vector3 ThrowStart => throwStart;
        /// <summary>Total flight time for the current throw (seconds).</summary>
        public float FlightDuration => flightDuration;
        /// <summary>Seconds left until the ball reaches ThrowTarget.</summary>
        public float RemainingFlightTime
            => Mathf.Max(0f, flightDuration - flightElapsed);

        Rigidbody rb;
        bool hasBeenThrown;
        Vector3 throwStart;
        Vector3 throwTarget;
        Vector3 bounceOrigin;
        Vector3 bounceDir;
        float flightDuration;
        float flightElapsed;
        float bounceElapsed;
        float flightPeak;
        float activeBounceDuration;
        float activeBounceSlide;
        /// <summary>Planar velocity during kickoff live bounce (X downfield, Y across).</summary>
        Vector3 kickoffBounceVel;
        float kickoffHopVelY;
        int kickoffHopCount;
        bool kickoffBouncePhysics;
        /// <summary>Incomplete / fumble skitter (same model as kickoff, clamped to field).</summary>
        Vector3 groundBounceVel;
        float groundHopVelY;
        int groundHopCount;
        Collider ballCollider;
        Transform carrier;
        Transform visual;

        string fumbleCausedBy;
        int fumbleRefYard;
        bool fumbleWasRun;
        bool fumbleResolved;
        bool offenseHadPossessionAtFumble;

        /// <summary>Once a catch/drop/tip/INT resolves, no more contest rolls this throw.</summary>
        bool outcomeLocked;

        /// <summary>Kickoff / special-teams loft — no pass contests, lands via callback.</summary>
        bool isSpecialTeamsKick;
        Action onKickLanded;

        /// <summary>Live tipped loft — catchable until it hits the tip landing spot.</summary>
        bool isTippedFlight;
        string tipIncompleteHeadline = "TIPPED PASS";
        ThrowingArc tipArc;

        /// <summary>True while a kickoff (or similar) ball is lofted without pass logic.</summary>
        public bool IsSpecialTeamsKick => isSpecialTeamsKick;
        /// <summary>True while a tipped ball is still in the air toward its tip landing.</summary>
        public bool IsTippedFlight => isTippedFlight;

        void Awake()
        {
            EnsureRigidbody();
            ballCollider = GetComponent<Collider>();
            if (ballCollider == null)
            {
                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = FormationRoster.BallCatchRadius;
                ballCollider = sphere;
            }
            else
            {
                ballCollider.isTrigger = true;
            }

            if (!isInAir)
                Park();
        }

        void EnsureRigidbody()
        {
            if (rb != null) return;
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        }

        void Park()
        {
            isInAir = false;
            isBouncing = false;
            isFumbleLoose = false;
            isKickoffLoose = false;
            fumbleResolved = false;
            hasBeenThrown = false;
            isSpecialTeamsKick = false;
            onKickLanded = null;
            isTippedFlight = false;
            HideTipArc();
            FlightHeight = 0f;
            PlayPlanePosition = transform.position;
            ResetBallSpin();
            EnsureRigidbody();
            // Zero while non-kinematic only — setting velocities on kinematic RBs spams warnings.
            rb.isKinematic = false;
            ClearRigidbodyMotion();
            rb.isKinematic = true;
        }

        void ClearRigidbodyMotion()
        {
            if (rb == null || rb.isKinematic) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        void EnsureVisual()
        {
            if (visual != null)
            {
                SuppressMeshRenderers();
                return;
            }
            visual = transform.Find("Visual");
            if (visual == null)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(transform, false);
                visual = v.transform;
            }
            SuppressMeshRenderers();
        }

        /// <summary>Prefab/scene balls ship a Unity sphere — never let it draw over the sprite.</summary>
        void SuppressMeshRenderers()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r != null) r.enabled = false;
            }
        }

        void OnEnable()
        {
            SuppressMeshRenderers();
        }

        public void ThrowToTarget(Vector3 target)
            => ThrowToTarget(target, bullet: false);

        /// <param name="bullet">Two-finger / dual-pointer pass — flatter loft, higher speed.</param>
        public void ThrowToTarget(Vector3 target, bool bullet)
        {
            EnsureRigidbody();
            EnsureVisual();
            MatchPresentation.Throw();

            hasBeenThrown = true;
            isInAir = true;
            isCaught = false;
            isBouncing = false;
            isSpecialTeamsKick = false;
            isTippedFlight = false;
            onKickLanded = null;
            outcomeLocked = false;
            HideTipArc();
            carrier = null;
            transform.SetParent(null, true);

            throwStart = new Vector3(transform.position.x, transform.position.y, 0f);
            if (visual != null) visual.localPosition = Vector3.zero;

            throwTarget = new Vector3(target.x, target.y, 0f);
            WeatherSystem.EnsureExists();
            if (WeatherSystem.Instance != null)
                throwTarget = WeatherSystem.Instance.ApplyThrowDrift(throwTarget);
            PlayPlanePosition = throwStart;
            FlightHeight = 0f;
            FlightT = 0f;
            ResetElevationScale();

            float distance = Mathf.Max(0.01f, Vector3.Distance(throwStart, throwTarget));
            float arcMul = bullet ? 0.32f : 1f;
            float speedMul = bullet ? 1.65f : 1f;
            float minH = bullet ? minArcHeight * 0.35f : minArcHeight;
            float maxH = bullet ? Mathf.Max(minH, maxArcHeight * 0.38f) : maxArcHeight;
            flightPeak = Mathf.Clamp(distance * arcHeightFactor * arcMul, minH, maxH);
            flightDuration = distance / Mathf.Max(0.01f, throwSpeed * speedMul);
            flightElapsed = 0f;

            // Collider stays on the play-plane root so loft can't phantom-hit receivers.
            if (ballCollider != null)
            {
                ballCollider.enabled = true;
                if (ballCollider is SphereCollider sphere)
                    sphere.radius = FormationRoster.BallCatchRadius;
            }

            rb.isKinematic = true;
            rb.detectCollisions = true;
            rb.useGravity = false;

            FollowCameraToBall();
        }

        /// <summary>
        /// Lofted kickoff / special-teams flight. No catch contests; invokes
        /// <paramref name="onLanded"/> when the ball reaches the landing spot.
        /// </summary>
        public void KickToTarget(Vector3 target, float speed, Action onLanded = null)
        {
            // Kickoff loft must always run — reactivate if a restyle/dedup hid the tee ball.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            enabled = true;

            EnsureRigidbody();
            EnsureVisual();
            CancelInvoke();

            hasBeenThrown = true;
            isInAir = true;
            isCaught = false;
            isBouncing = false;
            isFumbleLoose = false;
            isKickoffLoose = false;
            isSpecialTeamsKick = true;
            onKickLanded = onLanded;
            outcomeLocked = true;
            carrier = null;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            throwStart = new Vector3(transform.position.x, transform.position.y, 0f);
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
            }
            transform.rotation = Quaternion.identity;

            throwTarget = new Vector3(target.x, target.y, 0f);
            // Degenerate aim (same as tee) — nudge downfield so flight cannot be zero-length.
            if (Vector2.Distance(
                    new Vector2(throwStart.x, throwStart.y),
                    new Vector2(throwTarget.x, throwTarget.y)) < 0.5f)
            {
                float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                throwTarget = throwStart + Vector3.right * (20f * dir);
            }

            PlayPlanePosition = throwStart;
            FlightHeight = 0f;
            FlightT = 0f;

            float distance = Mathf.Max(0.01f, Vector3.Distance(throwStart, throwTarget));
            // Slightly higher arc than a pass so deep kickoffs read as kicks.
            flightPeak = Mathf.Clamp(distance * arcHeightFactor * 1.2f, minArcHeight * 1.25f, maxArcHeight);
            flightDuration = distance / Mathf.Max(8f, speed);
            flightElapsed = 0f;

            if (ballCollider != null)
            {
                ballCollider.isTrigger = true;
                ballCollider.enabled = false;
            }

            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.useGravity = false;
            rb.position = throwStart;

            FollowCameraToBall();
        }

        /// <summary>Abort an in-flight special-teams kick without invoking the land callback.</summary>
        public void CancelSpecialTeamsKick()
        {
            if (!isSpecialTeamsKick && !isKickoffLoose) return;
            onKickLanded = null;
            isSpecialTeamsKick = false;
            isKickoffLoose = false;
            isBouncing = false;
            kickoffBouncePhysics = false;
            kickoffHopVelY = 0f;
            kickoffBounceVel = Vector3.zero;
            ResetToParked(PlayPlanePosition.sqrMagnitude > 0.01f ? PlayPlanePosition : transform.position);
        }

        void FinishSpecialTeamsKick()
        {
            var cb = onKickLanded;
            onKickLanded = null;
            isSpecialTeamsKick = false;

            FlightHeight = 0f;
            FlightT = 1f;
            PlayPlanePosition = throwTarget;
            transform.position = throwTarget;
            EnsureVisual();
            if (visual != null) visual.localPosition = Vector3.zero;
            ResetBallSpin();

            isInAir = false;
            if (ballCollider != null)
                ballCollider.enabled = true;

            // Kickoff land callback starts live bounce — don't Park() first
            // (Park clears hasBeenThrown / bounce flags and can kill the hop).
            if (cb != null)
            {
                hasBeenThrown = true;
                cb.Invoke();
                return;
            }

            hasBeenThrown = false;
            Park();
        }

        /// <summary>
        /// Mid-air kickoff haul by the designated returner (descending window only).
        /// Does not use pass contest logic.
        /// </summary>
        public bool TryCatchKickoffInAir(Transform returner, float radius, float maxHeight)
        {
            if (!isSpecialTeamsKick || !isInAir || isCaught || returner == null)
                return false;
            if (FlightT < minCatchFlightT) return false;
            if (FlightHeight > Mathf.Max(0.4f, maxHeight)) return false;
            if (FieldManager.Instance != null
                && FieldManager.Instance.IsKickoffLooseTouchback(PlayPlanePosition))
                return false;

            float d = Vector2.Distance(
                new Vector2(PlayPlanePosition.x, PlayPlanePosition.y),
                new Vector2(returner.position.x, returner.position.y));
            if (d > Mathf.Max(0.4f, radius)) return false;

            onKickLanded = null;
            isSpecialTeamsKick = false;
            isKickoffLoose = false;
            Catch(returner);
            MatchPresentation.Catch();
            return true;
        }

        /// <summary>
        /// Kickoff hits the ground — live hop / skitter (not an auto-catch / dead ball).
        /// Returner must recover; OOB / out the back of the EZ → touchback.
        /// </summary>
        public void BeginKickoffLiveBounce(Vector3 origin, Vector3 knockDir)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            enabled = true;

            CancelInvoke();
            carrier = null;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            ResetBallSpin();

            // Do NOT clamp sidelines — kickoff must be able to bounce OOB for touchback.
            origin.z = 0f;
            transform.position = origin;
            PlayPlanePosition = origin;

            float kd = FieldManager.Instance != null ? FieldManager.Instance.KickDirX : 1f;
            knockDir.z = 0f;
            if (knockDir.sqrMagnitude < 0.01f)
                knockDir = Vector3.right * kd;
            knockDir.Normalize();

            // Prefer downfield (kick dir) with a readable lateral scatter.
            Vector3 planar = (knockDir * 1.35f
                              + Vector3.right * (kd * 0.55f)
                              + Vector3.up * UnityEngine.Random.Range(-0.65f, 0.65f)).normalized;
            bounceDir = planar;
            kickoffBounceVel = planar * Mathf.Max(4f, kickoffSlideSpeed);
            kickoffHopVelY = Mathf.Max(3.5f, kickoffHopSpeed);
            kickoffHopCount = 0;
            kickoffBouncePhysics = true;

            isCaught = false;
            isInAir = false;
            isBouncing = true;
            isFumbleLoose = false;
            isKickoffLoose = true;
            isSpecialTeamsKick = false;
            onKickLanded = null;
            fumbleResolved = false;
            hasBeenThrown = true;
            outcomeLocked = true;

            bounceOrigin = origin;
            bounceElapsed = 0f;
            activeBounceDuration = kickoffBounceDuration;
            activeBounceSlide = kickoffSlideSpeed;
            FlightHeight = 0.05f;

            EnsureVisual();
            if (visual != null)
                visual.localPosition = new Vector3(0f, FlightHeight, 0f);
            ApplyElevationScale();

            EnsureRigidbody();
            rb.isKinematic = true;
            rb.detectCollisions = true;
            if (ballCollider != null)
            {
                ballCollider.enabled = true;
                if (ballCollider is SphereCollider sphere)
                    sphere.radius = FormationRoster.BallCatchRadius;
            }

            FollowCameraToBall();
        }

        /// <summary>Live kickoff ball on the ground / skittering — returner race.</summary>
        public static bool TryGetLooseKickoff(out FootballBehavior ball)
        {
            ball = null;
            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (!fb.isKickoffLoose) continue;
                ball = fb;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Scoop / recover a live kickoff ball. Only the designated returner should call this.
        /// </summary>
        public bool TryPickupKickoff(Transform returner, float radius)
        {
            if (!isKickoffLoose || isCaught || returner == null) return false;
            if (isBouncing && bounceElapsed < kickoffRecoverAfter) return false;
            // Dead ball once it clears the end line / EZ sideline.
            if (FieldManager.Instance != null
                && FieldManager.Instance.IsKickoffLooseTouchback(PlayPlanePosition))
                return false;

            float d = Vector2.Distance(
                new Vector2(PlayPlanePosition.x, PlayPlanePosition.y),
                new Vector2(returner.position.x, returner.position.y));
            if (d > Mathf.Max(0.4f, radius)) return false;

            isKickoffLoose = false;
            isBouncing = false;
            kickoffBouncePhysics = false;
            Catch(returner);
            MatchPresentation.Catch();
            return true;
        }

        /// <summary>True when the kickoff bounce has finished skittering (ball may still be loose on the ground).</summary>
        public bool KickoffBounceSettled
            => isKickoffLoose && !isBouncing;

        void Update()
        {
            if (isCaught && !isFumbleLoose && !isKickoffLoose) return;
            if (!hasBeenThrown && !isFumbleLoose && !isKickoffLoose) return;

            if (isBouncing)
            {
                UpdateBounce();
                return;
            }

            if (!isInAir) return;

            flightElapsed += Time.deltaTime;
            FlightT = Mathf.Clamp01(flightElapsed / Mathf.Max(0.01f, flightDuration));

            // Root stays on the true field path (accurate catch / INT checks).
            PlayPlanePosition = Vector3.Lerp(throwStart, throwTarget, FlightT);
            PlayPlanePosition = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            FlightHeight = EvaluateLoft(FlightT, flightPeak);
            transform.position = PlayPlanePosition;

            EnsureVisual();
            if (visual != null)
                visual.localPosition = new Vector3(0f, FlightHeight, 0f);
            ApplyElevationScale();
            ApplyKickFlightSpin();

            if (isSpecialTeamsKick)
            {
                if (FlightT >= 1f)
                    FinishSpecialTeamsKick();
                return;
            }

            if (isTippedFlight)
            {
                RefreshTipArc();
                if (TryCatchTipped())
                    return;
                if (FlightT >= 1f)
                    FinishTippedFlightIncomplete();
                return;
            }

            if (TryCatchNearby())
                return;

            if (FlightT >= 1f)
                BeginIncomplete();
        }

        /// <summary>
        /// Perspective pop for lofted balls.
        /// Passes: grow on ascent, back to original size at peak, stay base on descent.
        /// Kickoffs / hops: grow with altitude (max near peak height).
        /// </summary>
        void ApplyElevationScale()
        {
            EnsureVisual();
            if (visual == null) return;

            float baseS = FormationRoster.BallScale;
            if (maxElevationScale <= 1.001f)
            {
                visual.localScale = Vector3.one * baseS;
                return;
            }

            float pop = 0f;
            if (isSpecialTeamsKick && isInAir)
            {
                // Kickoff: scale with loft height (reads biggest near apex).
                float refH = elevationScaleRefHeight > 0.01f
                    ? elevationScaleRefHeight
                    : Mathf.Max(0.35f, flightPeak > 0.01f ? flightPeak : bounceHopHeight);
                float t = Mathf.Clamp01(FlightHeight / refH);
                pop = t * t * (3f - 2f * t);
            }
            else if (isInAir && !isBouncing && FlightT < 0.5f)
            {
                // Pass: 0 at release → max mid-ascent → 0 at peak altitude.
                float ascent = FlightT / 0.5f;
                pop = Mathf.Sin(ascent * Mathf.PI);
            }
            else if (isBouncing && FlightHeight > 0.001f)
            {
                float refH = elevationScaleRefHeight > 0.01f
                    ? elevationScaleRefHeight
                    : Mathf.Max(0.35f, bounceHopHeight);
                float t = Mathf.Clamp01(FlightHeight / refH);
                pop = t * t * (3f - 2f * t);
            }

            float mul = Mathf.Lerp(1f, maxElevationScale, pop);
            visual.localScale = Vector3.one * (baseS * mul);
        }

        /// <summary>
        /// End-over-end tumble while a kick is lofted (FG / XP / punt / kickoff).
        /// Spin direction follows downfield travel so it reads as tumbling forward.
        /// </summary>
        void ApplyKickFlightSpin()
        {
            if (!isSpecialTeamsKick || !isInAir) return;
            EnsureVisual();
            if (visual == null || Mathf.Abs(kickSpinDegrees) < 1f) return;

            float dirX = throwTarget.x - throwStart.x;
            float dir = dirX >= 0f ? 1f : -1f;
            visual.Rotate(0f, 0f, -dir * kickSpinDegrees * Time.deltaTime);
        }

        void ResetBallSpin()
        {
            transform.rotation = Quaternion.identity;
            EnsureVisual();
            if (visual != null)
                visual.localRotation = Quaternion.identity;
        }

        void ResetElevationScale()
        {
            EnsureVisual();
            if (visual != null)
                visual.localScale = Vector3.one * FormationRoster.BallScale;
        }

        /// <summary>
        /// Loft curve that rises quickly off the hand (clears DL) then settles into the receiver.
        /// </summary>
        public static float EvaluateLoft(float t, float peak)
        {
            t = Mathf.Clamp01(t);
            // Asymmetric: faster rise, gentler fall — still 0 at ends.
            float rise = Mathf.Sin(t * Mathf.PI);
            // Pull early height up so LOS crossing is already elevated.
            float earlyBoost = 1f + 0.35f * (1f - t);
            return rise * peak * earlyBoost / 1.35f;
        }

        public static float PeakForDistance(float distance, float factor = 0.28f, float minH = 1.8f, float maxH = 11f)
            => Mathf.Clamp(distance * factor, minH, maxH);

        void LateUpdate()
        {
            // Prefab sphere must never reappear over the Retro sprite.
            SuppressMeshRenderers();

            if (!isCaught || carrier == null) return;
            StickToCarrier();
        }

        void StickToCarrier()
        {
            if (carrier == null) return;
            Vector3 pos = carrier.position + HandCarryOffset(carrier);
            pos.z = 0f;
            FlightHeight = 0f;
            PlayPlanePosition = pos;
            transform.SetPositionAndRotation(pos, Quaternion.identity);
            // Keep local pose stable while parented (world stick above).
            if (transform.parent == carrier)
                transform.localPosition = carrier.InverseTransformPoint(pos);

            EnsureVisual();
            if (visual != null) visual.localPosition = Vector3.zero;
            ResetElevationScale();
            LayerHeldBallOverCarrier();

            if (rb != null)
            {
                if (!rb.isKinematic)
                    ClearRigidbodyMotion();
                rb.isKinematic = true;
            }
        }

        /// <summary>
        /// Draw the held ball like a hand wearable — above the gotchi body sprite.
        /// </summary>
        void LayerHeldBallOverCarrier()
        {
            EnsureVisual();
            if (visual == null || carrier == null) return;
            var ballSr = visual.GetComponent<SpriteRenderer>();
            if (ballSr == null) return;

            int order = 92;
            var gotchiSr = carrier.GetComponentInChildren<GotchiFacingView>()?.GetComponent<SpriteRenderer>();
            if (gotchiSr == null)
                gotchiSr = carrier.GetComponentInChildren<SpriteRenderer>();
            if (gotchiSr != null)
                order = Mathf.Clamp(gotchiSr.sortingOrder + 3, 20, 99);

            ballSr.sortingOrder = order;
        }

        // Unscaled offsets in sprite space (64px canvas @ 64 PPU → 1 unit = full sprite).
        // Palm of on-chain hands-right / hands-left wearables (body-adjacent, mid-torso).
        const float HandSidePalmX = 0.11f;
        const float HandSidePalmY = -0.125f;
        // Front/back: character's right-hand slot (front = screen left, back = screen right).
        const float HandFrontPalmX = 0.20f;
        const float HandFrontPalmY = -0.14f;

        /// <summary>
        /// Offset from carrier root into the gotchi hand wearable slot for the
        /// current facing (scales with Visual / PlayerScale).
        /// </summary>
        public static Vector3 HandCarryOffset(Transform carrier)
        {
            float s = Mathf.Max(0.35f, FormationRoster.PlayerScale);
            if (carrier != null)
            {
                var vis = carrier.Find("Visual");
                if (vis != null)
                    s = Mathf.Max(0.35f, Mathf.Abs(vis.lossyScale.x));
            }

            GotchiFacing facing = ResolveCarrierFacing(carrier);
            Vector2 slot = HandSlotForFacing(facing);
            return new Vector3(slot.x * s, slot.y * s, -0.02f);
        }

        static GotchiFacing ResolveCarrierFacing(Transform carrier)
        {
            if (carrier != null)
            {
                var view = carrier.GetComponentInChildren<GotchiFacingView>();
                if (view != null)
                    return view.Facing;
            }

            float dirX = 1f;
            if (FieldManager.Instance != null)
                dirX = FieldManager.Instance.DriveDirX;
            return dirX >= 0f ? GotchiFacing.Right : GotchiFacing.Left;
        }

        /// <summary>Wearable hand palm in unscaled sprite units for a facing.</summary>
        public static Vector2 HandSlotForFacing(GotchiFacing facing)
        {
            switch (facing)
            {
                case GotchiFacing.Left:
                    return new Vector2(-HandSidePalmX, HandSidePalmY);
                case GotchiFacing.Front:
                    // Character's right hand = screen left.
                    return new Vector2(-HandFrontPalmX, HandFrontPalmY);
                case GotchiFacing.Back:
                    // Character's right hand = screen right.
                    return new Vector2(HandFrontPalmX, HandFrontPalmY);
                default:
                    return new Vector2(HandSidePalmX, HandSidePalmY);
            }
        }

        /// <summary>
        /// Snap / hike: pull the parked LOS ball into the QB (or carrier) hands.
        /// </summary>
        public static bool AttachHeldTo(Transform unit)
        {
            if (unit == null) return false;
            var ball = FindBestParkedBall();
            if (ball == null) return false;
            ball.Catch(unit);

            var pc = unit.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.hasBall = true;
                pc.ballObject = ball.gameObject;
            }
            return true;
        }

        static FootballBehavior FindBestParkedBall()
        {
            FootballBehavior best = null;
            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                var fb = all[i];
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (fb.isInAir || fb.isBouncing || fb.isFumbleLoose || fb.isKickoffLoose)
                    continue;
                if (fb.isCaught && fb.carrier != null)
                    return fb; // already held — re-stick
                best = fb;
            }

            if (best != null) return best;

            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null) return go.GetComponent<FootballBehavior>();
            }
            catch { /* tag missing */ }

            return null;
        }

        /// <summary>True when the pass is late / descending into the aimed spot.</summary>
        public bool IsInCatchWindow()
        {
            if (!isInAir || isCaught || isBouncing || isTippedFlight) return false;
            if (FlightT < minCatchFlightT) return false;
            if (!IsInGotchiCatchHeightBand(receiverCatchHeight)) return false;

            float toLanding = Vector2.Distance(
                new Vector2(PlayPlanePosition.x, PlayPlanePosition.y),
                new Vector2(throwTarget.x, throwTarget.y));
            return toLanding <= landingWindowRadius;
        }

        /// <summary>
        /// Tipped ball is catchable while loft is at gotchi body height
        /// (not sailing over heads, not already on the dirt).
        /// </summary>
        public bool IsInTipCatchWindow()
        {
            if (!isTippedFlight || !isInAir || isCaught || isBouncing) return false;
            return IsInGotchiCatchHeightBand(tipCatchMaxHeight);
        }

        /// <summary>Ball loft within the gotchi catch / dive height band.</summary>
        public bool IsInGotchiCatchHeightBand(float maxHeight)
        {
            float maxH = Mathf.Max(tipCatchMinHeight + 0.05f, maxHeight);
            return FlightHeight >= tipCatchMinHeight && FlightHeight <= maxH;
        }

        bool TryCatchNearby()
        {
            if (outcomeLocked || !IsInCatchWindow())
                return false;

            GameObject[] receivers;
            try { receivers = GameObject.FindGameObjectsWithTag("Receiver"); }
            catch { return false; }

            ReceiverController best = null;
            float bestDist = float.MaxValue;
            Vector2 plane = new Vector2(PlayPlanePosition.x, PlayPlanePosition.y);
            Vector2 land = new Vector2(throwTarget.x, throwTarget.y);

            foreach (var go in receivers)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.hasBall) continue;

                Vector2 rp = new Vector2(go.transform.position.x, go.transform.position.y);
                float dBall = Vector2.Distance(plane, rp);
                if (dBall > catchCheckRadius) continue;

                // Prefer receivers actually near the throw spot over anyone who wandered onto the path.
                float dLand = Vector2.Distance(land, rp);
                float score = dBall + dLand * 0.35f;
                if (score < bestDist)
                {
                    bestDist = score;
                    best = rc;
                }
            }

            if (best == null) return false;

            // Jump-ball if a defender is also on the landing spot.
            if (TryFindContestingDefender(out Collider defCol, out Transform defTr))
                return ResolveJumpBall(best, defCol, defTr);

            bool caught = best.AttemptCatch(this);
            if (!caught && !outcomeLocked && IsInCatchWindow())
            {
                // Drop at the spot — readable incomplete, not a silent continue.
                BeginDroppedIncomplete(best);
                return true;
            }
            return caught;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!hasBeenThrown || isCaught || isBouncing || outcomeLocked) return;
            if (!isInAir) return;

            if (other.CompareTag("Receiver"))
            {
                // Triggers only in the catch window — no mid-route snags.
                if (!IsInCatchWindow())
                    return;
                var receiver = other.GetComponent<ReceiverController>();
                if (receiver == null) return;

                if (TryFindContestingDefender(out Collider defCol, out Transform defTr))
                    ResolveJumpBall(receiver, defCol, defTr);
                else if (!receiver.AttemptCatch(this) && !outcomeLocked && IsInCatchWindow())
                    BeginDroppedIncomplete(receiver);
            }
            else if (other.CompareTag("Defender"))
            {
                ResolveDefenderContest(other);
            }
        }

        void BeginDroppedIncomplete(ReceiverController receiver)
        {
            string who = receiver != null ? receiver.gameObject.name : "RECEIVER";
            BeginIncomplete($"DROPPED PASS\n{who}", BannerTone.Tip);
        }

        bool IsDlContester(Collider other)
        {
            if (other == null) return false;
            if (other.name.StartsWith("DL_")) return true;
            var ai = other.GetComponent<DefenderAI>();
            return ai != null && ai.role == DefenderRole.Rush;
        }

        float ContestReachHeight(Collider other)
            => IsDlContester(other) ? dlContestHeight : dbContestHeight;

        bool CanDefenderContest(Collider other)
        {
            if (other == null) return false;
            if (isTippedFlight) return false;
            if (!IsInGotchiCatchHeightBand(ContestReachHeight(other))) return false;

            Vector2 plane = new Vector2(PlayPlanePosition.x, PlayPlanePosition.y);
            Vector2 dp = new Vector2(other.transform.position.x, other.transform.position.y);
            if (Vector2.Distance(plane, dp) > defenderContestRadius)
                return false;

            // INT / jump-ball only in the catch window; DL may tip a bit earlier.
            if (IsInCatchWindow())
                return true;
            if (IsDlContester(other) && FlightT >= dlTipMinFlightT
                && FlightHeight <= ContestReachHeight(other))
                return true;
            return false;
        }

        bool TryFindContestingDefender(out Collider col, out Transform tr)
        {
            col = null;
            tr = null;
            GameObject[] defs;
            try { defs = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { return false; }

            float best = float.MaxValue;
            Vector2 plane = new Vector2(PlayPlanePosition.x, PlayPlanePosition.y);
            foreach (var go in defs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var c = go.GetComponent<Collider>();
                if (c == null || !CanDefenderContest(c)) continue;
                float d = Vector2.Distance(plane, new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < best)
                {
                    best = d;
                    col = c;
                    tr = go.transform;
                }
            }
            return tr != null;
        }

        void ResolveDefenderContest(Collider other)
        {
            if (outcomeLocked || !CanDefenderContest(other)) return;

            // WR also there → jump ball.
            ReceiverController wr = FindNearbyReceiverInWindow();
            if (wr != null)
            {
                ResolveJumpBall(wr, other, other.transform);
                return;
            }

            bool dl = IsDlContester(other);
            float roll = UnityEngine.Random.value;
            float tipP = dl ? Mathf.Clamp01(tipChanceAlone + 0.2f) : tipChanceAlone;
            float intP = dl ? Mathf.Clamp01(intChanceAlone * 0.35f) : intChanceAlone;

            if (roll < tipP)
                Tip(other.transform);
            else if (roll < tipP + intP && IsInCatchWindow())
                Intercept(other.transform);
            // else whiff — ball continues / incomplete at landing
        }

        bool ResolveJumpBall(ReceiverController wr, Collider defCol, Transform defTr)
        {
            if (outcomeLocked || wr == null || isCaught || !isInAir) return false;

            wr.PlayJumpCatchPose();
            Transform def = defTr != null ? defTr : (defCol != null ? defCol.transform : null);
            float roll = UnityEngine.Random.value;
            if (roll < contestedIntChance)
            {
                Intercept(def);
                return true;
            }
            if (roll < contestedIntChance + contestedTipChance)
            {
                Tip(def);
                return true;
            }

            // Offense wins the jump — still subject to catch/drop.
            if (wr.AttemptCatch(this))
                return true;

            if (!outcomeLocked)
                BeginIncomplete("TIPPED PASS", BannerTone.Tip, def);
            return true;
        }

        ReceiverController FindNearbyReceiverInWindow()
        {
            if (!IsInCatchWindow()) return null;
            GameObject[] receivers;
            try { receivers = GameObject.FindGameObjectsWithTag("Receiver"); }
            catch { return null; }

            ReceiverController best = null;
            float bestDist = catchCheckRadius;
            Vector2 plane = new Vector2(PlayPlanePosition.x, PlayPlanePosition.y);
            foreach (var go in receivers)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.hasBall) continue;
                float d = Vector2.Distance(plane, new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = rc;
                }
            }
            return best;
        }

        void Tip(Transform by)
        {
            if (outcomeLocked || !isInAir || isCaught || isTippedFlight) return;
            string who = by != null ? by.name : "DEFENSE";
            Debug.Log($"Pass tipped by {who}");
            BeginTippedFlight(by, $"TIPPED PASS\n{who}");
        }

        /// <summary>
        /// Redirect the pass after a tip: show trajectory + landing spot, stay live
        /// so a nearby / diving player can haul it in at gotchi height before ground.
        /// </summary>
        void BeginTippedFlight(Transform tipper, string headline)
        {
            outcomeLocked = true; // no further tip/INT contests on this throw
            isTippedFlight = true;
            isInAir = true;
            isBouncing = false;
            isCaught = false;
            isSpecialTeamsKick = false;
            tipIncompleteHeadline = string.IsNullOrEmpty(headline) ? "TIPPED PASS" : headline;
            carrier = null;
            transform.SetParent(null, true);
            PassCollisionGate.NotifyCatchAttemptResolved();

            Vector3 start = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            Vector3 dir = throwTarget - throwStart;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.right;
            dir.Normalize();

            if (tipper != null)
            {
                Vector3 away = start - tipper.position;
                away.z = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = Vector3.up * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
                dir = (dir * 0.4f + away.normalized).normalized;
            }

            // Small lateral scatter so the tip landing isn't glued to the tipper.
            dir = (dir + Vector3.up * UnityEngine.Random.Range(-0.35f, 0.35f)).normalized;

            float yardLen = FieldManager.Instance != null
                ? Mathf.Max(0.01f, FieldManager.Instance.yardLength)
                : 1f;
            Vector3 land = start + dir * (tipRedirectYards * yardLen);
            if (FieldManager.Instance != null)
                land = FieldManager.Instance.ClampInBounds(land);
            land.z = 0f;

            throwStart = start;
            throwTarget = land;
            PlayPlanePosition = start;
            transform.position = start;

            float distance = Mathf.Max(0.75f, Vector3.Distance(throwStart, throwTarget));
            flightPeak = Mathf.Clamp(tipPeakHeight, 0.55f, tipCatchMaxHeight + 0.35f);
            flightDuration = distance / Mathf.Max(0.01f, tipFlightSpeed);
            // Keep current loft so a tip mid-air doesn't teleport to the ground.
            float startFrac = Mathf.Clamp(FlightHeight / Mathf.Max(0.01f, flightPeak), 0.08f, 0.42f);
            flightElapsed = startFrac * flightDuration;
            FlightT = startFrac;
            FlightHeight = EvaluateLoft(FlightT, flightPeak);
            ResetBallSpin();
            ResetElevationScale();

            EnsureRigidbody();
            rb.isKinematic = true;
            rb.detectCollisions = true;
            if (ballCollider != null)
            {
                ballCollider.enabled = true;
                if (ballCollider is SphereCollider sphere)
                    sphere.radius = FormationRoster.BallCatchRadius;
            }

            ShowTipArc();
            FollowCameraToBall();
            PlayBanner.Show("TIPPED!", 0.75f, BannerTone.Tip);
            MatchPresentation.Tip();
            Debug.Log($"Tipped flight → land {land} (dive / catch at gotchi height)");
        }

        void FinishTippedFlightIncomplete()
        {
            HideTipArc();
            isTippedFlight = false;
            // Allow BeginIncomplete to run after tip loft ends.
            outcomeLocked = false;
            BeginIncomplete(tipIncompleteHeadline, BannerTone.Tip);
        }

        bool TryCatchTipped()
        {
            if (!IsInTipCatchWindow()) return false;

            ReceiverController best = null;
            float bestScore = float.MaxValue;
            Vector2 plane = new Vector2(PlayPlanePosition.x, PlayPlanePosition.y);
            Vector2 land = new Vector2(throwTarget.x, throwTarget.y);

            GameObject[] receivers;
            try { receivers = GameObject.FindGameObjectsWithTag("Receiver"); }
            catch { return false; }

            foreach (var go in receivers)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.hasBall) continue;

                Vector2 rp = new Vector2(go.transform.position.x, go.transform.position.y);
                float dBall = Vector2.Distance(plane, rp);
                float dLand = Vector2.Distance(land, rp);
                bool diving = TecmoDive.IsOffenseDiveBusy(go.transform);
                float reach = diving ? tipDiveCatchRadius : tipCatchRadius;
                // Diving at the marked landing spot gets a bonus reach.
                if (diving && dLand <= tipDiveCatchRadius + tipLandingBonusRadius)
                    reach = tipDiveCatchRadius + tipLandingBonusRadius;

                if (dBall > reach && dLand > reach) continue;

                float score = dBall + dLand * 0.25f;
                if (diving) score *= 0.75f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = rc;
                }
            }

            if (best == null) return false;

            // Height already gated by IsInTipCatchWindow — haul it in.
            HideTipArc();
            isTippedFlight = false;
            best.CompleteTipCatch(this);
            return true;
        }

        void EnsureTipArc()
        {
            if (tipArc != null) return;
            tipArc = GetComponent<ThrowingArc>();
            if (tipArc == null)
                tipArc = gameObject.AddComponent<ThrowingArc>();
        }

        void ShowTipArc()
        {
            EnsureTipArc();
            tipArc.Show();
            RefreshTipArc();
        }

        void RefreshTipArc()
        {
            if (tipArc == null || !isTippedFlight) return;
            tipArc.SetPath(throwStart, throwTarget, flightPeak);
        }

        void HideTipArc()
        {
            if (tipArc != null)
                tipArc.Hide();
        }

        public void Catch(Transform catcher)
        {
            outcomeLocked = true;
            isCaught = true;
            isInAir = false;
            isBouncing = false;
            isFumbleLoose = false;
            isKickoffLoose = false;
            isSpecialTeamsKick = false;
            isTippedFlight = false;
            onKickLanded = null;
            HideTipArc();
            kickoffBouncePhysics = false;
            kickoffHopVelY = 0f;
            kickoffBounceVel = Vector3.zero;
            hasBeenThrown = true;
            carrier = catcher;
            FlightHeight = 0f;
            FlightT = 1f;
            EnsureVisual();
            if (visual != null) visual.localPosition = Vector3.zero;
            ResetBallSpin();
            ResetElevationScale();

            CancelInvoke();

            EnsureRigidbody();
            if (!rb.isKinematic)
                ClearRigidbodyMotion();
            rb.isKinematic = true;
            rb.detectCollisions = false;

            if (ballCollider != null)
                ballCollider.enabled = false;

            transform.SetParent(catcher, true);
            // Visual is already sized via FormationRoster.BallScale — don't shrink further.
            transform.localScale = Vector3.one;
            StickToCarrier();

            // Wire PlayerController so throw / scramble know we hold the ball.
            var pc = catcher.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.hasBall = true;
                pc.ballObject = gameObject;
            }

            // ReceiverController carriers (RB handoff / catch).
            var rc = catcher.GetComponent<ReceiverController>();
            if (rc != null)
                rc.hasBall = true;

            Debug.Log("Ball Caught — sticking to " + catcher.name);
        }

        /// <summary>
        /// Stop hand-follow without starting a throw. Required before throw/scramble
        /// release — unparent alone leaves isCaught/carrier set so LateUpdate snaps back.
        /// </summary>
        public void ReleaseFromCarrier()
        {
            isCaught = false;
            carrier = null;
            isInAir = false;
            transform.SetParent(null, true);
            if (ballCollider != null)
                ballCollider.enabled = true;
            EnsureRigidbody();
            if (rb != null)
            {
                rb.detectCollisions = true;
                rb.isKinematic = true;
            }
        }

        /// <summary>Return to pre-snap parked state at a field position.</summary>
        public void ResetToParked(Vector3 worldPos)
        {
            CancelInvoke();
            carrier = null;
            isCaught = false;
            isSpecialTeamsKick = false;
            onKickLanded = null;
            isTippedFlight = false;
            HideTipArc();
            transform.SetParent(null, true);
            var park = new Vector3(worldPos.x, worldPos.y, 0f);
            transform.position = park;
            transform.localScale = Vector3.one;
            PlayPlanePosition = park;
            FlightHeight = 0f;
            FlightT = 0f;
            isFumbleLoose = false;
            isKickoffLoose = false;
            fumbleResolved = false;
            EnsureVisual();
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localPosition = Vector3.zero;
            }
            ResetBallSpin();
            ResetElevationScale();
            Park();
            if (ballCollider == null)
                ballCollider = GetComponent<Collider>();
            if (ballCollider != null)
            {
                // Prefab ships SphereCollider isTrigger=0 — force trigger so kickers
                // aren't physically blocked inches short of the tee.
                ballCollider.isTrigger = true;
                ballCollider.enabled = true;
            }
            EnsureRigidbody();
            if (rb != null)
            {
                rb.detectCollisions = true;
                rb.position = park;
            }
        }

        /// <summary>
        /// Dive strip / wrap strip: detach any held ball (or spawn one) and start a live
        /// bounce that either side can race to recover.
        /// </summary>
        public static void ForceFumbleAt(
            Vector3 origin,
            Vector3 knockDir,
            string causedBy,
            int refYard,
            bool wasRunPlay)
        {
            FootballBehavior ball = FindHeldOrSceneBall();
            if (ball == null)
            {
                var go = new GameObject("Football");
                try { go.tag = "Football"; }
                catch (UnityException) { }
                ball = go.AddComponent<FootballBehavior>();
            }

            ball.BeginFumble(origin, knockDir, causedBy, refYard, wasRunPlay);
            RetroLookApplier.RestyleFootball();
        }

        /// <summary>Live loose ball after a fumble — both teams scramble toward this.</summary>
        public static bool TryGetLooseFumble(out FootballBehavior ball)
        {
            ball = null;
            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (!fb.isFumbleLoose || fb.fumbleResolved) continue;
                ball = fb;
                return true;
            }
            return false;
        }

        static FootballBehavior FindHeldOrSceneBall()
        {
            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            FootballBehavior childHeld = null;
            FootballBehavior any = null;
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                any = fb;
                if (fb.transform.parent != null || fb.isCaught)
                {
                    childHeld = fb;
                    break;
                }
            }
            return childHeld != null ? childHeld : any;
        }

        void BeginFumble(Vector3 origin, Vector3 knockDir, string causedBy, int refYard, bool wasRunPlay)
        {
            CancelInvoke();
            carrier = null;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;

            origin.z = 0f;
            if (FieldManager.Instance != null)
                origin = FieldManager.Instance.ClampInBounds(origin);
            transform.position = origin;
            PlayPlanePosition = origin;

            knockDir.z = 0f;
            if (knockDir.sqrMagnitude < 0.01f)
                knockDir = Vector3.left;
            knockDir.Normalize();
            // Skitter mostly backward / lateral from the hit.
            bounceDir = (knockDir + Vector3.left * 0.35f + Vector3.up * UnityEngine.Random.Range(-0.4f, 0.4f)).normalized;

            isCaught = false;
            isInAir = false;
            isBouncing = true;
            isFumbleLoose = true;
            fumbleResolved = false;
            hasBeenThrown = true;
            fumbleCausedBy = causedBy;
            fumbleRefYard = refYard;
            fumbleWasRun = wasRunPlay;
            offenseHadPossessionAtFumble = FieldManager.Instance == null
                                           || FieldManager.Instance.isPlayerPossession;

            bounceOrigin = origin;
            bounceElapsed = 0f;
            activeBounceDuration = fumbleBounceDuration;
            activeBounceSlide = fumbleSlideSpeed;
            FlightHeight = 0f;
            StartGroundBounce(bounceDir, Mathf.Max(2.5f, fumbleSlideSpeed), bounceHopSpeed * 1.1f);

            EnsureVisual();
            if (visual != null) visual.localPosition = Vector3.zero;

            EnsureRigidbody();
            rb.isKinematic = true;
            rb.detectCollisions = true;
            if (ballCollider != null)
            {
                ballCollider.enabled = true;
                if (ballCollider is SphereCollider sphere)
                    sphere.radius = FormationRoster.BallCatchRadius;
            }

            PassCollisionGate.NotifyCatchAttemptResolved();
            FollowCameraToBall();
            PlayBanner.Show("FUMBLE!", 1.15f, BannerTone.Turnover);
            Debug.Log($"Fumble forced by {causedBy}");
        }

        /// <summary>True if this instance should be cleared between plays.</summary>
        public bool ShouldClearBetweenPlays
            => isCaught || isInAir || isBouncing || isFumbleLoose || isKickoffLoose
               || hasBeenThrown || transform.parent != null;

        void BeginIncomplete(
            string headline = "INCOMPLETE PASS",
            BannerTone tone = BannerTone.Neutral,
            Transform tipper = null)
        {
            if (isCaught || isBouncing) return;
            // Tip loft ending re-enters here with outcomeLocked already true.
            if (outcomeLocked && !isTippedFlight) return;
            outcomeLocked = true;
            isTippedFlight = false;
            HideTipArc();

            isInAir = false;
            isBouncing = true;
            isFumbleLoose = false;
            carrier = null;
            transform.SetParent(null, true);
            FlightHeight = 0f;
            bounceElapsed = 0f;
            bounceOrigin = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            activeBounceDuration = bounceDuration;
            activeBounceSlide = bounceSlideSpeed;
            PassCollisionGate.NotifyCatchAttemptResolved();

            Vector3 dir = throwTarget - throwStart;
            dir.z = 0f;
            bounceDir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.right;

            // Tipped balls skitter sideways off the tipper so the bat-down reads clearly.
            float slide = bounceSlideSpeed;
            if (tipper != null)
            {
                Vector3 away = PlayPlanePosition - tipper.position;
                away.z = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = Vector3.up * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
                bounceDir = (bounceDir * 0.35f + away.normalized).normalized;
                slide = bounceSlideSpeed * 1.15f;
            }

            activeBounceSlide = slide;
            StartGroundBounce(bounceDir, slide, bounceHopSpeed);

            EnsureRigidbody();
            rb.isKinematic = true;
            transform.rotation = Quaternion.identity;

            FollowCameraToBall();
            if (FieldManager.Instance != null)
                FieldManager.Instance.AdvanceBall(0);
            string downLine = FieldManager.Instance != null
                ? FieldManager.Instance.GetPlayResultDownLine()
                : "";
            string title;
            if (FieldManager.Instance != null && FieldManager.Instance.JustFailedTwoPoint)
                title = "2-POINT NO GOOD";
            else
                title = string.IsNullOrEmpty(headline) ? "INCOMPLETE PASS" : headline;
            PlayBanner.ShowPlayOver(string.IsNullOrEmpty(downLine) || title.Contains("2-POINT")
                ? title
                : $"{title}\n{downLine}", tone);
            Debug.Log($"{title} — ball bouncing");
        }

        void StartGroundBounce(Vector3 dir, float slideSpeed, float hopSpeed)
        {
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            bounceDir = dir.normalized;
            groundBounceVel = bounceDir * Mathf.Max(1.5f, slideSpeed);
            groundHopVelY = Mathf.Max(1.5f, hopSpeed);
            groundHopCount = 0;
            FlightHeight = 0.02f;
        }

        void UpdateBounce()
        {
            // Kickoff uses hop + planar velocity so deep kicks can exit the endzone.
            if (isKickoffLoose && kickoffBouncePhysics)
            {
                UpdateKickoffBouncePhysics();
                return;
            }

            // Incomplete / fumble: forward skitter + discrete hops (never reverses).
            float dt = Time.deltaTime;
            bounceElapsed += dt;

            groundHopVelY -= bounceHopGravity * dt;
            FlightHeight += groundHopVelY * dt;

            PlayPlanePosition += groundBounceVel * dt;
            if (FieldManager.Instance != null)
                PlayPlanePosition = FieldManager.Instance.ClampInBounds(PlayPlanePosition);
            PlayPlanePosition = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);

            if (FlightHeight <= 0f)
            {
                FlightHeight = 0f;
                groundHopCount++;
                groundBounceVel *= Mathf.Clamp(bounceGroundFriction, 0.4f, 0.95f);

                float nextHop = Mathf.Abs(groundHopVelY) * Mathf.Clamp(bounceHopRestitution, 0.15f, 0.9f);
                float dur = Mathf.Max(0.5f, activeBounceDuration > 0f ? activeBounceDuration : bounceDuration);
                bool done = groundHopCount >= Mathf.Max(2, bounceMaxHops)
                            || groundBounceVel.magnitude < bounceSettleSpeed
                            || nextHop < 0.75f
                            || bounceElapsed >= dur;
                if (done)
                {
                    groundHopVelY = 0f;
                    groundBounceVel = Vector3.zero;
                    FlightHeight = 0f;
                    EnsureVisual();
                    if (visual != null) visual.localPosition = Vector3.zero;
                    ResetElevationScale();
                    transform.position = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
                    transform.rotation = Quaternion.identity;

                    if (isFumbleLoose && !fumbleResolved)
                        ResolveFumbleByNearest();
                    else if (!isFumbleLoose)
                        FinishIncomplete();
                    return;
                }

                groundHopVelY = nextHop;
            }

            transform.position = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            EnsureVisual();
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, FlightHeight, 0f);
                // Spin the sprite only — rotating the root made the ball look like it swirled.
                float speedT = Mathf.Clamp01(
                    groundBounceVel.magnitude / Mathf.Max(1f, bounceSlideSpeed));
                visual.Rotate(0f, 0f, bounceSpinDegrees * speedT * dt);
            }
            ApplyElevationScale();

            if (rb != null)
                rb.position = transform.position;

            if (isFumbleLoose && !fumbleResolved && bounceElapsed >= fumbleRecoverAfter)
                TryRecoverFumbleNearby();
        }

        /// <summary>
        /// Discrete hops with gravity + friction so the ball can skitter out the back of the EZ.
        /// Touchback resolve stays in <see cref="KickingController"/>.
        /// </summary>
        void UpdateKickoffBouncePhysics()
        {
            float dt = Time.deltaTime;
            bounceElapsed += dt;

            kickoffHopVelY -= kickoffHopGravity * dt;
            FlightHeight += kickoffHopVelY * dt;

            PlayPlanePosition += kickoffBounceVel * dt;
            // Free plane — may cross sideline / end line for TB.
            PlayPlanePosition = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);

            if (FlightHeight <= 0f)
            {
                FlightHeight = 0f;
                kickoffHopCount++;
                kickoffBounceVel *= Mathf.Clamp(kickoffGroundFriction, 0.4f, 0.95f);

                float nextHop = Mathf.Abs(kickoffHopVelY) * Mathf.Clamp(kickoffHopRestitution, 0.15f, 0.9f);
                bool done = kickoffHopCount >= Mathf.Max(2, kickoffMaxHops)
                            || kickoffBounceVel.magnitude < kickoffSettleSpeed
                            || nextHop < 1.1f;
                if (done)
                {
                    kickoffHopVelY = 0f;
                    FinishKickoffBounceSkitter();
                    return;
                }

                kickoffHopVelY = nextHop;
            }

            transform.position = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            EnsureVisual();
            if (visual != null)
                visual.localPosition = new Vector3(0f, FlightHeight, 0f);
            ApplyElevationScale();

            float spin = 420f * Mathf.Clamp01(kickoffBounceVel.magnitude / Mathf.Max(1f, kickoffSlideSpeed));
            transform.Rotate(0f, 0f, spin * dt);

            if (rb != null)
                rb.position = transform.position;
        }

        /// <summary>
        /// End of kickoff skitter — ball stays live on the ground for scoop / touchback resolve.
        /// </summary>
        void FinishKickoffBounceSkitter()
        {
            isBouncing = false;
            kickoffBouncePhysics = false;
            kickoffHopVelY = 0f;
            kickoffBounceVel = Vector3.zero;
            FlightHeight = 0f;
            EnsureVisual();
            if (visual != null) visual.localPosition = Vector3.zero;
            ResetElevationScale();
            transform.position = new Vector3(PlayPlanePosition.x, PlayPlanePosition.y, 0f);
            EnsureRigidbody();
            if (!rb.isKinematic)
                ClearRigidbodyMotion();
            rb.isKinematic = true;
            rb.position = transform.position;
            // Leave isKickoffLoose = true until pickup or touchback.
        }

        bool TryRecoverFumbleNearby()
        {
            if (!TryFindNearestRecoverer(PlayPlanePosition, fumbleRecoverRadius, out Transform who, out bool isDefense))
                return false;
            CompleteFumbleRecovery(who, isDefense);
            return true;
        }

        void ResolveFumbleByNearest()
        {
            // End of bounce — nearest body on the field gets it (biased toward diver if close).
            if (!TryFindNearestRecoverer(PlayPlanePosition, 40f, out Transform who, out bool isDefense))
            {
                // Fallback: offense recovers at spot.
                isDefense = false;
                who = GameObject.Find("Quarterback")?.transform;
            }
            CompleteFumbleRecovery(who, isDefense);
        }

        bool TryFindNearestRecoverer(Vector3 at, float maxDist, out Transform best, out bool isDefense)
        {
            Transform nearest = null;
            bool nearestDefense = false;
            float bestDist = maxDist;

            // Prefer the diver who caused it when they're still nearby.
            if (!string.IsNullOrEmpty(fumbleCausedBy))
            {
                var diver = GameObject.Find(fumbleCausedBy);
                if (diver != null && diver.activeInHierarchy)
                {
                    float d = Vector2.Distance(
                        new Vector2(at.x, at.y),
                        new Vector2(diver.transform.position.x, diver.transform.position.y));
                    if (d <= maxDist * 0.85f)
                    {
                        nearest = diver.transform;
                        bestDist = d * 0.72f; // slight bias
                        nearestDefense = true;
                    }
                }
            }

            GameObject[] defs;
            try { defs = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { defs = System.Array.Empty<GameObject>(); }
            foreach (var go in defs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                float d = Vector2.Distance(
                    new Vector2(at.x, at.y),
                    new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = go.transform;
                    nearestDefense = true;
                }
            }

            void ConsiderOffense(GameObject go)
            {
                if (go == null || !go.activeInHierarchy) return;
                float d = Vector2.Distance(
                    new Vector2(at.x, at.y),
                    new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = go.transform;
                    nearestDefense = false;
                }
            }

            foreach (var name in FormationRoster.SkillOffense)
                ConsiderOffense(GameObject.Find(name));

            foreach (var name in FormationRoster.OffensiveLine)
                ConsiderOffense(GameObject.Find(name));

            ConsiderOffense(GameObject.Find("Quarterback"));

            best = nearest;
            isDefense = nearestDefense;
            return best != null;
        }

        void CompleteFumbleRecovery(Transform recoverer, bool defenseRecovered)
        {
            if (fumbleResolved) return;
            fumbleResolved = true;
            isFumbleLoose = false;
            isBouncing = false;
            isInAir = false;
            FlightHeight = 0f;

            int spotYard = FieldManager.Instance != null
                ? FieldManager.Instance.WorldXToYard(PlayPlanePosition.x)
                : fumbleRefYard;

            string byName = recoverer != null ? recoverer.name : (defenseRecovered ? "DEFENSE" : "OFFENSE");
            string side = defenseRecovered ? "DEFENSE" : "OFFENSE";

            if (recoverer != null)
            {
                Catch(recoverer);
            }
            else
            {
                isCaught = true;
                EnsureRigidbody();
                rb.isKinematic = true;
            }

            // Defense recovers → live return (scoop-six possible). Offense recovers → keep running.
            if (defenseRecovered)
            {
                // Recovered in the offense's own endzone = defensive touchdown (no return needed).
                if (recoverer != null
                    && FieldManager.Instance != null
                    && FieldManager.Instance.IsInOwnEndzone(PlayPlanePosition))
                {
                    AwardDefensiveFumbleTouchdown(recoverer);
                    return;
                }

                if (recoverer != null)
                {
                    var ret = recoverer.GetComponent<InterceptionReturner>();
                    if (ret == null)
                        ret = recoverer.gameObject.AddComponent<InterceptionReturner>();
                    ret.Begin(offenseHadPossessionAtFumble, LiveReturnKind.Fumble);
                    // Already standing in the return scoring EZ (e.g. deep in opponent endzone).
                    if (ret.TryScoreIfAlreadyInEndzone())
                    {
                        FollowCameraToBall();
                        return;
                    }
                    FollowCameraToBall();
                    Debug.Log($"Fumble recovered by {byName} (DEFENSE) — live return at yard {spotYard}");
                    return;
                }

                // No body found — dead-ball turnover at the spot.
                if (FieldManager.Instance != null)
                    FieldManager.Instance.ChangeOfPossession("FUMBLE", spotYard);
                PlayBanner.ShowPlayOver($"FUMBLE\nRECOVERED BY {side}", BannerTone.Turnover);
                Invoke(nameof(CleanupFumbleBall), 0.75f);
                return;
            }

            // Offense recovers — continue as ball-carrier until downed / TD / OOB.
            if (TryBeginOffenseFumbleContinue(recoverer, spotYard))
            {
                PlayBanner.Show($"RECOVERED BY {byName}", 0.95f, BannerTone.Positive);
                FollowCameraToBall();
                Debug.Log($"Fumble recovered by {byName} (OFFENSE) — play continues at yard {spotYard}");
                return;
            }

            // Fallback: whistle / spot (no usable carrier component).
            SpotOffenseFumbleDead(spotYard, byName, side);
        }

        /// <summary>
        /// Defense recovers a fumble in the offense's own endzone → immediate defensive TD.
        /// </summary>
        void AwardDefensiveFumbleTouchdown(Transform recoverer)
        {
            bool offenseWasPlayer = offenseHadPossessionAtFumble;
            bool scorerIsPlayer = !offenseWasPlayer;
            // Prior offense attack dir — defense scores into the endzone offense was defending.
            int priorDir = FieldManager.Instance != null
                ? FieldManager.NormDir(FieldManager.Instance.driveDirection)
                : (offenseWasPlayer ? 1 : -1);
            bool towardLow = priorDir > 0;

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RecordTurnover(offenseWasPlayer);

            if (FieldManager.Instance == null)
            {
                PlayBanner.ShowPlayOver("SCOOP SIX\nTOUCHDOWN", BannerTone.Positive);
                return;
            }

            bool scored = FieldManager.Instance.TryScoreInterceptionReturnTouchdown(
                PlayPlanePosition.x, scorerIsPlayer, towardLow);
            if (!scored && !FieldManager.Instance.JustScoredTouchdown)
            {
                // Force possession + drive into the return scoring endzone, then TD.
                FieldManager.Instance.isPlayerPossession = scorerIsPlayer;
                FieldManager.Instance.driveDirection = towardLow ? -1 : 1;
                scored = FieldManager.Instance.TryScoreTouchdown(
                    towardLow
                        ? FieldManager.Instance.YardToWorldX(0f)
                        : FieldManager.Instance.YardToWorldX(100f));
            }

            PlayBanner.ShowPlayOver("SCOOP SIX\nTOUCHDOWN", BannerTone.Positive);
            Debug.Log(
                $"Fumble recovered in offense endzone by {recoverer.name} — " +
                $"defensive TD ({(scorerIsPlayer ? "player" : "opponent")} +6, scored={scored || FieldManager.Instance.JustScoredTouchdown})");

            if (GameManager.Instance != null)
                GameManager.Instance.isInterceptionReturn = false;

            FollowCameraToBall();
        }

        bool TryBeginOffenseFumbleContinue(Transform recoverer, int spotYard)
        {
            Transform runner = recoverer;
            if (runner != null
                && runner.GetComponent<ReceiverController>() == null
                && runner.GetComponent<QuarterbackController>() == null)
            {
                // OL / non-skill recoverer — pitch to nearest skill / QB.
                runner = FindNearestOffenseRunner(PlayPlanePosition) ?? runner;
                if (runner != recoverer)
                    Catch(runner);
            }

            if (runner == null) return false;

            var rc = runner.GetComponent<ReceiverController>();
            if (rc != null)
            {
                rc.BeginFumbleRecovery(fumbleRefYard, fumbleWasRun);
                return true;
            }

            var qbc = runner.GetComponent<QuarterbackController>();
            if (qbc != null)
            {
                qbc.BeginFumbleRecovery(fumbleRefYard);
                return true;
            }

            return false;
        }

        static Transform FindNearestOffenseRunner(Vector3 at)
        {
            Transform best = null;
            float bestDist = 18f;

            void Consider(GameObject go)
            {
                if (go == null || !go.activeInHierarchy) return;
                if (go.GetComponent<ReceiverController>() == null
                    && go.GetComponent<QuarterbackController>() == null)
                    return;
                float d = Vector2.Distance(
                    new Vector2(at.x, at.y),
                    new Vector2(go.transform.position.x, go.transform.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }

            foreach (var name in FormationRoster.SkillOffense)
                Consider(GameObject.Find(name));
            Consider(GameObject.Find("Quarterback"));
            return best;
        }

        void SpotOffenseFumbleDead(int spotYard, string byName, string side)
        {
            string playKind = fumbleWasRun ? "RUN" : "PASS";
            if (FieldManager.Instance != null)
            {
                if (FieldManager.Instance.TryScoreTouchdown(PlayPlanePosition.x)
                    || FieldManager.Instance.JustScoredTouchdown)
                {
                    PlayBanner.ShowPlayOver("TOUCHDOWN");
                }
                else
                {
                    int yardsGained = FieldManager.Instance.YardsGained(fumbleRefYard, spotYard);
                    FieldManager.Instance.AdvanceBall(yardsGained);
                    if (FieldManager.Instance.JustScoredTouchdown)
                    {
                        PlayBanner.ShowPlayOver("TOUCHDOWN");
                    }
                    else
                    {
                        string downLine = FieldManager.Instance.GetPlayResultDownLine();
                        string yardsText = yardsGained >= 0 ? $"+{yardsGained} YDS" : $"{yardsGained} YDS";
                        PlayBanner.ShowPlayOver(
                            $"FUMBLE ({playKind})\nRECOVERED BY {side}\n{byName}\n{yardsText}\n{downLine}");
                    }
                }
            }
            else
            {
                PlayBanner.ShowPlayOver($"FUMBLE\nRECOVERED BY {side}\n{byName}");
            }

            Debug.Log($"Fumble recovered by {byName} ({side}) — dead-ball spot at yard {spotYard}");
            Invoke(nameof(CleanupFumbleBall), 0.75f);
        }

        void CleanupFumbleBall()
        {
            ReturnCameraToPlayer();
            if (gameObject != null)
                Destroy(gameObject);
        }

        void FinishIncomplete()
        {
            isBouncing = false;
            FlightHeight = 0f;
            EnsureRigidbody();
            if (!rb.isKinematic)
                ClearRigidbodyMotion();
            rb.isKinematic = true;

            Invoke(nameof(CleanupIncomplete), 0.6f);
        }

        void CleanupIncomplete()
        {
            ReturnCameraToPlayer();
            Destroy(gameObject);
        }

        /// <summary>
        /// Tecmo CB jump: player taps A near a catchable ball → high INT / tip chance.
        /// Returns true if a contest resolved (INT, tip, or jump-ball).
        /// </summary>
        public static bool TryPlayerJumpIntercept(Transform defender, float radius)
        {
            if (defender == null) return false;
            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            FootballBehavior best = null;
            float bestDist = radius;
            Vector2 dp = new Vector2(defender.position.x, defender.position.y);
            foreach (var b in all)
            {
                if (b == null || !b.isInAir || b.isCaught || b.outcomeLocked) continue;
                if (!b.IsInCatchWindow()) continue;
                if (b.FlightHeight > b.dbContestHeight + 0.35f) continue;
                Vector2 bp = new Vector2(b.PlayPlanePosition.x, b.PlayPlanePosition.y);
                float d = Vector2.Distance(dp, bp);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }

            if (best == null) return false;
            return best.ResolvePlayerJump(defender);
        }

        bool ResolvePlayerJump(Transform defender)
        {
            if (outcomeLocked || !isInAir || isCaught || defender == null) return false;

            // Contested with a WR in window → jump ball with boosted INT.
            ReceiverController wr = FindNearbyReceiverInWindow();
            if (wr != null)
            {
                wr.PlayJumpCatchPose();
                float roll = UnityEngine.Random.value;
                // Player jump: strong INT lean vs contested baseline.
                if (roll < 0.55f)
                {
                    Intercept(defender);
                    return true;
                }
                if (roll < 0.78f)
                {
                    Tip(defender);
                    return true;
                }

                if (wr.AttemptCatch(this))
                    return true;
                if (!outcomeLocked)
                    BeginIncomplete("TIPPED PASS", BannerTone.Tip, defender);
                return true;
            }

            // Alone in the window — very high INT on a timed jump.
            float alone = UnityEngine.Random.value;
            if (alone < 0.72f)
            {
                Intercept(defender);
                return true;
            }
            if (alone < 0.9f)
            {
                Tip(defender);
                return true;
            }

            PlayBanner.Show("WHIFF!", 0.5f, BannerTone.Neutral);
            return true;
        }

        void Intercept(Transform interceptor)
        {
            if (outcomeLocked || !isInAir || isCaught) return;

            string who = interceptor != null ? interceptor.name : "DEFENSE";
            Debug.Log($"Intercepted by {who}!");

            isInAir = false;
            isBouncing = false;
            isFumbleLoose = false;
            FlightHeight = 0f;
            PassCollisionGate.NotifyCatchAttemptResolved();
            outcomeLocked = true;

            // 2-point try: INT is a dead-ball failed conversion (no return).
            if (FieldManager.Instance != null && FieldManager.Instance.IsTwoPointAttempt)
            {
                FieldManager.Instance.FailTwoPointAttempt();
                isCaught = true;
                carrier = null;
                transform.SetParent(null, true);
                PlayBanner.ShowPlayOver($"INTERCEPTION\n2-POINT NO GOOD", BannerTone.Turnover);
                FollowCameraToBall();
                Invoke(nameof(CleanupInterceptBall), Mathf.Max(0.35f, interceptionHoldSeconds));
                return;
            }

            bool offenseWasPlayer = FieldManager.Instance == null
                                    || FieldManager.Instance.isPlayerPossession;

            int spotYard = FieldManager.Instance != null
                ? FieldManager.Instance.WorldXToYard(PlayPlanePosition.x)
                : 20;

            // Live INT → attach + return. Tip/incomplete stay dead-ball (BeginIncomplete).
            if (interceptor != null)
            {
                Catch(interceptor);
                var ret = interceptor.GetComponent<InterceptionReturner>();
                if (ret == null)
                    ret = interceptor.gameObject.AddComponent<InterceptionReturner>();
                ret.Begin(offenseWasPlayer);
                FollowCameraToBall();
                return;
            }

            // No interceptor transform — dead-ball turnover at the spot.
            isCaught = true;
            if (FieldManager.Instance != null)
                FieldManager.Instance.ChangeOfPossession("INTERCEPTION", spotYard);
            PlayBanner.ShowPlayOver($"INTERCEPTION\n{who}\nAT THE {spotYard}", BannerTone.Turnover);
            FollowCameraToBall();
            Invoke(nameof(CleanupInterceptBall), Mathf.Max(0.35f, interceptionHoldSeconds));
        }

        void CleanupInterceptBall()
        {
            ReturnCameraToPlayer();
            if (gameObject != null)
                Destroy(gameObject);
        }

        void FollowCameraToBall()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.FollowBall(transform);
        }

        static void ReturnCameraToPlayer()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc != null)
            {
                if (PlayerDefenseController.PlayerIsOnDefense()
                    && PlayerDefenseController.Instance != null
                    && PlayerDefenseController.Instance.Controlled != null)
                {
                    cc.SetTarget(PlayerDefenseController.Instance.Controlled.transform);
                }
                else
                {
                    cc.FollowPlayer();
                }
            }
        }
    }
}
