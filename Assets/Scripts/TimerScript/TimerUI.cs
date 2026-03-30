using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private float currentTime;
    private bool counting;

    public string roomId; // assign in inspector (e.g., "Kitchen")

    private void Update()
    {
        if (counting)
        {
            currentTime += Time.deltaTime;
            if (timerText)
                timerText.text = $"{roomId} | {currentTime:F1}s";
        }
    }

    public void StartTimer()
    {
        currentTime = 0f;
        counting = true;
        if (timerText)
            timerText.text = $"{roomId} | 0.0s";
    }

    public float StopTimer()
    {
        counting = false;
        return currentTime; // return time spent
    }
}
