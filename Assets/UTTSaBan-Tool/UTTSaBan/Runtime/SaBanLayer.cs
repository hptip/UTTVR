using System;
using UnityEngine;

namespace UTT.SaBan
{
    public enum SaBanLayerKind
    {
        Terrain = 0,
        Nature = 1,
        Rail = 2,
        Bridge = 3,
        Tunnel = 4,
        Station = 5,
        Train = 6,
        Custom = 7
    }

    /// <summary>
    /// Một "lớp" của Sa Bàn (địa hình, ray, hầm, tàu...). Mọi vật thể thuộc lớp nằm bên trong GameObject này.
    /// Nút bật/tắt trên bảng điều khiển hoạt động bằng cách bật/tắt GameObject của lớp.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaBanLayer : MonoBehaviour
    {
        [Tooltip("Mã định danh duy nhất, không dấu, ví dụ: terrain, rail, tunnel.")]
        public string key = "custom";

        [Tooltip("Tên hiển thị trên nút (nên viết HOA), ví dụ: ĐỊA HÌNH.")]
        public string label = "LỚP";

        public SaBanLayerKind kind = SaBanLayerKind.Custom;

        [Tooltip("Có tạo nút bật/tắt cho lớp này trên bảng điều khiển không.")]
        public bool showOnPanel = true;

        [Tooltip("Thứ tự nút trên bảng (số nhỏ đứng trước).")]
        public int panelOrder = 100;

        [Tooltip("Lúc bắt đầu chạy game lớp này đang hiện hay ẩn.")]
        public bool visibleAtStart = true;

        // Optional runtime-only adapter for a verified visual-only layer. Other displays retain SetActive.
        [NonSerialized] public Func<bool> runtimeVisibilityReader;
        [NonSerialized] public Action<bool> runtimeVisibilityWriter;
        public bool IsVisible => runtimeVisibilityReader != null ? runtimeVisibilityReader() : gameObject.activeSelf;

        public event Action<SaBanLayer, bool> VisibilityChanged;

        public void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            if (runtimeVisibilityWriter != null) runtimeVisibilityWriter(visible);
            else gameObject.SetActive(visible);
            VisibilityChanged?.Invoke(this, visible);
        }

        public void Toggle() => SetVisible(!IsVisible);

        /// <summary>Giá trị mặc định cho từng loại lớp chuẩn.</summary>
        public static void GetPreset(SaBanLayerKind kind, out string key, out string label, out int order)
        {
            switch (kind)
            {
                case SaBanLayerKind.Train: key = "train"; label = "TÀU"; order = 5; break;
                case SaBanLayerKind.Terrain: key = "terrain"; label = "ĐỊA HÌNH"; order = 10; break;
                case SaBanLayerKind.Nature: key = "nature"; label = "NÚI/CÂY"; order = 20; break;
                case SaBanLayerKind.Rail: key = "rail"; label = "RAY"; order = 30; break;
                case SaBanLayerKind.Bridge: key = "bridge"; label = "CẦU"; order = 40; break;
                case SaBanLayerKind.Tunnel: key = "tunnel"; label = "HẦM"; order = 50; break;
                case SaBanLayerKind.Station: key = "station"; label = "NHÀ GA"; order = 60; break;
                default: key = "custom"; label = "KHÁC"; order = 100; break;
            }
        }
    }
}
