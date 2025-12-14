using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all museum visitor agents and target points in the scene.
/// This script coordinates multiple agents to ensure efficient navigation and target distribution.
/// </summary>
public class MuseumVisitorManager : MonoBehaviour
{
    [Header("Agent Management")]
    [Tooltip("All museum visitor agents in the scene (auto-populated if empty)")]
    public List<MuseumVisitorAgent> visitorAgents = new List<MuseumVisitorAgent>();

    [Tooltip("Automatically find all MuseumVisitorAgent components in the scene on start")]
    public bool autoFindAgents = true;

    [Tooltip("Prefab for spawning new agents (optional)")]
    public GameObject agentPrefab;

    [Tooltip("Maximum number of agents allowed (0 = unlimited)")]
    public int maxAgents = 0;

    [Header("Target Point Management")]
    [Tooltip("All target points in the scene (auto-populated if empty)")]
    public List<MuseumTargetPoint> targetPoints = new List<MuseumTargetPoint>();

    [Tooltip("Automatically find all MuseumTargetPoint components in the scene on start")]
    public bool autoFindTargets = true;

    [Tooltip("Tag name for target points (optional, for filtering)")]
    public string targetTag = "MuseumTarget";

    [Header("Spawn Settings")]
    [Tooltip("Spawn agents at random target points on start")]
    public bool spawnAtTargetPoints = false;

    [Tooltip("Spawn agents at specific spawn points (if empty, uses target points)")]
    public Transform[] spawnPoints;

    [Header("Global Settings")]
    [Tooltip("Global minimum wait time override for all targets (0 = use individual target settings)")]
    public float globalMinWaitTime = 0f;

    [Tooltip("Global maximum wait time override for all targets (0 = use individual target settings)")]
    public float globalMaxWaitTime = 0f;

    [Header("Debug Settings")]
    [Tooltip("Show debug information in the console")]
    public bool showDebugInfo = true;

    [Tooltip("Display target point information in Scene view")]
    public bool showTargetInfo = true;

    // Private variables
    private int currentTargetIndex = 0;
    private Dictionary<MuseumVisitorAgent, int> agentTargetIndices = new Dictionary<MuseumVisitorAgent, int>();

    private void Start()
    {
        InitializeManager();
    }

    /// <summary>
    /// Initializes the manager by finding agents and targets, and setting up the system.
    /// </summary>
    private void InitializeManager()
    {
        // Find all target points
        if (autoFindTargets)
        {
            if (!string.IsNullOrEmpty(targetTag))
            {
                // Find by tag
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(targetTag);
                foreach (var obj in taggedObjects)
                {
                    MuseumTargetPoint target = obj.GetComponent<MuseumTargetPoint>();
                    if (target != null && !targetPoints.Contains(target))
                    {
                        targetPoints.Add(target);
                    }
                }
            }
            else
            {
                // Find all in scene
                MuseumTargetPoint[] foundTargets = FindObjectsOfType<MuseumTargetPoint>();
                targetPoints = new List<MuseumTargetPoint>(foundTargets);
            }

            if (showDebugInfo)
            {
                Debug.Log($"MuseumVisitorManager: Found {targetPoints.Count} target points");
            }
        }

        // Find all agents
        if (autoFindAgents)
        {
            MuseumVisitorAgent[] foundAgents = FindObjectsOfType<MuseumVisitorAgent>();
            visitorAgents = new List<MuseumVisitorAgent>(foundAgents);

            if (showDebugInfo)
            {
                Debug.Log($"MuseumVisitorManager: Found {visitorAgents.Count} visitor agents");
            }
        }

        // Set manager reference for all agents
        foreach (var agent in visitorAgents)
        {
            if (agent != null)
            {
                agent.SetManager(this);
                
                // Initialize agent target index if using ordered visits
                if (!agentTargetIndices.ContainsKey(agent))
                {
                    agentTargetIndices[agent] = 0;
                }
            }
        }

        // Apply global wait time settings if set
        if (globalMinWaitTime > 0 || globalMaxWaitTime > 0)
        {
            ApplyGlobalWaitTimes();
        }

        // Spawn agents if prefab is provided
        if (agentPrefab != null && spawnAtTargetPoints)
        {
            SpawnAgentsAtTargets();
        }
    }

    /// <summary>
    /// Applies global wait time settings to all target points.
    /// </summary>
    private void ApplyGlobalWaitTimes()
    {
        foreach (var target in targetPoints)
        {
            if (globalMinWaitTime > 0)
                target.minWaitTime = globalMinWaitTime;

            if (globalMaxWaitTime > 0)
                target.maxWaitTime = globalMaxWaitTime;
        }
    }

    /// <summary>
    /// Spawns agents at target points or spawn points.
    /// </summary>
    private void SpawnAgentsAtTargets()
    {
        List<Transform> availableSpawnPoints = new List<Transform>();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            availableSpawnPoints.AddRange(spawnPoints);
        }
        else
        {
            // Use target points as spawn points
            foreach (var target in targetPoints)
            {
                if (target != null)
                    availableSpawnPoints.Add(target.transform);
            }
        }

        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogWarning("MuseumVisitorManager: No spawn points available!");
            return;
        }

        int agentsToSpawn = maxAgents > 0 ? Mathf.Min(maxAgents, availableSpawnPoints.Count) : availableSpawnPoints.Count;

        for (int i = 0; i < agentsToSpawn; i++)
        {
            if (visitorAgents.Count >= maxAgents && maxAgents > 0)
                break;

            Transform spawnPoint = availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)];
            GameObject newAgent = Instantiate(agentPrefab, spawnPoint.position, spawnPoint.rotation);
            MuseumVisitorAgent agent = newAgent.GetComponent<MuseumVisitorAgent>();

            if (agent != null)
            {
                visitorAgents.Add(agent);
                agent.SetManager(this);
                agentTargetIndices[agent] = 0;

                // Remove spawn point to avoid multiple agents at same location
                availableSpawnPoints.Remove(spawnPoint);
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"MuseumVisitorManager: Spawned {visitorAgents.Count} agents");
        }
    }

    /// <summary>
    /// Gets all available target points that can be visited.
    /// </summary>
    public List<MuseumTargetPoint> GetAvailableTargets(MuseumVisitorAgent requestingAgent)
    {
        List<MuseumTargetPoint> available = new List<MuseumTargetPoint>();

        foreach (var target in targetPoints)
        {
            if (target != null && target.CanBeVisited())
            {
                // Optionally exclude the current target of the requesting agent
                if (requestingAgent != null && requestingAgent.currentTarget == target)
                    continue;

                available.Add(target);
            }
        }

        return available;
    }

    /// <summary>
    /// Gets the next target in order for sequential visiting.
    /// </summary>
    public MuseumTargetPoint GetNextTargetInOrder(MuseumTargetPoint currentTarget)
    {
        if (targetPoints == null || targetPoints.Count == 0)
            return null;

        if (currentTarget == null)
        {
            // Return first target
            return targetPoints[0];
        }

        int currentIndex = targetPoints.IndexOf(currentTarget);
        if (currentIndex == -1)
            return targetPoints[0];

        // Move to next target (wrap around)
        int nextIndex = (currentIndex + 1) % targetPoints.Count;
        return targetPoints[nextIndex];
    }

    /// <summary>
    /// Gets all target points in the scene.
    /// </summary>
    public List<MuseumTargetPoint> GetTargetPoints()
    {
        return targetPoints;
    }

    /// <summary>
    /// Gets all visitor agents in the scene.
    /// </summary>
    public List<MuseumVisitorAgent> GetVisitorAgents()
    {
        return visitorAgents;
    }

    /// <summary>
    /// Adds a target point to the manager.
    /// </summary>
    public void AddTargetPoint(MuseumTargetPoint target)
    {
        if (target != null && !targetPoints.Contains(target))
        {
            targetPoints.Add(target);
        }
    }

    /// <summary>
    /// Removes a target point from the manager.
    /// </summary>
    public void RemoveTargetPoint(MuseumTargetPoint target)
    {
        if (targetPoints.Contains(target))
        {
            targetPoints.Remove(target);
        }
    }

    /// <summary>
    /// Adds a visitor agent to the manager.
    /// </summary>
    public void AddVisitorAgent(MuseumVisitorAgent agent)
    {
        if (agent != null && !visitorAgents.Contains(agent))
        {
            visitorAgents.Add(agent);
            agent.SetManager(this);
            agentTargetIndices[agent] = 0;
        }
    }

    /// <summary>
    /// Removes a visitor agent from the manager.
    /// </summary>
    public void RemoveVisitorAgent(MuseumVisitorAgent agent)
    {
        if (visitorAgents.Contains(agent))
        {
            visitorAgents.Remove(agent);
            agentTargetIndices.Remove(agent);
        }
    }

    /// <summary>
    /// Stops all agents.
    /// </summary>
    public void StopAllAgents()
    {
        foreach (var agent in visitorAgents)
        {
            if (agent != null)
            {
                agent.Stop();
            }
        }
    }

    /// <summary>
    /// Resumes all agents.
    /// </summary>
    public void ResumeAllAgents()
    {
        foreach (var agent in visitorAgents)
        {
            if (agent != null)
            {
                agent.Resume();
            }
        }
    }

    /// <summary>
    /// Gets statistics about the current state of the system.
    /// </summary>
    public string GetStatistics()
    {
        int movingCount = 0;
        int waitingCount = 0;
        int idleCount = 0;

        foreach (var agent in visitorAgents)
        {
            if (agent == null) continue;

            switch (agent.currentState)
            {
                case MuseumVisitorAgent.VisitorState.Moving:
                    movingCount++;
                    break;
                case MuseumVisitorAgent.VisitorState.Waiting:
                    waitingCount++;
                    break;
                case MuseumVisitorAgent.VisitorState.Idle:
                    idleCount++;
                    break;
            }
        }

        return $"Agents: {visitorAgents.Count} | Moving: {movingCount} | Waiting: {waitingCount} | Idle: {idleCount} | Targets: {targetPoints.Count}";
    }

    private void OnValidate()
    {
        // Ensure max wait time is greater than min wait time
        if (globalMaxWaitTime > 0 && globalMinWaitTime > 0 && globalMaxWaitTime < globalMinWaitTime)
        {
            globalMaxWaitTime = globalMinWaitTime;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showTargetInfo) return;

        // Draw connections between target points
        Gizmos.color = Color.green;
        for (int i = 0; i < targetPoints.Count; i++)
        {
            if (targetPoints[i] == null) continue;

            int nextIndex = (i + 1) % targetPoints.Count;
            if (targetPoints[nextIndex] != null)
            {
                Gizmos.DrawLine(
                    targetPoints[i].transform.position,
                    targetPoints[nextIndex].transform.position
                );
            }
        }
    }

    // Editor helper method (can be called from custom editor if needed)
    [ContextMenu("Refresh Targets and Agents")]
    private void RefreshTargetsAndAgents()
    {
        InitializeManager();
    }
}

