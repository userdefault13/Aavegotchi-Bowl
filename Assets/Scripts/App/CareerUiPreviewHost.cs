using UnityEngine;

namespace RetroBowl.App
{
    /// <summary>
    /// Edit-mode host that materializes CareerNav panels in CareerScene so you can
    /// view / poke UI in the Hierarchy. Disabled automatically when entering Play Mode
    /// (runtime CareerBootstrap owns the live UI).
    /// </summary>
    [ExecuteAlways]
    public class CareerUiPreviewHost : MonoBehaviour
    {
        public const string HostName = "CareerUiPreview";

        [Tooltip("Which career panel to leave active in the Hierarchy.")]
        public CareerScreen previewScreen = CareerScreen.NewCareer;

        [Tooltip("If true, every panel stays active (stacked) so you can multi-select in Hierarchy.")]
        public bool showAllPanels;

        CareerNav nav;

        void Awake()
        {
            if (!Application.isPlaying) return;

            // Play Mode: runtime CareerBootstrap owns live UI — drop this bake.
            var n = GetComponent<CareerNav>();
            if (n != null && CareerNav.Instance == n)
            {
                // OnDestroy will clear Instance when we disable/destroy.
            }
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                gameObject.SetActive(false);
                return;
            }

            EnsureNav();
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (!isActiveAndEnabled) return;
            // Defer so DestroyImmediate during validate is safe.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                ApplyPreview();
            };
#endif
        }

        public CareerNav EnsureNav()
        {
            if (nav == null)
                nav = GetComponent<CareerNav>();
            if (nav == null)
                nav = gameObject.AddComponent<CareerNav>();
            return nav;
        }

        public void Rebuild()
        {
            if (Application.isPlaying) return;
            var n = EnsureNav();
            n.RebuildUiFromCode();
            ParentCanvasUnderHost();
            ApplyPreview();
        }

        public void ApplyPreview()
        {
            if (Application.isPlaying) return;
            var n = EnsureNav();
            if (!n.HasBuiltUi)
                n.RebuildUiFromCode();

            ParentCanvasUnderHost();

            if (showAllPanels && n.UiCanvas != null)
            {
                n.UiCanvas.gameObject.SetActive(true);
                for (int i = 0; i < n.UiCanvas.transform.childCount; i++)
                {
                    var child = n.UiCanvas.transform.GetChild(i);
                    if (child != null)
                        child.gameObject.SetActive(true);
                }
                return;
            }

            n.PreviewScreen(previewScreen);
        }

        void ParentCanvasUnderHost()
        {
            var n = EnsureNav();
            if (n.UiCanvas == null) return;
            var canvasTf = n.UiCanvas.transform;
            if (canvasTf.parent != transform)
                canvasTf.SetParent(transform, false);
        }
    }
}
