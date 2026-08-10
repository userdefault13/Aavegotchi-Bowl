using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.App;
using RetroBowl.UI.Career;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Training Facility tip overlays: passing intro modal → aim bubbles wired to QB.isAiming.
    /// </summary>
    public class TrainingTutorialOverlay : MonoBehaviour
    {
        public static TrainingTutorialOverlay Instance { get; private set; }

        enum TipStep
        {
            PassingModal,
            DragToAim,
            ReleaseToThrow,
            Done
        }

        TipStep step = TipStep.PassingModal;
        Canvas canvas;
        GameObject modalRoot;
        GameObject bubbleRoot;
        TextMeshProUGUI bubbleLabel;
        QuarterbackController qb;

        public static void EnsureForTraining()
        {
            if (Instance != null)
            {
                Instance.Begin();
                return;
            }
            var go = new GameObject("TrainingTutorialOverlay");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TrainingTutorialOverlay>();
            Instance.Begin();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Begin()
        {
            EnsureUi();
            step = TipStep.PassingModal;
            ShowPassingModal(true);
            ShowBubble(false, "");
            qb = null;
            SaveService.Instance?.SetTutorial(CareerTutorialPhase.TrainingFacility);
        }

        void Update()
        {
            if (step == TipStep.Done || step == TipStep.PassingModal) return;

            if (qb == null)
                qb = Object.FindAnyObjectByType<QuarterbackController>();

            if (qb != null && qb.isAiming)
            {
                if (step != TipStep.ReleaseToThrow)
                {
                    step = TipStep.ReleaseToThrow;
                    ShowBubble(true, "Release to throw");
                }
            }
            else if (step == TipStep.ReleaseToThrow || step == TipStep.DragToAim)
            {
                if (step != TipStep.DragToAim)
                {
                    step = TipStep.DragToAim;
                    ShowBubble(true, "Drag and hold to aim");
                }
            }
        }

        void EnsureUi()
        {
            if (canvas != null) return;

            var go = new GameObject("TrainingTutorialCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            modalRoot = CareerUiKit.Panel(canvas.transform, "PassingModal");
            var bg = modalRoot.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.22f, 0.55f, 0.94f);
            var outline = modalRoot.GetComponent<Outline>();
            if (outline == null) outline = modalRoot.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);

            var box = CareerUiKit.BorderedBox(modalRoot.transform, "TipBox",
                new Vector2(0.5f, 0.55f), new Vector2(820f, 280f),
                new Color(0.14f, 0.24f, 0.58f, 1f));
            var body = CareerUiKit.Label(box.transform, "Body", new Vector2(0.5f, 0.55f),
                new Vector2(740f, 200f), 26f);
            body.text = "First up, passing.\n\nClick and drag in the opposite direction of the running receiver to aim, then release to throw.";
            body.textWrappingMode = TextWrappingModes.Normal;

            CareerUiKit.OutlinedButton(modalRoot.transform, "Continue",
                new Vector2(0.5f, 0.18f), new Vector2(220f, 56f), "CONTINUE", OnPassingContinue,
                new Color(0.14f, 0.24f, 0.58f, 1f));

            bubbleRoot = new GameObject("AimBubble");
            bubbleRoot.transform.SetParent(canvas.transform, false);
            var brt = bubbleRoot.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.12f);
            brt.anchorMax = new Vector2(0.5f, 0.12f);
            brt.sizeDelta = new Vector2(420f, 56f);
            var bimg = bubbleRoot.AddComponent<Image>();
            bimg.color = new Color(0.12f, 0.14f, 0.22f, 0.92f);
            var bubbleOutline = bubbleRoot.AddComponent<Outline>();
            bubbleOutline.effectColor = Color.white;
            bubbleOutline.effectDistance = new Vector2(2f, -2f);
            bubbleLabel = CareerUiKit.Label(bubbleRoot.transform, "Txt", new Vector2(0.5f, 0.5f),
                new Vector2(400f, 48f), 22f);
            CareerUiKit.Stretch(bubbleLabel.rectTransform);
            bubbleRoot.SetActive(false);
        }

        void ShowPassingModal(bool on)
        {
            if (modalRoot != null)
                modalRoot.SetActive(on);
        }

        void ShowBubble(bool on, string text)
        {
            if (bubbleRoot == null) return;
            bubbleRoot.SetActive(on);
            if (bubbleLabel != null && !string.IsNullOrEmpty(text))
                bubbleLabel.text = text;
        }

        void OnPassingContinue()
        {
            ShowPassingModal(false);
            step = TipStep.DragToAim;
            ShowBubble(true, "Drag and hold to aim");
        }

        public void Teardown()
        {
            if (canvas != null)
                Destroy(canvas.gameObject);
            canvas = null;
            modalRoot = null;
            bubbleRoot = null;
            bubbleLabel = null;
            step = TipStep.Done;
            if (SaveService.Instance != null
                && SaveService.Instance.TutorialPhase == CareerTutorialPhase.TrainingFacility)
                SaveService.Instance.SetTutorial(CareerTutorialPhase.Complete);
            Destroy(gameObject);
        }
    }
}
