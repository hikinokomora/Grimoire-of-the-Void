using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class BasicMovement : MonoBehaviour
{
    public bool canMove = true;
    [Header("References")]
    [SerializeField] private Transform cameraRoot;

    [Header("Movement")]
    [SerializeField] public float walkSpeed = 4f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float gravity = -25f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gamepadSensitivity = 130f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Take / Gravity Gun")]
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private LayerMask takeMask = ~0;
    [SerializeField] private float holdDistance = 2.2f;
    [SerializeField] private float holdMoveForce = 12f;
    [SerializeField] private float holdMaxAcceleration = 80f;
    [SerializeField] private float holdBreakDistance = 7f;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactMask = ~0;

    public Vector2 MoveInput => moveInput;
    public Transform CameraPivot => cameraRoot;
    public bool InStationView { get; private set; }

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;
    private float currentHorizontalSpeed;
    private float pitch;
    private BasicInput input;
    private bool lastInputWasGamepad;
    private Vector3 cameraInitialLocalPosition;
    private Rigidbody heldBody;
    private bool heldBodyInitialUseGravity;
    private float heldBodyInitialDrag;
    private float heldBodyInitialAngularDrag;


    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null && Camera.main != null)
        {
            cameraRoot = Camera.main.transform;
        }

        if (cameraRoot != null)
        {
            cameraInitialLocalPosition = cameraRoot.localPosition;
        }

        input = new BasicInput();
        RegisterInputCallbacks();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        input.Player.Enable();
    }

    private void OnDisable()
    {
        input.Player.Disable();
    }

    private void OnDestroy()
    {
        UnregisterInputCallbacks();

        input?.Dispose();
    }

    private void Update()
    {
        if (InStationView)
        {
            return;
        }
        RotateCamera();
        if (canMove) { HandleMovement(); }
    }

    private void FixedUpdate()
    {
        if (InStationView)
        {
            return;
        }
        HandleHeldObject();
    }

    public void EnterStationView()
    {
        InStationView = true;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
    }

    public void ExitStationView()
    {
        InStationView = false;
    }

    public void ForceDropHeld()
    {
        ReleaseHeldObject();
    }

    private void RegisterInputCallbacks()
    {
        input.Player.Move.performed += OnMovePerformed;
        input.Player.Move.canceled += OnMoveCanceled;

        input.Player.Look.performed += OnLookPerformed;
        input.Player.Look.canceled += OnLookCanceled;


        input.Player.Interact.performed += OnInteractPerformed;
        input.Player.Take.performed += OnTakePerformed;
    }

    private void UnregisterInputCallbacks()
    {
        if (input == null)
        {
            return;
        }

        input.Player.Move.performed -= OnMovePerformed;
        input.Player.Move.canceled -= OnMoveCanceled;

        input.Player.Look.performed -= OnLookPerformed;
        input.Player.Look.canceled -= OnLookCanceled;

        input.Player.Interact.performed -= OnInteractPerformed;
        input.Player.Take.performed -= OnTakePerformed;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        moveInput = ctx.ReadValue<Vector2>();
    }
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    private void OnLookPerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        lookInput = ctx.ReadValue<Vector2>();
        lastInputWasGamepad = ctx.control.device is Gamepad;
    }
    private void OnLookCanceled(InputAction.CallbackContext ctx) => lookInput = Vector2.zero;

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        Interact();
    }
    private void OnTakePerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        Take();
    }

    private void RotateCamera()
    {
        if (cameraRoot == null)
        {
            return;
        }

        float sensitivity = lastInputWasGamepad 
            ? gamepadSensitivity * Time.deltaTime 
            : mouseSensitivity;

        float yaw = lookInput.x * sensitivity;
        float pitchDelta = lookInput.y * sensitivity;

        transform.Rotate(Vector3.up * yaw);
        pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);
        
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;



        float moveMagnitude = Mathf.Clamp01(moveInput.magnitude);
        float desiredHorizontalSpeed = walkSpeed * moveMagnitude;
        currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, desiredHorizontalSpeed, acceleration * Time.deltaTime);

        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 horizontalVelocity = moveDirection.normalized * currentHorizontalSpeed;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }


    private void Interact()
    {
        if (InStationView)
        {
            return;
        }
        Ray ray = new Ray(cameraRoot.position, cameraRoot.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactMask, QueryTriggerInteraction.Collide))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = hit.collider.GetComponentInParent<IInteractable>();
            }

            interactable?.Interact();
        }
    }


    private void Take()
    {
        if (InStationView)
        {
            return;
        }
        if (heldBody != null)
        {
            ReleaseHeldObject();
            return;
        }
        Ray ray = new Ray(cameraRoot.position, cameraRoot.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, takeMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Rigidbody body = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
        {
            return;
        }

        heldBody = body;
        heldBodyInitialUseGravity = heldBody.useGravity;
        heldBodyInitialDrag = heldBody.linearDamping;
        heldBodyInitialAngularDrag = heldBody.angularDamping;

        heldBody.useGravity = false;
        heldBody.linearDamping = 4f;
        heldBody.angularDamping = 4f;
    }

    private void HandleHeldObject()
    {
        if (heldBody == null || cameraRoot == null)
        {
            return;
        }

        if (!heldBody.gameObject.activeInHierarchy)
        {
            ReleaseHeldObject();
            return;
        }

        Vector3 holdPoint = cameraRoot.position + cameraRoot.forward * holdDistance;
        Vector3 toHoldPoint = holdPoint - heldBody.worldCenterOfMass;

        if (toHoldPoint.sqrMagnitude > holdBreakDistance * holdBreakDistance)
        {
            ReleaseHeldObject();
            return;
        }

        Vector3 desiredVelocity = toHoldPoint * holdMoveForce;
        Vector3 velocityDelta = desiredVelocity - heldBody.linearVelocity;
        float fixedDelta = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        Vector3 acceleration = velocityDelta / fixedDelta;
        acceleration = Vector3.ClampMagnitude(acceleration, holdMaxAcceleration);

        heldBody.AddForce(acceleration, ForceMode.Acceleration);
    }

    private void ReleaseHeldObject()
    {
        if (heldBody == null)
        {
            return;
        }

        heldBody.useGravity = heldBodyInitialUseGravity;
        heldBody.linearDamping = heldBodyInitialDrag;
        heldBody.angularDamping = heldBodyInitialAngularDrag;
        heldBody = null;
    }
}
