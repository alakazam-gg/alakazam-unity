using UnityEngine;

namespace NeuralAkazam.Demo
{
    /// <summary>
    /// Simple script to move a cube around for testing MirageLSD.
    /// WASD to move, QE to rotate, Space to jump.
    /// </summary>
    public class CubeMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float jumpForce = 8f;

        [Header("Auto Movement")]
        [SerializeField] private bool autoMove = true;
        [SerializeField] private float autoMoveRadius = 3f;
        [SerializeField] private float autoMoveSpeed = 1f;

        private Rigidbody _rb;
        private bool _isGrounded = true;
        private float _autoMoveAngle = 0f;
        private Vector3 _startPosition;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _startPosition = transform.position;

            if (_rb == null && !autoMove)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }
        }

        private void Update()
        {
            if (autoMove)
            {
                AutoMove();
            }
            else
            {
                ManualMove();
            }
        }

        private void AutoMove()
        {
            // Circle around the start position
            _autoMoveAngle += autoMoveSpeed * Time.deltaTime;

            float x = _startPosition.x + Mathf.Cos(_autoMoveAngle) * autoMoveRadius;
            float z = _startPosition.z + Mathf.Sin(_autoMoveAngle) * autoMoveRadius;

            transform.position = new Vector3(x, _startPosition.y, z);

            // Rotate to face movement direction
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }

        private void ManualMove()
        {
            // WASD movement
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 move = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
            transform.Translate(move, Space.World);

            // QE rotation
            if (Input.GetKey(KeyCode.Q))
                transform.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.E))
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // Space to jump
            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded && _rb != null)
            {
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                _isGrounded = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ground") || collision.contacts[0].normal.y > 0.5f)
            {
                _isGrounded = true;
            }
        }
    }
}
