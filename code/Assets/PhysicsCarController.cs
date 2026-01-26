using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsCarController : MonoBehaviour
{
    [Header("Input (drag your action here)")]
    [SerializeField] private InputActionReference moveAxis;

    [Header("Mode Toggle (in-game)")]
    [Tooltip("Assign a Button action to toggle between Normal and One-Button driving.")]
    [SerializeField] private InputActionReference toggleDriveMode;

    [SerializeField] private bool oneButtonDriving = false;

    [Header("One Button Driving (hold to rotate + drive)")]
    [Tooltip("Assign a Button action. Hold to rotate 90° once, then drive forward while held.")]
    [SerializeField] private InputActionReference oneButtonDrive;

    [Header("Rigidbody (optional, auto-fills if empty)")]
    [SerializeField] private Rigidbody rb;

    [Header("Movement")]
    [SerializeField] private float acceleration = 35f;   // m/s^2
    [SerializeField] private float maxSpeed = 20f;       // m/s

    [Header("Snap Steering")]
    [Tooltip("Degrees per snap (90 for grid turns).")]
    [SerializeField] private float snapAngle = 90f;

    [Tooltip("How long between snap turns while holding left/right.")]
    [SerializeField] private float snapCooldown = 0.20f;

    [Tooltip("Stick threshold to count as left/right.")]
    [SerializeField] private float steerThreshold = 0.5f;

    [Header("State (read-only)")]
    [Tooltip("True when the player is actively driving the car")]
    public bool isDriving;

    private float steer;
    private float throttle;

    private float nextSnapTime;
    private int lastSteerDir;

    // One-button state
    private bool oneBtnHeld;
    private bool rotatedThisHold;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (moveAxis != null) moveAxis.action.Enable();

        if (oneButtonDrive != null)
        {
            oneButtonDrive.action.Enable();
            oneButtonDrive.action.performed += OnOneButtonPerformed;
            oneButtonDrive.action.canceled += OnOneButtonCanceled;
        }

        if (toggleDriveMode != null)
        {
            toggleDriveMode.action.Enable();
            toggleDriveMode.action.performed += OnToggleDriveMode;
        }
    }

    private void OnDisable()
    {
        if (moveAxis != null) moveAxis.action.Disable();

        if (oneButtonDrive != null)
        {
            oneButtonDrive.action.performed -= OnOneButtonPerformed;
            oneButtonDrive.action.canceled -= OnOneButtonCanceled;
            oneButtonDrive.action.Disable();
        }

        if (toggleDriveMode != null)
        {
            toggleDriveMode.action.performed -= OnToggleDriveMode;
            toggleDriveMode.action.Disable();
        }
    }

    private void OnToggleDriveMode(InputAction.CallbackContext ctx)
    {
        oneButtonDriving = !oneButtonDriving;

        // Reset mode-specific state so it doesn't "carry over"
        steer = 0f;
        throttle = 0f;

        lastSteerDir = 0;
        nextSnapTime = 0f;

        oneBtnHeld = false;
        rotatedThisHold = false;
    }

    private void OnOneButtonPerformed(InputAction.CallbackContext ctx)
    {
        oneBtnHeld = true;
    }

    private void OnOneButtonCanceled(InputAction.CallbackContext ctx)
    {
        oneBtnHeld = false;
        rotatedThisHold = false;
    }

    private void Update()
    {
        if (oneButtonDriving)
        {
            steer = 0f;
            throttle = oneBtnHeld ? 1f : 0f;
            return;
        }

        if (moveAxis == null) return;

        Vector2 input = moveAxis.action.ReadValue<Vector2>();
        steer = input.x;
        throttle = input.y;
    }

    private void FixedUpdate()
    {
        if (!rb) return;

        if (oneButtonDriving)
        {
            if (oneBtnHeld && !rotatedThisHold)
            {
                Quaternion deltaRot = Quaternion.Euler(0f, snapAngle, 0f);
                rb.MoveRotation(rb.rotation * deltaRot);
                rotatedThisHold = true;
            }
        }
        else
        {
            int steerDir = 0;
            if (steer >= steerThreshold) steerDir = 1;
            else if (steer <= -steerThreshold) steerDir = -1;

            bool pressedThisFrame = steerDir != 0 && lastSteerDir == 0;
            bool canRepeatHold = steerDir != 0 && Time.time >= nextSnapTime;

            if (pressedThisFrame || canRepeatHold)
            {
                float yaw = steerDir * snapAngle;
                Quaternion deltaRot = Quaternion.Euler(0f, yaw, 0f);
                rb.MoveRotation(rb.rotation * deltaRot);

                nextSnapTime = Time.time + snapCooldown;
            }

            lastSteerDir = steerDir;
        }

        Vector3 forwardVel = Vector3.Project(rb.linearVelocity, transform.forward);

        bool underCap = forwardVel.magnitude < maxSpeed;
        bool reversingAgainstVel =
            Mathf.Sign(throttle) != Mathf.Sign(Vector3.Dot(forwardVel, transform.forward));

        if (underCap || reversingAgainstVel)
        {
            Vector3 accel = transform.forward * (throttle * acceleration);
            rb.AddForce(accel, ForceMode.Acceleration);
        }

        bool hasThrottleInput = Mathf.Abs(throttle) > 0.05f;
        bool isMoving = rb.linearVelocity.magnitude > 0.3f;

        isDriving = hasThrottleInput && isMoving;
    }
}
