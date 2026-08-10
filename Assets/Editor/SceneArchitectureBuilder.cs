#if UNITY_EDITOR
using System.IO;
using RetroBowl.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Creates BootScene + CareerScene and wires Editor Build Settings (Boot / Career / Match).
/// Menu: Aavegotchi Bowl → Build Scene Architecture
/// </summary>
public static class SceneArchitectureBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string BootPath = ScenesDir + "/BootScene.unity";
    const string CareerPath = ScenesDir + "/CareerScene.unity";
    const string MatchPath = ScenesDir + "/MatchScene.unity";

    [MenuItem("Aavegotchi Bowl/Build Scene Architecture")]
    public static void Build()
    {
        Directory.CreateDirectory(ScenesDir);

        if (!File.Exists(MatchPath))
        {
            var legacy = ScenesDir + "/GameScene.unity";
            if (File.Exists(legacy))
            {
                AssetDatabase.RenameAsset(legacy, "MatchScene.unity");
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("MatchScene.unity missing — run Setup Project first or rename GameScene.");
            }
        }

        BuildBootScene();
        BuildCareerScene();
        EnsureMatchBootstrap();
        WriteBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Scene architecture ready: BootScene → CareerScene → MatchScene");
    }

    static void EnsureMatchBootstrap()
    {
        if (!File.Exists(MatchPath)) return;
        var scene = EditorSceneManager.OpenScene(MatchPath, OpenSceneMode.Single);
        bool found = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<MatchBootstrap>() != null
                || root.GetComponentInChildren<MatchBootstrap>(true) != null)
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            var host = new GameObject("MatchBootstrap");
            host.AddComponent<MatchBootstrap>();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        EditorSceneManager.SaveScene(scene);
    }

    static void BuildBootScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera");
        var camera = cam.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
        camera.orthographic = true;
        cam.tag = "MainCamera";
        cam.AddComponent<AudioListener>();

        var boot = new GameObject("BootLoader");
        boot.AddComponent<BootLoader>();

        var label = new GameObject("SplashText");
        // TextMesh needs runtime font — skip; BootLoader loads Career quickly.

        EditorSceneManager.SaveScene(scene, BootPath);
    }

    static void BuildCareerScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera");
        var camera = cam.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
        camera.orthographic = true;
        cam.tag = "MainCamera";
        cam.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        var boot = new GameObject("CareerBootstrap");
        boot.AddComponent<CareerBootstrap>();

        EditorSceneManager.SaveScene(scene, CareerPath);
    }

    static void WriteBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene(BootPath, true),
            new EditorBuildSettingsScene(CareerPath, true),
            new EditorBuildSettingsScene(MatchPath, File.Exists(MatchPath)),
        };
        EditorBuildSettings.scenes = scenes;
    }
}
#endif
