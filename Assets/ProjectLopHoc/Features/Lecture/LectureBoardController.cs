
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using ProjectLopHoc.Core.ScriptableObjects;

namespace ProjectLopHoc.Features.Lecture
{
    [RequireComponent(typeof(NetworkObject))]
    public class LectureBoardController : NetworkBehaviour
    {
        [Header("Dữ liệu")]
        public ClassroomConfigSO classroomConfig;

        [Header("Hiển thị")]
        public Renderer boardRenderer;
        public Image boardImage;

        [Networked] public int CurrentSlideIndex { get; set; }
        [Networked] public int CurrentLectureIndex { get; set; }

        private int _offlineSlideIndex;
        private int _offlineLectureIndex;

        public int DisplaySlideIndex =>
            Object != null ? CurrentSlideIndex : _offlineSlideIndex;

        public int DisplayLectureIndex =>
            Object != null ? CurrentLectureIndex : _offlineLectureIndex;

        private Sprite[] _loadedSlides = new Sprite[0];
        private int _cachedLectureIndex = -1;
        private int _cachedSlideIndex = -1;

        private MaterialPropertyBlock _propBlock;

        private static readonly int BaseMapId =
            Shader.PropertyToID("_BaseMap");

        private static readonly int MainTexId =
            Shader.PropertyToID("_MainTex");

        private void Start()
        {
            // Cho phép hiển thị slide khi chạy offline.
            if (Object == null)
            {
                InitializeDisplay();
            }
        }

        public override void Spawned()
        {
            // Khi Fusion khởi tạo, dùng trạng thái mạng.
            InitializeDisplay();
        }

        public override void Render()
        {
            RefreshIfChanged();
        }

        private void Update()
        {
            // Fusion chưa khởi tạo: chạy chế độ offline.
            if (Object == null)
            {
                RefreshIfChanged();
            }
        }

        private void InitializeDisplay()
        {
            if (_propBlock == null)
                _propBlock = new MaterialPropertyBlock();

            _cachedLectureIndex = -1;
            _cachedSlideIndex = -1;

            RefreshIfChanged();
        }

        private void RefreshIfChanged()
        {
            int lecture = DisplayLectureIndex;
            int slide = DisplaySlideIndex;

            if (lecture != _cachedLectureIndex)
            {
                LoadLectureSlides(lecture);
                RefreshDisplayVisual(slide);
            }
            else if (slide != _cachedSlideIndex)
            {
                RefreshDisplayVisual(slide);
            }
        }

        private void LoadLectureSlides(int lectureIndex)
        {
            _cachedLectureIndex = lectureIndex;

            if (classroomConfig == null ||
                classroomConfig.availableLectures == null ||
                classroomConfig.availableLectures.Count == 0)
            {
                Debug.LogWarning(
                    "[LectureBoard] Chưa cấu hình bài giảng.");

                _loadedSlides = new Sprite[0];
                return;
            }

            int safeIndex = Mathf.Clamp(
                lectureIndex,
                0,
                classroomConfig.availableLectures.Count - 1);

            string folder =
                classroomConfig.availableLectures[safeIndex];

            _loadedSlides = Resources.LoadAll<Sprite>(
                "Lectures/" + folder);

            System.Array.Sort(
                _loadedSlides,
                (a, b) => string.Compare(
                    a.name,
                    b.name,
                    System.StringComparison.Ordinal));

            if (_loadedSlides.Length == 0)
            {
                Debug.LogWarning(
                    "[LectureBoard] Không tìm thấy slide: " +
                    folder);
            }
        }

        private void RefreshDisplayVisual(int slideIndex)
        {
            _cachedSlideIndex = slideIndex;

            if (_loadedSlides == null ||
                _loadedSlides.Length == 0)
                return;

            int index = Mathf.Clamp(
                slideIndex, 0, _loadedSlides.Length - 1);

            Sprite sprite = _loadedSlides[index];

            if (sprite == null)
                return;

            if (boardImage != null)
            {
                boardImage.sprite = sprite;
                boardImage.preserveAspect = true;
            }

            if (boardRenderer != null)
            {
                if (_propBlock == null)
                    _propBlock = new MaterialPropertyBlock();

                boardRenderer.GetPropertyBlock(_propBlock);

                _propBlock.SetTexture(
                    BaseMapId, sprite.texture);

                _propBlock.SetTexture(
                    MainTexId, sprite.texture);

                boardRenderer.SetPropertyBlock(_propBlock);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestChangeSlide(int direction)
        {
            if (_loadedSlides.Length == 0)
                return;

            CurrentSlideIndex = Mathf.Clamp(
                CurrentSlideIndex + direction,
                0,
                _loadedSlides.Length - 1);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestChangeLecture(int direction)
        {
            if (classroomConfig == null ||
                classroomConfig.availableLectures == null ||
                classroomConfig.availableLectures.Count == 0)
                return;

            CurrentLectureIndex = Mathf.Clamp(
                CurrentLectureIndex + direction,
                0,
                classroomConfig.availableLectures.Count - 1);

            CurrentSlideIndex = 0;
        }

        public void NextSlide() => ChangeSlide(1);

        public void PreviousSlide() => ChangeSlide(-1);

        public void NextLecture() => ChangeLecture(1);

        public void PreviousLecture() => ChangeLecture(-1);

        private void ChangeSlide(int direction)
        {
            if (Object != null)
            {
                RPC_RequestChangeSlide(direction);
                return;
            }

            if (_loadedSlides.Length == 0)
                return;

            _offlineSlideIndex = Mathf.Clamp(
                _offlineSlideIndex + direction,
                0,
                _loadedSlides.Length - 1);

            RefreshIfChanged();
        }

        private void ChangeLecture(int direction)
        {
            if (Object != null)
            {
                RPC_RequestChangeLecture(direction);
                return;
            }

            if (classroomConfig == null ||
                classroomConfig.availableLectures == null ||
                classroomConfig.availableLectures.Count == 0)
                return;

            _offlineLectureIndex = Mathf.Clamp(
                _offlineLectureIndex + direction,
                0,
                classroomConfig.availableLectures.Count - 1);

            _offlineSlideIndex = 0;

            RefreshIfChanged();
        }
    }
}
