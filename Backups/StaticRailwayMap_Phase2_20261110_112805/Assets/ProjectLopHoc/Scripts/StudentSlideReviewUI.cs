using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectLopHoc.Core.ScriptableObjects;

namespace ProjectLopHoc.UI
{
    public class StudentSlideReviewUI : MonoBehaviour
    {
        [Header("Dữ liệu cấu hình bài giảng")]
        public ClassroomConfigSO classroomConfig;

        [Header("Hiển thị giao diện")]
        public Image slideImageDisplay;
        public TMP_Text slideCounterText;
        public TMP_Text lectureTitleText;
        public TMP_Dropdown lectureDropdown;

        private Sprite[] _currentSlides;
        private int _currentSlideIndex = 0;

        private void Start()
        {
            InitDropdown();
            LoadLecture(0);
        }

        private void InitDropdown()
        {
            if (classroomConfig == null || classroomConfig.availableLectures.Count == 0) return;

            lectureDropdown.ClearOptions();
            lectureDropdown.AddOptions(classroomConfig.availableLectures);
            lectureDropdown.onValueChanged.AddListener(LoadLecture);
        }

        public void LoadLecture(int index)
        {
            if (classroomConfig == null || classroomConfig.availableLectures.Count == 0) return;

            string folderName = classroomConfig.availableLectures[index];
            _currentSlides = Resources.LoadAll<Sprite>($"Lectures/{folderName}");
            _currentSlideIndex = 0;

            if (lectureTitleText != null)
                lectureTitleText.text = $"Chuyên đề: {folderName}";

            UpdateDisplay();
        }

        public void NextSlide()
        {
            if (_currentSlides == null || _currentSlides.Length == 0) return;
            _currentSlideIndex = Mathf.Clamp(_currentSlideIndex + 1, 0, _currentSlides.Length - 1);
            UpdateDisplay();
        }

        public void PreviousSlide()
        {
            if (_currentSlides == null || _currentSlides.Length == 0) return;
            _currentSlideIndex = Mathf.Clamp(_currentSlideIndex - 1, 0, _currentSlides.Length - 1);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_currentSlides == null || _currentSlides.Length == 0) return;

            slideImageDisplay.sprite = _currentSlides[_currentSlideIndex];
            if (slideCounterText != null)
                slideCounterText.text = $"Trang {_currentSlideIndex + 1}/{_currentSlides.Length}";
        }
    }
}