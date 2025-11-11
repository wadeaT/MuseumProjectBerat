using UnityEngine;

public class InteractionRaycaster : MonoBehaviour
{
    public float maxDistance = 6f;
    public LayerMask interactableMask = ~0; // by default everything
    public KeyCode interactKey = KeyCode.Mouse0; // left click

    [Header("Optional highlight")]
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer lastRenderer;

    [Header("Popup Panel")]
    public PopupPanel popupPrefab;

    private PopupPanel spawnedPanel;
    private Transform player;

    void Start()
    {
        player = Camera.main.transform;
    }

    void Update()
    {
        HandleAimAndClick();
    }

    private PopupPanel GetOrSpawnPanel()
    {
        if (spawnedPanel == null)
        {
            spawnedPanel = Instantiate(popupPrefab);
        }
        return spawnedPanel;
    }

    void HandleAimAndClick()
    {
        ClearHighlight();

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            var interactable = hit.collider.GetComponentInParent<InteractableObject>();
            if (interactable != null)
            {
                // give it panel provider and player reference once
                interactable.Init(player, GetOrSpawnPanel);

                // optional: highlight
                var rend = hit.collider.GetComponentInParent<Renderer>();
                if (rend != null && highlightMaterial != null)
                {
                    lastRenderer = rend;
                    originalMaterial = rend.sharedMaterial;
                    rend.sharedMaterial = highlightMaterial;
                }

                // click to interact
                if (Input.GetKeyDown(interactKey))
                {
                    interactable.OnSelected();
                }
            }
        }
    }

    void ClearHighlight()
    {
        if (lastRenderer != null && originalMaterial != null)
        {
            lastRenderer.sharedMaterial = originalMaterial;
            lastRenderer = null;
            originalMaterial = null;
        }
    }
}
