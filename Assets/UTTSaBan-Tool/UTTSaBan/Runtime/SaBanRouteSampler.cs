using UnityEngine;
using UnityEngine.Splines;

namespace UTT.SaBan
{
    /// <summary>
    /// Đo tuyến ray theo "độ dài thật" (đơn vị cục bộ của spline = mét của scene gốc), nên KHÔNG phụ thuộc
    /// vào việc Sa Bàn đang phóng to hay thu nhỏ. Cho biết vị trí / hướng tại khoảng cách d (mét) dọc ray.
    /// </summary>
    public sealed class SaBanRouteSampler
    {
        private const int DefaultSamples = 2048;

        private readonly SplineContainer _container;
        private readonly Spline _spline;
        private readonly float[] _cum;

        public float Length { get; private set; }
        public bool IsValid => _spline != null && Length > 0.0001f;

        public SaBanRouteSampler(SplineContainer container, int samples = DefaultSamples)
        {
            _container = container;
            _cum = new float[1];
            if (container == null || container.Splines == null || container.Splines.Count == 0) return;

            _spline = container.Splines[0];
            samples = Mathf.Max(16, samples);
            _cum = new float[samples + 1];
            Vector3 previous = LocalPosition(0f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 p = LocalPosition((float)i / samples);
                _cum[i] = _cum[i - 1] + Vector3.Distance(previous, p);
                previous = p;
            }
            Length = _cum[samples];
        }

        private Vector3 LocalPosition(float t) => (Vector3)SplineUtility.EvaluatePosition(_spline, t);

        /// <summary>Đổi khoảng cách (mét) thành tham số t (0..1) của spline.</summary>
        public float TAt(float distance)
        {
            if (!IsValid) return 0f;
            distance = Mathf.Clamp(distance, 0f, Length);
            int low = 0, high = _cum.Length - 1;
            while (low + 1 < high)
            {
                int mid = (low + high) >> 1;
                if (_cum[mid] < distance) low = mid; else high = mid;
            }
            float span = _cum[high] - _cum[low];
            float blend = span > 1e-6f ? (distance - _cum[low]) / span : 0f;
            return Mathf.Lerp((float)low / (_cum.Length - 1), (float)high / (_cum.Length - 1), blend);
        }

        /// <summary>Vị trí, hướng đi, hướng lên - theo toạ độ THẾ GIỚI (đã tính cả tỉ lệ thu nhỏ).</summary>
        public void EvaluateWorld(float distance, out Vector3 position, out Vector3 tangent, out Vector3 up)
        {
            EvaluateLocal(distance, out Vector3 lp, out Vector3 lt, out Vector3 lu);
            Transform tr = _container.transform;
            position = tr.TransformPoint(lp);
            tangent = tr.TransformDirection(lt);
            up = tr.TransformDirection(lu);
            if (tangent.sqrMagnitude < 1e-10f) tangent = tr.forward;
            if (up.sqrMagnitude < 1e-10f) up = Vector3.up;
            tangent.Normalize();
            up.Normalize();
        }

        /// <summary>Vị trí, hướng - theo toạ độ cục bộ của SplineContainer.</summary>
        public void EvaluateLocal(float distance, out Vector3 position, out Vector3 tangent, out Vector3 up)
        {
            float t = TAt(distance);
            position = LocalPosition(t);
            tangent = (Vector3)SplineUtility.EvaluateTangent(_spline, t);
            up = (Vector3)SplineUtility.EvaluateUpVector(_spline, t);
            if (tangent.sqrMagnitude < 1e-10f) tangent = Vector3.forward;
            if (up.sqrMagnitude < 1e-10f) up = Vector3.up;
        }
    }
}
