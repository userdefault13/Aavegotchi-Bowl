using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroBowl.EditorTools
{
    /// <summary>
    /// Assigns Broken Console SDF to TMP Settings + all scene TextMeshPro labels.
    /// Menu: Aavegotchi Bowl → TMP → Assign Broken Console Font
    /// </summary>
    public static class AssignBrokenConsoleFont
    {
        const string FontAssetPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/Broken Console Regular SDF.asset";
        const string TmpSettingsPath =
            "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Aavegotchi Bowl/TMP/Assign Broken Console Font")]
        public static void Assign()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                Debug.LogError($"[TMP] Missing font asset at {FontAssetPath}. Copy from Paarcel first.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = font;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }

            int changed = 0;
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            string activePath = SceneManager.GetActiveScene().path;

            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
                {
                    if (tmp == null) continue;
                    tmp.font = font;
                    if (font.material != null)
                        tmp.fontSharedMaterial = font.material;
                    // Pixel fonts read cleaner without synthetic bold.
                    if (tmp.fontStyle == FontStyles.Bold)
                        tmp.fontStyle = FontStyles.Normal;
                    EditorUtility.SetDirty(tmp);
                    changed++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TMP] Broken Console set as default + assigned to {changed} TMP label(s) in scenes.");
        }
    }
}
