using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    public enum KickMode
    {
        FieldGoal,
        Punt,
        ExtraPoint,
        Kickoff
    }

    /// <summary>
    /// Tecmo-style kick mini-game: grow/reset power meter, J to kick.
    /// Kickoff/punt: hold W/S at kick for top/bottom lane (no live aim cone).
    /// FG/XP: live W/S aim + green make band.
    /// </summary>
    public class KickingController : MonoBehaviour
    {
        public static KickingController Instance { get; private set; }

        [Header("Aim")]
        [Tooltip("FG / XP aim half-width (across-field Y).")]
        public float maxAimOffset = 3.2f;
        [Tooltip("Kickoff/punt: Y magnitude when holding W (top) or S (bottom) at kick.")]
        [Range(0.5f, 1f)] public float holdLaneSidelineFactor = 0.85f;
        public float aimMouseSensitivity = 0.012f;
        public float aimKeySpeed = 2.8f;
        public float uprightHalfWidth = 1.35f;

        /// <summary>Kickoff / punt — direction is held W/S at the kick press, not a live cone.</summary>
        bool UsesHoldLaneAim => mode == KickMode.Kickoff || mode == KickMode.Punt;

        [Header("Power (Tecmo grow / reset)")]
        [Tooltip("Seconds for the meter to fill 0→1 before wrapping.")]
        public float powerRampSeconds = 1.1f;
        [Tooltip("Kickoff: power ≤ this = onside (blue band).")]
        [Range(0.12f, 0.4f)] public float onsideMax = 0.28f;
        [Range(0.05f, 0.45f)] public float powerSweetMin = 0.55f;
        [Range(0.55f, 0.98f)] public float powerSweetMax = 0.88f;

        public bool IsActive { get; private set; }
        /// <summary>True after kick confirm while the ball is lofting / settling (pre-banner).</summary>
        public bool IsBallInFlight { get; private set; }
        /// <summary>True after kick confirm while the kicker runs up to the tee (pre-flight).</summary>
        public bool IsKickerApproaching { get; private set; }

        [Header("Kickoff Flight")]
        public float kickoffFlightSpeed = 22f;
        public float kickoffReturnerSpeed = 8.5f;
        public float kickoffSettleHold = 0.45f;
        [Tooltip("Weak kicks still clear past coverage into the landing zone (receive wall ≈65).")]
        public float kickoffMinLandYard = 67f;
        [Tooltip("Play-plane radius for a clean mid-air haul by the returner.")]
        public float kickoffAirCatchRadius = 1.45f;
        public float kickoffAirCatchMaxHeight = 1.3f;
        [Tooltip("Scoop radius once the ball is bouncing / grounded.")]
        public float kickoffRecoverRadius = 1.55f;
        [Tooltip("If the loose ball sits untouched this long in the receive EZ → touchback.")]
        public float kickoffTouchbackSettleTime = 0.55f;
        [Tooltip("Safety: force TB (EZ) or spot (field) if nobody scoops.")]
        public float kickoffLooseTimeout = 5.5f;

        [Header("Upright Kick Flight (FG / XP)")]
        [Tooltip("Loft speed toward the uprights after tee contact.")]
        public float uprightFlightSpeed = 18f;
        [Tooltip("Brief hold after the ball reaches the posts before GOOD / NO GOOD.")]
        public float uprightSettleHold = 0.35f;
        [Tooltip("World X of the scoring uprights (matches RetroLookApplier GoalpostWorldXAbs).")]
        public float uprightPostWorldX = 55.4f;

        [Header("Kickoff Approach")]
        [Tooltip("World units/sec the kicker runs +X toward the tee after confirm.")]
        public float kickerApproachSpeed = 9f;
        [Tooltip("Play-plane distance to tee that counts as foot-meets-ball.")]
        public float kickerContactDistance = 1.0f;
        [Tooltip("Launch once kicker X reaches tee X minus this slack (covers overshoot / sprite size).")]
        public float kickerContactXEpsilon = 0.2f;
        [Tooltip("Force loft if contact never registers (seconds after approach starts).")]
        public float kickerApproachTimeout = 1.5f;

        KickMode mode;
        float aimOffset;
        float power01;
        int kickDistanceYards;
        Vector3 kickOrigin;
        int ignoreKickFrames;

        bool pendingTouchback;
        bool isOnsideKick;
        int pendingSpot;
        Vector3 landTarget;
        bool kickoffLandingPrepared;
        bool waitingReturnerSelect;
        Transform returner;
        Transform kicker;
        float settleTimer;
        bool settling;
        float approachStartedAt;
        bool loggedKickoffLaunch;
        /// <summary>Ball bouncing / grounded live after kickoff land — awaiting scoop or TB.</summary>
        bool kickoffLiveBall;
        float kickoffLiveElapsed;
        float kickoffEzSettleTimer;

        // FG / XP result locked at confirm; flight is presentation.
        bool uprightLandingPrepared;
        bool pendingUprightGood;
        UprightMissKind pendingUprightMiss;

        enum UprightMissKind
        {
            None,
            WideLeft,
            WideRight,
            Short
        }

        LineRenderer coneLeft;
        LineRenderer coneRight;
        LineRenderer aimLine;

        GameObject uiRoot;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI hintLabel;
        Image powerFill;
        Image sweetBand;
        Image onsideBand;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("KickingController");
            if (host.GetComponent<KickingController>() == null)
                host.AddComponent<KickingController>();
        }

        public static void Begin(KickMode kickMode)
        {
            EnsureExists();
            Instance.BeginInternal(kickMode);
        }

        /// <summary>Abort kick UI (quarter end / reset) without scoring.</summary>
        public void Cancel()
        {
            AbortKickoffApproach();
            AbortKickoffFlight();
            IsActive = false;
            IsBallInFlight = false;
            IsKickerApproaching = false;
            kickoffLandingPrepared = false;
            uprightLandingPrepared = false;
            settling = false;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            ignoreKickFrames = 0;
            kicker = null;
            SetVisualsActive(false);
            HideUi();
            DisableOrphanKickUi();
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsKickoffCoverage)
                PlayerDefenseController.Instance.Cancel();
            if (GameManager.Instance != null)
                GameManager.Instance.isKicking = false;
            RetroLookApplier.SetGoalpostsVisible(true);
        }

        bool IsUprightKickMode
            => mode == KickMode.ExtraPoint || mode == KickMode.FieldGoal;

        /// <summary>FG / XP mini-game active (aim, approach, or loft) — camera lead toward posts.</summary>
        public bool IsUprightKickCamera
            => IsUprightKickMode && (IsActive || IsKickerApproaching || IsBallInFlight);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            EnsureVisuals();
            EnsureUi();
            SetVisualsActive(false);
            HideUi();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BeginInternal(KickMode kickMode)
        {
            if (FieldManager.Instance == null || GameManager.Instance == null)
                return;

            AbortKickoffApproach();
            AbortKickoffFlight();
            mode = kickMode;
            IsActive = true;
            IsBallInFlight = false;
            IsKickerApproaching = false;
            kickoffLandingPrepared = false;
            uprightLandingPrepared = false;
            settling = false;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            kicker = null;
            aimOffset = 0f;
            power01 = 0f;
            isOnsideKick = false;
            waitingReturnerSelect = false;
            pendingUprightGood = false;
            pendingUprightMiss = UprightMissKind.None;

            kickDistanceYards = mode switch
            {
                KickMode.FieldGoal => FieldManager.Instance.GetFieldGoalDistance(),
                KickMode.ExtraPoint => 33,
                KickMode.Kickoff => 65,
                _ => 40
            };

            // Spot formation before reading tee yard (XP / FG place kicker + ball).
            if (mode == KickMode.ExtraPoint)
                FormationRoster.PlaceExtraPointKick();
            else if (mode == KickMode.FieldGoal)
                FormationRoster.PlaceFieldGoalKick();

            float originYard = mode == KickMode.Kickoff
                ? FormationRoster.KickoffTeeYardAbs
                : FieldManager.Instance.currentYardLine;
            float losX = FieldManager.Instance.YardToWorldX(originYard);
            // Tee / aim origin always on the hashes (Y=0) — cone fans ± into both halves.
            kickOrigin = new Vector3(losX, 0f, 0f);

            // Keep tee ball on the hashes at the kick origin (kickoff / FG / XP).
            // Aim cone uses kickOrigin (tee) — never a goalpost sprite position.
            if (mode == KickMode.Kickoff || IsUprightKickMode)
            {
                RetroLookApplier.RestyleFootball();
                var ballGo = FieldManager.Instance.ballTransform != null
                    ? FieldManager.Instance.ballTransform.gameObject
                    : GameObject.Find("Football");
                if (ballGo == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t == null || t.name != "Football") continue;
                        if (t.hideFlags != HideFlags.None) continue;
                        if (!t.gameObject.scene.IsValid()) continue;
                        ballGo = t.gameObject;
                        break;
                    }
                }
                if (ballGo != null)
                {
                    ballGo.SetActive(true);
                    ballGo.transform.localScale = Vector3.one;
                    var fb = ballGo.GetComponent<FootballBehavior>();
                    if (fb != null)
                        fb.ResetToParked(kickOrigin);
                    else
                        ballGo.transform.position = kickOrigin;
                    FieldManager.Instance.ballTransform = ballGo.transform;
                    RetroLookApplier.RestyleFootball();
                }

                // Kickoff hides posts (tee misread); FG / XP needs uprights visible.
                RetroLookApplier.SetGoalpostsVisible(IsUprightKickMode);
            }
            else if (FieldManager.Instance.ballTransform != null)
            {
                FieldManager.Instance.ballTransform.position = kickOrigin;
            }

            // Keep isPreSnap true while the kick mini-game is up. Kickoff places
            // the QB in the receive wall past tee LOS — clearing pre-snap used to
            // instantly ConvertToScrambleRun and show "QB SCRAMBLE +YDS".
            GameManager.Instance.isKicking = true;
            GameManager.Instance.isPreSnap = true;
            GameManager.Instance.isKickoffReturn = false;
            GameManager.Instance.waitingForNextPlay = false;
            // Don't consume the play-call / intro click as the kick.
            ignoreKickFrames = 12;

            // Player receives: opponent kicks automatically — never put the human
            // on the kick meter (that felt like "I kick" → then control the returner).
            if (mode == KickMode.Kickoff
                && FieldManager.Instance != null
                && FieldManager.Instance.KickoffReceiverIsPlayer)
            {
                BeginOpponentKickoffAuto();
                return;
            }

            EnsureVisuals();
            EnsureUi();
            // Kickoff/punt: power meter only — no live aim cone.
            SetVisualsActive(!UsesHoldLaneAim);
            ShowUi();
            RefreshUi();
        }

        /// <summary>
        /// Opening / after opponent score — AI kicks, player fields the return.
        /// Tecmo: pick returner first, then watch the kick.
        /// </summary>
        void BeginOpponentKickoffAuto()
        {
            IsActive = false;
            ignoreKickFrames = 0;
            // AI kickoff: random top / center / bottom lane (same hold-lane model).
            float lane = HoldLaneYMagnitude();
            float roll = Random.value;
            aimOffset = roll < 0.34f ? lane : (roll < 0.67f ? 0f : -lane);
            power01 = Random.Range(0.58f, 0.92f);
            isOnsideKick = power01 <= onsideMax;

            SetVisualsActive(false);
            HideUi();
            DisableOrphanKickUi();

            waitingReturnerSelect = true;
            KickReturnerSelect.Begin(OnReturnerSelectedForReceive);
        }

        void OnReturnerSelectedForReceive(string unitName)
        {
            waitingReturnerSelect = false;
            FormationRoster.DesignatedKickReturnerName = unitName;
            FormationRoster.PlaceDesignatedKickReturner();
            ArmPlayerReturnerForFielding();
            PlayBanner.Show("RECEIVE!", 1.05f, BannerTone.Neutral);
            BeginKickoffApproach();
        }

        /// <summary>
        /// Player is the receiving team — put control/ring on RB before the ball lands
        /// so fielding is never "AI scoops, then you suddenly take over".
        /// </summary>
        void ArmPlayerReturnerForFielding()
        {
            returner = FindKickoffReturner();
            if (returner == null) return;

            var rc = returner.GetComponent<ReceiverController>();
            if (rc == null)
            {
                rc = returner.gameObject.AddComponent<ReceiverController>();
                if (returner.GetComponent<StaminaSprint>() == null)
                    returner.gameObject.AddComponent<StaminaSprint>();
                PlayerStun.GetOrAdd(returner.gameObject);
            }

            rc.ArmKickoffFielding();
        }

        void Update()
        {
            if (Instance != this) return;

            if (waitingReturnerSelect || KickReturnerSelect.IsOpen)
                return;

            // Meter gone, kicker running to tee — keep gates; no scramble.
            if (IsKickerApproaching)
            {
                TickKickerApproach();
                return;
            }

            // Meter gone, ball flying / live on the ground — kickoff chase+scoop or XP/FG settle.
            if (IsBallInFlight || kickoffLiveBall)
            {
                if (IsUprightKickMode)
                    TickUprightKickFlight();
                else
                    TickKickoffFlight();
                return;
            }

            // Hard guarantee: meter / aim never linger on normal downs or banners.
            bool gmKick = GameManager.Instance != null && GameManager.Instance.isKicking;
            bool bannerUp = GameManager.Instance != null && GameManager.Instance.waitingForNextPlay;
            if (!IsActive || !gmKick || bannerUp)
            {
                if (IsActive && (!gmKick || bannerUp))
                {
                    IsActive = false;
                    if (GameManager.Instance != null)
                        GameManager.Instance.isKicking = false;
                }
                SetVisualsActive(false);
                if (uiRoot != null && uiRoot.activeSelf)
                    HideUi();
                return;
            }

            if (GameManager.Instance.SuppressPlayClick)
                return;

            if (ignoreKickFrames > 0)
                ignoreKickFrames--;

            if (UsesHoldLaneAim)
            {
                // No live aim cone — W/S sampled only when the kick button is pressed.
                aimOffset = 0f;
                SetVisualsActive(false);
            }
            else
            {
                // FG / XP: live W/S / stick Y aim toward the uprights.
                float aimMax = CurrentAimMax();
                aimOffset += Input.GetAxis("Mouse Y") * aimMouseSensitivity * 80f;
                float aimY = TecmoInput.AimY();
                aimOffset += aimY * aimKeySpeed * Time.deltaTime;
                aimOffset = Mathf.Clamp(aimOffset, -aimMax, aimMax);
                UpdateCone();
                SetVisualsActive(true);
            }

            // Tecmo: meter grows then restarts (not a sine ping-pong).
            float ramp = Mathf.Max(0.35f, powerRampSeconds);
            power01 += Time.deltaTime / ramp;
            if (power01 >= 1f)
                power01 -= 1f;

            RefreshUi();

            if (ignoreKickFrames > 0) return;

            // J / East (B) to kick — Space / Enter / click / Start remain alts.
            bool kick = TecmoInput.KickDown()
                        || TecmoInput.ConfirmDown();
            if (kick)
                ResolveKick();
        }

        void ResolveKick()
        {
            if (!IsActive) return;
            IsActive = false;
            ignoreKickFrames = 0;
            SetVisualsActive(false);
            HideUi();
            DisableOrphanKickUi();

            // Kickoff / punt: hold W = top, S = bottom, neither = hashes.
            if (UsesHoldLaneAim)
                SampleHoldLaneAimFromInput();

            WeatherSystem.EnsureExists();
            if (WeatherSystem.Instance != null)
                WeatherSystem.Instance.ApplyKickModifiers(ref aimOffset, ref power01);

            switch (mode)
            {
                case KickMode.FieldGoal:
                case KickMode.ExtraPoint:
                    // Same feel as kickoff: approach → contact loft → banner.
                    // Kick SFX/shake waits for tee contact (BeginUprightKickFlight).
                    BeginUprightKickApproach();
                    break;
                case KickMode.Kickoff:
                    // Hide chrome, run kicker to tee, then loft on contact.
                    // Kick SFX/shake waits for tee contact (BeginKickoffFlight).
                    BeginKickoffApproach();
                    break;
                default:
                    MatchPresentation.Kick();
                    ClearKickingGate();
                    ResolvePunt();
                    break;
            }

            // Banner / flight owns the next click — keep kick chrome dismissed.
            SetVisualsActive(false);
            HideUi();
            DisableOrphanKickUi();
        }

        static void ClearKickingGate()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.isKicking = false;
        }

        void BeginUprightKickApproach()
        {
            if (FieldManager.Instance == null) return;

            PrepareUprightLanding();

            IsKickerApproaching = true;
            IsBallInFlight = false;
            settling = false;
            settleTimer = 0f;
            approachStartedAt = Time.time;
            loggedKickoffLaunch = false;
            kicker = FormationRoster.FindUprightKicker();

            float teeYard = FieldManager.Instance.currentYardLine;
            float dir = FieldManager.Instance.DriveDirX;
            float kickerYard = teeYard - FormationRoster.UprightKickerBehindYards * dir;
            if (kicker != null)
            {
                kicker.gameObject.SetActive(true);
                SetKickerPlayPlanePos(new Vector3(
                    FieldManager.Instance.YardToWorldX(kickerYard),
                    kickOrigin.y,
                    0f));
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            RetroLookApplier.SetGoalpostsVisible(true);

            var ball = GetKickoffBall();
            if (ball != null)
            {
                ball.gameObject.SetActive(true);
                ball.ResetToParked(kickOrigin);
                FieldManager.Instance.ballTransform = ball.transform;
            }

            var cam = Camera.main;
            if (cam != null && kicker != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null)
                    cc.SetTarget(kicker);
            }

            if (kicker == null)
            {
                IsKickerApproaching = false;
                BeginUprightKickFlight("no-kicker");
            }
        }

        void PrepareUprightLanding()
        {
            int dist = mode == KickMode.FieldGoal && FieldManager.Instance != null
                ? FieldManager.Instance.GetFieldGoalDistance()
                : kickDistanceYards;
            if (mode == KickMode.ExtraPoint)
                dist = 33;

            pendingUprightGood = EvaluateKickGood(dist);
            pendingUprightMiss = UprightMissKind.None;

            float postX = AttackEndzonePostWorldX();
            float landX;
            float landY = aimOffset;

            if (pendingUprightGood)
            {
                landX = postX;
                landY = Mathf.Clamp(aimOffset, -uprightHalfWidth * 0.85f, uprightHalfWidth * 0.85f);
            }
            else if (Mathf.Abs(aimOffset) > uprightHalfWidth)
            {
                landX = postX;
                landY = aimOffset;
                // Wide left/right relative to kicking toward the attack endzone.
                float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                pendingUprightMiss = (aimOffset * dir) > 0f
                    ? UprightMissKind.WideLeft
                    : UprightMissKind.WideRight;
            }
            else
            {
                // Short — die before the posts.
                float shortT = Mathf.Lerp(0.52f, 0.82f, Mathf.Clamp01(power01));
                landX = Mathf.Lerp(kickOrigin.x, postX, shortT);
                pendingUprightMiss = UprightMissKind.Short;
            }

            landTarget = new Vector3(landX, landY, 0f);
            uprightLandingPrepared = true;
            returner = null;
            kickoffLandingPrepared = false;
        }

        void BeginUprightKickFlight(string contactReason = "direct")
        {
            if (FieldManager.Instance == null) return;

            IsKickerApproaching = false;
            if (!uprightLandingPrepared)
                PrepareUprightLanding();

            settling = false;
            settleTimer = 0f;
            IsBallInFlight = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            RetroLookApplier.SetGoalpostsVisible(true);

            var ball = GetKickoffBall();
            if (ball == null)
            {
                Debug.LogWarning("[UprightKick] BeginUprightKickFlight: no FootballBehavior — resolving without loft.");
                FinishUprightKick();
                return;
            }

            MatchPresentation.Kick();
            ball.gameObject.SetActive(true);
            ball.enabled = true;
            ball.ResetToParked(kickOrigin);
            FieldManager.Instance.ballTransform = ball.transform;

            float speed = Mathf.Max(8f, uprightFlightSpeed);
            ball.KickToTarget(landTarget, speed, OnUprightBallLanded);

            if (!loggedKickoffLaunch)
            {
                loggedKickoffLaunch = true;
                Debug.Log(
                    $"[UprightKick] Launch ({contactReason}) mode={mode} tee={kickOrigin} → land={landTarget} " +
                    $"good={pendingUprightGood} miss={pendingUprightMiss} speed={speed:0.#}");
            }

            if (!ball.isInAir || !ball.IsSpecialTeamsKick)
            {
                Debug.LogWarning("[UprightKick] KickToTarget did not stay in flight — retrying once.");
                ball.KickToTarget(landTarget, speed, OnUprightBallLanded);
            }
        }

        void OnUprightBallLanded()
        {
            if (!IsBallInFlight || !IsUprightKickMode) return;
            settling = true;
            settleTimer = Mathf.Max(0.15f, uprightSettleHold);
        }

        void TickUprightKickFlight()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            SetVisualsActive(false);
            if (uiRoot != null && uiRoot.activeSelf)
                HideUi();

            if (!settling) return;

            settleTimer -= Time.deltaTime;
            if (settleTimer > 0f) return;

            settling = false;
            FinishUprightKick();
        }

        void FinishUprightKick()
        {
            IsBallInFlight = false;
            IsKickerApproaching = false;
            uprightLandingPrepared = false;
            settling = false;
            ClearKickingGate();

            if (FieldManager.Instance == null) return;

            bool good = pendingUprightGood;
            if (mode == KickMode.ExtraPoint)
            {
                FieldManager.Instance.ResolveExtraPoint(good);
                string msg = good
                    ? "EXTRA POINT\nGOOD"
                    : pendingUprightMiss switch
                    {
                        UprightMissKind.WideLeft => "EXTRA POINT\nNO GOOD\nWIDE LEFT",
                        UprightMissKind.WideRight => "EXTRA POINT\nNO GOOD\nWIDE RIGHT",
                        _ => "EXTRA POINT\nNO GOOD\nSHORT"
                    };
                PlayBanner.ShowPlayOver(msg, good ? BannerTone.Positive : BannerTone.Neutral);
                return;
            }

            // Field goal
            int dist = FieldManager.Instance.GetFieldGoalDistance();
            FieldManager.Instance.ResolveFieldGoal(good, dist);
            string fgMsg;
            if (good)
                fgMsg = $"FIELD GOAL\nGOOD\n({dist} YDS)";
            else if (pendingUprightMiss == UprightMissKind.WideLeft)
                fgMsg = $"FIELD GOAL\nNO GOOD\nWIDE LEFT";
            else if (pendingUprightMiss == UprightMissKind.WideRight)
                fgMsg = $"FIELD GOAL\nNO GOOD\nWIDE RIGHT";
            else
                fgMsg = $"FIELD GOAL\nNO GOOD\nSHORT";
            PlayBanner.ShowPlayOver(fgMsg, good ? BannerTone.Positive : BannerTone.Neutral);
        }

        void BeginKickoffApproach()
        {
            if (FieldManager.Instance == null) return;

            // Lock landing from the chosen aim/power now — flight uses these on contact.
            PrepareKickoffLanding();

            IsKickerApproaching = true;
            IsBallInFlight = false;
            settling = false;
            settleTimer = 0f;
            approachStartedAt = Time.time;
            loggedKickoffLaunch = false;
            kicker = FormationRoster.FindKickoffKicker();
            if (kicker != null)
            {
                kicker.gameObject.SetActive(true);
                // Snap onto the hashes behind the tee so aim→approach always starts clean.
                SetKickerPlayPlanePos(new Vector3(
                    FieldManager.Instance.YardToWorldX(FormationRoster.KickoffKickerYardAbs),
                    kickOrigin.y,
                    0f));
            }

            // Keep isKicking + isPreSnap through approach (scramble / play-call gates).
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            // Tee ball stays parked until contact; camera keeps midfield kickoff frame
            // so coverage / receive walls stay visible during the run-up.
            var ball = GetKickoffBall();
            if (ball != null)
            {
                ball.gameObject.SetActive(true);
                ball.ResetToParked(kickOrigin);
                if (FieldManager.Instance != null)
                    FieldManager.Instance.ballTransform = ball.transform;
            }

            // Player receive — camera stays on the returner (armed). Player kick —
            // arm a cover gunner so you can tackle the AI returner.
            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            if (!playerReceives)
            {
                if (PlayerDefenseController.Instance != null)
                    PlayerDefenseController.Instance.BeginKickoffCoverage();
                else if (kicker != null)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var cc = cam.GetComponent<CameraController>();
                        if (cc != null)
                            cc.SetTarget(kicker);
                    }
                }
            }

            // Missing kicker → launch immediately so the match cannot soft-lock.
            if (kicker == null)
            {
                IsKickerApproaching = false;
                BeginKickoffFlight("no-kicker");
            }
        }

        void TickKickerApproach()
        {
            // Hard-hold special-teams gates for the whole run-up.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            // Meter / cone must stay dismissed during approach.
            SetVisualsActive(false);
            if (uiRoot != null && uiRoot.activeSelf)
                HideUi();

            // Kickoff: returner breaks to the locked landing spot while the kicker runs up.
            if (mode == KickMode.Kickoff && kickoffLandingPrepared)
                TickKickoffReturnerChase(GetKickoffBall());

            // Authoritative tee is kickOrigin (hashes). Ball transform can lag / desync
            // from a duplicate Football and starve the distance check forever.
            Vector3 tee = new Vector3(kickOrigin.x, kickOrigin.y, 0f);

            if (kicker != null)
            {
                // Readable +X run-up toward the tee (hashes). Keep Y locked to tee.
                // Sync Rigidbody.position — transform-only writes can stall short of contact
                // when DefenderAI / ArcadeMove touch the kinematic body the same frame.
                var pos = kicker.position;
                var aim = tee;
                var next = Vector3.MoveTowards(
                    new Vector3(pos.x, aim.y, 0f),
                    aim,
                    Mathf.Max(1f, kickerApproachSpeed) * Time.deltaTime);
                SetKickerPlayPlanePos(next);
            }

            bool launched = TryLaunchFromApproach(tee);
            if (!launched && kicker == null)
            {
                if (IsUprightKickMode)
                    BeginUprightKickFlight("kicker-lost");
                else
                    BeginKickoffFlight("kicker-lost");
            }
        }

        /// <summary>
        /// Contact = generous radius OR kicker has reached/passed tee X OR approach timeout.
        /// Visual circles + football sprite overlap (~0.85) before the old 0.55 center check.
        /// </summary>
        bool TryLaunchFromApproach(Vector3 tee)
        {
            float contactR = Mathf.Max(0.8f, kickerContactDistance);
            float xEps = Mathf.Max(0.05f, kickerContactXEpsilon);
            float timeout = Mathf.Max(0.35f, kickerApproachTimeout);

            float dist = kicker != null
                ? Vector2.Distance(
                    new Vector2(kicker.position.x, kicker.position.y),
                    new Vector2(tee.x, tee.y))
                : 0f;

            bool byRadius = kicker == null || dist <= contactR;
            // Approach from behind the tee along kick/drive direction.
            float approachDir = mode == KickMode.Kickoff
                ? (FieldManager.Instance != null ? FieldManager.Instance.KickDirX : 1f)
                : (FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f);
            bool byX = kicker != null
                       && (kicker.position.x - tee.x) * approachDir >= -xEps;
            bool byTimeout = (Time.time - approachStartedAt) >= timeout;

            if (!byRadius && !byX && !byTimeout)
                return false;

            string reason = byRadius ? "radius" : byX ? "x-cross" : "timeout";
            IsKickerApproaching = false;
            if (IsUprightKickMode)
                BeginUprightKickFlight(reason);
            else
                BeginKickoffFlight(reason);
            return true;
        }

        void SetKickerPlayPlanePos(Vector3 worldPos)
        {
            if (kicker == null) return;
            worldPos.z = 0f;
            kicker.position = worldPos;
            var rb = kicker.GetComponent<Rigidbody>();
            if (rb != null)
            {
                ArcadeMove.ConfigureKinematicBody(rb);
                rb.position = worldPos;
            }
        }

        void PrepareKickoffLanding()
        {
            // Hold-lane W/S is intentional — do not treat sideline kicks as "bad aim".
            bool aimedOk = UsesHoldLaneAim
                           || Mathf.Abs(aimOffset) <= uprightHalfWidth * 1.6f;
            isOnsideKick = power01 <= onsideMax;
            bool deep = !isOnsideKick && power01 >= 0.7f && aimedOk;
            int kickDir = FieldManager.Instance != null
                ? FieldManager.NormDir(FieldManager.Instance.kickDirection)
                : 1;
            float wallYard = FormationRoster.KickoffReceiveWallYardAbs;
            float coverYard = FormationRoster.KickoffCoverageYardAbs;
            // Deep landings into / near the end line so bounce can exit for a touchback.
            float endDepth = FieldManager.KickReceiveEndLineDepthYards;
            float deepNear = kickDir > 0 ? 100.2f : -0.2f;
            float deepFar = kickDir > 0 ? 100f + endDepth - 0.4f : -(endDepth - 0.4f);
            float shallowFar = kickDir > 0 ? 94f : 6f;

            // Must clear past coverage to the landing zone / receive wall.
            float minYard = kickDir > 0
                ? Mathf.Max(kickoffMinLandYard, wallYard)
                : Mathf.Min(100f - kickoffMinLandYard, wallYard);

            float landYard;
            if (isOnsideKick)
            {
                // Tecmo blue band — short kick just past the coverage wall.
                pendingTouchback = false;
                float near = coverYard + 3f * kickDir;
                float far = coverYard + 12f * kickDir;
                float t = Mathf.InverseLerp(0f, onsideMax, power01);
                landYard = Mathf.Lerp(near, far, t);
                if (!aimedOk)
                    landYard -= kickDir * Random.Range(2f, 6f);
                pendingSpot = Mathf.Clamp(
                    FieldManager.Instance.ReceivingOwnYardsFromAbs(Mathf.RoundToInt(landYard)),
                    40,
                    55);
            }
            else if (deep)
            {
                // Deep into the receive endzone → bounce / TB if left untouched.
                pendingTouchback = true;
                pendingSpot = FieldManager.KickoffTouchbackOwnYard;
                landYard = Mathf.Lerp(deepNear, deepFar, Mathf.InverseLerp(0.7f, 1f, power01));
            }
            else
            {
                pendingTouchback = false;
                // Catchable / scoopable landing at or beyond the landing-zone line.
                float t = Mathf.InverseLerp(onsideMax, 1f, power01);
                landYard = Mathf.Lerp(minYard, shallowFar, t);
                if (!aimedOk)
                    landYard -= kickDir * Random.Range(4f, 10f);
                landYard = kickDir > 0
                    ? Mathf.Max(minYard, landYard)
                    : Mathf.Min(minYard, landYard);
                // Receiving-team own-yard estimate if return never starts (ResolveKickoff).
                pendingSpot = Mathf.Clamp(
                    FieldManager.Instance.ReceivingOwnYardsFromAbs(Mathf.RoundToInt(landYard)),
                    15,
                    45);
            }

            landTarget = new Vector3(
                FieldManager.Instance.YardToWorldX(landYard),
                aimOffset,
                0f);
            returner = FindKickoffReturner();
            kickoffLandingPrepared = true;
        }

        void BeginKickoffFlight(string contactReason = "direct")
        {
            if (FieldManager.Instance == null) return;

            IsKickerApproaching = false;

            if (!kickoffLandingPrepared)
                PrepareKickoffLanding();

            settling = false;
            settleTimer = 0f;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            IsBallInFlight = true;

            // Keep isKicking + isPreSnap through flight (scramble / play-call gates).
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            var ball = GetKickoffBall();
            if (ball == null)
            {
                // No ball — fall back to instant resolve so the match cannot soft-lock.
                Debug.LogWarning("[Kickoff] BeginKickoffFlight: no FootballBehavior — spotting without loft.");
                FinishTouchbackOrSpot(forceTouchback: pendingTouchback);
                return;
            }

            MatchPresentation.Kick();
            if (isOnsideKick)
                PlayBanner.Show("ONSIDE!", 1.1f, BannerTone.Tip);

            ball.gameObject.SetActive(true);
            ball.enabled = true;
            ball.ResetToParked(kickOrigin);
            if (FieldManager.Instance != null)
                FieldManager.Instance.ballTransform = ball.transform;

            float speed = Mathf.Max(8f, kickoffFlightSpeed);
            if (isOnsideKick)
                speed *= 0.72f;
            ball.KickToTarget(landTarget, speed, OnKickoffBallLanded);

            // Follow the football through loft + live bounce until the returner scoops.
            FollowKickoffBall(ball);

            if (!loggedKickoffLaunch)
            {
                loggedKickoffLaunch = true;
                Debug.Log(
                    $"[Kickoff] Launch ({contactReason}) tee={kickOrigin} → land={landTarget} " +
                    $"inAir={ball.isInAir} special={ball.IsSpecialTeamsKick} speed={speed:0.#}");
            }

            // KickToTarget must leave the tee — if something cleared flight flags, force once.
            if (!ball.isInAir || !ball.IsSpecialTeamsKick)
            {
                Debug.LogWarning("[Kickoff] KickToTarget did not stay in flight — retrying once.");
                ball.KickToTarget(landTarget, speed, OnKickoffBallLanded);
                FollowKickoffBall(ball);
            }
        }

        static void FollowKickoffBall(FootballBehavior ball)
        {
            if (ball == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.FollowBall(ball.transform);
        }

        static void FollowKickoffReturner(Transform returner)
        {
            if (returner == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.SetTarget(returner);
        }

        void AbortKickoffApproach()
        {
            if (!IsKickerApproaching) return;
            IsKickerApproaching = false;
            kicker = null;
            approachStartedAt = 0f;
        }

        void TickKickoffFlight()
        {
            // Hard-hold special-teams gates until pickup / touchback resolves.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKickoffReturn = false;
            }

            SetVisualsActive(false);
            if (uiRoot != null && uiRoot.activeSelf)
                HideUi();

            var ball = GetKickoffBall();

            // Mid-air haul — returner in the descending window.
            if (ball != null && ball.IsSpecialTeamsKick && ball.isInAir
                && TryKickoffAirCatch(ball))
                return;

            // Drive returner to projected land / live ball continuously.
            TickKickoffReturnerChase(ball);

            // Keep camera locked on the football every flight tick (aim SnapToYard can stick).
            if (ball != null)
                FollowKickoffBall(ball);

            // Live bounce / grounded scoop phase.
            if (kickoffLiveBall)
            {
                kickoffLiveElapsed += Time.deltaTime;

                // Bounce out the back of the EZ or sideline OOB in the EZ → touchback.
                if (TryKickoffBounceOutTouchback(ball))
                    return;

                // Midfield sideline OOB — spot receiving team there (not a touchback).
                if (TryKickoffMidfieldOutOfBounds(ball))
                    return;

                if (TryKickoffPickup(ball))
                    return;

                if (TryKickoffTouchbackSettle(ball))
                    return;

                if (kickoffLiveElapsed >= Mathf.Max(2f, kickoffLooseTimeout))
                {
                    // Safety: EZ → TB, else spot near the ball for AI / soft-lock.
                    bool inEz = ball != null && FieldManager.Instance != null
                                && FieldManager.Instance.IsInKickReceiveEndzone(ball.PlayPlanePosition.x);
                    if (inEz || pendingTouchback)
                        FinishTouchbackOrSpot(forceTouchback: true);
                    else
                        FinishTouchbackOrSpot(forceTouchback: false, spotOverride: SpotFromBall(ball));
                }
                return;
            }

            // Legacy settle path unused for catchable kickoffs (kept for rare no-ball fallback).
            if (!settling) return;

            settleTimer -= Time.deltaTime;
            if (settleTimer > 0f) return;

            settling = false;
            if (pendingTouchback)
                FinishTouchbackOrSpot(forceTouchback: true);
            else
                BeginKickoffReturnPlay();
        }

        void TickKickoffReturnerChase(FootballBehavior ball)
        {
            if (returner == null || !returner.gameObject.activeInHierarchy)
                returner = FindKickoffReturner();
            if (returner == null) return;

            returner.gameObject.SetActive(true);

            // Player-receive + AI: auto-break to the landing spot (approach + loft).
            // Human WASD starts after the catch (BeginKickoffReturnPlay).
            var fielding = returner.GetComponent<ReceiverController>();
            if (fielding != null && fielding.hasBall)
                return;

            // Default: sprint to the locked landing spot (set at kick confirm).
            Vector3 aim = landTarget;
            if (ball != null)
            {
                if (ball.isKickoffLoose || ball.isBouncing)
                {
                    // Grounded / skittering — chase the live ball.
                    aim = ball.PlayPlanePosition;
                }
                else if (ball.IsSpecialTeamsKick && ball.isInAir)
                {
                    // Commit to landing for most of the loft; only bias to the ball late.
                    Vector3 land = ball.ThrowTarget.sqrMagnitude > 0.01f
                        ? ball.ThrowTarget
                        : landTarget;
                    float t = Mathf.Clamp01(ball.FlightT);
                    aim = t < 0.72f
                        ? land
                        : Vector3.Lerp(land, ball.PlayPlanePosition, Mathf.InverseLerp(0.72f, 1f, t));
                }
            }

            // Deep kick: don't camp in the EZ until the ball is actually there.
            if (pendingTouchback && FieldManager.Instance != null
                && (ball == null || ball.IsSpecialTeamsKick || IsKickerApproaching))
            {
                float holdYard = FieldManager.Instance.kickDirection > 0 ? 98f : 2f;
                float holdX = FieldManager.Instance.YardToWorldX(holdYard);
                if (FieldManager.Instance.kickDirection > 0)
                {
                    if (aim.x > holdX)
                        aim = new Vector3(holdX, aim.y, 0f);
                }
                else if (aim.x < holdX)
                {
                    aim = new Vector3(holdX, aim.y, 0f);
                }
            }

            aim.z = 0f;
            float speed = Mathf.Max(6.5f, kickoffReturnerSpeed);
            // Close the last few yards a bit faster so they get under the ball.
            float dist = Vector2.Distance(
                new Vector2(returner.position.x, returner.position.y),
                new Vector2(aim.x, aim.y));
            if (dist > 2.5f)
                speed *= 1.15f;

            var next = Vector3.MoveTowards(
                returner.position,
                aim,
                speed * Time.deltaTime);
            next.z = 0f;
            returner.position = next;
            var rb = returner.GetComponent<Rigidbody>();
            if (rb != null)
            {
                ArcadeMove.ConfigureKinematicBody(rb);
                rb.position = next;
            }

            // Face the landing lane.
            var sr = returner.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && Mathf.Abs(aim.x - returner.position.x) > 0.05f)
                sr.flipX = aim.x < returner.position.x;
        }

        bool TryKickoffAirCatch(FootballBehavior ball)
        {
            if (returner == null || ball == null) return false;
            if (!ball.TryCatchKickoffInAir(returner, kickoffAirCatchRadius, kickoffAirCatchMaxHeight))
                return false;

            BeginKickoffReturnPlay();
            return true;
        }

        bool TryKickoffPickup(FootballBehavior ball)
        {
            if (returner == null || ball == null || !ball.isKickoffLoose) return false;
            if (!ball.TryPickupKickoff(returner, kickoffRecoverRadius))
                return false;

            BeginKickoffReturnPlay();
            return true;
        }

        bool TryKickoffBounceOutTouchback(FootballBehavior ball)
        {
            if (ball == null || FieldManager.Instance == null) return false;
            if (!ball.isKickoffLoose) return false;

            // Check plane + a hair of visual loft so hops past the end line still count.
            Vector3 plane = ball.PlayPlanePosition;
            if (!FieldManager.Instance.IsKickoffLooseTouchback(plane)
                && !FieldManager.Instance.IsPastKickReceiveEndLine(plane.x))
                return false;

            ball.CancelSpecialTeamsKick();
            FinishTouchbackOrSpot(forceTouchback: true);
            return true;
        }

        bool TryKickoffMidfieldOutOfBounds(FootballBehavior ball)
        {
            if (ball == null || FieldManager.Instance == null) return false;
            if (!ball.isKickoffLoose) return false;
            if (!FieldManager.Instance.IsOutOfBounds(ball.PlayPlanePosition))
                return false;
            // EZ OOB handled as touchback above.
            if (FieldManager.Instance.IsKickoffLooseTouchback(ball.PlayPlanePosition))
                return false;

            ball.CancelSpecialTeamsKick();
            int spot = SpotFromBall(ball);
            FinishTouchbackOrSpot(forceTouchback: false, spotOverride: Mathf.Clamp(spot, 15, 45));
            return true;
        }

        bool TryKickoffTouchbackSettle(FootballBehavior ball)
        {
            if (ball == null || FieldManager.Instance == null) return false;
            if (!ball.isKickoffLoose) return false;

            bool inEz = FieldManager.Instance.IsInKickReceiveEndzone(ball.PlayPlanePosition.x)
                        || FieldManager.Instance.IsPastKickReceiveEndLine(ball.PlayPlanePosition.x);
            if (!inEz)
            {
                kickoffEzSettleTimer = 0f;
                return false;
            }

            // Bounce still hopping — wait until it settles, then a short hold.
            if (ball.isBouncing)
            {
                kickoffEzSettleTimer = 0f;
                return false;
            }

            kickoffEzSettleTimer += Time.deltaTime;
            if (kickoffEzSettleTimer < Mathf.Max(0.2f, kickoffTouchbackSettleTime))
                return false;

            // Untouched in the endzone → touchback at the 35.
            ball.CancelSpecialTeamsKick();
            FinishTouchbackOrSpot(forceTouchback: true);
            return true;
        }

        static int SpotFromBall(FootballBehavior ball)
        {
            if (ball == null || FieldManager.Instance == null)
                return FieldManager.KickoffTouchbackOwnYard;
            // Receiving-team own yard from absolute field yard (uses kickDirection).
            int absYard = FieldManager.Instance.WorldXToYard(ball.PlayPlanePosition.x);
            return Mathf.Clamp(FieldManager.Instance.ReceivingOwnYardsFromAbs(absYard), 1, 50);
        }

        void OnKickoffBallLanded()
        {
            if (!IsBallInFlight && !kickoffLiveBall) return;

            var ball = GetKickoffBall();
            if (ball == null)
            {
                FinishTouchbackOrSpot(forceTouchback: pendingTouchback);
                return;
            }

            // Ground contact always bounces — mid-air hauls are handled in TickKickoffFlight.
            Vector3 land = ball.PlayPlanePosition.sqrMagnitude > 0.01f
                ? ball.PlayPlanePosition
                : landTarget;

            // Already out the back / EZ sideline on first contact → dead-ball touchback.
            if (FieldManager.Instance != null
                && FieldManager.Instance.IsKickoffLooseTouchback(land))
            {
                ball.CancelSpecialTeamsKick();
                FinishTouchbackOrSpot(forceTouchback: true);
                return;
            }

            Vector3 dir = landTarget - kickOrigin;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                float kd = FieldManager.Instance != null ? FieldManager.Instance.KickDirX : 1f;
                dir = Vector3.right * kd;
            }
            // Deep into the EZ: bias bounce harder toward the end line.
            if (FieldManager.Instance != null
                && FieldManager.Instance.IsInKickReceiveEndzone(land.x))
            {
                float kd = FieldManager.Instance.KickDirX;
                dir = (dir.normalized * 0.45f + Vector3.right * (kd * 1.1f)).normalized;
            }

            ball.BeginKickoffLiveBounce(land, dir.normalized);
            FollowKickoffBall(ball);

            IsBallInFlight = true;
            kickoffLiveBall = true;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            settling = false;
        }

        void BeginKickoffReturnPlay()
        {
            IsBallInFlight = false;
            IsKickerApproaching = false;
            kickoffLandingPrepared = false;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            settling = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isKicking = false;
                GameManager.Instance.isPreSnap = false;
                GameManager.Instance.isKickoffReturn = true;
                GameManager.Instance.waitingForNextPlay = false;
            }

            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;

            if (FieldManager.Instance != null)
            {
                // Possession belongs to the receiving team for the return.
                // Clear pending kickoff so banner continue cannot re-enter BeginKickoff.
                FieldManager.Instance.ConsumePendingKickoff();
                FieldManager.Instance.isPlayerPossession = playerReceives;
            }

            // Re-resolve at catch — never hand the ball to a stale opening-KO RB after a player kick.
            returner = FindKickoffReturner();
            if (returner == null)
            {
                int spot = SpotFromBall(GetKickoffBall());
                spot = Mathf.Clamp(spot, 15, 40);
                FinishTouchbackOrSpot(forceTouchback: false, spotOverride: spot);
                return;
            }

            // Player kicked — keep AI QB/offense possession systems off during the return.
            if (!playerReceives && DefensePlayDirector.Instance != null)
                DefensePlayDirector.SetAiOffensePossession(false);

            // Opponent S is DefenderAI-only until EnsureKickoffReturner / here adds RC.
            var rc = returner.GetComponent<ReceiverController>();
            if (rc == null)
            {
                rc = returner.gameObject.AddComponent<ReceiverController>();
                if (returner.GetComponent<StaminaSprint>() == null)
                    returner.gameObject.AddComponent<StaminaSprint>();
                PlayerStun.GetOrAdd(returner.gameObject);
            }

            // Ball → returner. Camera stays on the cover gunner when player kicked.
            if (playerReceives
                || PlayerDefenseController.Instance == null
                || !PlayerDefenseController.Instance.IsKickoffCoverage)
                FollowKickoffReturner(returner);

            // Absolute field yard of the catch.
            int catchYard;
            if (FieldManager.Instance != null)
                catchYard = FieldManager.Instance.WorldXToYard(returner.position.x);
            else
                catchYard = Mathf.Clamp(100 - pendingSpot, 50, 99);
            rc.BeginKickoffReturn(catchYard);

            // Re-assert gunner camera/ring after returner setup.
            if (!playerReceives
                && PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsKickoffCoverage
                && PlayerDefenseController.Instance.ControlledUnit != null)
            {
                var gunner = PlayerDefenseController.Instance.ControlledUnit;
                var cam = Camera.main;
                if (cam != null)
                {
                    var cc = cam.GetComponent<CameraController>();
                    if (cc != null) cc.SetTarget(gunner);
                }
                var ring = GameObject.Find("SelectionRing");
                if (ring != null)
                {
                    var sr = ring.GetComponent<SelectionRing>();
                    if (sr != null) sr.SetFollow(gunner);
                }
            }
        }

        void FinishTouchbackOrSpot(bool forceTouchback, int spotOverride = -1)
        {
            IsBallInFlight = false;
            IsKickerApproaching = false;
            kickoffLandingPrepared = false;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            settling = false;
            ClearKickingGate();
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsKickoffCoverage)
                PlayerDefenseController.Instance.Cancel();
            if (GameManager.Instance != null)
                GameManager.Instance.isKickoffReturn = false;

            if (FieldManager.Instance == null) return;

            if (forceTouchback)
            {
                int tb = FieldManager.KickoffTouchbackOwnYard;
                FieldManager.Instance.ResolveKickoff(tb, true);
                PlayBanner.ShowPlayOver($"TOUCHBACK\nBALL AT THE {tb}", BannerTone.Neutral);
                return;
            }

            int spot = spotOverride > 0 ? spotOverride : pendingSpot;
            FieldManager.Instance.ResolveKickoff(spot, false);
            PlayBanner.ShowPlayOver($"KICKOFF\nBALL AT THE {spot}", BannerTone.Neutral);
        }

        void AbortKickoffFlight()
        {
            if (!IsBallInFlight && !settling && !kickoffLiveBall) return;
            IsBallInFlight = false;
            kickoffLandingPrepared = false;
            uprightLandingPrepared = false;
            kickoffLiveBall = false;
            kickoffLiveElapsed = 0f;
            kickoffEzSettleTimer = 0f;
            settling = false;
            settleTimer = 0f;

            var ball = GetKickoffBall();
            if (ball != null && (ball.IsSpecialTeamsKick || ball.isKickoffLoose))
                ball.CancelSpecialTeamsKick();
        }

        static Transform FindKickoffReturner()
        {
            // Explicit flag only — missing FieldManager must not pick the player RB.
            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            string name = playerReceives
                ? (string.IsNullOrEmpty(FormationRoster.DesignatedKickReturnerName)
                    ? "RB"
                    : FormationRoster.DesignatedKickReturnerName)
                : "S";
            var go = GameObject.Find(name);
            if (go != null) return go.transform;

            // Inactive search fallback.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == name)
                    return t;
            }
            return null;
        }

        static FootballBehavior GetKickoffBall()
        {
            if (FieldManager.Instance != null && FieldManager.Instance.ballTransform != null)
            {
                var fb = FieldManager.Instance.ballTransform.GetComponent<FootballBehavior>();
                if (fb != null) return fb;
            }

            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null)
                {
                    var fb = go.GetComponent<FootballBehavior>();
                    if (fb != null) return fb;
                }
            }
            catch { /* tag missing */ }

            var named = GameObject.Find("Football");
            return named != null ? named.GetComponent<FootballBehavior>() : null;
        }

        bool EvaluateKickGood(int dist)
        {
            float needPower = Mathf.Clamp01(0.42f + dist / 95f);
            bool powerOk = power01 >= needPower * 0.85f
                           && power01 <= Mathf.Lerp(powerSweetMax, 0.98f, dist / 70f);
            bool aimOk = Mathf.Abs(aimOffset) <= uprightHalfWidth
                         * Mathf.Lerp(1.15f, 0.65f, Mathf.Clamp01(dist / 60f));
            float clutch = Random.Range(0f, 0.08f);
            return powerOk && aimOk && power01 + clutch >= needPower * 0.9f;
        }

        void ResolvePunt()
        {
            // Grow meter → distance (Tecmo: fuller bar = longer punt).
            // W/S lane is flavor for now — ResolvePunt spots by yards only.
            float p = Mathf.Clamp01(power01);
            int yards = Mathf.RoundToInt(Mathf.Lerp(26f, 62f, p));

            FieldManager.Instance.ResolvePunt(yards);
            string side = aimOffset > 0.5f ? "\nTOP" : (aimOffset < -0.5f ? "\nBOTTOM" : "");
            PlayBanner.ShowPlayOver($"PUNT\n{yards} YARDS{side}", BannerTone.Neutral);
        }

        /// <summary>
        /// NES-style: while kicking, hold W/Up = top lane, S/Down = bottom, neither = hashes.
        /// </summary>
        void SampleHoldLaneAimFromInput()
        {
            float lane = HoldLaneYMagnitude();
            float aimY = TecmoInput.AimY();
            if (aimY > 0.1f)
                aimOffset = lane;
            else if (aimY < -0.1f)
                aimOffset = -lane;
            else
                aimOffset = 0f;
        }

        float HoldLaneYMagnitude()
        {
            float sideline = FieldManager.Instance != null
                ? FieldManager.Instance.sidelineHalf
                : RetroLookApplier.SidelineY;
            float maxY = Mathf.Max(2.2f, sideline - FormationRoster.BodyRadius - 0.5f);
            return maxY * Mathf.Clamp01(holdLaneSidelineFactor);
        }

        float CurrentAimMax()
        {
            // FG / XP live aim only.
            return maxAimOffset;
        }

        /// <summary>World X of uprights for the attack endzone (driveDirection).</summary>
        float AttackEndzonePostWorldX()
        {
            float abs = RetroLookApplier.GoalpostWorldXAbs;
            if (abs > 1f)
                uprightPostWorldX = abs;
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            return dir >= 0f ? uprightPostWorldX : -uprightPostWorldX;
        }

        void UpdateCone()
        {
            // Live aim cone is FG / XP only (kickoff/punt use hold-lane W/S).
            if (FieldManager.Instance == null || UsesHoldLaneAim) return;

            float goalX = AttackEndzonePostWorldX();
            Vector3 origin = kickOrigin;
            Vector3 tip = new Vector3(goalX, aimOffset, 0f);
            float halfOpen = Mathf.Lerp(0.55f, 1.8f, 1f - power01);

            float lim = CurrentAimMax();
            Vector3 left = new Vector3(goalX, Mathf.Clamp(aimOffset + halfOpen, -lim, lim), 0f);
            Vector3 right = new Vector3(goalX, Mathf.Clamp(aimOffset - halfOpen, -lim, lim), 0f);

            SetLine(aimLine, origin, tip, new Color(1f, 1f, 0.35f, 0.95f));
            SetLine(coneLeft, origin, left, new Color(1f, 0.55f, 0.2f, 0.75f));
            SetLine(coneRight, origin, right, new Color(1f, 0.55f, 0.2f, 0.75f));
        }

        static void SetLine(LineRenderer lr, Vector3 a, Vector3 b, Color c)
        {
            if (lr == null) return;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startColor = c;
            lr.endColor = c;
        }

        void EnsureVisuals()
        {
            if (aimLine != null) return;
            aimLine = CreateLine("KickAimLine", 0.07f);
            coneLeft = CreateLine("KickConeLeft", 0.05f);
            coneRight = CreateLine("KickConeRight", 0.05f);
        }

        static LineRenderer CreateLine(string name, float width)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.6f;
            lr.numCapVertices = 2;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 40;
            lr.useWorldSpace = true;
            return lr;
        }

        void SetVisualsActive(bool on)
        {
            if (aimLine != null) aimLine.gameObject.SetActive(on);
            if (coneLeft != null) coneLeft.gameObject.SetActive(on);
            if (coneRight != null) coneRight.gameObject.SetActive(on);
        }

        void EnsureUi()
        {
            // Reclaim DDOL KickUi after domain reload / lost refs.
            if (uiRoot == null)
            {
                var existing = transform.Find("KickCanvas/KickUi");
                if (existing != null)
                    uiRoot = existing.gameObject;
            }

            if (uiRoot != null)
            {
                if (titleLabel == null || powerFill == null)
                    RebindUiRefs();
                return;
            }

            // Own canvas on the DDOL host so HideUi always targets the live meter
            // (scene-parented KickUi was getting destroyed / orphaned across loads).
            var canvasTf = transform.Find("KickCanvas");
            if (canvasTf == null)
            {
                var go = new GameObject("KickCanvas");
                go.transform.SetParent(transform, false);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;
                go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                go.AddComponent<GraphicRaycaster>();
                canvasTf = go.transform;
            }

            DisableOrphanKickUi();

            uiRoot = new GameObject("KickUi");
            uiRoot.transform.SetParent(canvasTf, false);
            var rootRt = uiRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            titleLabel = MakeLabel(uiRoot.transform, "KickTitle", new Vector2(0.5f, 0.88f), 42f);
            hintLabel = MakeLabel(uiRoot.transform, "KickHint", new Vector2(0.5f, 0.18f), 26f);

            var meterGo = new GameObject("PowerMeter");
            meterGo.transform.SetParent(uiRoot.transform, false);
            var meterRt = meterGo.AddComponent<RectTransform>();
            meterRt.anchorMin = new Vector2(0.5f, 0.08f);
            meterRt.anchorMax = new Vector2(0.5f, 0.08f);
            meterRt.pivot = new Vector2(0.5f, 0.5f);
            meterRt.sizeDelta = new Vector2(420f, 28f);
            var bg = meterGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

            var sweetGo = new GameObject("Sweet");
            sweetGo.transform.SetParent(meterGo.transform, false);
            var sweetRt = sweetGo.AddComponent<RectTransform>();
            sweetRt.anchorMin = new Vector2(powerSweetMin, 0.15f);
            sweetRt.anchorMax = new Vector2(powerSweetMax, 0.85f);
            sweetRt.offsetMin = Vector2.zero;
            sweetRt.offsetMax = Vector2.zero;
            sweetBand = sweetGo.AddComponent<Image>();
            sweetBand.color = new Color(0.35f, 0.9f, 0.4f, 0.45f);

            var onsideGo = new GameObject("Onside");
            onsideGo.transform.SetParent(meterGo.transform, false);
            var onsideRt = onsideGo.AddComponent<RectTransform>();
            onsideRt.anchorMin = new Vector2(0f, 0.15f);
            onsideRt.anchorMax = new Vector2(onsideMax, 0.85f);
            onsideRt.offsetMin = Vector2.zero;
            onsideRt.offsetMax = Vector2.zero;
            onsideBand = onsideGo.AddComponent<Image>();
            onsideBand.color = new Color(0.25f, 0.55f, 1f, 0.5f);
            onsideGo.SetActive(false);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(meterGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0.1f);
            fillRt.anchorMax = new Vector2(0f, 0.9f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            powerFill = fillGo.AddComponent<Image>();
            powerFill.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        }

        void RebindUiRefs()
        {
            if (uiRoot == null) return;
            var titleT = uiRoot.transform.Find("KickTitle");
            if (titleT != null) titleLabel = titleT.GetComponent<TextMeshProUGUI>();
            var hintT = uiRoot.transform.Find("KickHint");
            if (hintT != null) hintLabel = hintT.GetComponent<TextMeshProUGUI>();
            var fillT = uiRoot.transform.Find("PowerMeter/Fill");
            if (fillT != null) powerFill = fillT.GetComponent<Image>();
            var sweetT = uiRoot.transform.Find("PowerMeter/Sweet");
            if (sweetT != null) sweetBand = sweetT.GetComponent<Image>();
            var onsideT = uiRoot.transform.Find("PowerMeter/Onside");
            if (onsideT != null) onsideBand = onsideT.GetComponent<Image>();
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 80f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            tmp.raycastTarget = false;
            return tmp;
        }

        void ShowUi()
        {
            EnsureUi();
            if (uiRoot != null) uiRoot.SetActive(true);
        }

        void HideUi()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
        }

        void DisableOrphanKickUi()
        {
            foreach (var orphan in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (orphan == null || orphan.name != "KickUi") continue;
                if (orphan.IsChildOf(transform)) continue;
                orphan.gameObject.SetActive(false);
            }
        }

        void RefreshUi()
        {
            if (titleLabel != null)
            {
                titleLabel.text = mode switch
                {
                    KickMode.FieldGoal => $"FIELD GOAL  ·  {kickDistanceYards} YDS",
                    KickMode.ExtraPoint => "EXTRA POINT",
                    KickMode.Kickoff => "KICKOFF",
                    _ => "PUNT"
                };
            }

            if (hintLabel != null)
            {
                hintLabel.text = mode == KickMode.Kickoff
                    ? "HOLD W/S OR STICK  ·  KICK J/B  ·  BLUE = ONSIDE"
                    : IsUprightKickMode
                        ? "AIM  W/S / STICK   ·   KICK  J/B   ·   GREEN = MAKE"
                        : "HOLD W/S OR STICK  ·  KICK J/B  ·  FILL = DISTANCE";
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
                var c = hintLabel.color;
                c.a = pulse;
                hintLabel.color = c;
            }

            if (powerFill != null)
            {
                var rt = powerFill.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Clamp01(power01), 0.9f);
            }

            if (onsideBand != null)
            {
                bool showOnside = mode == KickMode.Kickoff;
                onsideBand.gameObject.SetActive(showOnside);
                if (showOnside)
                {
                    var ort = onsideBand.rectTransform;
                    ort.anchorMin = new Vector2(0f, 0.15f);
                    ort.anchorMax = new Vector2(Mathf.Clamp01(onsideMax), 0.85f);
                }
            }

            if (sweetBand != null && IsUprightKickMode)
            {
                float need = Mathf.Clamp01(0.42f + kickDistanceYards / 95f);
                float min = Mathf.Clamp01(need * 0.85f);
                float max = Mathf.Clamp01(Mathf.Lerp(powerSweetMax, 0.98f, kickDistanceYards / 70f));
                var rt = sweetBand.rectTransform;
                rt.anchorMin = new Vector2(min, 0.15f);
                rt.anchorMax = new Vector2(max, 0.85f);
                sweetBand.color = new Color(0.35f, 0.9f, 0.4f, 0.45f);
                sweetBand.gameObject.SetActive(true);
            }
            else if (sweetBand != null)
            {
                // Punt: soft mid-high band hint; kickoff uses blue onside band only.
                sweetBand.gameObject.SetActive(mode == KickMode.Punt);
                if (mode == KickMode.Punt)
                {
                    sweetBand.color = new Color(0.9f, 0.85f, 0.3f, 0.35f);
                    var rt = sweetBand.rectTransform;
                    rt.anchorMin = new Vector2(0.55f, 0.15f);
                    rt.anchorMax = new Vector2(0.95f, 0.85f);
                }
            }
        }
    }
}
