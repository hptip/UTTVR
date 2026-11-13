#if UNITY_EDITOR
using BIM4D.AI.Generative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cài Voice Input vào chatbot ĐÃ CÓ trong scene mà không Build lại prefab, không tạo lại robot.
/// Idempotent: chạy nhiều lần không tạo component/listener/UI trùng.
/// </summary>
public static class VoiceInputInstaller
{
    [MenuItem("Tools/AI Chatbot/Install Voice Input On Selected Chatbot")]
    public static void InstallOnSelected()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("BIM AI Voice", "Hãy thoát Play Mode trước khi cài Voice Input.", "OK");
            return;
        }

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("BIM AI Voice", "Hãy chọn GameObject chatbot (có AIChatbotUIController) trong Hierarchy.", "OK");
            return;
        }
        if (EditorUtility.IsPersistent(selected))
        {
            EditorUtility.DisplayDialog("BIM AI Voice",
                "Bạn đang chọn một Asset trong Project. Hãy chọn instance của chatbot trong Hierarchy, " +
                "hoặc chạy Tools > AI Chatbot > Build Prefabs để tạo prefab đã có sẵn Voice UI.", "OK");
            return;
        }

        var ui = selected.GetComponent<AIChatbotUIController>();
        if (ui == null) ui = selected.GetComponentInChildren<AIChatbotUIController>(true);
        if (ui == null) ui = selected.GetComponentInParent<AIChatbotUIController>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("BIM AI Voice", "Không tìm thấy AIChatbotUIController trên object đã chọn (hoặc cha/con của nó).", "OK");
            return;
        }

        XkiroBrain brain = Object.FindFirstObjectByType<XkiroBrain>(FindObjectsInactive.Include);
        if (brain == null)
        {
            EditorUtility.DisplayDialog("BIM AI Voice", "Không tìm thấy XkiroBrain trong Scene.", "OK");
            return;
        }
        if (ui.voiceButton == null)
        {
            EditorUtility.DisplayDialog("BIM AI Voice",
                "AIChatbotUIController chưa gán Voice Button. Hãy dùng prefab có VoiceButton hoặc gán Button trong Inspector.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Install BIM AI Voice Input");
        int group = Undo.GetCurrentGroup();

        if (brain.GetComponent<GroqSpeechToText>() == null)
            Undo.AddComponent<GroqSpeechToText>(brain.gameObject);

        bool changed = AIChatbotVoiceUIFactory.EnsureVoiceUI(ui, true);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = ui.gameObject;

        Debug.Log("[BIM AI Voice] " + (changed ? "Đã cài/cập nhật Voice Input." : "Voice Input đã đầy đủ, không có gì thay đổi.") +
                  " Kiểm tra GroqSpeechToText (Language, Silence Peak Threshold) và URL + App Key Worker trong BIMTTSSettings. Không Build lại prefab.", ui);
        EditorUtility.DisplayDialog("BIM AI Voice",
            (changed ? "Đã cài Voice Input vào chatbot." : "Voice Input đã đầy đủ, không có gì thay đổi.") +
            "\n\nLưu Scene, rồi nhập URL + App Key Worker bằng Tools > BIM AI > Create or Select TTS Settings.", "OK");
    }
}
#endif
