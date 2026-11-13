using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectLopHoc.Features.Quiz
{
    public class QuizPopupUI : MonoBehaviour
    {
        [Header("Khung chính")]
        [Tooltip("GameObject chứa toàn bộ popup - thường chính là object này")]
        public GameObject rootPanel;

        [Header("Tâm ngắm (Crosshair)")]
        [Tooltip("Kéo GameObject Crosshair vào đây (nếu để trống script sẽ tự tìm trong Scene)")]
        public GameObject crosshairUI;

        [Header("Phần câu hỏi")]
        public TMP_Text progressText;
        public TMP_Text questionText;
        public Button[] optionButtons; // đúng 4 nút, thứ tự A, B, C, D
        public TMP_Text[] optionLabels; // 4 Text con tương ứng của optionButtons
        public Button nextButton;
        public Button prevButton;
        public Button submitButton;

        [Header("Nút thoát (góc trên phải)")]
        [Tooltip("Cho phép sinh viên thoát làm bài giữa chừng mà không nộp điểm")]
        public Button exitButton;

        [Header("Màn hình kết quả")]
        public GameObject resultPanel;
        public TMP_Text resultText;
        public Button closeButton;

        private QuizSO _quiz;
        private QuizSessionController _session;
        private int _currentIndex;
        private int[] _selectedAnswers;

        private readonly Color _normalColor = Color.white;
        private readonly Color _selectedColor = new Color(0.6f, 0.8f, 1f);

        private void Awake()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int idx = i;
                optionButtons[i].onClick.AddListener(() => SelectOption(idx));
            }

            nextButton.onClick.AddListener(NextQuestion);
            prevButton.onClick.AddListener(PrevQuestion);
            submitButton.onClick.AddListener(SubmitQuiz);
            closeButton.onClick.AddListener(ExitPopup);

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(ExitPopup);
            }

            // Tự động tìm Crosshair trong Scene nếu chưa gán thủ công
            if (crosshairUI == null)
            {
                crosshairUI = GameObject.Find("Crosshair");
            }

            ForceHide(false); // Khởi tạo: chỉ ẩn panel, không can thiệp chuột
        }

        private void LateUpdate()
        {
            // Ép buộc mở khóa chuột MỌI FRAME trong lúc bảng trắc nghiệm hoặc bảng kết quả đang mở
            if ((rootPanel != null && rootPanel.activeInHierarchy) || 
                (resultPanel != null && resultPanel.activeInHierarchy))
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                }
                if (!Cursor.visible)
                {
                    Cursor.visible = true;
                }
            }
        }

        public void ShowQuiz(QuizSO quiz, QuizSessionController session)
        {
            if (quiz == null || quiz.questions == null || quiz.questions.Count == 0)
            {
                Debug.LogWarning("[QuizPopupUI] Đề thi trống, không có câu hỏi để hiển thị.");
                return;
            }

            _quiz = quiz;
            _session = session;
            _currentIndex = 0;
            _selectedAnswers = new int[quiz.questions.Count];
            for (int i = 0; i < _selectedAnswers.Length; i++) _selectedAnswers[i] = -1;

            resultPanel.SetActive(false);
            rootPanel.SetActive(true);

            // Ẩn tâm ngắm khi bắt đầu làm bài
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(false);
            }

            // Mở chuột để thao tác UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RefreshQuestion();
        }

        public void ForceHide(bool lockCursor = true)
        {
            if (rootPanel != null) rootPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);

            // Bật lại tâm ngắm Crosshair
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(true);
            }

            // Khóa chuột trở lại tâm màn hình FPS
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ExitPopup()
        {
            // Thoát hoàn toàn popup và khóa lại chuột vào tâm
            ForceHide(true);
        }

        private void RefreshQuestion()
        {
            var q = _quiz.questions[_currentIndex];
            progressText.text = $"Câu {_currentIndex + 1}/{_quiz.questions.Count}";
            questionText.text = q.questionText;

            for (int i = 0; i < optionLabels.Length; i++)
            {
                optionLabels[i].text = q.options[i];
                optionButtons[i].image.color = (_selectedAnswers[_currentIndex] == i) ? _selectedColor : _normalColor;
            }

            prevButton.interactable = _currentIndex > 0;
            nextButton.interactable = _currentIndex < _quiz.questions.Count - 1;
        }

        private void SelectOption(int index)
        {
            _selectedAnswers[_currentIndex] = index;
            RefreshQuestion();
        }

        private void NextQuestion()
        {
            if (_currentIndex < _quiz.questions.Count - 1)
            {
                _currentIndex++;
                RefreshQuestion();
            }
        }

        private void PrevQuestion()
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                RefreshQuestion();
            }
        }

        private void SubmitQuiz()
        {
            int score = 0;
            for (int i = 0; i < _quiz.questions.Count; i++)
            {
                if (_selectedAnswers[i] == _quiz.questions[i].correctIndex) score++;
            }

            resultText.text = $"Bạn trả lời đúng {score}/{_quiz.questions.Count} câu.";
            resultPanel.SetActive(true);

            // 1. Lấy tên thật từ DataBridge (nếu chưa có thì lấy tên mặc định)
            string studentName = (DataBridge.Instance != null && !string.IsNullOrEmpty(DataBridge.Instance.CurrentFullName)) 
                                 ? DataBridge.Instance.CurrentFullName 
                                 : "Sinh viên";
            
            // Đồng bộ số bài đã nộp qua mạng Photon Fusion
            _session?.RPC_SubmitResult(studentName, score, _quiz.questions.Count);

            // 2. Ghi vĩnh viễn vào SQLite có liên kết User_ID
            string quizName = (_quiz != null) ? _quiz.quizName : "Bài kiểm tra";
            if (DataBridge.Instance != null)
            {
                DataBridge.Instance.SaveQuizResult(quizName, score, _quiz.questions.Count);
            }
        }
    }
}