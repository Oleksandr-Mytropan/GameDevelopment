using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    float currentSpeed;

    [Header("Camera Bob")]
    Vector3 cameraDefaultLocalPos;
    float bobTimer;
    float bobOffsetY;

    public float bobSpeedWalk = 10f;
    public float bobSpeedRun = 13f;
    public float bobSpeedCrouch = 8f;
    public float bobSpeedProne = 5f;

    public float bobAmountWalk = 0.1f;
    public float bobAmountRun = 0.12f;
    public float bobAmountCrouch = 0.08f;
    public float bobAmountProne = 0.06f;

    [Header("Mouse")]
    public float baseSensitivity = 3.0f;
    public float dpi = 800f;
    public Transform cameraTransform;

    [Header("Movement Speeds")]
    public float walkSpeed = 6.5f;
    public float runSpeed = 10.8f;
    public float crouchSpeed = 4f;
    public float proneSpeed = 1.5f;

    [Header("Jump & Gravity")]
    public float gravity = -28f;
    public float jumpForce = 2.2f;
    public float fallMultiplier = 2.5f;

    [Header("Heights")]
    public float standHeight = 1.9f;
    public float crouchHeight = 1.5f;
    public float proneHeight = 0.6f;

    [Header("Camera height in different modes")]
    public float cameraStandY = 2.1f;
    public float cameraCrouchY = 1.4f;
    public float cameraProneY = 0.55f;

    CharacterController controller;
    Vector3 velocity;

    float xRotation;
    float yRotation;
    bool isGrounded;

    float currentCamY;

    enum MoveState { Stand, Crouch, Prone }
    MoveState currentState = MoveState.Stand;
    MoveState desiredState = MoveState.Stand;

    bool crouchToggle;
    bool proneToggle;
    bool justGotUp;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentCamY = cameraStandY;
        cameraDefaultLocalPos = cameraTransform.localPosition;
        currentCamY = cameraDefaultLocalPos.y;
    }

    void Update()
    {
        GroundCheck();
        HandleStateInput();
        Look();
        Move();
        UpdateHeightAndCamera();
    }

    // ================= INPUT =================

    void HandleStateInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (currentState == MoveState.Prone)
            {
                proneToggle = false;
                crouchToggle = true;
                desiredState = MoveState.Crouch;
            }
            else
            {
                crouchToggle = !crouchToggle;
                desiredState = crouchToggle ? MoveState.Crouch : MoveState.Stand;
            }
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (!proneToggle)
            {
                proneToggle = true;
                crouchToggle = false;
                desiredState = MoveState.Prone;
            }
            else
            {
                proneToggle = false;
                desiredState = MoveState.Stand;
                justGotUp = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState != MoveState.Stand)
            {
                crouchToggle = false;
                proneToggle = false;
                desiredState = MoveState.Stand;
                justGotUp = true;
            }
        }
    }

    // ================= MOVEMENT =================

    void Move()
    {
        bool isTransitioning = Mathf.Abs(controller.height - GetTargetHeight()) > 0.05f;

        // Базова цільова швидкість
        float targetSpeed = walkSpeed;

        if (!isTransitioning && currentState == MoveState.Stand)
        {
            if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.LeftShift))
                targetSpeed = runSpeed;
            else
                targetSpeed = walkSpeed;
        }
        else if (currentState == MoveState.Crouch)
            targetSpeed = crouchSpeed;
        else if (currentState == MoveState.Prone)
            targetSpeed = proneSpeed;

        // Плавний перехід швидкості
        float acceleration = 6f;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        // === Нормалізація для діагонального руху ===
        if (move.magnitude > 1f)
            move.Normalize();

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (isGrounded && Input.GetButtonDown("Jump") && currentState == MoveState.Stand && !justGotUp)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        justGotUp = false;

        velocity.y += (velocity.y < 0 ? gravity * fallMultiplier : gravity) * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ================= HEIGHT + CAMERA =================

    void UpdateHeightAndCamera()
    {
        float targetHeight = GetTargetHeight();
        float targetCamY = GetTargetCamY();

        float distance = Mathf.Abs(controller.height - targetHeight);
        float smooth = Mathf.Lerp(12f, 4f, distance);

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * smooth);

        Vector3 center = controller.center;
        center.y = Mathf.Lerp(center.y, targetHeight / 2f, Time.deltaTime * smooth);
        controller.center = center;

        currentCamY = Mathf.Lerp(currentCamY, targetCamY, Time.deltaTime * smooth);

        // ===== HEAD BOB =====
        float inputMagnitude = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).magnitude;

        bool isMoving = inputMagnitude > 0.1f && isGrounded;

        if (isMoving)
        {
            float bobSpeed = bobSpeedWalk;
            float bobAmount = bobAmountWalk;

            if (currentState == MoveState.Stand && Input.GetKey(KeyCode.LeftShift))
            {
                bobSpeed = bobSpeedRun;
                bobAmount = bobAmountRun;
            }
            else if (currentState == MoveState.Crouch)
            {
                bobSpeed = bobSpeedCrouch;
                bobAmount = bobAmountCrouch;
            }
            else if (currentState == MoveState.Prone)
            {
                bobSpeed = bobSpeedProne;
                bobAmount = bobAmountProne;
            }

            bobTimer += Time.deltaTime * bobSpeed;
            bobOffsetY = Mathf.Sin(bobTimer) * bobAmount;
        }
        else
        {
            bobTimer = 0f;
            bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, Time.deltaTime * 10f);
        }
        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = currentCamY + bobOffsetY;
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            camPos,
            Time.deltaTime * 12f
        );

        if (Mathf.Abs(controller.height - GetTargetHeight()) < 0.05f)
        {
            currentState = desiredState;
        }

    }

    float GetTargetHeight()
    {
        switch (desiredState)
        {
            case MoveState.Crouch: return crouchHeight;
            case MoveState.Prone: return proneHeight;
            default: return standHeight;
        }
    }

    float GetTargetCamY()
    {
        switch (desiredState)
        {
            case MoveState.Crouch: return cameraCrouchY;
            case MoveState.Prone: return cameraProneY;
            default: return cameraStandY;
        }
    }

    // ================= CAMERA LOOK =================

    void Look()
    {
        float sens = baseSensitivity * (dpi / 800f);

        float mouseX = Input.GetAxisRaw("Mouse X") * sens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sens;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        yRotation += mouseX;

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    void GroundCheck()
    {
        isGrounded = controller.isGrounded;
    }
}