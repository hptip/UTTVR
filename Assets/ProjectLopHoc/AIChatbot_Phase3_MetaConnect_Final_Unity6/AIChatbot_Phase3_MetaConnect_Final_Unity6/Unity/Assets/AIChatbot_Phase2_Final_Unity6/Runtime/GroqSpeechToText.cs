using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using BIM4D.AI.Speech;

namespace BIM4D.AI.Generative
{
    /// <summary>Phân loại lỗi nhận diện giọng nói để UI hiển thị thông báo phù hợp.</summary>
    public enum SpeechErrorCode
    {
        Unknown = 0,
        Busy,
        NotInPlayMode,
        PermissionDenied,
        NoMicrophone,
        MicrophoneUnavailable,
        MicrophoneStartFailed,
        NoAudio,
        SilentAudio,
        MissingApiKey,
        InvalidApiKey,
        RateLimited,
        Network,
        Timeout,
        EmptyTranscript
    }

    /// <summary>Lỗi có mã phân loại. Message luôn là tiếng Việt, sẵn sàng hiển thị cho người dùng.</summary>
    public sealed class SpeechRecognitionException : InvalidOperationException
    {
        public SpeechErrorCode Code { get; }
        public SpeechRecognitionException(SpeechErrorCode code, string message) : base(message) { Code = code; }
    }

    [DisallowMultipleComponent]
    public sealed class GroqSpeechToText : MonoBehaviour
    {
        public enum SpeechLanguage
        {
            English = 0,
            Vietnamese = 1,
            Auto = 2
        }

        // Gọi Groq qua backend proxy (Cloudflare Worker): URL + App Key lấy từ Resources/BIMTTSSettings.asset.
        // Khóa Groq chỉ nằm ở secret GROQ_API_KEY của Worker, không có trong Unity/APK.
        [SerializeField] private string model = "whisper-large-v3-turbo";
        [SerializeField] private SpeechLanguage language = SpeechLanguage.Vietnamese;
        [SerializeField, TextArea(1, 3)] private string transcriptionPrompt = "BIM, Revit, IFC, openBIM, Navisworks, Tekla, Civil 3D, clash detection, LOD, LOI.";
        [SerializeField, Min(1)] private int timeoutSeconds = 60;
        [Tooltip("Use Unity's default microphone instead of the selected device. Changes apply to the next recording.")]
        [SerializeField] private bool useDefaultMicrophone = true;
        [SerializeField] private string microphoneDevice = "";
        [SerializeField, Range(1, 120)] private int maxRecordingSeconds = 30;
        [SerializeField, Min(8000)] private int sampleRate = 16000;
        [Tooltip("Biên độ đỉnh (0..1) tối thiểu của bản ghi. Thấp hơn = coi là im lặng và KHÔNG gửi lên Groq.")]
        [SerializeField, Range(0f, 0.2f)] private float silencePeakThreshold = 0.02f;
        [SerializeField] private UnityEvent<string> onTranscribed = new UnityEvent<string>();
        [SerializeField] private UnityEvent<string> onError = new UnityEvent<string>();

        private AudioClip recording;
        private string activeDevice;
        private float recordingStartedAt;
        private bool startingRecording;

        public UnityEvent<string> OnTranscribed => onTranscribed;
        public UnityEvent<string> OnError => onError;
        public string LanguageCode => language == SpeechLanguage.Vietnamese ? "vi" : language == SpeechLanguage.English ? "en" : "";
        public float MaxRecordingSeconds => maxRecordingSeconds;
        public float RecordingElapsed => recording == null ? 0f : Mathf.Max(0f, Time.realtimeSinceStartup - recordingStartedAt);
        public float RecordingRemaining => Mathf.Max(0f, maxRecordingSeconds - RecordingElapsed);
        public bool IsStarting => startingRecording;
        public float SilencePeakThreshold => silencePeakThreshold;
        /// <summary>Biên độ đỉnh (0..1) của bản ghi gần nhất, đo trước khi gửi.</summary>
        public float LastRecordingPeak { get; private set; }

        public void SetLanguage(SpeechLanguage value) => language = value;
        public void SetTranscriptionPrompt(string value) => transcriptionPrompt = value ?? "";

        /// <summary>Approximate current microphone level (0..1). Intended for a lightweight UI meter.</summary>
        public float GetCurrentLevel()
        {
            if (recording == null) return 0f;
            int position = Microphone.GetPosition(activeDevice);
            if (position <= 0) return 0f;
            int count = Mathf.Min(256, position);
            var samples = new float[count * recording.channels];
            int offset = Mathf.Max(0, position - count);
            if (!recording.GetData(samples, offset)) return 0f;
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return Mathf.Clamp01((float)Math.Sqrt(sum / Math.Max(1, samples.Length)) * 5f);
        }

        /// <summary>Immediately ends and discards an active recording without uploading it.</summary>
        public void CancelRecording()
        {
            if (recording == null) return;
            Microphone.End(activeDevice);
            Destroy(recording);
            recording = null;
            activeDevice = null;
        }
        public bool UseDefaultMicrophone => useDefaultMicrophone;
        public bool IsRecording => recording != null;
        public bool IsTranscribing { get; private set; }
        public string LastTranscript { get; private set; } = "";

        /// <summary>Returns currently available microphone names for a settings dropdown.</summary>
        public string[] GetMicrophoneDevices() => Microphone.devices;

        /// <summary>Returns the selected device name, or an empty string when using the default microphone.</summary>
        public string GetMicrophoneDevice() => useDefaultMicrophone ? string.Empty : microphoneDevice;

        /// <summary>Selects a microphone for the next recording. Null/empty selects the default microphone.</summary>
        public void SetMicrophoneDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                useDefaultMicrophone = true;
                return;
            }
            if (Array.IndexOf(GetMicrophoneDevices(), deviceName) < 0)
                throw new ArgumentException("The selected microphone is unavailable.", nameof(deviceName));

            microphoneDevice = deviceName;
            useDefaultMicrophone = false;
        }

        /// <summary>Overrides the selected device with the default microphone on the next recording.</summary>
        public void SetUseDefaultMicrophone(bool useDefault) => useDefaultMicrophone = useDefault;

        /// <summary>Starts a non-looping microphone recording. Call StopRecording to submit it.</summary>
        [ContextMenu("Start Recording (Play Mode)")]
        public async void StartRecording()
        {
            try { await StartRecordingAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportError(exception); }
        }

        public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
        {
            if (!Application.isPlaying) throw new SpeechRecognitionException(SpeechErrorCode.NotInPlayMode, "Chỉ ghi âm được khi đang ở Play Mode.");
            if (startingRecording || IsRecording || IsTranscribing)
                throw new SpeechRecognitionException(SpeechErrorCode.Busy, "Hệ thống ghi âm/nhận diện đang bận. Vui lòng thử lại sau giây lát.");

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var token = lifetime.Token;
            startingRecording = true;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                {
                    var permission = new TaskCompletionSource<bool>();
                    var callbacks = new UnityEngine.Android.PermissionCallbacks();
                    callbacks.PermissionGranted += _ => permission.TrySetResult(true);
                    callbacks.PermissionDenied += _ => permission.TrySetResult(false);
                    callbacks.PermissionDeniedAndDontAskAgain += _ => permission.TrySetResult(false);
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
                    while (!permission.Task.IsCompleted) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                    if (!await permission.Task) throw new SpeechRecognitionException(SpeechErrorCode.PermissionDenied, "Chưa được cấp quyền sử dụng microphone.");
                }
#else
                var permission = Application.RequestUserAuthorization(UserAuthorization.Microphone);
                while (!permission.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                    throw new SpeechRecognitionException(SpeechErrorCode.PermissionDenied, "Chưa được cấp quyền sử dụng microphone.");
#endif
                token.ThrowIfCancellationRequested();
                if (!isActiveAndEnabled) throw new OperationCanceledException();
                var devices = GetMicrophoneDevices();
                if (devices.Length == 0) throw new SpeechRecognitionException(SpeechErrorCode.NoMicrophone, "Không tìm thấy microphone. Hãy kiểm tra thiết bị đầu vào.");
                // A null device name tells Unity to use the default microphone, which need not be devices[0].
                // Keep the active device separate so settings changes cannot redirect StopRecording or cleanup.
                activeDevice = useDefaultMicrophone ? null : microphoneDevice;
                if (!useDefaultMicrophone && Array.IndexOf(devices, activeDevice) < 0)
                    throw new SpeechRecognitionException(SpeechErrorCode.MicrophoneUnavailable, "Microphone đã chọn không khả dụng. Hãy chọn thiết bị khác hoặc bật Use Default Microphone.");
                recording = Microphone.Start(activeDevice, false, maxRecordingSeconds, sampleRate);
                recordingStartedAt = Time.realtimeSinceStartup;
                if (recording == null) throw new SpeechRecognitionException(SpeechErrorCode.MicrophoneStartFailed, "Không thể khởi động microphone. Thiết bị có thể đang được ứng dụng khác sử dụng.");
            }
            finally { startingRecording = false; }
        }

        [ContextMenu("Stop Recording and Transcribe (Play Mode)")]
        public async void StopRecording()
        {
            try { await StopRecordingAndTranscribeAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportError(exception); }
        }

        public async Task<string> StopRecordingAndTranscribeAsync(CancellationToken cancellationToken = default)
        {
            if (recording == null) throw new SpeechRecognitionException(SpeechErrorCode.NoAudio, "Không có bản ghi âm nào để nhận diện.");
            var clip = recording;
            int frames = Microphone.GetPosition(activeDevice);
            // A non-looping microphone returns position zero once its buffer is full.
            if (frames <= 0 && Time.realtimeSinceStartup - recordingStartedAt >= clip.length)
                frames = clip.samples;
            Microphone.End(activeDevice);
            recording = null;
            try
            {
                if (frames <= 0)
                    throw new SpeechRecognitionException(SpeechErrorCode.NoAudio, "Không có dữ liệu âm thanh. Hãy kiểm tra microphone.");
                // Đo mức âm thật của bản ghi TRƯỚC khi gửi: không gửi audio im lặng lên Groq
                // (Whisper có thể 'bịa' transcript hoặc lặp lại prompt khi gặp im lặng).
                LastRecordingPeak = ComputePeak(clip, frames);
                if (LastRecordingPeak < silencePeakThreshold)
                    throw new SpeechRecognitionException(SpeechErrorCode.SilentAudio,
                        "Không nghe thấy tiếng. Hãy kiểm tra microphone và quyền truy cập.");
                byte[] wav = EncodeWav(clip, frames);
                return await TranscribeAsync(wav, "recording.wav", "audio/wav", cancellationToken);
            }
            finally { Destroy(clip); }
        }

        /// <summary>The clip must have readable PCM data (Read/Write enabled for imported audio).</summary>
        public Task<string> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            return TranscribeAsync(EncodeWav(clip, clip.samples), "audio.wav", "audio/wav", cancellationToken);
        }

        /// <summary>Uploads an encoded audio file. Returns text and raises OnTranscribed on success.</summary>
        public async Task<string> TranscribeAsync(byte[] audio, string fileName = "audio.wav",
            string contentType = "audio/wav", CancellationToken cancellationToken = default)
        {
            if (IsTranscribing) throw new SpeechRecognitionException(SpeechErrorCode.Busy, "Đang nhận diện một đoạn âm thanh khác. Vui lòng đợi.");
            var backend = BIMTTSSettings.Load();
            if (!backend.IsProxyConfigured) throw new SpeechRecognitionException(SpeechErrorCode.MissingApiKey, backend.ProxyProblem);
            if (string.IsNullOrWhiteSpace(backend.appKey)) throw new SpeechRecognitionException(SpeechErrorCode.MissingApiKey, "Thiếu App Key của Worker. Mở Tools > BIM AI > Create or Select TTS Settings và nhập App Key.");
            if (audio == null || audio.Length == 0) throw new ArgumentException("Dữ liệu âm thanh rỗng.", nameof(audio));
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("An audio filename and content type are required.");

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var token = lifetime.Token;
            token.ThrowIfCancellationRequested();
            IsTranscribing = true;
            try
            {
                var form = new List<IMultipartFormSection>
                {
                    new MultipartFormFileSection("file", audio, fileName, contentType),
                    new MultipartFormDataSection("model", model),
                    new MultipartFormDataSection("response_format", "json")
                };
                if (!string.IsNullOrWhiteSpace(LanguageCode))
                    form.Add(new MultipartFormDataSection("language", LanguageCode));
                if (!string.IsNullOrWhiteSpace(transcriptionPrompt))
                    form.Add(new MultipartFormDataSection("prompt", transcriptionPrompt));
                using var request = UnityWebRequest.Post(backend.ProxyBaseUrl + "/v1/audio/transcriptions", form);
                request.SetRequestHeader("X-App-Key", backend.appKey.Trim());
                request.timeout = timeoutSeconds;
                var operation = request.SendWebRequest();
                while (!operation.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                token.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                    throw CreateHttpException(request);
                var response = JsonUtility.FromJson<TranscriptionResponse>(request.downloadHandler.text);
                if (response == null || string.IsNullOrWhiteSpace(response.text))
                    throw new SpeechRecognitionException(SpeechErrorCode.EmptyTranscript, "Không nhận diện được nội dung giọng nói. Hãy nói rõ hơn và thử lại.");
                LastTranscript = response.text.Trim();
                onTranscribed.Invoke(LastTranscript);
                return LastTranscript;
            }
            finally { IsTranscribing = false; }
        }

        /// <summary>Biên độ đỉnh |sample| (0..1) của <paramref name="frames"/> frame đầu tiên.</summary>
        private static float ComputePeak(AudioClip clip, int frames)
        {
            var samples = new float[checked(frames * clip.channels)];
            if (!clip.GetData(samples, 0)) return 0f;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = samples[i] < 0f ? -samples[i] : samples[i];
                if (a > peak) peak = a;
            }
            return peak;
        }

        private SpeechRecognitionException CreateHttpException(UnityWebRequest request)
        {
            long code = request.responseCode;
            string error = request.error ?? "";
            if (code == 401 || code == 403)
                return new SpeechRecognitionException(SpeechErrorCode.InvalidApiKey,
                    $"App Key của Worker không hợp lệ hoặc đã bị thu hồi (HTTP {code}).");
            if (code == 429)
                return new SpeechRecognitionException(SpeechErrorCode.RateLimited,
                    "Groq đang giới hạn lượt gọi (HTTP 429). Vui lòng thử lại sau.");
            if (error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
                return new SpeechRecognitionException(SpeechErrorCode.Timeout,
                    $"Groq không phản hồi trong {timeoutSeconds} giây. Hãy kiểm tra Internet và thử lại.");
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return new SpeechRecognitionException(SpeechErrorCode.Network,
                    "Không thể kết nối Groq. Hãy kiểm tra kết nối Internet.");
            return new SpeechRecognitionException(SpeechErrorCode.Unknown,
                $"Groq nhận diện thất bại (HTTP {code}): {error}");
        }

        private static byte[] EncodeWav(AudioClip clip, int frames)
        {
            var samples = new float[checked(frames * clip.channels)];
            if (!clip.GetData(samples, 0)) throw new InvalidOperationException("Không đọc được dữ liệu PCM của AudioClip.");
            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM, signed little-endian 16-bit.
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (float sample in samples)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }

        private void OnDisable()
        {
            CancelRecording();
        }

        private void ReportError(Exception exception)
        {
            Debug.LogError($"{nameof(GroqSpeechToText)}: {exception.Message}", this);
            onError.Invoke(exception.Message);
        }

        [Serializable]
        private sealed class TranscriptionResponse { public string text; }
    }
}
