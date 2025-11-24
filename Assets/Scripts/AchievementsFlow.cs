using UnityEngine;
using UnityEngine.SceneManagement;

public class AchievementsFlow : MonoBehaviour
{
    [Header("Next Scene")]
    public string susScene = "SUSScene"; // change to your exact scene name

    public void GoToSUS()
    {
        Debug.Log("Loading SUS scene...");
        SceneManager.LoadScene(susScene);
    }
}
