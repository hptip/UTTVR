using System;
using UnityEngine;
using UnityEngine.Splines;

namespace UTT.SaBan
{
    /// <summary>
    /// Cho đoàn tàu thu nhỏ chạy dọc tuyến ray của Sa Bàn. Tốc độ tính theo "thời gian cả hành trình",
    /// nên đổi tỉ lệ Sa Bàn thì tàu vẫn chạy hết tuyến trong cùng một khoảng thời gian.
    /// Chạy x1 / x2 / x3 / x4 = nhân tốc độ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaBanTrainRunner : MonoBehaviour
    {
        [Header("Tuyến ray")]
        public SplineContainer route;

        [Header("Các toa (toa đầu đứng trước)")]
        public Transform[] cars = new Transform[0];

        [Tooltip("Khoảng cách từ mỗi toa tới toa đầu, tính bằng MÉT THẬT. Toa đầu = 0.")]
        public float[] carBackOffsets = new float[0];

        [Tooltip("Độ nâng của từng toa so với mặt ray, MÉT THẬT.")]
        public float[] carHeights = new float[0];

        [Header("Vận hành")]
        [Min(5f)] public float journeySecondsAt1x = 90f;
        public bool loop = true;
        [Range(0f, 1f)] public float startProgress = 0.02f;
        [Min(0.05f)] public float easeSeconds = 0.6f;

        public bool IsRunning { get; private set; }
        public int SpeedMultiplier { get; private set; } = 1;
        public float Progress01 => (_sampler != null && _sampler.Length > 0f) ? Mathf.Clamp01(_distance / _sampler.Length) : 0f;
        public event Action StateChanged;

        private SaBanRouteSampler _sampler;
        private float _distance;
        private float _speedFactor;

        private void OnEnable() { Rebuild(); }

        private void Start()
        {
            Rebuild();
            PlaceAtStart();
        }

        /// <summary>Đo lại tuyến ray (gọi khi đổi route).</summary>
        public void Rebuild()
        {
            _sampler = route != null ? new SaBanRouteSampler(route) : null;
        }

        public void PlaceAtStart()
        {
            if (_sampler == null) Rebuild();
            if (_sampler == null || !_sampler.IsValid) return;
            _distance = _sampler.Length * startProgress;
            _speedFactor = 0f;
            Place();
        }

        public void Play() { if (!IsRunning) { IsRunning = true; StateChanged?.Invoke(); } }
        public void Pause() { if (IsRunning) { IsRunning = false; StateChanged?.Invoke(); } }
        public void Toggle() { if (IsRunning) Pause(); else Play(); }

        public void SetSpeed(int multiplier)
        {
            SpeedMultiplier = Mathf.Clamp(multiplier, 1, 8);
            StateChanged?.Invoke();
        }

        public void ResetToStart()
        {
            IsRunning = false;
            PlaceAtStart();
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (_sampler == null || !_sampler.IsValid) return;

            float target = IsRunning ? SpeedMultiplier : 0f;
            _speedFactor = Mathf.MoveTowards(_speedFactor, target, (4f / Mathf.Max(0.05f, easeSeconds)) * Time.deltaTime);

            if (_speedFactor > 0f)
            {
                float metersPerSecond = _sampler.Length / Mathf.Max(5f, journeySecondsAt1x);
                _distance += metersPerSecond * _speedFactor * Time.deltaTime;
                if (_distance >= _sampler.Length)
                {
                    if (loop) _distance -= _sampler.Length;
                    else
                    {
                        _distance = _sampler.Length;
                        IsRunning = false;
                        _speedFactor = 0f;
                        StateChanged?.Invoke();
                    }
                }
            }
        }

        private void LateUpdate()
        {
            Place();
        }

        /// <summary>Đặt các toa lên ray theo khoảng cách hiện tại. Gọi được cả trong Editor.</summary>
        public void Place()
        {
            if (_sampler == null || !_sampler.IsValid || cars == null) return;
            float unit = route != null ? route.transform.lossyScale.x : 1f; // 1 mét thật = bao nhiêu đơn vị thế giới

            for (int i = 0; i < cars.Length; i++)
            {
                Transform car = cars[i];
                if (car == null) continue;
                float back = (carBackOffsets != null && i < carBackOffsets.Length) ? carBackOffsets[i] : 0f;
                float height = (carHeights != null && i < carHeights.Length) ? carHeights[i] : 0f;

                _sampler.EvaluateWorld(Mathf.Max(0f, _distance - back), out Vector3 pos, out Vector3 tan, out Vector3 up);
                car.position = pos + up * (height * unit);
                car.rotation = Quaternion.LookRotation(tan, up);
            }
        }

        /// <summary>Thông tin ngắn để hiển thị trên bảng.</summary>
        public string StatusText()
        {
            string state = IsRunning ? "ĐANG CHẠY" : "ĐANG DỪNG";
            return state + "  |  x" + SpeedMultiplier + "  |  " + Mathf.RoundToInt(Progress01 * 100f) + "%";
        }
    }
}
