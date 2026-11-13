using System;
using System.IO;
using UnityEngine;

namespace BIM4D.AI.Generative
{
    /// <summary>Loads local development secrets from Application.persistentDataPath/bim_ai_secrets.json.</summary>
    public static class BIMAISecrets
    {
        [Serializable]
        private sealed class SecretsFile
        {
            public string xkiroApiKey = "";
            public string groqApiKey = "";
        }

        private static SecretsFile cached;
        private static DateTime lastWriteUtc;
        public static string ConfigPath => Path.Combine(Application.persistentDataPath, "bim_ai_secrets.json");

        public static string XkiroApiKey => Clean(Load().xkiroApiKey);
        public static string GroqApiKey => Clean(Load().groqApiKey);

        // Bỏ khoảng trắng và coi giá trị mẫu (PASTE_...) trong file tạo tự động là "chưa có key".
        private static string Clean(string key)
        {
            key = key?.Trim() ?? "";
            return key.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase) ? "" : key;
        }

        public static void Reload()
        {
            cached = null;
            Load();
        }

        private static SecretsFile Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    if (cached == null)
                        Debug.LogWarning($"[BIM AI] Chưa tìm thấy file API key: {ConfigPath}. Tạo bằng Tools > BIM AI > Create or Open Local API Config (xem Documentation/SetupGuide.md).");
                    return cached ?? (cached = new SecretsFile());
                }

                DateTime write = File.GetLastWriteTimeUtc(ConfigPath);
                if (cached != null && write == lastWriteUtc) return cached;
                string json = File.ReadAllText(ConfigPath);
                cached = JsonUtility.FromJson<SecretsFile>(json) ?? new SecretsFile();
                lastWriteUtc = write;
                return cached;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BIM AI] Không đọc được file cấu hình API key: " + ex.Message);
                return cached ?? (cached = new SecretsFile());
            }
        }
    }
}
