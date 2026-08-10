using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Managers;

namespace RetroBowl.App
{
    /// <summary>BootScene entry — spawn DDOL app root, then open Save Select.</summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] float splashSeconds = 0.35f;

        void Start()
        {
            EnsureAppRoot();
            Invoke(nameof(OpenCareer), Mathf.Max(0.05f, splashSeconds));
        }

        void OpenCareer()
        {
            if (TeamManager.Instance != null)
                TeamManager.Instance.EnsureDefaultTeamsIfNeeded();
            // First screen is always Save Select — slot load happens on CONTINUE / NEW GAME.
            SceneFlow.Instance?.GoToCareer(CareerScreen.SaveSelect);
        }

        public static void EnsureAppRoot()
        {
            var root = GameObject.Find("AppRoot");
            if (root == null && GameManager.Instance != null)
                root = GameManager.Instance.gameObject;
            if (root == null)
                root = new GameObject("AppRoot");

            DontDestroyOnLoad(root);

            InputBootstrap.EnsureConfigured();

            if (root.GetComponent<GameManager>() == null)
                root.AddComponent<GameManager>();
            if (root.GetComponent<AudioManager>() == null)
                root.AddComponent<AudioManager>();
            if (root.GetComponent<SeasonManager>() == null)
                root.AddComponent<SeasonManager>();
            if (root.GetComponent<TeamManager>() == null)
                root.AddComponent<TeamManager>();
            if (root.GetComponent<ScoreManager>() == null)
                root.AddComponent<ScoreManager>();
            if (root.GetComponent<SceneFlow>() == null)
                root.AddComponent<SceneFlow>();
            if (root.GetComponent<SaveService>() == null)
                root.AddComponent<SaveService>();
            if (root.GetComponent<CareerNav>() == null)
                root.AddComponent<CareerNav>();
        }
    }
}
