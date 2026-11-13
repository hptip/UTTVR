#if UNITY_EDITOR
using System.IO;
using BIM4D.AI.Generative;
using UnityEditor;
using UnityEngine;

public static class BIMAISecretsEditor
{
    [MenuItem("Tools/BIM AI/Create or Open Local API Config")]
    public static void CreateOrOpen()
    {
        string path = BIMAISecrets.ConfigPath;
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,
                "{\n  \"xkiroApiKey\": \"PASTE_XKIRO_KEY_HERE\",\n  \"groqApiKey\": \"PASTE_GROQ_KEY_HERE\"\n}\n");
        }
        EditorUtility.RevealInFinder(path);
        Debug.Log("[BIM AI] Local API config: " + path + " (không commit file này lên Git).");
    }
}
#endif
