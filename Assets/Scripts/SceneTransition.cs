using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Name of the scene to load (must be in Build Settings)")]
    public string sceneName;

    [Header("Optional Settings")]
    [Tooltip("Delay before loading scene (in seconds)")]
    public float transitionDelay = 0f;

    [Tooltip("Show debug messages")]
    public bool showDebugMessages = true;

    void Start()
    {
        // Verify the GameObject has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("SceneTransition requires a Collider component!", this);
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("Collider should be set as Trigger for SceneTransition to work properly!", this);
        }

        // Verify scene name is set
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name is not set! Please assign a scene name in the Inspector.", this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the player entered the trigger
        if (other.CompareTag("Player"))
        {
            if (showDebugMessages)
            {
                Debug.Log($"Player entered portal. Loading scene: {sceneName}");
            }

            // Load scene with optional delay
            if (transitionDelay > 0)
            {
                Invoke(nameof(LoadScene), transitionDelay);
            }
            else
            {
                LoadScene();
            }
        }
    }

    void LoadScene()
    {
        // Check if scene exists in build settings
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"Scene '{sceneName}' cannot be loaded! Make sure it's added to Build Settings (File → Build Settings).", this);
        }
    }
}