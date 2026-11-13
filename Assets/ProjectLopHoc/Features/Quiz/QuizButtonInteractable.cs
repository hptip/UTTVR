using UnityEngine;
using ProjectLopHoc.Core.Interfaces;
using ProjectLopHoc.Core.UI;

namespace ProjectLopHoc.Features.Quiz
{
    public enum QuizButtonAction
    {
        OpenQuiz,
        CloseQuiz
    }

    [RequireComponent(typeof(Collider))]
    public class QuizButtonInteractable : MonoBehaviour, IInteractable
    {
        [Header("Cấu hình nút")]
        public QuizSessionController session;
        public QuizButtonAction action = QuizButtonAction.OpenQuiz;

        [Header("Tên nút tuỳ chọn (để trống sẽ dùng OpenQuiz / CloseQuiz mặc định)")]
        public string tooltipText = "";

        [Header("Hiệu ứng hover")]
        public Renderer buttonRenderer;
        public Color hoverColor = Color.yellow;

        private Color _originalColor;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponent<Renderer>();
            }

            if (buttonRenderer != null)
            {
                _originalColor = buttonRenderer.sharedMaterial.color;
            }
        }

        public void OnInteract(GameObject interactor)
        {
            if (session == null)
            {
                Debug.LogWarning("[QuizButtonInteractable] Chưa gán Session trong Inspector.");
                return;
            }

            if (action == QuizButtonAction.OpenQuiz)
            {
                session.OpenQuiz();
            }
            else
            {
                session.CloseQuiz();
            }
        }

        public void OnHoverEnter()
        {
            if (buttonRenderer != null)
            {
                buttonRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", hoverColor);
                buttonRenderer.SetPropertyBlock(_propBlock);
            }

            TooltipUI.Instance?.Show(GetDisplayTooltip(), transform.position);
        }

        public void OnHoverExit()
        {
            if (buttonRenderer != null)
            {
                buttonRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", _originalColor);
                buttonRenderer.SetPropertyBlock(_propBlock);
            }

            TooltipUI.Instance?.Hide();
        }

        private string GetDisplayTooltip()
        {
            if (!string.IsNullOrEmpty(tooltipText)) return tooltipText;

            switch (action)
            {
                case QuizButtonAction.OpenQuiz: return "OpenQuiz";
                case QuizButtonAction.CloseQuiz: return "CloseQuiz";
                default: return "";
            }
        }
    }
}