using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Gọi backend proxy (Cloudflare Worker) -> MetaConnect POST /v1/audio/speech. Unity KHÔNG BAO GIỜ giữ khóa MetaConnect.
    /// Hợp đồng với proxy: POST {proxy}/v1/tts  JSON {text, lang, model, voice, speed}  + header X-App-Key  -> audio/wav.
    /// Lỗi trả theo envelope MetaConnect: {"error":{"code","message","retry_after_seconds"}}.
    /// Chỉ dùng UnityWebRequest (Editor/Windows); WAV được kiểm tra bằng BIMWavParser ở thread nền.
    /// </summary>
    public sealed class MetaConnectTTSClient : IBIMTextToSpeechProvider
    {
        [Serializable] public sealed class TtsRequestDto { public string text; public string lang; public string model; public string voice; public float speed; }
        [Serializable] public sealed class ErrorBodyDto { public string code; public string message; public float retry_after_seconds; }
        [Serializable] public sealed class ErrorEnvelopeDto { public ErrorBodyDto error; }
        [Serializable] public sealed class VoiceDto { public string name; public string lang; public string gender; }
        [Serializable] public sealed class VoicesDto { public VoiceDto[] voices; }

        private readonly BIMTTSSettings settings;
        private readonly SemaphoreSlim gate;

        public MetaConnectTTSClient(BIMTTSSettings settings)
        {
            this.settings = settings;
            gate = new SemaphoreSlim(Mathf.Clamp(settings != null ? settings.maxConcurrentRequests : 1, 1, 2));
        }

        public string Name => "MetaConnect (qua backend proxy)";
        public bool IsConfigured => settings != null && settings.IsProxyConfigured;

        // ------------------------------------------------------------------ tạo âm thanh

        public async Task<BIMTtsAudio> GenerateAudioAsync(string text, BIMTtsOptions options, CancellationToken ct)
        {
            if (!IsConfigured)
                throw new BIMTtsException(BIMTtsErrorCode.NotConfigured, settings != null ? (settings.ProxyProblem ?? "Chưa cấu hình backend TTS.") : "Chưa cấu hình backend TTS.");
            if (string.IsNullOrWhiteSpace(text))
                throw new BIMTtsException(BIMTtsErrorCode.BadRequest, "Không có nội dung để đọc.");
            if (options == null || string.IsNullOrWhiteSpace(options.Voice))
                throw new BIMTtsException(BIMTtsErrorCode.VoiceNotFound, "Chưa chọn giọng đọc.");

            int attempt = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await SendOnceAsync(text, options, ct);
                }
                catch (BIMTtsException ex) when (IsRetryable(ex.Code) && attempt < settings.maxRetries)
                {
                    attempt++;
                    float wait = ex.RetryAfterSeconds > 0f ? Mathf.Min(ex.RetryAfterSeconds, 15f) : (attempt == 1 ? 1f : 3f);
                    await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                }
            }
        }

        private static bool IsRetryable(BIMTtsErrorCode c)
        {
            // Không retry: Unauthorized, BadRequest, PayloadTooLarge, VoiceNotFound, QuotaExceeded, InvalidAudio, NotConfigured.
            return c == BIMTtsErrorCode.RateLimited || c == BIMTtsErrorCode.ConcurrencyLimited ||
                   c == BIMTtsErrorCode.Timeout || c == BIMTtsErrorCode.Network || c == BIMTtsErrorCode.ServerError;
        }

        private async Task<BIMTtsAudio> SendOnceAsync(string text, BIMTtsOptions options, CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try
            {
                var dto = new TtsRequestDto
                {
                    text = text,
                    lang = options.Language == BIMTtsLanguage.English ? "en" : "vi",
                    model = options.Model ?? "",
                    voice = options.Voice,
                    speed = options.Speed
                };
                byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(dto));

                using (var req = new UnityWebRequest(settings.ProxyBaseUrl + "/v1/tts", UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Accept", "audio/wav");
                    if (!string.IsNullOrEmpty(settings.appKey)) req.SetRequestHeader("X-App-Key", settings.appKey.Trim());
                    req.timeout = Mathf.Max(5, options.TimeoutSeconds);

                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        if (ct.IsCancellationRequested) { req.Abort(); ct.ThrowIfCancellationRequested(); }
                        await Task.Yield();
                    }
                    ct.ThrowIfCancellationRequested();

                    if (req.result != UnityWebRequest.Result.Success) throw MapError(req);

                    byte[] data = req.downloadHandler.data;
                    string requestId = req.GetResponseHeader("X-Request-Id");
                    var parsed = await Task.Run(() =>
                    {
                        BIMTtsAudio a; string e;
                        bool ok = BIMWavParser.TryParse(data, out a, out e);
                        return (ok, a, e);
                    });
                    ct.ThrowIfCancellationRequested();
                    if (!parsed.ok)
                        throw new BIMTtsException(BIMTtsErrorCode.InvalidAudio, "Âm thanh nhận về không hợp lệ: " + parsed.e, 0f, req.responseCode, requestId);
                    return parsed.a;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        // ------------------------------------------------------------------ danh sách giọng (chẩn đoán)

        public async Task<List<BIMVoiceInfo>> GetAvailableVoicesAsync(CancellationToken ct)
        {
            if (!IsConfigured) throw new BIMTtsException(BIMTtsErrorCode.NotConfigured, settings.ProxyProblem ?? "Chưa cấu hình backend TTS.");
            using (var req = UnityWebRequest.Get(settings.ProxyBaseUrl + "/v1/tts/voices"))
            {
                if (!string.IsNullOrEmpty(settings.appKey)) req.SetRequestHeader("X-App-Key", settings.appKey.Trim());
                req.timeout = Mathf.Max(5, settings.timeoutSeconds);
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested) { req.Abort(); ct.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
                if (req.result != UnityWebRequest.Result.Success) throw MapError(req);
                var list = new List<BIMVoiceInfo>();
                try
                {
                    var dto = JsonUtility.FromJson<VoicesDto>(req.downloadHandler.text);
                    if (dto != null && dto.voices != null)
                        foreach (var v in dto.voices) list.Add(new BIMVoiceInfo { Name = v.name, Language = v.lang, Gender = v.gender });
                }
                catch (Exception) { throw new BIMTtsException(BIMTtsErrorCode.Unknown, "Không đọc được danh sách giọng."); }
                return list;
            }
        }

        // ------------------------------------------------------------------ ánh xạ lỗi

        private static BIMTtsException MapError(UnityWebRequest req)
        {
            long status = req.responseCode;
            string requestId = req.GetResponseHeader("X-Request-Id");

            string code = "";
            float retry = 0f;
            try
            {
                string body = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(body) && body.TrimStart().StartsWith("{"))
                {
                    var env = JsonUtility.FromJson<ErrorEnvelopeDto>(body);
                    if (env != null && env.error != null) { code = env.error.code ?? ""; retry = env.error.retry_after_seconds; }
                }
            }
            catch (Exception) { /* body không phải JSON lỗi: dựa vào mã HTTP */ }

            if (retry <= 0f)
            {
                string h = req.GetResponseHeader("Retry-After");
                float parsed;
                if (!string.IsNullOrEmpty(h) && float.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) retry = parsed;
            }

            // Chỉ ghi mã/trạng thái/request id - không ghi nội dung hội thoại hay khóa.
            Debug.LogWarning("[BIM TTS] Lỗi dịch vụ: HTTP " + status + (code.Length > 0 ? " " + code : "") + (string.IsNullOrEmpty(requestId) ? "" : " (request " + requestId + ")"));

            if (req.result == UnityWebRequest.Result.ConnectionError || status == 0)
            {
                string err = (req.error ?? "").ToLowerInvariant();
                bool timeout = err.Contains("timeout") || err.Contains("timed out");
                return new BIMTtsException(timeout ? BIMTtsErrorCode.Timeout : BIMTtsErrorCode.Network,
                    timeout ? "Hết thời gian chờ dịch vụ giọng nói." : "Không thể kết nối dịch vụ giọng nói.", 0f, status, requestId);
            }

            switch (code)
            {
                case "quota_exceeded": return new BIMTtsException(BIMTtsErrorCode.QuotaExceeded, "Đã hết quota TTS.", retry, status, requestId);
                case "rate_limit_exceeded": return new BIMTtsException(BIMTtsErrorCode.RateLimited, "Dịch vụ đang giới hạn tốc độ.", retry, status, requestId);
                case "concurrency_limit_exceeded": return new BIMTtsException(BIMTtsErrorCode.ConcurrencyLimited, "Quá nhiều yêu cầu đồng thời.", retry, status, requestId);
                case "voice_not_found": return new BIMTtsException(BIMTtsErrorCode.VoiceNotFound, "Giọng đọc không tồn tại.", 0f, status, requestId);
                case "payload_too_large": return new BIMTtsException(BIMTtsErrorCode.PayloadTooLarge, "Nội dung vượt giới hạn.", 0f, status, requestId);
                case "authentication_failed":
                case "app_key_invalid":
                case "api_key_expired":
                case "api_key_revoked":
                case "insufficient_scope":
                case "permission_denied":
                case "upstream_auth_error":
                    return new BIMTtsException(BIMTtsErrorCode.Unauthorized, "Lỗi xác thực dịch vụ.", 0f, status, requestId);
                case "request_timeout": return new BIMTtsException(BIMTtsErrorCode.Timeout, "Hết thời gian chờ dịch vụ giọng nói.", 0f, status, requestId);
            }

            if (status == 401 || status == 403) return new BIMTtsException(BIMTtsErrorCode.Unauthorized, "Lỗi xác thực dịch vụ.", 0f, status, requestId);
            if (status == 413) return new BIMTtsException(BIMTtsErrorCode.PayloadTooLarge, "Nội dung vượt giới hạn.", 0f, status, requestId);
            if (status == 400 || status == 404 || status == 422) return new BIMTtsException(BIMTtsErrorCode.BadRequest, "Yêu cầu không hợp lệ.", 0f, status, requestId);
            if (status == 408 || status == 504) return new BIMTtsException(BIMTtsErrorCode.Timeout, "Hết thời gian chờ dịch vụ giọng nói.", 0f, status, requestId);
            if (status == 429) return new BIMTtsException(BIMTtsErrorCode.RateLimited, "Dịch vụ đang giới hạn tốc độ.", retry, status, requestId);
            if (status >= 500) return new BIMTtsException(BIMTtsErrorCode.ServerError, "Dịch vụ giọng nói đang lỗi.", retry, status, requestId);
            return new BIMTtsException(BIMTtsErrorCode.Unknown, "Không thể tạo âm thanh.", 0f, status, requestId);
        }
    }
}
