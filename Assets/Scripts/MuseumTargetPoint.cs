using UnityEngine;

/// <summary>
/// Represents a target point where museum visitors can stop and observe exhibits.
/// Attach this script to GameObjects in the scene to mark them as potential destinations.
/// </summary>
[System.Serializable]
public class MuseumTargetPoint : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The name or ID of this target point (for debugging)")]
    public string targetName = "Target Point";

    [Tooltip("The priority weight of this target (higher = more likely to be visited)")]
    [Range(0.1f, 10f)]
    public float priorityWeight = 1f;

    [Header("Wait Time Settings")]
    [Tooltip("Minimum time (in seconds) visitors will wait at this point")]
    public float minWaitTime = 2f;

    [Tooltip("Maximum time (in seconds) visitors will wait at this point")]
    public float maxWaitTime = 8f;

    [Header("Gizmo Settings")]
    [Tooltip("Color of the gizmo sphere in the Scene view")]
    public Color gizmoColor = Color.yellow;

    [Tooltip("Size of the gizmo sphere")]
    [Range(0.1f, 2f)]
    public float gizmoSize = 0.5f;

    [Header("Optional Settings")]
    [Tooltip("If enabled, only one visitor can visit this point at a time")]
    public bool isExclusive = false;

    [Tooltip("Maximum number of visitors allowed at this point simultaneously (0 = unlimited)")]
    public int maxVisitors = 0;

    // Runtime data
    private int currentVisitors = 0;

    /// <summary>
    /// Gets a random wait time between min and max wait time for this target.
    /// </summary>
    public float GetRandomWaitTime()
    {
        return Random.Range(minWaitTime, maxWaitTime);
    }

    /// <summary>
    /// Checks if a visitor can visit this target (if it's exclusive and already occupied).
    /// </summary>
    public bool CanBeVisited()
    {
        if (isExclusive && currentVisitors > 0)
            return false;

        if (maxVisitors > 0 && currentVisitors >= maxVisitors)
            return false;

        return true;
    }

    /// <summary>
    /// Called when a visitor arrives at this target.
    /// </summary>
    public void OnVisitorArrived()
    {
        currentVisitors++;
    }

    /// <summary>
    /// Called when a visitor leaves this target.
    /// </summary>
    public void OnVisitorLeft()
    {
        currentVisitors = Mathf.Max(0, currentVisitors - 1);
    }

    /// <summary>
    /// Gets the current number of visitors at this target.
    /// </summary>
    public int GetCurrentVisitors()
    {
        return currentVisitors;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoSize);
        
        // Draw a line upward to show the target point
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * gizmoSize * 2);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.5f);
    }

    private void OnValidate()
    {
        // Ensure maxWaitTime is always greater than minWaitTime
        if (maxWaitTime < minWaitTime)
            maxWaitTime = minWaitTime;
    }
}

