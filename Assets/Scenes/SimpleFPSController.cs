using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Mouse")]
    public float mouseSensitivity = 150f;
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
    public float crouchHeight = 1.2f;
    public float proneHeight = 0.6f;

    public float cameraStandY = 1.6f;
    public float cameraCrouchY = 1.0f;
    public float cameraProneY = 0.5f;

    [Header("Smoothness")]
    public float heightSmooth = 6f;

    CharacterController controller;
    Vector3 velocity;
    float xRotation;
    bool isGrounded;

    enum MoveState { Stand, Crouch, Prone }
    MoveState currentState = MoveState.Stand;
    MoveState previousState = MoveState.Stand; // стан до лежання

    bool crouchToggle = false;
    bool proneToggle = false;

    bool justGotUp = false; // прапорець, щоб пропустити стрибок/toggle після підйому

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        GroundCheck();
        HandleStateInput();
        Look();
        Move();
    }

    void HandleStateInput()
    {
        // TOGGLE присідання (Ctrl)
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (currentState == MoveState.Prone)
            {
                // лежачи → присідання
                proneToggle = false;
                crouchToggle = true;
                currentState = MoveState.Crouch;
            }
            else if (!justGotUp)
            {
                crouchToggle = !crouchToggle;
                currentState = crouchToggle ? MoveState.Crouch : MoveState.Stand;
            }
        }

        // TOGGLE лягти (Z)
        if (Input.GetKeyDown(KeyCode.Z) && !justGotUp)
        {
            if (!proneToggle)
            {
                // запам'ятати стан до лягання
                previousState = currentState;
                proneToggle = true;
                crouchToggle = false;
                currentState = MoveState.Prone;
            }
            else
            {
                // якщо натиснув Z коли лежиш → піднятись у стоя
                proneToggle = false;
                currentState = MoveState.Stand;
                justGotUp = true;
            }
        }

        // Space → підняття зі стану лежачи або присідання без стрибка
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState == MoveState.Prone)
            {
                proneToggle = false;
                currentState = MoveState.Stand;
                justGotUp = true;
            }
            else if (currentState == MoveState.Crouch)
            {
                crouchToggle = false;
                currentState = MoveState.Stand;
                justGotUp = true;
            }
        }
    }


    void GroundCheck()
    {
        isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            controller.height / 2f + 0.2f
        );
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        float speed = walkSpeed;

        bool movingForward = Input.GetKey(KeyCode.W);
        bool pressingShift = Input.GetKey(KeyCode.LeftShift);

        // Біг тільки вперед, стоячи
        if (currentState == MoveState.Stand && movingForward && pressingShift)
            speed = runSpeed;
        else if (currentState == MoveState.Crouch)
            speed = crouchSpeed;
        else if (currentState == MoveState.Prone)
            speed = proneSpeed;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // Гравітація
        if (isGrounded && velocity.y < 0)
            velocity.y = -3f;

        // Стрибок – пропускаємо один кадр після підйому з лежання
        if (isGrounded && Input.GetButtonDown("Jump") && currentState == MoveState.Stand && !justGotUp)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        if (justGotUp)
            justGotUp = false; // один кадр після підйому пропускаємо стрибок/тригери

        if (velocity.y < 0)
            velocity.y += gravity * fallMultiplier * Time.deltaTime;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        UpdateHeight();
    }
    void UpdateHeight()
    {
        float targetHeight = standHeight;
        float targetCamY = cameraStandY;
        float smoothFactor = heightSmooth;

        // визначаємо цільову висоту та камеру
        if (currentState == MoveState.Crouch)
        {
            targetHeight = crouchHeight;
            targetCamY = cameraCrouchY;
            smoothFactor = 8f; // середній перехід
        }
        else if (currentState == MoveState.Prone)
        {
            targetHeight = proneHeight;
            targetCamY = cameraProneY;
            smoothFactor = 4f; // довгий перехід лежання
        }
        else if (previousState == MoveState.Prone && currentState == MoveState.Stand)
        {
            smoothFactor = 6f; // стоя після лежання
        }

        // плавний перехід висоти
        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            Time.deltaTime * smoothFactor
        );

        Vector3 center = controller.center;
        center.y = Mathf.Lerp(controller.center.y, targetHeight / 2f, Time.deltaTime * smoothFactor);
        controller.center = center;

        // плавний перехід камери
        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * smoothFactor);
        cameraTransform.localPosition = camPos;
    }

}
