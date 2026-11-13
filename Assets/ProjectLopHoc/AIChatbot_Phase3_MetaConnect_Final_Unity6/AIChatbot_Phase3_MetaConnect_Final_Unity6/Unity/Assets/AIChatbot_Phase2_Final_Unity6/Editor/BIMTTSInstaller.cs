#if UNITY_EDITOR
using BIM4D.AI.Speech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cài Voice Output (TTS MetaConnect) vào chatbot ĐÃ CÓ trong scene mà không Build lại toàn bộ prefab/robot.
/// Idempotent: chạy nhiều lần không tạo trùng. Tự tạo Resources/BIMTTSSettings.asset nếu chưa có.
/// </summary>
public static class BIMTTSInstaller
{
    const string RootFolder = "Assets/AIChatbot_Phase2_Final_Unity6";
    // Cài lại nhiều lần an toàn: Factory kiểm tra component/UI đã có trước khi tạo.

    [MenuItem("Tools/AI Chatbot/Install Voice Output")]
    public static void InstallOnSelected()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("BIM Voice Output", "Hãy thoát Play Mode trước khi cài Voice Output.", "OK");
            return;
        }

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("BIM Voice Output", "Hãy chọn GameObject chatbot (có AIChatbotUIController) trong Hierarchy.", "OK");
            return;
        }
        if (EditorUtility.IsPersistent(selected))
        {
            EditorUtility.DisplayDialog("BIM Voice Output",
                "Bạn đang chọn một Asset trong Project. Hãy chọn instance của chatbot trong Hierarchy, " +
                "hoặc chạy Tools > AI Chatbot > Build Prefabs để tạo prefab đã có sẵn Voice Output.", "OK");
            return;
        }

        var ui = selected.GetComponent<AIChatbotUIController>();
        if (ui == null) ui = selected.GetComponentInChildren<AIChatbotUIController>(true);
        if (ui == null) ui = selected.GetComponentInParent<AIChatbotUIController>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("BIM Voice Output", "Không tìm thấy AIChatbotUIController trên object đã chọn (hoặc cha/con của nó).", "OK");
            return;
        }
        if (ui.aiMessagePrefab == null)
        {
            EditorUtility.DisplayDialog("BIM Voice Output", "AIChatbotUIController chưa gán AI Message Prefab. Không thể thêm nút Đọc.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Install BIM AI Voice Output");
        int group = Undo.GetCurrentGroup();

        bool changed = BIMVoiceOutputFactory.EnsureVoiceOutput(ui, true);
        var settings = EnsureSettingsAsset();

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = ui.gameObject;

        string msg = (changed ? "Đã cài Voice Output vào chatbot." : "Voice Output đã đầy đủ, không có gì thay đổi.") +
                     "\n\nLưu Scene. Rồi mở BIMTTSSettings (đã chọn sẵn) và nhập Proxy URL + App Key của backend." +
                     "\nKHÔNG nhập khóa MetaConnect vào Unity. Xem Documentation/Voice_Output_Guide.md.";
        Debug.Log("[BIM Voice Output] " + msg.Replace("\n", " "), ui);
        Selection.activeObject = settings;
        EditorUtility.DisplayDialog("BIM Voice Output", msg, "OK");
    }

    [MenuItem("Tools/BIM AI/Create or Select TTS Settings")]
    public static void CreateOrSelectSettings()
    {
        var s = EnsureSettingsAsset();
        Selection.activeObject = s;
        EditorGUIUtility.PingObject(s);
    }

    static BIMTTSSettings EnsureSettingsAsset()
    {
        var existing = Resources.Load<BIMTTSSettings>(BIMTTSSettings.ResourceName);
        if (existing != null) return existing;

        string parent = AssetDatabase.IsValidFolder(RootFolder) ? RootFolder : "Assets";
        string folder = parent + "/Resources";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, "Resources");

        var asset = ScriptableObject.CreateInstance<BIMTTSSettings>();
        AssetDatabase.CreateAsset(asset, folder + "/" + BIMTTSSettings.ResourceName + ".asset");
        AssetDatabase.SaveAssets();
        Debug.Log("[BIM TTS] Đã tạo " + folder + "/BIMTTSSettings.asset. Nhập Proxy URL và App Key của backend (KHÔNG nhập khóa MetaConnect).");
        return asset;
    }
}
#endif
