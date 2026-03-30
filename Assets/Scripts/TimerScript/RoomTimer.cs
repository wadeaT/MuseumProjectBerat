using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RoomTimer : MonoBehaviour
{
    public string roomId;
    public TimerUI timerUI;

    private float enterTime;
    private bool inside;

    void OnTriggerEnter(Collider other)
    {
        if (inside || !other.CompareTag("Player")) return;
        inside = true;
        enterTime = Time.time;

        if (timerUI) timerUI.StartTimer();

        // ✅ NEW: Notify all trackers of room change
        NotifyTrackersOfRoomChange();

        Debug.Log($"Entered {roomId}");
    }

    void OnTriggerExit(Collider other)
    {
        if (!inside || !other.CompareTag("Player")) return;
        inside = false;

        float timeSpent = Time.time - enterTime;
        if (timerUI) timerUI.StopTimer();

        Debug.Log($"Exited {roomId} after {timeSpent:F2}s");
        RoomTimerManager.Instance.ReportRoomTime(roomId, timeSpent);
    }

    // ✅ NEW METHOD: Notify all trackers when room changes
    void NotifyTrackersOfRoomChange()
    {
        // Notify HandProximityTracker
        if (HandProximityTracker.Instance != null)
        {
            HandProximityTracker.Instance.SetCurrentRoom(roomId);
        }

        // Notify HeadTrackingAnalyzer
        if (HeadTrackingAnalyzer.Instance != null)
        {
            HeadTrackingAnalyzer.Instance.SetCurrentRoom(roomId);
        }

        // Notify CuriosityTracker
        if (CuriosityTracker.Instance != null)
        {
            CuriosityTracker.Instance.OnRoomEntered(roomId);
        }

        Debug.Log($"🏛️ [RoomTimer] Notified trackers: Now in {roomId}");
    }
}