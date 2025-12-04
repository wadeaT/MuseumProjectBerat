using UnityEngine;
using UnityEngine.InputSystem; // ✅ NEW INPUT SYSTEM

public class HiddenCard : MonoBehaviour
{
    [Header("Card Identity")]
    [Tooltip("Unique ID for this card (e.g., 'balcony_card_01')")]
    public string cardID = "balcony_card_01";

    [Tooltip("Which room is this card in? (e.g., 'balcony', 'living_room')")]
    public string roomID = "balcony";

    [Tooltip("Title shown when discovered")]
    public string cardTitle = "The Heart of the Home";

    [Tooltip("Description/story for this card")]
    [TextArea(3, 6)]
    public string cardDescription = "This balcony, called a 'çardak' in Albanian, was the social center of traditional Berat homes. Families gathered here for meals, women wove textiles, and honored guests were welcomed with coffee and raki. The wooden railings allowed residents to observe street life below while maintaining privacy—a clever feature of Ottoman-era architecture.";

    [Header("Discovery Settings")]
    [Tooltip("How close must the player be to notice this card? (in meters)")]
    public float detectionRadius = 3f;

    [Tooltip("Must the player look directly at the card to collect it?")]
    public bool requiresDirectLook = true;

    [Tooltip("Interaction key (for non-VR testing)")]
    public KeyCode interactionKey = KeyCode.E;

    [Tooltip("Enable mobile touch controls (tap to collect)")]
    public bool useMobileControls = true;

    [Tooltip("Auto-collect after looking for this many seconds (0 = disabled)")]
    [Range(0f, 3f)]
    public float autoCollectDelay = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Particle effect or glow that shows when player is near")]
    public GameObject hintEffect;

    [Tooltip("Color when idle")]
    public Color idleColor = new Color(1f, 0.6f, 0.2f); // Warm orange

    [Tooltip("Color when player can collect")]
    public Color activeColor = new Color(1f, 0.8f, 0f); // Bright gold

    [Header("Audio")]
    [Tooltip("Sound when card is discovered (optional)")]
    public AudioClip discoverySound;

    // Internal variables
    private bool isDiscovered = false;
    private Transform playerTransform;
    private Camera playerCamera;
    private Renderer cardRenderer;
    private AudioSource audioSource;
    private Material cardMaterial;
    private float lookTimer = 0f; // For auto-collect feature

    void Start()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;

            // Try to find the camera - check main camera first, then search in children
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                // If Camera.main doesn't work, search for camera in player's children
                playerCamera = player.GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                Debug.LogError("HiddenCard: No camera found! Card interaction won't work.");
            }
            else
            {
                Debug.Log($"HiddenCard: Camera found - {playerCamera.name}");
            }
        }
        else
        {
            Debug.LogError("HiddenCard: No GameObject with 'Player' tag found! Please tag your player.");
        }

        // Get the renderer to change colors
        cardRenderer = GetComponent<Renderer>();
        if (cardRenderer != null)
        {
            cardMaterial = new Material(cardRenderer.material);
            cardRenderer.material = cardMaterial;
            cardMaterial.color = idleColor;

            // Make it glow
            cardMaterial.EnableKeyword("_EMISSION");
            cardMaterial.SetColor("_EmissionColor", idleColor * 0.5f);
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && discoverySound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Hide hint effect initially
        if (hintEffect != null)
        {
            hintEffect.SetActive(false);
        }

        // Check if already discovered
        CheckIfAlreadyDiscovered();
    }

    void Update()
    {
        if (isDiscovered || playerTransform == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRadius)
        {
            if (hintEffect != null)
            {
                hintEffect.SetActive(true);
            }

            if (requiresDirectLook)
            {
                if (IsPlayerLookingAtCard())
                {
                    if (cardMaterial != null)
                    {
                        cardMaterial.color = activeColor;
                        cardMaterial.SetColor("_EmissionColor", activeColor);
                    }

                    ShowInteractionPrompt(true);

                    // ✅ NEW INPUT SYSTEM SUPPORT
                    bool interactionTriggered = false;

                    // Check for keyboard input (PC/Editor)
                    if (Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
                    {
                        interactionTriggered = true;
                    }

                    // Check for mobile touch/mouse click (unified input)
                    if (useMobileControls)
                    {
                        // Pointer works for both mouse and touch!
                        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                        {
                            interactionTriggered = true;
                        }
                    }

                    // Auto-collect after looking for X seconds
                    if (autoCollectDelay > 0)
                    {
                        lookTimer += Time.deltaTime;
                        if (lookTimer >= autoCollectDelay)
                        {
                            interactionTriggered = true;
                        }
                    }

                    if (interactionTriggered)
                    {
                        CollectCard();
                    }
                }
                else
                {
                    lookTimer = 0f; // Reset timer when not looking
                    if (cardMaterial != null)
                    {
                        cardMaterial.color = idleColor;
                        cardMaterial.SetColor("_EmissionColor", idleColor * 0.5f);
                    }
                    ShowInteractionPrompt(false);
                }
            }
            else
            {
                if (distanceToPlayer <= detectionRadius * 0.5f)
                {
                    CollectCard();
                }
            }
        }
        else
        {
            if (hintEffect != null)
            {
                hintEffect.SetActive(false);
            }
            ShowInteractionPrompt(false);
        }
    }

    bool IsPlayerLookingAtCard()
    {
        if (playerCamera == null)
            return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, detectionRadius + 2f))
        {
            if (hit.collider.gameObject == gameObject)
            {
                return true;
            }
        }

        return false;
    }

    void CollectCard()
    {
        isDiscovered = true;

        Debug.Log($"Card Discovered: {cardTitle}");
        if (BadgeManager.instance != null)
        {
            BadgeManager.instance.OnCardCollected(cardID, roomID); // ✅ Now passing roomID
        }

        if (audioSource != null && discoverySound != null)
        {
            audioSource.PlayOneShot(discoverySound);
        }

        // Show card discovery UI
        if (CardDiscoveryUI.Instance != null)
        {
            CardDiscoveryUI.Instance.ShowCardDiscovery(cardTitle, cardDescription);
        }

        if (cardRenderer != null)
        {
            cardRenderer.enabled = false;
        }

        if (hintEffect != null)
        {
            hintEffect.SetActive(false);
        }

        PlayerPrefs.SetInt($"Card_{cardID}_Found", 1);
        PlayerPrefs.Save();

        ShowInteractionPrompt(false);
    }

    void CheckIfAlreadyDiscovered()
    {
        if (PlayerPrefs.GetInt($"Card_{cardID}_Found", 0) == 1)
        {
            isDiscovered = true;

            if (cardRenderer != null)
            {
                cardRenderer.enabled = false;
            }

            Debug.Log($"Card {cardID} was already discovered previously.");
        }
    }

    void ShowInteractionPrompt(bool show)
    {
        // We'll implement this with UI later
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius * 0.5f);
    }
}