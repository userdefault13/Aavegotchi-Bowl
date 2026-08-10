using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Cycles the spiral flipbook on the football Visual while a pass is in the air,
    /// and restores the static ball sprite once it is held, bouncing or parked.
    /// Kicks keep their end-over-end transform tumble instead.
    /// </summary>
    [DefaultExecutionOrder(260)]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BallSpinAnimator : MonoBehaviour
    {
        public const string FramesPath = "Retro/BallSpin/BallSpin_";

        /// <summary>
        /// The static ball's body is 20px wide @ 16 PPU (1.25 world units). Spin frames
        /// draw the body 6px wide, so this PPU keeps the ball the same length in flight.
        /// </summary>
        const float SpinPixelsPerUnit = 6f / 1.25f;

        /// <summary>
        /// Ball body sits in rows 1–3 of the 6px canvas, so a plain center pivot would
        /// float it above the hand. Pivot on the body instead.
        /// </summary>
        static readonly Vector2 SpinPivot = new Vector2(0.5f, 4f / 6f);

        [Tooltip("Spiral playback rate. The source gif is 10 fps; faster reads better in flight.")]
        public float framesPerSecond = 18f;

        static Sprite[] frames;

        SpriteRenderer sr;
        FootballBehavior ball;
        Sprite restSprite;
        float elapsed;
        int index;

        /// <summary>Add / refresh the animator on a football Visual object.</summary>
        public static BallSpinAnimator Attach(GameObject visual)
        {
            if (visual == null) return null;
            var anim = visual.GetComponent<BallSpinAnimator>();
            if (anim == null)
                anim = visual.AddComponent<BallSpinAnimator>();
            anim.CaptureRestSprite();
            return anim;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            ball = GetComponentInParent<FootballBehavior>();
            CaptureRestSprite();
        }

        void CaptureRestSprite()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (sr.sprite != null && !IsSpinFrame(sr.sprite))
                restSprite = sr.sprite;
        }

        void Update()
        {
            if (sr == null) return;
            if (ball == null)
            {
                ball = GetComponentInParent<FootballBehavior>();
                if (ball == null) return;
            }

            if (!IsSpiraling())
            {
                CaptureRestSprite();
                if (restSprite != null && IsSpinFrame(sr.sprite))
                    sr.sprite = restSprite;
                elapsed = 0f;
                index = 0;
                return;
            }

            var loop = LoadFrames();
            if (loop == null || loop.Length == 0) return;

            elapsed += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, framesPerSecond);
            while (elapsed >= step)
            {
                elapsed -= step;
                index = (index + 1) % loop.Length;
            }

            sr.sprite = loop[index];
        }

        /// <summary>Live pass (or tipped ball) — kicks tumble, held / loose balls sit still.</summary>
        bool IsSpiraling()
            => ball.isInAir
               && !ball.IsSpecialTeamsKick
               && !ball.isBouncing
               && !ball.isCaught;

        static bool IsSpinFrame(Sprite sprite)
        {
            if (sprite == null || frames == null) return false;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == sprite) return true;
            }
            return false;
        }

        static Sprite[] LoadFrames()
        {
            if (frames != null) return frames;

            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 32; i++)
            {
                var frame = LoadFrame(FramesPath + i);
                if (frame == null) break;
                list.Add(frame);
            }

            frames = list.ToArray();
            if (frames.Length == 0)
                Debug.LogWarning($"BallSpinAnimator: no frames found at Resources/{FramesPath}*");
            return frames;
        }

        /// <summary>
        /// Textures ship as plain (non-Sprite) importer assets, so build the sprite
        /// here with the PPU that matches the static ball's world size.
        /// </summary>
        static Sprite LoadFrame(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                SpinPivot,
                SpinPixelsPerUnit);
        }
    }
}
