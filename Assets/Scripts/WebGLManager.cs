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
            IsMobile = IsMobileBrowser();
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
        // Setup cursor based on platform
        if (IsMobile)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
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

        // Re-lock cursor on desktop when entering gameplay scenes
        if (!IsMobile && scene.name != "LoginScene") // Adjust scene name as needed
        {
            LockCursor();
        }
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

        // ESC toggles cursor
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        // Click to re-lock (only if not clicking UI)
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