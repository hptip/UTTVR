using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Quản lý MỘT AudioSource duy nhất (trên GameObject con riêng): dựng AudioClip từ PCM16, phát, dừng, âm lượng.
    /// Clip chỉ tồn tại trong lúc phát rồi bị Destroy -> RAM thường trực chỉ là PCM16 trong cache (nhỏ hơn float32 một nửa).
    /// Không biết gì về MetaConnect hay giao diện chat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BIMAudioPlayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private AudioSource source;

        public float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                if (source != null) source.volume = volume;
            }
        }

        public bool IsPlaying => source != null && source.isPlaying;

        private void Awake() => EnsureSource();

        private void EnsureSource()
        {
            if (source != null) return;
            var go = new GameObject("BIM_TTS_AudioSource");
            go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // 2D: nghe đều, không phụ thuộc vị trí camera
            source.volume = volume;
        }

        /// <summary>Dựng AudioClip từ PCM16. Chuyển đổi float chạy ở thread nền; AudioClip.Create/SetData ở Main Thread.</summary>
        public static async Task<AudioClip> CreateClipAsync(BIMTtsAudio audio, string name)
        {
            if (audio == null || audio.Pcm16 == null || audio.Pcm16.Length < 2 || audio.Channels < 1)
                throw new BIMTtsException(BIMTtsErrorCode.InvalidAudio, "Không có dữ liệu âm thanh để phát.");
            byte[] pcm = audio.Pcm16;
            float[] samples = await Task.Run(() => BIMWavParser.ToFloat(pcm));
            var clip = AudioClip.Create(name, samples.Length / audio.Channels, audio.Channels, audio.SampleRate, false);
            if (!clip.SetData(samples, 0))
            {
                Destroy(clip);
                throw new BIMTtsException(BIMTtsErrorCode.InvalidAudio, "Không tạo được AudioClip.");
            }
            return clip;
        }

        /// <summary>Phát một đoạn tới khi xong rồi giải phóng clip. Hủy bằng token sẽ dừng ngay và ném OperationCanceledException.</summary>
        public async Task PlayAsync(BIMTtsAudio audio, CancellationToken ct)
        {
            EnsureSource();
            ct.ThrowIfCancellationRequested();
            AudioClip clip = await CreateClipAsync(audio, "BIMTTS");
            try
            {
                ct.ThrowIfCancellationRequested();
                source.clip = clip;
                source.volume = volume;
                source.Play();
                await Task.Yield();
                while (source != null && source.isPlaying)
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
                    await Task.Yield();
                }
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                Stop();
                if (clip != null) Destroy(clip);
            }
        }

        public void Stop()
        {
            if (source == null) return;
            source.Stop();
            source.clip = null; // không giữ tham chiếu tới clip sắp bị Destroy
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
    }
}
