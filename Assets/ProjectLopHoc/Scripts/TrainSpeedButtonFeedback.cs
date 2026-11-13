using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Clear and persistent pointer feedback for the train speed level buttons.</summary>
[DisallowMultipleComponent]
public sealed class TrainSpeedButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = new Color(0.08f, 0.35f, 0.5f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.78f, 0.08f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.12f, 0.8f, 0.58f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.15f, 0.72f, 1f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.18f, 0.22f, 0.25f, 1f);

    private Button _button;
    private bool _pointerInside;
    private bool _pointerDown;
    private static TrainSpeedButtonFeedback _lastClicked;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (targetGraphic == null && _button != null)
            targetGraphic = _button.targetGraphic;

        if (_button != null)
            _button.transition = Selectable.Transition.None;

        ApplyVisual();
    }

    private void OnEnable() => ApplyVisual();

    private void OnDisable()
    {
        _pointerInside = false;
        _pointerDown = false;
        if (_lastClicked == this)
            _lastClicked = null;
        ApplyVisual();
    }

    private void OnDestroy()
    {
        if (_lastClicked == this)
            _lastClicked = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        ApplyVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        _pointerDown = false;
        ApplyVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDown = true;
        ApplyVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDown = false;
        ApplyVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null || !_button.IsInteractable()) return;

        TrainSpeedButtonFeedback previous = _lastClicked;
        _lastClicked = this;
        if (previous != null && previous != this)
            previous.ApplyVisual();
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (targetGraphic == null) return;

        Color color = _button != null && !_button.IsInteractable()
            ? disabledColor
            : _pointerDown
                ? pressedColor
                : _pointerInside
                    ? hoverColor
                    : _lastClicked == this
                        ? selectedColor
                        : normalColor;

        targetGraphic.color = color;
    }
}
