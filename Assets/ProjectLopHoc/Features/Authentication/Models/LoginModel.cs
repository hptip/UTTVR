using UnityEngine;

namespace ProjectLopHoc.Features.Authentication.Models
{
    public class LoginModel
    {
        private const string PREF_DISPLAY_NAME = "PLAYER_DISPLAY_NAME";

        public string DisplayName { get; private set; }
        public bool IsLecturer { get; private set; }

        public LoginModel()
        {
            // Tự động tải tên đã lưu từ lần chơi trước
            DisplayName = PlayerPrefs.GetString(PREF_DISPLAY_NAME, "SinhVien");
            IsLecturer = false;
        }

        public void SetUserData(string name, bool isLecturer)
        {
            DisplayName = string.IsNullOrEmpty(name) ? "Người chơi ẩn danh" : name;
            IsLecturer = isLecturer;

            // Lưu tên lại cho các lần sau
            PlayerPrefs.SetString(PREF_DISPLAY_NAME, DisplayName);
            PlayerPrefs.Save();
        }
    }
}