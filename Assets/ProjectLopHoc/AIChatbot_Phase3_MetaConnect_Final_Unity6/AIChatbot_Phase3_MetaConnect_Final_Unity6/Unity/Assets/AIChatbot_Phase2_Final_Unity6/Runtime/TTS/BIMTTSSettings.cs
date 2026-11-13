using System;
using System.Collections.Generic;
using UnityEngine;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Cấu hình Voice Output. Tạo bằng menu Tools > BIM AI > Create or Select TTS Settings
    /// (file Resources/BIMTTSSettings.asset, đã kèm sẵn trong package). Nếu chưa có file, runtime dùng giá trị mặc định trong code.
    /// TUYỆT ĐỐI KHÔNG đặt khóa MetaConnect ở đây: asset này được đóng gói vào build Windows.
    /// Chỉ chứa Proxy URL và App Key của backend (có thể bị trích xuất -> backend giới hạn quota và có thể thu hồi).
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "BIM AI/TTS Settings")]
    public sealed class BIMTTSSettings : ScriptableObject
    {
        public const string ResourceName = "BIMTTSSettings";

        [Header("Backend proxy (KHÔNG nhập khóa MetaConnect vào đây)")]
        [Tooltip("Editor/Windows. Ví dụ: http://127.0.0.1:8787 (wrangler dev) hoặc https://<worker>.workers.dev")]
        public string proxyUrlEditorWindows = "http://127.0.0.1:8787";
        [Tooltip("App Key của backend proxy (không phải khóa MetaConnect).")]
        public string appKey = "";

        [Header("Model (alias MetaConnect)")]
        public string modelVietnamese = "tts-vi";
        public string modelEnglish = "tts-en";

        [Header("Giọng đọc = TÊN giọng gửi lên API (không phải ID)")]
        [Tooltip("Giọng trong catalogue MetaConnect mà backend cho phép (VI_VOICES). Mặc định đầu danh sách = Quang Sơn. Chỉ thêm giọng khi đã nghe thử.")]
        public string[] vietnameseVoices =
        {
            "Quang Sơn", "Thái Sơn", "Trúc Ly", "Minh Đức", "Phạm Tuyên", "Xuân Vĩnh", "Thanh Bình",
            "Mạnh Dũng", "Minh Quân", "Anh Khôi", "Minh Triết", "Đức Trí"
        };
        public string defaultVietnameseVoice = "Quang Sơn";
        [Tooltip("ĐỂ TRỐNG cho tới khi bạn nghe thử và xác minh giọng nam tiếng Anh (xem Voice_Output_Guide.md). " +
                 "MetaConnect âm thầm đổi sang giọng mặc định nếu tên giọng sai, nên không được đoán.")]
        public string[] englishVoices = new string[0];
        public string defaultEnglishVoice = "";

        [Header("Mặc định người dùng (lưu PlayerPrefs sau khi đổi)")]
        public BIMTtsLanguage defaultLanguage = BIMTtsLanguage.Vietnamese;
        [Tooltip("Bật = tự chọn tts-vi / tts-en theo ngôn ngữ của câu trả lời. Người dùng đổi được trên UI.")]
        public bool defaultAutoLanguage = false;
        [Range(0.7f, 1.5f)] public float defaultSpeed = 1.0f;
        public bool autoSpeakDefault = false;

        [Header("Giới hạn tốc độ đọc (MetaConnect cho 0.25-4.0)")]
        public float minSpeed = 0.7f;
        public float maxSpeed = 1.5f;

        [Header("Chia đoạn câu trả lời dài")]
        [Min(50)] public int firstChunkChars = 220;
        [Min(100)] public int chunkChars = 450;

        [Header("Cache âm thanh (RAM) - PCM16, theo MB")]
        [Min(1)] public int cacheBudgetMB = 24;

        [Header("Mạng")]
        [Min(5)] public int timeoutSeconds = 30;
        [Tooltip("Số request TTS đồng thời. Mặc định 1 (tuần tự). Tối đa 2.")]
        [Range(1, 2)] public int maxConcurrentRequests = 1;
        [Range(0, 4)] public int maxRetries = 2;
        [Tooltip("Sau lỗi xác thực/hết quota, tạm dừng gọi TTS trong bấy nhiêu giây để không gọi lặp.")]
        [Min(5)] public int blockSecondsAfterAuthOrQuota = 60;

        public long CacheBudgetBytes => Math.Max(1, cacheBudgetMB) * 1024L * 1024L;

        public string ProxyBaseUrl
        {
            get
            {
                string u = proxyUrlEditorWindows;
                return (u ?? "").Trim().TrimEnd('/');
            }
        }

        /// <summary>null = hợp lệ; ngược lại là lý do (tiếng Việt) cho người dùng.</summary>
        public string ProxyProblem
        {
            get
            {
                string u = ProxyBaseUrl;
                const string platform = "Proxy Url Editor Windows";
                if (u.Length == 0) return "Chưa nhập " + platform + " trong BIMTTSSettings.";
                bool https = u.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                bool http = u.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                if (!https && !http) return "Proxy URL phải bắt đầu bằng https:// (hoặc http://127.0.0.1 khi chạy thử cục bộ).";
                return null;
            }
        }

        public bool IsProxyConfigured => ProxyProblem == null;

        public List<string> GetVoices(BIMTtsLanguage lang)
        {
            var src = lang == BIMTtsLanguage.Vietnamese ? vietnameseVoices : englishVoices;
            var list = new List<string>();
            if (src == null) return list;
            foreach (var s in src)
            {
                string v = (s ?? "").Trim();
                if (v.Length > 0 && !list.Contains(v)) list.Add(v);
            }
            return list;
        }

        public string DefaultVoice(BIMTtsLanguage lang)
        {
            var list = GetVoices(lang);
            string d = ((lang == BIMTtsLanguage.Vietnamese ? defaultVietnameseVoice : defaultEnglishVoice) ?? "").Trim();
            if (d.Length > 0 && list.Contains(d)) return d;
            return list.Count > 0 ? list[0] : "";
        }

        public string ModelFor(BIMTtsLanguage lang) =>
            ((lang == BIMTtsLanguage.Vietnamese ? modelVietnamese : modelEnglish) ?? "").Trim();

        private void OnValidate()
        {
            minSpeed = Mathf.Clamp(minSpeed, 0.25f, 2f);
            maxSpeed = Mathf.Clamp(maxSpeed, minSpeed, 4f);
            defaultSpeed = Mathf.Clamp(defaultSpeed, minSpeed, maxSpeed);
            if (chunkChars < firstChunkChars) chunkChars = firstChunkChars;
            if (chunkChars > 1000) chunkChars = 1000; // backend mặc định chặn >1000 ký tự/request
        }

        // ------------------------------------------------------------------ nạp

        private static BIMTTSSettings cached;
        private static bool warned;

        public static BIMTTSSettings Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<BIMTTSSettings>(ResourceName);
            if (cached == null)
            {
                cached = CreateInstance<BIMTTSSettings>();
                cached.hideFlags = HideFlags.DontSave;
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[BIM TTS] Chưa có Resources/BIMTTSSettings.asset. Đang dùng cấu hình mặc định (chưa có App Key). " +
                                     "Tạo bằng Tools > BIM AI > Create or Select TTS Settings.");
                }
            }
            return cached;
        }
    }
}
