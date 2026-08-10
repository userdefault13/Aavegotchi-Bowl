using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Retro Bowl–style dotted throw trajectory (white dots with dark outline),
    /// plus a red landing marker at the projected catch/spot.
    /// </summary>
    public class ThrowingArc : MonoBehaviour
    {
        [SerializeField] int dotCount = 18;
        [SerializeField] float dotWorldSize = 0.35f;
        [SerializeField] float landingMarkerSize = 0.72f;
        [SerializeField] float arcHeightFactor = 0.28f;
        [SerializeField] float minArcHeight = 1.8f;
        [SerializeField] float maxArcHeight = 11f;
        [SerializeField] Color dotColor = Color.white;
        [SerializeField] Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        [SerializeField] Color landingColor = new Color(0.95f, 0.12f, 0.1f, 1f);

        Transform[] dots;
        SpriteRenderer[] renderers;
        Transform landingMarker;
        SpriteRenderer landingRenderer;
        Sprite dotSprite;
        Sprite landingSprite;
        bool visible;

        public bool IsVisible => visible;

        void Awake() => EnsureDots();

        void EnsureDots()
        {
            if (dotSprite == null)
                dotSprite = MakeDotSprite(dotColor, outlineColor);
            if (landingSprite == null)
                landingSprite = MakeLandingSprite(landingColor);

            if (dots == null || dots.Length != dotCount)
            {
                if (dots != null)
                {
                    for (int i = 0; i < dots.Length; i++)
                    {
                        if (dots[i] != null)
                            Destroy(dots[i].gameObject);
                    }
                }

                dots = new Transform[dotCount];
                renderers = new SpriteRenderer[dotCount];

                for (int i = 0; i < dotCount; i++)
                {
                    var go = new GameObject($"ArcDot_{i}");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = dotSprite;
                    sr.sortingOrder = 20;
                    float t = i / (float)(dotCount - 1);
                    float scale = Mathf.Lerp(1f, 0.65f, t) * dotWorldSize;
                    go.transform.localScale = Vector3.one * scale;
                    go.SetActive(false);
                    dots[i] = go.transform;
                    renderers[i] = sr;
                }
            }

            if (landingMarker == null)
            {
                var go = new GameObject("LandingMarker");
                go.transform.SetParent(transform, false);
                landingRenderer = go.AddComponent<SpriteRenderer>();
                landingRenderer.sprite = landingSprite;
                landingRenderer.sortingOrder = 22;
                go.transform.localScale = Vector3.one * landingMarkerSize;
                go.SetActive(false);
                landingMarker = go.transform;
            }
            else if (landingRenderer != null && landingRenderer.sprite == null)
            {
                landingRenderer.sprite = landingSprite;
            }
        }

        public void Show()
        {
            EnsureDots();
            visible = true;
            for (int i = 0; i < dots.Length; i++)
                dots[i].gameObject.SetActive(true);
            if (landingMarker != null)
                landingMarker.gameObject.SetActive(true);
        }

        public void Hide()
        {
            visible = false;
            if (dots == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null)
                    dots[i].gameObject.SetActive(false);
            }
            if (landingMarker != null)
                landingMarker.gameObject.SetActive(false);
        }

        /// <summary>Place dots along a parabola from start→end. Optional peak overrides default arc height.</summary>
        public void SetPath(Vector3 start, Vector3 end)
            => SetPath(start, end, -1f);

        public void SetPath(Vector3 start, Vector3 end, float peakOverride)
        {
            EnsureDots();
            start.z = 0f;
            end.z = 0f;

            float dist = Vector3.Distance(start, end);
            float peak = peakOverride > 0.01f
                ? peakOverride
                : FootballBehavior.PeakForDistance(dist, arcHeightFactor, minArcHeight, maxArcHeight);

            for (int i = 0; i < dots.Length; i++)
            {
                float t = i / (float)(dots.Length - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y += FootballBehavior.EvaluateLoft(t, peak);
                p.z = 0f;
                dots[i].position = p;

                if (visible && !dots[i].gameObject.activeSelf)
                    dots[i].gameObject.SetActive(true);
            }

            // Landing is the play-plane target (loft is 0 at t=1).
            if (landingMarker != null)
            {
                landingMarker.position = end;
                if (visible && !landingMarker.gameObject.activeSelf)
                    landingMarker.gameObject.SetActive(true);
            }
        }

        static Sprite MakeDotSprite(Color fill, Color outline)
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            float r = (size - 1) * 0.5f;
            float rFill = r - 2f;
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= rFill)
                        tex.SetPixel(x, y, fill);
                    else if (d <= r)
                        tex.SetPixel(x, y, outline);
                    else
                        tex.SetPixel(x, y, clear);
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        static Sprite MakeLandingSprite(Color fill)
        {
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (size - 1) * 0.5f;
            float outer = cx - 0.5f;
            float inner = outer - 3.5f;
            float core = 3.2f;
            var outline = new Color(0.05f, 0.02f, 0.02f, 0.95f);
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= core)
                        tex.SetPixel(x, y, fill);
                    else if (d <= inner)
                        tex.SetPixel(x, y, clear);
                    else if (d <= outer - 1.2f)
                        tex.SetPixel(x, y, fill);
                    else if (d <= outer)
                        tex.SetPixel(x, y, outline);
                    else
                        tex.SetPixel(x, y, clear);
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
