using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private float sensitivity = 300f;
    [SerializeField]
    private Transform playerBody;
    [SerializeField]
    private PlayerInput playerInput;
    
    float verticalRotation = 0f;
    
    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float mouseX = playerInput.MousePosition.x * sensitivity * Time.deltaTime;
        float mouseY = playerInput.MousePosition.y * sensitivity * Time.deltaTime;
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        
        transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
