
using System;
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

        private Sprite[] _currentSlides = Array.Empty<Sprite>();
        private int _currentSlideIndex;

        private void Start()
        {
            if (classroomConfig == null)
            {
                Debug.LogError(
                    "[StudentSlide] Chưa gán ClassroomConfigSO!", this);
                return;
            }

            if (slideImageDisplay == null)
                Debug.LogError(
                    "[StudentSlide] Chưa gán Slide Image Display!", this);

            if (slideCounterText == null)
                Debug.LogError(
                    "[StudentSlide] Chưa gán Slide Counter Text!", this);

            InitDropdown();
            LoadLecture(0);
        }

        private void InitDropdown()
        {
            if (lectureDropdown == null ||
                classroomConfig.availableLectures == null)
                return;

            lectureDropdown.onValueChanged.RemoveListener(LoadLecture);
            lectureDropdown.ClearOptions();
            lectureDropdown.AddOptions(classroomConfig.availableLectures);
            lectureDropdown.SetValueWithoutNotify(0);
            lectureDropdown.onValueChanged.AddListener(LoadLecture);
        }

        public void LoadLecture(int index)
        {
            if (classroomConfig == null ||
                classroomConfig.availableLectures == null ||
                index < 0 ||
                index >= classroomConfig.availableLectures.Count)
                return;

            string folderName = classroomConfig.availableLectures[index];

            _currentSlides = Resources.LoadAll<Sprite>(
                $"Lectures/{folderName}");

            Array.Sort(_currentSlides, (a, b) =>
                string.Compare(
                    a.name, b.name,
                    StringComparison.OrdinalIgnoreCase));

            _currentSlideIndex = 0;

            Debug.Log(
                $"[StudentSlide] Đã tải {_currentSlides.Length} slide " +
                $"từ Lectures/{folderName}", this);

            if (lectureTitleText != null)
                lectureTitleText.text = $"Chuyên đề: {folderName}";

            UpdateDisplay();
        }

        public void NextSlide()
        {
            Debug.Log("[StudentSlide] NextSlide được gọi", this);

            if (_currentSlides.Length == 0)
                return;

            _currentSlideIndex = Mathf.Min(
                _currentSlideIndex + 1,
                _currentSlides.Length - 1);

            UpdateDisplay();
        }

        public void PreviousSlide()
        {
            Debug.Log("[StudentSlide] PreviousSlide được gọi", this);

            if (_currentSlides.Length == 0)
                return;

            _currentSlideIndex = Mathf.Max(
                _currentSlideIndex - 1, 0);

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_currentSlides.Length == 0)
            {
                if (slideImageDisplay != null)
                {
                    slideImageDisplay.sprite = null;
                    slideImageDisplay.enabled = false;
                }

                if (slideCounterText != null)
                    slideCounterText.text = "Không có slide";

                return;
            }

            Sprite currentSprite = _currentSlides[_currentSlideIndex];

            if (slideImageDisplay != null)
            {
                slideImageDisplay.enabled = true;
                slideImageDisplay.sprite = currentSprite;
            }

            if (slideCounterText != null)
                slideCounterText.text =
                    $"Trang {_currentSlideIndex + 1}/{_currentSlides.Length}";

            Debug.Log(
                $"[StudentSlide] Trang {_currentSlideIndex + 1}/" +
                $"{_currentSlides.Length} | Sprite: {currentSprite.name}" +
                $" | Image: {slideImageDisplay?.name}" +
                $" | Counter: {slideCounterText?.name}",
                this);
        }
    }
}
