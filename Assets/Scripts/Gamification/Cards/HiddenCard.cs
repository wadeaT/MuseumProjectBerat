using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization; // ✅ ADD THIS
using UnityEngine.Localization.Settings; // ✅ ADD THIS

public class HiddenCard : MonoBehaviour
{
    [Header("Card Identity")]
    [Tooltip("Unique ID for this card (e.g., 'balcony_card_01')")]
    public string cardID = "balcony_card_01";

    [Tooltip("Which room is this card in? (e.g., 'balcony', 'living_room')")]
    public string roomID = "balcony";

    [Header("Localized Text")]
    [Tooltip("Localized title for this card")]
    public LocalizedString cardTitle; // ✅ CHANGED from string to LocalizedString

    [Tooltip("Localized description/story for this card")]
    public LocalizedString cardDescription; // ✅ CHANGED from string to LocalizedString

    [Header("Discovery Settings")]
    [Tooltip("How close must the player be to notice this card? (in meters)")]
    public float detectionRadius = 3f;

    [Tooltip("Must the player look directly at the card to collect it?")]
    public bool requiresDirectLook = true;

    [Tooltip("Interaction key (for non-VR testing)")]
    public KeyCode interactionKey = KeyCode.E;

    [Tooltip("Auto-collect after looking for this many seconds (0 = disabled)")]
    [Range(0f, 3f)]
    public float autoCollectDelay = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Particle effect or glow that shows when player is near")]
    public GameObject hintEffect;

    [Tooltip("Color when idle")]
    public Color idleColor = new Color(1f, 0.6f, 0.2f);

    [Tooltip("Color when player can collect")]
    public Color activeColor = new Color(1f, 0.8f, 0f);

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
    private float lookTimer = 0f;

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
                Debug.LogError("HiddenCard: No camera found! Card interaction won't work.");
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

        StartCoroutine(ForceColliderRefresh());
    }

    System.Collections.IEnumerator ForceColliderRefresh()
    {
        yield return new WaitForSeconds(0.1f);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            yield return null;
            col.enabled = true;
            Debug.Log($"[{cardID}] Collider refreshed!");
        }
    }

    void Update()
    {
        if (isDiscovered || playerTransform == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (hintEffect != null)
        {
            hintEffect.SetActive(distanceToPlayer <= detectionRadius);
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

                if (autoCollectDelay > 0)
                {
                    lookTimer += Time.deltaTime;
                    if (lookTimer >= autoCollectDelay)
                    {
                        CollectCard();
                        lookTimer = 0f;
                    }
                }
            }
            else
            {
                lookTimer = 0f;
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

    public void TriggerCollection()
    {
        if (!isDiscovered)
        {
            CollectCard();
        }
    }

    public bool IsDiscovered()
    {
        return isDiscovered;
    }

    void CollectCard()
    {
        isDiscovered = true;

        // ✅ Get localized strings asynchronously
        var titleOperation = cardTitle.GetLocalizedStringAsync();
        var descOperation = cardDescription.GetLocalizedStringAsync();

        titleOperation.Completed += (op) =>
        {
            string localizedTitle = op.Result;
            Debug.Log($"Card Discovered: {localizedTitle}");

            descOperation.Completed += (descOp) =>
            {
                string localizedDesc = descOp.Result;

                // Show card discovery UI with localized text
                if (CardDiscoveryUI.Instance != null)
                {
                    CardDiscoveryUI.Instance.ShowCardDiscovery(localizedTitle, localizedDesc);
                }
            };
        };

        if (BadgeManager.instance != null)
        {
            BadgeManager.instance.OnCardCollected(cardID, roomID);
        }

        if (audioSource != null && discoverySound != null)
        {
            audioSource.PlayOneShot(discoverySound);
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
        // Placeholder for UI prompt
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius * 0.5f);
    }
}