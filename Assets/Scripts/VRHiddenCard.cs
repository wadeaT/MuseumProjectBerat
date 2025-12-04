using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRHiddenCard : MonoBehaviour
{
    [Header("Card Identity")]
    public string cardID = "balcony_card_01";
    public string roomID = "balcony";
    public string cardTitle = "Card Title";

    [TextArea(3, 6)]
    public string cardDescription = "Description here";

    [Header("Audio")]
    public AudioClip discoverySound;

    private bool isDiscovered = false;
    private XRBaseInteractable interactable;
    private Renderer cardRenderer;
    private AudioSource audioSource;

    void Start()
    {
        cardRenderer = GetComponent<Renderer>();

        // Get XR Simple Interactable
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
        {
            Debug.LogError($"{cardTitle}: Missing XR Simple Interactable component!");
            return;
        }

        interactable.selectEntered.AddListener(OnVRCollect);

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && discoverySound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Check if already discovered
        CheckIfAlreadyDiscovered();
    }

    void OnVRCollect(SelectEnterEventArgs args)
    {
        if (isDiscovered) return;
        CollectCard();
    }

    void CollectCard()
    {
        isDiscovered = true;

        Debug.Log($"VR Card Discovered: {cardTitle}");

        // Notify badge manager
        if (BadgeManager.instance != null)
        {
            BadgeManager.instance.OnCardCollected(cardID, roomID);
        }

        // Play sound
        if (audioSource != null && discoverySound != null)
        {
            audioSource.PlayOneShot(discoverySound);
        }

        // Show card UI
        if (CardDiscoveryUI.Instance != null)
        {
            CardDiscoveryUI.Instance.ShowCardDiscovery(cardTitle, cardDescription);
        }

        // Hide card
        if (cardRenderer != null)
        {
            cardRenderer.enabled = false;
        }

        // Save
        PlayerPrefs.SetInt($"Card_{cardID}_Found", 1);
        PlayerPrefs.Save();
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
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnVRCollect);
        }
    }
}