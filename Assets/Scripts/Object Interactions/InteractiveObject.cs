using System;
using System.Collections.Generic;
using UnityEngine;

public class InteractiveObject : MonoBehaviour
{
    [Header("Object Information")]
    [Tooltip("Name of this object (e.g., 'Copper Coffee Pot')")]
    public string objectTitle = "Museum Artifact";

    [Tooltip("Description of this object and its historical significance")]
    [TextArea(4, 10)]
    public string objectDescription = "This artifact tells a story of Ottoman-era life in Berat. Fill in the description here.";
    [Tooltip("Optional image of this object")]
    public Sprite objectImage; 

    [Header("Interaction Settings")]
    [Tooltip("How close must the player be to interact? (in meters)")]
    public float interactionRadius = 2.5f;

    [Tooltip("Must the player look directly at the object?")]
    public bool requiresDirectLook = true;

    [Tooltip("Key to press to examine the object")]
    public KeyCode interactionKey = KeyCode.E;

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

    void Update()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Check if player is in range
        if (distanceToPlayer <= interactionRadius)
        {
            // Check if looking at object (if required)
            if (requiresDirectLook)
            {
                if (IsPlayerLookingAtObject())
                {
                    HighlightObject(true);
                    ShowInteractionPrompt(true);

                    // Player can interact
                    if (Input.GetKeyDown(interactionKey))
                    {
                        ExamineObject();
                    }
                }
                else
                {
                    HighlightObject(false);
                    ShowInteractionPrompt(false);
                }
            }
            else
            {
                // Auto-highlight if in range (no look requirement)
                HighlightObject(true);
                ShowInteractionPrompt(true);

                if (Input.GetKeyDown(interactionKey))
                {
                    ExamineObject();
                }
            }
        }
        else
        {
            // Player too far away
            HighlightObject(false);
            ShowInteractionPrompt(false);
        }
    }

    /// <summary>
    /// Check if player is looking at this object
    /// </summary>
    bool IsPlayerLookingAtObject()
    {
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionRadius + 1f))
        {
            if (hit.collider.gameObject == gameObject)
            {
                return true;
            }
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
    /// Called when player examines the object
    /// </summary>
    void ExamineObject()
    {
        Debug.Log($"Examining: {objectTitle}");

        // Play sound
        if (audioSource != null && examinationSound != null)
        {
            audioSource.PlayOneShot(examinationSound);
        }

        // ✅ Start tracking AND wait for panel close
        if (trackInteractionTime && !currentlyExamining)
        {
            StartInteractionTracking();
            StartCoroutine(WaitForPanelClose()); // ← ADD THIS LINE!
        }

        // Show info panel
        if (ObjectInfoUI.Instance != null)
        {
            ObjectInfoUI.Instance.ShowObjectInfo(objectTitle, objectDescription, objectImage);
        }
        else
        {
            Debug.LogWarning("ObjectInfoUI not found!");

            if (CardDiscoveryUI.Instance != null)
            {
                CardDiscoveryUI.Instance.ShowCardDiscovery(objectTitle, objectDescription);
            }
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

        Debug.Log($"📊 Started tracking: {objectTitle} (Interaction #{totalInteractions})");
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

        Debug.Log($"📊 {objectTitle}: Examined for {timeSpent:F1} seconds (Total: {totalTimeSpent:F1}s over {totalInteractions} interactions)");

        // Save to Firebase
        SaveInteractionToFirebase(timeSpent);
    }

    /// <summary>
    /// Save interaction data to Firebase
    /// </summary>

    async void SaveInteractionToFirebase(float duration)
    {
        // Log to console
        Debug.Log($"📊 INTERACTION DATA:");
        Debug.Log($"   Object: {objectTitle}");
        Debug.Log($"   Duration: {duration:F1} seconds");
        Debug.Log($"   Total Interactions: {totalInteractions}");
        Debug.Log($"   Average Time: {(totalTimeSpent / totalInteractions):F1} seconds");
        Debug.Log($"   Timestamp: {System.DateTime.UtcNow}");

        // Save to Firebase
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            // Get current user ID
            string userId = FirebaseManager.Instance.Auth?.CurrentUser?.UserId;

            if (!string.IsNullOrEmpty(userId))
            {
                float avgTime = totalTimeSpent / totalInteractions;

                await FirebaseManager.Instance.SaveObjectInteractionAsync(
                    userId,
                    objectTitle,
                    duration,
                    totalInteractions,
                    avgTime
                );
            }
            else
            {
                Debug.LogWarning("⚠️ No user logged in - interaction not saved to Firebase");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ FirebaseManager not ready - interaction only logged locally");
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