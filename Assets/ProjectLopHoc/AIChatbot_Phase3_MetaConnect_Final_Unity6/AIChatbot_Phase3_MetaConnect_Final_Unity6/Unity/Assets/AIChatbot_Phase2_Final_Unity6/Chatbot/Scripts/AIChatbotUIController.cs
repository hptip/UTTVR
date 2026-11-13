using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Điều khiển giao diện chatbot: mở/đóng, chuột, crosshair, hiển thị tin nhắn.
/// KHÔNG gọi API trực tiếp - mọi yêu cầu AI đi qua AIChatbotIntegration.
/// </summary>
public class AIChatbotUIController : MonoBehaviour
{
    [Header("UI (builder đã gán sẵn)")]
    public GameObject panel;
    public ScrollRect scrollRect;
    public RectTransform content;
    public TMP_InputField inputField;
    public Button sendButton;
    public Button closeButton;
    public Button voiceButton;          // Push-to-talk: dùng PointerVoiceTrigger, KHÔNG dùng onClick
    public Button clearButton;          // Xóa nội dung ô nhập (không xóa lịch sử chat)
    public TMP_Text statusText;

    [Header("Message prefabs")]
    public ChatBubble userMessagePrefab;
    public ChatBubble aiMessagePrefab;
    public ChatBubble loadingMessagePrefab;

    [Header("Kết nối AI")]
    public AIChatbotIntegration integration;

    [Header("Điều khiển game")]
    [Tooltip("Để trống = tự tìm GameObject tên 'Crosshair'")]
    public GameObject crosshair;
    [Tooltip("Kéo script look/move của Player vào đây. Chỉ các script này bị tắt khi chat mở.")]
    public Behaviour[] behavioursToDisable;
    public bool openWithCKey = true;

    [Header("Khác")]
    [TextArea] public string greeting = "Xin chào! Mình là trợ lý AI. Bạn muốn hỏi gì?";
    public bool showVoiceButton = true;
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();
    public UnityEvent onVoiceButtonClicked = new UnityEvent(); // Giữ tương thích serialized cũ; Push-to-talk không dùng OnClick.
    [Tooltip("Phát sau khi một tin nhắn được gửi đi qua Send().")]
    public UnityEvent onMessageSent = new UnityEvent();
    [Tooltip("Phát khi người dùng bấm Clear (xóa ô nhập).")]
    public UnityEvent onInputCleared = new UnityEvent();

    /// <summary>
    /// Phát SAU khi bubble câu trả lời AI THẬT đã hiển thị (chỉ từ OnReply thành công).
    /// Không phát cho tin nhắn người dùng, lời chào, hay thông báo lỗi. Dùng cho Voice Output (TTS).
    /// Lỗi trong người nghe không làm hỏng chat.
    /// </summary>
    public event System.Action<ChatBubble, string> onAiReplyAdded;

    public bool IsOpen { get; private set; }
    public bool IsBusy => _busy;
    public bool IsVoiceOperationLocked => _voiceOperationLocked;

    CursorLockMode _prevLock;
    bool _prevVisible;
    bool _crosshairWasActive;
    readonly List<Behaviour> _disabled = new List<Behaviour>();
    bool _busy;
    bool _voiceOperationLocked;
    ChatBubble _loading;
    Coroutine _dots;

    void Awake()
    {
        if (integration == null) integration = GetComponentInChildren<AIChatbotIntegration>(true);
        if (integration == null) integration = GetComponentInParent<AIChatbotIntegration>();
        EnsureEventSystem();

        if (sendButton) sendButton.onClick.AddListener(Send);
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (voiceButton)
        {
            voiceButton.gameObject.SetActive(showVoiceButton);
            // Push-to-talk uses PointerDown/PointerUp, never Button.onClick.
        }
        if (clearButton) clearButton.onClick.AddListener(ClearInput);
        if (inputField)
        {
            inputField.onSubmit.AddListener(_ => Send());
            // Khi focus vào ô nhập (kể cả sau khi có transcript) KHÔNG bôi đen toàn bộ chữ,
            // để người dùng sửa transcript ngay mà không lỡ ghi đè.
            inputField.onFocusSelectAll = false;
        }

        panel.SetActive(false);
        SetStatusText("Sẵn sàng");
        if (!string.IsNullOrEmpty(greeting)) AddMessage(greeting, false);
    }

    void Update()
    {
        if (!IsOpen)
        {
            if (openWithCKey && ChatbotInputController.TogglePressed()) Open();
            return;
        }
        bool typing = inputField != null && (inputField.isFocused || IsInputSelected());
        if (ChatbotInputController.EscapePressed()) Close();
        else if (!typing && ChatbotInputController.TogglePressed()) Close();
    }

    void LateUpdate()
    {
        // Giữ chuột mở dù script khác cố khóa lại
        if (IsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------- Mở / đóng ----------
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        _prevLock = Cursor.lockState;
        _prevVisible = Cursor.visible;

        panel.SetActive(true);
        if (!_busy) SetStatusText("Sẵn sàng");

        if (crosshair == null) crosshair = GameObject.Find("Crosshair");
        _crosshairWasActive = crosshair != null && crosshair.activeSelf;
        if (crosshair != null) crosshair.SetActive(false);

        _disabled.Clear();
        if (behavioursToDisable != null)
            foreach (var b in behavioursToDisable)
                if (b != null && b.enabled) { b.enabled = false; _disabled.Add(b); }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(FocusNextFrame());
        StartCoroutine(ScrollToBottom());
        onOpened.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        SetVoiceOperationLocked(false); // phòng hờ: không bao giờ để Send bị khóa sau khi đóng
        panel.SetActive(false);

        foreach (var b in _disabled) if (b != null) b.enabled = true;
        _disabled.Clear();

        if (crosshair != null && _crosshairWasActive) crosshair.SetActive(true);

        Cursor.lockState = _prevLock;
        Cursor.visible = _prevVisible;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        onClosed.Invoke();
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    IEnumerator FocusNextFrame()
    {
        yield return null; // tránh ký tự 'C' lọt vào ô nhập
        if (IsOpen && inputField != null) inputField.ActivateInputField();
    }

    // ---------- Gửi / nhận ----------
    public void Send()
    {
        if (_busy || _voiceOperationLocked || inputField == null) return;
        string text = inputField.text.Trim();
        if (text.Length == 0) return;
        if (integration == null)
        {
            AddMessage("[Lỗi] Chưa có AIChatbotIntegration.", false);
            return;
        }

        inputField.text = "";
        AddMessage(text, true);
        SetBusy(true); // trạng thái: "Đang gửi câu hỏi đến AI…"
        integration.Ask(text, OnReply, OnError);
        // Ask() đã giao request cho XkiroBrain (HTTP đang chạy). Nếu Ask báo lỗi ngay thì _busy đã false.
        if (_busy) SetStatusText("AI đang tạo câu trả lời…");
        inputField.ActivateInputField();
        onMessageSent.Invoke();
    }

    void OnReply(string reply)
    {
        SetBusy(false);
        bool hasContent = !string.IsNullOrWhiteSpace(reply);
        string shown = hasContent ? reply.Trim() : "(AI không trả lời nội dung)";
        var bubble = AddMessageAndGetBubble(shown, false); // văn bản luôn hiển thị TRƯỚC, độc lập với TTS
        SetStatusText("Hoàn tất");
        if (bubble != null && hasContent && onAiReplyAdded != null)
        {
            try { onAiReplyAdded.Invoke(bubble, shown); }
            catch (System.Exception ex) { Debug.LogException(ex); } // lỗi ở người nghe không được phá chat
        }
    }

    void OnError(string error)
    {
        SetBusy(false);
        AddMessage("[Lỗi] " + error, false);
        SetStatusText("Có lỗi. Xem thông báo trong khung chat.");
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        RefreshInteractable();
        if (busy) SetStatusText("Đang gửi câu hỏi đến AI…");

        if (busy)
        {
            if (loadingMessagePrefab != null)
            {
                _loading = Instantiate(loadingMessagePrefab, content);
                _loading.Set("AI đang suy nghĩ", MaxBubbleWidth());
                _dots = StartCoroutine(AnimateDots());
                StartCoroutine(ScrollToBottom());
            }
        }
        else
        {
            if (_dots != null) StopCoroutine(_dots);
            _dots = null;
            if (_loading != null) Destroy(_loading.gameObject);
            _loading = null;
        }
    }

    IEnumerator AnimateDots()
    {
        int n = 0;
        while (_loading != null)
        {
            n = (n + 1) % 4;
            _loading.text.text = "AI đang suy nghĩ" + new string('.', n);
            yield return new WaitForSeconds(0.4f);
        }
    }

    // ---------- Trạng thái AI (header) ----------
    // Trạng thái GIỌNG NÓI (nghe / nhận diện) hiển thị riêng ở dòng Voice Status do VoiceInputView quản lý,
    // nên "AI đang..." chỉ xuất hiện khi thật sự đang gửi/chờ AI.
    public void SetStatusText(string message)
    {
        if (statusText != null) statusText.text = message ?? "";
    }

    // ---------- API cho Voice / nút Clear ----------
    public string GetInputText() => inputField != null ? inputField.text : "";

    /// <summary>Đặt nội dung ô nhập (KHÔNG gửi), đưa caret về cuối và focus để sửa ngay.</summary>
    public void SetInputText(string text)
    {
        if (inputField == null) return;
        inputField.text = text ?? "";
        FocusInput();
    }

    /// <summary>Focus ô nhập, caret ở cuối, không bôi đen. Không làm gì nếu chatbot đang đóng.</summary>
    public void FocusInput()
    {
        if (!IsOpen || inputField == null) return;
        inputField.ActivateInputField();
        inputField.MoveTextEnd(false);
    }

    /// <summary>Xóa ô nhập. KHÔNG gửi AI, KHÔNG xóa lịch sử hội thoại.</summary>
    public void ClearInput()
    {
        if (inputField == null) return;
        inputField.text = "";
        inputField.caretPosition = 0;
        FocusInput();
        onInputCleared.Invoke();
    }

    /// <summary>Khóa Send trong lúc đang ghi âm / nhận diện để transcript không bị đè hoặc gửi dở.</summary>
    public void SetVoiceOperationLocked(bool locked)
    {
        _voiceOperationLocked = locked;
        RefreshInteractable();
    }

    void RefreshInteractable()
    {
        if (sendButton != null) sendButton.interactable = !_busy && !_voiceOperationLocked;
        // Nút Mic và Clear luôn nhận sự kiện: Mic tự xử lý trạng thái bận, Clear luôn dùng được.
    }

    bool IsInputSelected()
    {
        return EventSystem.current != null && inputField != null &&
               EventSystem.current.currentSelectedGameObject == inputField.gameObject;
    }

    // ---------- Tin nhắn ----------
    /// <summary>Giữ nguyên chữ ký/hành vi cũ (tương thích ngược).</summary>
    public void AddMessage(string message, bool fromUser)
    {
        AddMessageAndGetBubble(message, fromUser);
    }

    /// <summary>Giống AddMessage nhưng trả về bubble vừa tạo (null nếu không tạo được).</summary>
    public ChatBubble AddMessageAndGetBubble(string message, bool fromUser)
    {
        var prefab = fromUser ? userMessagePrefab : aiMessagePrefab;
        if (prefab == null || content == null) return null;
        var bubble = Instantiate(prefab, content);
        bubble.Set(message, MaxBubbleWidth());
        StartCoroutine(ScrollToBottom());
        return bubble;
    }

    float MaxBubbleWidth()
    {
        float w = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.width : 0f;
        return w > 50f ? w * 0.82f : 360f;
    }

    IEnumerator ScrollToBottom()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ClearMessages()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        if (!string.IsNullOrEmpty(greeting)) AddMessage(greeting, false);
    }

    // ---------- EventSystem ----------
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
#if UNITY_2022_2_OR_NEWER
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
