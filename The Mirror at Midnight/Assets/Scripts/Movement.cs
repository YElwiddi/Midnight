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
    
    // Private variables
    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    
    // Input locks
    [Header("Input Control")]
    [Tooltip("Set this to false to disable all player movement and camera rotation")]
    public bool canMove = true;
    
    [Tooltip("If true, player can still move but not control the camera")]
    public bool canControlCamera = true;
    
    [Tooltip("If true, player can still walk but not run or jump")]
    public bool canRun = true;
    public bool canJump = true;

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
    }

    void Update()
    {
        // Movement is allowed if canMove is true
        if (canMove)
        {
            // We are grounded, so recalculate move direction based on axes
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);
            
            // Press Left Shift to run (only if canRun is true)
            bool isRunning = canRun && Input.GetKey(KeyCode.LeftShift);
            float curSpeedX = (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical");
            float curSpeedY = (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal");
            
            // Store vertical movement
            float movementDirectionY = moveDirection.y;
            
            // Calculate target movement direction
            Vector3 targetDirection = (forward * curSpeedX) + (right * curSpeedY);
            
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
        }
        else
        {
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
    }
    
    // Public method to completely disable player input
    public void DisableAllInput()
    {
        canMove = false;
        canControlCamera = false;
        canRun = false;
        canJump = false;
    }
    
    // Public method to completely enable player input
    public void EnableAllInput()
    {
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
}