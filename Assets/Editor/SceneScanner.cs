
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class ClassroomSceneScanner
{
    [MenuItem("Tools/Classroom/Export Scene Report")]
    public static void Export()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("No valid active Scene.");
            return;
        }

        var sb = new StringBuilder();
        var types = new Dictionary<string, int>();

        int objects = 0;
        int renderers = 0;
        int colliders = 0;
        int missingScripts = 0;

        sb.AppendLine("SCENE: " + scene.name);
        sb.AppendLine("PATH: " + scene.path);
        sb.AppendLine("=== HIERARCHY ===");

        foreach (var root in scene.GetRootGameObjects())
            Scan(root.transform, 0);

        sb.AppendLine();
        sb.AppendLine("=== SUMMARY ===");
        sb.AppendLine("GameObjects: " + objects);
        sb.AppendLine("Renderers: " + renderers);
        sb.AppendLine("Colliders: " + colliders);
        sb.AppendLine("Missing Scripts: " + missingScripts);

        sb.AppendLine();
        sb.AppendLine("=== COMPONENT COUNTS ===");

        foreach (var pair in types)
            sb.AppendLine(pair.Key + ": " + pair.Value);

        string directory = @"F:\UnityTemp";
        Directory.CreateDirectory(directory);

        string path = Path.Combine(
            directory, "Classroom_Scene_Report.txt");

        File.WriteAllText(path, sb.ToString());

        Debug.Log("Scene report exported: " + path);
        EditorUtility.RevealInFinder(path);

        void Scan(Transform t, int depth)
        {
            GameObject go = t.gameObject;
            objects++;

            string indent = new string(' ', depth * 2);

            sb.AppendLine(
                indent + "- " + go.name +
                " | Active=" + go.activeSelf +
                " | Layer=" + LayerMask.LayerToName(go.layer) +
                " | Position=" + t.position
            );

            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null)
                {
                    missingScripts++;
                    sb.AppendLine(indent + "  ! MISSING SCRIPT");
                    continue;
                }

                string type = component.GetType().Name;

                if (!types.ContainsKey(type))
                    types[type] = 0;

                types[type]++;

                sb.AppendLine(indent + "  [" + type + "]");

                if (component is Renderer renderer)
                {
                    renderers++;
                    sb.AppendLine(
                        indent + "    Materials=" +
                        renderer.sharedMaterials.Length);
                }

                if (component is Collider)
                    colliders++;

                if (component is MonoBehaviour script)
                {
                    var monoScript = MonoScript.FromMonoBehaviour(script);

                    if (monoScript != null)
                        sb.AppendLine(
                            indent + "    Script=" +
                            AssetDatabase.GetAssetPath(monoScript));
                }
            }

            foreach (Transform child in t)
                Scan(child, depth + 1);
        }
    }
}
