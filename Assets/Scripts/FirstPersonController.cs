using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HangingWoman
{
    /// <summary>
    /// Slow, grounded first person controller meant for walking (not running and gunning).
    /// WASD / arrows to move, mouse to look, Shift to hurry, Ctrl or C to crouch,
    /// Space to jump (optional), F for the flashlight, Esc to free the cursor.
    /// Works with both the legacy Input Manager and the new Input System package.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Pivot for pitch, head bob and crouching. The camera should be a child of this.")]
        public Transform cameraRoot;
        public Camera playerCamera;
        [Tooltip("Optional spotlight toggled with F.")]
        public Light flashlight;

        [Header("Movement")]
        public float walkSpeed = 1.9f;
        public float sprintSpeed = 3.8f;
        public float crouchSpeed = 1.0f;
        public float acceleration = 8f;
        [Range(0f, 1f)] public float airControl = 0.3f;
        public float gravity = -18f;
        public bool allowJump = true;
        public float jumpHeight = 0.8f;

        [Header("Look")]
        public float lookSensitivity = 2f;
        public bool invertY;
        public float maxPitch = 85f;
        [Tooltip("Degrees the camera rolls when strafing.")]
        public float strafeTilt = 1.2f;

        [Header("Crouch")]
        public float standingHeight = 1.8f;
        public float crouchingHeight = 1.1f;
        public float standingEyeHeight = 1.65f;
        public float crouchingEyeHeight = 0.95f;
        public float crouchTransitionSpeed = 8f;

        [Header("Head Bob")]
        public bool headBob = true;
        [Tooltip("Metres travelled per footstep while walking.")]
        public float stepLength = 0.75f;
        public float bobAmount = 0.035f;
        public float swayAmount = 0.02f;

        [Header("Footsteps")]
        public AudioSource footstepSource;
        [Tooltip("Leave empty to use generated wet-pavement footsteps.")]
        public AudioClip[] footstepClips;
        public float footstepVolume = 0.35f;

        /// <summary>Scales all movement speeds. Used by triggers to slow the player down.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>When true, the player can't move or look; scripted events drive the camera through Yaw and Pitch.</summary>
        public bool InputLocked { get; set; }
        /// <summary>Body heading in degrees.</summary>
        public float Yaw
        {
            get => transform.eulerAngles.y;
            set => transform.rotation = Quaternion.Euler(0f, value, 0f);
        }
        /// <summary>Camera pitch in degrees; positive looks down, negative looks up.</summary>
        public float Pitch
        {
            get => pitch;
            set => pitch = Mathf.Clamp(value, -maxPitch, maxPitch);
        }
        public Vector3 EyePosition => playerCamera != null ? playerCamera.transform.position : transform.position + Vector3.up * standingEyeHeight;
        public bool IsCrouching => crouching;
        public bool IsGrounded => controller.isGrounded;
        public float CurrentSpeed => planarVelocity.magnitude;

        CharacterController controller;
        Vector3 planarVelocity;
        float verticalVelocity;
        float pitch;
        float roll;
        bool crouching;
        bool sprinting;
        float currentEyeHeight;
        float stepProgress;
        float stepSide = 1f;
        float bobWeight;
        bool wasGrounded = true;
        float fallSpeed;
        int lastClip = -1;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraRoot == null && playerCamera != null) cameraRoot = playerCamera.transform.parent;
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            currentEyeHeight = standingEyeHeight;

            if (footstepSource == null)
            {
                footstepSource = gameObject.AddComponent<AudioSource>();
                footstepSource.playOnAwake = false;
                footstepSource.spatialBlend = 0f;
            }
            if (footstepClips == null || footstepClips.Length == 0)
            {
                footstepClips = new AudioClip[4];
                for (int i = 0; i < footstepClips.Length; i++)
                    footstepClips[i] = ProceduralAudio.CreateFootstep(100 + i);
            }
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            HandleCursor();
            if (Cursor.lockState == CursorLockMode.Locked && !InputLocked) HandleLook();
            HandleCrouch();
            HandleMovement();
            HandleHeadBob();

            if (flashlight != null && !InputLocked && FPInput.FlashlightPressed())
                flashlight.enabled = !flashlight.enabled;
        }

        void HandleCursor()
        {
            if (FPInput.EscapePressed()) LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && FPInput.ClickPressed()) LockCursor(true);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void HandleLook()
        {
            Vector2 look = FPInput.Look() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f, Space.Self);
            pitch += invertY ? look.y : -look.y;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        }

        void HandleCrouch()
        {
            bool wantsCrouch = InputLocked ? crouching : FPInput.CrouchHeld();
            if (wantsCrouch) crouching = true;
            else if (crouching && CanStand()) crouching = false;

            float targetHeight = crouching ? crouchingHeight : standingHeight;
            if (!Mathf.Approximately(controller.height, targetHeight))
            {
                controller.height = Mathf.MoveTowards(controller.height, targetHeight,
                    Time.deltaTime * crouchTransitionSpeed * (standingHeight - crouchingHeight));
                controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            }
        }

        bool CanStand()
        {
            Vector3 top = transform.position + Vector3.up * (controller.height - 0.05f);
            float distance = standingHeight - controller.height + 0.1f;
            return !Physics.SphereCast(top - Vector3.up * controller.radius * 0.5f, controller.radius * 0.9f,
                Vector3.up, out _, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void HandleMovement()
        {
            float dt = Time.deltaTime;
            bool grounded = controller.isGrounded;

            Vector2 input = InputLocked ? Vector2.zero : FPInput.Move();
            if (input.sqrMagnitude > 1f) input.Normalize();

            sprinting = !InputLocked && !crouching && FPInput.SprintHeld() && input.y > 0.1f;
            float speed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            speed *= SpeedMultiplier;

            Vector3 desired = (transform.right * input.x + transform.forward * input.y) * speed;
            float accel = grounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, accel * dt);

            if (grounded)
            {
                if (!wasGrounded && fallSpeed < -4f) PlayFootstep(Mathf.Clamp01(-fallSpeed / 10f) + 0.4f);
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (allowJump && !crouching && !InputLocked && FPInput.JumpPressed())
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * dt;
            fallSpeed = verticalVelocity;
            wasGrounded = grounded;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
        }

        void HandleHeadBob()
        {
            if (cameraRoot == null) return;
            float dt = Time.deltaTime;

            float targetEye = crouching ? crouchingEyeHeight : standingEyeHeight;
            currentEyeHeight = Mathf.Lerp(currentEyeHeight, targetEye, dt * crouchTransitionSpeed);

            Vector3 horizontal = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            float speed = horizontal.magnitude;
            bool moving = controller.isGrounded && speed > 0.2f;

            if (moving)
            {
                float length = stepLength * (sprinting ? 1.35f : crouching ? 0.8f : 1f);
                stepProgress += speed * dt / length;
                if (stepProgress >= 1f)
                {
                    stepProgress -= 1f;
                    stepSide = -stepSide;
                    PlayFootstep(sprinting ? 1.4f : crouching ? 0.5f : 1f);
                }
            }
            bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, dt * 3f);

            float intensity = headBob ? bobWeight * Mathf.Clamp(speed / Mathf.Max(walkSpeed, 0.01f), 0f, 1.6f) : 0f;
            float bobY = -bobAmount * (1f + Mathf.Cos(stepProgress * Mathf.PI * 2f)) * 0.5f * intensity;
            float swayX = swayAmount * Mathf.Sin(stepProgress * Mathf.PI) * stepSide * intensity;

            float strafe = Vector3.Dot(controller.velocity, transform.right);
            roll = Mathf.Lerp(roll, -strafe / Mathf.Max(walkSpeed, 0.01f) * strafeTilt, dt * 6f);

            cameraRoot.localPosition = new Vector3(swayX, currentEyeHeight + bobY, 0f);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        void PlayFootstep(float loudness)
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;
            int index = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && index == lastClip) index = (index + 1) % footstepClips.Length;
            lastClip = index;
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(footstepClips[index], footstepVolume * loudness);
        }

        /// <summary>Thin wrapper so the controller runs on either input backend.</summary>
        static class FPInput
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            public static Vector2 Move()
            {
                var k = Keyboard.current;
                Vector2 v = Vector2.zero;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                }
                var g = Gamepad.current;
                if (g != null) v += g.leftStick.ReadValue();
                return v;
            }

            public static Vector2 Look()
            {
                Vector2 v = Vector2.zero;
                // Scaled to roughly match the legacy "Mouse X/Y" axes.
                if (Mouse.current != null) v += Mouse.current.delta.ReadValue() * 0.1f;
                if (Gamepad.current != null) v += Gamepad.current.rightStick.ReadValue() * (60f * Time.deltaTime);
                return v;
            }

            public static bool SprintHeld() =>
                (Keyboard.current?.leftShiftKey.isPressed ?? false) || (Gamepad.current?.leftStickButton.isPressed ?? false);
            public static bool CrouchHeld() =>
                (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed)) ||
                (Gamepad.current?.buttonEast.isPressed ?? false);
            public static bool JumpPressed() =>
                (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false);
            public static bool FlashlightPressed() =>
                (Keyboard.current?.fKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false);
            public static bool EscapePressed() => Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
            public static bool ClickPressed() => Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#else
            public static Vector2 Move() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            public static Vector2 Look() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            public static bool SprintHeld() => Input.GetKey(KeyCode.LeftShift);
            public static bool CrouchHeld() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            public static bool JumpPressed() => Input.GetKeyDown(KeyCode.Space);
            public static bool FlashlightPressed() => Input.GetKeyDown(KeyCode.F);
            public static bool EscapePressed() => Input.GetKeyDown(KeyCode.Escape);
            public static bool ClickPressed() => Input.GetMouseButtonDown(0);
#endif
        }
    }
}
