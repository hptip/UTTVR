using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ProjectLopHoc.Features.Lecture
{
    public class TeacherLecturePanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private LectureActionManager actionManager;
        [SerializeField] private LectureBoardController board;
        [SerializeField] private Image slidePreview;
        [SerializeField] private TMP_Text lectureName, slideCounter;
        [SerializeField] private TMP_Dropdown lectureDropdown;
        public Button previousLessonButton, nextLessonButton;
        private void OnEnable()
        {
            var mouseRaycaster = GetComponentInParent<GraphicRaycaster>();
            if (mouseRaycaster != null) mouseRaycaster.enabled = true;
            if (board == null) return;
            board.PresentationChanged += RefreshView;
            if (lectureDropdown != null) lectureDropdown.onValueChanged.AddListener(LoadLecture);
            board.RefreshPresentation(); RefreshView();
        }
        private void OnDisable()
        {
            if (board != null) board.PresentationChanged -= RefreshView;
            if (lectureDropdown != null) lectureDropdown.onValueChanged.RemoveListener(LoadLecture);
        }
        public void Open() { if (panel != null) panel.SetActive(true); if (board != null) { board.RefreshPresentation(); RefreshView(); } }
        public void Close() { if (panel != null) panel.SetActive(false); }
        public void NextSlide() { if (actionManager != null) actionManager.NextSlide(); }
        public void PreviousSlide() { if (actionManager != null) actionManager.PreviousSlide(); }
        public void NextLecture() { if (actionManager != null) actionManager.NextLecture(); }
        public void PreviousLecture() { if (actionManager != null) actionManager.PreviousLecture(); }
        public void LoadLecture(int index) { if (board != null) board.SelectLecture(index); }
        private void RefreshView()
        {
            if (board == null) return;
            if (slidePreview != null) { slidePreview.sprite = board.DisplaySprite; slidePreview.enabled = board.DisplaySprite != null; slidePreview.preserveAspect = true; }
            if (lectureName != null) lectureName.text = board.LectureCount == 0 ? "Chưa có bài giảng" : $"Bài giảng {board.DisplayLectureIndex + 1}/{board.LectureCount}: {board.DisplayLectureName}";
            if (slideCounter != null) slideCounter.text = board.SlideCount == 0 ? "Không có slide" : $"Trang {Mathf.Clamp(board.DisplaySlideIndex + 1, 1, board.SlideCount)}/{board.SlideCount}";
            if (previousLessonButton != null) previousLessonButton.interactable = board.CanPreviousLecture;
            if (nextLessonButton != null) nextLessonButton.interactable = board.CanNextLecture;
            if (lectureDropdown != null)
            {
                bool rebuild = lectureDropdown.options.Count != board.LectureCount;
                for (int i = 0; !rebuild && i < board.LectureCount; i++) rebuild = lectureDropdown.options[i].text != board.classroomConfig.availableLectures[i];
                if (rebuild) { lectureDropdown.ClearOptions(); if (board.LectureCount > 0) lectureDropdown.AddOptions(board.classroomConfig.availableLectures); }
                lectureDropdown.SetValueWithoutNotify(board.LectureCount == 0 ? 0 : board.DisplayLectureIndex);
                lectureDropdown.interactable = board.CanNavigate && board.LectureCount > 0;
            }
        }
    }
}
