using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace UTT.SaBan
{
    /// <summary>
    /// Gốc của một Sa Bàn. Cấu trúc:
    ///   SaBanRoot
    ///    ├─ Table            (mặt bàn + chân bàn)
    ///    ├─ Model            (được phóng/thu theo "Model Scale")
    ///    │   └─ Content      (mô hình, giữ NGUYÊN toạ độ của scene gốc; chứa các lớp)
    ///    └─ SaBan_ControlPanel (bảng nút điều khiển, KHÔNG bị thu nhỏ, di chuyển tự do)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaBanRoot : MonoBehaviour
    {
        [Header("Cấu trúc (do công cụ tạo, thường không cần sửa)")]
        public Transform model;
        public Transform content;
        public SplineContainer route;
        public SaBanTrainRunner trainRunner;
        public Transform panel;

        [Header("Tỉ lệ")]
        [Tooltip("Hệ số thu nhỏ của mô hình. Ví dụ 0.0001 = tỉ lệ 1 : 10000. Có thể kéo trực tiếp khi đang chạy game.")]
        [Min(0.0000001f)] public float modelScale = 0.0001f;

        [Header("Mặt bàn")]
        public Vector2 tableSize = new Vector2(3f, 1.4f);
        public float tableTopHeight = 0.8f;

        public event System.Action<float> ScaleChanged;

        /// <summary>Tỉ lệ thực tế đang áp dụng lên thế giới (đã tính cả scale của cha).</summary>
        public float WorldScale => model != null ? model.lossyScale.x : modelScale;

        private void Awake() { ApplyScale(); }

        private void OnValidate() { ApplyScale(); }

        private void Start()
        {
            // Lớp nào được đặt "ẩn lúc bắt đầu" thì ẩn đi
            foreach (SaBanLayer layer in GetLayers())
            {
                if (!layer.visibleAtStart && layer.gameObject.activeSelf) layer.gameObject.SetActive(false);
            }
        }

        public void SetModelScale(float scale)
        {
            modelScale = Mathf.Max(1e-7f, scale);
            ApplyScale();
        }

        public void ApplyScale()
        {
            if (model == null) return;
            model.localScale = Vector3.one * Mathf.Max(1e-7f, modelScale);
            ScaleChanged?.Invoke(modelScale);
        }

        /// <summary>Danh sách lớp, sắp theo thứ tự nút trên bảng.</summary>
        public List<SaBanLayer> GetLayers()
        {
            var list = new List<SaBanLayer>(GetComponentsInChildren<SaBanLayer>(true));
            list.Sort((a, b) => a.panelOrder.CompareTo(b.panelOrder));
            return list;
        }

        public SaBanLayer FindLayer(string key)
        {
            foreach (SaBanLayer layer in GetComponentsInChildren<SaBanLayer>(true))
                if (layer.key == key) return layer;
            return null;
        }

        public bool SetLayerVisible(string key, bool visible)
        {
            SaBanLayer layer = FindLayer(key);
            if (layer == null) return false;
            layer.SetVisible(visible);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 0.8f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, tableTopHeight, 0f), new Vector3(tableSize.x, 0.005f, tableSize.y));
        }
    }
}
