using UnityEngine;

/// <summary>Small playable showcase driver; the same navigation component is also used by Enemy.</summary>
[RequireComponent(typeof(ActorNavigation))]
public sealed class NavigationDemoWalker : MonoBehaviour
{
    public Vector3[] waypoints;
    public float speed = 2.5f;
    public CombatPositionRing combatTarget;
    public float combatRadius = 2.5f;
    private ActorNavigation navigation;
    private int waypoint;
    private void Awake() => navigation = GetComponent<ActorNavigation>();
    private void Update()
    {
        if (combatTarget != null)
        {
            if (combatTarget.TryReserve(navigation, combatRadius, out var position)) navigation.MoveTo(position, speed);
            else navigation.MoveTo(combatTarget.WaitingPosition(navigation, combatRadius), speed);
        }
        else if (waypoints != null && waypoints.Length > 0)
        {
            if (navigation.Status == NavigationStatus.Arrived) waypoint = (waypoint + 1) % waypoints.Length;
            navigation.MoveTo(waypoints[waypoint], speed);
        }
    }
    private void OnDisable() { if (combatTarget != null) combatTarget.Release(navigation); }
}
