using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// InteractiveObject - WebGL-safe version using coroutines instead of async/await
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
    private string cachedLocalizedTitle;

    void Awake()
    {
        if (interactableMask.value == 0)
            interactableMask = LayerMask.GetMask("Interactable");
    }

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
                Debug.LogError($"[InteractiveObject] ({name}): No camera found!");
            }
        }
        else
        {
            Debug.LogError("[InteractiveObject] No GameObject with 'Player' tag found!");
        }

        // Get renderer and store original material
        objectRenderer = GetComponent<Renderer>();

        // If no renderer on this object, try to find one in children
        if (objectRenderer == null)
        {
            objectRenderer = GetComponentInChildren<Renderer>();

            if (objectRenderer != null)
            {
                Debug.Log($"[InteractiveObject] {name}: Using renderer from child object '{objectRenderer.gameObject.name}'");
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
            Debug.LogWarning($"[InteractiveObject] {name}: No Renderer found on this object or its children. Highlight won't work.");
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

        // Cache the localized title for tracking
        CacheLocalizedTitle();
    }

    private void CacheLocalizedTitle()
    {
        if (objectTitle != null && !objectTitle.IsEmpty)
        {
            var op = objectTitle.GetLocalizedStringAsync();
            op.Completed += (handle) =>
            {
                cachedLocalizedTitle = handle.Result;
            };
        }
        else
        {
            cachedLocalizedTitle = name;
        }
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
                        ExamineObject();
                        lookTimer = 0f;
                    }
                }
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
        if (isHighlighted == highlight) return;

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

    public void TriggerExamination()
    {
        ExamineObject();
    }

    /// <summary>
    /// Called when player examines the object - coroutine-based
    /// </summary>
    void ExamineObject()
    {
        Debug.Log($"[InteractiveObject] Examining: {name}");

        // Play sound
        if (audioSource != null && examinationSound != null)
        {
            audioSource.PlayOneShot(examinationSound);
        }

        // Start tracking AND wait for panel close
        if (trackInteractionTime && !currentlyExamining)
        {
            StartInteractionTracking();
            StartCoroutine(WaitForPanelClose());
        }

        // Start coroutine to get localized strings and show UI
        StartCoroutine(ShowInfoCoroutine());
    }

    private IEnumerator ShowInfoCoroutine()
    {
        string localizedTitle = cachedLocalizedTitle ?? name;
        string localizedDescription = "";

        // Get localized title if not cached
        if (objectTitle != null && !objectTitle.IsEmpty)
        {
            bool titleLoaded = false;
            var titleOp = objectTitle.GetLocalizedStringAsync();
            titleOp.Completed += (handle) =>
            {
                localizedTitle = handle.Result;
                titleLoaded = true;
            };

            // Wait with timeout
            float timeout = 0f;
            while (!titleLoaded && timeout < 2f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
        }

        // Get localized description
        if (objectDescription != null && !objectDescription.IsEmpty)
        {
            bool descLoaded = false;
            var descOp = objectDescription.GetLocalizedStringAsync();
            descOp.Completed += (handle) =>
            {
                localizedDescription = handle.Result;
                descLoaded = true;
            };

            // Wait with timeout
            float timeout = 0f;
            while (!descLoaded && timeout < 2f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
        }

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
    void StartInteractionTracking()
    {
        currentlyExamining = true;
        interactionStartTime = Time.time;
        totalInteractions++;
        Debug.Log($"[InteractiveObject] Started tracking: {cachedLocalizedTitle ?? name} (Interaction #{totalInteractions})");
    }

    /// <summary>
    /// Stop tracking and save to Firebase
    /// </summary>
    void StopInteractionTracking()
    {
        if (!currentlyExamining) return;

        currentlyExamining = false;
        float timeSpent = Time.time - interactionStartTime;
        totalTimeSpent += timeSpent;
        string objectName = cachedLocalizedTitle ?? name;
        Debug.Log($"[InteractiveObject] {objectName}: Examined for {timeSpent:F1} seconds (Total: {totalTimeSpent:F1}s over {totalInteractions} interactions)");

        // Save to Firebase
        SaveInteractionToFirebase(timeSpent, objectName);
    }

    /// <summary>
    /// Save interaction data to Firebase - non-blocking
    /// </summary>
    void SaveInteractionToFirebase(float duration, string objectName)
    {
        // Log to console
        Debug.Log($"[InteractiveObject] INTERACTION DATA:");
        Debug.Log($"  Object: {objectName}");
        Debug.Log($"  Duration: {duration:F1} seconds");
        Debug.Log($"  Total Interactions: {totalInteractions}");
        Debug.Log($"  Average Time: {(totalTimeSpent / totalInteractions):F1} seconds");
        Debug.Log($"  Timestamp: {DateTime.UtcNow}");

        // Save to Firebase
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            // Get current user ID (WebGL uses CurrentParticipantCode)
            string userId = FirebaseManager.Instance.CurrentParticipantCode;

            if (!string.IsNullOrEmpty(userId))
            {
                float avgTime = totalTimeSpent / totalInteractions;

                FirebaseManager.Instance.SaveObjectInteraction(
                    userId,
                    objectName,
                    duration,
                    totalInteractions,
                    avgTime,
                    (success) =>
                    {
                        if (!success)
                        {
                            Debug.LogWarning($"[InteractiveObject] Failed to save interaction to Firebase");
                        }
                    }
                );
            }
            else
            {
                Debug.LogWarning("[InteractiveObject] No user logged in - interaction not saved to Firebase");
            }
        }
        else
        {
            Debug.LogWarning("[InteractiveObject] FirebaseManager not ready - interaction only logged locally");
        }
    }

    /// <summary>
    /// Wait for panel to close, then stop tracking
    /// </summary>
    IEnumerator WaitForPanelClose()
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