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
}
