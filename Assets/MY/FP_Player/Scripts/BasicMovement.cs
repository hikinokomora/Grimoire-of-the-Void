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
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float blockSpeed = 1.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float gravity = -25f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gamepadSensitivity = 130f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchingHeight = 1.1f;
    [SerializeField] private float crouchLerpSpeed = 12f;
    [SerializeField] private float crouchCameraDrop = 0.35f;

    [Header("Take / Gravity Gun")]
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private LayerMask takeMask = ~0;
    [SerializeField] private float holdDistance = 2.2f;
    [SerializeField] private float holdMoveForce = 12f;
    [SerializeField] private float holdMaxAcceleration = 80f;
    [SerializeField] private float holdBreakDistance = 7f;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("Combat")]
    [SerializeField] private float maxHealth = 100f;

    public bool IsCrouching { get; private set; }
    public bool IsBlocking { get; private set; }
    public bool IsDead { get; private set; }
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public Vector2 MoveInput => moveInput;
    public bool IsSprinting => sprintHeld && !IsCrouching && !IsBlocking && moveInput.y > 0.1f;
    public Transform CameraPivot => cameraRoot;
    public bool InStationView { get; private set; }

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintHeld;
    private float verticalVelocity;
    private float currentHorizontalSpeed;
    private float pitch;
    private BasicInput input;
    private bool lastInputWasGamepad;
    private bool hitRequested;
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

        controller.height = standingHeight;
        controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);

        CurrentHealth = maxHealth;

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
        if (IsDead)
        {
            return;
        }
        if (InStationView)
        {
            return;
        }
        RotateCamera();
        if (canMove) { HandleMovement(); }
        
        HandleCrouchHeight();
        HandleCrouchCamera();
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
        sprintHeld = false;
        IsBlocking = false;
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

        input.Player.Sprint.performed += OnSprintPerformed;
        input.Player.Sprint.canceled += OnSprintCanceled;

        input.Player.Crouch.performed += OnCrouchPerformed;
        input.Player.Crouch.canceled += OnCrouchCanceled;

        input.Player.Block.performed += OnBlockPerformed;
        input.Player.Block.canceled += OnBlockCanceled;

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

        input.Player.Sprint.performed -= OnSprintPerformed;
        input.Player.Sprint.canceled -= OnSprintCanceled;

        input.Player.Crouch.performed -= OnCrouchPerformed;
        input.Player.Crouch.canceled -= OnCrouchCanceled;

        input.Player.Block.performed -= OnBlockPerformed;
        input.Player.Block.canceled -= OnBlockCanceled;

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
    private void OnSprintPerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        sprintHeld = true;
    }
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => sprintHeld = false;
    private void OnCrouchPerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        TryCrouch();
    }
    private void OnCrouchCanceled(InputAction.CallbackContext ctx) { }
    private void OnBlockPerformed(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        IsBlocking = true;
    }
    private void OnBlockCanceled(InputAction.CallbackContext ctx)
    {
        if (InStationView) { return; }
        IsBlocking = false;
    }
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


    public void TakeDamage(float amount, Vector3 attackerWorldPosition)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    private void Die()
    {
        IsDead = true;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        sprintHeld = false;
        IsBlocking = false;
        ReleaseHeldObject();
        input.Player.Disable();
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

        float targetSpeed = walkSpeed;
        if (IsBlocking)
        {
            targetSpeed = blockSpeed;
        }
        else if (IsCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if (sprintHeld)
        {
            targetSpeed = sprintSpeed;
        }

        float moveMagnitude = Mathf.Clamp01(moveInput.magnitude);
        float desiredHorizontalSpeed = targetSpeed * moveMagnitude;
        currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, desiredHorizontalSpeed, acceleration * Time.deltaTime);

        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 horizontalVelocity = moveDirection.normalized * currentHorizontalSpeed;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouchHeight()
    {
        float targetHeight = IsCrouching ? crouchingHeight : standingHeight;
        float currentHeight = Mathf.Lerp(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
        
        // Ensure minimum safe distance from controller radius
        float minSafeHeight = controller.radius * 2.1f;
        currentHeight = Mathf.Max(currentHeight, minSafeHeight);
        
        controller.height = currentHeight;
        controller.center = new Vector3(controller.center.x, currentHeight * 0.5f, controller.center.z);
    }

    private void HandleCrouchCamera()
    {
        if (cameraRoot == null)
        {
            return;
        }

        float targetYOffset = IsCrouching ? -Mathf.Abs(crouchCameraDrop) : 0f;
        Vector3 targetLocalPosition = cameraInitialLocalPosition + Vector3.up * targetYOffset;
        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, targetLocalPosition, crouchLerpSpeed * Time.deltaTime);
    }

    private void TryCrouch()
    {
        if (IsCrouching)
        {
            if (!CanStandUp())
            {
                return;
            }
        }
        IsCrouching = !IsCrouching;
    }

    private bool CanStandUp()
    {
        float radius = controller.radius * 0.95f;
        float targetHeight = Mathf.Max(standingHeight, radius * 2f);
        Vector3 center = transform.TransformPoint(new Vector3(controller.center.x, targetHeight * 0.5f, controller.center.z));

        float halfLine = Mathf.Max(0f, targetHeight * 0.5f - radius);
        Vector3 bottom = center - Vector3.up * halfLine;
        Vector3 top = center + Vector3.up * halfLine;

        bool controllerWasEnabled = controller.enabled;
        controller.enabled = false;

        int layerMask = ~(1 << gameObject.layer);
        bool blocked = Physics.CheckCapsule(bottom, top, radius, layerMask, QueryTriggerInteraction.Ignore);

        controller.enabled = controllerWasEnabled;
        return !blocked;
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
