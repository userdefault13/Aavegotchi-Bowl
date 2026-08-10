using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public enum LiveReturnKind
    {
        Interception,
        Fumble
    }

    /// <summary>
    /// Live defensive return (INT or fumble recovery) — AI carrier sprints toward the
    /// prior offense's endzone. Scoop-six / pick-six on score; tackle / OOB spots + flips.
    /// </summary>
    public class InterceptionReturner : MonoBehaviour
    {
        public static InterceptionReturner Active { get; private set; }

        [Header("Return")]
        public float moveSpeed = 3.8f;
        public float acceleration = 11f;
        public float tackleRadius = 0.9f;
        public float breakImmunity = 0.55f;

        public bool IsActive { get; private set; }
        public bool TowardLowEndzone { get; private set; }
        public bool ScorerIsPlayer { get; private set; }
        public LiveReturnKind Kind { get; private set; } = LiveReturnKind.Interception;

        Rigidbody rb;
        SpriteRenderer spriteRenderer;
        Vector3 velocity;
        float immuneUntil;
        float returnBannerAt = -1f;
        bool tacklePending;
        string originalTag = "Defender";

        public static bool TryGetActive(out InterceptionReturner ret)
        {
            ret = Active;
            return ret != null && ret.IsActive;
        }

        public static bool IsUnitReturning(Component unit)
            => TryGetActive(out var ret) && ret != null && unit != null && ret.transform == unit.transform;

        public void Begin(bool offenseWasPlayer)
            => Begin(offenseWasPlayer, LiveReturnKind.Interception);

        public void Begin(bool offenseWasPlayer, LiveReturnKind kind)
        {
            EnsureBody();

            Kind = kind;
            // Returning team = defense that took the ball away.
            ScorerIsPlayer = !offenseWasPlayer;
            // Return toward the endzone the prior offense was defending.
            int priorDir = FieldManager.Instance != null
                ? FieldManager.NormDir(FieldManager.Instance.driveDirection)
                : (offenseWasPlayer ? 1 : -1);
            TowardLowEndzone = priorDir > 0;
            IsActive = true;
            Active = this;
            tacklePending = false;
            velocity = Vector3.zero;
            immuneUntil = Time.time + 0.55f;
            returnBannerAt = Time.time + 1.05f;

            originalTag = gameObject.tag;
            try { gameObject.tag = "BallCarrier"; }
            catch (UnityException) { }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isInterceptionReturn = true;
                GameManager.Instance.isPreSnap = false;
                GameManager.Instance.waitingForNextPlay = false;
                GameManager.Instance.isKicking = false;
            }

            var dai = GetComponent<DefenderAI>();
            if (dai != null)
                dai.IsPlayerControlled = false;

            FollowCamera();
            if (kind == LiveReturnKind.Fumble)
                PlayBanner.Show($"RECOVERED BY {gameObject.name}", 1.0f, BannerTone.Turnover);
            else
                PlayBanner.Show("INTERCEPTION", 1.0f, BannerTone.Turnover);

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RecordTurnover(offenseWasPlayer);

            Debug.Log($"{gameObject.name} {kind} return — toward {(TowardLowEndzone ? "yard 0" : "yard 100")}");

            // Recovered / intercepted already in the scoring endzone → score now (no return sprint).
            TryScoreIfAlreadyInEndzone();
        }

        /// <summary>
        /// If the returner is already in the defensive scoring endzone (e.g. fumble
        /// recovery in the offense's own EZ), award the TD immediately.
        /// </summary>
        public bool TryScoreIfAlreadyInEndzone()
            => IsActive && TryFinishTouchdown();

        void EnsureBody()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                    rb = gameObject.AddComponent<Rigidbody>();
                ArcadeMove.ConfigureKinematicBody(rb);
            }

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void Update()
        {
            if (!IsActive) return;

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.waitingForNextPlay
                || (FieldManager.Instance != null && FieldManager.Instance.JustScoredTouchdown))
            {
                velocity = Vector3.zero;
                return;
            }

            if (returnBannerAt > 0f && Time.time >= returnBannerAt)
            {
                returnBannerAt = -1f;
                PlayBanner.Show("RETURN!", 0.9f, BannerTone.Neutral);
            }

            if (tacklePending)
            {
                velocity = Vector3.zero;
                return;
            }

            if (PlayerStun.IsUnitStunned(this))
            {
                velocity = Vector3.zero;
                return;
            }

            RunAiReturn();

            if (TryFinishTouchdown())
                return;

            if (IsOutOfBounds())
            {
                if (FieldManager.Instance != null)
                    transform.position = FieldManager.Instance.ClampInBounds(transform.position);
                FinishDown("RETURN — OUT OF BOUNDS");
                return;
            }
        }

        void FixedUpdate()
        {
            if (!IsActive || rb == null) return;

            if (tacklePending
                || GameManager.Instance == null
                || GameManager.Instance.waitingForNextPlay)
            {
                ArcadeMove.Apply(rb, Vector3.zero);
                return;
            }

            ArcadeMove.Apply(rb, velocity);
        }

        void RunAiReturn()
        {
            float dirX = TowardLowEndzone ? -1f : 1f;
            // Sideline awareness — weave away from the nearest sideline when close.
            float y = transform.position.y;
            float sideline = FieldManager.Instance != null
                ? FieldManager.Instance.sidelineHalf
                : RetroLookApplier.SidelineY;
            float edge = sideline - 0.85f;
            float weave = Mathf.Sin(Time.time * 3.2f) * 2.2f;
            if (Mathf.Abs(y) > edge * 0.72f)
                weave = -Mathf.Sign(y) * 3.4f;

            Vector3 targetVel = new Vector3(moveSpeed * dirX, weave, 0f);
            if (targetVel.sqrMagnitude > 0.01f)
                targetVel = targetVel.normalized * moveSpeed;
            velocity = Vector3.Lerp(velocity, targetVel, acceleration * Time.deltaTime);

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        /// <summary>Offense pursuer wrap attempt. Returns true if the return ended.</summary>
        public bool TryAcceptTackle(Transform tackler)
        {
            if (!IsActive) return false;
            // Second defender can still stack HP during an active mash lock.
            if (TecmoContact.IsActiveForCarrier(transform) && tackler != null)
                return TecmoContact.TryJoinTackler(tackler);

            if (tacklePending) return false;
            if (Time.time < immuneUntil) return false;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return false;
            if (FieldManager.Instance != null && FieldManager.Instance.JustScoredTouchdown)
                return false;
            if (TryFinishTouchdown()) return true;

            if (GameRules.EnableTecmoContact && tackler != null)
            {
                tacklePending = true;
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                if (TecmoContact.Begin(
                        transform,
                        tackler,
                        onBroken: () =>
                        {
                            tacklePending = false;
                            immuneUntil = Time.time + breakImmunity;
                            float dirX = TowardLowEndzone ? -1f : 1f;
                            transform.position += Vector3.right * (0.45f * dirX);
                        },
                        onTackled: () => FinishDown("RETURN")))
                    return true;
                tacklePending = false;
            }

            bool tackled = SoftTackle.ResolveWrap(
                transform,
                tackler,
                isPocketSack: false,
                playerCarrier: false);

            if (!tackled)
            {
                immuneUntil = Time.time + breakImmunity;
                // Nudge toward scoring endzone on a break.
                float dirX = TowardLowEndzone ? -1f : 1f;
                transform.position += Vector3.right * (0.45f * dirX);
                return false;
            }

            tacklePending = true;
            velocity = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);
            FinishDown("RETURN");
            return true;
        }

        bool TryFinishTouchdown()
        {
            if (!IsActive || FieldManager.Instance == null) return false;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return false;

            // Absolute return endzone — do NOT use driveDirection / kickDirection helpers
            // (those still reflect the prior offense / kickoff and miss scoop-six / pick-six).
            int returnDir = TowardLowEndzone ? -1 : 1;
            float planeX = FieldManager.Instance.ScoringPlaneWorldX(transform, returnDir);
            if (!FieldManager.Instance.IsInDefensiveReturnScoringEndzone(
                    planeX, TowardLowEndzone))
                return false;

            ClearCarrierState(detachBall: true);

            if (FieldManager.Instance.TryScoreInterceptionReturnTouchdown(
                    planeX, ScorerIsPlayer, TowardLowEndzone)
                || FieldManager.Instance.JustScoredTouchdown)
            {
                string td = Kind == LiveReturnKind.Fumble
                    ? "SCOOP SIX\nTOUCHDOWN"
                    : "PICK SIX\nTOUCHDOWN";
                PlayBanner.ShowPlayOver(td, BannerTone.Positive);
                Debug.Log(
                    $"{gameObject.name} {td.Replace('\n', ' ')} " +
                    $"({(ScorerIsPlayer ? "player" : "opponent")} +6)");
                return true;
            }

            return false;
        }

        void FinishDown(string header)
        {
            if (!IsActive) return;

            if (TryFinishTouchdown())
                return;

            int spotYard = 20;
            if (FieldManager.Instance != null)
            {
                spotYard = Mathf.Clamp(
                    FieldManager.Instance.WorldXToYard(transform.position.x),
                    1,
                    99);
            }

            ClearCarrierState(detachBall: true);

            string reason = Kind == LiveReturnKind.Fumble ? "FUMBLE" : "INTERCEPTION";
            if (FieldManager.Instance != null)
                FieldManager.Instance.ResolveDefensiveReturnSpot(spotYard, reason);
            else if (GameManager.Instance != null)
                GameManager.Instance.isInterceptionReturn = false;

            string title = Kind == LiveReturnKind.Fumble
                ? (header.StartsWith("RETURN") ? "FUMBLE RETURN" : header)
                : header;

            // Neutral tone — avoid replaying the turnover sting on the spot banner.
            PlayBanner.ShowPlayOver(
                $"{title}\nBALL AT THE {spotYard}\nFIRST DOWN",
                BannerTone.Neutral);
            Debug.Log($"{reason} return down — ball at {spotYard}");
        }

        void ClearCarrierState(bool detachBall)
        {
            IsActive = false;
            if (Active == this) Active = null;
            tacklePending = false;
            velocity = Vector3.zero;
            returnBannerAt = -1f;
            // isInterceptionReturn stays up until Resolve / pick-six / HaltLivePlaySystems
            // so DefensePlayDirector does not arm between Clear and ShowPlayOver.

            if (detachBall)
                DetachHeldBall();

            try { gameObject.tag = string.IsNullOrEmpty(originalTag) ? "Defender" : originalTag; }
            catch (UnityException)
            {
                try { gameObject.tag = "Defender"; }
                catch (UnityException) { }
            }

            ReturnCameraToOffense();
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

                child.SetParent(null, true);
                if (fb != null)
                {
                    Vector3 spot = transform.position;
                    spot.z = 0f;
                    fb.ResetToParked(spot);
                }
            }
        }

        bool IsOutOfBounds()
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.IsOutOfBounds(transform.position);
            return Mathf.Abs(transform.position.y) >= RetroLookApplier.SidelineY;
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.SetTarget(transform);

            var ring = GameObject.Find("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(transform);
            }
        }

        static void ReturnCameraToOffense()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc != null)
                cc.FollowPlayer();
        }

        void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
                if (GameManager.Instance != null)
                    GameManager.Instance.isInterceptionReturn = false;
            }
        }
    }
}
