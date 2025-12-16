using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;



public class InteractiveObject : MonoBehaviour
{
    [Header("Object Information")]
    [Tooltip("Localized title of this object")]
    public LocalizedString objectTitle;

    [Tooltip("Localized description of this object")]
    public LocalizedString objectDescription;


    //[Tooltip("Description of this object and its historical significance")]
    //[TextArea(4, 10)]
    //public string objectDescription = "This artifact tells a story of Ottoman-era life in Berat. Fill in the description here.";

    [Tooltip("Optional image of this object")]
    public Sprite objectImage;

    [Header("Interaction Settings")]
    [Tooltip("How close must the player be to interact? (in meters)")]
    public float interactionRadius = 2.5f;

    [Tooltip("Must the player look directly at the object?")]
    public bool requiresDirectLook = true;

    [Tooltip("Key to press to examine the object")]
    public KeyCode interactionKey = KeyCode.E;

    // ✅ REMOVED: useMobileControls - now handled by InteractionManager button

    [Tooltip("Auto-examine after looking for this many seconds (0 = disabled)")]
    [Range(0f, 3f)]
    public float autoExamineDelay = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Color when player is too far away")]
    public Color idleColor = new Color(1f, 1f, 1f, 1f); // White

    [Tooltip("Color when player can interact")]
    public Color highlightColor = new Color(1f, 0.9f, 0.6f, 1f); // Warm glow

    [Tooltip("How bright should the highlight glow be?")]
    [Range(0f, 2f)]
    public float glowIntensity = 0.5f;

    [Header("Optional Effects")]
    [Tooltip("Particle effect or glow (optional)")]
    public GameObject highlightEffect;

    [Tooltip("Sound when examined (optional)")]
    public AudioClip examinationSound;

    [Header("Analytics Tracking")]
    [Tooltip("Track how long players examine this object")]
    public bool trackInteractionTime = true;

    public LayerMask interactableMask;

    // Internal variables
    private Transform playerTransform;
    private Camera playerCamera;
    private Renderer objectRenderer;
    private Material objectMaterial;
    private Color originalColor;
    private AudioSource audioSource;
    private bool isHighlighted = false;
    private float interactionStartTime;
    private int totalInteractions = 0;
    private float totalTimeSpent = 0f;
    private bool currentlyExamining = false;
    private float lookTimer = 0f; // For auto-examine feature

    void Start()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                Debug.LogError($"InteractiveObject ({objectTitle}): No camera found!");
            }
        }
        else
        {
            Debug.LogError("InteractiveObject: No GameObject with 'Player' tag found!");
        }

        // Get renderer and store original material
        objectRenderer = GetComponent<Renderer>();

        // If no renderer on this object, try to find one in children
        if (objectRenderer == null)
        {
            objectRenderer = GetComponentInChildren<Renderer>();

            if (objectRenderer != null)
            {
                Debug.Log($"{objectTitle}: Using renderer from child object '{objectRenderer.gameObject.name}'");
            }
        }

        if (objectRenderer != null)
        {
            // Create a copy of the material so we don't affect other objects
            objectMaterial = new Material(objectRenderer.material);
            objectRenderer.material = objectMaterial;
            originalColor = objectMaterial.color;

            // Enable emission for glow effect
            objectMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning($"{objectTitle}: No Renderer found on this object or its children. Highlight won't work.");
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && examinationSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Hide highlight effect initially
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(false);
        }
    }

    void Awake()
    {
        if (interactableMask.value == 0)
            interactableMask = LayerMask.GetMask("Interactable");
    }


    void Update()
    {
        if (playerTransform == null) return;

        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Debug.DrawRay(ray.origin, ray.direction * (interactionRadius + 1f), Color.yellow);
        }

        // Check if looking at object (if required)
        if (requiresDirectLook)
        {
            if (IsPlayerLookingAtObject())
            {
                HighlightObject(true);
                ShowInteractionPrompt(true);

                // Auto-examine after looking for X seconds
                if (autoExamineDelay > 0)
                {
                    lookTimer += Time.deltaTime;
                    if (lookTimer >= autoExamineDelay)
                    {
                        ExamineObjectAsync();
                        lookTimer = 0f;
                    }
                }

                // ✅ E key still works for PC/Editor testing
                // Touch/mobile is handled by InteractionManager button
            }
            else
            {
                lookTimer = 0f; // Reset timer when not looking
                HighlightObject(false);
                ShowInteractionPrompt(false);
            }
        }
        else
        {
            // Auto-highlight if in range (no look requirement)
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= interactionRadius)
            {
                HighlightObject(true);
                ShowInteractionPrompt(true);
            }
            else
            {
                HighlightObject(false);
                ShowInteractionPrompt(false);
            }
        }
    }

    bool IsPlayerLookingAtObject()
    {
        if (playerCamera == null) return false;

        if (interactableMask.value == 0)
            interactableMask = LayerMask.GetMask("Interactable");

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        float dist = interactionRadius + 1f;

        if (Physics.Raycast(ray, out var hit, dist, interactableMask, QueryTriggerInteraction.Ignore))
        {
            // Find the InteractiveObject that the ray actually hit
            var io = hit.collider.GetComponentInParent<InteractiveObject>();

            if (io == null) return false;

            if (io == this)
            {
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Highlight or unhighlight the object
    /// </summary>
    void HighlightObject(bool highlight)
    {
        if (isHighlighted == highlight) return; // Already in this state

        isHighlighted = highlight;

        if (objectMaterial != null)
        {
            if (highlight)
            {
                // Apply highlight
                objectMaterial.color = highlightColor;
                objectMaterial.SetColor("_EmissionColor", highlightColor * glowIntensity);
            }
            else
            {
                // Return to original
                objectMaterial.color = originalColor;
                objectMaterial.SetColor("_EmissionColor", Color.black);
            }
        }

        // Toggle highlight effect
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(highlight);
        }
    }

    /// <summary>
    /// ✅ PUBLIC METHOD - Called by InteractionManager when button is pressed
    /// </summary>
    public void TriggerExamination()
    {
        ExamineObjectAsync();
    }

    /// <summary>
    /// Called when player examines the object
    /// </summary>
    async Task ExamineObjectAsync()
    {
        Debug.Log($"Examining: {objectTitle.TableReference}/{objectTitle.TableEntryReference}");

        // Play sound
        if (audioSource != null && examinationSound != null)
        {
            audioSource.PlayOneShot(examinationSound);
        }

        // ✅ Start tracking AND wait for panel close
        if (trackInteractionTime && !currentlyExamining)
        {
            StartInteractionTracking();
            StartCoroutine(WaitForPanelClose());
        }

        // Get localized strings asynchronously
        var titleTask = objectTitle.GetLocalizedStringAsync();
        var descTask = objectDescription.GetLocalizedStringAsync();

        await System.Threading.Tasks.Task.WhenAll(titleTask.Task, descTask.Task);

        string localizedTitle = titleTask.Result;
        string localizedDescription = descTask.Result;
        // Show info panel
        if (ObjectInfoUI.Instance != null)
        {
            ObjectInfoUI.Instance.ShowObjectInfo(localizedTitle, localizedDescription, objectImage);
        }
    }

    /// <summary>
    /// Show/hide interaction prompt (placeholder for now)
    /// </summary>
    void ShowInteractionPrompt(bool show)
    {
        // You can implement a UI prompt here later if needed
        // For now, the highlight is enough visual feedback
    }

    /// <summary>
    /// Draw gizmo in editor to show interaction radius
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }


    /// <summary>
    /// Start tracking how long player examines this object
    /// </summary>
    async void StartInteractionTracking()
    {
        currentlyExamining = true;
        interactionStartTime = Time.time;
        totalInteractions++;
        string localizedTitle = await objectTitle.GetLocalizedStringAsync().Task;
        Debug.Log($" Started tracking: {localizedTitle} (Interaction #{totalInteractions})");
    }

    /// <summary>
    /// Stop tracking and save to Firebase
    /// </summary>
    async void StopInteractionTracking()
    {
        if (!currentlyExamining) return;

        currentlyExamining = false;
        float timeSpent = Time.time - interactionStartTime;
        totalTimeSpent += timeSpent;
        string localizedTitle = await objectTitle.GetLocalizedStringAsync().Task;
        Debug.Log($" {localizedTitle}: Examined for {timeSpent:F1} seconds (Total: {totalTimeSpent:F1}s over {totalInteractions} interactions)");

        // Save to Firebase
        SaveInteractionToFirebase(timeSpent, localizedTitle);
    }

    /// <summary>
    /// Save interaction data to Firebase
    /// </summary>
    async void SaveInteractionToFirebase(float duration, string objectName)
    {
        // Log to console
        Debug.Log($"  INTERACTION DATA:");
        Debug.Log($"   Object: {objectName}");
        Debug.Log($"   Duration: {duration:F1} seconds");
        Debug.Log($"   Total Interactions: {totalInteractions}");
        Debug.Log($"   Average Time: {(totalTimeSpent / totalInteractions):F1} seconds");
        Debug.Log($"   Timestamp: {System.DateTime.UtcNow}");

        // Save to Firebase
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            // ✅ FIXED: Use GetCurrentUserId() instead of accessing Auth directly
            // This method works for both WebGL and non-WebGL builds
            string userId = FirebaseManager.Instance.GetCurrentUserId();

            if (!string.IsNullOrEmpty(userId))
            {
                float avgTime = totalTimeSpent / totalInteractions;

                await FirebaseManager.Instance.SaveObjectInteractionAsync(
                    userId,
                    objectName,
                    duration,
                    totalInteractions,
                    avgTime
                );
            }
            else
            {
                Debug.LogWarning("⚠ No user logged in - interaction not saved to Firebase");
            }
        }
        else
        {
            Debug.LogWarning("⚠ FirebaseManager not ready - interaction only logged locally");
        }
    }

    /// <summary>
    /// Wait for panel to close, then stop tracking
    /// </summary>
    System.Collections.IEnumerator WaitForPanelClose()
    {
        // Wait until panel is active
        while (ObjectInfoUI.Instance != null && ObjectInfoUI.Instance.infoPanel != null &&
               !ObjectInfoUI.Instance.infoPanel.activeSelf)
        {
            yield return null;
        }

        // Wait until panel closes
        while (ObjectInfoUI.Instance != null && ObjectInfoUI.Instance.infoPanel != null &&
               ObjectInfoUI.Instance.infoPanel.activeSelf)
        {
            yield return null;
        }

        // Panel closed - stop tracking
        StopInteractionTracking();
    }
}