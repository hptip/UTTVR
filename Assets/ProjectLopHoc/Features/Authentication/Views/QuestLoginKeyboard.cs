using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProjectLopHoc.Features.Authentication.Views
{
    /// <summary>Uses the platform keyboard on Android/Quest and provides a tracked-device 3D fallback elsewhere.</summary>
    public sealed class QuestLoginKeyboard : MonoBehaviour
    {
        private TMP_InputField _nameField;
        private TMP_InputField _passwordField;
        private Transform _canvasTransform;
        private TMP_InputField _activeField;
        private TouchScreenKeyboard _nativeKeyboard;
        private GameObject _fallbackRoot;
        private bool _shift;
        private int _selectionAnchor;
        private int _selectionFocus;
        private bool _hasSelection;

        public void Initialize(TMP_InputField nameField, TMP_InputField passwordField, Transform canvasTransform)
        {
            _nameField = nameField;
            _passwordField = passwordField;
            _canvasTransform = canvasTransform;
            if (_nameField != null) _nameField.onSelect.AddListener(OnNameSelected);
            if (_passwordField != null) _passwordField.onSelect.AddListener(OnPasswordSelected);
        }

        private void OnNameSelected(string _) => OpenFor(_nameField, false);
        private void OnPasswordSelected(string _) => OpenFor(_passwordField, true);

        private void OpenFor(TMP_InputField field, bool password)
        {
            if (field == null) return;

            bool keyboardAlreadyOpen = _activeField == field &&
                                       (_nativeKeyboard != null || (_fallbackRoot != null && _fallbackRoot.activeSelf));
            if (keyboardAlreadyOpen)
            {
                CaptureSelection(field);
                return;
            }

            CloseKeyboard(false);
            _activeField = field;
            CaptureSelection(field);

            // Quest reports the Android soft keyboard as supported, but the
            // TouchScreenKeyboard overlay is not reliably surfaced over an XR
            // application. Use the tracked-device keyboard in an active headset.
            bool inHeadset = XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android;
            if (Application.isMobilePlatform && TouchScreenKeyboard.isSupported && !inHeadset)
            {
                _nativeKeyboard = TouchScreenKeyboard.Open(field.text, TouchScreenKeyboardType.Default,
                    false, false, password, false, password ? "Mật khẩu" : "Mã tài khoản");
                if (_nativeKeyboard != null)
                {
                    Debug.Log($"[QuestLoginKeyboard] Opened platform keyboard for {field.name}.");
                    return;
                }
            }

            Debug.Log($"[QuestLoginKeyboard] Showing tracked 3D keyboard for {field.name}; XR active={inHeadset}.");
            ShowFallback();
        }

        private void Update()
        {
            if (_nativeKeyboard == null || _activeField == null) return;
            if (_nativeKeyboard.text != _activeField.text)
                _activeField.SetTextWithoutNotify(_nativeKeyboard.text);

            if (_nativeKeyboard.status == TouchScreenKeyboard.Status.Done ||
                _nativeKeyboard.status == TouchScreenKeyboard.Status.Canceled ||
                _nativeKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
                CloseKeyboard(false);
        }

        private void ShowFallback()
        {
            if (_fallbackRoot == null) BuildFallback();
            _fallbackRoot.SetActive(true);
        }

        private void BuildFallback()
        {
            var root = new GameObject("Quest 3D Keyboard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1200f, 440f);
            root.transform.localScale = Vector3.one * 0.00075f;
            if (_canvasTransform != null)
            {
                // Keep the full keyboard in the headset's view and in front of the
                // login canvas. The old 0.76 m offset placed most keys below the view.
                root.transform.SetPositionAndRotation(_canvasTransform.position - _canvasTransform.up * 0.20f + _canvasTransform.forward * 0.08f, _canvasTransform.rotation);
            }
            else
            {
                Camera camera = Camera.main;
                if (camera != null) root.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 1.3f, Quaternion.LookRotation(root.transform.position - camera.transform.position));
            }

            var background = new GameObject("Keyboard Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 0.97f);

            AddRow(root.transform, "1234567890", -150f, 82f);
            AddRow(root.transform, "QWERTYUIOP", -55f, 82f);
            AddRow(root.transform, "ASDFGHJKL", 40f, 82f);
            AddRow(root.transform, "ZXCVBNM", 135f, 82f);
            AddSpecialKey(root.transform, "Shift", -390f, 135f, 150f, 82f, ToggleShift);
            AddSpecialKey(root.transform, "⌫", 390f, 135f, 150f, 82f, Backspace);
            AddSpecialKey(root.transform, "Clear", -500f, 235f, 160f, 82f, ClearActiveField);
            AddSpecialKey(root.transform, "Space", -120f, 235f, 420f, 82f, () => Insert(" "));
            AddSpecialKey(root.transform, "Enter", 245f, 235f, 220f, 82f, Submit);
            AddSpecialKey(root.transform, "Close", 505f, 235f, 150f, 82f, () => CloseKeyboard(true));
            _fallbackRoot = root;
        }

        private void CaptureSelection(TMP_InputField field)
        {
            if (field == null) return;
            int textLength = field.text != null ? field.text.Length : 0;
            _selectionAnchor = Mathf.Clamp(field.selectionStringAnchorPosition, 0, textLength);
            _selectionFocus = Mathf.Clamp(field.selectionStringFocusPosition, 0, textLength);
            _hasSelection = true;
        }

        private void GetEditRange(out int start, out int end)
        {
            int textLength = _activeField.text != null ? _activeField.text.Length : 0;
            if (_activeField.isFocused || !_hasSelection)
                CaptureSelection(_activeField);

            start = Mathf.Clamp(Mathf.Min(_selectionAnchor, _selectionFocus), 0, textLength);
            end = Mathf.Clamp(Mathf.Max(_selectionAnchor, _selectionFocus), 0, textLength);
        }

        private void SetTextAndCaret(string text, int caret)
        {
            _activeField.text = text;
            caret = Mathf.Clamp(caret, 0, text.Length);
            _activeField.selectionStringAnchorPosition = caret;
            _activeField.selectionStringFocusPosition = caret;
            _activeField.ForceLabelUpdate();
            _selectionAnchor = _selectionFocus = caret;
            _hasSelection = true;
            if (_nativeKeyboard != null) _nativeKeyboard.text = text;
        }

        private void AddRow(Transform parent, string keys, float y, float width)
        {
            float step = width + 10f;
            float start = -(keys.Length - 1) * step * 0.5f;
            for (int i = 0; i < keys.Length; i++)
            {
                char key = keys[i];
                AddSpecialKey(parent, _shift ? char.ToUpperInvariant(key).ToString() : key.ToString(), start + i * step, y, width, 82f,
                    () => Insert(_shift ? char.ToUpperInvariant(key).ToString() : char.ToLowerInvariant(key).ToString()));
            }
        }

        private void AddSpecialKey(Transform parent, string caption, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Key " + caption, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.19f, 0.24f, 0.34f, 1f);
            go.GetComponent<Button>().onClick.AddListener(action);

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = caption;
            text.fontSize = 35f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        private void Insert(string value)
        {
            if (_activeField == null) return;
            GetEditRange(out int start, out int end);
            string text = _activeField.text ?? string.Empty;
            string updated = text.Remove(start, end - start).Insert(start, value);
            SetTextAndCaret(updated, start + value.Length);
        }

        private void Backspace()
        {
            if (_activeField == null || string.IsNullOrEmpty(_activeField.text)) return;
            GetEditRange(out int start, out int end);
            if (start == end)
            {
                if (start == 0) return;
                start--;
            }
            string updated = _activeField.text.Remove(start, end - start);
            SetTextAndCaret(updated, start);
        }

        private void ClearActiveField()
        {
            if (_activeField == null) return;
            SetTextAndCaret(string.Empty, 0);
        }

        private void ToggleShift()
        {
            _shift = !_shift;
            if (_fallbackRoot == null) return;
            foreach (Transform child in _fallbackRoot.transform)
            {
                if (!child.name.StartsWith("Key ") || child.name.Length != 5) continue;
                var label = child.Find("Label");
                var text = label != null ? label.GetComponent<TextMeshProUGUI>() : null;
                if (text != null) text.text = _shift ? child.name.Substring(4).ToUpperInvariant() : child.name.Substring(4).ToLowerInvariant();
            }
        }
        private void Submit() => CloseKeyboard(true);

        private void CloseKeyboard(bool deselect)
        {
            if (_nativeKeyboard != null)
            {
                _nativeKeyboard.active = false;
                _nativeKeyboard = null;
            }
            if (_fallbackRoot != null) _fallbackRoot.SetActive(false);
            if (deselect && _activeField != null) _activeField.DeactivateInputField();
            _activeField = null;
            _hasSelection = false;
        }

        private void OnDestroy()
        {
            if (_nameField != null) _nameField.onSelect.RemoveListener(OnNameSelected);
            if (_passwordField != null) _passwordField.onSelect.RemoveListener(OnPasswordSelected);
        }
    }
}
