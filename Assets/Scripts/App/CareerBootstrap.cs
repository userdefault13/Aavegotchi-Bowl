using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroBowl.App
{
    /// <summary>CareerScene entry — ensure EventSystem + CareerNav (SceneFlow shows the screen).</summary>
    public class CareerBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // Strip edit-mode preview so it never owns CareerNav.Instance in Play Mode.
            var preview = GameObject.Find(CareerUiPreviewHost.HostName);
            if (preview != null)
            {
                var previewNav = preview.GetComponent<CareerNav>();
                if (previewNav != null && CareerNav.Instance == previewNav)
                    CareerNav.ClearInstance();
                DestroyImmediate(preview);
            }

            BootLoader.EnsureAppRoot();

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            CareerNav.EnsureExists();
        }

        void Start()
        {
            // Direct CareerScene open (no Boot/SceneFlow coroutine yet) — Save Select first.
            // SceneFlow.LoadCareerRoutine will Show() again with the intended screen afterward.
            if (CareerNav.Instance == null) return;
            CareerNav.Instance.Show(CareerScreen.SaveSelect);
        }
    }
}
