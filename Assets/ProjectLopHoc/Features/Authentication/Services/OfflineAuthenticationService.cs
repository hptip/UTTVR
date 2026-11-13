using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ProjectLopHoc.Features.Authentication.Services
{
    /// <summary>Authenticates against the password-hash export bundled with the Offline VR Demo.</summary>
    public static class OfflineAuthenticationService
    {
        private const int MinimumIterations = 100000;
        private static OfflineUserList _cache;

        [Serializable]
        private sealed class OfflineUserList { public OfflineUser[] users; }

        [Serializable]
        public sealed class OfflineUser
        {
            public int id;
            public string username;
            public string full_name;
            public string role;
            public string salt;
            public string passwordHash;
            public int iterations;
        }

        public static OfflineUser FindUser(string username)
        {
            if (_cache == null)
            {
                TextAsset source = Resources.Load<TextAsset>("offline_users");
                if (source == null) return null;
                _cache = JsonUtility.FromJson<OfflineUserList>(source.text);
            }

            if (_cache == null || _cache.users == null) return null;
            foreach (OfflineUser user in _cache.users)
                if (user != null && string.Equals(user.username, username, StringComparison.Ordinal))
                    return user;
            return null;
        }

        public static bool VerifyPassword(OfflineUser user, string password)
        {
            if (user == null || user.iterations < MinimumIterations) return false;
            try
            {
                byte[] salt = Convert.FromBase64String(user.salt);
                byte[] expected = Convert.FromBase64String(user.passwordHash);
                using (var derive = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, user.iterations, HashAlgorithmName.SHA256))
                {
                    byte[] actual = derive.GetBytes(expected.Length);
                    int diff = actual.Length ^ expected.Length;
                    for (int i = 0; i < actual.Length && i < expected.Length; i++) diff |= actual[i] ^ expected[i];
                    return diff == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
