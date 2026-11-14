using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectLopHoc.Core.ScriptableObjects;
using ProjectLopHoc.Features.Lecture;
namespace ProjectLopHoc.UI
{
    public class StudentSlideReviewUI : MonoBehaviour
    {
        public ClassroomConfigSO classroomConfig;
        public Image slideImageDisplay;
        public TMP_Text slideCounterText, lectureTitleText;
        public TMP_Dropdown lectureDropdown;
        public LectureBoardController board;
        public Button previousLessonButton, nextLessonButton;
        private void OnEnable()
        {
            var mouseRaycaster = GetComponentInParent<GraphicRaycaster>();
            if (mouseRaycaster != null) mouseRaycaster.enabled = true;
            if (board == null) return;
            classroomConfig = board.classroomConfig; board.PresentationChanged += RefreshView;
            if (lectureDropdown != null) lectureDropdown.onValueChanged.AddListener(LoadLecture);
            board.RefreshPresentation(); RefreshView();
        }
        private void OnDisable()
        {
            if (board != null) board.PresentationChanged -= RefreshView;
            if (lectureDropdown != null) lectureDropdown.onValueChanged.RemoveListener(LoadLecture);
        }
        public void LoadLecture(int index) { if (board != null) board.SelectLecture(index); }
        public void NextSlide() { if (board != null) board.NextSlide(); }
        public void PreviousSlide() { if (board != null) board.PreviousSlide(); }
        private void RefreshView()
        {
            if (board == null) return;
            classroomConfig = board.classroomConfig;
            if (slideImageDisplay != null) { slideImageDisplay.sprite = board.DisplaySprite; slideImageDisplay.enabled = board.DisplaySprite != null; slideImageDisplay.preserveAspect = true; }
            if (slideCounterText != null) slideCounterText.text = board.SlideCount == 0 ? "Không có slide" : $"Trang {Mathf.Clamp(board.DisplaySlideIndex + 1, 1, board.SlideCount)}/{board.SlideCount}";
            if (lectureTitleText != null) lectureTitleText.text = board.LectureCount == 0 ? "Chưa có bài giảng" : $"Bài giảng {board.DisplayLectureIndex + 1}/{board.LectureCount}: {board.DisplayLectureName}";
            if (previousLessonButton != null) previousLessonButton.interactable = board.CanPreviousLecture;
            if (nextLessonButton != null) nextLessonButton.interactable = board.CanNextLecture;
            if (lectureDropdown != null)
            {
                bool rebuild = lectureDropdown.options.Count != board.LectureCount;
                for (int i = 0; !rebuild && i < board.LectureCount; i++) rebuild = lectureDropdown.options[i].text != classroomConfig.availableLectures[i];
                if (rebuild) { lectureDropdown.ClearOptions(); if (board.LectureCount > 0) lectureDropdown.AddOptions(classroomConfig.availableLectures); }
                lectureDropdown.SetValueWithoutNotify(board.LectureCount == 0 ? 0 : board.DisplayLectureIndex);
                lectureDropdown.interactable = board.CanNavigate && board.LectureCount > 0;
            }
        }
    }
}
