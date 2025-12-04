using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRObjectInteraction : MonoBehaviour
{
    [Header("Object Info")]
    public string objectTitle = "Museum Artifact";

    [TextArea(3, 6)]
    public string objectDescription = "Description here";

    public Sprite objectImage;

    [Header("Audio")]
    public AudioClip examinationSound;

    private XRBaseInteractable interactable;
    private AudioSource audioSource;

    void Start()
    {
        // Get XR Simple Interactable
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
        {
            Debug.LogError($"{objectTitle}: Missing XR Simple Interactable component!");
            return;
        }

        // Listen for VR controller interaction
        interactable.selectEntered.AddListener(OnVRSelect);

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && examinationSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void OnVRSelect(SelectEnterEventArgs args)
    {
        Debug.Log($"VR: Examining {objectTitle}");

        // Play sound
        if (audioSource != null && examinationSound != null)
        {
            audioSource.PlayOneShot(examinationSound);
        }

        // Show info panel
        if (ObjectInfoUI.Instance != null)
        {
            ObjectInfoUI.Instance.ShowObjectInfo(objectTitle, objectDescription, objectImage);
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnVRSelect);
        }
    }
}