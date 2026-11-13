using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BIM4D.AI.Speech
{
    /// <summary>Phân loại lỗi TTS để UI hiển thị thông báo dễ hiểu (không lộ chi tiết nội bộ).</summary>
    public enum BIMTtsErrorCode
    {
        Unknown = 0,
        NotConfigured,
        Unauthorized,       // 401/403, App Key sai, backend thiếu khóa MetaConnect
        BadRequest,         // 400
        PayloadTooLarge,    // 413
        VoiceNotFound,      // giọng không tồn tại / chưa cấu hình
        RateLimited,        // rate_limit_exceeded (429)
        QuotaExceeded,      // quota_exceeded (429) - KHÔNG retry
        ConcurrencyLimited, // concurrency_limit_exceeded (429)
        Timeout,
        Network,
        ServerError,        // 5xx
        InvalidAudio
    }

    public enum BIMTtsLanguage { Vietnamese = 1, English = 2 }

    /// <summary>Lỗi TTS có mã. Message tiếng Việt, KHÔNG chứa khóa API hay nội dung hội thoại.</summary>
    public sealed class BIMTtsException : Exception
    {
        public BIMTtsErrorCode Code { get; }
        public float RetryAfterSeconds { get; }
        public long HttpStatus { get; }
        public string RequestId { get; }

        public BIMTtsException(BIMTtsErrorCode code, string message, float retryAfterSeconds = 0f, long httpStatus = 0, string requestId = null)
            : base(message)
        {
            Code = code;
            RetryAfterSeconds = retryAfterSeconds;
            HttpStatus = httpStatus;
            RequestId = requestId;
        }
    }

    /// <summary>Âm thanh PCM16 little-endian đã kiểm tra header. Giữ dạng byte (nhỏ hơn float32 một nửa) để cache nhẹ.</summary>
    public sealed class BIMTtsAudio
    {
        public byte[] Pcm16;
        public int SampleRate;
        public int Channels;

        public long SizeBytes => Pcm16 == null ? 0 : Pcm16.LongLength;
        public double DurationSeconds =>
            (Pcm16 == null || SampleRate <= 0 || Channels <= 0) ? 0 : Pcm16.Length / (2.0 * Channels * SampleRate);
    }

    public sealed class BIMTtsOptions
    {
        public BIMTtsLanguage Language = BIMTtsLanguage.Vietnamese;
        public string Model;
        public string Voice;
        /// <summary>Tốc độ đọc (tham số `speed` của MetaConnect, 0.25-4.0; app giới hạn 0.7-1.5).</summary>
        public float Speed = 1f;
        public int TimeoutSeconds = 30;
    }

    public sealed class BIMVoiceInfo
    {
        public string Name;
        public string Language;
        public string Gender;
    }

    /// <summary>Hợp đồng chung cho nhà cung cấp TTS. Controller chỉ biết interface này.</summary>
    public interface IBIMTextToSpeechProvider
    {
        string Name { get; }
        /// <summary>Đã có đủ cấu hình (proxy URL...) để gọi dịch vụ chưa.</summary>
        bool IsConfigured { get; }
        Task<BIMTtsAudio> GenerateAudioAsync(string text, BIMTtsOptions options, CancellationToken ct);
        Task<List<BIMVoiceInfo>> GetAvailableVoicesAsync(CancellationToken ct);
    }
}
