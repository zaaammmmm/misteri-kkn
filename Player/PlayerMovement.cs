using UnityEngine;
using KKN.Game.Core;

namespace KKN.Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float sprintSpeed = 4f;
        [SerializeField] private float crouchSpeed = 1.2f;

        [Header("Acceleration")]
        [SerializeField] private float acceleration = 4f;
        [SerializeField] private float deceleration = 6f;
        [SerializeField] private float airControl = 0.2f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 0.8f;
        [SerializeField] private float jumpCooldown = 0.2f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float fallMultiplier = 2.5f;

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float sprintDrain = 22f;
        [SerializeField] private float staminaRecover = 14f;
        [SerializeField] private float recoverDelay = 1.2f;

        [Header("Crouch")]
        [SerializeField] private float normalHeight = 2f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchSmooth = 10f;

        [Header("Headbob")]
        [SerializeField] private float bobFrequency = 7f;
        [SerializeField] private float bobAmplitude = 0.03f;

        [Header("Camera Sway")]
        [SerializeField] private float swayAmount = 2f;
        [SerializeField] private float swaySmooth = 6f;

        [Header("Landing")]
        [SerializeField] private float landingImpact = 0.12f;
        [SerializeField] private float landingRecoverSpeed = 6f;

        private CharacterController controller;

        private Vector3 moveVelocity;
        private Vector3 velocity;

        private float stamina;
        private float recoverTimer;
        private float jumpTimer;

        private float headbobTimer;

        private float defaultCamY;
        private Vector3 defaultCamLocalPos;

        private bool wasGrounded;

        private float landingOffset;

        void Start()
        {
            controller = GetComponent<CharacterController>();

            stamina = maxStamina;

            if (cameraTransform != null)
            {
                defaultCamY = cameraTransform.localPosition.y;
                defaultCamLocalPos = cameraTransform.localPosition;
            }
        }

        void Update()
        {
            if (PlayerState.Instance != null &&
                !PlayerState.Instance.CanMove)
                return;

            HandleMovement();
            HandleJump();
            HandleGravity();
            HandleStamina();
            HandleCrouch();
            HandleHeadbob();
            HandleCameraSway();
            HandleLanding();

            wasGrounded = controller.isGrounded;
        }

        void HandleMovement()
        {
            Vector2 input = InputManager.Instance != null
                ? InputManager.Instance.GetMovementInput()
                : new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical")
                );

            Vector3 direction =
                (transform.right * input.x +
                 transform.forward * input.y).normalized;

            bool moving = direction.magnitude > 0.1f;

            bool crouching =
                PlayerState.Instance != null &&
                PlayerState.Instance.IsCrouching;

            bool sprintHeld = InputManager.Instance != null
                ? InputManager.Instance.GetRunHeld()
                : Input.GetKey(GameConstants.KEY_RUN);

            bool canSprint =
                moving &&
                !crouching &&
                stamina > 5f &&
                PlayerState.Instance != null &&
                PlayerState.Instance.CanSprint;

            if (PlayerState.Instance != null)
                PlayerState.Instance.IsRunning =
                    sprintHeld && canSprint;

            float targetSpeed = walkSpeed;

            if (PlayerState.Instance != null &&
                PlayerState.Instance.IsRunning)
            {
                targetSpeed = sprintSpeed;
            }

            if (crouching)
            {
                targetSpeed = crouchSpeed;
            }

            Vector3 targetVelocity = direction * targetSpeed;

            float control =
                controller.isGrounded ? 1f : airControl;

            float accel =
                moving ? acceleration : deceleration;

            moveVelocity = Vector3.Lerp(
                moveVelocity,
                targetVelocity,
                accel * control * Time.deltaTime
            );

            controller.Move(moveVelocity * Time.deltaTime);
        }

        void HandleJump()
        {
            jumpTimer -= Time.deltaTime;

            bool jumpPressed =
                Input.GetKeyDown(KeyCode.Space);

            bool crouching =
                PlayerState.Instance != null &&
                PlayerState.Instance.IsCrouching;

            if (controller.isGrounded &&
                jumpPressed &&
                jumpTimer <= 0f &&
                !crouching)
            {
                velocity.y =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);

                jumpTimer = jumpCooldown;
            }
        }

        void HandleGravity()
        {
            if (controller.isGrounded &&
                velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;

            if (velocity.y < 0)
            {
                velocity.y +=
                    gravity * (fallMultiplier - 1)
                    * Time.deltaTime;
            }

            controller.Move(velocity * Time.deltaTime);
        }

        void HandleStamina()
        {
            bool running =
                PlayerState.Instance != null &&
                PlayerState.Instance.IsRunning;

            if (running)
            {
                stamina -= sprintDrain * Time.deltaTime;
                recoverTimer = recoverDelay;
            }
            else
            {
                recoverTimer -= Time.deltaTime;

                if (recoverTimer <= 0f)
                {
                    stamina +=
                        staminaRecover * Time.deltaTime;
                }
            }

            stamina = Mathf.Clamp(
                stamina,
                0,
                maxStamina
            );
        }

        void HandleCrouch()
        {
            bool crouchPressed =
                InputManager.Instance != null
                && InputManager.Instance.GetCrouchDown();

            if (crouchPressed &&
                PlayerState.Instance != null)
            {
                PlayerState.Instance.IsCrouching =
                    !PlayerState.Instance.IsCrouching;
            }

            bool crouching =
                PlayerState.Instance != null &&
                PlayerState.Instance.IsCrouching;

            float targetHeight =
                crouching ? crouchHeight : normalHeight;

            controller.height = Mathf.Lerp(
                controller.height,
                targetHeight,
                crouchSmooth * Time.deltaTime
            );

            Vector3 targetCenter =
                new Vector3(
                    0,
                    targetHeight / 2f,
                    0
                );

            controller.center = Vector3.Lerp(
                controller.center,
                targetCenter,
                crouchSmooth * Time.deltaTime
            );

            if (cameraTransform != null)
            {
                float targetCamY =
                    crouching
                    ? defaultCamY - 0.45f
                    : defaultCamY;

                Vector3 camPos =
                    cameraTransform.localPosition;

                camPos.y = Mathf.Lerp(
                    camPos.y,
                    targetCamY,
                    crouchSmooth * Time.deltaTime
                );

                cameraTransform.localPosition = camPos;
            }
        }

        void HandleHeadbob()
        {
            if (cameraTransform == null)
                return;

            Vector3 horizontalVelocity =
                new Vector3(
                    controller.velocity.x,
                    0,
                    controller.velocity.z
                );

            bool moving =
                horizontalVelocity.magnitude > 0.1f;

            if (moving &&
                controller.isGrounded)
            {
                bool running =
                    PlayerState.Instance != null &&
                    PlayerState.Instance.IsRunning;

                float frequency =
                    running
                    ? bobFrequency * 1.4f
                    : bobFrequency;

                float amplitude =
                    running
                    ? bobAmplitude * 1.3f
                    : bobAmplitude;

                headbobTimer +=
                    Time.deltaTime * frequency;

                float bobY =
                    Mathf.Sin(headbobTimer)
                    * amplitude;

                float bobX =
                    Mathf.Cos(headbobTimer * 0.5f)
                    * amplitude * 0.5f;

                Vector3 target =
                    defaultCamLocalPos +
                    new Vector3(
                        bobX,
                        bobY - landingOffset,
                        0
                    );

                cameraTransform.localPosition =
                    Vector3.Lerp(
                        cameraTransform.localPosition,
                        target,
                        8f * Time.deltaTime
                    );
            }
            else
            {
                headbobTimer = 0;

                Vector3 target =
                    defaultCamLocalPos
                    - new Vector3(
                        0,
                        landingOffset,
                        0
                    );

                cameraTransform.localPosition =
                    Vector3.Lerp(
                        cameraTransform.localPosition,
                        target,
                        6f * Time.deltaTime
                    );
            }
        }

        void HandleCameraSway()
        {
            if (cameraTransform == null)
                return;

            float mouseX =
                Input.GetAxis("Mouse X");

            float mouseY =
                Input.GetAxis("Mouse Y");

            Quaternion targetRot =
                Quaternion.Euler(
                    -mouseY * swayAmount,
                    mouseX * swayAmount,
                    0
                );

            cameraTransform.localRotation =
                Quaternion.Slerp(
                    cameraTransform.localRotation,
                    targetRot,
                    swaySmooth * Time.deltaTime
                );
        }

        void HandleLanding()
        {
            if (!wasGrounded &&
                controller.isGrounded)
            {
                landingOffset = landingImpact;
            }

            landingOffset = Mathf.Lerp(
                landingOffset,
                0,
                landingRecoverSpeed * Time.deltaTime
            );
        }

        public float GetStaminaPercent()
        {
            return stamina / maxStamina;
        }
    }
}