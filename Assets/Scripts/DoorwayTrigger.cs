using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorwayTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        SceneManager.LoadScene("MainMenuScene");
    }
}
