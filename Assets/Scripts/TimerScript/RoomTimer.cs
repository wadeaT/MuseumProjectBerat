using UnityEngine;

/// <summary>
/// RoomTimer - Tracks time spent in a room trigger zone.
/// FIXED: Added null checks, scene transition handling, and duplicate exit prevention.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomTimer : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Unique identifier for this room (e.g., 'kitchen', 'bedroom')")]
    public string roomId;

    [Header("UI References")]
    [Tooltip("Optional UI component to display timer")]
    public TimerUI timerUI;

    [Header("Debug")]
    public bool showDebugMessages = true;

    private float enterTime;
    private bool inside = false;
    private bool hasReportedExit = false; // Prevents duplicate reporting

    void Start()
    {
        // Validate setup
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[RoomTimer] {roomId}: Missing Collider component!");
            return;
        }

        if (!col.isTrigger)
        {
            Debug.LogWarning($"[RoomTimer] {roomId}: Collider should be set as Trigger!");
        }

        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogError($"[RoomTimer] Room ID is not set on {gameObject.name}!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignore if already inside or not the player
        if (inside || !other.CompareTag("Player")) return;

        inside = true;
        hasReportedExit = false;
        enterTime = Time.time;

        if (timerUI != null)
        {
            timerUI.StartTimer();
        }

        if (showDebugMessages)
        {
            Debug.Log($"[RoomTimer] Player entered '{roomId}'");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Ignore if not inside or not the player
        if (!inside || !other.CompareTag("Player")) return;

        SaveAndExit("OnTriggerExit");
    }

    /// <summary>
    /// Called when the GameObject is disabled (e.g., scene change while inside)
    /// This ensures time is saved even if player teleports/transitions out
    /// </summary>
    void OnDisable()
    {
        if (inside && !hasReportedExit)
        {
            SaveAndExit("OnDisable (scene transition)");
        }
    }

    /// <summary>
    /// Called when the GameObject is destroyed
    /// </summary>
    void OnDestroy()
    {
        if (inside && !hasReportedExit)
        {
            SaveAndExit("OnDestroy");
        }
    }

    /// <summary>
    /// Saves the room time and marks the player as exited
    /// </summary>
    private void SaveAndExit(string trigger)
    {
        // Prevent duplicate reporting
        if (hasReportedExit) return;
        hasReportedExit = true;

        inside = false;
        float timeSpent = Time.time - enterTime;

        // Stop the UI timer
        if (timerUI != null)
        {
            timerUI.StopTimer();
        }

        if (showDebugMessages)
        {
            Debug.Log($"[RoomTimer] Player exited '{roomId}' after {timeSpent:F2}s (trigger: {trigger})");
        }

        // Report to RoomTimerManager with null check
        if (RoomTimerManager.Instance != null)
        {
            RoomTimerManager.Instance.ReportRoomTime(roomId, timeSpent);
        }
        else
        {
            Debug.LogWarning($"[RoomTimer] RoomTimerManager.Instance is null! Time for '{roomId}' not saved.");
        }
    }

    /// <summary>
    /// Force exit the room (call from external scripts if needed)
    /// </summary>
    public void ForceExit()
    {
        if (inside)
        {
            SaveAndExit("ForceExit");
        }
    }

    /// <summary>
    /// Check if player is currently in this room
    /// </summary>
    public bool IsPlayerInside()
    {
        return inside;
    }

    /// <summary>
    /// Get current time spent in room (only valid while inside)
    /// </summary>
    public float GetCurrentTimeInRoom()
    {
        if (!inside) return 0f;
        return Time.time - enterTime;
    }

    void OnDrawGizmosSelected()
    {
        // Draw the trigger bounds in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}