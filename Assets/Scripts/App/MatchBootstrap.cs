using UnityEngine;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI;

namespace RetroBowl.App
{
    /// <summary>
    /// MatchScene entry — attach match-only systems to AppRoot GameManager
    /// (Boot already owns the DDOL instance).
    /// </summary>
    public class MatchBootstrap : MonoBehaviour
    {
        void Awake()
        {
            BootLoader.EnsureAppRoot();
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Destroy duplicate scene managers that would fight DDOL.
            CullDuplicateManagers();

            EnsureMatchSystems(gm.gameObject);

            // Scene-local MenuManager becomes match-only controller.
            var menus = Object.FindObjectsByType<MenuManager>();
            foreach (var m in menus)
            {
                if (m.GetComponent<MatchMenuController>() == null)
                    m.gameObject.AddComponent<MatchMenuController>();
            }
        }

        public static void CullDuplicateManagers()
        {
            foreach (var gm in Object.FindObjectsByType<GameManager>())
            {
                if (gm != GameManager.Instance)
                    Destroy(gm.gameObject);
            }

            // MatchScene ships leftover Team/Season/Score managers from the old monolith —
            // destroy those GameObjects so they cannot Start() and wipe career franchise data.
            CullOtherThan(TeamManager.Instance);
            CullOtherThan(SeasonManager.Instance);
            CullOtherThan(ScoreManager.Instance);
        }

        static void CullOtherThan<T>(T keep) where T : Component
        {
            if (keep == null) return;
            foreach (var obj in Object.FindObjectsByType<T>())
            {
                if (obj == null || obj == keep) continue;
                // Never nuke AppRoot — only strip the stray component if it landed there.
                if (GameManager.Instance != null && obj.gameObject == GameManager.Instance.gameObject)
                {
                    Destroy(obj);
                    continue;
                }
                Destroy(obj.gameObject);
            }
        }

        public static void EnsureMatchSystems(GameObject host)
        {
            if (host == null) return;
            if (host.GetComponent<RetroLookApplier>() == null)
                host.AddComponent<RetroLookApplier>();
            if (host.GetComponent<SnapCadence>() == null)
                host.AddComponent<SnapCadence>();
            if (host.GetComponent<PlayBanner>() == null)
                host.AddComponent<PlayBanner>();
            if (host.GetComponent<PlayRoutePreview>() == null)
                host.AddComponent<PlayRoutePreview>();
            if (host.GetComponent<TackleBattle>() == null)
                host.AddComponent<TackleBattle>();
            if (host.GetComponent<ContactBattle>() == null)
                host.AddComponent<ContactBattle>();
            if (host.GetComponent<TecmoContact>() == null)
                host.AddComponent<TecmoContact>();
            if (host.GetComponent<LineBattle>() == null)
                host.AddComponent<LineBattle>();
            if (host.GetComponent<StaminaSprintHud>() == null)
                host.AddComponent<StaminaSprintHud>();
            if (host.GetComponent<PassCollisionGate>() == null)
                host.AddComponent<PassCollisionGate>();
            if (host.GetComponent<PlayerDefenseController>() == null)
                host.AddComponent<PlayerDefenseController>();
            if (host.GetComponent<DefensePlayDirector>() == null)
                host.AddComponent<DefensePlayDirector>();
            if (host.GetComponent<KickingController>() == null)
                host.AddComponent<KickingController>();
            if (host.GetComponent<PostScoreFlow>() == null)
                host.AddComponent<PostScoreFlow>();
            if (host.GetComponent<WeatherSystem>() == null)
                host.AddComponent<WeatherSystem>();
        }
    }
}
