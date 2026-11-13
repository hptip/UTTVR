using UnityEngine;
using System;
using System.Threading.Tasks;
using ProjectLopHoc.Core.ScriptableObjects;
using ProjectLopHoc.Features.Authentication.Models;
using ProjectLopHoc.Features.Authentication.Views;

namespace ProjectLopHoc.Features.Authentication.Controllers
{
    public class LoginController : MonoBehaviour
    {
        [Header("--- Tham chiếu cấu hình ---")]
        public ClassroomConfigSO classroomConfig;

        [Header("--- Tham chiếu View ---")]
        public LoginView loginView;

        private LoginModel _model;
        private string _selectedRole = "student";
        private bool _loginInProgress = false;

        public static event Action<string, bool> OnLoginSuccess;

        private void Start()
        {
            if (loginView == null)
            {
                Debug.LogError("[LoginController] LoginView chưa được gán!");
                return;
            }

            _model = new LoginModel();
            loginView.SetInitialCredentials("sv01", "123456");
            
            // Luôn hiện trường mật khẩu để cả Sinh viên lẫn Giảng viên đều phải nhập
            loginView.SetPasswordFieldVisible(true);

            loginView.OnRoleChanged += HandleRoleChanged;
            loginView.OnLoginClicked += HandleLogin;
        }

        private void OnDestroy()
        {
            if (loginView != null)
            {
                loginView.OnRoleChanged -= HandleRoleChanged;
                loginView.OnLoginClicked -= HandleLogin;
            }
        }

        private void HandleRoleChanged(string role)
        {
            if (_loginInProgress) return;
            _selectedRole = role;
            loginView.SetError("");
        }

        private async void HandleLogin()
        {
            if (_loginInProgress) return;

            if (loginView == null) return;

            string inputUsername = loginView.GetInputName();
            string inputPassword = loginView.GetInputPassword();

            if (string.IsNullOrWhiteSpace(inputUsername) || string.IsNullOrWhiteSpace(inputPassword))
            {
                loginView.SetError("Vui lòng nhập đầy đủ Mã tài khoản và Mật khẩu!");
                return;
            }

            if (DataBridge.Instance == null)
            {
                loginView.SetError("Lỗi hệ thống: Không tìm thấy DataBridge trong Scene!");
                return;
            }

            _loginInProgress = true;
            loginView.SetError("Đang xác thực tài khoản offline...");

            // =========================================================
            // 1. XÁC THỰC TÀI KHOẢN VỚI SQLITE SERVER
            // =========================================================
            var (isSuccess, message) = await DataBridge.Instance.LoginAsync(inputUsername, inputPassword);

            if (!isSuccess)
            {
                loginView.SetError(message);
                _loginInProgress = false; // Mở khóa để người dùng nhập lại
                return;
            }

            // =========================================================
            // 2. KIỂM TRA KHỚP VAI TRÒ (ROLE) GIỮA GIAO DIỆN & CSDL
            // =========================================================
            string userRole = DataBridge.Instance.CurrentRole; // "student", "teacher", "admin"
            bool roleMatches =
                (_selectedRole == "student" && userRole == "student") ||
                (_selectedRole == "teacher" && userRole == "teacher") ||
                (_selectedRole == "admin" && userRole == "admin");

            if (!roleMatches)
            {
                loginView.SetError("Vai trò đã chọn không khớp với tài khoản.");
                _loginInProgress = false;
                return;
            }

            bool isActuallyTeacher = userRole == "teacher";

            // =========================================================
            // 3. ĐỒNG BỘ DỮ LIỆU TÊN THẬT VÀO HỆ THỐNG
            // =========================================================
            string realFullName = DataBridge.Instance.CurrentFullName;
            _model.SetUserData(realFullName, isActuallyTeacher);

            // Gán đầy đủ cả 2 khóa để Fusion và QuizPopupUI đọc đồng bộ
            PlayerPrefs.SetInt("IsLecturer", isActuallyTeacher ? 1 : 0);
            PlayerPrefs.SetString("PlayerName", realFullName);
            PlayerPrefs.SetString("Username", realFullName);
            PlayerPrefs.SetInt("UserId", DataBridge.Instance.CurrentUserId);
            PlayerPrefs.SetString("UserRole", userRole);
            PlayerPrefs.Save();

            loginView.SetError("Đang kết nối vào khuôn viên UTT...");

            try
            {
                OnLoginSuccess?.Invoke(realFullName, isActuallyTeacher);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LoginController] Lỗi event: " + ex);
            }

            // =========================================================
            // 4. KẾT NỐI PHOTON FUSION
            // =========================================================
            if (NetworkManager.Instance != null)
            {
                bool started = await NetworkManager.Instance.StartCampusSession(realFullName, isActuallyTeacher);
                if (started) loginView.gameObject.SetActive(false);
                else
                {
                    loginView.SetError("Không thể tải UTTCampus. Vui lòng thử lại.");
                    _loginInProgress = false;
                }
            }
            else
            {
                loginView.SetError("Lỗi: Không tìm thấy NetworkManager trong Scene!");
                _loginInProgress = false;
            }
        }
    }
}
