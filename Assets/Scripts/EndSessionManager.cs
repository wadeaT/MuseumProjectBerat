using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class EndSessionManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string achievementsScene = "AchievementsScene";

    [Header("Keyboard Shortcut")]
    [Tooltip("Key to press to end session (works even when cursor is locked)")]
    public Key endSessionKey = Key.F10;

    [Header("Optional UI")]
    [Tooltip("Optional: Tooltip text to show the shortcut hint")]
    public TMPro.TextMeshProUGUI shortcutHintText;

    void Start()
    {
        // Update hint text if assigned
        if (shortcutHintText != null)
        {
            shortcutHintText.text = $"Press {endSessionKey} to End Session";
        }
    }

    void Update()
    {
        // Check for keyboard shortcut
        if (Keyboard.current != null && Keyboard.current[endSessionKey].wasPressedThisFrame)
        {
            Debug.Log($"[EndSessionManager] {endSessionKey} pressed - ending session");
            EndMuseumSession();
        }
    }

    /// <summary>
    /// Called by UI button OR keyboard shortcut
    /// </summary>
    public void EndMuseumSession()
    {
        Debug.Log("[EndSessionManager] Ending museum session... loading Achievements.");
        SceneManager.LoadScene(achievementsScene);
    }
}