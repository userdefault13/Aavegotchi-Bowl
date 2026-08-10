#if UNITY_EDITOR
using System;
using System.IO;
using RetroBowl.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bakes CareerNav's runtime UI into CareerScene so panels are visible in the Hierarchy.
/// Covers every CareerScreen (Save Select → Home → Front Office → Roster → …).
/// Menu: Aavegotchi Bowl → Career UI → …
/// </summary>
public static class CareerUiPreviewBaker
{
    const string CareerScenePath = "Assets/Scenes/CareerScene.unity";
    const string PrefabFolder = "Assets/Prefabs/Career/UI";

    [MenuItem("Aavegotchi Bowl/Career UI/Bake Preview Into CareerScene", priority = 20)]
    public static void BakeIntoCareerScene()
    {
        if (!File.Exists(CareerScenePath))
        {
            EditorUtility.DisplayDialog(
                "CareerScene missing",
                "Assets/Scenes/CareerScene.unity not found.\nRun Aavegotchi Bowl → Build Scene Architecture first.",
                "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(CareerScenePath, OpenSceneMode.Single);
        EnsureEventSystem();

        var hostGo = GameObject.Find(CareerUiPreviewHost.HostName);
        if (hostGo != null)
            Undo.DestroyObjectImmediate(hostGo);

        hostGo = new GameObject(CareerUiPreviewHost.HostName);
        Undo.RegisterCreatedObjectUndo(hostGo, "Career UI Preview");
        var host = hostGo.AddComponent<CareerUiPreviewHost>();
        host.previewScreen = CareerScreen.Home;

        host.Rebuild();
        Selection.activeGameObject = hostGo;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        int panelCount = CountPanels(host);
        Debug.Log(
            $"Career UI baked ({panelCount} panels) under CareerUiPreview.\n" +
            "Use Career UI → Show Screen → … for Front Office, Roster, Training, etc.\n" +
            "Rebuild after CareerNav / CareerUiKit code changes.");
        EditorUtility.DisplayDialog(
            "Career UI Preview",
            $"All career rooms are under CareerUiPreview → CareerCanvas ({panelCount} panels).\n\n" +
            "• Aavegotchi Bowl → Career UI → Show Screen → Front Office / Roster / …\n" +
            "• Or set Preview Screen on the host Inspector\n" +
            "• Rebuild Preview after code edits\n" +
            "• Save Panels As Prefabs for snapshots\n\n" +
            "Hierarchy edits are preview-only (runtime still builds from code).",
            "OK");
    }

    [MenuItem("Aavegotchi Bowl/Career UI/Rebuild Preview", priority = 21)]
    public static void RebuildPreview()
    {
        var host = FindHost();
        if (host == null)
        {
            if (EditorUtility.DisplayDialog(
                    "No preview host",
                    "CareerUiPreview not found in the open scene. Bake it into CareerScene now?",
                    "Bake", "Cancel"))
                BakeIntoCareerScene();
            return;
        }

        host.Rebuild();
        EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        Selection.activeGameObject = host.gameObject;
        Debug.Log($"Career UI preview rebuilt — showing {host.previewScreen} ({CountPanels(host)} panels).");
    }

    [MenuItem("Aavegotchi Bowl/Career UI/Save Panels As Prefabs", priority = 22)]
    public static void SavePanelsAsPrefabs()
    {
        var host = FindHost();
        if (host == null)
        {
            EditorUtility.DisplayDialog("No preview", "Bake the Career UI preview first.", "OK");
            return;
        }

        host.Rebuild();
        var nav = host.EnsureNav();
        if (nav.UiCanvas == null)
        {
            EditorUtility.DisplayDialog("Empty", "Career canvas did not build.", "OK");
            return;
        }

        Directory.CreateDirectory(PrefabFolder);
        int saved = 0;
        foreach (CareerScreen screen in Enum.GetValues(typeof(CareerScreen)))
        {
            var panel = nav.UiCanvas.transform.Find(screen.ToString());
            if (panel == null) continue;

            string path = $"{PrefabFolder}/{screen}.prefab";
            bool wasActive = panel.gameObject.activeSelf;
            panel.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject, path);
            panel.gameObject.SetActive(wasActive);
            saved++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Prefabs saved",
            $"Wrote {saved} panel prefabs to:\n{PrefabFolder}\n\n" +
            "These are reference snapshots. Runtime still builds from CareerNav code.",
            "OK");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PrefabFolder));
    }

    // ── Show Screen — every CareerScreen ──
    // Unity MenuItem paths must be compile-time constants, so list each screen.

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/01 Save Select", false, 40)]
    static void M01() => Show(CareerScreen.SaveSelect);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/02 Welcome", false, 41)]
    static void M02() => Show(CareerScreen.Welcome);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/03 New Career", false, 42)]
    static void M03() => Show(CareerScreen.NewCareer);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/04 News", false, 43)]
    static void M04() => Show(CareerScreen.News);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/05 Home", false, 44)]
    static void M05() => Show(CareerScreen.Home);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/06 Choose Team", false, 45)]
    static void M06() => Show(CareerScreen.ChooseTeam);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/07 Roster", false, 46)]
    static void M07() => Show(CareerScreen.Roster);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/08 Player Profile", false, 47)]
    static void M08() => Show(CareerScreen.PlayerProfile);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/09 Staff Profile", false, 48)]
    static void M09() => Show(CareerScreen.StaffProfile);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/10 Draft", false, 49)]
    static void M10() => Show(CareerScreen.Draft);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/11 Free Agents", false, 50)]
    static void M11() => Show(CareerScreen.FreeAgents);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/12 Front Office", false, 51)]
    static void M12() => Show(CareerScreen.FrontOffice);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/13 Playbook", false, 52)]
    static void M13() => Show(CareerScreen.Playbook);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/14 Training", false, 53)]
    static void M14() => Show(CareerScreen.Training);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/15 XP", false, 54)]
    static void M15() => Show(CareerScreen.Xp);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/16 League", false, 55)]
    static void M16() => Show(CareerScreen.League);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/17 Playoffs", false, 56)]
    static void M17() => Show(CareerScreen.Playoffs);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/18 Pre Match", false, 57)]
    static void M18() => Show(CareerScreen.PreMatch);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/19 Post Match", false, 58)]
    static void M19() => Show(CareerScreen.PostMatch);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/20 Stats", false, 59)]
    static void M20() => Show(CareerScreen.Stats);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/21 Hall Of Fame", false, 60)]
    static void M21() => Show(CareerScreen.HallOfFame);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/22 Options", false, 61)]
    static void M22() => Show(CareerScreen.Options);

    [MenuItem("Aavegotchi Bowl/Career UI/Show Screen/23 Details", false, 62)]
    static void M23() => Show(CareerScreen.Details);

    static void Show(CareerScreen screen)
    {
        var host = FindHost();
        if (host == null)
        {
            BakeIntoCareerScene();
            host = FindHost();
            if (host == null) return;
        }

        host.previewScreen = screen;
        host.showAllPanels = false;
        host.ApplyPreview();
        EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        Selection.activeGameObject = host.gameObject;

        // Ping the live panel GameObject in Hierarchy.
        var nav = host.EnsureNav();
        if (nav.UiCanvas != null)
        {
            var panel = nav.UiCanvas.transform.Find(screen.ToString());
            if (panel != null)
                EditorGUIUtility.PingObject(panel.gameObject);
        }

        Debug.Log($"Career UI preview → {screen}");
    }

    static int CountPanels(CareerUiPreviewHost host)
    {
        if (host == null) return 0;
        var nav = host.EnsureNav();
        if (nav.UiCanvas == null) return 0;
        int n = 0;
        foreach (CareerScreen screen in Enum.GetValues(typeof(CareerScreen)))
        {
            if (nav.UiCanvas.transform.Find(screen.ToString()) != null)
                n++;
        }
        return n;
    }

    static CareerUiPreviewHost FindHost()
    {
        return UnityEngine.Object.FindAnyObjectByType<CareerUiPreviewHost>(FindObjectsInactive.Include);
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(es, "EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}

[CustomEditor(typeof(CareerUiPreviewHost))]
public class CareerUiPreviewHostEditor : Editor
{
    static readonly (string label, CareerScreen[] screens)[] Groups =
    {
        ("Onboarding", new[]
        {
            CareerScreen.SaveSelect, CareerScreen.Welcome, CareerScreen.NewCareer, CareerScreen.News
        }),
        ("Hub", new[]
        {
            CareerScreen.Home, CareerScreen.FrontOffice, CareerScreen.Roster, CareerScreen.Playbook,
            CareerScreen.Training, CareerScreen.League, CareerScreen.HallOfFame, CareerScreen.Options
        }),
        ("Team", new[]
        {
            CareerScreen.ChooseTeam, CareerScreen.PlayerProfile, CareerScreen.StaffProfile,
            CareerScreen.Draft, CareerScreen.FreeAgents, CareerScreen.Stats, CareerScreen.Details
        }),
        ("Match", new[]
        {
            CareerScreen.PreMatch, CareerScreen.PostMatch, CareerScreen.Playoffs, CareerScreen.Xp
        }),
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var host = (CareerUiPreviewHost)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Every career room (Front Office, Roster, Training, …) is under CareerCanvas.\n" +
            "Rebuild after CareerNav / CareerUiKit changes. Play Mode removes this host.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rebuild From Code", GUILayout.Height(28)))
        {
            host.Rebuild();
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        }
        if (GUILayout.Button("Apply Preview", GUILayout.Height(28)))
        {
            host.ApplyPreview();
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Quick Show", EditorStyles.boldLabel);

        foreach (var group in Groups)
        {
            EditorGUILayout.LabelField(group.label, EditorStyles.miniBoldLabel);
            int cols = 4;
            int i = 0;
            while (i < group.screens.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i < group.screens.Length; c++, i++)
                {
                    var screen = group.screens[i];
                    bool active = host.previewScreen == screen && !host.showAllPanels;
                    var prev = GUI.backgroundColor;
                    if (active)
                        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                    if (GUILayout.Button(NiceName(screen), GUILayout.Height(24)))
                    {
                        Undo.RecordObject(host, "Preview Screen");
                        host.previewScreen = screen;
                        host.showAllPanels = false;
                        host.ApplyPreview();
                        EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
                        EditorUtility.SetDirty(host);

                        var nav = host.EnsureNav();
                        if (nav.UiCanvas != null)
                        {
                            var panel = nav.UiCanvas.transform.Find(screen.ToString());
                            if (panel != null)
                            {
                                Selection.activeGameObject = panel.gameObject;
                                EditorGUIUtility.PingObject(panel.gameObject);
                            }
                        }
                    }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    static string NiceName(CareerScreen screen)
    {
        switch (screen)
        {
            case CareerScreen.SaveSelect: return "Saves";
            case CareerScreen.NewCareer: return "New Career";
            case CareerScreen.ChooseTeam: return "Team";
            case CareerScreen.PlayerProfile: return "Player";
            case CareerScreen.StaffProfile: return "Staff";
            case CareerScreen.FreeAgents: return "FA";
            case CareerScreen.FrontOffice: return "F.O.";
            case CareerScreen.HallOfFame: return "HOF";
            case CareerScreen.PreMatch: return "Week";
            case CareerScreen.PostMatch: return "Result";
            default: return screen.ToString();
        }
    }
}
#endif
