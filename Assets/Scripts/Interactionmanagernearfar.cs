using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Quest 3 Interaction Manager - Works with Near-Far Interactor
/// Compatible with XR Interaction Toolkit 3.0+ Starter Assets prefab
/// </summary>
public class InteractionManagerNearFar : MonoBehaviour
{
    public static InteractionManagerNearFar Instance { get; private set; }

    [Header("XR Interactor References")]
    [Tooltip("Right hand interactor (XRRayInteractor or Near-Far Interactor)")]
    public XRBaseInteractor rightHandInteractor;

    [Tooltip("Left hand interactor (XRRayInteractor or Near-Far Interactor)")]
    public XRBaseInteractor leftHandInteractor;

    [Header("Settings")]
    public float interactionDistance = 10f;
    public LayerMask interactableMask;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Currently targeted objects
    private InteractiveObject currentObjectTarget;
    private HiddenCard currentCardTarget;

    // Input Actions
    private InputAction rightTriggerAction;
    private InputAction leftTriggerAction;

    // Public accessors for hand tracking
    public Vector3 RightHandPosition => rightHandInteractor != null ? rightHandInteractor.transform.position : Vector3.zero;
    public Vector3 LeftHandPosition => leftHandInteractor != null ? leftHandInteractor.transform.position : Vector3.zero;
    public bool IsRightHandActive => rightHandInteractor != null && rightHandInteractor.enabled;
    public bool IsLeftHandActive => leftHandInteractor != null && leftHandInteractor.enabled;

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

        // Setup trigger input
        rightTriggerAction = new InputAction("RightTrigger", binding: "<XRController>{RightHand}/triggerPressed");
        rightTriggerAction.Enable();

        leftTriggerAction = new InputAction("LeftTrigger", binding: "<XRController>{LeftHand}/triggerPressed");
        leftTriggerAction.Enable();
    }

    void Start()
    {
        Debug.Log("=== NEAR-FAR INTERACTION MANAGER STARTING ===");

        if (interactableMask.value == 0)
        {
            interactableMask = LayerMask.GetMask("Interactable");
        }

        // Auto-find interactors if not assigned
        if (rightHandInteractor == null)
        {
            rightHandInteractor = FindInteractor("Right");
        }

        if (leftHandInteractor == null)
        {
            leftHandInteractor = FindInteractor("Left");
        }

        Debug.Log($"Right Hand Interactor: {(rightHandInteractor != null ? "✅ " + rightHandInteractor.name : "❌ Not Found")}");
        Debug.Log($"Left Hand Interactor: {(leftHandInteractor != null ? "✅ " + leftHandInteractor.name : "❌ Not Found")}");

        if (rightHandInteractor == null && leftHandInteractor == null)
        {
            Debug.LogError("❌ NO INTERACTORS FOUND! Please assign manually in Inspector.");
        }
    }

    XRBaseInteractor FindInteractor(string handName)
    {
        // Try to find Near-Far Interactor first (new prefab style)
        string[] searchPatterns = {
            $"{handName} Controller/Near-Far Interactor",
            $"{handName}Hand Controller/Near-Far Interactor",
            $"{handName} Controller",
            $"{handName}Hand Controller"
        };

        foreach (string pattern in searchPatterns)
        {
            // Try finding as child path first
            Transform found = transform.Find($"Camera Offset/{pattern}");
            if (found != null)
            {
                var interactor = found.GetComponent<XRBaseInteractor>();
                if (interactor != null)
                {
                    if (showDebugLogs) Debug.Log($"Found {handName} interactor: {found.name}");
                    return interactor;
                }
            }

            // Try finding by GameObject.Find
            GameObject obj = GameObject.Find(pattern);
            if (obj != null)
            {
                var interactor = obj.GetComponent<XRBaseInteractor>();
                if (interactor != null)
                {
                    if (showDebugLogs) Debug.Log($"Found {handName} interactor: {obj.name}");
                    return interactor;
                }
            }
        }

        // Last resort - find any XRRayInteractor on controllers
        foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None))
        {
            if (ray.name.Contains(handName))
            {
                if (showDebugLogs) Debug.Log($"Found {handName} XRRayInteractor: {ray.name}");
                return ray;
            }
        }

        Debug.LogWarning($"⚠️ Could not find {handName} hand interactor automatically.");
        return null;
    }

    void Update()
    {
        UpdateCurrentTarget();

        if (rightTriggerAction.triggered || leftTriggerAction.triggered)
        {
            TryInteract();
        }
    }

    void UpdateCurrentTarget()
    {
        currentObjectTarget = null;
        currentCardTarget = null;

        // Use right hand as primary, left as fallback
        XRBaseInteractor activeInteractor = GetActiveInteractor();
        if (activeInteractor == null) return;

        // Check if it's a ray interactor (has raycast capability)
        if (activeInteractor is XRRayInteractor rayInteractor)
        {
            // Use XRRayInteractor's raycast
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                ProcessHit(hit);
            }
        }
        else
        {
            // Fallback: manual raycast from interactor position
            Ray ray = new Ray(activeInteractor.transform.position, activeInteractor.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableMask))
            {
                ProcessHit(hit);
            }
        }
    }

    void ProcessHit(RaycastHit hit)
    {
        if (hit.distance > interactionDistance) return;

        // Check for InteractiveObject
        InteractiveObject io = hit.collider.GetComponentInParent<InteractiveObject>();
        if (io != null)
        {
            currentObjectTarget = io;
            return;
        }

        // Check for HiddenCard
        HiddenCard card = hit.collider.GetComponent<HiddenCard>();
        if (card == null) card = hit.collider.GetComponentInParent<HiddenCard>();

        if (card != null && !card.IsDiscovered())
        {
            currentCardTarget = card;
        }
    }

    XRBaseInteractor GetActiveInteractor()
    {
        // Prefer right hand
        if (rightHandInteractor != null && rightHandInteractor.enabled && rightHandInteractor.isActiveAndEnabled)
        {
            return rightHandInteractor;
        }

        // Fallback to left hand
        if (leftHandInteractor != null && leftHandInteractor.enabled && leftHandInteractor.isActiveAndEnabled)
        {
            return leftHandInteractor;
        }

        return null;
    }

    public void TryInteract()
    {
        if (currentObjectTarget != null)
        {
            if (showDebugLogs) Debug.Log($"🎯 Interacting with: {currentObjectTarget.objectTitle}");
            currentObjectTarget.TriggerExamination();
        }
        else if (currentCardTarget != null)
        {
            if (showDebugLogs) Debug.Log($"📜 Collecting card: {currentCardTarget.cardTitle}");
            currentCardTarget.TriggerCollection();
        }
        else
        {
            if (showDebugLogs) Debug.Log("❌ Nothing to interact with");
        }
    }

    public bool HasTarget()
    {
        return currentObjectTarget != null || currentCardTarget != null;
    }

    void OnDestroy()
    {
        rightTriggerAction?.Disable();
        rightTriggerAction?.Dispose();
        leftTriggerAction?.Disable();
        leftTriggerAction?.Dispose();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        XRBaseInteractor active = GetActiveInteractor();
        if (active != null)
        {
            Gizmos.color = HasTarget() ? Color.green : Color.yellow;
            Gizmos.DrawRay(active.transform.position, active.transform.forward * interactionDistance);
        }
    }
}