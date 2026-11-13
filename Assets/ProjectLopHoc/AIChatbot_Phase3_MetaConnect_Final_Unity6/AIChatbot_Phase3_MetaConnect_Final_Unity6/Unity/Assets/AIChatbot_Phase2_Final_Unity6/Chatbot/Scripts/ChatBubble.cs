using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>Một dòng tin nhắn (row + bubble). Tự co giãn theo độ dài chữ, tối đa maxWidth.</summary>
public class ChatBubble : MonoBehaviour
{
    public TMP_Text text;
    public LayoutElement layout;   // LayoutElement của Bubble
    public float horizontalPadding = 28f;

    public void Set(string message, float maxWidth)
    {
        text.text = message;
        float w = text.GetPreferredValues(message).x + horizontalPadding;
        layout.preferredWidth = Mathf.Min(w, maxWidth);
    }
}
