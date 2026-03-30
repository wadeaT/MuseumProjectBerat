using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// Interactive Object - Examinable objects in the museum
/// Updated to use event-based tracking with ObjectInfoUI for closed-loop reading adaptation
/// </summary>
public class InteractiveObject : MonoBehaviour
{
    [Header("Object Information")]
    [Tooltip("Localized title of this object")]
    public LocalizedString objectTitle;

    [Tooltip("Localized description of this object")]
    public LocalizedString objectDescription;

    [Tooltip("Optional image of this object")]
    public Sprite objectImage;

    [Header("Interaction Settings")]
    [Tooltip("How close must the player be to interact? (in meters)")]
    public float interactionRadius = 2.5f;

    [Tooltip("Must the player look directly at the object?")]
    public bool requiresDirectLook = true;

    [Tooltip("Key to press to examine the object")]
    public KeyCode interactionKey = KeyCode.E;

    [Tooltip("Auto-examine after looking for this many seconds (0 = disabled)")]
    [Range(0f, 3f)]
    public float autoExamineDelay = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Color when player is too far away")]
    public Color idleColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Color when player can interact")]
    public Color highlightColor = new Color(1f, 0.9f, 0.6f, 1f);

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

    [Header("Quest 3 VR")]
    [Tooltip("Use controller-based highlighting instead of head-based (for Quest 3)")]
    public bool useControllerHighlighting = true;

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
    private float lookTimer = 0f;
    private string currentLocalizedTitle = "";
    private float lastExaminationTime = -10f;
    private float examinationCooldown = 2.0f;

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
            objectMaterial = new Material(objectRenderer.material);
            objectRenderer.material = objectMaterial;
            originalColor = objectMaterial.color;
            objectMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning($"{objectTitle}: No Renderer found. Highlight won't work.");
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

        // Subscribe to panel closed event for tracking
        if (ObjectInfoUI.Instance != null)
        {
            ObjectInfoUI.Instance.OnPanelClosed += HandlePanelClosed;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from event
        if (ObjectInfoUI.Instance != null)
        {
            ObjectInfoUI.Instance.OnPanelClosed -= HandlePanelClosed;
        }

        // Clean up instantiated material to prevent memory leak
        if (objectMaterial != null)
        {
            Destroy(objectMaterial);
        }
    }

    void Awake()
    {
        if (interactableMask.value == 0)
            interactableMask = LayerMask.GetMask("Interactable");
    }

    void Update()
    {
        // If using controller highlighting, MinimalInteractionManager handles it
        if (useControllerHighlighting)
        {
            // Just show hint effect when player is near (optional)
            if (playerTransform != null && highlightEffect != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                highlightEffect.SetActive(distanceToPlayer <= interactionRadius);
            }
            return;
        }

        // OLD SYSTEM: Head-based highlighting (backward compatibility)
        if (playerTransform == null) return;

        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Debug.DrawRay(ray.origin, ray.direction * (interactionRadius + 1f), Color.yellow);
        }

        if (requiresDirectLook)
        {
            if (IsPlayerLookingAtObject())
            {
                HighlightObject(true);
                ShowInteractionPrompt(true);

                if (autoExamineDelay > 0)
                {
                    lookTimer += Time.deltaTime;
                    if (lookTimer >= autoExamineDelay)
                    {
                        ExamineObjectAsync();
                        lookTimer = 0f;
                    }
                }
            }
            else
            {
                lookTimer = 0f;
                HighlightObject(false);
                ShowInteractionPrompt(false);
            }
        }
        else
        {
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
            var io = hit.collider.GetComponentInParent<InteractiveObject>();
            if (io == null) return false;
            if (io == this) return true;
            return false;
        }

        return false;
    }

    void HighlightObject(bool highlight)
    {
        if (isHighlighted == highlight) return;

        isHighlighted = highlight;

        if (objectMaterial != null)
        {
            if (highlight)
            {
                objectMaterial.color = highlightColor;
                objectMaterial.SetColor("_EmissionColor", highlightColor * glowIntensity);
            }
            else
            {
                objectMaterial.color = originalColor;
                objectMaterial.SetColor("_EmissionColor", Color.black);
            }
        }

        if (highlightEffect != null)
        {
            highlightEffect.SetActive(highlight);
        }
    }

    public void SetHighlight(bool highlight)
    {
        HighlightObject(highlight);
        ShowInteractionPrompt(highlight);
    }

    public void TriggerExamination()
    {
        if (Time.time - lastExaminationTime < examinationCooldown)
        {
            Debug.Log($"[InteractiveObject] Debounced: {gameObject.name} (cooldown {examinationCooldown}s)");
            return;
        }
        lastExaminationTime = Time.time;
        ExamineObjectAsync();
    }

    /// <summary>
    /// Called when player examines the object
    /// </summary>
    async Task ExamineObjectAsync()
    {
        Debug.Log($"Examining object...");

        // Play sound
        if (audioSource != null && examinationSound != null)
        {
            audioSource.PlayOneShot(examinationSound);
        }

        // Get localized title
        var titleTask = objectTitle.GetLocalizedStringAsync();
        await titleTask.Task;
        string localizedTitle = titleTask.Result;
        currentLocalizedTitle = localizedTitle;

        // Start tracking
        if (trackInteractionTime && !currentlyExamining)
        {
            StartInteractionTracking(localizedTitle);
        }

        // Track object view for curiosity detection
        CuriosityIntegration curiosity = GetComponent<CuriosityIntegration>();
        if (curiosity != null)
        {
            curiosity.OnObjectExamined();
        }

        // Use LLM to generate adaptive content
        // Content length is now determined by closed-loop engagement (including reading behavior)
        if (LLMAdaptiveContentManager.Instance != null)
        {
            // Show loading panel first - ✅ NOW PASSES objectId
            if (ObjectInfoUI.Instance != null)
            {
                ObjectInfoUI.Instance.ShowObjectInfo(gameObject.name, localizedTitle, "Loading...", objectImage);
            }

            // Get LLM-generated content with OBJECT type
            LLMAdaptiveContentManager.Instance.GetAdaptiveContent(
                gameObject.name,
                localizedTitle,
                ContentType.Object,
                (generatedContent) =>
                {
                    // Content generated! Update UI - ✅ PASSES objectId
                    if (ObjectInfoUI.Instance != null)
                    {
                        ObjectInfoUI.Instance.ShowObjectInfo(gameObject.name, localizedTitle, generatedContent, objectImage);
                    }

                    Debug.Log($"[InteractiveObject] Displayed adaptive content for {localizedTitle}");
                }
            );
        }
        else
        {
            // Fallback to localized description
            var descTask = objectDescription.GetLocalizedStringAsync();
            await descTask.Task;
            string localizedDescription = descTask.Result;

            if (ObjectInfoUI.Instance != null)
            {
                // ✅ PASSES objectId for tracking even in fallback
                ObjectInfoUI.Instance.ShowObjectInfo(gameObject.name, localizedTitle, localizedDescription, objectImage);
            }
        }
    }

    void ShowInteractionPrompt(bool show)
    {
        // Placeholder for UI prompt
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }

    // ============================================================================
    // EVENT-BASED TRACKING (replaces polling coroutine)
    // ============================================================================

    /// <summary>
    /// Start tracking when object is examined
    /// </summary>
    void StartInteractionTracking(string localizedTitle)
    {
        currentlyExamining = true;
        interactionStartTime = Time.unscaledTime;
        totalInteractions++;
        Debug.Log($"📊 Started tracking: {localizedTitle} (Interaction #{totalInteractions})");
    }

    /// <summary>
    /// Handle panel closed event from ObjectInfoUI
    /// This replaces the polling coroutine with event-based tracking
    /// </summary>
    void HandlePanelClosed(string objectId, string objectName, float viewDuration, bool wasLikelyRead)
    {
        // Only process if this is the object we're tracking
        if (!currentlyExamining)
            return;

        // Check if this event is for our object
        if (objectId != gameObject.name)
            return;

        currentlyExamining = false;
        totalTimeSpent += viewDuration;

        Debug.Log($"📊 {currentLocalizedTitle}: Examined for {viewDuration:F1}s (wasLikelyRead: {wasLikelyRead})");
        Debug.Log($"   Total: {totalTimeSpent:F1}s over {totalInteractions} interactions");

        // Save to Firebase
        SaveInteractionToFirebase(viewDuration, currentLocalizedTitle, wasLikelyRead);
    }

    /// <summary>
    /// Save interaction data to Firebase via centralized FirebaseLogger
    /// </summary>
    async void SaveInteractionToFirebase(float duration, string objectName, bool wasLikelyRead)
    {
        Debug.Log($"  INTERACTION DATA:");
        Debug.Log($"   Object: {objectName}");
        Debug.Log($"   Duration: {duration:F1} seconds");
        Debug.Log($"   Was Likely Read: {wasLikelyRead}");
        Debug.Log($"   Total Interactions: {totalInteractions}");
        Debug.Log($"   Average Time: {(totalTimeSpent / totalInteractions):F1} seconds");

        if (!FirebaseLogger.HasSession)
        {
            Debug.LogWarning("FirebaseManager not ready - interaction only logged locally");
            return;
        }

        float avgTime = totalTimeSpent / totalInteractions;

        // Session-scoped write — single source of truth for object viewing data
        var viewData = new Dictionary<string, object>
        {
            { "objectId", gameObject.name },
            { "objectName", objectName },
            { "viewDuration", duration },
            { "wasLikelyRead", wasLikelyRead },
            { "totalInteractions", totalInteractions },
            { "totalTimeSpent", totalTimeSpent },
            { "averageTime", avgTime }
        };

        string docId = FirebaseLogger.GenerateDocId(gameObject.name);
        await FirebaseLogger.LogSessionData("objectViewingTime", viewData, docId, "[InteractiveObject]");
    }
}