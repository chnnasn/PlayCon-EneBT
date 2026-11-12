using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Target-owned, capacity-limited combat positions. Reservations never live in a shared SO.</summary>
[DisallowMultipleComponent]
public sealed class CombatPositionRing : MonoBehaviour
{
    [Range(4, 24)] public int slotCount = 12;
    [Min(0f)] public float separationPadding = 0.12f;
    [Min(0.1f)] public float leaseSeconds = 1f;
    private sealed class Reservation
    {
        public ActorNavigation owner;
        public int slot;
        public float radius, expires, validateAt;
        public Vector3 point, center;
    }
    private readonly List<Reservation> reservations = new List<Reservation>();
    private NavMeshPath path;
    private readonly Collider[] overlaps = new Collider[16];
    public int ReservationCount { get { Prune(); return reservations.Count; } }

    public bool TryReserve(ActorNavigation owner, float ringRadius, out Vector3 position)
    {
        position = transform.position;
        if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled || owner.Profile == null) return false;
        Prune();
        for (var index = reservations.Count - 1; index >= 0; index--)
        {
            var existing = reservations[index];
            if (existing.owner != owner) continue;
            if (Time.time < existing.validateAt && (transform.position - existing.center).sqrMagnitude < 0.09f)
            {
                existing.expires = Time.time + leaseSeconds;
                position = existing.point;
                return true;
            }
            if (TryPoint(owner, existing.slot, ringRadius, out position))
            {
                UpdateReservation(existing, position, ringRadius);
                return true;
            }
            reservations.RemoveAt(index);
        }

        var bestScore = float.PositiveInfinity;
        var bestSlot = -1;
        var bestPoint = transform.position;
        for (var slot = 0; slot < slotCount; slot++)
        {
            if (!TryPoint(owner, slot, ringRadius, out var candidate)) continue;
            var score = (candidate - owner.transform.position).sqrMagnitude;
            if (score >= bestScore) continue;
            bestScore = score;
            bestSlot = slot;
            bestPoint = candidate;
        }
        if (bestSlot < 0) return false;
        var reservation = new Reservation { owner = owner, slot = bestSlot };
        UpdateReservation(reservation, bestPoint, ringRadius);
        reservations.Add(reservation);
        position = bestPoint;
        return true;
    }

    private void UpdateReservation(Reservation reservation, Vector3 point, float radius)
    {
        reservation.point = point;
        reservation.center = transform.position;
        reservation.radius = radius;
        reservation.expires = Time.time + leaseSeconds;
        reservation.validateAt = Time.time + 0.4f;
    }

    private bool TryPoint(ActorNavigation owner, int slot, float radius, out Vector3 point)
    {
        var angle = slot * Mathf.PI * 2f / slotCount;
        var raw = transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        point = raw;
        if (!owner.TrySample(raw, out point) || (point - raw).sqrMagnitude > 0.36f) return false;
        foreach (var other in reservations)
        {
            if (other.owner == owner) continue;
            var clearance = owner.Profile.radius + other.owner.Profile.radius + separationPadding;
            var otherAngle = other.slot * Mathf.PI * 2f / slotCount;
            var otherPoint = transform.position + new Vector3(Mathf.Sin(otherAngle), 0f, Mathf.Cos(otherAngle)) * other.radius;
            if ((otherPoint - point).sqrMagnitude < clearance * clearance) return false;
        }
        var bottom = point + Vector3.up * (owner.Profile.radius + 0.06f);
        var top = point + Vector3.up * Mathf.Max(owner.Profile.radius + 0.06f, owner.Profile.height - owner.Profile.radius);
        if (Physics.OverlapCapsuleNonAlloc(bottom, top, owner.Profile.radius, overlaps, owner.Profile.solidMask, QueryTriggerInteraction.Ignore) > 0)
            return false;
        if (path == null) path = new NavMeshPath();
        return owner.CanReach(point, path);
    }

    public Vector3 WaitingPosition(ActorNavigation owner, float radius)
    {
        var direction = owner.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        return transform.position + direction.normalized * (radius + owner.Profile.radius * 2f + 1f);
    }

    public void Release(ActorNavigation owner) => reservations.RemoveAll(item => item.owner == owner);
    private void Prune() => reservations.RemoveAll(item => item.owner == null || !item.owner.isActiveAndEnabled ||
        item.owner.Profile == null || Time.time > item.expires);
    private void OnDisable() => reservations.Clear();
}
