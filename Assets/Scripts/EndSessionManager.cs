using UnityEngine;
using UnityEngine.SceneManagement;

public class EndSessionManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string achievementsScene = "AchievementsScene"; 

    public void EndMuseumSession()
    {
        Debug.Log("Ending museum session... loading Achievements.");
        SceneManager.LoadScene(achievementsScene);
    }
}
