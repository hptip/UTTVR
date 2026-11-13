using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mouse/touch push-to-talk input (chỉ chuột TRÁI). Nhả ngoài nút = hủy.
/// Chỉ phát sự kiện Pressed/Released/Cancelled (IVoiceTrigger); không biết gì về ghi âm.
/// Một XRVoiceTrigger sau này chỉ cần implement cùng interface.
/// </summary>
public sealed class PointerVoiceTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler, IVoiceTrigger
{
    public event Action Pressed;
    public event Action Released;
    public event Action Cancelled;

    [SerializeField] private bool cancelWhenReleasedOutside = true;

    private int activePointerId = int.MinValue;
    private bool pointerInside = true;

    public bool CancelWhenReleasedOutside => cancelWhenReleasedOutside;
    /// <summary>Đang có một pointer giữ nút.</summary>
    public bool IsHeld => activePointerId != int.MinValue;
    /// <summary>Pointer đang giữ nút có còn nằm trên nút không (dùng cho gợi ý "thả để hủy").</summary>
    public bool IsPointerInside => pointerInside;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Chỉ chuột trái (touch được Unity quy về Left). Chuột phải/giữa không kích hoạt ghi âm.
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (activePointerId != int.MinValue) return;
        activePointerId = eventData.pointerId;
        pointerInside = true;
        Pressed?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId) pointerInside = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId) pointerInside = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        bool releasedInside = pointerInside && eventData.pointerEnter != null &&
                              (eventData.pointerEnter == gameObject || eventData.pointerEnter.transform.IsChildOf(transform));
        activePointerId = int.MinValue;
        pointerInside = true;
        if (cancelWhenReleasedOutside && !releasedInside) Cancelled?.Invoke();
        else Released?.Invoke();
    }

    private void OnDisable()
    {
        if (activePointerId == int.MinValue) return;
        activePointerId = int.MinValue;
        pointerInside = true;
        Cancelled?.Invoke();
    }
}
