using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class SceneTeleporter : InteractableObject
{
    [Header("Cấu hình chuyển cảnh")]
    public string destinationScene = "YenTuHaLongXanh";

    public override void OnInteract()
    {
        ChuyenScene();
    }

    private void ChuyenScene()
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning)
        {
            // Trong Fusion Shared Mode, gọi trực tiếp LoadScene qua SceneRef
            int sceneIndex = ResolveBuildIndex(destinationScene);
            if (sceneIndex >= 0)
            {
                runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
            else
            {
                Debug.LogError($"[SceneTeleporter] Scene '{destinationScene}' chưa được tích chọn trong Build Settings/Build Profiles!");
            }
        }
        else
        {
            // Chạy đơn máy offline không qua Fusion
            SceneManager.LoadScene(destinationScene);
        }
    }

    private static int ResolveBuildIndex(string sceneNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(sceneNameOrPath)) return -1;

        int exactPathIndex = SceneUtility.GetBuildIndexByScenePath(sceneNameOrPath);
        if (exactPathIndex >= 0) return exactPathIndex;

        string requestedName = System.IO.Path.GetFileNameWithoutExtension(sceneNameOrPath);
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(buildSceneName, requestedName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
