using UnityEngine;

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
        // For now, use a test user ID
        userId = "test_wadia";

        // Later, replace with:
        // userId = FirebaseManager.Instance.Auth.CurrentUser.UserId;
    }

    public async void ReportRoomTime(string roomId, float timeSpent)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            Debug.LogWarning("Firebase not ready yet.");
            return;
        }

        await FirebaseManager.Instance.SaveRoomTimeAsync(userId, roomId, timeSpent);
    }
}
