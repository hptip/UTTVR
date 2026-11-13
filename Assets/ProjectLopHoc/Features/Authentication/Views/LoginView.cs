using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace ProjectLopHoc.Features.Authentication.Views
{
    public class LoginView : MonoBehaviour
    {
        [Header("--- Input Fields ---")]
        public TMP_InputField nameInputField;       // Dùng nhập Mã SV hoặc Tài khoản GV
        public TMP_InputField passwordInputField;   // Mật khẩu do Admin cấp

        [Header("--- Role Selection ---")]
        public Toggle studentToggle;
        public Toggle lecturerToggle;
        [NonSerialized] public Toggle adminToggle;
        public GameObject passwordFieldContainer;

        [Header("--- Major Selection (Chuyên ngành) ---")]
        public Toggle toggleDuongSatTDC; 
        public Toggle toggleQuanLyDieuHanh;
        public Toggle toggleCongNghePhuongTien;
        public Toggle toggleXayDungQuanLy;

        [Header("--- Buttons & Status ---")]
        public Button loginButton;
        [Tooltip("Kéo Text hiển thị thông báo lỗi vào đây (tuỳ chọn)")]
        public TMP_Text statusText;

        public event Action<string> OnRoleChanged;
        public event Action OnLoginClicked;

        private void Awake()
        {
            if (passwordInputField != null)
            {
                passwordInputField.contentType = TMP_InputField.ContentType.Password;
                passwordInputField.ForceLabelUpdate();
            }

            var keyboard = GetComponent<QuestLoginKeyboard>();
            if (keyboard == null) keyboard = gameObject.AddComponent<QuestLoginKeyboard>();
            keyboard.Initialize(nameInputField, passwordInputField, transform);

            if (studentToggle != null)
                studentToggle.onValueChanged.AddListener(OnStudentToggleChanged);

            if (lecturerToggle != null)
                lecturerToggle.onValueChanged.AddListener(OnLecturerToggleChanged);

            EnsureAdminToggle();

            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);
        }

        private void OnStudentToggleChanged(bool isOn)
        {
            if (!isOn) return;
            OnRoleChanged?.Invoke("student");
            // Cả Sinh viên cũng cần nhập mật khẩu do Admin cấp
            SetPasswordFieldVisible(true);
        }

        private void OnLecturerToggleChanged(bool isOn)
        {
            if (!isOn) return;
            OnRoleChanged?.Invoke("teacher");
            SetPasswordFieldVisible(true);
        }

        private void OnAdminToggleChanged(bool isOn)
        {
            if (!isOn) return;
            OnRoleChanged?.Invoke("admin");
            SetPasswordFieldVisible(true);
        }

        private void EnsureAdminToggle()
        {
            if (adminToggle != null || studentToggle == null || lecturerToggle == null) return;
            Transform group = studentToggle.transform.parent;
            if (group == null) return;

            GameObject copy = Instantiate(lecturerToggle.gameObject, group);
            copy.name = "AdminToggle";
            adminToggle = copy.GetComponent<Toggle>();
            adminToggle.isOn = false;
            adminToggle.onValueChanged.RemoveAllListeners();
            adminToggle.onValueChanged.AddListener(OnAdminToggleChanged);

            RectTransform rect = copy.GetComponent<RectTransform>();
            RectTransform lecturerRect = lecturerToggle.GetComponent<RectTransform>();
            if (rect != null && lecturerRect != null)
            {
                rect.anchoredPosition = lecturerRect.anchoredPosition + new Vector2(0f, -50f);
                rect.sizeDelta = lecturerRect.sizeDelta;
            }
            var label = copy.transform.Find("Label");
            if (label != null)
            {
                var legacyText = label.GetComponent<Text>();
                if (legacyText != null) legacyText.text = "Quản trị viên";
                var tmp = label.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = "Quản trị viên";
            }
        }

        private void OnLoginButtonClicked()
        {
            OnLoginClicked?.Invoke();
        }

        public void SetInitialName(string savedName)
        {
            if (nameInputField != null) nameInputField.text = savedName;
        }

        public void SetInitialCredentials(string username, string password)
        {
            if (nameInputField != null)
            {
                nameInputField.SetTextWithoutNotify(username);
                nameInputField.ForceLabelUpdate();
            }

            if (passwordInputField != null)
            {
                passwordInputField.SetTextWithoutNotify(password);
                passwordInputField.ForceLabelUpdate();
            }
        }

        public void ClearCredentialFields()
        {
            if (nameInputField != null)
            {
                nameInputField.SetTextWithoutNotify(string.Empty);
                nameInputField.ForceLabelUpdate();
            }

            if (passwordInputField != null)
            {
                passwordInputField.SetTextWithoutNotify(string.Empty);
                passwordInputField.ForceLabelUpdate();
            }
        }

        public void SetPasswordFieldVisible(bool visible)
        {
            if (passwordFieldContainer != null) passwordFieldContainer.SetActive(visible);
        }

        public void SetError(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            if (!string.IsNullOrEmpty(message))
            {
                Debug.Log("[Thông báo trạng thái]: " + message);
            }
        }

        public string GetInputName() => nameInputField != null ? nameInputField.text.Trim() : string.Empty;
        public string GetInputPassword() => passwordInputField != null ? passwordInputField.text : string.Empty;

        public string GetSelectedMajor()
        {
            if (toggleDuongSatTDC != null && toggleDuongSatTDC.isOn) return "Đường sắt tốc độ cao";
            if (toggleQuanLyDieuHanh != null && toggleQuanLyDieuHanh.isOn) return "Quản lý và điều hành";
            if (toggleCongNghePhuongTien != null && toggleCongNghePhuongTien.isOn) return "Công nghệ phương tiện";
            if (toggleXayDungQuanLy != null && toggleXayDungQuanLy.isOn) return "Xây dựng và quản lý";
            return "Chưa chọn chuyên ngành";
        }

        private void OnDestroy()
        {
            if (studentToggle != null) studentToggle.onValueChanged.RemoveListener(OnStudentToggleChanged);
            if (lecturerToggle != null) lecturerToggle.onValueChanged.RemoveListener(OnLecturerToggleChanged);
            if (adminToggle != null) adminToggle.onValueChanged.RemoveListener(OnAdminToggleChanged);
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        }
    }
}
