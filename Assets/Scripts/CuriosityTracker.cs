using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tracks curiosity indicators: object revisitation, exploration patterns, room sequence
/// Distinguishes curiosity from engagement
/// </summary>
public class CuriosityTracker : MonoBehaviour
{
    public static CuriosityTracker Instance { get; private set; }

    [Header("Curiosity Thresholds")]
    [Tooltip("How many visits to an object indicates curiosity?")]
    public int revisitThreshold = 2;

    [Tooltip("Time window for revisit detection (seconds)")]
    public float revisitTimeWindow = 60f;

    [Tooltip("Systematic exploration threshold (0-1, higher = more systematic)")]
    [Range(0f, 1f)]
    public float systematicThreshold = 0.7f;

    [Tooltip("Random exploration threshold (0-1, lower = more random)")]
    [Range(0f, 1f)]
    public float randomThreshold = 0.3f;

    [Header("Room Adjacency")]
    [Tooltip("Define which rooms are adjacent (for systematic exploration detection)")]
    public List<RoomConnection> roomConnections = new List<RoomConnection>
    {
        new RoomConnection("balcony", "bedroom"),
        new RoomConnection("bedroom", "guest_room"),
        new RoomConnection("guest_room", "kitchen"),
        new RoomConnection("kitchen", "workshop"),
        new RoomConnection("workshop", "archive")
    };

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool showDetailedLogs = false;

    // Tracking data
    private Dictionary<string, ObjectVisitData> objectVisits = new Dictionary<string, ObjectVisitData>();
    private List<RoomVisit> roomSequence = new List<RoomVisit>();
    private string currentRoom = "";
    private ExplorationStyle explorationStyle = ExplorationStyle.Mixed;
    private int totalCuriousObjects = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (showDebugLogs)
        {
            Debug.Log("✅ [CuriosityTracker] Initialized - Tracking object revisits and exploration patterns");
        }
    }

    // ============================================================================
    // OBJECT CURIOSITY TRACKING
    // ============================================================================

    /// <summary>
    /// Called when user views/interacts with an object
    /// </summary>
    public void OnObjectViewed(string objectId, string objectName)
    {
        // Initialize if first time
        if (!objectVisits.ContainsKey(objectId))
        {
            objectVisits[objectId] = new ObjectVisitData
            {
                objectId = objectId,
                objectName = objectName,
                visitCount = 0,
                visitTimes = new List<float>()
            };
        }

        // Record visit
        objectVisits[objectId].visitCount++;
        objectVisits[objectId].visitTimes.Add(Time.time);
        objectVisits[objectId].lastVisitTime = Time.time;

        // Check if this indicates curiosity
        if (objectVisits[objectId].visitCount >= revisitThreshold)
        {
            // Check if visits are within time window (not just scattered)
            float now = Time.time;
            int recentVisitCount = 0;
            List<float> visitTimes = objectVisits[objectId].visitTimes;
            for (int i = 0; i < visitTimes.Count; i++)
            {
                if (now - visitTimes[i] < revisitTimeWindow)
                    recentVisitCount++;
            }

            if (recentVisitCount >= revisitThreshold && !objectVisits[objectId].markedAsCurious)
            {
                objectVisits[objectId].markedAsCurious = true;
                totalCuriousObjects++;

                if (showDebugLogs)
                {
                    Debug.Log($"🤔 [CuriosityTracker] CURIOSITY DETECTED for '{objectName}' (visited {objectVisits[objectId].visitCount} times)");
                }

                // Log to Firebase
                LogCuriousObjectToFirebase(objectId, objectName, objectVisits[objectId].visitCount);
            }
        }

        if (showDetailedLogs)
        {
            Debug.Log($"[CuriosityTracker] Object '{objectName}' viewed (visit #{objectVisits[objectId].visitCount})");
        }
    }

    /// <summary>
    /// Check if user has shown curiosity about a specific object
    /// </summary>
    public bool IsObjectOfCuriosity(string objectId)
    {
        return objectVisits.ContainsKey(objectId) && objectVisits[objectId].markedAsCurious;
    }

    /// <summary>
    /// Get how many times an object was visited
    /// </summary>
    public int GetObjectVisitCount(string objectId)
    {
        return objectVisits.ContainsKey(objectId) ? objectVisits[objectId].visitCount : 0;
    }

    /// <summary>
    /// Get total number of objects that sparked curiosity
    /// </summary>
    public int GetCuriousObjectCount()
    {
        return totalCuriousObjects;
    }

    // ============================================================================
    // EXPLORATION PATTERN TRACKING
    // ============================================================================

    /// <summary>
    /// Called when user enters a new room
    /// </summary>
    public void OnRoomEntered(string roomId)
    {
        if (roomId == currentRoom) return; // Already in this room

        currentRoom = roomId;
        roomSequence.Add(new RoomVisit
        {
            roomId = roomId,
            enterTime = Time.time
        });

        if (showDetailedLogs)
        {
            Debug.Log($"[CuriosityTracker] Entered room: {roomId} (Room #{roomSequence.Count})");
        }

        // Analyze exploration pattern if we have enough data
        if (roomSequence.Count >= 3)
        {
            AnalyzeExplorationPattern();
        }
    }

    /// <summary>
    /// Analyze user's exploration pattern: Systematic vs Random
    /// </summary>
    void AnalyzeExplorationPattern()
    {
        if (roomSequence.Count < 2) return;

        int adjacentTransitions = 0;
        int totalTransitions = roomSequence.Count - 1;

        // Check how many transitions were to adjacent rooms
        for (int i = 1; i < roomSequence.Count; i++)
        {
            string fromRoom = roomSequence[i - 1].roomId;
            string toRoom = roomSequence[i].roomId;

            if (AreRoomsAdjacent(fromRoom, toRoom))
            {
                adjacentTransitions++;
            }
        }

        // Calculate systematicness ratio
        float systematicRatio = (float)adjacentTransitions / totalTransitions;

        // Determine exploration style
        ExplorationStyle newStyle;
        if (systematicRatio >= systematicThreshold)
            newStyle = ExplorationStyle.Systematic;
        else if (systematicRatio <= randomThreshold)
            newStyle = ExplorationStyle.Random;
        else
            newStyle = ExplorationStyle.Mixed;

        // Log if style changed
        if (newStyle != explorationStyle)
        {
            explorationStyle = newStyle;
            if (showDebugLogs)
            {
                Debug.Log($"[CuriosityTracker] Exploration style: <b>{explorationStyle}</b> (systematic ratio: {systematicRatio:F2})");
            }
        }

        if (showDetailedLogs)
        {
            Debug.Log($"[CuriosityTracker] Adjacent transitions: {adjacentTransitions}/{totalTransitions} = {systematicRatio:F2}");
        }
    }

    /// <summary>
    /// Check if two rooms are adjacent
    /// </summary>
    bool AreRoomsAdjacent(string room1, string room2)
    {
        foreach (var connection in roomConnections)
        {
            if ((connection.room1 == room1 && connection.room2 == room2) ||
                (connection.room1 == room2 && connection.room2 == room1))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get current exploration style
    /// </summary>
    public ExplorationStyle GetExplorationStyle()
    {
        return explorationStyle;
    }

    /// <summary>
    /// Get room visit sequence (for analysis)
    /// </summary>
    public List<string> GetRoomSequence()
    {
        return roomSequence.Select(r => r.roomId).ToList();
    }

    /// <summary>
    /// Check if user is a systematic explorer (methodical)
    /// </summary>
    public bool IsSystematicExplorer()
    {
        return explorationStyle == ExplorationStyle.Systematic;
    }

    /// <summary>
    /// Check if user is a random explorer (impulsive)
    /// </summary>
    public bool IsRandomExplorer()
    {
        return explorationStyle == ExplorationStyle.Random;
    }

    // ============================================================================
    // CURIOSITY SCORE
    // ============================================================================

    /// <summary>
    /// Calculate overall curiosity score (0-100)
    /// Combines object revisits, head tilt, and exploration style
    /// </summary>
    public float GetCuriosityScore()
    {
        float score = 0f;

        // Component 1: Object revisitation (0-40 points)
        float objectScore = Mathf.Min(totalCuriousObjects * 10f, 40f);
        score += objectScore;

        // Component 2: Head tilt curiosity (0-30 points)
        if (HeadTrackingAnalyzer.Instance != null && HeadTrackingAnalyzer.Instance.IsCurious())
        {
            float tiltDuration = HeadTrackingAnalyzer.Instance.GetTiltDuration();
            score += Mathf.Min(tiltDuration * 10f, 30f);
        }

        // Component 3: Exploration thoroughness (0-30 points)
        if (explorationStyle == ExplorationStyle.Systematic)
            score += 30f;
        else if (explorationStyle == ExplorationStyle.Mixed)
            score += 20f;
        else if (explorationStyle == ExplorationStyle.Random)
            score += 10f;

        return Mathf.Clamp(score, 0f, 100f);
    }

    /// <summary>
    /// Check if user is highly curious (for special content)
    /// </summary>
    public bool IsHighlyCurious()
    {
        return GetCuriosityScore() >= 70f;
    }

    // ============================================================================
    // FIREBASE LOGGING
    // ============================================================================

    /// <summary>
    /// Log curious object to Firebase
    /// </summary>
    async void LogCuriousObjectToFirebase(string objectId, string objectName, int visitCount)
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { "objectId", objectId },
                { "objectName", objectName },
                { "visitCount", visitCount },
                { "curiosityDetected", true }
            };

            string docId = FirebaseLogger.GenerateDocId($"curious_{objectId}");
            await FirebaseLogger.LogSessionData("curiosityTracking", data, docId, "[CuriosityTracker]");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CuriosityTracker] Failed to log: {e.Message}");
        }
    }

    /// <summary>
    /// Log exploration pattern summary to Firebase
    /// </summary>
    public async void LogExplorationSummaryToFirebase()
    {
        if (!FirebaseLogger.HasSession)
            return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { "explorationStyle", explorationStyle.ToString() },
                { "totalRoomsVisited", roomSequence.Count },
                { "roomSequence", string.Join(" -> ", roomSequence.Select(r => r.roomId)) },
                { "curiousObjectCount", totalCuriousObjects },
                { "curiosityScore", GetCuriosityScore() }
            };

            await FirebaseLogger.MergeSessionData("explorationPattern", data, "summary", "[CuriosityTracker]");

            if (showDebugLogs)
            {
                Debug.Log("[CuriosityTracker] Logged exploration summary to Firebase");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CuriosityTracker] Failed to log summary: {e.Message}");
        }
    }

    /// <summary>
    /// Get comprehensive curiosity summary
    /// </summary>
    public CuriositySummary GetCuriositySummary()
    {
        return new CuriositySummary
        {
            totalObjectsViewed = objectVisits.Count,
            curiousObjectCount = totalCuriousObjects,
            explorationStyle = explorationStyle,
            roomsVisited = roomSequence.Count,
            curiosityScore = GetCuriosityScore()
        };
    }
}

// ============================================================================
// DATA STRUCTURES
// ============================================================================

[System.Serializable]
public class RoomConnection
{
    public string room1;
    public string room2;

    public RoomConnection(string r1, string r2)
    {
        room1 = r1;
        room2 = r2;
    }
}

public class ObjectVisitData
{
    public string objectId;
    public string objectName;
    public int visitCount;
    public List<float> visitTimes;
    public float lastVisitTime;
    public bool markedAsCurious;
}

public class RoomVisit
{
    public string roomId;
    public float enterTime;
}

public enum ExplorationStyle
{
    Systematic,  // Methodical, room-by-room
    Random,      // Impulsive, jumping around
    Mixed        // Combination of both
}

public struct CuriositySummary
{
    public int totalObjectsViewed;
    public int curiousObjectCount;
    public ExplorationStyle explorationStyle;
    public int roomsVisited;
    public float curiosityScore;
}
