using UnityEngine;

/// <summary>
/// RoomTimerManager - WebGL-safe version without async void
/// </summary>
public class RoomTimerManager : MonoBehaviour
{
    public static RoomTimerManager Instance;
    private string userId;

    private void Awake()
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

    private void Start()
    {
        // Wait until PlayerManager is initialized
        if (PlayerManager.Instance != null)
        {
            userId = PlayerManager.Instance.userId;
            Debug.Log("[RoomTimerManager] Loaded userId = " + userId);
        }
        else
        {
            Debug.LogWarning("[RoomTimerManager] PlayerManager not found in scene!");
        }
    }

    /// <summary>
    /// Report room time - non-blocking callback approach instead of async
    /// </summary>
    public void ReportRoomTime(string roomId, float timeSpent)
    {
        // Pull live user ID from PlayerManager
        string currentUserId = PlayerManager.Instance != null
            ? PlayerManager.Instance.userId
            : null;

        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[RoomTimerManager] No logged-in user. Cannot save room time.");
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("[RoomTimerManager] Firebase not ready yet.");
            return;
        }

        // Non-blocking Firebase save
        FirebaseManager.Instance.SaveRoomTime(currentUserId, roomId, timeSpent, (success) =>
        {
            if (success)
            {
                Debug.Log($"[RoomTimerManager] Room time saved: {roomId} = {timeSpent:F2}s");
            }
            else
            {
                Debug.LogWarning($"[RoomTimerManager] Failed to save room time for {roomId}");
            }
        });
    }
}