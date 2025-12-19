using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Manages object and card interaction via a dedicated button instead of screen taps.
/// Works with both InteractiveObject and HiddenCard.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The button the user presses to interact")]
    public Button interactButton;

    [Tooltip("Main camera used for raycasting")]
    public Camera playerCamera;

    [Header("Raycast Settings")]
    [Tooltip("How far to check for interactive objects")]
    public float interactionDistance = 10f;

    [Tooltip("Layer mask for interactive objects")]
    public LayerMask interactableMask;

    [Header("Visual Feedback")]
    [Tooltip("Optional: Change button color when object is in range")]
    public bool changeButtonWhenTargeting = true;

    [Tooltip("Button color when targeting an object")]
    public Color targetingColor = new Color(1f, 0.9f, 0.6f, 1f);

    [Tooltip("Button color when not targeting")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.5f);

    // Currently targeted objects
    private InteractiveObject currentObjectTarget;
    private HiddenCard currentCardTarget;
    private Image buttonImage;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Debug.Log("=== INTERACTION MANAGER STARTING ===");

        // Find camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        Debug.Log($"Camera assigned: {playerCamera != null}");

        // Setup button
        if (interactButton != null)
        {
            interactButton.onClick.AddListener(OnInteractButtonPressed);
            buttonImage = interactButton.GetComponent<Image>();

            if (buttonImage != null && changeButtonWhenTargeting)
            {
                buttonImage.color = normalColor;
            }
            Debug.Log(" Button listener added!");
        }
        else
        {
            Debug.LogError(" InteractionManager: No interact button assigned!");
        }

        // Setup layer mask if not set
        if (interactableMask.value == 0)
        {
            interactableMask = LayerMask.GetMask("Interactable");
            Debug.Log($"Layer mask auto-set to Interactable: {interactableMask.value}");
        }
    }

    void Update()
    {
        UpdateCurrentTarget();

        // Also allow keyboard interaction (E key) for PC/Editor testing
        if (Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    /// <summary>
    /// Raycast to find what object/card the player is looking at
    /// </summary>
    void UpdateCurrentTarget()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Reset targets
        currentObjectTarget = null;
        currentCardTarget = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            // Check for InteractiveObject
            InteractiveObject io = hit.collider.GetComponentInParent<InteractiveObject>();
            if (io != null)
            {
                currentObjectTarget = io;
                UpdateButtonVisual(true);
                return;
            }

            // Check for HiddenCard
            HiddenCard card = hit.collider.GetComponent<HiddenCard>();
            if (card == null)
            {
                card = hit.collider.GetComponentInParent<HiddenCard>();
            }

            if (card != null && !card.IsDiscovered())
            {
                currentCardTarget = card;
                UpdateButtonVisual(true);
                return;
            }
        }

        // Not looking at anything interactable
        UpdateButtonVisual(false);
    }

    void UpdateButtonVisual(bool hasTarget)
    {
        if (buttonImage != null && changeButtonWhenTargeting)
        {
            buttonImage.color = hasTarget ? targetingColor : normalColor;
        }
    }

    /// <summary>
    /// Called when the interact button is pressed
    /// </summary>
    void OnInteractButtonPressed()
    {
        Debug.Log(" INTERACT BUTTON PRESSED!");
        TryInteract();
    }

    /// <summary>
    /// Attempt to interact with the current target
    /// </summary>
    public void TryInteract()
    {
        if (currentObjectTarget != null)
        {
            Debug.Log($" Interacting with OBJECT: {currentObjectTarget.objectTitle}");
            currentObjectTarget.TriggerExamination();
        }
        else if (currentCardTarget != null)
        {
            Debug.Log($" Interacting with CARD: {currentCardTarget.cardTitle}");
            currentCardTarget.TriggerCollection();
        }
        else
        {
            Debug.Log(" Nothing to interact with");
        }
    }

    /// <summary>
    /// Check if currently targeting something
    /// </summary>
    public bool HasTarget()
    {
        return currentObjectTarget != null || currentCardTarget != null;
    }
}