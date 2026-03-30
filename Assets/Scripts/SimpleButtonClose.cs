using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Close UI panels with Quest 3 controller buttons
/// Uses XR Input Actions for better VR compatibility
/// </summary>
public class SimpleButtonCloseVR : MonoBehaviour
{
    [Header("What to close?")]
    [Tooltip("The GameObject to deactivate (usually this panel itself)")]
    public GameObject panelToClose;

    [Header("Optional: Call a method instead")]
    [Tooltip("If your panel has a Close method, specify the component")]
    public MonoBehaviour componentWithCloseMethod;

    [Tooltip("Name of the close method (e.g., 'CloseDiscovery', 'CloseInfo')")]
    public string closeMethodName = "";

    [Header("Debug")]
    public bool showDebugMessages = true;

    // Input actions
    private InputAction primaryButtonAction;
    private InputAction secondaryButtonAction;

    private void OnEnable()
    {
        // Auto-assign panel if not set
        if (panelToClose == null)
        {
            panelToClose = gameObject;
        }

        // Setup input actions for VR controllers
        SetupInputActions();
    }

    private void OnDisable()
    {
        // Clean up input actions
        if (primaryButtonAction != null)
        {
            primaryButtonAction.performed -= OnButtonPressed;
            primaryButtonAction.Disable();
        }

        if (secondaryButtonAction != null)
        {
            secondaryButtonAction.performed -= OnButtonPressed;
            secondaryButtonAction.Disable();
        }
    }

    private void SetupInputActions()
    {
        // Create input actions for A/X buttons (primary button on both controllers)
        primaryButtonAction = new InputAction(
            name: "PrimaryButton",
            binding: "<XRController>{RightHand}/primaryButton"
        );
        primaryButtonAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        primaryButtonAction.performed += OnButtonPressed;
        primaryButtonAction.Enable();

        // Create input actions for B/Y buttons (secondary button on both controllers)
        secondaryButtonAction = new InputAction(
            name: "SecondaryButton",
            binding: "<XRController>{RightHand}/secondaryButton"
        );
        secondaryButtonAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
        secondaryButtonAction.performed += OnButtonPressed;
        secondaryButtonAction.Enable();

        if (showDebugMessages)
        {
            Debug.Log("[SimpleButtonCloseVR] Input actions enabled for: " + gameObject.name);
        }
    }

    private void Update()
    {
        // Keyboard input for testing in editor
        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.E].wasPressedThisFrame ||
                Keyboard.current[Key.Escape].wasPressedThisFrame ||
                Keyboard.current[Key.Space].wasPressedThisFrame)
            {
                if (showDebugMessages)
                {
                    Debug.Log("[SimpleButtonCloseVR] Keyboard press detected!");
                }
                ClosePanel();
            }
        }
    }

    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (showDebugMessages)
        {
            Debug.Log("[SimpleButtonCloseVR] VR Button pressed! Closing panel...");
        }
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (showDebugMessages)
        {
            Debug.Log("[SimpleButtonCloseVR] ClosePanel called for: " + gameObject.name);
        }

        // Option 1: Call a method if specified
        if (componentWithCloseMethod != null && !string.IsNullOrEmpty(closeMethodName))
        {
            if (showDebugMessages)
            {
                Debug.Log("[SimpleButtonCloseVR] Calling method: " + closeMethodName);
            }
            componentWithCloseMethod.SendMessage(closeMethodName, SendMessageOptions.DontRequireReceiver);
        }
        // Option 2: Just deactivate the GameObject
        else if (panelToClose != null)
        {
            if (showDebugMessages)
            {
                Debug.Log("[SimpleButtonCloseVR] Deactivating: " + panelToClose.name);
            }
            panelToClose.SetActive(false);
        }

        // Also unpause game if needed
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            if (showDebugMessages)
            {
                Debug.Log("[SimpleButtonCloseVR] Unpaused game");
            }
        }
    }
}