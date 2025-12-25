using UnityEngine;

namespace NeuralAkazam.Demo
{
    /// <summary>
    /// Procedurally creates and animates a simple humanoid mannequin.
    /// Walks back and forth with basic leg/arm swing animation.
    /// </summary>
    public class WalkingMannequin : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float walkDistance = 5f;
        [SerializeField] private float turnSpeed = 5f;

        [Header("Animation")]
        [SerializeField] private float stepFrequency = 2f;
        [SerializeField] private float legSwingAngle = 30f;
        [SerializeField] private float armSwingAngle = 45f;
        [SerializeField] private float bodyBob = 0.05f;

        [Header("Appearance")]
        [SerializeField] private Color mannequinColor = new Color(0.6f, 0.6f, 0.6f);

        // Body parts
        private Transform _body;
        private Transform _head;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private Transform _leftArm;
        private Transform _rightArm;

        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _animationTime;
        private bool _walkingForward = true;

        private void Start()
        {
            _startPosition = transform.position;
            _targetPosition = _startPosition + Vector3.forward * walkDistance;
            CreateMannequin();
        }

        private void CreateMannequin()
        {
            // Create material
            var material = new Material(Shader.Find("Standard"));
            material.color = mannequinColor;

            // Body (torso) - capsule
            var bodyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGO.name = "Body";
            bodyGO.transform.SetParent(transform);
            bodyGO.transform.localPosition = new Vector3(0, 1.1f, 0);
            bodyGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.3f);
            bodyGO.GetComponent<Renderer>().material = material;
            Destroy(bodyGO.GetComponent<Collider>());
            _body = bodyGO.transform;

            // Head - sphere
            var headGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGO.name = "Head";
            headGO.transform.SetParent(transform);
            headGO.transform.localPosition = new Vector3(0, 1.75f, 0);
            headGO.transform.localScale = new Vector3(0.3f, 0.35f, 0.3f);
            headGO.GetComponent<Renderer>().material = material;
            Destroy(headGO.GetComponent<Collider>());
            _head = headGO.transform;

            // Left Leg - capsule
            var leftLegGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftLegGO.name = "LeftLeg";
            leftLegGO.transform.SetParent(transform);
            leftLegGO.transform.localPosition = new Vector3(-0.15f, 0.4f, 0);
            leftLegGO.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            leftLegGO.GetComponent<Renderer>().material = material;
            Destroy(leftLegGO.GetComponent<Collider>());
            _leftLeg = leftLegGO.transform;

            // Right Leg - capsule
            var rightLegGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightLegGO.name = "RightLeg";
            rightLegGO.transform.SetParent(transform);
            rightLegGO.transform.localPosition = new Vector3(0.15f, 0.4f, 0);
            rightLegGO.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            rightLegGO.GetComponent<Renderer>().material = material;
            Destroy(rightLegGO.GetComponent<Collider>());
            _rightLeg = rightLegGO.transform;

            // Left Arm - capsule
            var leftArmGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArmGO.name = "LeftArm";
            leftArmGO.transform.SetParent(transform);
            leftArmGO.transform.localPosition = new Vector3(-0.35f, 1.2f, 0);
            leftArmGO.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
            leftArmGO.GetComponent<Renderer>().material = material;
            Destroy(leftArmGO.GetComponent<Collider>());
            _leftArm = leftArmGO.transform;

            // Right Arm - capsule
            var rightArmGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArmGO.name = "RightArm";
            rightArmGO.transform.SetParent(transform);
            rightArmGO.transform.localPosition = new Vector3(0.35f, 1.2f, 0);
            rightArmGO.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
            rightArmGO.GetComponent<Renderer>().material = material;
            Destroy(rightArmGO.GetComponent<Collider>());
            _rightArm = rightArmGO.transform;
        }

        private void Update()
        {
            // Move towards target
            Vector3 direction = (_targetPosition - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, _targetPosition);

            if (distance > 0.1f)
            {
                // Move
                transform.position += direction * walkSpeed * Time.deltaTime;

                // Rotate to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

                // Animate
                _animationTime += Time.deltaTime * stepFrequency;
                AnimateWalk();
            }
            else
            {
                // Reached target, turn around
                _walkingForward = !_walkingForward;
                _targetPosition = _walkingForward ? _startPosition + Vector3.forward * walkDistance : _startPosition;
            }
        }

        private void AnimateWalk()
        {
            float cycle = Mathf.Sin(_animationTime * Mathf.PI * 2);

            // Leg swing (opposite to each other)
            float legAngle = cycle * legSwingAngle;
            _leftLeg.localRotation = Quaternion.Euler(legAngle, 0, 0);
            _rightLeg.localRotation = Quaternion.Euler(-legAngle, 0, 0);

            // Arm swing (opposite to legs for natural walk)
            float armAngle = cycle * armSwingAngle;
            _leftArm.localRotation = Quaternion.Euler(-armAngle, 0, 0);
            _rightArm.localRotation = Quaternion.Euler(armAngle, 0, 0);

            // Body bob (up/down with each step)
            float bob = Mathf.Abs(Mathf.Sin(_animationTime * Mathf.PI * 4)) * bodyBob;
            _body.localPosition = new Vector3(0, 1.1f + bob, 0);
            _head.localPosition = new Vector3(0, 1.75f + bob, 0);

            // Slight body sway
            float sway = Mathf.Sin(_animationTime * Mathf.PI * 2) * 2f;
            _body.localRotation = Quaternion.Euler(0, 0, sway);
        }
    }
}
