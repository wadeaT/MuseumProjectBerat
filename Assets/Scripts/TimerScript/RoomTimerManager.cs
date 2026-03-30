using UnityEngine;

public class RoomTimerManager : MonoBehaviour
{
    public static RoomTimerManager Instance;

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


    public async void ReportRoomTime(string roomId, float timeSpent)
    {
        // Pull live user ID from PlayerManager
        string currentUserId = PlayerManager.Instance != null
            ? PlayerManager.Instance.userId
            : null;

        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("RoomTimerManager: No logged-in user. Cannot save room time.");
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("Firebase not ready yet.");
            return;
        }

        await FirebaseManager.Instance.SaveRoomTimeAsync(currentUserId, roomId, timeSpent);
    }
}
