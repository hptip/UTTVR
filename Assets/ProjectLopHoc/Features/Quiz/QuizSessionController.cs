using Fusion;
using UnityEngine;

namespace ProjectLopHoc.Features.Quiz
{
    /// <summary>
    /// Gắn vào 1 GameObject trong Scene Classroom (ví dụ đặt cạnh bàn giáo viên).
    /// Khi giáo viên bấm nút "Cho làm bài", popup trắc nghiệm sẽ tự động hiện lên
    /// TRÊN MÀN HÌNH CỦA TẤT CẢ NGƯỜI trong phòng cùng lúc (kể cả người vào trễ).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class QuizSessionController : NetworkBehaviour
    {
        [Header("Cấu hình đề thi")]
        [Tooltip("Kéo file QuizSO đã import câu hỏi vào đây")]
        public QuizSO quizConfig;

        [Header("Giao diện Popup")]
        [Tooltip("Kéo GameObject đang gắn QuizPopupUI vào đây (Canvas làm bài)")]
        public QuizPopupUI popupUI;

        [Networked] public bool IsQuizActive { get; set; }
        [Networked] public int SubmittedCount { get; set; }

        private bool _lastIsActive;

        public override void Spawned()
        {
            _lastIsActive = IsQuizActive;
            ApplyPopupState();
        }

        // Chạy trên MỌI client mỗi frame - phát hiện thay đổi để bật/tắt popup đúng lúc
        public override void Render()
        {
            if (IsQuizActive != _lastIsActive)
            {
                _lastIsActive = IsQuizActive;
                ApplyPopupState();
            }
        }

        private void ApplyPopupState()
        {
            if (popupUI == null)
            {
                Debug.LogWarning("[QuizSessionController] Chưa gán Popup UI trong Inspector.");
                return;
            }

            if (IsQuizActive)
            {
                popupUI.ShowQuiz(quizConfig, this);
            }
            else
            {
                popupUI.ForceHide();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_OpenQuiz()
        {
            SubmittedCount = 0;
            IsQuizActive = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_CloseQuiz()
        {
            IsQuizActive = false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SubmitResult(string studentName, int score, int totalQuestions)
        {
            SubmittedCount++;
            Debug.Log($"[Quiz] {studentName} nộp bài: {score}/{totalQuestions}. Tổng đã nộp: {SubmittedCount}.");
        }

        // Hàm không tham số - dùng để gán vào nút bấm 3D (IInteractable)
        public void OpenQuiz() => RPC_OpenQuiz();
        public void CloseQuiz() => RPC_CloseQuiz();
    }
}
