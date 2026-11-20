using UnityEngine;

/// <summary>
/// Contrôleur FPS complet avec head bob, sprint, lampe torche.
/// Remplace CharacterBody3D Godot par CharacterController Unity.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ── Vitesses ──
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5.0f;
    [SerializeField] private float sprintSpeed = 8.0f;
    [SerializeField] private float backwardSpeedMultiplier = 0.6f;
    [SerializeField] private float strafeSpeedMultiplier = 0.8f;

    // ── Accélération / Décélération ──
    [Header("Acceleration")]
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 8f;

    // ── Saut & Gravité ──
    [Header("Jump & Gravity")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float fallGravityMultiplier = 1.5f;

    // ── Caméra ──
    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float normalFov = 70f;
    [SerializeField] private float sprintFov = 80f;
    [SerializeField] private float fovLerpSpeed = 6f;

    // ── Head Bob ──
    [Header("Head Bob")]
    [SerializeField] private float headBobFrequency = 2.0f;
    [SerializeField] private float headBobAmplitude = 0.04f;

    // ── Lampe Torche ──
    [Header("Flashlight")]
    [SerializeField] private bool hasFlashlight = true;
    [SerializeField] private Color flashlightColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private float flashlightIntensity = 2.5f;
    [SerializeField] private float flashlightRange = 15f;
    [SerializeField] private float flashlightAngle = 35f;

    // ── Accroupissement ──
    [Header("Crouch")]
    [Tooltip("Maintenir Ctrl gauche ou C pour s'accroupir (tunnels, trous, bouches d'aération).")]
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchSpeedMultiplier = 0.45f;
    [SerializeField] private float crouchTransitionSpeed = 6f;

    private bool _isCrouching;
    private float _standHeight;
    private float _standCenterY;
    private float _standCameraY;

    public bool IsCrouching => _isCrouching;

    private CharacterController _controller;
    private Camera _camera;
    private Light _flashlight;
    private float _rotationX = 0f;
    private float _headBobTimer = 0f;
    private float _defaultCameraY;
    private Vector3 _velocity;

    private PauseMenu _pauseMenu;

    // ── Assis (chaises, lits) ──
    public static PlayerController Instance { get; private set; }
    public bool IsSeated { get; private set; }
    private const float SeatedEyeHeight = 0.75f; // yeux au-dessus de l'assise
    private Vector3 _standPosition;
    private int _seatFrame = -1;

    private void Awake()
    {
        Instance = this;
    }

    /// S'asseoir : le joueur est posé sur le point d'assise, ne se déplace plus, peut regarder autour.
    public void SitOn(Seat seat)
    {
        if (IsSeated || seat == null) return;
        if (_isCrouching) return;
        _standPosition = transform.position;
        _controller.enabled = false;
        IsSeated = true;
        _seatFrame = Time.frameCount;
        _velocity = Vector3.zero;
        transform.position = seat.SeatPoint;
        transform.rotation = Quaternion.Euler(0f, seat.facingYaw, 0f);
        _rotationX = 10f;
        _defaultCameraY = SeatedEyeHeight;
        var cam = _camera.transform.localPosition;
        cam.y = SeatedEyeHeight;
        _camera.transform.localPosition = cam;
    }

    /// Se lever : retour à l'endroit d'où l'on s'est assis.
    public void StandUp()
    {
        if (!IsSeated || Time.frameCount == _seatFrame) return;
        IsSeated = false;
        transform.position = _standPosition;
        _controller.enabled = true;
        _defaultCameraY = _standCameraY;
        var cam = _camera.transform.localPosition;
        cam.y = _standCameraY;
        _camera.transform.localPosition = cam;
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _camera = GetComponentInChildren<Camera>();

        // Capture la souris
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _defaultCameraY = _camera.transform.localPosition.y;
        _standCameraY = _defaultCameraY;
        _standHeight = _controller.height;
        _standCenterY = _controller.center.y;
        _camera.fieldOfView = normalFov;

        SetupFlashlight();
        SetupPauseMenu();
        UpdateSettings();
    }

    private void SetupFlashlight()
    {
        if (!hasFlashlight) return;

        // Cherche une lampe existante ou en crée une
        _flashlight = _camera.GetComponentInChildren<Light>();
        if (_flashlight == null)
        {
            GameObject flashlightObj = new GameObject("Flashlight");
            flashlightObj.transform.SetParent(_camera.transform);
            flashlightObj.transform.localPosition = new Vector3(0.3f, -0.2f, 0f);
            flashlightObj.transform.localRotation = Quaternion.identity;

            _flashlight = flashlightObj.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.color = flashlightColor;
            _flashlight.intensity = flashlightIntensity;
            _flashlight.range = flashlightRange;
            _flashlight.spotAngle = flashlightAngle;
            _flashlight.shadows = LightShadows.Soft;
        }
    }

    private void SetupPauseMenu()
    {
        // Le PauseMenu sera un prefab dans la scène, pas créé dynamiquement
        _pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
    }

    public void UpdateSettings()
    {
        if (GameManager.Instance != null)
        {
            mouseSensitivity = GameManager.Instance.MouseSensitivity;
            normalFov = GameManager.Instance.NormalFov;
            sprintFov = GameManager.Instance.SprintFov;
            headBobAmplitude = GameManager.Instance.HeadBobAmplitude;
            walkSpeed = GameManager.Instance.WalkSpeed;
            sprintSpeed = GameManager.Instance.SprintSpeed;
        }
    }

    private void Update()
    {
        HandlePauseInput();

        // Ne fait rien si le jeu est en pause ou la souris libre
        if (Cursor.lockState != CursorLockMode.Locked) return;

        HandleMouseLook();
        HandleFlashlight();

        // Assis : Espace (ou un déplacement) pour se lever ; E est géré par l'interaction
        if (IsSeated && Time.frameCount != _seatFrame &&
            (KeyBindings.Down(GameAction.Jump) || KeyBindings.Down(GameAction.Forward) || KeyBindings.Down(GameAction.Backward)))
            StandUp();
    }

    private void FixedUpdate()
    {
        if (Cursor.lockState != CursorLockMode.Locked || IsSeated) return;
        HandleMovement();
    }

    private void HandlePauseInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_pauseMenu != null && !_pauseMenu.IsOpen)
            {
                _pauseMenu.OpenMenu();
            }
        }
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotation horizontale du corps
        transform.Rotate(Vector3.up * mouseX);

        // Rotation verticale de la caméra (clampée)
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -90f, 90f);
        _camera.transform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
    }

    private void HandleFlashlight()
    {
        if (_flashlight != null && KeyBindings.Down(GameAction.Flashlight))
        {
            _flashlight.enabled = !_flashlight.enabled;
            Debug.Log($"[Player] Lampe torche : {(_flashlight.enabled ? "ON" : "OFF")}");
        }
    }

    private void HandleMovement()
    {
        float dt = Time.fixedDeltaTime;

        // ── Gravité ──
        if (_controller.isGrounded)
        {
            if (_velocity.y < 0) _velocity.y = -2f; // Petit offset pour rester collé au sol
        }
        else
        {
            float gravForce = gravity;
            if (_velocity.y < 0) gravForce *= fallGravityMultiplier;
            _velocity.y -= gravForce * dt;
        }

        // ── Accroupissement ──
        UpdateCrouch(dt);

        // ── Saut ──
        if (KeyBindings.Held(GameAction.Jump) && _controller.isGrounded && !_isCrouching)
        {
            _velocity.y = jumpForce;
        }

        // ── Sprint ──
        bool isSprinting = KeyBindings.Held(GameAction.Sprint) && _controller.isGrounded && !_isCrouching;
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        if (_isCrouching) targetSpeed *= crouchSpeedMultiplier;

        // ── Direction ──
        float inputX = KeyBindings.Axis(GameAction.Left, GameAction.Right, KeyCode.LeftArrow, KeyCode.RightArrow);
        float inputZ = KeyBindings.Axis(GameAction.Backward, GameAction.Forward, KeyCode.DownArrow, KeyCode.UpArrow);
        Vector3 direction = (transform.right * inputX + transform.forward * inputZ).normalized;

        // ── Multiplicateur directionnel ──
        if (direction.sqrMagnitude > 0.01f)
        {
            float speedMultiplier = 1.0f;
            if (inputZ < -0.1f)
                speedMultiplier = backwardSpeedMultiplier;
            else if (Mathf.Abs(inputX) > 0.1f && Mathf.Abs(inputZ) < 0.1f)
                speedMultiplier = strafeSpeedMultiplier;

            float finalSpeed = targetSpeed * speedMultiplier;

            // Accélération progressive
            _velocity.x = Mathf.MoveTowards(_velocity.x, direction.x * finalSpeed, acceleration * dt);
            _velocity.z = Mathf.MoveTowards(_velocity.z, direction.z * finalSpeed, acceleration * dt);
        }
        else
        {
            // Décélération progressive
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, deceleration * dt);
            _velocity.z = Mathf.MoveTowards(_velocity.z, 0f, deceleration * dt);
        }

        _controller.Move(_velocity * dt);

        // ── Head Bobbing ──
        Vector2 horizontalVelocity = new Vector2(_velocity.x, _velocity.z);
        if (_controller.isGrounded && horizontalVelocity.magnitude > 0.5f)
        {
            float bobFreq = isSprinting ? headBobFrequency * 1.5f : headBobFrequency;
            _headBobTimer += dt * bobFreq;
            float bobOffset = Mathf.Sin(_headBobTimer * Mathf.PI * 2f) * headBobAmplitude;

            Vector3 camPos = _camera.transform.localPosition;
            camPos.y = _defaultCameraY + bobOffset;
            _camera.transform.localPosition = camPos;
        }
        else
        {
            _headBobTimer = 0f;
            Vector3 camPos = _camera.transform.localPosition;
            camPos.y = Mathf.MoveTowards(camPos.y, _defaultCameraY, headBobAmplitude * 20f * dt);
            _camera.transform.localPosition = camPos;
        }

        // ── Effet FOV dynamique ──
        float targetFovValue = isSprinting && horizontalVelocity.magnitude > 1.0f ? sprintFov : normalFov;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFovValue, fovLerpSpeed * dt);
    }

    private void UpdateCrouch(float dt)
    {
        bool wantsCrouch = KeyBindings.Held(GameAction.Crouch);
        if (wantsCrouch) _isCrouching = true;
        else if (_isCrouching && CanStandUp()) _isCrouching = false; // reste accroupi sous un plafond bas

        float targetHeight = _isCrouching ? crouchHeight : _standHeight;
        float height = Mathf.MoveTowards(_controller.height, targetHeight, crouchTransitionSpeed * dt);
        if (!Mathf.Approximately(height, _controller.height))
        {
            _controller.height = height;
            _controller.center = new Vector3(_controller.center.x, _standCenterY - (_standHeight - height) * 0.5f, _controller.center.z);
        }
        _defaultCameraY = _standCameraY - (_standHeight - _controller.height);
    }

    private bool CanStandUp()
    {
        float radius = _controller.radius * 0.95f;
        Vector3 top = transform.position + _controller.center + Vector3.up * (_controller.height * 0.5f - radius);
        float distance = _standHeight - _controller.height + 0.05f;
        return !Physics.SphereCast(top, radius, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
    }
}
