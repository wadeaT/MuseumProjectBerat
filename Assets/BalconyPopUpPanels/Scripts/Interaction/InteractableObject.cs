using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableObject : MonoBehaviour
{
    [TextArea(1, 3)] public string objectTitle = "Object Title";
    [TextArea(3, 10)] public string objectDescription = "Object description...";
    public Sprite objectImage; // optional

    [Header("Popup spawn offset from object center")]
    public Vector3 popupOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Proximity")]
    public float requiredDistance = 3.0f;  // must be within this to open
    public bool requireInRange = true;

    private Transform player;
    private System.Func<PopupPanel> requestPanel; // assigned by manager

    void Reset()
    {
        // Make sure collider is not a trigger so raycast hits it
        var col = GetComponent<Collider>();
        col.isTrigger = false;
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    public void Init(Transform playerTransform, System.Func<PopupPanel> popupProvider)
    {
        player = playerTransform;
        requestPanel = popupProvider;
    }

    public bool IsInRange()
    {
        if (player == null) return true;
        float d = Vector3.Distance(player.position, transform.position);
        return d <= requiredDistance;
    }

    public void OnSelected()
    {
        if (requireInRange && !IsInRange()) return;
        if (requestPanel == null) return;

        PopupPanel panel = requestPanel.Invoke();
        panel.SetContent(objectTitle, objectDescription, objectImage);
        Vector3 spawnPos = transform.position + popupOffset;
        panel.ShowAt(spawnPos);
    }
}
