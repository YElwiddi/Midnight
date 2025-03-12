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
    
    [Header("Camera Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

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
            
            float movementDirectionY = moveDirection.y;
            moveDirection = (forward * curSpeedX) + (right * curSpeedY);

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

            // Move the controller
            characterController.Move(moveDirection * Time.deltaTime);
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
        }

        // Camera rotation - only if canControlCamera is true
        if (canControlCamera && canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            }
            
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