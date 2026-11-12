using UnityEngine;

[RequireComponent(typeof(DynamicNavigationObstacle))]
public sealed class NavigationDemoDoor : MonoBehaviour
{
    [Min(1f)] public float toggleSeconds = 5f;
    private float nextToggle;
    private DynamicNavigationObstacle obstacle;
    private Renderer visual;
    private void Awake()
    {
        obstacle = GetComponent<DynamicNavigationObstacle>();
        visual = GetComponent<Renderer>();
        nextToggle = Time.time + toggleSeconds;
    }
    private void Update()
    {
        if (Time.time < nextToggle) return;
        nextToggle = Time.time + toggleSeconds;
        obstacle.SetBlocking(!obstacle.IsBlocking);
        if (visual != null) visual.enabled = obstacle.IsBlocking;
    }
}
