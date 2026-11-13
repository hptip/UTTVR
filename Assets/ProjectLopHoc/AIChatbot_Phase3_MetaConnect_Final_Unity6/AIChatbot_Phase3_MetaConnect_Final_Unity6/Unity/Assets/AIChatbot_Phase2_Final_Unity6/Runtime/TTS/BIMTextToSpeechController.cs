using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Điều phối Voice Output. KHÔNG bao giờ gọi XkiroBrain/Integration/Groq.
    /// Chỉ nhận câu trả lời AI đã hiển thị (AIChatbotUIController.onAiReplyAdded) hoặc yêu cầu từ nút "Đọc", rồi:
    /// làm sạch -> chia đoạn (không cắt nội dung) -> MetaConnectTTSClient -> BIMAudioPlayer.
    /// Lỗi TTS không bao giờ ảnh hưởng tới văn bản đã hiển thị.
    /// GIỮ tên class + GUID của Phase 2 để không mất tham chiếu trong Inspector/prefab/scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BIMAudioPlayer))]
    public sealed class BIMTextToSpeechController : MonoBehaviour
    {
        public static BIMTextToSpeechController Instance { get; private set; }

        [Header("Tham chiếu (để trống = tự tìm)")]
        [SerializeField] private AIChatbotUIController uiController;
        [SerializeField] private VoiceInputController voiceInput;

        [Header("Cấu hình (để trống = Resources/BIMTTSSettings)")]
        [SerializeField] private BIMTTSSettings settingsOverride;

        // ---- Cài đặt người dùng (PlayerPrefs, không chứa bí mật) ----
        private const string PrefAutoSpeak = "BIMTTS_AutoSpeak";
        private const string PrefLanguage = "BIMTTS_Language";
        private const string PrefAutoLang = "BIMTTS_AutoLanguage";
        private const string PrefVolume = "BIMTTS_Volume";
        private const string PrefSpeed = "BIMTTS_Speed";
        private const string PrefVoiceVi = "BIMTTS_VoiceVi";
        private const string PrefVoiceEn = "BIMTTS_VoiceEn";

        private BIMTTSSettings settings;
        public BIMTTSSettings Settings
        {
            get
            {
                if (settings == null) settings = settingsOverride != null ? settingsOverride : BIMTTSSettings.Load();
                return settings;
            }
        }

        public bool AutoSpeak
        {
            get => PlayerPrefs.GetInt(PrefAutoSpeak, Settings.autoSpeakDefault ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(PrefAutoSpeak, value ? 1 : 0); SettingsChanged?.Invoke(); }
        }

        /// <summary>
        /// Chế độ Auto: mỗi lần đọc tự chọn tts-vi / tts-en theo ngôn ngữ của chính tin nhắn đó (heuristic dấu tiếng Việt).
        /// Tắt = dùng đúng ngôn ngữ người dùng chọn thủ công (Language), KHÔNG tự đổi theo nội dung.
        /// </summary>
        public bool AutoLanguage
        {
            get => PlayerPrefs.GetInt(PrefAutoLang, Settings.defaultAutoLanguage ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(PrefAutoLang, value ? 1 : 0); SettingsChanged?.Invoke(); }
        }

        /// <summary>Ngôn ngữ chọn thủ công (cũng là ngôn ngữ của danh sách giọng đang hiển thị). Ở chế độ Auto luôn là Tiếng Việt.</summary>
        public BIMTtsLanguage Language
        {
            get => PlayerPrefs.GetInt(PrefLanguage, (int)Settings.defaultLanguage) == (int)BIMTtsLanguage.English
                ? BIMTtsLanguage.English : BIMTtsLanguage.Vietnamese;
            set { PlayerPrefs.SetInt(PrefLanguage, (int)value); SettingsChanged?.Invoke(); }
        }

        public float Volume
        {
            get => PlayerPrefs.GetFloat(PrefVolume, 0.8f);
            set { float v = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefVolume, v); if (player != null) player.Volume = v; SettingsChanged?.Invoke(); }
        }

        /// <summary>Tốc độ đọc, trong [minSpeed, maxSpeed] (mặc định 0.7-1.5), bước 0.05.</summary>
        public float Speed
        {
            get => ClampSpeed(PlayerPrefs.GetFloat(PrefSpeed, Settings.defaultSpeed));
            set { PlayerPrefs.SetFloat(PrefSpeed, ClampSpeed(value)); SettingsChanged?.Invoke(); }
        }

        private float ClampSpeed(float v) => Mathf.Round(Mathf.Clamp(v, Settings.minSpeed, Settings.maxSpeed) * 20f) / 20f;

        public IReadOnlyList<string> Voices => Settings.GetVoices(Language);

        /// <summary>Giọng đang chọn cho ngôn ngữ hiện tại; rỗng nếu ngôn ngữ chưa có giọng nào được cấu hình.</summary>
        public string CurrentVoice => VoiceFor(Language);

        /// <summary>Giọng đã lưu cho ngôn ngữ lang; không có/không hợp lệ thì dùng giọng mặc định đã cấu hình của đúng ngôn ngữ đó (không bao giờ mượn giọng ngôn ngữ khác).</summary>
        public string VoiceFor(BIMTtsLanguage lang)
        {
            var list = Settings.GetVoices(lang);
            string saved = PlayerPrefs.GetString(lang == BIMTtsLanguage.Vietnamese ? PrefVoiceVi : PrefVoiceEn, "");
            if (saved.Length > 0 && Has(list, saved)) return saved;
            return Settings.DefaultVoice(lang);
        }

        public string VoiceDisplay
        {
            get
            {
                var list = Voices;
                if (list.Count == 0) return "Chưa có giọng";
                return (AutoLanguage ? "Auto · " : "") + CurrentVoice + " (" + (IndexOf(list, CurrentVoice) + 1) + "/" + list.Count + ")";
            }
        }

        /// <summary>Vòng: Tự động → Tiếng Việt → English → Tự động.</summary>
        public void CycleLanguageMode()
        {
            if (AutoLanguage) { AutoLanguage = false; Language = BIMTtsLanguage.Vietnamese; }
            else if (Language == BIMTtsLanguage.Vietnamese) Language = BIMTtsLanguage.English;
            else { Language = BIMTtsLanguage.Vietnamese; AutoLanguage = true; }
        }

        public void ToggleLanguage() => CycleLanguageMode(); // giữ tương thích tên cũ

        /// <summary>direction = +1 (Next) hoặc -1 (Previous). Áp dụng cho danh sách giọng đang hiển thị.</summary>
        public void CycleVoice(int direction)
        {
            var list = Voices;
            if (list.Count == 0) return;
            int i = (IndexOf(list, CurrentVoice) + (direction >= 0 ? 1 : -1) + list.Count) % list.Count;
            PlayerPrefs.SetString(Language == BIMTtsLanguage.Vietnamese ? PrefVoiceVi : PrefVoiceEn, list[i]);
            SettingsChanged?.Invoke();
        }

        private static bool Has(IReadOnlyList<string> list, string v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return true;
            return false;
        }

        private static int IndexOf(IReadOnlyList<string> list, string v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return i;
            return 0;
        }

        public event Action SettingsChanged;
        /// <summary>(thông báo, là lỗi?). Chuỗi rỗng = xóa thông báo.</summary>
        public event Action<string, bool> StatusChanged;
        public event Action<bool> SpeakingChanged;

        public bool IsSpeaking => current != null;

        private IBIMTextToSpeechProvider provider;
        private BIMAudioPlayer player;
        private BIMSpeakButton current;
        private CancellationTokenSource cts;
        private int runId;
        private bool uiHooked;
        private bool voiceHooked;

        // Tạm dừng gọi dịch vụ sau lỗi xác thực / hết quota (không retry liên tục).
        private float blockedUntil;
        private string blockedMessage = "";

        // ---- Cache PCM16 theo MB (LRU) ----
        private sealed class CacheEntry
        {
            public string Key;
            public List<BIMTtsAudio> Chunks;
            public long Bytes;
            public LinkedListNode<CacheEntry> Node;
        }
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private readonly LinkedList<CacheEntry> lru = new LinkedList<CacheEntry>();
        private long cacheBytes;

        public long CacheBytes => cacheBytes;
        public bool HasCache(string key) => !string.IsNullOrEmpty(key) && cache.ContainsKey(key);

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            Instance = this;
            player = GetComponent<BIMAudioPlayer>();
            provider = new MetaConnectTTSClient(Settings);
            player.Volume = Volume;
        }

        private void OnEnable()
        {
            Instance = this;
            EnsureReferences();
            if (uiController != null && !uiHooked)
            {
                uiController.onAiReplyAdded += OnAiReplyAdded;
                uiController.onClosed.AddListener(Stop);        // đóng chatbot: dừng phát + hủy hàng đợi
                uiController.onMessageSent.AddListener(Stop);   // câu hỏi mới: dừng giọng cũ, không phát chồng
                uiHooked = true;
            }
            if (voiceInput != null && !voiceHooked)
            {
                voiceInput.StateChanged += OnVoiceInputStateChanged; // dừng TTS khi bắt đầu ghi âm
                voiceHooked = true;
            }
        }

        private void OnDisable()
        {
            if (uiController != null && uiHooked)
            {
                uiController.onAiReplyAdded -= OnAiReplyAdded;
                uiController.onClosed.RemoveListener(Stop);
                uiController.onMessageSent.RemoveListener(Stop);
            }
            uiHooked = false;
            if (voiceInput != null && voiceHooked) voiceInput.StateChanged -= OnVoiceInputStateChanged;
            voiceHooked = false;
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
            ClearCache();
            if (Instance == this) Instance = null;
        }

#if UNITY_ANDROID || UNITY_IOS
        /// <summary>Chỉ thiết bị di động: về nền thì dừng. Trên Windows PC Alt-Tab KHÔNG làm gián đoạn giọng đọc.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) Stop();
        }
#endif

        private void EnsureReferences()
        {
            if (uiController == null) uiController = GetComponent<AIChatbotUIController>();
            if (uiController == null) uiController = GetComponentInParent<AIChatbotUIController>(true);
            if (uiController == null) uiController = FindFirstObjectByType<AIChatbotUIController>(FindObjectsInactive.Include);
            if (voiceInput == null && uiController != null) voiceInput = uiController.GetComponent<VoiceInputController>();
            if (voiceInput == null) voiceInput = FindFirstObjectByType<VoiceInputController>(FindObjectsInactive.Include);
        }

        // ------------------------------------------------------------------ sự kiện từ UI / Voice Input

        private void OnAiReplyAdded(ChatBubble bubble, string text)
        {
            if (bubble == null) return;
            var button = bubble.GetComponentInChildren<BIMSpeakButton>(true);
            if (button == null) return; // prefab chưa có nút Đọc: bỏ qua, chat vẫn bình thường
            if (!button.Bind(text)) return; // đã gắn đúng nội dung này rồi: chống sự kiện lặp
            if (AutoSpeak && button.TryMarkAutoSpoken()) Speak(button);
        }

        private void OnVoiceInputStateChanged(VoiceInputController.VoiceState state, string message)
        {
            if (state == VoiceInputController.VoiceState.Starting || state == VoiceInputController.VoiceState.Recording)
                Stop();
        }

        // ------------------------------------------------------------------ điều khiển công khai

        /// <summary>Nhấn nút Đọc: đang tạo/phát tin này thì Dừng, ngược lại Đọc (hoặc Đọc lại từ cache).</summary>
        public void Toggle(BIMSpeakButton source)
        {
            if (source == null) return;
            if (source == current) { Stop(); return; }
            Speak(source);
        }

        public void Speak(BIMSpeakButton source)
        {
            if (source == null) return;
            var s = Settings;

            StopInternal(); // chỉ MỘT tin nhắn được đọc tại một thời điểm; tin đang đọc bị thay thế

            if (Time.realtimeSinceStartup < blockedUntil) { Fail(source, blockedMessage); return; }

            string problem = s.ProxyProblem;
            if (problem != null) { Fail(source, problem); return; }

            string clean = BIMTextSanitizer.Sanitize(source.Text);
            if (clean.Length == 0)
            {
                source.SetState(BIMSpeakButton.SpeakState.Idle);
                Report("Không có nội dung để đọc.", true);
                return;
            }

            // Auto: chọn model/giọng theo chính nội dung tin nhắn; thủ công: đúng lựa chọn của người dùng.
            var lang = AutoLanguage
                ? (BIMTextSanitizer.DetectLanguage(clean) == "vi" ? BIMTtsLanguage.Vietnamese : BIMTtsLanguage.English)
                : Language;
            string voice = VoiceFor(lang);
            if (string.IsNullOrEmpty(voice))
            {
                Fail(source, lang == BIMTtsLanguage.English
                    ? "Chưa cấu hình giọng tiếng Anh nên không đọc được câu tiếng Anh. Xem Voice_Output_Guide.md (mục Giọng tiếng Anh) rồi điền BIMTTSSettings > English Voices."
                    : "Chưa có giọng tiếng Việt trong BIMTTSSettings.");
                return;
            }

            string model = s.ModelFor(lang);
            if (model.Length == 0) { Fail(source, "Chưa cấu hình model TTS trong BIMTTSSettings."); return; }

            float speed = Speed;
            string key = BuildKey(lang, model, voice, speed, clean);
            source.LastCacheKey = key;

            // Chỉ là gợi ý (KHÔNG tự đổi ngôn ngữ): chọn English nhưng văn bản có dấu tiếng Việt.
            string hint = (!AutoLanguage && lang == BIMTtsLanguage.English && BIMTextSanitizer.DetectLanguage(clean) == "vi")
                ? "(Lưu ý: đang chọn English nhưng văn bản có vẻ là tiếng Việt.)" : "";

            var options = new BIMTtsOptions
            {
                Language = lang,
                Model = model,
                Voice = voice,
                Speed = speed,
                TimeoutSeconds = s.timeoutSeconds
            };

            current = source;
            int id = ++runId;
            cts = new CancellationTokenSource();
            _ = RunAsync(id, cts.Token, source, clean, options, key, hint);
        }

        /// <summary>Dừng ngay: hủy request đang tạo + hàng đợi, dừng âm thanh. Gọi nhiều lần vẫn an toàn.</summary>
        public void Stop()
        {
            StopInternal();
            Report("", false);
        }

        public void NotifyButtonDestroyed(BIMSpeakButton button)
        {
            if (button == null) return;
            if (button == current) StopInternal();
        }

        public void ClearCache()
        {
            cache.Clear();
            lru.Clear();
            cacheBytes = 0;
        }

        // ------------------------------------------------------------------ nội bộ

        private void StopInternal()
        {
            runId++; // mọi tác vụ đang chạy trở thành "cũ", không được cập nhật UI nữa
            if (cts != null)
            {
                var old = cts;
                cts = null;
                try { old.Cancel(); } catch (Exception) { }
            }
            if (player != null) player.Stop();
            if (current != null)
            {
                var b = current;
                current = null;
                if (b != null) b.SetState(HasCache(b.LastCacheKey) ? BIMSpeakButton.SpeakState.Ready : BIMSpeakButton.SpeakState.Idle);
                SpeakingChanged?.Invoke(false);
            }
        }

        private bool IsCurrent(int id) => id == runId && this != null;

        private async Task RunAsync(int id, CancellationToken token, BIMSpeakButton source, string text, BIMTtsOptions options, string key, string hint)
        {
            var s = Settings;
            SpeakingChanged?.Invoke(true);
            bool completed = false;
            try
            {
                CacheEntry hit;
                if (cache.TryGetValue(key, out hit))
                {
                    // Replay: dùng lại âm thanh trong cache, KHÔNG gọi dịch vụ.
                    Touch(hit);
                    source.SetState(BIMSpeakButton.SpeakState.Playing);
                    Report("", false);
                    var cachedChunks = hit.Chunks;
                    for (int i = 0; i < cachedChunks.Count; i++)
                    {
                        await player.PlayAsync(cachedChunks[i], token);
                        if (!IsCurrent(id)) return;
                    }
                    completed = true;
                }
                else
                {
                    source.SetState(BIMSpeakButton.SpeakState.Generating);
                    Report(hint.Length == 0 ? "Đang tạo âm thanh…" : "Đang tạo âm thanh… " + hint, false);

                    // Chia đoạn: KHÔNG cắt bỏ nội dung. Mọi đoạn được đọc tuần tự theo đúng thứ tự.
                    var parts = BIMTextSanitizer.SplitIntoChunks(text, s.firstChunkChars, s.chunkChars);
                    if (parts.Count == 0) throw new BIMTtsException(BIMTtsErrorCode.BadRequest, "Không có nội dung để đọc.");

                    var tasks = new Task<BIMTtsAudio>[parts.Count];
                    int started = 0;
                    long budget = s.CacheBudgetBytes;
                    long keptBytes = 0;
                    bool retain = true;
                    var kept = new List<BIMTtsAudio>(parts.Count);

                    for (int i = 0; i < parts.Count; i++)
                    {
                        // Xếp hàng sẵn đoạn kế tiếp trong lúc đoạn hiện tại phát; số request ĐỒNG THỜI do client giới hạn (mặc định 1).
                        int upTo = Mathf.Min(i + 1, parts.Count - 1);
                        while (started <= upTo)
                        {
                            tasks[started] = provider.GenerateAudioAsync(parts[started], options, token);
                            ObserveFault(tasks[started]);
                            started++;
                        }

                        BIMTtsAudio audio = await tasks[i];
                        tasks[i] = null;
                        if (!IsCurrent(id)) return;

                        if (i == 0)
                        {
                            source.SetState(BIMSpeakButton.SpeakState.Playing);
                            Report("", false);
                        }
                        await player.PlayAsync(audio, token);
                        if (!IsCurrent(id)) return;

                        // Chỉ giữ để Replay nếu cả câu trả lời nằm trong ngân sách; vượt thì thôi giữ (RAM không vượt ngân sách).
                        if (retain)
                        {
                            keptBytes += audio.SizeBytes;
                            if (keptBytes > budget) { retain = false; kept.Clear(); }
                            else kept.Add(audio);
                        }
                    }

                    completed = true;
                    if (retain && kept.Count > 0) StoreCache(key, kept, keptBytes);
                }

                if (IsCurrent(id) && completed) source.SetState(BIMSpeakButton.SpeakState.Ready);
            }
            catch (OperationCanceledException)
            {
                // Người dùng bấm Dừng / đóng chatbot / bắt đầu ghi âm / Pause: không phải lỗi.
            }
            catch (BIMTtsException ex)
            {
                if (IsCurrent(id)) HandleError(source, ex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BIM TTS] " + ex.GetType().Name); // không ghi ex.Message: có thể chứa dữ liệu hội thoại
                if (IsCurrent(id)) Fail(source, "Không thể tạo âm thanh, vui lòng thử lại.");
            }
            finally
            {
                if (IsCurrent(id))
                {
                    current = null;
                    SpeakingChanged?.Invoke(false);
                }
            }
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void HandleError(BIMSpeakButton source, BIMTtsException ex)
        {
            string msg = UserMessage(ex);
            if (ex.Code == BIMTtsErrorCode.Unauthorized || ex.Code == BIMTtsErrorCode.QuotaExceeded)
            {
                blockedUntil = Time.realtimeSinceStartup + Settings.blockSecondsAfterAuthOrQuota;
                blockedMessage = msg;
            }
            Fail(source, msg);
        }

        private void Fail(BIMSpeakButton source, string message)
        {
            if (source != null) source.SetState(BIMSpeakButton.SpeakState.Error);
            Report(message, true);
        }

        private void Report(string message, bool isError) => StatusChanged?.Invoke(message ?? "", isError);

        // ------------------------------------------------------------------ cache

        private static string BuildKey(BIMTtsLanguage lang, string model, string voice, float speed, string text)
        {
            ulong h = 14695981039346656037UL; // FNV-1a 64-bit (ổn định giữa các lần chạy)
            unchecked
            {
                for (int i = 0; i < text.Length; i++) { h ^= text[i]; h *= 1099511628211UL; }
            }
            return (lang == BIMTtsLanguage.English ? "en" : "vi") + "|" + model + "|" + voice + "|" + speed.ToString("0.00", CultureInfo.InvariantCulture) + "|wav|" + text.Length + "|" + h.ToString("x16");
        }

        private void Touch(CacheEntry e)
        {
            if (e.Node == null) return;
            lru.Remove(e.Node);
            lru.AddLast(e.Node);
        }

        private void StoreCache(string key, List<BIMTtsAudio> chunks, long bytes)
        {
            CacheEntry old;
            if (cache.TryGetValue(key, out old)) RemoveEntry(old);

            var e = new CacheEntry { Key = key, Chunks = chunks, Bytes = bytes };
            e.Node = lru.AddLast(e);
            cache[key] = e;
            cacheBytes += bytes;

            long budget = Settings.CacheBudgetBytes;
            while (cacheBytes > budget && lru.First != null && lru.First.Value != e) RemoveEntry(lru.First.Value);
        }

        private void RemoveEntry(CacheEntry e)
        {
            if (e.Node != null) lru.Remove(e.Node);
            cache.Remove(e.Key);
            cacheBytes -= e.Bytes;
        }

        /// <summary>Thông báo dễ hiểu cho người dùng; không lộ chi tiết nội bộ.</summary>
        private static string UserMessage(BIMTtsException ex)
        {
            switch (ex.Code)
            {
                case BIMTtsErrorCode.NotConfigured: return ex.Message;
                case BIMTtsErrorCode.Unauthorized: return "Lỗi xác thực dịch vụ giọng nói (App Key hoặc khóa backend). Kiểm tra BIMTTSSettings và backend.";
                case BIMTtsErrorCode.QuotaExceeded: return "Đã hết quota giọng nói. Văn bản vẫn dùng bình thường.";
                case BIMTtsErrorCode.RateLimited: return "Dịch vụ giọng nói đang bận, vui lòng thử lại sau ít giây.";
                case BIMTtsErrorCode.ConcurrencyLimited: return "Dịch vụ giọng nói đang xử lý quá nhiều yêu cầu, vui lòng thử lại.";
                case BIMTtsErrorCode.Network: return "Mất kết nối tới dịch vụ giọng nói. Văn bản vẫn dùng bình thường.";
                case BIMTtsErrorCode.Timeout: return "Dịch vụ giọng nói không phản hồi kịp.";
                case BIMTtsErrorCode.ServerError: return "Dịch vụ giọng nói đang gặp sự cố, vui lòng thử lại sau.";
                case BIMTtsErrorCode.VoiceNotFound: return "Giọng đọc không khả dụng. Kiểm tra tên giọng trong BIMTTSSettings.";
                case BIMTtsErrorCode.BadRequest: return "Yêu cầu giọng nói không hợp lệ (nội dung hoặc tham số).";
                case BIMTtsErrorCode.PayloadTooLarge: return "Đoạn văn vượt giới hạn dịch vụ giọng nói.";
                case BIMTtsErrorCode.InvalidAudio: return "Âm thanh nhận về không hợp lệ, không phát.";
                default: return "Không thể tạo âm thanh, vui lòng thử lại.";
            }
        }
    }
}
