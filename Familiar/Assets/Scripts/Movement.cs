using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]

public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    [Tooltip("Set this higher to make movement more responsive")]
    public float movementSharpness = 15.0f;
    
    [Header("Camera Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    [Tooltip("Maximum vertical angle in degrees - set close to 90 for full up/down look")]
    public float lookXLimit = 89f; // Changed from 45 to 89 for near-full vertical rotation
    [Tooltip("Invert the vertical camera axis")]
    public bool invertMouseY = false;

    [Header("Dialogue Camera Settings")]
    [Tooltip("Height offset from NPC pivot point to look at during dialogue (e.g., 1.6 for head height)")]
    public float dialogueLookHeight = 1.6f;
    [Tooltip("How quickly the camera rotates to face the NPC during dialogue")]
    public float dialogueCameraSpeed = 5f;
    
    [Header("Footstep Sound Settings")]
    public AudioSource footstepAudioSource;
    public AudioClip[] walkingFootstepSounds;
    public AudioClip[] runningFootstepSounds;
    [Tooltip("Time between footstep sounds when walking")]
    public float walkingFootstepInterval = 0.5f;
    [Tooltip("Time between footstep sounds when running")]
    public float runningFootstepInterval = 0.3f;
    [Range(0f, 1f)]
    public float footstepVolume = 0.7f;
    
    // Private variables
    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private bool wasGrounded = false;

    // Footstep audio state
    private float lastFootstepTime;
    private float footstepCooldownUntil;
    
    // Input locks
    [Header("Input Control")]
    [Tooltip("Set this to false to disable all player movement and camera rotation")]
    public bool canMove = true;

    [Tooltip("If true, player can still move but not control the camera")]
    public bool canControlCamera = true;

    [Tooltip("If true, player can still walk but not run or jump")]
    public bool canRun = true;
    public bool canJump = true;

    // Dialogue camera control
    public bool isInDialogue = false;
    public Transform dialogueTarget;
    private float defaultDialogueLookHeight;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Initialize rotation from current camera orientation
        if (playerCamera != null)
        {
            rotationX = playerCamera.transform.localEulerAngles.x;
            
            // Adjust angles over 180 to be negative for proper clamping
            if (rotationX > 180)
            {
                rotationX -= 360;
            }
            
            // Ensure initial rotation is within bounds
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        }
        
        // Create AudioSource if not assigned
        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.spatialBlend = 1.0f; // Make sound 3D
            footstepAudioSource.volume = footstepVolume;
        }

        // Store default dialogue look height
        defaultDialogueLookHeight = dialogueLookHeight;
    }

    void Update()
    {
        bool isMoving = false;
        bool isRunning = false;

        if (canMove)
        {
            // Calculate movement direction
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);

            isRunning = canRun && Input.GetKey(KeyCode.LeftShift);
            float speed = isRunning ? runningSpeed : walkingSpeed;
            float curSpeedX = speed * Input.GetAxis("Vertical");
            float curSpeedY = speed * Input.GetAxis("Horizontal");

            float movementDirectionY = moveDirection.y;
            Vector3 targetDirection = (forward * curSpeedX) + (right * curSpeedY);

            isMoving = targetDirection.sqrMagnitude > 0.01f;

            moveDirection.x = targetDirection.x;
            moveDirection.z = targetDirection.z;

            // Jump
            if (canJump && Input.GetButton("Jump") && characterController.isGrounded)
            {
                moveDirection.y = jumpSpeed;
            }
            else
            {
                moveDirection.y = movementDirectionY;
            }

            // Apply gravity
            if (!characterController.isGrounded)
            {
                moveDirection.y -= gravity * Time.deltaTime;
            }

            characterController.Move(moveDirection * Time.deltaTime);

            // Stop horizontal movement when no input
            if (!isMoving && characterController.isGrounded)
            {
                moveDirection.x = 0;
                moveDirection.z = 0;
            }

            // Handle footstep sounds
            UpdateFootsteps(isMoving, isRunning);

            // Landing sound
            if (characterController.isGrounded && !wasGrounded)
            {
                TryPlayLandingSound();
            }

            wasGrounded = characterController.isGrounded;
        }
        else
        {
            // Apply gravity only when movement disabled
            moveDirection.y -= gravity * Time.deltaTime;
            if (characterController.isGrounded)
            {
                moveDirection.y = 0;
            }

            characterController.Move(new Vector3(0, moveDirection.y, 0) * Time.deltaTime);

            moveDirection.x = 0;
            moveDirection.z = 0;
            wasGrounded = characterController.isGrounded;
        }

        // Camera rotation - only if canControlCamera is true
        if (canControlCamera && canMove)
        {
            // Get mouse input and apply inversion if needed
            float mouseY = Input.GetAxis("Mouse Y");
            if (invertMouseY)
                mouseY = -mouseY;

            // Apply the mouse input to rotation
            rotationX += -mouseY * lookSpeed;

            // Clamp the vertical rotation to avoid flipping
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

            // Apply rotation to camera
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            }

            // Rotate the player horizontally (left/right)
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }

        // Handle dialogue camera facing
        if (isInDialogue && dialogueTarget != null && playerCamera != null)
        {
            Vector3 targetPosition = dialogueTarget.position + Vector3.up * dialogueLookHeight;
            Vector3 direction = targetPosition - playerCamera.transform.position;
            Vector3 localDirection = transform.InverseTransformDirection(direction);
            Quaternion targetRotation = Quaternion.LookRotation(localDirection);
            playerCamera.transform.localRotation = Quaternion.Slerp(
                playerCamera.transform.localRotation,
                targetRotation,
                dialogueCameraSpeed * Time.deltaTime
            );
        }
    }
    
    private void UpdateFootsteps(bool isMoving, bool isRunning)
    {
        // Only play footsteps when grounded and moving
        if (!characterController.isGrounded || !isMoving)
            return;

        // Check if enough time has passed since last footstep
        float interval = isRunning ? runningFootstepInterval : walkingFootstepInterval;
        float timeSinceLastStep = Time.time - lastFootstepTime;

        if (timeSinceLastStep >= interval && Time.time >= footstepCooldownUntil)
        {
            PlayFootstepSound(isRunning);
            lastFootstepTime = Time.time;
        }
    }

    private void PlayFootstepSound(bool isRunning)
    {
        if (footstepAudioSource == null)
            return;

        // Don't play if already playing a footstep
        if (footstepAudioSource.isPlaying)
            return;

        AudioClip[] sounds = isRunning ? runningFootstepSounds : walkingFootstepSounds;

        // Fallback to walking sounds if running sounds not assigned
        if ((sounds == null || sounds.Length == 0) && walkingFootstepSounds != null)
            sounds = walkingFootstepSounds;

        if (sounds == null || sounds.Length == 0)
            return;

        AudioClip clip = sounds[Random.Range(0, sounds.Length)];
        if (clip != null)
        {
            footstepAudioSource.clip = clip;
            footstepAudioSource.volume = footstepVolume;
            footstepAudioSource.Play();
        }
    }

    private void TryPlayLandingSound()
    {
        // Respect cooldown to prevent sounds on movement re-enable
        if (Time.time < footstepCooldownUntil)
            return;

        // Don't play if already playing a footstep
        if (footstepAudioSource == null || footstepAudioSource.isPlaying)
            return;

        if (walkingFootstepSounds == null || walkingFootstepSounds.Length == 0)
            return;

        AudioClip clip = walkingFootstepSounds[Random.Range(0, walkingFootstepSounds.Length)];
        if (clip != null)
        {
            footstepAudioSource.clip = clip;
            footstepAudioSource.volume = footstepVolume * 1.2f;
            footstepAudioSource.Play();
            lastFootstepTime = Time.time;
        }
    }

    private void StopFootstepAudio()
    {
        if (footstepAudioSource != null)
        {
            footstepAudioSource.Stop();
        }
        footstepCooldownUntil = Time.time + 0.5f;
        lastFootstepTime = 0f;
    }
    
    public void DisableAllInput()
    {
        canMove = false;
        canControlCamera = false;
        canRun = false;
        canJump = false;
        StopFootstepAudio();
    }

    public void EnableAllInput()
    {
        StopFootstepAudio();
        canMove = true;
        canControlCamera = true;
        canRun = true;
        canJump = true;
    }
    
    // Public method to disable only camera control
    public void DisableCameraControl()
    {
        canControlCamera = false;
    }
    
    // Public method to enable only camera control
    public void EnableCameraControl()
    {
        canControlCamera = true;
    }

    // Public method to set camera target for dialogue
    public void SetCameraTarget(Transform target)
    {
        dialogueTarget = target;
        isInDialogue = true;
    }

    // Public method to set camera target with custom height (for kneeling NPCs, etc.)
    public void SetCameraTarget(Transform target, float customLookHeight)
    {
        dialogueTarget = target;
        dialogueLookHeight = customLookHeight;
        isInDialogue = true;
    }

    // Public method to clear camera target after dialogue
    public void ClearCameraTarget()
    {
        dialogueTarget = null;
        dialogueLookHeight = defaultDialogueLookHeight;
        isInDialogue = false;
    }
}
