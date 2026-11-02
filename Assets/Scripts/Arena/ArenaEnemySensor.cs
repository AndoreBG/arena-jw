using System;
using UnityEngine;

[Serializable]
public struct ArenaEnemySnapshot
{
    public Transform target;
    public float distance;
    public bool visible;
    public Vector3 lastSeenPosition;
    public bool hasLastSeenPosition;
}

/// <summary>Sensor sem autoridade de movimento; memoriza apenas posições realmente vistas.</summary>
public sealed class ArenaEnemySensor : MonoBehaviour
{
    [SerializeField] private Transform eyes;
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float viewDistance = 24f;
    [SerializeField, Range(0f, 360f)] private float fieldOfView = 120f;
    [SerializeField] private float targetHeight = 1.1f;
    [SerializeField] private LayerMask blockingMask = ~0;
    private Vector3 lastSeen;
    private bool hasLastSeen;
    private ArenaEnemySnapshot snapshot;
    public ArenaEnemySnapshot Snapshot => snapshot;
    public Transform Target { get => target; set => target = value; }
    public void SetTarget(Transform value) { target = value; }

    public ArenaEnemySnapshot Observe(Transform newTarget)
    {
        target = newTarget;
        bool visible = false;
        float distance = float.PositiveInfinity;
        if (target != null)
        {
            Vector3 origin = eyes != null ? eyes.position : transform.position + Vector3.up;
            Vector3 point = target.position + Vector3.up * targetHeight;
            Vector3 delta = point - origin;
            distance = delta.magnitude;
            Vector3 flatForward = (eyes != null ? eyes.forward : transform.forward);
            if (distance <= viewDistance && distance > 0.001f && Vector3.Angle(flatForward, delta) <= fieldOfView * 0.5f)
            {
                // RaycastAll ensures the sensor can ignore its own hierarchy and see the target collider.
                RaycastHit[] hits = Physics.RaycastAll(origin, delta / distance, distance, blockingMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                visible = true;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                    if (hit.transform == target || hit.transform.IsChildOf(target)) break;
                    // Hit a non-target solid object before the player.
                    visible = false;
                    break;
                }
                if (visible) { lastSeen = target.position; hasLastSeen = true; }
            }
        }
        snapshot = new ArenaEnemySnapshot { target = target, distance = distance, visible = visible, lastSeenPosition = lastSeen, hasLastSeenPosition = hasLastSeen };
        return snapshot;
    }

    public void InvalidateMemory() { hasLastSeen = false; snapshot.hasLastSeenPosition = false; }
    private void OnValidate() { viewDistance = Mathf.Max(0f, viewDistance); fieldOfView = Mathf.Clamp(fieldOfView, 0f, 360f); }
}

public enum ArenaEnemyAction { Patrol, Search, Pursue, Attack }

public struct ArenaEnemyDecisionInput
{
    public bool visible, hasMemory, attackInRange;
    public ArenaEnemyAction previous;
}

/// <summary>Lógica pura da seleção ponderada (útil também para testes Edit Mode).</summary>
public static class ArenaEnemyDecision
{
    public static ArenaEnemyAction Choose(ArenaEnemyDecisionInput input, int searchWeight, int pursueWeight, int attackWeight)
    {
        bool attack = input.visible && input.attackInRange;
        bool pursue = input.visible;
        bool search = !input.visible && input.hasMemory;
        // Deterministic tie priority: Attack > Pursue > Search. Patrol is fallback only.
        ArenaEnemyAction best = ArenaEnemyAction.Patrol;
        int bestWeight = int.MinValue;
        Consider(ArenaEnemyAction.Search, search, searchWeight, ref best, ref bestWeight);
        Consider(ArenaEnemyAction.Pursue, pursue, pursueWeight, ref best, ref bestWeight);
        Consider(ArenaEnemyAction.Attack, attack, attackWeight, ref best, ref bestWeight);
        return best;
    }
    private static void Consider(ArenaEnemyAction candidate, bool applicable, int weight, ref ArenaEnemyAction best, ref int bestWeight)
    { if (applicable && weight >= bestWeight) { best = candidate; bestWeight = weight; } }
}
