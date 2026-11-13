using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Dải cài đặt giọng đọc: Auto Speak, Ngôn ngữ (Tự động/Việt/English), giọng (Trước/Sau), Tốc độ, Âm lượng, Dừng, trạng thái.
    /// Chỉ nút bấm + slider (không cần chuột phải/bàn phím) nên dùng được bằng chuột hoặc bất kỳ thiết bị trỏ nào.
    /// Chỉ kết nối giao diện với BIMTextToSpeechController; không chứa logic TTS.
    /// Tham chiếu UI do BIMVoiceOutputFactory (Builder/Installer) tự gán.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BIMVoiceOutputUI : MonoBehaviour
    {
        public BIMTextToSpeechController controller;

        [Header("Thành phần UI")]
        public Button autoSpeakButton;
        public TMP_Text autoSpeakLabel;
        public Button languageButton;
        public TMP_Text languageLabel;
        public Button voicePrevButton;
        public Button voiceNextButton;
        public TMP_Text voiceLabel;
        public Button stopButton;
        public Slider volumeSlider;
        public Slider speedSlider;
        public TMP_Text speedLabel;
        public TMP_Text statusText;

        [Header("Màu")]
        public Color autoOnColor = new Color(0.10f, 0.50f, 0.40f, 1f);
        public Color autoOffColor = new Color(0.20f, 0.30f, 0.40f, 1f);
        public Color statusNormalColor = new Color(0.60f, 0.75f, 0.85f, 1f);
        public Color statusErrorColor = new Color(1.00f, 0.45f, 0.45f, 1f);

        private bool hooked;
        private bool refreshing;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<BIMTextToSpeechController>();
            if (controller == null) controller = FindFirstObjectByType<BIMTextToSpeechController>(FindObjectsInactive.Include);
            if (controller == null) return;

            if (speedSlider != null)
            {
                speedSlider.minValue = controller.Settings.minSpeed;
                speedSlider.maxValue = controller.Settings.maxSpeed;
                speedSlider.wholeNumbers = false;
            }

            HookControls();
            controller.SettingsChanged += Refresh;
            controller.StatusChanged += OnStatus;
            controller.SpeakingChanged += OnSpeaking;
            Refresh();
            OnSpeaking(controller.IsSpeaking);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.SettingsChanged -= Refresh;
            controller.StatusChanged -= OnStatus;
            controller.SpeakingChanged -= OnSpeaking;
        }

        private void HookControls()
        {
            if (hooked) return;
            hooked = true;
            if (autoSpeakButton != null) autoSpeakButton.onClick.AddListener(() => controller.AutoSpeak = !controller.AutoSpeak);
            if (languageButton != null) languageButton.onClick.AddListener(() => controller.CycleLanguageMode());
            if (voicePrevButton != null) voicePrevButton.onClick.AddListener(() => controller.CycleVoice(-1));
            if (voiceNextButton != null) voiceNextButton.onClick.AddListener(() => controller.CycleVoice(+1));
            if (stopButton != null) stopButton.onClick.AddListener(() => controller.Stop());
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => { if (!refreshing) controller.Volume = v; });
            if (speedSlider != null) speedSlider.onValueChanged.AddListener(v => { if (!refreshing) controller.Speed = v; });
        }

        private void Refresh()
        {
            if (controller == null) return;
            refreshing = true;
            if (autoSpeakLabel != null) autoSpeakLabel.text = controller.AutoSpeak ? "Tự đọc: BẬT" : "Tự đọc: TẮT";
            if (autoSpeakButton != null && autoSpeakButton.targetGraphic != null)
                autoSpeakButton.targetGraphic.color = controller.AutoSpeak ? autoOnColor : autoOffColor;
            if (languageLabel != null)
                languageLabel.text = controller.AutoLanguage ? "Tự động" : (controller.Language == BIMTtsLanguage.English ? "English" : "Tiếng Việt");
            if (voiceLabel != null) voiceLabel.text = controller.VoiceDisplay;
            bool multi = controller.Voices.Count > 1;
            if (voicePrevButton != null) voicePrevButton.interactable = multi;
            if (voiceNextButton != null) voiceNextButton.interactable = multi;
            if (volumeSlider != null) volumeSlider.value = controller.Volume;
            if (speedSlider != null) speedSlider.value = controller.Speed;
            if (speedLabel != null) speedLabel.text = "Tốc độ " + controller.Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
            refreshing = false;
        }

        private void OnStatus(string message, bool isError)
        {
            if (statusText == null) return;
            statusText.text = message ?? "";
            statusText.color = isError ? statusErrorColor : statusNormalColor;
        }

        private void OnSpeaking(bool speaking)
        {
            if (stopButton != null) stopButton.interactable = speaking;
        }
    }
}
