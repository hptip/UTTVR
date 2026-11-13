using UnityEngine;

namespace ProjectLopHoc.Features.Lecture
{
    /// <summary>
    /// Điều phối lệnh điều khiển bài giảng và chuyển slide tới LectureBoardController.
    /// </summary>
    public class LectureActionManager : MonoBehaviour
    {
        [Header("--- Tham chiếu Bảng Chiếu ---")]
        [Tooltip("Kéo đối tượng gắn LectureBoardController vào đây")]
        public LectureBoardController boardController;

        public void NextSlide()
        {
            if (CheckBoard()) boardController.NextSlide();
        }

        public void PreviousSlide()
        {
            if (CheckBoard()) boardController.PreviousSlide();
        }

        public void NextLecture()
        {
            if (CheckBoard()) boardController.NextLecture();
        }

        public void PreviousLecture()
        {
            if (CheckBoard()) boardController.PreviousLecture();
        }

        private bool CheckBoard()
        {
            if (boardController == null)
            {
                Debug.LogWarning($"[{name}] Chưa gán LectureBoardController trên Inspector.");
                return false;
            }
            return true;
        }
    }
}