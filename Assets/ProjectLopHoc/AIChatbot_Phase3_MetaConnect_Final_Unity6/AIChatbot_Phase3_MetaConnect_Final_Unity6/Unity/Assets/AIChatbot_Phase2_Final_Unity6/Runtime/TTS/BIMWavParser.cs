using System;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Đọc WAV do MetaConnect trả về (WAV có thể được stream nên trường kích thước data có thể là 0 hoặc 0xFFFFFFFF).
    /// Hàm thuần C# (không phụ thuộc Unity) -> chạy được trên mọi nền tảng. Không ném ngoại lệ: trả false + lý do.
    /// Chấp nhận PCM 16-bit (WAVE_FORMAT_PCM hoặc EXTENSIBLE) và IEEE float 32-bit (đổi sang PCM16); mono hoặc stereo.
    /// </summary>
    public static class BIMWavParser
    {
        public const int MaxBytes = 64 * 1024 * 1024;

        public static bool TryParse(byte[] b, out BIMTtsAudio audio, out string error)
        {
            audio = null;
            error = null;
            if (b == null || b.Length < 44) { error = "Dữ liệu quá ngắn để là WAV."; return false; }
            if (b.Length > MaxBytes) { error = "WAV quá lớn."; return false; }
            if (!Tag(b, 0, "RIFF") || !Tag(b, 8, "WAVE")) { error = "Không phải tệp WAV (thiếu RIFF/WAVE)."; return false; }

            int fmtTag = 0, channels = 0, rate = 0, bits = 0;
            bool haveFmt = false;
            long dataStart = -1, dataLen = 0;

            long pos = 12;
            while (pos + 8 <= b.Length)
            {
                long size = U32(b, pos + 4);
                long body = pos + 8;

                if (Tag(b, pos, "fmt "))
                {
                    if (size < 16 || body + 16 > b.Length) { error = "Khối fmt không hợp lệ."; return false; }
                    fmtTag = U16(b, body);
                    channels = U16(b, body + 2);
                    rate = (int)U32(b, body + 4);
                    bits = U16(b, body + 14);
                    if (fmtTag == 0xFFFE && size >= 40 && body + 26 <= b.Length) fmtTag = U16(b, body + 24); // sub-format
                    haveFmt = true;
                }
                else if (Tag(b, pos, "data"))
                {
                    long available = b.Length - body;
                    dataStart = body;
                    // Stream: size = 0 hoặc 0xFFFFFFFF hoặc lớn hơn dữ liệu thực -> dùng phần còn lại của buffer.
                    dataLen = (size == 0 || size > available) ? available : size;
                    break;
                }

                long next = body + size + (size & 1);
                if (next <= pos || next > b.LongLength) break; // chunk lạ có kích thước vô lý -> dừng quét
                pos = next;
            }

            if (!haveFmt) { error = "Thiếu khối fmt."; return false; }
            if (dataStart < 0) { error = "Thiếu khối data."; return false; }
            if (channels < 1 || channels > 2) { error = "Số kênh không hỗ trợ: " + channels + "."; return false; }
            if (rate < 8000 || rate > 96000) { error = "Sample rate không hợp lý: " + rate + "."; return false; }

            int bytesPerSample;
            if (fmtTag == 1 && bits == 16) bytesPerSample = 2;
            else if (fmtTag == 3 && bits == 32) bytesPerSample = 4;
            else { error = "Định dạng WAV không hỗ trợ (tag " + fmtTag + ", " + bits + " bit)."; return false; }

            int frame = channels * bytesPerSample;
            dataLen -= dataLen % frame;
            if (dataLen <= 0) { error = "WAV không có mẫu âm thanh."; return false; }

            byte[] pcm;
            if (bytesPerSample == 2)
            {
                pcm = new byte[dataLen];
                Buffer.BlockCopy(b, (int)dataStart, pcm, 0, (int)dataLen);
            }
            else
            {
                int count = (int)(dataLen / 4);
                pcm = new byte[count * 2];
                for (int i = 0; i < count; i++)
                {
                    float f = BitConverter.ToSingle(b, (int)dataStart + i * 4);
                    if (float.IsNaN(f)) f = 0f;
                    if (f > 1f) f = 1f; else if (f < -1f) f = -1f;
                    short v = (short)Math.Round(f * 32767f);
                    pcm[i * 2] = (byte)(v & 0xFF);
                    pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
                }
            }

            audio = new BIMTtsAudio { Pcm16 = pcm, SampleRate = rate, Channels = channels };
            return true;
        }

        /// <summary>PCM16 -> float [-1,1] để tạo AudioClip. Gọi trên thread nền được.</summary>
        public static float[] ToFloat(byte[] pcm16)
        {
            int n = pcm16.Length / 2;
            var f = new float[n];
            for (int i = 0; i < n; i++)
                f[i] = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8)) / 32768f;
            return f;
        }

        private static bool Tag(byte[] b, long o, string t)
        {
            if (o + 4 > b.Length) return false;
            return b[o] == t[0] && b[o + 1] == t[1] && b[o + 2] == t[2] && b[o + 3] == t[3];
        }
        private static int U16(byte[] b, long o) => b[o] | (b[o + 1] << 8);
        private static long U32(byte[] b, long o) => (long)b[o] | ((long)b[o + 1] << 8) | ((long)b[o + 2] << 16) | ((long)b[o + 3] << 24);
    }
}
