using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ULTRASTATS;

internal sealed class UltraStatsScrollbarDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ScrollRect ScrollRect = null!;
    public RectTransform TrackRect = null!;
    public RectTransform HandleRect = null!;
    public float MinHandleHeight = 72f;

    private bool _dragging;
    private float _pointerOffsetY;

    private void LateUpdate()
    {
        RefreshHandle();
    }

    public void RefreshHandle()
    {
        if (ScrollRect == null || TrackRect == null || HandleRect == null || ScrollRect.content == null || ScrollRect.viewport == null)
            return;

        float trackHeight = TrackRect.rect.height;
        if (trackHeight <= 0f)
            return;

        float contentHeight = Mathf.Max(1f, ScrollRect.content.rect.height);
        float viewportHeight = Mathf.Max(1f, ScrollRect.viewport.rect.height);
        float visibleRatio = Mathf.Clamp01(viewportHeight / contentHeight);
        float handleHeight = Mathf.Clamp(trackHeight * visibleRatio, MinHandleHeight, trackHeight);

        HandleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, handleHeight);

        float travel = Mathf.Max(0f, trackHeight - handleHeight);
        float centerY = travel <= 0f
            ? 0f
            : Mathf.Lerp(-travel * 0.5f, travel * 0.5f, ScrollRect.verticalNormalizedPosition);

        HandleRect.anchoredPosition = new Vector2(0f, centerY);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (TrackRect == null || HandleRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                TrackRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float halfHandle = HandleRect.rect.height * 0.5f;
        float handleCenter = HandleRect.anchoredPosition.y;
        bool clickedHandle = Mathf.Abs(localPoint.y - handleCenter) <= halfHandle;

        _pointerOffsetY = clickedHandle ? (handleCenter - localPoint.y) : 0f;
        SetFromLocalCenter(localPoint.y + _pointerOffsetY);
        _dragging = true;
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || TrackRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                TrackRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            SetFromLocalCenter(localPoint.y + _pointerOffsetY);
            eventData.Use();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
    }

    private void SetFromLocalCenter(float handleCenterY)
    {
        if (ScrollRect == null || TrackRect == null || HandleRect == null)
            return;

        float trackHeight = TrackRect.rect.height;
        float handleHeight = HandleRect.rect.height;
        float travel = Mathf.Max(0f, trackHeight - handleHeight);

        float clampedCenter = Mathf.Clamp(handleCenterY, -travel * 0.5f, travel * 0.5f);
        float normalized = travel <= 0f
            ? 1f
            : (clampedCenter + travel * 0.5f) / travel;

        ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        ScrollRect.velocity = Vector2.zero;
        RefreshHandle();
    }
}

internal sealed class UltraStatsFixedWheelScroll : MonoBehaviour, IScrollHandler
{
    public ScrollRect ScrollRect = null!;
    public float PixelsPerWheelTick = 96f;

    public void OnScroll(PointerEventData eventData)
    {
        if (ScrollRect == null || ScrollRect.content == null || ScrollRect.viewport == null)
            return;

        float direction = Mathf.Sign(eventData.scrollDelta.y);
        if (Mathf.Approximately(direction, 0f))
            return;

        float contentHeight = ScrollRect.content.rect.height;
        float viewportHeight = ScrollRect.viewport.rect.height;
        float scrollableHeight = Mathf.Max(0f, contentHeight - viewportHeight);
        if (scrollableHeight <= 0f)
            return;

        float normalizedStep = Mathf.Max(1f, PixelsPerWheelTick) / scrollableHeight;
        ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            ScrollRect.verticalNormalizedPosition + direction * normalizedStep
        );
        ScrollRect.velocity = Vector2.zero;
        eventData.Use();
    }
}
