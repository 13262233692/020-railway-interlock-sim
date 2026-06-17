using UnityEngine;

namespace RailwayInterlock.CameraSystem
{
    public class TopDownCameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;
        public Vector3 offset = new Vector3(0, 80f, -40f);

        [Header("Movement")]
        public float followSpeed = 5f;
        public float zoomSpeed = 100f;
        public float panSpeed = 50f;
        public float rotationSpeed = 100f;

        [Header("Bounds")]
        public float minHeight = 20f;
        public float maxHeight = 200f;
        public float minAngle = 30f;
        public float maxAngle = 80f;

        [Header("Input")]
        public bool useMouseInput = true;
        public bool useKeyboardInput = true;
        public KeyCode mousePanButton = KeyCode.Mouse2;
        public KeyCode mouseRotateButton = KeyCode.Mouse1;

        private Vector3 _currentVelocity;
        private float _currentYAngle;
        private float _currentPitchAngle = 60f;
        private float _currentHeight;

        private void Start()
        {
            if (target != null)
            {
                transform.position = target.position + offset;
            }
            transform.rotation = Quaternion.Euler(_currentPitchAngle, 0, 0);
            _currentHeight = transform.position.y;
            _currentYAngle = transform.eulerAngles.y;
        }

        private void Update()
        {
            if (useMouseInput)
            {
                HandleZoom();
                HandleMousePan();
                HandleMouseRotate();
            }

            if (useKeyboardInput)
            {
                HandleKeyboardMovement();
                HandleKeyboardRotation();
            }

            if (target != null)
            {
                FollowTarget();
            }
            else
            {
                FreeMovement();
            }
        }

        private void FollowTarget()
        {
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, 1f / followSpeed);
        }

        private void FreeMovement()
        {
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                Vector3 delta = transform.forward * scroll * zoomSpeed * Time.deltaTime * 60f;
                Vector3 newPos = transform.position + delta;
                newPos.y = Mathf.Clamp(newPos.y, minHeight, maxHeight);
                transform.position = newPos;
                _currentHeight = transform.position.y;
            }
        }

        private void HandleMousePan()
        {
            if (Input.GetKey(mousePanButton))
            {
                float h = Input.GetAxis("Mouse X");
                float v = Input.GetAxis("Mouse Y");

                Vector3 right = transform.right;
                right.y = 0;
                right.Normalize();

                Vector3 up = Vector3.Cross(right, Vector3.up);
                up.Normalize();

                Vector3 pan = (-right * h + -up * v) * panSpeed * Time.deltaTime;
                transform.position += pan;
            }
        }

        private void HandleMouseRotate()
        {
            if (Input.GetKey(mouseRotateButton))
            {
                float h = Input.GetAxis("Mouse X");
                float v = Input.GetAxis("Mouse Y");

                _currentYAngle += h * rotationSpeed * Time.deltaTime;
                _currentPitchAngle -= v * rotationSpeed * Time.deltaTime;
                _currentPitchAngle = Mathf.Clamp(_currentPitchAngle, minAngle, maxAngle);

                transform.rotation = Quaternion.Euler(_currentPitchAngle, _currentYAngle, 0);

                offset = Quaternion.Euler(0, _currentYAngle, 0) * new Vector3(0, offset.y, offset.z);
            }
        }

        private void HandleKeyboardMovement()
        {
            float h = 0;
            float v = 0;
            float upDown = 0;

            if (Input.GetKey(KeyCode.W)) v += 1;
            if (Input.GetKey(KeyCode.S)) v -= 1;
            if (Input.GetKey(KeyCode.D)) h += 1;
            if (Input.GetKey(KeyCode.A)) h -= 1;
            if (Input.GetKey(KeyCode.E)) upDown += 1;
            if (Input.GetKey(KeyCode.Q)) upDown -= 1;

            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            float speed = panSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2f : 1f);
            Vector3 move = (forward * v + right * h + Vector3.up * upDown) * speed * Time.deltaTime;
            transform.position += move;

            float clampedY = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
            transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);
        }

        private void HandleKeyboardRotation()
        {
            float rot = 0;
            if (Input.GetKey(KeyCode.LeftArrow)) rot -= 1;
            if (Input.GetKey(KeyCode.RightArrow)) rot += 1;

            if (Mathf.Abs(rot) > 0.001f)
            {
                _currentYAngle += rot * rotationSpeed * 0.5f * Time.deltaTime;
                transform.rotation = Quaternion.Euler(_currentPitchAngle, _currentYAngle, 0);
                offset = Quaternion.Euler(0, _currentYAngle, 0) * new Vector3(0, offset.y, offset.z);
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                _currentPitchAngle = Mathf.Min(maxAngle, _currentPitchAngle + rotationSpeed * 0.3f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(_currentPitchAngle, _currentYAngle, 0);
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                _currentPitchAngle = Mathf.Max(minAngle, _currentPitchAngle - rotationSpeed * 0.3f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(_currentPitchAngle, _currentYAngle, 0);
            }
        }

        public void ResetView()
        {
            offset = new Vector3(0, 80f, -40f);
            _currentPitchAngle = 60f;
            _currentYAngle = 0;
            transform.rotation = Quaternion.Euler(_currentPitchAngle, _currentYAngle, 0);
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void ClearTarget()
        {
            target = null;
        }
    }
}
