using UnityEngine;

public class LockCharacterController : MonoBehaviour
{
    [Header("Fixed Values (won't change at runtime)")]
    public float fixedHeight = 0.8f;
    public float fixedCenterY = 0.5f;
    public float fixedRadius = 0.1f;

    private CharacterController cc;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (cc != null)
        {
            cc.height = fixedHeight;
            cc.center = new Vector3(0, fixedCenterY, 0);
            cc.radius = fixedRadius;
        }
    }
}