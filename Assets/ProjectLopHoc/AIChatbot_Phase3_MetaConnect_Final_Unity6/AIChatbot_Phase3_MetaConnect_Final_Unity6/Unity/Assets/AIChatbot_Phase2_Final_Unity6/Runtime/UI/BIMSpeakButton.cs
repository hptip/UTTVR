using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Nút "Đọc" gắn trên từng bubble AI. MỘT nút đổi trạng thái:
    /// Đọc → ... (đang tạo) → Dừng (đang phát) → Đọc lại (xong / còn cache) / Lỗi.
    /// Nút KHÔNG giữ âm thanh: cache nằm ở BIMTextToSpeechController (theo MB). Nếu cache đã bị xóa, "Đọc lại" sẽ gọi TTS lại.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BIMSpeakButton : MonoBehaviour
    {
        public enum SpeakState { Idle, Generating, Playing, Ready, Error }

        [Header("Thành phần UI (Builder/Installer tự gán)")]
        public Button button;
        public TMP_Text label;
        public Graphic background;

        [Header("Màu theo trạng thái")]
        public Color idleColor = new Color(0.20f, 0.35f, 0.50f, 1f);
        public Color generatingColor = new Color(0.75f, 0.60f, 0.15f, 1f);
        public Color playingColor = new Color(0.85f, 0.30f, 0.20f, 1f);
        public Color readyColor = new Color(0.10f, 0.50f, 0.40f, 1f);
        public Color errorColor = new Color(0.60f, 0.10f, 0.10f, 1f);

        [Header("Nhãn")]
        public string idleLabel = "Đọc";
        public string generatingLabel = "...";
        public string playingLabel = "Dừng";
        public string readyLabel = "Đọc lại";
        public string errorLabel = "Lỗi";

        public SpeakState State { get; private set; } = SpeakState.Idle;
        /// <summary>Nội dung gốc (chưa làm sạch) của tin nhắn AI này.</summary>
        public string Text { get; private set; } = "";
        /// <summary>Khóa cache của lần đọc gần nhất (nội dung + model + giọng + tốc độ).</summary>
        public string LastCacheKey { get; set; }

        private bool listenerAdded;
        private bool bound;
        private bool autoSpoken;

        private void Awake() => HookButton();

        private void HookButton()
        {
            if (listenerAdded || button == null) return;
            button.onClick.AddListener(OnClicked);
            listenerAdded = true;
        }

        /// <summary>Gắn nội dung và hiện nút. Trả false nếu đã gắn đúng nội dung này (chống sự kiện lặp).</summary>
        public bool Bind(string text)
        {
            text = text ?? "";
            if (bound && Text == text) return false;
            bound = true;
            Text = text;
            gameObject.SetActive(true);
            HookButton();
            SetState(SpeakState.Idle);
            return true;
        }

        /// <summary>Mỗi tin nhắn chỉ được tự đọc (Auto Speak) tối đa một lần.</summary>
        public bool TryMarkAutoSpoken()
        {
            if (autoSpoken) return false;
            autoSpoken = true;
            return true;
        }

        private void OnClicked()
        {
            var controller = BIMTextToSpeechController.Instance;
            if (controller != null) controller.Toggle(this);
        }

        public void SetState(SpeakState state)
        {
            State = state;
            string text; Color color;
            switch (state)
            {
                case SpeakState.Generating: text = generatingLabel; color = generatingColor; break;
                case SpeakState.Playing: text = playingLabel; color = playingColor; break;
                case SpeakState.Ready: text = readyLabel; color = readyColor; break;
                case SpeakState.Error: text = errorLabel; color = errorColor; break;
                default: text = idleLabel; color = idleColor; break;
            }
            if (label != null) label.text = text;
            if (background != null) background.color = color;
        }

        private void OnDestroy()
        {
            var controller = BIMTextToSpeechController.Instance;
            if (controller != null) controller.NotifyButtonDestroyed(this);
        }
    }
}
