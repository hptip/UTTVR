using System;
using System.Threading;
using System.Threading.Tasks;
using BIM4D.AI.Generative;
using UnityEngine;

/// <summary>
/// Điều phối Push-to-Talk: IVoiceTrigger → VoiceInputController → GroqSpeechToText.
///
/// QUY TẮC:
///  - Chỉ đưa transcript vào ô nhập. KHÔNG bao giờ gọi AIChatbotIntegration / XkiroBrain.
///    Người dùng phải tự nhấn Gửi (AIChatbotUIController.Send).
///  - Mỗi lần nhấn mic là một PHIÊN riêng (sessionId + CancellationTokenSource).
///    Phiên cũ bị hủy sẽ không bao giờ được cập nhật UI/transcript nữa.
///  - Không chứa code hiển thị; giao diện nằm ở VoiceInputView (lắng nghe StateChanged).
/// </summary>
[DisallowMultipleComponent]
public sealed class VoiceInputController : MonoBehaviour
{
    public enum VoiceState { Idle, Starting, Recording, Transcribing, TranscriptReady, Error }

    [Header("Tham chiếu (để trống = tự tìm)")]
    [SerializeField] private GroqSpeechToText speechToText;
    [SerializeField] private AIChatbotUIController uiController;
    [Tooltip("Component implement IVoiceTrigger (PointerVoiceTrigger, XRVoiceTrigger...). Để trống = dùng/tạo PointerVoiceTrigger trên Voice Button.")]
    [SerializeField] private MonoBehaviour triggerSource;

    [Header("Hành vi")]
    [SerializeField, Min(0.1f)] private float minimumRecordingSeconds = 0.4f;
    [Tooltip("Bật: nối transcript vào chữ đang có trong ô nhập. Tắt: thay thế.")]
    [SerializeField] private bool appendTranscript = true;
    [Tooltip("Thời gian tối đa đợi phiên cũ (đã hủy) dọn xong trước khi bắt đầu phiên mới.")]
    [SerializeField, Min(0f)] private float previousSessionWaitSeconds = 1.5f;
    [Tooltip("Hủy phiên khi cửa sổ mất focus (Alt-Tab) hoặc ứng dụng bị tạm dừng.")]
    [SerializeField] private bool cancelOnFocusLoss = true;

    public const string HintIdle = "Giữ nút mic để nói.";

    /// <summary>(state, message). Message là tiếng Việt, sẵn sàng hiển thị.</summary>
    public event Action<VoiceState, string> StateChanged;

    public VoiceState State { get; private set; } = VoiceState.Idle;
    public string StatusMessage { get; private set; } = HintIdle;
    public bool IsSessionActive => State == VoiceState.Starting || State == VoiceState.Recording || State == VoiceState.Transcribing;

    public GroqSpeechToText SpeechToText => speechToText;
    public float RecordingElapsed => speechToText != null ? speechToText.RecordingElapsed : 0f;
    public float MaxRecordingSeconds => speechToText != null ? speechToText.MaxRecordingSeconds : 0f;
    public float CurrentLevel => speechToText != null && State == VoiceState.Recording ? speechToText.GetCurrentLevel() : 0f;

    private IVoiceTrigger trigger;
    private bool triggerHooked;
    private bool uiHooked;
    private int sessionId;
    private CancellationTokenSource sessionCts;
    private bool releaseRequested;

    // ------------------------------------------------------------------ lifecycle

    private void Awake()
    {
        EnsureReferences();
        ResolveTrigger();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ResolveTrigger();
        HookTrigger();
        HookUi();
        SetState(VoiceState.Idle, HintIdle);
    }

    private void OnDisable()
    {
        UnhookTrigger();
        UnhookUi();
        CancelSession();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && cancelOnFocusLoss && IsSessionActive) CancelSession();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && cancelOnFocusLoss && IsSessionActive) CancelSession();
    }

    private void EnsureReferences()
    {
        if (uiController == null) uiController = GetComponent<AIChatbotUIController>();
        if (uiController == null) uiController = GetComponentInParent<AIChatbotUIController>(true);
        if (uiController == null) uiController = FindFirstObjectByType<AIChatbotUIController>(FindObjectsInactive.Include);
        if (speechToText == null) speechToText = GetComponent<GroqSpeechToText>();
        // Include: GroqSpeechToText nằm trên object 'Chat Bot' có thể đang inactive.
        if (speechToText == null) speechToText = FindFirstObjectByType<GroqSpeechToText>(FindObjectsInactive.Include);
    }

    // ------------------------------------------------------------------ trigger (PC / XR)

    /// <summary>Đổi nguồn đầu vào lúc chạy (ví dụ XRVoiceTrigger). Không cần sửa logic ghi âm.</summary>
    public void SetTrigger(IVoiceTrigger newTrigger)
    {
        UnhookTrigger();
        trigger = newTrigger;
        if (isActiveAndEnabled) HookTrigger();
    }

    private void ResolveTrigger()
    {
        if (trigger != null) return;
        if (triggerSource != null) trigger = triggerSource as IVoiceTrigger;
        if (trigger != null) return;
        if (uiController != null && uiController.voiceButton != null)
        {
            var go = uiController.voiceButton.gameObject;
            var pointer = go.GetComponent<PointerVoiceTrigger>();
            if (pointer == null) pointer = go.AddComponent<PointerVoiceTrigger>();
            trigger = pointer;
        }
    }

    private void HookTrigger()
    {
        if (trigger == null || triggerHooked) return;
        trigger.Pressed += OnPressed;
        trigger.Released += OnReleased;
        trigger.Cancelled += OnTriggerCancelled;
        triggerHooked = true;
    }

    private void UnhookTrigger()
    {
        if (trigger == null || !triggerHooked) return;
        trigger.Pressed -= OnPressed;
        trigger.Released -= OnReleased;
        trigger.Cancelled -= OnTriggerCancelled;
        triggerHooked = false;
    }

    private void HookUi()
    {
        if (uiController == null || uiHooked) return;
        uiController.onClosed.AddListener(CancelSession);
        uiController.onMessageSent.AddListener(OnMessageSent);
        uiController.onInputCleared.AddListener(OnInputCleared);
        uiHooked = true;
    }

    private void UnhookUi()
    {
        if (uiController == null || !uiHooked) return;
        uiController.onClosed.RemoveListener(CancelSession);
        uiController.onMessageSent.RemoveListener(OnMessageSent);
        uiController.onInputCleared.RemoveListener(OnInputCleared);
        uiHooked = false;
    }

    // ------------------------------------------------------------------ trigger events

    private void OnPressed()
    {
        if (!isActiveAndEnabled) return;
        if (IsSessionActive) return; // không cho hai phiên chạy đồng thời

        EnsureReferences();
        if (uiController == null || speechToText == null)
        {
            SetState(VoiceState.Error, "Thiếu cấu hình Voice Input: không tìm thấy AIChatbotUIController hoặc GroqSpeechToText.");
            return;
        }
        if (uiController.IsBusy)
        {
            SetState(State, "Chatbot đang xử lý câu hỏi, vui lòng đợi.");
            return;
        }

        BeginSession();
    }

    private void OnReleased()
    {
        if (State == VoiceState.Starting || State == VoiceState.Recording) releaseRequested = true;
    }

    private void OnTriggerCancelled() => CancelSession();

    private void OnMessageSent()
    {
        if (State == VoiceState.TranscriptReady || State == VoiceState.Error) SetState(VoiceState.Idle, HintIdle);
    }

    private void OnInputCleared()
    {
        if (IsSessionActive) CancelSession();
        else if (State == VoiceState.TranscriptReady || State == VoiceState.Error) SetState(VoiceState.Idle, HintIdle);
    }

    // ------------------------------------------------------------------ session

    private bool IsCurrent(int id) => id == sessionId && this != null;

    private void BeginSession()
    {
        CancelToken();
        sessionCts = new CancellationTokenSource();
        int id = ++sessionId;
        releaseRequested = false;
        uiController.SetVoiceOperationLocked(true);
        SetState(VoiceState.Starting, "Đang khởi động microphone…");
        _ = RunSessionAsync(id, sessionCts.Token);
    }

    private async Task RunSessionAsync(int id, CancellationToken token)
    {
        bool limitReached = false;
        try
        {
            // Một phiên cũ vừa bị hủy có thể còn đang dọn (xin quyền / upload). Đợi nó xong,
            // tối đa previousSessionWaitSeconds, để không gặp lỗi "đang bận".
            float waitStart = Time.realtimeSinceStartup;
            while (speechToText.IsStarting || speechToText.IsRecording || speechToText.IsTranscribing)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - waitStart > previousSessionWaitSeconds) break;
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();

            // Có thể chờ hộp thoại xin quyền microphone; hủy qua token sẽ thoát ngay.
            await speechToText.StartRecordingAsync(token);
            if (!IsCurrent(id))
            {
                speechToText.CancelRecording();
                return;
            }

            SetState(VoiceState.Recording, "Đang nghe… Giữ nút mic và nói.");

            // Chờ: nhả nút / hết thời lượng / bị hủy.
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (releaseRequested) break;
                if (speechToText.RecordingElapsed >= speechToText.MaxRecordingSeconds - 0.05f)
                {
                    limitReached = true;
                    break;
                }
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();

            if (!limitReached && speechToText.RecordingElapsed < minimumRecordingSeconds)
            {
                speechToText.CancelRecording();
                EndSession(id, VoiceState.Idle, "Thời gian nói quá ngắn. Hãy giữ nút mic lâu hơn.");
                return;
            }

            SetState(VoiceState.Transcribing, limitReached
                ? "Đã đạt giới hạn thời gian ghi âm. Đang nhận diện giọng nói…"
                : "Đang nhận diện giọng nói…");

            // Kiểm tra im lặng + gửi Groq nằm trong GroqSpeechToText. Token hủy được cả lúc upload.
            string transcript = await speechToText.StopRecordingAndTranscribeAsync(token);
            if (!IsCurrent(id)) return; // phiên cũ: tuyệt đối không đụng tới UI

            transcript = transcript == null ? "" : transcript.Trim();
            if (transcript.Length == 0)
                throw new SpeechRecognitionException(SpeechErrorCode.EmptyTranscript,
                    "Không nhận diện được nội dung giọng nói. Hãy nói rõ hơn và thử lại.");

            // Đọc nội dung ô nhập TẠI THỜI ĐIỂM NÀY (người dùng có thể đã gõ trong lúc chờ).
            string current = uiController.GetInputText();
            string combined = appendTranscript && !string.IsNullOrWhiteSpace(current)
                ? current.TrimEnd() + " " + transcript
                : transcript;
            uiController.SetInputText(combined); // focus + caret ở cuối, KHÔNG gửi

            EndSession(id, VoiceState.TranscriptReady, limitReached
                ? "Đã nhận diện (đạt giới hạn thời gian ghi âm). Kiểm tra nội dung rồi nhấn Gửi."
                : "Đã nhận diện. Kiểm tra nội dung rồi nhấn Gửi.");
        }
        catch (OperationCanceledException)
        {
            // Thường do CancelSession (đã tự cập nhật trạng thái). Nếu phiên vẫn "hiện hành"
            // (ví dụ object bị destroy) thì dọn dẹp ở đây.
            if (IsCurrent(id))
            {
                SafeCancelRecording();
                EndSession(id, VoiceState.Idle, "Đã hủy ghi âm.");
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrent(id))
            {
                Debug.LogWarning("[Voice Input] Lỗi của phiên đã hủy (bỏ qua): " + ex.Message, this);
                return;
            }
            SafeCancelRecording();
            Debug.LogWarning("[Voice Input] " + ex.Message, this);
            EndSession(id, VoiceState.Error, ToVietnameseError(ex));
        }
    }

    /// <summary>Kết thúc phiên hiện hành: mở khóa Send, đặt trạng thái. Bỏ qua nếu phiên đã cũ.</summary>
    private void EndSession(int id, VoiceState state, string message)
    {
        if (!IsCurrent(id)) return;
        if (uiController != null)
        {
            uiController.SetVoiceOperationLocked(false);
            if (state != VoiceState.TranscriptReady) uiController.FocusInput();
        }
        SetState(state, message);
    }

    /// <summary>Hủy phiên hiện tại (nếu có). Gọi nhiều lần vẫn an toàn.</summary>
    public void CancelSession()
    {
        bool wasActive = IsSessionActive;
        sessionId++; // mọi phiên đang chạy trở thành "cũ"
        releaseRequested = false;
        CancelToken();
        SafeCancelRecording();
        if (uiController != null) uiController.SetVoiceOperationLocked(false);
        if (wasActive) SetState(VoiceState.Idle, "Đã hủy ghi âm.");
    }

    private void CancelToken()
    {
        if (sessionCts == null) return;
        var cts = sessionCts;
        sessionCts = null;
        try { cts.Cancel(); } catch (Exception) { /* callback của task đã kết thúc: bỏ qua */ }
    }

    private void SafeCancelRecording()
    {
        if (speechToText != null) speechToText.CancelRecording();
    }

    // ------------------------------------------------------------------ state / errors

    private void SetState(VoiceState state, string message)
    {
        State = state;
        StatusMessage = message ?? "";
        StateChanged?.Invoke(State, StatusMessage);
    }

    private static string ToVietnameseError(Exception ex)
    {
        // GroqSpeechToText đã trả message tiếng Việt kèm mã lỗi.
        if (ex is SpeechRecognitionException) return ex.Message;
        if (ex is TimeoutException) return "Hết thời gian chờ. Hãy kiểm tra Internet và thử lại.";
        string m = string.IsNullOrEmpty(ex.Message) ? "lỗi không xác định" : ex.Message;
        return "Không thể xử lý giọng nói: " + m;
    }
}
