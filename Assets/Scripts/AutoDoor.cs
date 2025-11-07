using UnityEngine;

public class AutoDoor : MonoBehaviour
{
    public Transform door;
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public float snapThreshold = 0.1f;

    private bool playerNear = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Collider doorCollider;

    void Start()
    {
        // Store initial closed rotation
        closedRotation = door.rotation;

        // Calculate open rotation
        openRotation = Quaternion.Euler(door.eulerAngles + new Vector3(0, openAngle, 0));

        // Get door collider and warn if missing
        doorCollider = door.GetComponent<Collider>();
        if (doorCollider == null)
        {
            Debug.LogWarning("Door has no collider attached! Collider toggling will be disabled.", this);
        }
    }

    void Update()
    {
        // Determine target rotation based on player proximity
        Quaternion targetRotation = playerNear ? openRotation : closedRotation;

        // Smoothly rotate door towards target
        door.rotation = Quaternion.Slerp(door.rotation, targetRotation, Time.deltaTime * openSpeed);

        // Calculate angle to target
        float angleToTarget = Quaternion.Angle(door.rotation, targetRotation);

        // Snap to final position when very close to prevent jittering
        if (angleToTarget < snapThreshold)
        {
            door.rotation = targetRotation;
        }

        // Handle collider state if collider exists
        if (doorCollider != null)
        {
            if (playerNear && angleToTarget < 5f && doorCollider.enabled)
            {
                doorCollider.enabled = false;
            }
            else if (!playerNear && angleToTarget < 5f && !doorCollider.enabled)
            {
                doorCollider.enabled = true;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
        }
    }
}