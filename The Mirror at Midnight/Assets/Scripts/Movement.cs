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
    private float footstepTimer = 0;
    private bool wasGrounded = false;
    private bool wasMovementDisabled = false; // Track when movement was just re-enabled
    private float movementReenabledCooldown = 0f; // Cooldown after re-enabling movement
    
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
    }

    void Update()
    {
        // Update cooldown timer
        if (movementReenabledCooldown > 0)
        {
            movementReenabledCooldown -= Time.deltaTime;
        }
        
        // Movement is allowed if canMove is true
        bool isMoving = false;
        bool isRunning = false;
        
        if (canMove)
        {
            // Check if movement was just re-enabled
            if (wasMovementDisabled)
            {
                wasMovementDisabled = false;
                movementReenabledCooldown = 0.5f; // Half second cooldown for sounds
                footstepTimer = 0f; // Reset timer
                
                // Clear all queued audio
                if (footstepAudioSource != null)
                {
                    footstepAudioSource.Stop();
                    footstepAudioSource.clip = null;
                }
            }
            
            // We are grounded, so recalculate move direction based on axes
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);
            
            // Press Left Shift to run (only if canRun is true)
            isRunning = canRun && Input.GetKey(KeyCode.LeftShift);
            float curSpeedX = (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical");
            float curSpeedY = (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal");
            
            // Store vertical movement
            float movementDirectionY = moveDirection.y;
            
            // Calculate target movement direction
            Vector3 targetDirection = (forward * curSpeedX) + (right * curSpeedY);
            
            // Check if player is moving horizontally
            isMoving = !Mathf.Approximately(targetDirection.magnitude, 0);
            
            // Apply sharper movement by directly setting the movement direction
            // instead of smoothly interpolating
            if (movementSharpness > 0)
            {
                // Direct movement with high sharpness for more responsive control
                moveDirection.x = targetDirection.x;
                moveDirection.z = targetDirection.z;
            }
            else
            {
                // Fallback to original behavior if sharpness is disabled
                moveDirection.x = targetDirection.x;
                moveDirection.z = targetDirection.z;
            }

            // Jump only if canJump is true
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

            // Move the controller with direct input for sharper response
            characterController.Move(moveDirection * Time.deltaTime);
            
            // If no input, quickly stop horizontal movement to prevent sliding
            if (Mathf.Approximately(Input.GetAxis("Vertical"), 0) && 
                Mathf.Approximately(Input.GetAxis("Horizontal"), 0) && 
                characterController.isGrounded)
            {
                // Reset horizontal movement immediately when no input is detected
                moveDirection.x = 0;
                moveDirection.z = 0;
            }
            
            // Handle footsteps ONLY if cooldown has expired
            if (movementReenabledCooldown <= 0)
            {
                HandleFootsteps(isMoving, isRunning);
            }
            
            // Check for landing after being in the air (also respect cooldown)
            if (characterController.isGrounded && !wasGrounded && movementReenabledCooldown <= 0)
            {
                PlayLandingSound();
            }
            
            // Update grounded state for next frame
            wasGrounded = characterController.isGrounded;
        }
        else
        {
            // Mark that movement is disabled
            if (!wasMovementDisabled)
            {
                wasMovementDisabled = true;
                footstepTimer = 0f;
                
                // Stop all audio
                if (footstepAudioSource != null)
                {
                    footstepAudioSource.Stop();
                    footstepAudioSource.clip = null;
                }
            }
            
            // Reset vertical movement when canMove is false
            // This prevents "storing" jump input while paused
            moveDirection.y -= gravity * Time.deltaTime;
            if (characterController.isGrounded)
            {
                moveDirection.y = 0;
            }
            
            // Apply only gravity when movement is disabled
            characterController.Move(new Vector3(0, moveDirection.y, 0) * Time.deltaTime);
            
            // Reset horizontal movement completely when disabled
            moveDirection.x = 0;
            moveDirection.z = 0;
            
            // Update grounded state for next frame
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
            Vector3 targetPosition = dialogueTarget.position + Vector3.up * 0.5f; // Adjust 0.5f as needed
            Vector3 direction = targetPosition - playerCamera.transform.position;
            Vector3 localDirection = transform.InverseTransformDirection(direction);
            playerCamera.transform.localRotation = Quaternion.LookRotation(localDirection);
        }
    }
    
    // Handle playing footstep sounds based on movement
    private void HandleFootsteps(bool isMoving, bool isRunning)
    {
        if (characterController.isGrounded && isMoving)
        {
            // Determine the appropriate footstep interval
            float footstepInterval = isRunning ? runningFootstepInterval : walkingFootstepInterval;
            
            // Update the timer
            footstepTimer += Time.deltaTime;
            
            // Play footstep sound when interval is reached
            if (footstepTimer >= footstepInterval)
            {
                PlayFootstepSound(isRunning);
                footstepTimer = 0f;
            }
        }
        else
        {
            // Reset timer when not moving or not grounded
            footstepTimer = 0f;
        }
    }
    
    // Play appropriate footstep sound
    private void PlayFootstepSound(bool isRunning)
    {
        if (footstepAudioSource != null)
        {
            // Use correct sound array based on running state
            AudioClip[] soundArray = isRunning ? walkingFootstepSounds : walkingFootstepSounds;
            
            // Check if we have any footstep sounds assigned
            if (soundArray != null && soundArray.Length > 0)
            {
                // Pick a random sound from the array
                AudioClip footstepSound = soundArray[Random.Range(0, soundArray.Length)];
                
                if (footstepSound != null)
                {
                    // Set volume and play the sound
                    footstepAudioSource.volume = footstepVolume;
                    footstepAudioSource.PlayOneShot(footstepSound);
                }
            }
        }
    }
    
    // Play a sound when landing from a jump or fall
    private void PlayLandingSound()
    {
        if (footstepAudioSource != null && walkingFootstepSounds != null && walkingFootstepSounds.Length > 0)
        {
            // Use walking footstep for landing or create dedicated landing sounds if desired
            AudioClip landSound = walkingFootstepSounds[Random.Range(0, walkingFootstepSounds.Length)];
            if (landSound != null)
            {
                // Play landing sound at slightly higher volume
                footstepAudioSource.volume = Mathf.Min(footstepVolume * 1.2f, 1.0f);
                footstepAudioSource.PlayOneShot(landSound);
            }
        }
    }
    
    // Public method to completely disable player input
    public void DisableAllInput()
    {
        canMove = false;
        canControlCamera = false;
        canRun = false;
        canJump = false;
        
        // Clear audio state
        footstepTimer = 0f;
        if (footstepAudioSource != null)
        {
            footstepAudioSource.Stop();
            footstepAudioSource.clip = null;
        }
    }
    
    // Public method to completely enable player input
    public void EnableAllInput()
    {
        // Clear audio state before enabling
        footstepTimer = 0f;
        movementReenabledCooldown = 0.5f; // Set cooldown
        
        if (footstepAudioSource != null)
        {
            footstepAudioSource.Stop();
            footstepAudioSource.clip = null;
        }
        
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

    // Public method to clear camera target after dialogue
    public void ClearCameraTarget()
    {
        dialogueTarget = null;
        isInDialogue = false;
    }
}
