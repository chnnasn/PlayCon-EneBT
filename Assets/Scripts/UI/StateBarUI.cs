using UnityEngine;
using UnityEngine.UI;

public class StateBarUI : MonoBehaviour, IUI
{
    [SerializeField] private Image RedBloodBar;
    [SerializeField] private Image GreenBloodBar;
    [SerializeField, Min(0.01f)] private float damageTrailSpeed = 0.5f;
    private Camera viewCamera;
    private Canvas parentCanvas;
    private CanvasGroup visibility;
    private RectTransform rectTransform;
    private float displayedHealth = 1f;

    public void Init()
    {
        rectTransform = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        visibility = GetComponent<CanvasGroup>();
        if (visibility == null)
            visibility = gameObject.AddComponent<CanvasGroup>();
        visibility.interactable = false;
        visibility.blocksRaycasts = false;
        if (RedBloodBar != null)
            RedBloodBar.raycastTarget = false;
        if (GreenBloodBar != null)
            GreenBloodBar.raycastTarget = false;
    }

    private void Awake()
    {
        Init();
    }

    private void Update()
    {
        if (RedBloodBar != null)
            RedBloodBar.fillAmount = Mathf.MoveTowards(RedBloodBar.fillAmount, displayedHealth, damageTrailSpeed * Time.unscaledDeltaTime);
    }

    public void UpdateBlood(float percentage, bool immediate = false)
    {
        displayedHealth = float.IsNaN(percentage) || float.IsInfinity(percentage) ? 0f : Mathf.Clamp01(percentage);
        if (GreenBloodBar != null)
            GreenBloodBar.fillAmount = displayedHealth;
        if (RedBloodBar != null && (immediate || displayedHealth > RedBloodBar.fillAmount))
            RedBloodBar.fillAmount = displayedHealth;
    }

    public void SetVisible(bool visible)
    {
        if (visibility == null)
            Init();
        visibility.alpha = visible ? 1f : 0f;
    }

    public void ShowAt(Vector3 worldPos)
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
            viewCamera = Camera.main;
        if (viewCamera == null || parentCanvas == null || rectTransform == null)
        {
            SetVisible(false);
            return;
        }

        var viewport = viewCamera.WorldToViewportPoint(worldPos);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            SetVisible(false);
            return;
        }

        var screenPoint = viewCamera.WorldToScreenPoint(worldPos);
        var canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (rectTransform.parent is RectTransform parent && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, canvasCamera, out var localPoint))
            rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);

        var distance = Mathf.Max(0.1f, Vector3.Distance(worldPos, viewCamera.transform.position));
        transform.localScale = Vector3.one * Mathf.Clamp(24f / distance, 0.5f, 8f);
        SetVisible(true);
    }
}
