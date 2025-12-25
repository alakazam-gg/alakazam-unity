using UnityEngine;

namespace AlakazamPortal.Demo
{
    /// <summary>
    /// Simple fly camera controller for exploring scenes.
    /// WASD to move, mouse to look, Shift to go faster, Space/Ctrl for up/down.
    /// </summary>
    public class FlyCam : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float fastMultiplier = 3f;
        [SerializeField] private float smoothing = 5f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 90f;

        [Header("Controls")]
        [SerializeField] private bool requireRightClick = true;

        private float _rotationX = 0f;
        private float _rotationY = 0f;
        private Vector3 _targetPosition;
        private bool _isControlling = false;

        private void Start()
        {
            _targetPosition = transform.position;
            _rotationX = transform.eulerAngles.y;
            _rotationY = transform.eulerAngles.x;

            // Normalize rotation Y to -180 to 180 range
            if (_rotationY > 180) _rotationY -= 360;
        }

        private void Update()
        {
            // Check if we should be controlling
            if (requireRightClick)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    _isControlling = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    _isControlling = false;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                _isControlling = true;
            }

            if (_isControlling)
            {
                HandleLook();
            }

            HandleMovement();

            // Smooth position
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * smoothing);
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            _rotationX += mouseX;
            _rotationY -= mouseY;
            _rotationY = Mathf.Clamp(_rotationY, -maxLookAngle, maxLookAngle);

            transform.rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
        }

        private void HandleMovement()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal"); // A/D
            float vertical = Input.GetAxis("Vertical");     // W/S
            float upDown = 0f;

            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
                upDown = 1f;
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q))
                upDown = -1f;

            // Calculate direction relative to camera
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = Vector3.up;

            Vector3 moveDirection = (forward * vertical + right * horizontal + up * upDown).normalized;

            // Apply speed
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= fastMultiplier;

            // Update target position
            _targetPosition += moveDirection * speed * Time.deltaTime;
        }
    }
}
