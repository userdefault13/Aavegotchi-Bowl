using UnityEngine;
using RetroBowl.Gameplay;

namespace RetroBowl.Core
{
    /// <summary>
    /// Field / drive state with true bidirectional offense.
    ///
    /// Rules (absolute field):
    /// - Yard 0 = left endzone, yard 100 = right endzone. World X = YardToWorldX(abs).
    /// - <see cref="currentYardLine"/> is always the absolute LOS / ball yard (0–100).
    /// - <see cref="driveDirection"/> +1 → score at 100; offense lined up on the smaller-X side of LOS.
    /// - <see cref="driveDirection"/> −1 → score at 0; offense lined up on the larger-X side of LOS.
    /// - Own yard for HUD = dir&gt;0 ? abs : 100−abs. Advances add yards×driveDirection to abs.
    /// - Opening kickoff: tee at abs 35, <see cref="kickDirection"/> +1. Receiver gets
    ///   driveDirection −1 (attack back toward 0) — no fake 100−abs remap of world X.
    /// - After a score: scorer kicks with kickDirection = scoring driveDirection
    ///   (tee at own 35 → abs 35 or 65). Receiving offense driveDirection = −kickDirection.
    /// </summary>
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }

        [Header("Field Dimensions")]
        public float fieldLength = 100f;
        public float fieldWidth = 53.3f;
        public float yardLength = 1f;
        /// <summary>
        /// Visual / OOB sideline Y (across field). Kept in sync with
        /// <c>RetroLookApplier.SidelineY</c> (5.85) by RetroLookApplier.Apply.
        /// </summary>
        public float sidelineHalf = 5.85f;
        /// <summary>
        /// Float epsilon on the painted goal line (world units → yards via yardLength).
        /// </summary>
        const float EndzoneEntryGraceYards = 0.15f;

        /// <summary>World X of the attack goal line (orange pylon / Field.png white). Always matches paint.</summary>
        public float AttackGoalWorldX
        {
            get
            {
                EnsureYardLengthMatchesPaint();
                return driveDirection > 0
                    ? RetroLookApplier.PlayableHalfWidth
                    : -RetroLookApplier.PlayableHalfWidth;
            }
        }

        /// <summary>Keep yard↔world mapping locked to Field.png goal lines.</summary>
        public void EnsureYardLengthMatchesPaint()
        {
            float half = RetroLookApplier.PlayableHalfWidth;
            float expected = (half * 2f) / 100f;
            if (Mathf.Abs(yardLength - expected) > 0.0001f)
                yardLength = expected;
        }

        [Header("Ball Position")]
        public Transform ballTransform;
        /// <summary>
        /// Absolute LOS / ball yard (0 = left EZ, 100 = right EZ).
        /// World X is always <see cref="YardToWorldX"/>(currentYardLine).
        /// </summary>
        public int currentYardLine = 20;
        /// <summary>
        /// +1 = offense attacks toward yard 100; −1 = toward yard 0.
        /// </summary>
        public int driveDirection = 1;
        /// <summary>
        /// Kickoff flight direction (+1 toward 100, −1 toward 0).
        /// Receiving drive after KO uses −kickDirection.
        /// </summary>
        public int kickDirection = 1;
        public bool isPlayerPossession = true;
        public int down = 1;
        public int yardsToGo = 10;
        /// <summary>Absolute yard the offense must reach for a first down.</summary>
        int firstDownMarker = 30;

        /// <summary>Absolute first-down stick yard.</summary>
        public int FirstDownYard => firstDownMarker;

        /// <summary>Offense-own yard line for HUD ("Ball on 20").</summary>
        public int OwnYardLine => ToOwnYards(currentYardLine);

        /// <summary>World-space unit X for current drive (+1 or −1).</summary>
        public float DriveDirX => driveDirection >= 0 ? 1f : -1f;

        /// <summary>World-space unit X for kickoff flight.</summary>
        public float KickDirX => kickDirection >= 0 ? 1f : -1f;

        /// <summary>Return run direction (opposite kick flight).</summary>
        public float ReturnDirX => -KickDirX;

        [Header("Drive Management")]
        public int startingYardLine = 20;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // SceneFlow / StartNewGame owns match kickoff; only seed if still pristine.
            if (down == 1 && currentYardLine == startingYardLine && isPlayerPossession)
                InitializeDrive(true);
        }

        /// <summary>Normalize to ±1.</summary>
        public static int NormDir(int dir) => dir >= 0 ? 1 : -1;

        /// <summary>Offense-own yards from an absolute spot (uses current driveDirection).</summary>
        public int ToOwnYards(int absYard)
            => driveDirection > 0 ? absYard : 100 - absYard;

        /// <summary>Absolute yard from offense-own yards (uses current driveDirection).</summary>
        public int OwnToAbsolute(int ownYard)
            => driveDirection > 0 ? ownYard : 100 - ownYard;

        /// <summary>Yards gained by offense from absolute from→to.</summary>
        public int YardsGained(int fromAbs, int toAbs)
            => (toAbs - fromAbs) * NormDir(driveDirection);

        /// <summary>True if <paramref name="yard"/> has reached/passed <paramref name="marker"/> in drive direction.</summary>
        public bool HasReachedOrPassed(int yard, int marker)
            => driveDirection > 0 ? yard >= marker : yard <= marker;

        /// <summary>True if world Y is past either sideline (playable green ends here).</summary>
        public bool IsOutOfBounds(Vector3 worldPos)
            => Mathf.Abs(worldPos.y) >= sidelineHalf;

        /// <summary>Clamp a position onto the field (just inside the sidelines / OOB).</summary>
        public Vector3 ClampInBounds(Vector3 worldPos)
        {
            float lim = Mathf.Max(0.05f, sidelineHalf - 0.05f);
            worldPos.y = Mathf.Clamp(worldPos.y, -lim, lim);
            worldPos.z = 0f;
            return worldPos;
        }

        public void InitializeDrive(bool playerHasBall)
        {
            isPlayerPossession = playerHasBall;
            driveDirection = 1;
            kickDirection = 1;
            currentYardLine = startingYardLine;
            down = 1;
            yardsToGo = 10;
            SetFirstDownMarkers();
            PendingTryAfterTd = false;
            PendingKickoff = false;
            IsTwoPointAttempt = false;
            KickoffReceiverIsPlayer = false;
            LastScorerWasPlayer = false;
            LastScorerDriveDirection = 1;
            FirstHalfKickDirection = 1;
            HasSecondHalfKickoffPending = false;
            SecondHalfReceiverIsPlayer = true;
            ClearPlayResultFlags();
            UpdateBallPosition();
        }

        /// <summary>Fallback / practice: player offense at own 20, attacking +X.</summary>
        public void BeginPlayerDrive()
        {
            InitializeDrive(true);
        }

        /// <summary>
        /// Week-game open before GET READY / kickoff mini-game.
        /// Opponent kicks L→R from abs 35; player receives (driveDirection set on resolve).
        /// </summary>
        public void PrepareOpeningReceive()
        {
            PrepareOpeningKickoff(receiverIsPlayer: true, kickDir: 1);
        }

        /// <summary>
        /// Opening / 2nd-half kickoff after the coin toss.
        /// <paramref name="kickDir"/> +1 = tee at abs 35 kicking toward 100;
        /// −1 = tee at abs 65 kicking toward 0.
        /// </summary>
        public void PrepareOpeningKickoff(bool receiverIsPlayer, int kickDir)
        {
            KickoffReceiverIsPlayer = receiverIsPlayer;
            PendingKickoff = true;
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            // Kicking team is the non-receiver — seed LastScorer* so PrepareKickoff-style tee works.
            LastScorerWasPlayer = !receiverIsPlayer;
            LastScorerDriveDirection = NormDir(kickDir);
            FirstHalfKickDirection = NormDir(kickDir);
            kickDirection = NormDir(kickDir);
            // Kicking team "has" the ball until ResolveKickoff.
            isPlayerPossession = !receiverIsPlayer;
            currentYardLine = KickoffTeeAbsoluteYard;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + 10 * NormDir(kickDirection);
            ClearPlayResultFlags();
            UpdateBallPosition();
        }

        /// <summary>Practice sandbox: ball at midfield, 1st &amp; 10, player offense +X.</summary>
        public void SetupPracticeField()
        {
            isPlayerPossession = true;
            driveDirection = 1;
            kickDirection = 1;
            currentYardLine = 50;
            down = 1;
            yardsToGo = 10;
            SetFirstDownMarkers();
            PendingTryAfterTd = false;
            PendingKickoff = false;
            IsTwoPointAttempt = false;
            ClearPlayResultFlags();
            UpdateBallPosition();
        }

        void SetFirstDownMarkers()
        {
            yardsToGo = Mathf.Min(10, GetYardsToEndzone());
            firstDownMarker = Mathf.Clamp(
                currentYardLine + yardsToGo * NormDir(driveDirection),
                0,
                100);
        }

        public string GetDownAndDistance()
        {
            if (IsTwoPointAttempt)
                return "2-PT TRY";

            string ord = down switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => "4th"
            };

            if (yardsToGo >= GetYardsToEndzone())
                return $"{ord} & GOAL";

            return $"{ord} & {yardsToGo}";
        }

        /// <summary>Unclamped absolute yard from world X (0 = left goal, 100 = right goal).</summary>
        public float WorldXToYardUnclamped(float worldX)
        {
            EnsureYardLengthMatchesPaint();
            return (worldX / Mathf.Max(0.01f, yardLength)) + 50f;
        }

        /// <summary>
        /// Absolute yard → world X. Midfield (50) is X=0; yardLength is world units per yard
        /// (synced so 0/100 land on the painted goal lines).
        /// </summary>
        public float YardToWorldX(float yard)
        {
            EnsureYardLengthMatchesPaint();
            return (yard - 50f) * yardLength;
        }

        /// <summary>
        /// Convert world X to absolute yard line 0–100.
        /// Does not report 100 until the ball is actually at/past the goal line.
        /// </summary>
        public int WorldXToYard(float worldX)
        {
            float y = WorldXToYardUnclamped(worldX);
            if (y >= 100f - EndzoneEntryGraceYards) return 100;
            if (y <= EndzoneEntryGraceYards) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(y), 1, 99);
        }

        /// <summary>
        /// True when the carrier has broken the plane of the painted attack goal line
        /// (orange pylons / Field.png white).
        /// </summary>
        public bool IsInScoringEndzone(float worldX)
        {
            float goal = AttackGoalWorldX;
            float eps = EndzoneEntryGraceYards * Mathf.Max(0.01f, yardLength);
            return driveDirection > 0
                ? worldX >= goal - eps
                : worldX <= goal + eps;
        }

        public bool IsInScoringEndzone(Vector3 worldPos)
            => IsInScoringEndzone(worldPos.x);

        /// <summary>
        /// Break-the-plane X for a ball carrier — farthest-downfield of collider,
        /// gotchi sprite bounds, and the football (visual plane, not just pivot).
        /// Ignores FX sprites (throw arc dots, etc.) so aiming into the endzone
        /// cannot falsely break the plane.
        /// </summary>
        /// <param name="attackDir">
        /// +1 toward yard 100, −1 toward yard 0. 0 = use current <see cref="driveDirection"/>.
        /// </param>
        public float ScoringPlaneWorldX(Transform carrier, int attackDir = 0)
        {
            int dir = attackDir != 0 ? NormDir(attackDir) : NormDir(driveDirection);
            float x = carrier != null ? carrier.position.x : 0f;
            bool usedBodySprite = false;

            if (carrier != null)
            {
                // Collider hit radius is smaller than the gotchi sprite — use BOTH
                // so a visual plane break matches the score check.
                var col = carrier.GetComponent<Collider>();
                if (col != null)
                {
                    Bounds b = col.bounds;
                    x = dir > 0 ? Mathf.Max(x, b.max.x) : Mathf.Min(x, b.min.x);
                }

                // Prefer the gotchi body sprite — never throw-arc / ring children.
                var gotchi = carrier.GetComponentInChildren<GotchiFacingView>();
                if (gotchi != null)
                {
                    var gsr = gotchi.GetComponent<SpriteRenderer>();
                    if (gsr != null && gsr.enabled)
                    {
                        Bounds b = gsr.bounds;
                        x = dir > 0 ? Mathf.Max(x, b.max.x) : Mathf.Min(x, b.min.x);
                        usedBodySprite = true;
                    }
                }

                if (!usedBodySprite)
                {
                    var srs = carrier.GetComponentsInChildren<SpriteRenderer>();
                    for (int i = 0; i < srs.Length; i++)
                    {
                        if (srs[i] == null || !srs[i].enabled) continue;
                        if (!IsCarrierBodySprite(srs[i])) continue;
                        Bounds b = srs[i].bounds;
                        x = dir > 0 ? Mathf.Max(x, b.max.x) : Mathf.Min(x, b.min.x);
                        usedBodySprite = true;
                    }
                }

                if (col == null && !usedBodySprite)
                {
                    float r = Mathf.Max(FormationRoster.BodyRadius, FormationRoster.LineNose);
                    x += dir > 0 ? r : -r;
                }
            }

            if (ballTransform != null)
            {
                float bx = ballTransform.position.x;
                var fb = ballTransform.GetComponent<FootballBehavior>();
                if (fb != null)
                    bx = fb.PlayPlanePosition.x;
                // Ball body only — tip/throw arc dots under the ball must not extend the plane.
                var ballSr = ballTransform.GetComponent<SpriteRenderer>();
                if (ballSr == null)
                {
                    var childSrs = ballTransform.GetComponentsInChildren<SpriteRenderer>();
                    for (int i = 0; i < childSrs.Length; i++)
                    {
                        if (childSrs[i] == null || !childSrs[i].enabled) continue;
                        if (!IsCarrierBodySprite(childSrs[i])) continue;
                        ballSr = childSrs[i];
                        break;
                    }
                }
                if (ballSr != null && ballSr.enabled)
                {
                    Bounds bb = ballSr.bounds;
                    bx = dir > 0 ? Mathf.Max(bx, bb.max.x) : Mathf.Min(bx, bb.min.x);
                }
                x = dir > 0 ? Mathf.Max(x, bx) : Mathf.Min(x, bx);
            }

            return x;
        }

        /// <summary>
        /// True for body / ball sprites that can break the plane.
        /// False for throw-arc dots, landing markers, selection rings, etc.
        /// </summary>
        static bool IsCarrierBodySprite(SpriteRenderer sr)
        {
            if (sr == null) return false;
            if (sr.GetComponentInParent<ThrowingArc>() != null) return false;
            if (sr.GetComponentInParent<SelectionRing>() != null) return false;
            if (sr.GetComponentInParent<PlayRoutePreview>() != null) return false;
            return true;
        }

        public bool IsCarrierInScoringEndzone(Transform carrier)
            => IsInScoringEndzone(ScoringPlaneWorldX(carrier));

        /// <summary>Award TD using the carrier's break-the-plane X (not the pivot).</summary>
        public bool TryScoreTouchdown(Transform carrier)
        {
            if (carrier == null) return false;
            return TryScoreTouchdown(ScoringPlaneWorldX(carrier));
        }

        /// <summary>
        /// Award a touchdown after the caller already confirmed break-the-plane.
        /// Skips a second world-X check so pivot/spot mismatches cannot kill the score.
        /// </summary>
        public bool AwardTouchdown()
        {
            if (JustScoredSafety) return false;
            if (JustScoredTouchdown) return true;
            Touchdown();
            return true;
        }

        /// <summary>
        /// Offense's own endzone (behind their goal line) — tackle / OOB / sack here = safety.
        /// </summary>
        public bool IsInOwnEndzone(float worldX)
        {
            EnsureYardLengthMatchesPaint();
            float y = WorldXToYardUnclamped(worldX);
            return driveDirection > 0
                ? y <= EndzoneEntryGraceYards
                : y >= 100f - EndzoneEntryGraceYards;
        }

        public bool IsInOwnEndzone(Vector3 worldPos)
            => IsInOwnEndzone(worldPos.x);

        /// <summary>
        /// Kickoff return scores into the kicking team's endzone (opposite kick flight).
        /// </summary>
        public bool IsInKickReturnScoringEndzone(float worldX)
        {
            EnsureYardLengthMatchesPaint();
            float goal = kickDirection > 0
                ? -RetroLookApplier.PlayableHalfWidth
                : RetroLookApplier.PlayableHalfWidth;
            float eps = EndzoneEntryGraceYards * Mathf.Max(0.01f, yardLength);
            return kickDirection > 0
                ? worldX <= goal + eps
                : worldX >= goal - eps;
        }

        public bool IsInKickReturnScoringEndzone(Vector3 worldPos)
            => IsInKickReturnScoringEndzone(worldPos.x);

        /// <summary>
        /// INT / fumble return scoring endzone by absolute return direction
        /// (not <see cref="driveDirection"/> or <see cref="kickDirection"/>).
        /// </summary>
        public bool IsInDefensiveReturnScoringEndzone(float worldX, bool towardLowEndzone)
        {
            EnsureYardLengthMatchesPaint();
            float goal = towardLowEndzone
                ? -RetroLookApplier.PlayableHalfWidth
                : RetroLookApplier.PlayableHalfWidth;
            float eps = EndzoneEntryGraceYards * Mathf.Max(0.01f, yardLength);
            return towardLowEndzone
                ? worldX <= goal + eps
                : worldX >= goal - eps;
        }

        public bool IsInDefensiveReturnScoringEndzone(Vector3 worldPos, bool towardLowEndzone)
            => IsInDefensiveReturnScoringEndzone(worldPos.x, towardLowEndzone);

        /// <summary>Receiving-team touchback spot (own yards) after kickoff.</summary>
        public const int KickoffTouchbackOwnYard = 35;

        /// <summary>Receiving team's own endzone during a kickoff (deep / touchback side).</summary>
        public bool IsInKickReceiveEndzone(float worldX)
        {
            float y = WorldXToYardUnclamped(worldX);
            return kickDirection > 0
                ? y >= 100f - EndzoneEntryGraceYards
                : y <= EndzoneEntryGraceYards;
        }

        /// <summary>
        /// Yards past the goal line to the end line / goalposts (~10 yd NFL depth;
        /// matches Field.png blue EZ back at <see cref="RetroBowl.Gameplay.RetroLookApplier.GoalpostWorldXAbs"/>).
        /// Crossing this = out the back of the receive endzone → touchback.
        /// </summary>
        public const float KickReceiveEndLineDepthYards = 10f;

        /// <summary>Past the back line of the receive endzone (out the back of the EZ).</summary>
        public bool IsPastKickReceiveEndLine(float worldX)
        {
            float y = WorldXToYardUnclamped(worldX);
            return kickDirection > 0
                ? y >= 100f + KickReceiveEndLineDepthYards
                : y <= -KickReceiveEndLineDepthYards;
        }

        /// <summary>
        /// Loose kickoff becomes a touchback: out the back of the EZ, or sideline OOB
        /// while in / past the receive endzone.
        /// </summary>
        public bool IsKickoffLooseTouchback(Vector3 playPlanePos)
        {
            if (IsPastKickReceiveEndLine(playPlanePos.x))
                return true;
            if (!IsOutOfBounds(playPlanePos))
                return false;
            // Sideline OOB only counts as TB in the receive endzone (not midfield).
            return IsInKickReceiveEndzone(playPlanePos.x)
                   || IsPastKickReceiveEndLine(playPlanePos.x);
        }

        public int GetYardsToEndzone()
        {
            return driveDirection > 0
                ? Mathf.Max(0, 100 - currentYardLine)
                : Mathf.Max(0, currentYardLine);
        }

        /// <summary>
        /// Absolute tee yard for the kicking team's own 35 given kickDirection.
        /// </summary>
        public int KickoffTeeAbsoluteYard
            => kickDirection > 0 ? 35 : 65;

        /// <summary>
        /// Receiving-team own yards from an absolute spot, assuming they will drive
        /// opposite the current kickDirection.
        /// </summary>
        public int ReceivingOwnYardsFromAbs(int absYard)
        {
            int recvDir = -NormDir(kickDirection);
            return recvDir > 0 ? absYard : 100 - absYard;
        }

        public bool JustGotFirstDown { get; private set; }
        /// <summary>True after AdvanceBall flips possession on a failed 4th down.</summary>
        public bool JustTurnedOver { get; private set; }
        /// <summary>True after a touchdown this play — blocks double-scoring until the next snap.</summary>
        public bool JustScoredTouchdown { get; private set; }
        /// <summary>True after a safety this play — defense scored 2; kickoff pending.</summary>
        public bool JustScoredSafety { get; private set; }

        /// <summary>Waiting for PAT / 2PT choice (or AI auto-XP) after a TD.</summary>
        public bool PendingTryAfterTd { get; private set; }
        /// <summary>Kickoff mini-game should run next.</summary>
        public bool PendingKickoff { get; private set; }
        /// <summary>Who just scored (for try + kickoff receiving team).</summary>
        public bool LastScorerWasPlayer { get; private set; }
        /// <summary>Drive direction of the scoring team (ensuing kickoff uses this as kickDirection).</summary>
        public int LastScorerDriveDirection { get; private set; } = 1;
        /// <summary>Live 2-point conversion attempt from the 2.</summary>
        public bool IsTwoPointAttempt { get; private set; }
        /// <summary>Successful 2-point conversion this play (banner / kickoff).</summary>
        public bool JustConvertedTwoPoint { get; private set; }
        /// <summary>Failed 2-point try this play (tackle / incomplete / INT).</summary>
        public bool JustFailedTwoPoint { get; private set; }
        /// <summary>Receiving team for the pending / active kickoff.</summary>
        public bool KickoffReceiverIsPlayer { get; private set; }

        /// <summary>First-half kick direction from the coin toss (ends switch at half).</summary>
        public int FirstHalfKickDirection { get; set; } = 1;

        /// <summary>Who receives to open the 2nd half (coin toss deferral).</summary>
        public bool SecondHalfReceiverIsPlayer { get; set; } = true;

        /// <summary>True until the Q3 kickoff from the coin toss has been started.</summary>
        public bool HasSecondHalfKickoffPending { get; set; }

        /// <summary>
        /// Kick loft / live return has started — mini-game is done.
        /// Prevents PostScoreFlow from re-entering kickoff on the next banner.
        /// </summary>
        public void ConsumePendingKickoff() => PendingKickoff = false;

        /// <summary>Clear per-play result flags (call on snap). Does not clear try/kickoff flow.</summary>
        public void ClearPlayResultFlags()
        {
            JustGotFirstDown = false;
            JustTurnedOver = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            JustConvertedTwoPoint = false;
            JustFailedTwoPoint = false;
        }

        /// <summary>Banner title after a score spot (TD vs 2-point good).</summary>
        public string GetScoreBannerTitle()
        {
            if (JustConvertedTwoPoint) return "2-POINT GOOD!\n+2";
            if (JustScoredSafety) return "SAFETY\n+2";
            return "TOUCHDOWN";
        }

        /// <summary>
        /// Award a TD if the spot is in the scoring endzone and we have not already
        /// scored this play.
        /// </summary>
        public bool TryScoreTouchdown(float worldX)
        {
            if (JustScoredTouchdown || JustScoredSafety) return JustScoredTouchdown;
            if (!IsInScoringEndzone(worldX)) return false;
            Touchdown();
            return true;
        }

        /// <summary>
        /// Offense downed / OOB in their own endzone → defense scores a safety (2 pts + kickoff).
        /// Not awarded on 2-point tries (failed conversion instead).
        /// </summary>
        public bool TryScoreSafety(float worldX)
        {
            if (JustScoredTouchdown || JustScoredSafety) return JustScoredSafety;
            if (IsTwoPointAttempt) return false;
            if (GameManager.Instance != null
                && (GameManager.Instance.isKickoffReturn || GameManager.Instance.isInterceptionReturn))
                return false;
            if (!IsInOwnEndzone(worldX)) return false;
            Safety();
            return true;
        }

        public void AdvanceBall(int yards)
        {
            // Play already ended in a score — ignore further spotting.
            if (JustScoredTouchdown || JustScoredSafety || JustFailedTwoPoint) return;

            JustGotFirstDown = false;
            JustTurnedOver = false;
            currentYardLine += yards * NormDir(driveDirection);
            currentYardLine = Mathf.Clamp(currentYardLine, 0, 100);

            // 2-point try: one play only — score or fail (no downs).
            if (IsTwoPointAttempt)
            {
                if (HasReachedOrPassed(currentYardLine, driveDirection > 0 ? 100 : 0)
                    && (driveDirection > 0 ? currentYardLine >= 100 : currentYardLine <= 0))
                {
                    Touchdown();
                }
                else
                {
                    FailTwoPointAttempt();
                }
                UpdateBallPosition();
                return;
            }

            if (HasReachedOrPassed(currentYardLine, driveDirection > 0 ? 100 : 0)
                && (driveDirection > 0 ? currentYardLine >= 100 : currentYardLine <= 0))
            {
                Touchdown();
                return;
            }

            if (HasReachedOrPassed(currentYardLine, firstDownMarker))
            {
                JustGotFirstDown = true;
                FirstDown();
            }
            else
            {
                down++;
                yardsToGo = Mathf.Abs(firstDownMarker - currentYardLine);
                if (down > 4)
                {
                    Turnover();
                    JustTurnedOver = true;
                }
            }

            UpdateBallPosition();
        }

        void FirstDown()
        {
            down = 1;
            SetFirstDownMarkers();
            Debug.Log("First Down!");
        }

        void Touchdown()
        {
            if (JustScoredTouchdown || JustScoredSafety) return;

            JustScoredTouchdown = true;
            JustScoredSafety = false;
            JustGotFirstDown = false;
            JustTurnedOver = false;

            bool scorerIsPlayer = isPlayerPossession;
            LastScorerDriveDirection = NormDir(driveDirection);

            // Successful 2-point conversion.
            if (IsTwoPointAttempt)
            {
                Debug.Log($"{(scorerIsPlayer ? "Player" : "Opponent")} 2-POINT CONVERSION!");
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(scorerIsPlayer, 2);
                IsTwoPointAttempt = false;
                PendingTryAfterTd = false;
                JustConvertedTwoPoint = true;
                JustFailedTwoPoint = false;
                LastScorerWasPlayer = scorerIsPlayer;
                PendingKickoff = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.CheckSuddenDeathWin();
                return;
            }

            JustConvertedTwoPoint = false;
            JustFailedTwoPoint = false;

            Debug.Log($"{(scorerIsPlayer ? "Player" : "Opponent")} Touchdown!");

            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scorerIsPlayer, 6);

            if (ScoreManager.Instance != null)
            {
                if (scorerIsPlayer)
                    ScoreManager.Instance.playerStats.touchdowns++;
                else
                    ScoreManager.Instance.opponentStats.touchdowns++;
            }

            // Keep possession + driveDirection for the try; kickoff comes after PAT / 2PT.
            LastScorerWasPlayer = scorerIsPlayer;
            PendingTryAfterTd = true;
            PendingKickoff = false;
            IsTwoPointAttempt = false;
            // XP hash: 15 yards from the scoring endzone.
            currentYardLine = driveDirection > 0 ? 85 : 15;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo * NormDir(driveDirection);
            UpdateBallPosition();
        }

        /// <summary>
        /// Defense scores 2 for tackling / OOB / sack in the offense's own endzone.
        /// Scoring team kicks off (same PendingKickoff path as FG).
        /// </summary>
        void Safety()
        {
            if (JustScoredSafety || JustScoredTouchdown) return;

            JustScoredSafety = true;
            JustScoredTouchdown = false;
            JustGotFirstDown = false;
            JustTurnedOver = false;
            IsTwoPointAttempt = false;
            PendingTryAfterTd = false;

            bool defenseIsPlayer = !isPlayerPossession;
            LastScorerWasPlayer = defenseIsPlayer;
            // Defense scored — they kick the ensuing kickoff toward the end they defend.
            LastScorerDriveDirection = -NormDir(driveDirection);

            Debug.Log($"{(defenseIsPlayer ? "Player" : "Opponent")} Safety! +2");

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ScoreSafety(defenseIsPlayer);
            else if (GameManager.Instance != null)
                GameManager.Instance.AddScore(defenseIsPlayer, 2);

            PendingKickoff = true;
            // Park ball at own 20 of the team that gave up the safety (kick tee prep).
            currentYardLine = driveDirection > 0 ? 20 : 80;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo * NormDir(driveDirection);
            UpdateBallPosition();

            if (GameManager.Instance != null)
                GameManager.Instance.CheckSuddenDeathWin();
        }

        public void SpotForExtraPoint()
        {
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            isPlayerPossession = LastScorerWasPlayer;
            driveDirection = NormDir(LastScorerDriveDirection);
            currentYardLine = driveDirection > 0 ? 85 : 15;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + yardsToGo * NormDir(driveDirection);
            UpdateBallPosition();
        }

        public void SpotForTwoPoint()
        {
            PendingTryAfterTd = false;
            IsTwoPointAttempt = true;
            JustConvertedTwoPoint = false;
            JustFailedTwoPoint = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            isPlayerPossession = LastScorerWasPlayer;
            driveDirection = NormDir(LastScorerDriveDirection);
            // Offense at the opponent 2 (absolute 98 or 2 depending on drive dir).
            currentYardLine = driveDirection > 0 ? 98 : 2;
            down = 1;
            yardsToGo = 2;
            firstDownMarker = driveDirection > 0 ? 100 : 0;
            UpdateBallPosition();
            Debug.Log($"2-POINT TRY — ball at abs {currentYardLine}, dir {driveDirection}");
        }

        public void FailTwoPointAttempt()
        {
            if (JustConvertedTwoPoint || JustFailedTwoPoint) return;
            Debug.Log("2-POINT CONVERSION FAILED");
            IsTwoPointAttempt = false;
            PendingTryAfterTd = false;
            JustFailedTwoPoint = true;
            JustConvertedTwoPoint = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            PendingKickoff = true;
        }

        /// <summary>PAT result from kick mini-game or AI auto.</summary>
        public void ResolveExtraPoint(bool good)
        {
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            if (good && GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(LastScorerWasPlayer, 1);
                Debug.Log("Extra Point Good!");
            }
            else
            {
                Debug.Log("Extra Point Missed!");
            }

            PendingKickoff = true;
            if (GameManager.Instance != null)
                GameManager.Instance.CheckSuddenDeathWin();
        }

        public void PrepareKickoff(bool receiverIsPlayer)
        {
            KickoffReceiverIsPlayer = receiverIsPlayer;
            PendingKickoff = true;
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            JustConvertedTwoPoint = false;
            JustFailedTwoPoint = false;
            // Scorer kicks continuing the same field direction they just scored.
            kickDirection = NormDir(LastScorerDriveDirection);
            // Kicking team still "has" the ball until ResolveKickoff.
            isPlayerPossession = !receiverIsPlayer;
            currentYardLine = KickoffTeeAbsoluteYard;
            down = 1;
            yardsToGo = 10;
            firstDownMarker = currentYardLine + 10 * NormDir(kickDirection);
            UpdateBallPosition();
        }

        /// <summary>
        /// Spot the receiving team after the kickoff mini-game (no live return).
        /// <paramref name="spotYard"/> is receiving-team own yards (e.g. touchback 35).
        /// World X stays absolute — no remap flip of the ball.
        /// </summary>
        public void ResolveKickoff(int spotYard, bool touchback)
        {
            PendingKickoff = false;
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            JustGotFirstDown = false;
            JustTurnedOver = false;
            isPlayerPossession = KickoffReceiverIsPlayer;
            driveDirection = -NormDir(kickDirection);
            if (touchback)
                spotYard = KickoffTouchbackOwnYard;
            currentYardLine = Mathf.Clamp(OwnToAbsolute(Mathf.Clamp(spotYard, 1, 50)), 1, 99);
            down = 1;
            SetFirstDownMarkers();
            UpdateBallPosition();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.playLosYard = currentYardLine;
                GameManager.Instance.isKickoffReturn = false;
                GameManager.Instance.CheckSuddenDeathWin();
            }

            RetroLookApplier.SetGoalpostsVisible(true);

            Debug.Log(touchback
                ? $"Touchback — abs {currentYardLine} (own {OwnYardLine}), dir {driveDirection}"
                : $"Kickoff — abs {currentYardLine} (own {OwnYardLine}), dir {driveDirection}");
        }

        /// <summary>
        /// Spot after a kickoff return tackle / OOB / dive.
        /// <paramref name="absoluteYard"/> is world/absolute field yard (0–100).
        /// Keeps absolute world spot; sets driveDirection = −kickDirection.
        /// </summary>
        public void ResolveKickoffReturnSpot(int absoluteYard)
        {
            PendingKickoff = false;
            PendingTryAfterTd = false;
            IsTwoPointAttempt = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            JustGotFirstDown = false;
            JustTurnedOver = false;
            isPlayerPossession = KickoffReceiverIsPlayer;
            driveDirection = -NormDir(kickDirection);
            currentYardLine = Mathf.Clamp(absoluteYard, 1, 99);
            down = 1;
            SetFirstDownMarkers();
            UpdateBallPosition();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.playLosYard = currentYardLine;
                GameManager.Instance.isKickoffReturn = false;
                GameManager.Instance.CheckSuddenDeathWin();
            }

            RetroLookApplier.SetGoalpostsVisible(true);

            Debug.Log($"Kickoff return — abs {currentYardLine} (own {OwnYardLine}), dir {driveDirection}");
        }

        /// <summary>Kickoff return reaches the kicking team's endzone.</summary>
        public bool TryScoreKickReturnTouchdown(float worldX)
        {
            if (JustScoredTouchdown) return true;
            if (!IsInKickReturnScoringEndzone(worldX)) return false;
            isPlayerPossession = KickoffReceiverIsPlayer;
            // Scorer was returning opposite kick — their scoring driveDirection matches return.
            driveDirection = -NormDir(kickDirection);
            PendingKickoff = false;
            Touchdown();
            if (GameManager.Instance != null)
                GameManager.Instance.isKickoffReturn = false;
            RetroLookApplier.SetGoalpostsVisible(true);
            return true;
        }

        /// <summary>
        /// Interception return TD (pick-six). Scorer is the intercepting team.
        /// <paramref name="towardLowEndzone"/> = running toward yard 0 (−X).
        /// </summary>
        public bool TryScoreInterceptionReturnTouchdown(
            float worldX, bool scorerIsPlayer, bool towardLowEndzone)
        {
            if (JustScoredTouchdown) return true;
            if (!IsInDefensiveReturnScoringEndzone(worldX, towardLowEndzone))
                return false;

            isPlayerPossession = scorerIsPlayer;
            driveDirection = towardLowEndzone ? -1 : 1;
            PendingKickoff = false;
            Touchdown();
            if (GameManager.Instance != null)
                GameManager.Instance.isInterceptionReturn = false;
            RetroLookApplier.SetGoalpostsVisible(true);
            return true;
        }

        /// <summary>
        /// INT return tackled / OOB — flip possession at the world spot, 1st &amp; 10.
        /// </summary>
        public void ResolveInterceptionReturnSpot(int worldYard)
            => ResolveDefensiveReturnSpot(worldYard, "INTERCEPTION");

        /// <summary>
        /// INT / fumble return tackled / OOB — flip possession at the world spot, 1st &amp; 10.
        /// </summary>
        public void ResolveDefensiveReturnSpot(int worldYard, string reason)
        {
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            if (GameManager.Instance != null)
                GameManager.Instance.isInterceptionReturn = false;
            ChangeOfPossession(string.IsNullOrEmpty(reason) ? "TURNOVER" : reason, worldYard);
            if (GameManager.Instance != null)
                GameManager.Instance.playLosYard = currentYardLine;
        }

        void Turnover()
        {
            Debug.Log("Turnover on Downs!");
            ChangeOfPossession("TURNOVER ON DOWNS", currentYardLine);
        }

        /// <summary>Banner line after AdvanceBall — TD / TOD / first down / down-distance.</summary>
        public string GetPlayResultDownLine()
        {
            if (JustScoredSafety) return "SAFETY!";
            if (JustConvertedTwoPoint) return "2-POINT GOOD!";
            if (JustFailedTwoPoint) return "2-POINT NO GOOD";
            if (JustScoredTouchdown) return "TOUCHDOWN!";
            if (JustTurnedOver) return "TURNOVER ON DOWNS!";
            if (JustGotFirstDown) return "FIRST DOWN!";
            if (IsTwoPointAttempt) return "2-POINT TRY";
            return GetDownAndDistance();
        }

        /// <summary>
        /// Flip possession at an absolute spot. New offense attacks the opposite endzone
        /// (driveDirection flips). World X is not remapped.
        /// </summary>
        public void ChangeOfPossession(string reason = null, int? spotYard = null)
        {
            // INT / fumble on a 2-point try — dead ball, kickoff (no return drive).
            if (IsTwoPointAttempt)
            {
                FailTwoPointAttempt();
                return;
            }

            JustGotFirstDown = false;
            JustTurnedOver = false;
            JustScoredTouchdown = false;
            JustScoredSafety = false;
            int yard = spotYard ?? currentYardLine;
            yard = Mathf.Clamp(yard, 0, 100);
            isPlayerPossession = !isPlayerPossession;
            driveDirection = -NormDir(driveDirection);
            currentYardLine = Mathf.Clamp(yard, 1, 99);
            down = 1;
            SetFirstDownMarkers();
            UpdateBallPosition();
            Debug.Log(
                $"Change of possession{(string.IsNullOrEmpty(reason) ? "" : $": {reason}")}"
                + $" — abs {currentYardLine} (own {OwnYardLine}), dir {driveDirection}");
        }

        /// <summary>End the play at an absolute spot without flipping possession.</summary>
        public void SpotBallAfterPlay(int endYardAbs)
        {
            if (JustScoredTouchdown || JustScoredSafety) return;
            JustGotFirstDown = false;
            JustTurnedOver = false;
            int startYard = currentYardLine;
            int gained = YardsGained(startYard, endYardAbs);
            AdvanceBall(gained);
        }

        /// <summary>NFL-style FG distance: yards to EZ + ~17 (snap/hold).</summary>
        public int GetFieldGoalDistance()
            => Mathf.Max(18, GetYardsToEndzone() + 17);

        /// <summary>AI / legacy RNG field goal (no kick mini-game).</summary>
        public void FieldGoalAttempt()
        {
            int distance = GetFieldGoalDistance();
            float successChance = Mathf.Clamp(1f - (distance / 70f), 0.3f, 0.95f);
            bool success = Random.value < successChance;
            ResolveFieldGoal(success, distance);
        }

        /// <summary>Apply FG result after the kick mini-game (or AI RNG).</summary>
        public void ResolveFieldGoal(bool good, int distance)
        {
            LastScorerWasPlayer = isPlayerPossession;
            if (good && GameManager.Instance != null)
            {
                Debug.Log($"Field Goal Good! ({distance} yds)");
                LastScorerDriveDirection = NormDir(driveDirection);
                GameManager.Instance.AddScore(isPlayerPossession, 3);
                if (ScoreManager.Instance != null)
                {
                    if (isPlayerPossession)
                        ScoreManager.Instance.playerStats.fieldGoals++;
                    else
                        ScoreManager.Instance.opponentStats.fieldGoals++;
                }

                PendingKickoff = true;
                PendingTryAfterTd = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.CheckSuddenDeathWin();
            }
            else
            {
                Debug.Log($"Field Goal Missed! ({distance} yds)");
                // Missed FG — other team takes over at previous LOS (absolute).
                PendingKickoff = false;
                ChangeOfPossession("MISSED FG", currentYardLine);
            }
        }

        /// <summary>AI / legacy RNG punt (no kick mini-game).</summary>
        public void Punt()
        {
            ResolvePunt(Random.Range(35, 55));
        }

        /// <summary>Apply punt result after the kick mini-game (or AI RNG).</summary>
        public void ResolvePunt(int puntDistance)
        {
            puntDistance = Mathf.Clamp(puntDistance, 15, 70);
            int landAbs = currentYardLine + puntDistance * NormDir(driveDirection);
            landAbs = Mathf.Clamp(landAbs, 0, 100);

            // Touchback if into the endzone — new offense at own 20.
            bool touchback = driveDirection > 0 ? landAbs >= 100 : landAbs <= 0;
            isPlayerPossession = !isPlayerPossession;
            driveDirection = -NormDir(driveDirection);
            if (touchback)
                currentYardLine = OwnToAbsolute(20);
            else
                currentYardLine = Mathf.Clamp(landAbs, 1, 99);

            down = 1;
            SetFirstDownMarkers();
            Debug.Log(
                $"Punt! {puntDistance} yds → abs {currentYardLine} (own {OwnYardLine}), dir {driveDirection}");
            UpdateBallPosition();
        }

        void UpdateBallPosition()
        {
            if (ballTransform == null) return;

            // Never yank the live football to the LOS mid-play or during the
            // result banner (COMPLETE PASS / tackle). FormationRoster parks a
            // clean snap ball on ReadyNextPlay / PlaceOnly.
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.waitingForNextPlay) return;
                if (!GameManager.Instance.isPreSnap) return;
            }

            // Spot between plays: X = absolute yard, Y = hashes.
            float xPosition = YardToWorldX(currentYardLine);
            ballTransform.position = new Vector3(xPosition, 0f, 0f);
        }
    }
}
