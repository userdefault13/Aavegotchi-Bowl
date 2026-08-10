using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Retro Bowl 2D camera: orthographic, follows downfield X (and Y when tracking the ball).
    /// While the ball is in flight, adds a smoothed +Y loft offset so deep throws feel elevated.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Follow")]
        public Transform target;
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        [Tooltip("Units/sec follow speed. High = locks to player (no camera coast after stop).")]
        public float smoothSpeed = 80f;
        public bool followPlayer = true;
        /// <summary>When true, camera Y tracks the target (used for the thrown ball).</summary>
        public bool followTargetY;

        [Header("Ball Loft")]
        [Tooltip("How much of the ball's FlightHeight lifts the camera (screen-up / +Y).")]
        public float loftFollowFactor = 0.5f;
        [Tooltip("Max camera elevation from loft so deep bombs don't lose the field.")]
        public float loftFollowMax = 4.5f;
        [Tooltip("SmoothDamp time for loft rise and return.")]
        public float loftSmoothTime = 0.22f;

        [Header("Orthographic")]
        public float orthoSize = 12f;
        public float zoomedOrthoSize = 8f;
        public float zoomSpeed = 6f;
        /// <summary>Wide enough that kicker (31) + tee (35) + walls (60/65) share the frame.</summary>
        public float kickoffOrthoSize = 11.5f;

        [Header("Bounds")]
        public float minX = -50f;
        public float maxX = 50f;
        public float minY = -8f;
        public float maxY = 8f;

        Camera cam;
        bool isZoomed;
        FootballBehavior ballFlight;
        float loftOffset;
        float loftVelocity;
        float shakeAmp;
        float shakeUntil;
        Vector3 shakeOffset;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
            }

            transform.rotation = Quaternion.identity;

            if (target == null)
            {
                GameObject player = null;
                try { player = GameObject.FindGameObjectWithTag("Player"); }
                catch (UnityException) { /* tag missing */ }
                if (player == null)
                    player = GameObject.Find("Quarterback");
                if (player != null) target = player.transform;
            }
        }

        void LateUpdate()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
                if (cam == null) cam = Camera.main;
            }

            bool playing = GameManager.Instance == null
                           || GameManager.Instance.currentState == GameState.Playing
                           || GameManager.Instance.currentState == GameState.Paused;
            bool preSnap = GameManager.Instance != null && GameManager.Instance.isPreSnap;
            bool kicking = GameManager.Instance != null && GameManager.Instance.isKicking;
            bool kickReturn = GameManager.Instance != null && GameManager.Instance.IsLiveReturn;
            // Kickoff intro (empty field) also parks on the tee yard line.
            bool pendingKick = FieldManager.Instance != null && FieldManager.Instance.PendingKickoff
                               && GameManager.Instance != null && GameManager.Instance.waitingForNextPlay
                               && !kickReturn;
            bool kickApproach = KickingController.Instance != null
                                && KickingController.Instance.IsKickerApproaching;
            bool kickFlightFlag = KickingController.Instance != null
                                  && KickingController.Instance.IsBallInFlight;
            bool looseKickoff = FootballBehavior.TryGetLooseKickoff(out var looseKoBall);

            // Authoritative ball: special-teams loft OR live bounce.
            FootballBehavior koBall = ResolveKickoffFollowBall(looseKoBall);
            // Also treat controller flight flag + any live football as kick follow.
            if (koBall == null && kickFlightFlag)
                koBall = FindAnyLiveFootball();
            // Player-receive fielding: stay on the returner (WASD), not the flying ball.
            bool playerFieldingKick = !kickReturn
                                      && FieldManager.Instance != null
                                      && FieldManager.Instance.KickoffReceiverIsPlayer
                                      && (kicking || kickFlightFlag || looseKickoff);
            // Player-kick coverage: stay on the gunner so you can chase/tackle.
            bool playerKickCover = PlayerDefenseController.Instance != null
                                   && PlayerDefenseController.Instance.IsKickoffCoverage;
            bool kickBallFollow = !kickReturn && !playerFieldingKick && !playerKickCover
                                  && koBall != null
                                  && (koBall.IsSpecialTeamsKick || koBall.isKickoffLoose
                                      || kickFlightFlag || looseKickoff);

            // Pass / tip bounce — same hard lock as kickoff so the camera keeps up.
            FootballBehavior passBall = null;
            bool passBallFollow = !kickBallFollow && !kicking && !kickReturn
                                  && !playerKickCover && !pendingKick
                                  && TryGetPassFlightBall(out passBall);

            if (kickBallFollow)
            {
                FollowBall(koBall.transform);
                ballFlight = koBall;
            }
            else if (passBallFollow)
            {
                FollowBall(passBall.transform);
                ballFlight = passBall;
            }

            UpdateLoftOffset((playing && !preSnap && !kicking)
                             || kickBallFollow || passBallFollow
                             || kickFlightFlag || looseKickoff || kickReturn);
            UpdateShake();

            // 1) Kickoff ball — HARD lock (scene smoothSpeed is often ~5; kick flies at ~22).
            if (kickBallFollow && koBall != null)
            {
                TrackFlightBall(koBall);
            }
            // 1b) Pass in flight — same hard lock as kickoff.
            else if (passBallFollow && passBall != null)
            {
                TrackFlightBall(passBall);
            }
            // 2) Player gunner / receive fielding — stay on the controlled unit.
            else if ((playerKickCover || playerFieldingKick) && target != null)
            {
                followPlayer = true;
                TrackTarget(approachBias: false, minSpeed: 40f);
            }
            // 3) Live return — follow returner fast.
            else if (kickReturn && target != null)
            {
                followPlayer = true;
                TrackTarget(approachBias: false, minSpeed: 40f);
            }
            // 4) Kicker run-up — follow kicker, soft bias to midfield frame.
            else if (playing && kickApproach && !kickBallFollow && target != null)
            {
                followPlayer = true;
                TrackTarget(approachBias: true, minSpeed: 30f);
            }
            // 5) Normal live follow.
            else if (followPlayer && target != null && playing
                     && !preSnap && !kicking && !pendingKick)
            {
                TrackTarget(approachBias: false, minSpeed: 0f);
            }
            // 5b) Defense pre-snap: follow the Q/E-selected defender (not LOS park).
            else if (playing && preSnap && !kicking && !pendingKick
                     && PlayerDefenseController.Instance != null
                     && PlayerDefenseController.Instance.IsActive
                     && !PlayerDefenseController.Instance.IsKickoffCoverage
                     && PlayerDefenseController.PlayerIsOnDefense()
                     && target != null)
            {
                followPlayer = true;
                TrackTarget(approachBias: false, minSpeed: 40f);
            }
            // 6) Pre-snap / kick aim / intro — park on midfield or LOS.
            else if (!playing || preSnap || kicking || pendingKick)
            {
                bool kickoffCam = FieldManager.Instance != null
                                  && FieldManager.Instance.PendingKickoff
                                  && !kickReturn
                                  && !kickBallFollow;
                float yard = kickoffCam
                    ? FormationRoster.KickoffCameraYardAbs
                    : FormationRoster.CurrentLosYard();
                if (!kickoffCam && kicking && !kickReturn
                    && KickingController.Instance != null
                    && KickingController.Instance.IsUprightKickCamera)
                {
                    float dir = FieldManager.Instance != null
                        ? FieldManager.Instance.DriveDirX
                        : 1f;
                    yard += FormationRoster.UprightKickCameraLeadYards * dir;
                }
                float x = RetroLookApplier.YardToX(yard);
                transform.position = new Vector3(x, offset.y, offset.z) + shakeOffset;
            }

            if (Input.GetKeyDown(KeyCode.Z))
                isZoomed = !isZoomed;

            if (cam != null)
            {
                // Wide only during aim / approach — tighten once the ball is flying.
                bool kickoffWide = FieldManager.Instance != null
                                   && FieldManager.Instance.PendingKickoff
                                   && !kickBallFollow
                                   && !kickReturn;
                float size = kickoffWide
                    ? kickoffOrthoSize
                    : (isZoomed ? zoomedOrthoSize : orthoSize);
                if (kickoffWide && Mathf.Abs(cam.orthographicSize - kickoffOrthoSize) > 0.05f)
                    cam.orthographicSize = kickoffOrthoSize;
                else if (kickBallFollow || passBallFollow)
                    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, orthoSize, zoomSpeed * Time.deltaTime);
                else
                    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, zoomSpeed * Time.deltaTime);
            }

            transform.rotation = Quaternion.identity;
        }

        /// <summary>Live pass / incomplete bounce — not special-teams kickoff.</summary>
        static bool TryGetPassFlightBall(out FootballBehavior ball)
        {
            ball = null;
            if (FieldManager.Instance != null && FieldManager.Instance.ballTransform != null)
            {
                var fb = FieldManager.Instance.ballTransform.GetComponent<FootballBehavior>();
                if (IsPassFlightBall(fb))
                {
                    ball = fb;
                    return true;
                }
            }

            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null)
                {
                    var fb = go.GetComponent<FootballBehavior>();
                    if (IsPassFlightBall(fb))
                    {
                        ball = fb;
                        return true;
                    }
                }
            }
            catch (UnityException) { /* tag missing */ }

            var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (!IsPassFlightBall(fb)) continue;
                ball = fb;
                return true;
            }
            return false;
        }

        static bool IsPassFlightBall(FootballBehavior fb)
        {
            if (fb == null || !fb.gameObject.scene.IsValid()) return false;
            if (fb.IsSpecialTeamsKick || fb.isKickoffLoose) return false;
            return fb.isInAir || fb.isBouncing || fb.isFumbleLoose;
        }

        static FootballBehavior ResolveKickoffFollowBall(FootballBehavior looseKoBall)
        {
            if (looseKoBall != null) return looseKoBall;

            if (FieldManager.Instance != null && FieldManager.Instance.ballTransform != null)
            {
                var fb = FieldManager.Instance.ballTransform.GetComponent<FootballBehavior>();
                if (fb != null && (fb.IsSpecialTeamsKick || fb.isKickoffLoose))
                    return fb;
            }

            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null)
                {
                    var fb = go.GetComponent<FootballBehavior>();
                    if (fb != null && (fb.IsSpecialTeamsKick || fb.isKickoffLoose))
                        return fb;
                }
            }
            catch (UnityException) { /* tag missing */ }

            // Inactive / duplicate search — kickoff often parks extras.
            var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (fb.IsSpecialTeamsKick || fb.isKickoffLoose)
                    return fb;
            }

            return null;
        }

        static FootballBehavior FindAnyLiveFootball()
        {
            if (FieldManager.Instance != null && FieldManager.Instance.ballTransform != null)
            {
                var fb = FieldManager.Instance.ballTransform.GetComponent<FootballBehavior>();
                if (fb != null && (fb.isInAir || fb.isBouncing || fb.isKickoffLoose))
                    return fb;
            }

            var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (fb.isInAir || fb.isBouncing || fb.isKickoffLoose || fb.IsSpecialTeamsKick)
                    return fb;
            }
            return null;
        }

        /// <summary>
        /// Hard-lock onto a lofted football (kickoff or pass). Soft smoothSpeed cannot keep up.
        /// </summary>
        void TrackFlightBall(FootballBehavior ball)
        {
            if (ball == null) return;

            Vector3 plane = ball.PlayPlanePosition;
            if (plane.sqrMagnitude < 0.0001f)
                plane = ball.transform.position;
            plane.z = 0f;

            float loft = Mathf.Clamp(ball.FlightHeight * loftFollowFactor, 0f, loftFollowMax);
            // Allow camera into both endzones (world X ≈ ±50+ for abs yards 0/100).
            float lo = Mathf.Min(minX, -55f);
            float hi = Mathf.Max(maxX, 55f);
            float x = Mathf.Clamp(plane.x + offset.x, lo, hi);
            // Same Y/Z framing as aim park (offset), plus loft lift.
            var desired = new Vector3(x, offset.y + loft, offset.z) + shakeOffset;
            transform.position = desired;
        }

        void TrackTarget(bool approachBias, float minSpeed)
        {
            if (target == null) return;

            float x = Mathf.Clamp(target.position.x + offset.x, minX, maxX);
            if (approachBias && FieldManager.Instance != null && FieldManager.Instance.PendingKickoff)
            {
                float midX = RetroLookApplier.YardToX(FormationRoster.KickoffCameraYardAbs);
                x = Mathf.Lerp(x, midX, 0.72f);
            }

            float baseY = followTargetY
                ? Mathf.Clamp(target.position.y + offset.y, minY, maxY)
                : offset.y;
            var desired = new Vector3(x, baseY + loftOffset, offset.z) + shakeOffset;
            float speed = Mathf.Max(smoothSpeed, minSpeed);
            // Kick / return tracking must not crawl when the scene overrides smoothSpeed to ~5.
            float step = Mathf.Max(1f, speed) * Time.deltaTime;
            if (Vector3.Distance(transform.position, desired) <= step)
                transform.position = desired;
            else
                transform.position = Vector3.MoveTowards(transform.position, desired, step);
        }

        void UpdateLoftOffset(bool allowLoft)
        {
            if (!allowLoft)
            {
                ClearLoftImmediate();
                return;
            }

            float loftTarget = 0f;
            if (ballFlight != null && (ballFlight.isInAir || ballFlight.isBouncing))
            {
                loftTarget = Mathf.Clamp(
                    ballFlight.FlightHeight * loftFollowFactor,
                    0f,
                    loftFollowMax);
            }

            loftOffset = Mathf.SmoothDamp(
                loftOffset,
                loftTarget,
                ref loftVelocity,
                Mathf.Max(0.01f, loftSmoothTime));
        }

        void ClearLoftImmediate()
        {
            loftOffset = 0f;
            loftVelocity = 0f;
        }

        void ClearBallFlight()
        {
            ballFlight = null;
            // loftOffset damps to 0 via UpdateLoftOffset — do not snap.
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            followTargetY = false;
            followPlayer = true;
            ClearBallFlight();
        }

        /// <summary>Hard-park the camera on a yard line (kickoff midfield frame).</summary>
        public void SnapToYard(float yard)
        {
            float x = RetroLookApplier.YardToX(yard);
            transform.position = new Vector3(x, offset.y, offset.z);
            followPlayer = false;
            ClearBallFlight();
        }

        public void FollowBall(Transform ball)
        {
            target = ball;
            followTargetY = true;
            followPlayer = true; // clear SnapToYard lock from kickoff aim framing
            ballFlight = ball != null ? ball.GetComponent<FootballBehavior>() : null;
        }

        public void FollowPlayer()
        {
            GameObject player = null;
            try { player = GameObject.FindGameObjectWithTag("Player"); }
            catch (UnityException) { /* tag missing */ }
            if (player == null)
                player = GameObject.Find("Quarterback");
            target = player != null ? player.transform : null;
            followTargetY = false;
            followPlayer = true;
            ClearBallFlight();
        }

        public void SetFollowEnabled(bool enabled) => followPlayer = enabled;

        /// <summary>Short screen punch for tackles / scores / tips.</summary>
        public void Shake(float amplitude, float duration)
        {
            shakeAmp = Mathf.Max(shakeAmp, Mathf.Max(0.01f, amplitude));
            shakeUntil = Mathf.Max(shakeUntil, Time.time + Mathf.Max(0.05f, duration));
        }

        void UpdateShake()
        {
            if (Time.time >= shakeUntil || shakeAmp <= 0.001f)
            {
                shakeOffset = Vector3.zero;
                shakeAmp = 0f;
                return;
            }

            float t = Mathf.Clamp01((shakeUntil - Time.time) / 0.35f);
            float a = shakeAmp * t;
            shakeOffset = new Vector3(
                Random.Range(-a, a),
                Random.Range(-a * 0.6f, a * 0.6f),
                0f);
        }
    }
}
