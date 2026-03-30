using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorwayTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        SceneManager.LoadScene("MainMenuScene");
    }
}
