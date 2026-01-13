using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;

public class WebGLManager : MonoBehaviour
{
    public static WebGLManager Instance { get; private set; }

    public bool IsMobile { get; private set; }
    public bool IsCursorLocked { get; private set; }

    [DllImport("__Internal")]
    private static extern bool IsMobileBrowser();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Detect platform once
#if UNITY_WEBGL && !UNITY_EDITOR
            IsMobile = IsMobileBrowser();
#else
            IsMobile = false; // Default to desktop in Editor
#endif
            Debug.Log($"[WebGLManager] Mobile: {IsMobile}");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Setup cursor based on platform AND current scene
        string currentScene = SceneManager.GetActiveScene().name;

        if (IsMobile)
        {
            UnlockCursor();
        }
        else
        {
            // On desktop: unlock cursor for UI scenes, lock for gameplay
            if (IsUIScene(currentScene))
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        // Find and configure mobile controls in current scene
        ConfigureMobileControls();

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Configure mobile controls in each new scene
        ConfigureMobileControls();

        // Handle cursor based on scene type (desktop only)
        if (!IsMobile)
        {
            if (IsUIScene(scene.name))
            {
                UnlockCursor();
                Debug.Log($"[WebGLManager] UI scene '{scene.name}' - cursor unlocked");
            }
            else
            {
                LockCursor();
                Debug.Log($"[WebGLManager] Gameplay scene '{scene.name}' - cursor locked");
            }
        }
    }

    /// <summary>
    /// Check if the scene is a UI-focused scene where cursor should be unlocked.
    /// Add any additional UI scene names here.
    /// </summary>
    private bool IsUIScene(string sceneName)
    {
        return sceneName == "LoginScene" ||
               sceneName == "AchievementsScene" ||
               sceneName == "SUSScene"; // Add SUS scene too if needed
    }

    void ConfigureMobileControls()
    {
        // Find mobile controls canvas by tag or name
        GameObject mobileControls = GameObject.FindWithTag("MobileControls");

        // If not found by tag, try by name
        if (mobileControls == null)
        {
            mobileControls = GameObject.Find("MobileControlsCanvas");
        }

        if (mobileControls != null)
        {
            mobileControls.SetActive(IsMobile);
            Debug.Log($"[WebGLManager] Mobile controls: {(IsMobile ? "VISIBLE" : "HIDDEN")}");
        }
    }

    void Update()
    {
        // Skip cursor management on mobile
        if (IsMobile) return;

        // Skip auto-lock behavior in UI scenes
        string currentScene = SceneManager.GetActiveScene().name;
        if (IsUIScene(currentScene))
        {
            // In UI scenes, only handle ESC to toggle (optional)
            // But don't auto-lock on click
            return;
        }

        // ESC toggles cursor (gameplay scenes only)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        // Click to re-lock (only if not clicking UI) - gameplay scenes only
        if (!IsCursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsPointerOverUI())
            {
                LockCursor();
            }
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public void LockCursor()
    {
        IsCursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[WebGLManager] Cursor LOCKED");
    }

    public void UnlockCursor()
    {
        IsCursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[WebGLManager] Cursor UNLOCKED");
    }
}