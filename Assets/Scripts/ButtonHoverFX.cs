using UnityEngine;
using UnityEngine.EventSystems;

using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Cursor")]
    public Texture2D hoverCursor;
    public Vector2 cursorHotspot = Vector2.zero;

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float speed = 14f;

    [Header("Optional: small position nudge (pixels)")]
    [SerializeField] private bool nudgeOnHover = false;
    [SerializeField] private float nudgeY = 2f;

    private RectTransform rt;
    private Vector3 baseScale;
    private Vector2 basePos;
    private Vector3 targetScale;
    private Vector2 targetPos;
    private bool hovering;
    private bool pressed;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseScale = rt.localScale;
        basePos = rt.anchoredPosition;

        targetScale = baseScale;
        targetPos = basePos;
    }

    private void OnEnable()
    {
        // Reset when menu re-opens.
        if (rt == null) rt = GetComponent<RectTransform>();
        rt.localScale = baseScale;
        rt.anchoredPosition = basePos;

        hovering = false;
        pressed = false;
        targetScale = baseScale;
        targetPos = basePos;
    }

    private void Update()
    {
        rt.localScale = Vector3.Lerp(rt.localScale, targetScale, Time.unscaledDeltaTime * speed);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        ApplyTargets();

        if (hoverCursor != null)
            Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressed = false;
        ApplyTargets();

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        ApplyTargets();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        ApplyTargets();
    }

    private void ApplyTargets()
    {
        if (pressed)
            targetScale = baseScale * pressedScale;
        else if (hovering)
            targetScale = baseScale * hoverScale;
        else
            targetScale = baseScale;

        if (nudgeOnHover && hovering && !pressed)
            targetPos = basePos + new Vector2(0f, nudgeY);
        else
            targetPos = basePos;
    }
}