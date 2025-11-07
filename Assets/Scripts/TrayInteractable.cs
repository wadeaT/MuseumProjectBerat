using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TrayInteractable : MonoBehaviour
{
    [Header("UI References")]
    public GameObject infoPanel;          // TrayInfoPanel (root)
    public TMP_Text titleText;            // TMP title
    public TMP_Text descriptionText;      // TMP body
    public RawImage infoImage;            // UI RawImage to show photo
    public Texture defaultTexture;        // Default texture for the image

    [Header("Default Content (optional)")]
    public string defaultTitle;
    [TextArea(3, 8)]
    public string defaultDescription;
    public Texture defaultImage;          // Changed from Sprite to Texture
    public AudioClip defaultAudioClip;

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public float maxClickDistance = 5f;   // max raycast distance to click

    private AudioSource audioSource;
    private bool isPlayerNear = false;
    private bool isOpen = false;
    private Camera mainCamera;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // default AudioSource settings (override if you want spatial)
        audioSource.playOnAwake = false;

        mainCamera = Camera.main;
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    void Update()
    {
        if (isPlayerNear)
        {
            if (Input.GetKeyDown(interactKey))
            {
                TogglePanel();
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryClickOpen();
            }
        }
        else
        {
            // If player walks away, ensure panel closed
            if (isOpen == true)
                ClosePanel();
        }
    }

    private void TogglePanel()
    {
        if (isOpen) ClosePanel();
        else OpenPanelWithDefaults();
    }

    private void OpenPanelWithDefaults()
    {
        SetContent(defaultTitle, defaultDescription, defaultImage, defaultAudioClip);
        OpenPanel();
    }

    private void OpenPanel()
    {
        if (infoPanel == null) return;

        infoPanel.SetActive(true);
        isOpen = true;

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();
    }

    private void ClosePanel()
    {
        if (infoPanel == null) return;

        infoPanel.SetActive(false);
        isOpen = false;

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // Allows setting content at runtime (call from other systems if needed)
    public void SetContent(string title, string description, Texture texture, AudioClip clip)
    {
        if (titleText != null) titleText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = description ?? "";

        // Set texture, fallback to defaultTexture if null
        if (infoImage != null)
        {
            infoImage.texture = texture != null ? texture : defaultTexture;
        }

        if (audioSource != null) audioSource.clip = clip;
    }

    // Try to open by clicking the tray (raycast)
    private void TryClickOpen()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance))
        {
            // Fixed: Added parentheses for correct logic evaluation
            if (hit.collider != null && (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform))
            {
                TogglePanel();
            }
        }
    }

    // Proximity detection
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            // Optional: show hint UI "Press E to interact"
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            ClosePanel();
            // Optional: hide hint UI
        }
    }
}