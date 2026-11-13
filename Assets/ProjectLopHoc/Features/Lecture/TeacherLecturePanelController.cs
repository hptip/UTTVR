
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectLopHoc.Features.Lecture
{
    public class TeacherLecturePanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private LectureActionManager actionManager;
        [SerializeField] private LectureBoardController board;
        [SerializeField] private Image slidePreview;
        [SerializeField] private TMP_Text lectureName;
        [SerializeField] private TMP_Text slideCounter;

        private int lastLecture = -1;
        private int lastSlide = -1;
        private Sprite[] slides = new Sprite[0];

        public void Open()
        {
            if (panel != null)
                panel.SetActive(true);

            Refresh(true);
        }

        public void Close()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        public void NextSlide()
        {
            if (actionManager != null)
                actionManager.NextSlide();
        }

        public void PreviousSlide()
        {
            if (actionManager != null)
                actionManager.PreviousSlide();
        }

        public void NextLecture()
        {
            if (actionManager != null)
                actionManager.NextLecture();
        }

        public void PreviousLecture()
        {
            if (actionManager != null)
                actionManager.PreviousLecture();
        }

        private void Update()
        {
            if (panel != null && panel.activeInHierarchy)
                Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (board == null ||
                board.classroomConfig == null ||
                board.classroomConfig.availableLectures == null)
                return;

                int lecture = board.DisplayLectureIndex;
                int slide = board.DisplaySlideIndex;

            if (!force &&
                lecture == lastLecture &&
                slide == lastSlide)
                return;

            var lectures = board.classroomConfig.availableLectures;

            if (lecture < 0 || lecture >= lectures.Count)
                return;

            if (force || lecture != lastLecture)
            {
                string folder = lectures[lecture];

                slides = Resources.LoadAll<Sprite>(
                    "Lectures/" + folder);

                System.Array.Sort(slides,
                    (a, b) => string.Compare(
                        a.name, b.name,
                        System.StringComparison.Ordinal));

                if (lectureName != null)
                    lectureName.text = folder;
            }

            if (slidePreview != null)
            {
                slidePreview.sprite =
                    slides.Length > 0
                    ? slides[Mathf.Clamp(slide, 0, slides.Length - 1)]
                    : null;

                slidePreview.preserveAspect = true;
            }

            if (slideCounter != null)
            {
                slideCounter.text =
                    slides.Length > 0
                    ? $"Slide {Mathf.Clamp(slide + 1, 1, slides.Length)} / {slides.Length}"
                    : "Không có slide";
            }

            lastLecture = lecture;
            lastSlide = slide;
        }
    }
}
