using System;
using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public class QuarterbackController : MonoBehaviour
    {
        [Header("Throwing")]
        public float throwPower = 20f;
        public float maxThrowDistance = 45f;
        public float throwArc = 0.5f;
        public float aimClickRadius = 2.5f;

        [Header("Scramble")]
        public float losCrossBuffer = 0.2f;
        public float tackleRadius = 0.85f;
        [Tooltip("Pull aim within this distance of the QB to cancel the throw and scramble on release.")]
        public float scrambleCancelRadius = 1.75f;
        [Tooltip("Scramble cancel only if the aim point is this far (or less) past the QB downfield.")]
        public float scrambleMaxDownfield = 1.1f;

        [Header("Aiming")]
        public Transform aimTarget;
        Vector3 targetPosition;

        [Header("State")]
        public bool isAiming;
        public bool hasThrown;
        /// <summary>QB crossed the LOS — pass is dead, this is a run.</summary>
        public bool isScrambling;
        /// <summary>AI offense drives throws; human input disabled.</summary>
        public bool aiControlled;

        [Header("References")]
        public GameObject ballPrefab;
        GameObject currentBall;
        Camera mainCamera;
        public Transform[] receivers;

        PlayerController playerController;
        ThrowingArc throwingArc;
        float aimStartedAt;
        const float AimThrowGrace = 0.2f;
        int losYardAtScramble;
        float immuneUntil;
        bool tacklePending;
        /// <summary>Aim target is near the QB — release runs instead of throwing.</summary>
        bool aimCancelledForScramble;
        /// <summary>0 = LMB, 1 = RMB, 2 = E, 3 = right stick — which control started this aim.</summary>
        int aimInputMode = -1;
        /// <summary>Latched when a second finger / mouse button joins during aim → bullet pass.</summary>
        bool aimBulletPass;
        readonly OffenseDiveSlide diveSlide = new OffenseDiveSlide();

        /// <summary>True during Tecmo dive burst or get-up while scrambling.</summary>
        public bool IsDiveBusy => diveSlide.IsBusy;

        public void SetAiControlled(bool ai)
        {
            aiControlled = ai;
            if (ai && playerController != null)
                playerController.SetControlled(false);
        }

        /// <summary>AI offense throws to a world aim point (no mouse aim).</summary>
        public void AiThrowTo(Vector3 worldTarget)
        {
            if (hasThrown || isScrambling) return;
            if (aiControlled == false)
                SetAiControlled(true);

            targetPosition = worldTarget;
            targetPosition.z = 0f;
            ThrowBall();
        }

        void Start()
        {
            mainCamera = Camera.main;
            playerController = GetComponent<PlayerController>();
            diveSlide.BindHost(transform);
            EnsureThrowingArc();
            FindReceivers();
        }

        void EnsureThrowingArc()
        {
            throwingArc = GetComponent<ThrowingArc>();
            if (throwingArc == null)
                throwingArc = gameObject.AddComponent<ThrowingArc>();
            throwingArc.Hide();
        }

        void Update()
        {
            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.isKicking
                || GameManager.Instance.isKickoffReturn
                || GameManager.Instance.waitingForNextPlay)
            {
                // Plane already broken — still award even if the play-end flag raced ahead.
                if (GameManager.Instance != null
                    && GameManager.Instance.waitingForNextPlay
                    && IsHoldingBall()
                    && TryFinishTouchdown())
                    return;

                if (isAiming) CancelAim();
                if (GameManager.Instance == null
                    || (!GameManager.Instance.waitingForNextPlay
                        && !GameManager.Instance.isKicking
                        && !GameManager.Instance.isKickoffReturn
                        && !GameManager.Instance.isInterceptionReturn))
                    isScrambling = false;
                return;
            }

            // Goal-line: score even if mash / tackle-pending froze the QB.
            if (IsHoldingBall() && TryFinishTouchdown())
                return;

            if (GameManager.Instance.inTackleBattle || tacklePending)
                return;

            // INT / fumble return — QB joins the pursuit (can't throw; ball is gone).
            if (GameManager.Instance.isInterceptionReturn && !IsHoldingBall())
            {
                if (isAiming) CancelAim();
                isScrambling = false;
                bool humanQb = !aiControlled
                               && playerController != null
                               && playerController.IsControlled;
                if (humanQb)
                    TryTackleIntReturnerIfClose();
                else
                    PursueInterceptionReturner();
                return;
            }

            // Loose fumble — race to the ball.
            if (!IsHoldingBall() && FootballBehavior.TryGetLooseFumble(out _))
            {
                if (isAiming) CancelAim();
                isScrambling = false;
                PursueLooseFumble();
                return;
            }

            // Scramble / pocket carrier reaches the scoring endzone.
            if (IsHoldingBall() && TryFinishTouchdown())
                return;

            // Sideline — dead ball wherever the QB steps out with possession.
            if (IsHoldingBall() && IsOutOfBounds())
            {
                FinishOutOfBounds();
                return;
            }

            // Scramble run: no more passing — steer, dive, get up, or get tackled.
            if (isScrambling)
            {
                // Heal silent drops (old PlayerController.Tackle orphaned the ball
                // without starting a fumble — play would limbo forever).
                if (!IsHoldingBall())
                {
                    if (playerController != null)
                        playerController.GiveBall();
                    else
                        FootballBehavior.AttachHeldTo(transform);

                    if (!IsHoldingBall())
                    {
                        // Still empty — force a live fumble at the QB so either side can recover.
                        float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                        int refYard = losYardAtScramble;
                        if (FieldManager.Instance != null)
                            refYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                        FootballBehavior.ForceFumbleAt(
                            transform.position,
                            Vector3.right * dir,
                            "OrphanedScrambleBall",
                            refYard,
                            wasRunPlay: true);
                        isScrambling = false;
                        if (playerController != null)
                        {
                            playerController.hasBall = false;
                            playerController.SetControlled(false);
                        }
                        return;
                    }
                }

                if (UpdateScrambleDive())
                    return;
                if (aiControlled && !diveSlide.IsBusy)
                    AiScrambleMove();
                if (TryFinishTouchdown())
                    return;
                if (Time.time >= immuneUntil)
                {
                    if (diveSlide.IsBusy)
                        CheckScrambleDiveContact();
                    else
                        CheckScrambleTackle();
                }
                return;
            }

            if (hasThrown) return;

            // AI offense — DefensePlayDirector calls AiThrowTo; still check sacks / LOS.
            if (aiControlled)
            {
                if (Time.time >= immuneUntil
                    && (playerController != null && playerController.hasBall))
                    CheckPocketSack();

                if (HasCrossedLineOfScrimmage())
                    ConvertToScrambleRun();
                return;
            }

            // Pocket pressure — DL can sack while still aiming behind the LOS.
            if (Time.time >= immuneUntil
                && (isAiming || (playerController != null && playerController.hasBall)))
                CheckPocketSack();

            // Crossed the LOS with the ball still → becomes a run play.
            if (HasCrossedLineOfScrimmage())
            {
                ConvertToScrambleRun();
                return;
            }

            if (!isAiming)
            {
                var cadence = SnapCadence.Instance;
                if (cadence != null && cadence.IsActive)
                    return;

                // Retro Bowl: press+hold to aim (mouse) · right stick deflect = pad throw aim.
                // Hut click must not auto-arm aim or MouseButtonUp on the snap click instantly throws.
                if (Input.GetMouseButtonDown(0))
                    StartAiming(0);
                else if (Input.GetMouseButtonDown(1))
                    StartAiming(1);
                else if (Input.GetKeyDown(KeyCode.E))
                    StartAiming(2);
                else if (TecmoInput.ThrowStickPressedThisFrame())
                    StartAiming(3);
                return;
            }

            // Still holding aim input — update arc. Released → throw or scramble.
            if (!IsAimInputHeld())
            {
                // Past LOS: cannot pass — scramble with the ball (all aim modes).
                if (HasCrossedLineOfScrimmage())
                {
                    ConvertToScrambleRun(fromAimCancel: true);
                    return;
                }

                // Stick aim: release always throws (hike + aim + release) behind LOS.
                if (aimInputMode == 3)
                {
                    ThrowBall();
                    return;
                }

                if (Time.time - aimStartedAt < AimThrowGrace)
                {
                    // Accidental click — cancel aim, don't throw.
                    CancelAim();
                    return;
                }

                if (aimCancelledForScramble)
                    ConvertToScrambleRun(fromAimCancel: true);
                else
                    ThrowBall();
                return;
            }

            UpdateAim();

            // Crossing LOS while aiming also kills the pass.
            if (HasCrossedLineOfScrimmage())
            {
                ConvertToScrambleRun();
                return;
            }
        }

        bool IsAimInputHeld()
        {
            switch (aimInputMode)
            {
                case 0:
                case 1:
                    // Keep aiming while any pointer is down (second finger for bullet).
                    return Input.GetMouseButton(0)
                           || Input.GetMouseButton(1)
                           || TecmoInput.AimPointerCount() > 0;
                case 2: return Input.GetKey(KeyCode.E);
                case 3: return TecmoInput.ThrowStickHeld();
                default: return false;
            }
        }

        /// <summary>
        /// While scrambling: Tecmo A dive → yards burst → get up → run again.
        /// Touch during dive/get-up → tackled.
        /// </summary>
        bool UpdateScrambleDive()
        {
            if (aiControlled || tacklePending) return false;

            Vector3 currentVel = Vector3.zero;
            var input = ArcadeMove.ReadDigitalPlanar();
            if (input.sqrMagnitude > 0.01f)
                currentVel = new Vector3(input.x, input.y, 0f);

            bool backOnFeet = diveSlide.Tick(
                canStart: !diveSlide.IsBusy,
                currentVelocity: currentVel,
                camera: mainCamera,
                out Vector3 diveVel);

            if (backOnFeet)
            {
                // Resume scramble control after get-up.
                if (playerController != null)
                    playerController.SetControlled(true);
                immuneUntil = Time.time + 0.2f;
                return false;
            }

            if (!diveSlide.IsBusy)
                return false;

            // Dive / get-up — freeze steer; drive the body ourselves while diving.
            if (playerController != null && playerController.IsControlled)
                playerController.SetControlled(false);

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                ArcadeMove.Apply(rb, diveVel);

            return false;
        }

        void CheckScrambleDiveContact()
        {
            if (tacklePending || !isScrambling) return;
            GameObject[] defs;
            try { defs = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { return; }

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            foreach (var d in defs)
            {
                if (d == null || !d.activeInHierarchy) continue;
                if (PlayerStun.IsUnitStunned(d)) continue;
                float dist = Vector2.Distance(pos, new Vector2(d.transform.position.x, d.transform.position.y));
                if (dist > 0.95f) continue;

                var popcorn = SoftTackle.EvaluatePopcorn(transform, d.transform);
                if (popcorn == SoftTackle.PopcornResult.CarrierPopcornsTackler)
                {
                    SoftTackle.ApplyCarrierPopcorn(transform, d.transform);
                    return;
                }

                diveSlide.Reset();
                ApplySackOrTackle(d.name, aiCarrier: false, hardHit: true, tackler: d.transform);
                return;
            }
        }

        bool IsHoldingBall()
        {
            if (hasThrown && !isScrambling) return false;

            // Prefer real attachment — never claim possession after a silent drop.
            if (playerController != null)
            {
                if (playerController.HasBallAttached())
                    return true;
                // Flag said we had it but the ball walked off — heal or admit it's gone.
                if (playerController.hasBall && !playerController.HasBallAttached())
                {
                    if (FootballBehavior.AttachHeldTo(transform)
                        || playerController.ballObject != null)
                    {
                        playerController.hasBall = true;
                        return playerController.HasBallAttached();
                    }
                    playerController.hasBall = false;
                    return false;
                }
            }

            if (isScrambling || isAiming)
            {
                // Scramble/aim without a wired PlayerController — check child football.
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child != null && child.GetComponent<FootballBehavior>() != null)
                        return true;
                }
            }

            return false;
        }

        bool IsOutOfBounds()
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.IsOutOfBounds(transform.position);
            return Mathf.Abs(transform.position.y) >= RetroLookApplier.SidelineY;
        }

        void FinishOutOfBounds()
        {
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return;
            if (tacklePending) return;
            if (hasThrown && !isScrambling) return;

            bool wasScrambling = isScrambling;
            int refYard = wasScrambling
                ? losYardAtScramble
                : (GameManager.Instance != null
                    ? GameManager.Instance.playLosYard
                    : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20));

            if (FieldManager.Instance != null)
                transform.position = FieldManager.Instance.ClampInBounds(transform.position);

            isAiming = false;
            isScrambling = false;
            hasThrown = true;
            tacklePending = false;
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }

            if (ResolveTouchdownAtSpot())
            {
                try { gameObject.tag = "Player"; }
                catch (UnityException) { }
                return;
            }

            if (ResolveSafetyAtSpot())
            {
                try { gameObject.tag = "Player"; }
                catch (UnityException) { }
                return;
            }

            int yardsGained = 0;
            string downLine = "";

            if (FieldManager.Instance != null)
            {
                int endYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                yardsGained = FieldManager.Instance.YardsGained(refYard, endYard);
                FieldManager.Instance.AdvanceBall(yardsGained);

                if (FieldManager.Instance.JustConvertedTwoPoint
                    || FieldManager.Instance.JustScoredTouchdown)
                {
                    PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                if (FieldManager.Instance.JustScoredSafety)
                {
                    PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                if (FieldManager.Instance.JustFailedTwoPoint)
                {
                    PlayBanner.ShowPlayOver("2-POINT NO GOOD", BannerTone.Neutral);
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                downLine = FieldManager.Instance.GetPlayResultDownLine();
            }

            string yardsText = yardsGained >= 0 ? $"+{yardsGained} YDS" : $"{yardsGained} YDS";
            PlayBanner.ShowPlayOver($"OUT OF BOUNDS\n{yardsText}\n{downLine}");
            Debug.Log($"QB out of bounds — {yardsText}, {downLine}");

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
        }

        bool HasCrossedLineOfScrimmage()
        {
            if (GameManager.Instance == null) return false;
            // Kickoff walls park the QB past tee LOS — never scramble without the ball.
            if (!IsHoldingBall()) return false;
            float losX = GameManager.Instance.GetPlayLosWorldX();
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            return (transform.position.x - losX) * dir > losCrossBuffer;
        }

        void ConvertToScrambleRun(bool fromAimCancel = false)
        {
            if (isScrambling || hasThrown) return;
            if (GameManager.Instance != null
                && (GameManager.Instance.isKicking || GameManager.Instance.isPreSnap
                    || GameManager.Instance.isKickoffReturn
                    || GameManager.Instance.isInterceptionReturn
                    || GameManager.Instance.waitingForNextPlay))
                return;

            isScrambling = true;
            isAiming = false;
            aimCancelledForScramble = false;
            aimInputMode = -1;
            aimBulletPass = false;
            hasThrown = false; // didn't throw — still "has" the ball as runner
            diveSlide.Reset();
            immuneUntil = Time.time + 0.4f;

            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.SetControlled(true);
                // Keep / restore the football in hand — never scramble empty.
                if (!playerController.HasBallAttached())
                    playerController.GiveBall();
                else
                    playerController.hasBall = true;
            }
            else
            {
                FootballBehavior.AttachHeldTo(transform);
            }

            // AI scramble: don't hand control to the human.
            if (aiControlled && playerController != null)
                playerController.SetControlled(false);

            losYardAtScramble = GameManager.Instance != null
                ? GameManager.Instance.playLosYard
                : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20);

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            // Camera / ring: stay on defender when human is on defense.
            if (!aiControlled)
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

            if (fromAimCancel)
            {
                PlayBanner.Show("SCRAMBLE", 1.1f);
                Debug.Log("Throw cancelled near QB — scramble / run");
            }
            else
            {
                PlayBanner.Show("SCRAMBLE", 1.4f);
                Debug.Log("QB crossed LOS — scramble / run play");
            }
        }

        void CheckScrambleTackle()
        {
            if (TryGetNearestDefender(out Transform tackler, out string name, out float dist)
                && dist <= tackleRadius)
                ApplySackOrTackle(name, tackler: tackler);
        }

        void CheckPocketSack()
        {
            if (TryGetNearestDefender(out Transform tackler, out string name, out float dist)
                && dist <= tackleRadius)
                ApplySackOrTackle(name, tackler: tackler);
        }

        bool TryGetNearestDefender(out Transform tackler, out string name, out float dist)
        {
            tackler = null;
            name = null;
            dist = float.MaxValue;

            GameObject[] defenders;
            try { defenders = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { return false; }

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            foreach (var d in defenders)
            {
                if (d == null || !d.activeInHierarchy) continue;
                if (PlayerStun.IsUnitStunned(d)) continue;
                float dDist = Vector2.Distance(pos, new Vector2(d.transform.position.x, d.transform.position.y));
                if (dDist < dist)
                {
                    dist = dDist;
                    tackler = d.transform;
                    name = d.name;
                }
            }

            return tackler != null;
        }

        /// <summary>DL / pursuit gets home while QB still has the ball.</summary>
        public void ApplySackOrTackle(
            string tacklerName = null,
            bool aiCarrier = false,
            bool hardHit = false,
            Transform tackler = null)
        {
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return;
            // Already in a Tecmo mash lock — second defender stacks HP.
            if (TecmoContact.IsActiveForCarrier(transform) && tackler != null)
            {
                TecmoContact.TryJoinTackler(tackler);
                return;
            }
            if (GameManager.Instance != null && GameManager.Instance.inTackleBattle)
                return;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi)
                return;
            if (tacklePending) return;
            // Tecmo: contact during dive / get-up is a tackle.
            if (diveSlide.IsBusy)
                diveSlide.Reset();
            if (Time.time < immuneUntil) return;
            if (hasThrown && !isScrambling)
                return;
            if (IsHoldingBall() && TryFinishTouchdown())
                return;

            bool isSack = !isScrambling;
            tacklePending = true;

            // Infer AI carrier from possession if not specified.
            if (!aiCarrier && FieldManager.Instance != null && !FieldManager.Instance.isPlayerPossession)
                aiCarrier = true;

            Action onBroken = () => OnQbTackleBroken(isSack);
            Action onTackled = () =>
            {
                if (isScrambling)
                    FinishScrambleTackle();
                else
                    FinishSack(tacklerName);
            };

            if (tackler == null && !string.IsNullOrEmpty(tacklerName))
            {
                var go = GameObject.Find(tacklerName);
                if (go != null) tackler = go.transform;
            }

            // Dive / hard hit → instant tackle (Tecmo).
            if (hardHit)
            {
                if (playerController != null)
                    playerController.SetControlled(false);
                TecmoContact.ResolveInstantTackle(
                    transform,
                    tackler,
                    onTackled: onTackled,
                    onBroken: onBroken,
                    hardHit: true,
                    pocketSack: isSack);
                return;
            }

            // Non-dive → Tecmo mash lock (Z break / wrap).
            if (GameRules.EnableTecmoContact && tackler != null)
            {
                if (TecmoContact.Begin(
                        transform,
                        tackler,
                        onBroken: () =>
                        {
                            tacklePending = false;
                            onBroken();
                        },
                        onTackled: () =>
                        {
                            if (playerController != null)
                                playerController.SetControlled(false);
                            onTackled();
                        },
                        pocketSack: isSack))
                    return;
            }

            // Retro Bowl fallback: soft wrap / sack (no mash meters).
            if (!GameRules.EnableContactBattle)
            {
                tacklePending = false;
                bool playerCarrier = !aiCarrier
                                     && playerController != null
                                     && playerController.IsControlled;
                bool tackled = SoftTackle.ResolveWrap(
                    transform,
                    tackler,
                    isPocketSack: isSack,
                    playerCarrier: playerCarrier);

                if (!tackled)
                {
                    onBroken();
                    return;
                }

                if (SoftTackle.RollFumble(transform, tackler, hardHit, isPocketSack: isSack))
                {
                    string who = !string.IsNullOrEmpty(tacklerName)
                        ? tacklerName
                        : (tackler != null ? tackler.name : "DEFENSE");
                    ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(transform, tackler));
                    return;
                }

                tacklePending = true;
                if (playerController != null)
                    playerController.SetControlled(false);
                onTackled();
                return;
            }

            // Dive / hard-hit keeps the old forced QTE; wraps use ContactBattle.
            bool started;
            if (hardHit)
            {
                if (playerController != null)
                    playerController.SetControlled(false);

                started = TackleBattle.Begin(
                    transform,
                    tacklerName,
                    onBroken: onBroken,
                    onTackled: onTackled,
                    isSack: isSack,
                    aiCarrier: aiCarrier,
                    hardHit: true);
            }
            else
            {
                if (tackler == null)
                {
                    tacklePending = false;
                    onTackled();
                    return;
                }

                // Keep PlayerController.IsControlled true until Begin so the mash UI
                // detects the human QB; inTackleBattle freezes movement.
                started = ContactBattle.Begin(
                    transform,
                    tackler,
                    onOffenseWon: onBroken,
                    onDefenseWon: onTackled);

                if (started && playerController != null)
                    playerController.SetControlled(false);
            }

            if (!started)
            {
                tacklePending = false;
                bool playerCarrier = !aiCarrier
                                     && playerController != null
                                     && playerController.IsControlled;
                bool tackled = SoftTackle.ResolveWrap(
                    transform,
                    tackler,
                    isPocketSack: isSack,
                    playerCarrier: playerCarrier);
                if (!tackled)
                {
                    onBroken();
                    return;
                }

                if (SoftTackle.RollFumble(transform, tackler, hardHit, isPocketSack: isSack))
                {
                    string who = !string.IsNullOrEmpty(tacklerName)
                        ? tacklerName
                        : (tackler != null ? tackler.name : "DEFENSE");
                    ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(transform, tackler));
                    return;
                }

                tacklePending = true;
                if (playerController != null)
                    playerController.SetControlled(false);
                onTackled();
            }
        }

        /// <summary>Dive hit on QB (pocket or scramble) — forced fumble, not a sack battle.</summary>
        public void ForceFumbleFromDive(string tacklerName, Vector3 knockDir)
        {
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return;
            if (hasThrown && !isScrambling) return;
            if (!IsHoldingBall() && !(playerController != null && playerController.hasBall))
                return;

            int refYard = isScrambling
                ? losYardAtScramble
                : (GameManager.Instance != null
                    ? GameManager.Instance.playLosYard
                    : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20));

            isAiming = false;
            isScrambling = false;
            hasThrown = true;
            tacklePending = false;
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }

            FootballBehavior.ForceFumbleAt(
                transform.position,
                knockDir,
                tacklerName,
                refYard,
                wasRunPlay: true);

            Debug.Log($"QB fumbled from dive by {tacklerName}");
        }

        /// <summary>QB recovers a live fumble — continue as a scramble carrier.</summary>
        public void BeginFumbleRecovery(int refYard)
        {
            isAiming = false;
            isScrambling = true;
            hasThrown = false;
            tacklePending = false;
            losYardAtScramble = refYard;
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = true;
                bool playerOffense = FieldManager.Instance == null
                                     || FieldManager.Instance.isPlayerPossession;
                playerController.SetControlled(playerOffense);
            }

            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            immuneUntil = Time.time + 0.4f;
            diveSlide.Reset();

            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null) cc.SetTarget(transform);
            }

            Debug.Log("QB recovered fumble — scramble continues");
        }

        void PursueLooseFumble()
        {
            if (!FootballBehavior.TryGetLooseFumble(out var ball) || ball == null)
                return;
            if (PlayerStun.IsUnitStunned(this))
                return;

            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 delta = ball.PlayPlanePosition - transform.position;
            delta.z = 0f;
            Vector3 vel = delta.sqrMagnitude > 0.01f
                ? delta.normalized * 6.8f
                : Vector3.zero;
            ArcadeMove.Apply(rb, vel);

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && Mathf.Abs(vel.x) > 0.05f)
                sr.flipX = vel.x < 0f;
        }

        void AiScrambleMove()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;
            // Jog downfield / slightly random lane.
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            float lane = Mathf.Sin(Time.time * 2.2f) * 1.4f;
            Vector3 vel = new Vector3(4.2f * dir, lane * 0.35f, 0f);
            ArcadeMove.Apply(rb, vel);
        }

        void PursueInterceptionReturner()
        {
            if (!InterceptionReturner.TryGetActive(out var ret) || ret == null)
                return;
            if (PlayerStun.IsUnitStunned(this))
                return;

            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 aim = ret.transform.position;
            aim += Vector3.right * (ret.TowardLowEndzone ? -0.7f : 0.7f);
            Vector3 delta = aim - transform.position;
            delta.z = 0f;
            Vector3 vel = delta.sqrMagnitude > 0.01f ? delta.normalized * 5.4f : Vector3.zero;
            ArcadeMove.Apply(rb, vel);

            TryTackleIntReturnerIfClose();
        }

        void TryTackleIntReturnerIfClose()
        {
            if (!InterceptionReturner.TryGetActive(out var ret) || ret == null)
                return;
            if (PlayerStun.IsUnitStunned(this))
                return;

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(ret.transform.position.x, ret.transform.position.y));
            if (dist <= tackleRadius * 1.1f)
                ret.TryAcceptTackle(transform);
        }

        void OnQbTackleBroken(bool wasSack)
        {
            tacklePending = false;
            immuneUntil = Time.time + 0.85f;

            if (playerController != null)
            {
                playerController.hasBall = true;
                playerController.SetControlled(!aiControlled);
            }

            // Escape the pocket → keep the ball and run.
            if (wasSack && !isScrambling)
            {
                isAiming = false;
                if (throwingArc != null)
                    throwingArc.Hide();
                isScrambling = true;
                hasThrown = false;
                losYardAtScramble = GameManager.Instance != null
                    ? GameManager.Instance.playLosYard
                    : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20);
                try { gameObject.tag = "BallCarrier"; }
                catch (UnityException) { }
            }

            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            transform.position += Vector3.right * (0.45f * dir);
            Debug.Log("QB broke the tackle — keep going!");
        }

        void FinishSack(string tacklerName)
        {
            tacklePending = false;
            if (hasThrown && !isScrambling) return;
            // Allow finish after lost battle even if flags weird.
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return;

            isAiming = false;
            hasThrown = true;
            isScrambling = false;
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }

            int losYard = GameManager.Instance != null
                ? GameManager.Instance.playLosYard
                : (FieldManager.Instance != null ? FieldManager.Instance.currentYardLine : 20);

            int yardsGained = 0;
            string downLine = "";

            if (FieldManager.Instance != null)
            {
                // Sack in own endzone = safety (before AdvanceBall parks the loss).
                if (FieldManager.Instance.TryScoreSafety(transform.position.x)
                    || FieldManager.Instance.JustScoredSafety)
                {
                    PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
                    Debug.Log($"Sack SAFETY by {tacklerName}");
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                int endYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                yardsGained = FieldManager.Instance.YardsGained(losYard, endYard);
                if (yardsGained > 0)
                    yardsGained = 0;
                FieldManager.Instance.AdvanceBall(yardsGained);

                downLine = FieldManager.Instance.GetPlayResultDownLine();
            }

            string lossLine = yardsGained < 0
                ? $"LOSS OF {Mathf.Abs(yardsGained)} YARDS"
                : "NO GAIN";

            var sb = new System.Text.StringBuilder();
            sb.Append("SACK");
            sb.Append('\n').Append(lossLine);
            if (!string.IsNullOrEmpty(tacklerName))
                sb.Append('\n').Append(tacklerName);
            if (!string.IsNullOrEmpty(downLine))
                sb.Append('\n').Append(downLine);

            PlayBanner.ShowPlayOver(sb.ToString());
            Debug.Log($"Sack by {tacklerName} — {lossLine}, {downLine}");

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
        }

        void FinishScrambleTackle()
        {
            FinishScrambleDown("QB SCRAMBLE");
        }

        /// <summary>QB slide — safe spot at dive end (no fumble risk).</summary>
        void FinishScrambleDive()
        {
            // Legacy safe slide — unused by Tecmo get-up dive; keep for kneel/edge cases.
            diveSlide.Reset();
            FinishScrambleDown("QB SLIDE");
        }

        void FinishScrambleDown(string header)
        {
            tacklePending = false;
            if (!isScrambling && hasThrown) return;

            isScrambling = false;
            hasThrown = true;
            isAiming = false;
            aimCancelledForScramble = false;
            diveSlide.Reset();
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                ArcadeMove.Apply(rb, Vector3.zero);

            if (ResolveTouchdownAtSpot())
            {
                try { gameObject.tag = "Player"; }
                catch (UnityException) { }
                return;
            }

            if (ResolveSafetyAtSpot())
            {
                try { gameObject.tag = "Player"; }
                catch (UnityException) { }
                return;
            }

            int yardsGained = 0;
            string downLine = "";

            if (FieldManager.Instance != null)
            {
                int endYard = FieldManager.Instance.WorldXToYard(transform.position.x);
                yardsGained = FieldManager.Instance.YardsGained(losYardAtScramble, endYard);
                FieldManager.Instance.AdvanceBall(yardsGained);

                if (FieldManager.Instance.JustConvertedTwoPoint
                    || FieldManager.Instance.JustScoredTouchdown)
                {
                    PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                if (FieldManager.Instance.JustScoredSafety)
                {
                    PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                if (FieldManager.Instance.JustFailedTwoPoint)
                {
                    PlayBanner.ShowPlayOver("2-POINT NO GOOD", BannerTone.Neutral);
                    try { gameObject.tag = "Player"; }
                    catch (UnityException) { }
                    return;
                }

                downLine = FieldManager.Instance.GetPlayResultDownLine();
            }

            string yardsText = yardsGained >= 0 ? $"+{yardsGained} YDS" : $"{yardsGained} YDS";
            PlayBanner.ShowPlayOver($"{header}\n{yardsText}\n{downLine}");
            Debug.Log($"{header} — {yardsText}, {downLine}");

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
        }

        bool TryFinishTouchdown()
        {
            if (!IsHoldingBall()) return false;
            if (FieldManager.Instance == null || !FieldManager.Instance.IsCarrierInScoringEndzone(transform))
                return false;

            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive)
                TackleBattle.Instance.CancelForScore();
            if (ContactBattle.Instance != null && ContactBattle.Instance.IsActive)
                ContactBattle.Instance.ForceCancel();
            if (TecmoContact.Instance != null && TecmoContact.Instance.IsActive)
                TecmoContact.Instance.ForceCancel();

            isAiming = false;
            isScrambling = false;
            hasThrown = true;
            tacklePending = false;
            if (throwingArc != null)
                throwingArc.Hide();

            if (playerController != null)
            {
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }

            // Plane already validated — award without re-checking pivot/spot.
            if (FieldManager.Instance.AwardTouchdown()
                || FieldManager.Instance.JustScoredTouchdown)
            {
                PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
                Debug.Log("QB scored a touchdown");
            }

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
            return true;
        }

        bool ResolveTouchdownAtSpot()
        {
            if (FieldManager.Instance == null) return false;
            if (!FieldManager.Instance.TryScoreTouchdown(transform)
                && !FieldManager.Instance.JustScoredTouchdown)
                return false;

            PlayBanner.ShowPlayOver(FieldManager.Instance.GetScoreBannerTitle());
            Debug.Log("QB scored a touchdown");
            return true;
        }

        bool ResolveSafetyAtSpot()
        {
            if (FieldManager.Instance == null) return false;
            if (!FieldManager.Instance.TryScoreSafety(transform.position.x)
                && !FieldManager.Instance.JustScoredSafety)
                return false;

            PlayBanner.ShowPlayOver("SAFETY\n+2", BannerTone.Positive);
            Debug.Log("QB — SAFETY");
            return true;
        }

        /// <summary>Legacy no-op — aim starts on player press after snap (not on hut click).</summary>
        public void BeginAimFromCadence()
        {
            // Intentionally empty. Auto-aim on hut made MouseButtonUp throw/scramble instantly.
        }

        /// <summary>
        /// Open throw trajectory from right-stick aim (after D-pad hike).
        /// D-pad (ArcadeMove) still steers the QB in the pocket.
        /// </summary>
        public void BeginStickAim()
        {
            if (aiControlled || hasThrown || isScrambling || isAiming) return;
            StartAiming(3);
        }

        bool IsClickNearQb()
        {
            if (!TryGetPlayPlanePoint(out Vector3 point))
                return false;
            return Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(point.x, point.y)) <= aimClickRadius;
        }

        void FindReceivers()
        {
            GameObject[] receiverObjects = GameObject.FindGameObjectsWithTag("Receiver");
            receivers = new Transform[receiverObjects.Length];
            for (int i = 0; i < receiverObjects.Length; i++)
                receivers[i] = receiverObjects[i].transform;
        }

        void StartAiming(int inputMode)
        {
            if (hasThrown || isScrambling) return;
            EnsureThrowingArc();
            isAiming = true;
            aimInputMode = inputMode;
            aimCancelledForScramble = false;
            // RMB aim starts as a bullet pass; LMB can upgrade to bullet if RMB joins later.
            aimBulletPass = inputMode == 1;
            aimStartedAt = Time.time;
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            targetPosition = transform.position + Vector3.right * (8f * dir);
            targetPosition.z = 0f;
            throwingArc.Show();
            UpdateAim();
        }

        void CancelAim()
        {
            isAiming = false;
            aimCancelledForScramble = false;
            aimInputMode = -1;
            aimBulletPass = false;
            if (throwingArc != null)
                throwingArc.Hide();
        }

        void UpdateAim()
        {
            // Right-click, or second finger / dual mouse button during aim → bullet on release.
            if (aimInputMode == 1 || TecmoInput.AimPointerCount() >= 2
                || (aimInputMode != 3 && Input.GetMouseButton(1)))
                aimBulletPass = true;

            if (aimInputMode == 3)
            {
                if (!TryGetStickAimPoint(out targetPosition))
                    return;
            }
            else if (aimInputMode != 2 || Input.GetMouseButton(0) || Input.GetMouseButton(1)
                     || TecmoInput.AimPointerCount() > 0)
            {
                // Mouse / trackpad / E — same pull-back invert as the right stick.
                if (!TryGetPullBackPointerAimPoint(out targetPosition))
                    return;
            }

            Vector3 start = transform.position;
            start.z = 0f;
            targetPosition.z = 0f;

            float distanceToTarget = Vector3.Distance(start, targetPosition);
            if (distanceToTarget > maxThrowDistance)
            {
                Vector3 direction = (targetPosition - start).normalized;
                targetPosition = start + direction * maxThrowDistance;
            }

            if (aimTarget != null)
                aimTarget.position = targetPosition;

            // Pull aim onto / just past the QB to cancel (mouse / E only).
            // Stick release always throws — no scramble-cancel via stick.
            float planarDist = Vector2.Distance(
                new Vector2(start.x, start.y),
                new Vector2(targetPosition.x, targetPosition.y));
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            float downfield = (targetPosition.x - start.x) * dir;
            aimCancelledForScramble = aimInputMode != 3
                                     && planarDist <= scrambleCancelRadius
                                     && downfield <= scrambleMaxDownfield;

            if (aimCancelledForScramble)
            {
                if (throwingArc != null)
                    throwingArc.Hide();
                return;
            }

            if (throwingArc != null)
            {
                if (!throwingArc.IsVisible)
                    throwingArc.Show();
                float dist = Vector3.Distance(start, targetPosition);
                float peak = FootballBehavior.PeakForDistance(dist);
                if (aimBulletPass)
                    peak = Mathf.Clamp(peak * 0.32f, 0.55f, 3.2f);
                throwingArc.SetPath(start + Vector3.right * (0.35f * dir), targetPosition, peak);
            }
        }

        /// <summary>Right stick → aim point relative to QB (mag scales throw distance).</summary>
        bool TryGetStickAimPoint(out Vector3 point)
        {
            point = Vector3.zero;
            Vector2 stick = TecmoInput.ReadThrowStick();
            float mag = stick.magnitude;
            if (mag < TecmoInput.StickThrowHoldDeadzone)
                return false;

            Vector2 dir2 = stick / mag;
            float t = Mathf.InverseLerp(
                TecmoInput.StickThrowHoldDeadzone,
                1f,
                Mathf.Clamp01(mag));
            float dist = Mathf.Lerp(4f, maxThrowDistance, t);

            float drive = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            // Pull-back aim: stick opposite drive = downfield. Stick Y is already inverted in
            // ReadThrowStick (pull down → aim toward top of field).
            float aimX = dir2.x * -drive;
            float aimY = dir2.y;
            Vector2 aimDir = new Vector2(aimX, aimY);
            if (aimDir.sqrMagnitude < 0.01f)
                aimDir = new Vector2(drive, 0f);
            aimDir.Normalize();

            point = transform.position + new Vector3(aimDir.x, aimDir.y, 0f) * dist;
            point.z = 0f;
            return true;
        }

        /// <summary>
        /// Mouse / trackpad play-plane point mirrored across the QB on X — pull behind
        /// (toward your endzone) to aim farther downfield, same feel as stick pull-back.
        /// </summary>
        bool TryGetPullBackPointerAimPoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (!TryGetPlayPlanePoint(out Vector3 cursor))
                return false;

            Vector3 qb = transform.position;
            qb.z = 0f;
            cursor.z = 0f;

            float dx = cursor.x - qb.x;
            float dy = cursor.y - qb.y;
            // Mirror across QB: pull back+down → aim downfield+up (top of field).
            Vector3 offset = new Vector3(-dx, -dy, 0f);

            float dist = offset.magnitude;
            if (dist < 0.05f)
            {
                float drive = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                point = qb + Vector3.right * (4f * drive);
                point.z = 0f;
                return true;
            }

            if (dist > maxThrowDistance)
                offset = offset.normalized * maxThrowDistance;

            point = qb + offset;
            point.z = 0f;
            return true;
        }

        bool TryGetPlayPlanePoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null) return false;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var playPlane = new Plane(Vector3.forward, Vector3.zero);
            if (!playPlane.Raycast(ray, out float distance))
                return false;

            point = ray.GetPoint(distance);
            point.z = 0f;
            return true;
        }

        void ThrowBall()
        {
            if (isScrambling) return;

            // Past LOS — illegal forward pass; keep running with it.
            if (HasCrossedLineOfScrimmage())
            {
                ConvertToScrambleRun();
                return;
            }

            isAiming = false;
            aimCancelledForScramble = false;
            aimInputMode = -1;
            bool bullet = aimBulletPass;
            aimBulletPass = false;
            hasThrown = true;
            if (throwingArc != null)
                throwingArc.Hide();

            // Prefer the ball already in hand — Instantiating a second one left the
            // held copy stuck on the QB (LateUpdate StickToCarrier).
            FootballBehavior held = null;
            if (playerController != null && playerController.ballObject != null)
                held = playerController.ballObject.GetComponent<FootballBehavior>();
            if (held == null)
                held = FindHeldFootballOnSelf();

            if (playerController != null)
                playerController.RemoveBall();

            GameObject ball;
            FootballBehavior ballBehavior;
            if (held != null)
            {
                ball = held.gameObject;
                ballBehavior = held;
                ball.SetActive(true);
            }
            else
            {
                if (ballPrefab == null)
                {
                    Debug.LogError("QuarterbackController: ballPrefab is not assigned.");
                    hasThrown = false;
                    return;
                }

                float throwDir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                Vector3 spawn = transform.position + Vector3.right * (0.4f * throwDir);
                spawn.z = 0f;
                ball = Instantiate(ballPrefab, spawn, Quaternion.identity);
                ballBehavior = ball.GetComponent<FootballBehavior>();
                if (ballBehavior == null)
                    ballBehavior = ball.AddComponent<FootballBehavior>();
            }

            // Kill any duplicate footballs still parented to the QB.
            ClearOtherChildFootballs(ball);

            if (currentBall != null && currentBall != ball)
            {
                currentBall.SetActive(false);
                Destroy(currentBall);
            }
            currentBall = ball;

            if (FieldManager.Instance != null)
                FieldManager.Instance.ballTransform = ball.transform;

            ballBehavior.ThrowToTarget(targetPosition, bullet);
            FollowCameraToBall(ball.transform);

            Invoke(nameof(ResetThrow), 3f);
        }

        FootballBehavior FindHeldFootballOnSelf()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var fb = child.GetComponent<FootballBehavior>();
                if (fb != null) return fb;
            }

            if (FieldManager.Instance != null && FieldManager.Instance.ballTransform != null)
            {
                var fb = FieldManager.Instance.ballTransform.GetComponent<FootballBehavior>();
                if (fb != null && (fb.transform.parent == transform || fb.isCaught))
                    return fb;
            }

            return null;
        }

        void ClearOtherChildFootballs(GameObject keep)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null || child.gameObject == keep) continue;
                if (child.GetComponent<FootballBehavior>() == null
                    && !child.name.Contains("Football"))
                    continue;
                Destroy(child.gameObject);
            }
        }

        static void FollowCameraToBall(Transform ball)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.FollowBall(ball);
        }

        void ResetThrow()
        {
            hasThrown = false;
        }

        public void NotifyPassCaught()
        {
            isAiming = false;
            hasThrown = true;
            isScrambling = false;
            if (throwingArc != null)
                throwingArc.Hide();
            if (playerController != null)
            {
                playerController.RemoveBall();
                playerController.SetControlled(false);
            }
            CancelInvoke(nameof(ResetThrow));
        }

        public void ResetPlayState()
        {
            isAiming = false;
            hasThrown = false;
            isScrambling = false;
            aimCancelledForScramble = false;
            tacklePending = false;
            immuneUntil = 0f;
            currentBall = null;
            diveSlide.Reset();
            CancelAim();

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            aiControlled = !playerOffense;

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
            if (playerController != null)
            {
                playerController.hasBall = true;
                playerController.SetControlled(playerOffense);
            }
        }

        /// <summary>Special teams: freeze QB so kickoff walls cannot become a scramble.</summary>
        public void FreezeForKickoff()
        {
            isAiming = false;
            hasThrown = false;
            isScrambling = false;
            aimCancelledForScramble = false;
            tacklePending = false;
            immuneUntil = 0f;
            currentBall = null;
            diveSlide.Reset();
            CancelAim();
            aiControlled = false;

            try { gameObject.tag = "Player"; }
            catch (UnityException) { }
            if (playerController != null)
            {
                playerController.RemoveBall();
                playerController.hasBall = false;
                playerController.SetControlled(false);
            }
        }

        public Transform GetNearestReceiver(Vector3 position)
        {
            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (Transform receiver in receivers)
            {
                if (receiver == null) continue;

                float distance = Vector3.Distance(position, receiver.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = receiver;
                }
            }

            return nearest;
        }
    }
}
