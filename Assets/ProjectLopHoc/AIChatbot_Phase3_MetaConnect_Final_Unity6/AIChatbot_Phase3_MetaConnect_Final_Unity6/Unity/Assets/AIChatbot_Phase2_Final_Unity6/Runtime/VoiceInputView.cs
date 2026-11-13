using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị trạng thái Voice: màu/nhãn nút mic, dòng trạng thái, thanh mức âm, đồng hồ 00:05 / 00:30.
/// Chỉ ĐỌC từ VoiceInputController; không điều khiển ghi âm. Các tham chiếu UI do AIChatbotBuilder /
/// VoiceInputInstaller tự gán.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoiceInputView : MonoBehaviour
{
    [Header("Nguồn dữ liệu (để trống = tự tìm)")]
    public VoiceInputController controller;
    [Tooltip("Tùy chọn: dùng để hiện gợi ý 'Thả để HỦY' khi con trỏ rời khỏi nút lúc đang ghi.")]
    public PointerVoiceTrigger pointerTrigger;

    [Header("Thành phần UI")]
    public Graphic micGraphic;
    public TMP_Text micLabel;
    public TMP_Text statusText;
    public Slider levelSlider;
    public TMP_Text timerText;

    [Header("Màu nút Mic")]
    public Color idleColor = new Color(0.20f, 0.28f, 0.38f, 1f);
    public Color startingColor = new Color(0.75f, 0.60f, 0.15f, 1f);
    public Color recordingColor = new Color(0.85f, 0.15f, 0.20f, 1f);
    public Color recordingPulseColor = new Color(1.00f, 0.45f, 0.35f, 1f);
    public Color transcribingColor = new Color(0.20f, 0.45f, 0.80f, 1f);
    public Color readyColor = new Color(0.10f, 0.55f, 0.35f, 1f);
    public Color errorColor = new Color(0.60f, 0.10f, 0.10f, 1f);

    [Header("Nhãn nút Mic")]
    public string idleLabel = "Mic";
    public string startingLabel = "...";
    public string recordingLabel = "Ghi";
    public string transcribingLabel = "...";
    public string readyLabel = "Mic";
    public string errorLabel = "Lỗi";

    [Header("Màu chữ trạng thái")]
    public Color statusNormalColor = new Color(0.60f, 0.75f, 0.85f, 1f);
    public Color statusRecordingColor = new Color(1.00f, 0.60f, 0.50f, 1f);
    public Color statusErrorColor = new Color(1.00f, 0.45f, 0.45f, 1f);

    [Header("Khác")]
    [Min(1f)] public float levelSmoothing = 16f;
    [Min(0f)] public float pulseSpeed = 6f;

    private float level;
    private bool outsideHintShown;
    private bool subscribed;

    private void OnEnable()
    {
        if (controller == null) controller = GetComponent<VoiceInputController>();
        if (controller == null) controller = GetComponentInParent<VoiceInputController>(true);
        if (controller == null) controller = FindFirstObjectByType<VoiceInputController>(FindObjectsInactive.Include);
        if (controller == null) return;

        controller.StateChanged += OnStateChanged;
        subscribed = true;
        Apply(controller.State, controller.StatusMessage);
    }

    private void OnDisable()
    {
        if (subscribed && controller != null) controller.StateChanged -= OnStateChanged;
        subscribed = false;
    }

    private void OnStateChanged(VoiceInputController.VoiceState state, string message) => Apply(state, message);

    private void Apply(VoiceInputController.VoiceState state, string message)
    {
        outsideHintShown = false;
        SetStatus(message, state);

        Color color;
        string label;
        switch (state)
        {
            case VoiceInputController.VoiceState.Starting: color = startingColor; label = startingLabel; break;
            case VoiceInputController.VoiceState.Recording: color = recordingColor; label = recordingLabel; break;
            case VoiceInputController.VoiceState.Transcribing: color = transcribingColor; label = transcribingLabel; break;
            case VoiceInputController.VoiceState.TranscriptReady: color = readyColor; label = readyLabel; break;
            case VoiceInputController.VoiceState.Error: color = errorColor; label = errorLabel; break;
            default: color = idleColor; label = idleLabel; break;
        }
        if (micGraphic != null) micGraphic.color = color;
        if (micLabel != null) micLabel.text = label;

        if (state != VoiceInputController.VoiceState.Recording)
        {
            level = 0f;
            if (levelSlider != null) levelSlider.value = 0f;
            SetTimer(0f);
        }
    }

    private void Update()
    {
        if (controller == null || controller.State != VoiceInputController.VoiceState.Recording) return;

        SetTimer(controller.RecordingElapsed);

        float blend = 1f - Mathf.Exp(-levelSmoothing * Time.unscaledDeltaTime);
        level = Mathf.Lerp(level, controller.CurrentLevel, blend);
        if (levelSlider != null) levelSlider.value = level;

        if (micGraphic != null)
            micGraphic.color = Color.Lerp(recordingColor, recordingPulseColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));

        // Gợi ý hủy: con trỏ đang giữ nhưng đã rời khỏi nút → nhả lúc này sẽ HỦY.
        if (pointerTrigger != null && pointerTrigger.CancelWhenReleasedOutside)
        {
            bool outside = pointerTrigger.IsHeld && !pointerTrigger.IsPointerInside;
            if (outside != outsideHintShown)
            {
                outsideHintShown = outside;
                SetStatus(outside ? "Thả chuột ở đây để HỦY ghi âm." : controller.StatusMessage, controller.State);
            }
        }
    }

    private void SetStatus(string message, VoiceInputController.VoiceState state)
    {
        if (statusText == null) return;
        statusText.text = message ?? "";
        statusText.color = state == VoiceInputController.VoiceState.Error ? statusErrorColor
            : state == VoiceInputController.VoiceState.Recording ? statusRecordingColor
            : statusNormalColor;
    }

    private void SetTimer(float elapsedSeconds)
    {
        if (timerText == null) return;
        float max = controller != null ? controller.MaxRecordingSeconds : 0f;
        if (max <= 0f) max = 30f;
        timerText.text = Format(elapsedSeconds) + " / " + Format(max);
    }

    private static string Format(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
