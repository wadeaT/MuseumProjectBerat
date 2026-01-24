using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// RoomTimerManager - WebGL-safe singleton that manages room time tracking.
/// FIXED: Removed unused cached userId, added local accumulation, better error handling.
/// </summary>
public class RoomTimerManager : MonoBehaviour
{
    public static RoomTimerManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Minimum time (seconds) to report. Shorter visits are ignored.")]
    public float minimumTimeToReport = 1f;

    [Tooltip("Show debug messages in console")]
    public bool showDebugMessages = true;

    [Header("Batching (Optional)")]
    [Tooltip("Batch multiple room updates before saving to Firebase")]
    public bool useBatching = false;

    [Tooltip("Flush batch after this many seconds")]
    public float batchFlushInterval = 30f;

    // Local tracking of accumulated room times (for current session)
    private Dictionary<string, RoomSessionData> sessionRoomData = new Dictionary<string, RoomSessionData>();

    // Batch queue for pending Firebase writes
    private Dictionary<string, PendingRoomUpdate> pendingUpdates = new Dictionary<string, PendingRoomUpdate>();
    private float lastFlushTime;

    private class RoomSessionData
    {
        public float totalTime;
        public int visitCount;
    }

    private class PendingRoomUpdate
    {
        public string roomId;
        public float timeToAdd;
        public int visitsToAdd;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[RoomTimerManager] Initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        lastFlushTime = Time.time;
    }

    private void Update()
    {
        // Periodic batch flush
        if (useBatching && pendingUpdates.Count > 0)
        {
            if (Time.time - lastFlushTime >= batchFlushInterval)
            {
                FlushPendingUpdates();
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Flush when app is paused (mobile browsers)
        if (pauseStatus && pendingUpdates.Count > 0)
        {
            FlushPendingUpdates();
        }
    }

    private void OnApplicationQuit()
    {
        // Flush any pending updates before quitting
        if (pendingUpdates.Count > 0)
        {
            FlushPendingUpdates();
        }
    }

    /// <summary>
    /// Report time spent in a room. Called by RoomTimer components.
    /// </summary>
    public void ReportRoomTime(string roomId, float timeSpent)
    {
        // Validate input
        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning("[RoomTimerManager] Cannot report time: roomId is empty.");
            return;
        }

        // Ignore very short visits (likely accidental)
        if (timeSpent < minimumTimeToReport)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[RoomTimerManager] Ignoring short visit to '{roomId}': {timeSpent:F2}s < {minimumTimeToReport}s minimum");
            }
            return;
        }

        // Track locally for this session
        TrackLocally(roomId, timeSpent);

        // Get current user ID
        string currentUserId = GetCurrentUserId();

        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[RoomTimerManager] No logged-in user. Room time tracked locally but not saved to Firebase.");
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("[RoomTimerManager] Firebase not ready. Room time tracked locally but not saved to Firebase.");
            return;
        }

        // Save to Firebase (batched or immediate)
        if (useBatching)
        {
            QueueUpdate(roomId, timeSpent);
        }
        else
        {
            SaveToFirebase(currentUserId, roomId, timeSpent, 1);
        }
    }

    /// <summary>
    /// Track room time locally for session statistics
    /// </summary>
    private void TrackLocally(string roomId, float timeSpent)
    {
        if (!sessionRoomData.ContainsKey(roomId))
        {
            sessionRoomData[roomId] = new RoomSessionData();
        }

        sessionRoomData[roomId].totalTime += timeSpent;
        sessionRoomData[roomId].visitCount++;

        if (showDebugMessages)
        {
            var data = sessionRoomData[roomId];
            Debug.Log($"[RoomTimerManager] Session stats for '{roomId}': {data.totalTime:F1}s total, {data.visitCount} visits");
        }
    }

    /// <summary>
    /// Queue an update for batched saving
    /// </summary>
    private void QueueUpdate(string roomId, float timeSpent)
    {
        if (!pendingUpdates.ContainsKey(roomId))
        {
            pendingUpdates[roomId] = new PendingRoomUpdate
            {
                roomId = roomId,
                timeToAdd = 0f,
                visitsToAdd = 0
            };
        }

        pendingUpdates[roomId].timeToAdd += timeSpent;
        pendingUpdates[roomId].visitsToAdd++;

        if (showDebugMessages)
        {
            Debug.Log($"[RoomTimerManager] Queued update for '{roomId}': +{timeSpent:F2}s (pending: {pendingUpdates.Count} rooms)");
        }
    }

    /// <summary>
    /// Flush all pending updates to Firebase
    /// </summary>
    public void FlushPendingUpdates()
    {
        if (pendingUpdates.Count == 0) return;

        string currentUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[RoomTimerManager] Cannot flush: No user logged in.");
            return;
        }

        if (showDebugMessages)
        {
            Debug.Log($"[RoomTimerManager] Flushing {pendingUpdates.Count} pending room updates...");
        }

        foreach (var kvp in pendingUpdates)
        {
            SaveToFirebase(currentUserId, kvp.Value.roomId, kvp.Value.timeToAdd, kvp.Value.visitsToAdd);
        }

        pendingUpdates.Clear();
        lastFlushTime = Time.time;
    }

    /// <summary>
    /// Save room time to Firebase using increment operation
    /// </summary>
    private void SaveToFirebase(string userId, string roomId, float timeToAdd, int visitsToAdd)
    {
        FirebaseManager.Instance.SaveRoomTimeIncrement(userId, roomId, timeToAdd, visitsToAdd, (success) =>
        {
            if (success)
            {
                if (showDebugMessages)
                {
                    Debug.Log($"[RoomTimerManager] Saved to Firebase: '{roomId}' +{timeToAdd:F2}s, +{visitsToAdd} visit(s)");
                }
            }
            else
            {
                Debug.LogWarning($"[RoomTimerManager] Failed to save room time for '{roomId}' to Firebase");
            }
        });
    }

    /// <summary>
    /// Get the current user ID from PlayerManager
    /// </summary>
    private string GetCurrentUserId()
    {
        if (PlayerManager.Instance != null && !string.IsNullOrEmpty(PlayerManager.Instance.userId))
        {
            return PlayerManager.Instance.userId;
        }

        // Fallback to FirebaseManager's participant code
        if (FirebaseManager.Instance != null && !string.IsNullOrEmpty(FirebaseManager.Instance.CurrentParticipantCode))
        {
            return FirebaseManager.Instance.CurrentParticipantCode;
        }

        return null;
    }

    /// <summary>
    /// Get session statistics for a specific room
    /// </summary>
    public (float totalTime, int visitCount) GetSessionStats(string roomId)
    {
        if (sessionRoomData.TryGetValue(roomId, out var data))
        {
            return (data.totalTime, data.visitCount);
        }
        return (0f, 0);
    }

    /// <summary>
    /// Get all session statistics
    /// </summary>
    public Dictionary<string, (float totalTime, int visitCount)> GetAllSessionStats()
    {
        var result = new Dictionary<string, (float, int)>();
        foreach (var kvp in sessionRoomData)
        {
            result[kvp.Key] = (kvp.Value.totalTime, kvp.Value.visitCount);
        }
        return result;
    }

    /// <summary>
    /// Clear session statistics (call when user logs out)
    /// </summary>
    public void ClearSessionStats()
    {
        sessionRoomData.Clear();
        pendingUpdates.Clear();
        Debug.Log("[RoomTimerManager] Session stats cleared");
    }
}