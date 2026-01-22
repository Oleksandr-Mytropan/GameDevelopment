using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Mouse")]
    public float baseSensitivity = 1.0f; // 0.6 – 1.2 комфортно при 800 DPI
    public float dpi = 800f;
    public Transform cameraTransform;

    [Header("Movement Speeds")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float crouchSpeed = 2f;
    public float proneSpeed = 1.5f;

    [Header("Jump & Gravity")]
    public float gravity = -45f;
    public float jumpForce = 3.0f;
    public float fallMultiplier = 3.5f;

    [Header("Heights")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.5f;
    public float proneHeight = 0.6f;

    public float cameraStandY = 1.6f;
    public float cameraCrouchY = 1.3f;
    public float cameraProneY = 0.5f;

    CharacterController controller;
    Vector3 velocity;

    float xRotation;
    float yRotation;
    bool isGrounded;

    float currentCamY; // ✅ стабілізація камери

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
    }

    void Update()
    {
        GroundCheck();
        HandleStateInput();
        Look();          // ✅ ТУТ, не LateUpdate
        Move();
        UpdateHeight();
    }

    // ================= INPUT =================

    void HandleStateInput()
    {
        // Ctrl → crouch
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (currentState == MoveState.Prone)
            {
                proneToggle = false;
                crouchToggle = true;
                currentState = desiredState = MoveState.Crouch;
            }
            else
            {
                crouchToggle = !crouchToggle;
                currentState = desiredState = crouchToggle ? MoveState.Crouch : MoveState.Stand;
            }
        }

        // Z → prone
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (!proneToggle)
            {
                proneToggle = true;
                crouchToggle = false;
                currentState = desiredState = MoveState.Prone;
            }
            else
            {
                proneToggle = false;
                currentState = desiredState = MoveState.Stand;
                justGotUp = true;
            }
        }

        // Space → stand (без стрибка)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState == MoveState.Crouch || currentState == MoveState.Prone)
            {
                crouchToggle = false;
                proneToggle = false;
                currentState = desiredState = MoveState.Stand;
                justGotUp = true;
            }
        }
    }

    // ================= MOVEMENT =================

    void Move()
    {
        bool isTransitioning = Mathf.Abs(controller.height - GetTargetHeight()) > 0.05f;

        float speed = walkSpeed;

        if (!isTransitioning &&
            currentState == MoveState.Stand &&
            Input.GetKey(KeyCode.W) &&
            Input.GetKey(KeyCode.LeftShift))
        {
            speed = runSpeed;
        }
        else if (currentState == MoveState.Crouch)
            speed = crouchSpeed;
        else if (currentState == MoveState.Prone)
            speed = proneSpeed;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        if (isGrounded)
            velocity.y = -2f;

        if (isGrounded && Input.GetButtonDown("Jump") &&
            currentState == MoveState.Stand && !justGotUp)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        if (justGotUp)
            justGotUp = false;

        velocity.y += (velocity.y < 0 ? gravity * fallMultiplier : gravity) * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ================= HEIGHT & CAMERA =================

    void UpdateHeight()
    {
        float targetHeight = GetTargetHeight();
        float targetCamY = GetTargetCamY();

        float distance = Mathf.Abs(controller.height - targetHeight);
        float smooth = Mathf.Lerp(12f, 3f, distance);

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * smooth);

        Vector3 center = controller.center;
        center.y = Mathf.Lerp(center.y, targetHeight / 2f, Time.deltaTime * smooth);
        controller.center = center;

        // ✅ стабілізований Y камери
        currentCamY = Mathf.Lerp(currentCamY, targetCamY, Time.deltaTime * smooth);

        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = currentCamY;
        cameraTransform.localPosition = camPos;
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

    // ================= CAMERA =================

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

    // ================= UTILS =================

    void GroundCheck()
    {
        isGrounded = controller.isGrounded;
    }
}
