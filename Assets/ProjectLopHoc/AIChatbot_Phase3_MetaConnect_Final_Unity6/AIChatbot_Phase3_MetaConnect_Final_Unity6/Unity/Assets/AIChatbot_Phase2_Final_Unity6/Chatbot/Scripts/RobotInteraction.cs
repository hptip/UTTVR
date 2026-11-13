using UnityEngine;

/// <summary>Gắn vào Robot (cần Collider). Nhìn vào robot trong tầm và nhấn E / click để mở chatbot chung.</summary>
public class RobotInteraction : MonoBehaviour
{
    public AIChatbotUIController chatbot;   // để trống = tự tìm
    public float maxDistance = 4f;

    void Update()
    {
        if (chatbot == null)
        {
#if UNITY_2022_2_OR_NEWER
            chatbot = FindFirstObjectByType<AIChatbotUIController>();
#else
            chatbot = FindObjectOfType<AIChatbotUIController>();
#endif
            return;
        }
        if (chatbot.IsOpen) return;

        var cam = Camera.main;
        if (cam == null) return;
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out var hit, maxDistance) &&
            (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) &&
            ChatbotInputController.InteractPressed())
        {
            chatbot.Open();
        }
    }
}
