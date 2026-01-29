using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FullBodyFpsController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform meshRoot;
    public Animator animator;

    [Header("Mouse")]
    public float baseSensitivity = 3.0f;
    public float dpi = 800f;
    public float clampAngle = 80f;

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
    public float standHeight = 2.3f;
    public float crouchHeight = 1.65f;
    public float proneHeight = 1f;

    [Header("Character Controller Centers")]
    public Vector3 controllerCenterStanding = new Vector3(0, 1.15f, 0);
    public Vector3 controllerCenterCrouch = new Vector3(0, 0.825f, 0);
    public Vector3 controllerCenterProne = new Vector3(0, 0.5f, 0);

    [Header("Camera height in different modes")]
    public float cameraStandY = 2.1f;
    public float cameraCrouchY = 1.45f;
    public float cameraProneY = 0.8f;

    [Header("Camera Bob")]
    public float bobSpeedWalk = 10f;
    public float bobSpeedRun = 13f;
    public float bobSpeedCrouch = 8f;
    public float bobSpeedProne = 5f;
    public float bobAmountWalk = 0.1f;
    public float bobAmountRun = 0.12f;
    public float bobAmountCrouch = 0.08f;
    public float bobAmountProne = 0.06f;

    [Header("Foot IK")]
    public bool enableFootIK = true;
    public float footIkRayDistance = 1.2f;
    public LayerMask groundMask = ~0;
    public float footIkWeight = 1.0f;
    public float footIkSmoothTime = 0.08f;

    [Header("Bones")]
    public Transform headBone;
    public Transform spineBone;
    public float spineRotationWeight = 0.4f;

    // Internal variables
    CharacterController controller;
    Vector3 velocity;
    float currentSpeed;
    Vector3 currentMoveDirection; // Для аніматора
    
    float xRotation;
    float yRotation;
    bool isGrounded;

    float currentCamY;
    Vector3 cameraDefaultLocalPos;
    float bobTimer;
    float bobOffsetY;

    enum MoveState { Stand, Crouch, Prone }
    MoveState currentState = MoveState.Stand;
    MoveState desiredState = MoveState.Stand;

    bool crouchToggle;
    bool proneToggle;
    bool justGotUp;

    Quaternion baseHeadRotation;
    Quaternion baseSpineRotation;

    // Foot IK variables
    float leftFootWeight = 0f, rightFootWeight = 0f;
    Vector3 leftFootPos, rightFootPos;
    Quaternion leftFootRot, rightFootRot;
    Vector3 leftFootVel, rightFootVel;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!cameraTransform) cameraTransform = Camera.main.transform;
        cameraDefaultLocalPos = cameraTransform.localPosition;
        currentCamY = cameraDefaultLocalPos.y;

        if (!meshRoot) meshRoot = transform;

        if (headBone) baseHeadRotation = headBone.localRotation;
        if (spineBone) baseSpineRotation = spineBone.localRotation;
    }

    void Update()
    {
        GroundCheck();
        HandleStateInput();
        Look();
        Move();
        UpdateHeightAndCamera();
        UpdateAnimatorParameters();
    }

    void LateUpdate()
    {
        // Sync head and spine bones with camera rotation
        if (headBone)
        {
            headBone.localRotation = Quaternion.Euler(xRotation, 0f, 0f) * baseHeadRotation;
        }
        if (spineBone)
        {
            spineBone.localRotation = Quaternion.Euler(xRotation * spineRotationWeight, 0f, 0f) * baseSpineRotation;
        }
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

        // Зберігаємо напрямок для аніматора
        currentMoveDirection = move;

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (isGrounded && Input.GetButtonDown("Jump") && currentState == MoveState.Stand && !justGotUp)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            if (animator) animator.SetTrigger("Jump");
        }

        justGotUp = false;

        velocity.y += (velocity.y < 0 ? gravity * fallMultiplier : gravity) * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ================= HEIGHT + CAMERA =================

    void UpdateHeightAndCamera()
    {
        float targetHeight = GetTargetHeight();
        float targetCamY = GetTargetCamY();
        Vector3 targetCenter = GetTargetCenter();

        float distance = Mathf.Abs(controller.height - targetHeight);
        float smooth = Mathf.Lerp(12f, 4f, distance);

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * smooth);

        Vector3 center = controller.center;
        controller.center = Vector3.Lerp(controller.center, targetCenter, Time.deltaTime * smooth);

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

    Vector3 GetTargetCenter()
    {
        switch (desiredState)
        {
            case MoveState.Crouch: return controllerCenterCrouch;
            case MoveState.Prone: return controllerCenterProne;
            default: return controllerCenterStanding;
        }
    }

    // ================= CAMERA LOOK ==================

    void Look()
    {
        float sens = baseSensitivity * (dpi / 800f);

        float mouseX = Input.GetAxisRaw("Mouse X") * sens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sens;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -clampAngle, clampAngle);
        yRotation += mouseX;

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    void GroundCheck()
    {
        isGrounded = controller.isGrounded;
    }

    // ===================== ANIMATOR =====================
    void UpdateAnimatorParameters()
    {
        if (!animator) return;

        // Використовуємо збережений напрямок руху замість controller.velocity
        Vector3 localVel = transform.InverseTransformDirection(currentMoveDirection * currentSpeed);

        bool isSprinting = currentState == MoveState.Stand && Input.GetKey(KeyCode.LeftShift);
        bool isProne = currentState == MoveState.Prone;
        bool isCrouched = currentState == MoveState.Crouch;

        float maxSpeed = isProne ? proneSpeed : isCrouched ? crouchSpeed : isSprinting ? runSpeed : walkSpeed;
        float moveX = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
        float moveY = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);

        animator.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        animator.SetFloat("MoveY", moveY, 0.1f, Time.deltaTime);
        animator.SetBool("Sprint", isSprinting);
        animator.SetBool("IsGrounded", controller.isGrounded);
    }

    // ===================== FOOT IK =====================
    void OnAnimatorIK(int layerIndex)
    {
        if (!animator || !enableFootIK) return;

        UpdateFootIK(AvatarIKGoal.LeftFoot, ref leftFootWeight, ref leftFootPos, ref leftFootRot, ref leftFootVel);
        UpdateFootIK(AvatarIKGoal.RightFoot, ref rightFootWeight, ref rightFootPos, ref rightFootRot, ref rightFootVel);
    }

    void UpdateFootIK(AvatarIKGoal goal, ref float weight, ref Vector3 footPos, ref Quaternion footRot, ref Vector3 vel)
    {
        Vector3 origin = animator.GetIKPosition(goal) + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, footIkRayDistance, groundMask))
        {
            Vector3 targetPos = hit.point + Vector3.up * 0.02f;
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;

            footPos = Vector3.SmoothDamp(footPos, targetPos, ref vel, footIkSmoothTime);
            footRot = Quaternion.Slerp(footRot, targetRot, 1f - Mathf.Exp(-20f * Time.deltaTime));
            weight = Mathf.MoveTowards(weight, footIkWeight, Time.deltaTime * 5f);
        }
        else
        {
            weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime * 5f);
        }

        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);

        if (weight > 0.01f)
        {
            animator.SetIKPosition(goal, footPos);
            animator.SetIKRotation(goal, footRot);
        }
    }
}
