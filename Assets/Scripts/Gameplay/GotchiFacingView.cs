using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Swaps USDC/UNI 4-view sprites from planar movement (replaces flipX mirroring).
    /// Skin is locked to P1=USDC / P2=UNI. Pre-snap facing uses offense/defense
    /// side so teams still look at each other across the LOS.
    /// Also Y-sorts so lower-on-screen gotchis draw on top.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(200)]
    public class GotchiFacingView : MonoBehaviour
    {
        [Tooltip("True = P1 USDC; false = P2 UNI. Independent of offense/defense.")]
        public bool playerOne = true;

        [Tooltip("True = current offense side (faces drive/kick); false = defense side.")]
        public bool offenseSide = true;

        /// <summary>Legacy alias for <see cref="playerOne"/> (skin, not LOS role).</summary>
        public bool offenseTeam
        {
            get => playerOne;
            set => playerOne = value;
        }

        [Tooltip("Optional: drive facing from this body (defaults to parent / root).")]
        public Transform motionRoot;

        SpriteRenderer sr;
        GotchiFacing facing = GotchiFacing.Front;
        Vector3 lastWorldPos;
        bool hasLastPos;

        public GotchiFacing Facing => facing;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (motionRoot == null)
            {
                motionRoot = transform.parent != null ? transform.parent : transform;
            }
            ApplyFacing(PreSnapFacing(offenseSide), force: true);
        }

        void OnEnable()
        {
            hasLastPos = false;
            ApplyFacing(IsPreSnap() ? PreSnapFacing(offenseSide) : facing, force: true);
        }

        void LateUpdate()
        {
            if (sr == null) return;

            // Dedicated L/R art — never mirror.
            sr.flipX = false;
            ApplyDepthSort();

            if (IsPreSnap())
            {
                ApplyFacing(PreSnapFacing(offenseSide), force: false);
                return;
            }

            Vector2 planar = ReadPlanarDelta();
            var next = GotchiTeamSprites.FacingFromVelocity(planar, facing);
            if (next != facing)
                ApplyFacing(next, force: false);
        }

        /// <summary>Controllers may push velocity directly for snappier facing.</summary>
        public void SetPlanarVelocity(Vector2 planar)
        {
            if (IsPreSnap())
            {
                ApplyFacing(PreSnapFacing(offenseSide), force: false);
                return;
            }

            var next = GotchiTeamSprites.FacingFromVelocity(planar, facing);
            ApplyFacing(next, force: false);
        }

        /// <summary>Set franchise skin (P1 USDC / P2 UNI) and LOS role (offense/defense).</summary>
        public void SetIdentity(bool isPlayerOne, bool isOffenseSide)
        {
            bool changed = playerOne != isPlayerOne || offenseSide != isOffenseSide;
            playerOne = isPlayerOne;
            offenseSide = isOffenseSide;
            if (changed || sr == null || sr.sprite == null)
                ApplyFacing(IsPreSnap() ? PreSnapFacing(offenseSide) : facing, force: true);
        }

        /// <summary>Legacy — sets skin only; prefer <see cref="SetIdentity"/>.</summary>
        public void SetTeam(bool isPlayerOne)
        {
            if (playerOne == isPlayerOne) return;
            playerOne = isPlayerOne;
            ApplyFacing(IsPreSnap() ? PreSnapFacing(offenseSide) : facing, force: true);
        }

        /// <summary>Force a facing (e.g. after formation place).</summary>
        public void SetFacing(GotchiFacing next, bool force = true)
            => ApplyFacing(next, force);

        /// <summary>
        /// Pre-snap / kick lineup: offense looks downfield; defense looks at the offense.
        /// </summary>
        public static GotchiFacing PreSnapFacing(bool isOffenseSide = true)
        {
            float dir = 1f;
            if (FieldManager.Instance != null)
            {
                if (GameManager.Instance != null && GameManager.Instance.isKicking)
                    dir = FieldManager.Instance.KickDirX;
                else
                    dir = FieldManager.Instance.DriveDirX;
            }

            // Offense faces attack / kick flight; defense faces the opposite way.
            if (!isOffenseSide)
                dir = -dir;

            return dir >= 0f ? GotchiFacing.Right : GotchiFacing.Left;
        }

        static bool IsPreSnap()
        {
            var gm = GameManager.Instance;
            if (gm == null) return true;
            if (gm.waitingForNextPlay) return true;
            if (gm.isPreSnap) return true;
            // READY / SET / HUT — still lined up.
            if (SnapCadence.Instance != null && SnapCadence.Instance.IsActive) return true;
            if (gm.currentState != GameState.Playing) return true;
            return false;
        }

        Vector2 ReadPlanarDelta()
        {
            Transform root = motionRoot != null ? motionRoot : transform;
            Vector3 pos = root.position;

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null && root.parent != null)
                rb = root.parent.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                var v = rb.linearVelocity;
                if (new Vector2(v.x, v.y).sqrMagnitude > 0.01f)
                    return new Vector2(v.x, v.y);
            }

            if (!hasLastPos)
            {
                lastWorldPos = pos;
                hasLastPos = true;
                return Vector2.zero;
            }

            Vector3 d = pos - lastWorldPos;
            lastWorldPos = pos;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            return new Vector2(d.x, d.y) / dt;
        }

        void ApplyFacing(GotchiFacing next, bool force)
        {
            if (!force && next == facing && sr != null && sr.sprite != null)
            {
                sr.flipX = false;
                return;
            }

            facing = next;
            var sprite = GotchiTeamSprites.Get(playerOne, facing);
            if (sprite == null) return;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sprite = sprite;
            sr.flipX = false;
        }

        /// <summary>
        /// Lower on screen (smaller world Y) → higher sortingOrder so front gotchis
        /// draw over those toward the top sideline.
        /// </summary>
        void ApplyDepthSort()
        {
            Transform root = motionRoot != null ? motionRoot : transform;
            // y ≈ ±5.85 → order ≈ 79 … 21; keep under ball/UI (≥100).
            int order = Mathf.Clamp(Mathf.RoundToInt(50f - root.position.y * 5f), 1, 90);
            if (sr.sortingOrder != order)
                sr.sortingOrder = order;
        }
    }
}
