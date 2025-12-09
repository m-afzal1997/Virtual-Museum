using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the behavior of a single museum visitor agent.
/// This script handles navigation, movement, and waiting at target points.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class MuseumVisitorAgent : MonoBehaviour
{
    [Header("NavMesh Agent Settings")]
    [Tooltip("Reference to the NavMeshAgent component (auto-assigned if not set)")]
    private NavMeshAgent navAgent;

    [Header("Movement Settings")]
    [Tooltip("Walking speed of the agent")]
    [Range(0.5f, 5f)]
    public float walkSpeed = 2f;

    [Tooltip("Stopping distance from target (should be close to 0 for precise stopping)")]
    [Range(0f, 1f)]
    public float stoppingDistance = 0.1f;

    [Tooltip("Maximum distance to look for targets")]
    [Range(5f, 50f)]
    public float maxTargetSearchDistance = 30f;

    [Header("Target Selection Settings")]
    [Tooltip("How often (in seconds) the agent checks for new targets")]
    [Range(0.5f, 10f)]
    public float targetSearchInterval = 2f;

    [Tooltip("Should the agent cycle through targets in order?")]
    public bool useTargetOrder = false;

    [Tooltip("Should the agent visit targets randomly?")]
    public bool visitRandomly = true;

    [Header("Animation Settings")]
    [Tooltip("Reference to the Animator component (auto-assigned if not set)")]
    private Animator animator;

    [Tooltip("Name of the speed parameter in the Animator (for walking animation)")]
    public string speedParameterName = "Speed";

    [Tooltip("Name of the isWalking parameter in the Animator (if you have one)")]
    public string isWalkingParameterName = "IsWalking";

    [Header("State Management")]
    [Tooltip("Current state of the agent (read-only for debugging)")]
    public VisitorState currentState = VisitorState.Idle;

    [Tooltip("Current target point the agent is moving to or waiting at")]
    public MuseumTargetPoint currentTarget;

    [Header("Debug Settings")]
    [Tooltip("Show debug lines and info in Scene view")]
    public bool showDebugInfo = true;

    [Tooltip("Color of debug lines")]
    public Color debugLineColor = Color.cyan;

    // Private variables
    private MuseumVisitorManager manager;
    private float lastTargetSearchTime = 0f;
    private float waitEndTime = 0f;
    private bool isWaiting = false;
    private Vector3 lastPosition;
    private float velocity;

    /// <summary>
    /// Enum representing the different states of a visitor agent.
    /// </summary>
    public enum VisitorState
    {
        Idle,           // Not moving, waiting for a target
        Moving,         // Moving towards a target
        Waiting,        // Arrived at target and waiting
        Returning       // Returning to start position (if applicable)
    }

    private void Awake()
    {
        // Get required components
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Find the manager if not assigned
        if (manager == null)
            manager = FindObjectOfType<MuseumVisitorManager>();

        // Initialize NavMeshAgent settings
        if (navAgent != null)
        {
            navAgent.speed = walkSpeed;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.autoBraking = true;
        }

        lastPosition = transform.position;
    }

    private void Start()
    {
        // Set initial state
        currentState = VisitorState.Idle;
        
        // Start looking for targets
        if (manager != null && manager.GetTargetPoints().Count > 0)
        {
            SelectNextTarget();
        }
        else
        {
            Debug.LogWarning($"MuseumVisitorAgent on {gameObject.name}: No MuseumVisitorManager found or no target points available!");
        }
    }

    private void Update()
    {
        UpdateVelocity();
        UpdateAnimation();
        UpdateState();

        // Check if we need to find a new target
        if (Time.time - lastTargetSearchTime >= targetSearchInterval)
        {
            if (currentState == VisitorState.Idle)
            {
                SelectNextTarget();
            }
            lastTargetSearchTime = Time.time;
        }

        // Check if we've reached our destination
        if (currentState == VisitorState.Moving && navAgent != null && !navAgent.pathPending)
        {
            if (navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f)
                {
                    OnReachedTarget();
                }
            }
        }

        // Check if waiting time is over
        if (isWaiting && Time.time >= waitEndTime)
        {
            OnWaitingComplete();
        }
    }

    /// <summary>
    /// Updates the current state based on agent's condition.
    /// </summary>
    private void UpdateState()
    {
        if (isWaiting)
        {
            currentState = VisitorState.Waiting;
        }
        else if (navAgent != null && navAgent.hasPath && navAgent.remainingDistance > navAgent.stoppingDistance)
        {
            currentState = VisitorState.Moving;
        }
        else
        {
            currentState = VisitorState.Idle;
        }
    }

    /// <summary>
    /// Calculates the current velocity for animation purposes.
    /// </summary>
    private void UpdateVelocity()
    {
        velocity = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    /// <summary>
    /// Updates animator parameters based on movement state.
    /// </summary>
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // Update speed parameter
        if (!string.IsNullOrEmpty(speedParameterName))
        {
            animator.SetFloat(speedParameterName, velocity);
        }

        // Update walking boolean
        if (!string.IsNullOrEmpty(isWalkingParameterName))
        {
            animator.SetBool(isWalkingParameterName, currentState == VisitorState.Moving);
        }
    }

    /// <summary>
    /// Selects the next target point to move to.
    /// </summary>
    public void SelectNextTarget()
    {
        if (manager == null)
        {
            Debug.LogWarning($"MuseumVisitorAgent on {gameObject.name}: No manager found!");
            return;
        }

        var availableTargets = manager.GetAvailableTargets(this);
        
        if (availableTargets == null || availableTargets.Count == 0)
        {
            // No targets available, stay idle
            currentState = VisitorState.Idle;
            return;
        }

        MuseumTargetPoint selectedTarget = null;

        if (visitRandomly)
        {
            // Select random target weighted by priority
            selectedTarget = SelectWeightedRandomTarget(availableTargets);
        }
        else if (useTargetOrder)
        {
            // Select next target in order
            selectedTarget = manager.GetNextTargetInOrder(currentTarget);
        }
        else
        {
            // Select nearest target
            selectedTarget = GetNearestTarget(availableTargets);
        }

        if (selectedTarget != null)
        {
            MoveToTarget(selectedTarget);
        }
    }

    /// <summary>
    /// Selects a random target weighted by priority.
    /// </summary>
    private MuseumTargetPoint SelectWeightedRandomTarget(List<MuseumTargetPoint> targets)
    {
        if (targets == null || targets.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var target in targets)
        {
            totalWeight += target.priorityWeight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var target in targets)
        {
            currentWeight += target.priorityWeight;
            if (randomValue <= currentWeight)
            {
                return target;
            }
        }

        // Fallback to last target
        return targets[targets.Count - 1];
    }

    /// <summary>
    /// Gets the nearest target from the list.
    /// </summary>
    private MuseumTargetPoint GetNearestTarget(List<MuseumTargetPoint> targets)
    {
        if (targets == null || targets.Count == 0) return null;

        MuseumTargetPoint nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var target in targets)
        {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < nearestDistance && distance <= maxTargetSearchDistance)
            {
                nearestDistance = distance;
                nearest = target;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Commands the agent to move to a specific target.
    /// </summary>
    public void MoveToTarget(MuseumTargetPoint target)
    {
        if (target == null || navAgent == null)
        {
            Debug.LogWarning($"MuseumVisitorAgent on {gameObject.name}: Cannot move to null target or NavMeshAgent is null!");
            return;
        }

        // Check if target can be visited
        if (!target.CanBeVisited())
        {
            // Try to find another target
            SelectNextTarget();
            return;
        }

        currentTarget = target;
        navAgent.SetDestination(target.transform.position);
        currentState = VisitorState.Moving;
        isWaiting = false;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} is moving to {target.targetName}");
        }
    }

    /// <summary>
    /// Called when the agent reaches its target.
    /// </summary>
    private void OnReachedTarget()
    {
        if (currentTarget == null) return;

        currentTarget.OnVisitorArrived();
        
        float waitTime = currentTarget.GetRandomWaitTime();
        waitEndTime = Time.time + waitTime;
        isWaiting = true;
        currentState = VisitorState.Waiting;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} reached {currentTarget.targetName} and will wait for {waitTime:F1} seconds");
        }

        // Stop the agent
        if (navAgent != null)
        {
            navAgent.isStopped = true;
        }
    }

    /// <summary>
    /// Called when the waiting period at a target is complete.
    /// </summary>
    private void OnWaitingComplete()
    {
        if (currentTarget != null)
        {
            currentTarget.OnVisitorLeft();
        }

        isWaiting = false;
        currentState = VisitorState.Idle;
        currentTarget = null;

        // Resume agent movement
        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        // Select next target
        SelectNextTarget();
    }

    /// <summary>
    /// Sets the manager for this agent.
    /// </summary>
    public void SetManager(MuseumVisitorManager newManager)
    {
        manager = newManager;
    }

    /// <summary>
    /// Forces the agent to stop and reset.
    /// </summary>
    public void Stop()
    {
        if (currentTarget != null)
        {
            currentTarget.OnVisitorLeft();
        }

        currentTarget = null;
        isWaiting = false;
        currentState = VisitorState.Idle;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
    }

    /// <summary>
    /// Resumes the agent's movement.
    /// </summary>
    public void Resume()
    {
        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        if (currentState == VisitorState.Idle)
        {
            SelectNextTarget();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        // Draw line to current target
        if (currentTarget != null && currentState == VisitorState.Moving)
        {
            Gizmos.color = debugLineColor;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            
            // Draw arrow pointing to target
            Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
            Vector3 arrowHead = transform.position + direction * 2f;
            Gizmos.DrawLine(transform.position, arrowHead);
        }
    }

    private void OnDestroy()
    {
        // Clean up when agent is destroyed
        if (currentTarget != null)
        {
            currentTarget.OnVisitorLeft();
        }
    }
}

