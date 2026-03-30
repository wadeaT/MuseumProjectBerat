using UnityEngine;
using UnityEngine.Video;

public class VideoTriggerZone : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            videoPlayer.Play();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            videoPlayer.Stop();
        }
    }
}
