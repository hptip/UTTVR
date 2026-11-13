using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UTT.SaBan
{
    /// <summary>
    /// Gắn các nút của bảng điều khiển với Sa Bàn lúc chạy game:
    /// chạy/dừng tàu, tốc độ x1-x4, về ga đầu, ẩn/hiện từng lớp.
    /// Mỗi nút bấm được bằng: (1) chuột khi con trỏ đang mở, (2) ngắm tâm + phím E ở góc nhìn thứ nhất.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaBanControlPanel : MonoBehaviour
    {
        public SaBanRoot root;

        [Tooltip("Bảng luôn xoay (chỉ theo trục đứng) để mặt chữ hướng về người chơi.")]
        public bool faceLocalPlayer = true;

        [Min(0.1f)] public float turnSpeed = 8f;

        private static readonly Color LayerOn = new Color(0.10f, 0.52f, 0.48f, 1f);
        private static readonly Color LayerOff = new Color(0.30f, 0.32f, 0.37f, 1f);
        private static readonly Color PlayIdle = new Color(0.12f, 0.55f, 0.28f, 1f);
        private static readonly Color PlayRunning = new Color(0.78f, 0.38f, 0.10f, 1f);
        private static readonly Color SpeedIdle = new Color(0.20f, 0.28f, 0.45f, 1f);
        private static readonly Color SpeedActive = new Color(0.15f, 0.60f, 0.90f, 1f);

        private sealed class LayerBinding
        {
            public SaBanLayer layer;
            public Image image;
            public Text text;
            public SaBanPanelButton interact;
        }

        private Canvas _canvas;
        private Text _status;
        private Text _playLabel;
        private Image _playImage;
        private SaBanPanelButton _playInteract;
        private readonly Image[] _speedImages = new Image[5];
        private readonly List<LayerBinding> _layers = new List<LayerBinding>();
        private float _nextRefresh;

        private SaBanTrainRunner Runner => root != null ? root.trainRunner : null;

        private void Start()
        {
            if (root == null) root = GetComponentInParent<SaBanRoot>();
            _canvas = GetComponent<Canvas>();
            Bind();
            Refresh();
        }

        private void Bind()
        {
            _status = FindText("StatusText");

            Transform play = transform.Find("Btn_Play");
            if (play != null)
            {
                _playLabel = FindText(play, "Label");
                _playImage = play.GetComponent<Image>();
                _playInteract = play.GetComponent<SaBanPanelButton>();
                Hook(play, () => { if (Runner != null) Runner.Toggle(); Refresh(); });
            }

            for (int n = 1; n <= 4; n++)
            {
                int multiplier = n;
                Transform t = transform.Find("Btn_Speed_" + n);
                if (t == null) continue;
                _speedImages[n] = t.GetComponent<Image>();
                Hook(t, () => { if (Runner != null) Runner.SetSpeed(multiplier); Refresh(); });
            }

            Transform reset = transform.Find("Btn_Reset");
            if (reset != null) Hook(reset, () => { if (Runner != null) Runner.ResetToStart(); Refresh(); });

            _layers.Clear();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Btn_Layer_", StringComparison.Ordinal)) continue;
                string key = child.name.Substring("Btn_Layer_".Length);
                SaBanLayer layer = root != null ? root.FindLayer(key) : null;
                if (layer == null) { child.gameObject.SetActive(false); continue; }

                var binding = new LayerBinding
                {
                    layer = layer,
                    image = child.GetComponent<Image>(),
                    text = FindText(child, "Label"),
                    interact = child.GetComponent<SaBanPanelButton>()
                };
                _layers.Add(binding);
                Hook(child, () => { binding.layer.Toggle(); Refresh(); });
            }
        }

        private static void Hook(Transform t, Action action)
        {
            Button button = t.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action());
            }
            SaBanPanelButton interact = t.GetComponent<SaBanPanelButton>();
            if (interact != null) interact.onActivate = action;
        }

        private Text FindText(string objectName)
        {
            Transform t = transform.Find(objectName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Text FindText(Transform parent, string objectName)
        {
            Transform t = parent.Find(objectName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        /// <summary>Cập nhật chữ và màu của tất cả nút theo trạng thái hiện tại.</summary>
        public void Refresh()
        {
            SaBanTrainRunner runner = Runner;
            if (_status != null) _status.text = runner != null ? runner.StatusText() : "CHƯA CÓ TÀU";

            bool running = runner != null && runner.IsRunning;
            if (_playLabel != null) _playLabel.text = running ? "DỪNG TÀU" : "CHẠY TÀU";
            if (_playImage != null) _playImage.color = running ? PlayRunning : PlayIdle;
            if (_playInteract != null) _playInteract.objectName = (running ? "DỪNG TÀU" : "CHẠY TÀU") + "\n[Nhấn E]";

            int speed = runner != null ? runner.SpeedMultiplier : 1;
            for (int n = 1; n <= 4; n++)
            {
                if (_speedImages[n] != null) _speedImages[n].color = (n == speed) ? SpeedActive : SpeedIdle;
            }

            foreach (LayerBinding b in _layers)
            {
                bool visible = b.layer != null && b.layer.IsVisible;
                string label = SaBanPanelFactory.LayerLabel(b.layer, visible);
                if (b.text != null) b.text.text = label;
                if (b.image != null) b.image.color = visible ? LayerOn : LayerOff;
                if (b.interact != null) b.interact.objectName = label + "\n[Nhấn E]";
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.25f;
                Refresh();
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (_canvas != null && _canvas.worldCamera == null && cam != null) _canvas.worldCamera = cam;

            if (!faceLocalPlayer || cam == null) return;
            // Canvas World Space đọc được khi nhìn từ phía -Z của nó, nên "forward" của bảng phải chỉ từ người chơi ra xa.
            Vector3 away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(away.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(turnSpeed * Time.deltaTime));
        }
    }
}
