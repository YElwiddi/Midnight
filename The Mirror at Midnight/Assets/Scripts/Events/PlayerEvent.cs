using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Movement))]
public class PlayerEvent : MonoBehaviour
{
    // Reference to the Movement script
    private Movement playerMovement;
    
    // Optional UnityEvent callbacks that can be assigned in the inspector
    [Header("Movement Events")]
    [Tooltip("Event triggered when player movement is enabled")]
    public UnityEvent OnMovementEnabled;
    
    [Tooltip("Event triggered when player movement is disabled")]
    public UnityEvent OnMovementDisabled;
    
    [Header("Camera Control Events")]
    [Tooltip("Event triggered when camera control is enabled")]
    public UnityEvent OnCameraControlEnabled;
    
    [Tooltip("Event triggered when camera control is disabled")]
    public UnityEvent OnCameraControlDisabled;
    
    [Header("State Tracking")]
    [SerializeField, Tooltip("Read-only indicator of current movement state")]
    private bool isMovementEnabled = true;
    
    [SerializeField, Tooltip("Read-only indicator of current camera control state")]
    private bool isCameraControlEnabled = true;
    
    // Property to check if movement is currently enabled
    public bool IsMovementEnabled => isMovementEnabled;
    
    // Property to check if camera control is currently enabled
    public bool IsCameraControlEnabled => isCameraControlEnabled;

    private void Awake()
    {
        // Get reference to the Movement component
        playerMovement = GetComponent<Movement>();
        
        if (playerMovement == null)
        {
            Debug.LogError("PlayerEvent requires a Movement component on the same GameObject.");
        }
        
        // Initialize state tracking variables
        isMovementEnabled = playerMovement.canMove;
        isCameraControlEnabled = playerMovement.canControlCamera;
    }

    #region Movement Control Methods
    
    /// <summary>
    /// Enable player movement and running/jumping capabilities, lock cursor
    /// </summary>
    public void EnableMovement()
    {
        if (playerMovement != null)
        {
            playerMovement.canMove = true;
            playerMovement.canRun = true;
            playerMovement.canJump = true;
            
            // Lock cursor and hide it
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Update tracking variable
            isMovementEnabled = true;
            
            // Invoke the UnityEvent
            OnMovementEnabled?.Invoke();
            
            Debug.Log("Player movement enabled, cursor locked");
        }
    }
    
    /// <summary>
    /// Disable player movement and running/jumping capabilities, unlock cursor
    /// </summary>
    public void DisableMovement()
    {
        if (playerMovement != null)
        {
            playerMovement.canMove = false;
            playerMovement.canRun = false;
            playerMovement.canJump = false;
            
            // Unlock cursor and make it visible
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Update tracking variable
            isMovementEnabled = false;
            
            // Invoke the UnityEvent
            OnMovementDisabled?.Invoke();
            
            Debug.Log("Player movement disabled, cursor unlocked");
        }
    }
    
    /// <summary>
    /// Toggle the player movement state
    /// </summary>
    public void ToggleMovement()
    {
        if (isMovementEnabled)
        {
            DisableMovement();
        }
        else
        {
            EnableMovement();
        }
    }
    
    #endregion
    
    #region Camera Control Methods
    
    /// <summary>
    /// Enable camera control for the player
    /// </summary>
    public void EnableCameraControl()
    {
        if (playerMovement != null)
        {
            playerMovement.canControlCamera = true;
            
            // Update tracking variable
            isCameraControlEnabled = true;
            
            // Invoke the UnityEvent
            OnCameraControlEnabled?.Invoke();
            
            Debug.Log("Camera control enabled");
        }
    }
    
    /// <summary>
    /// Disable camera control for the player
    /// </summary>
    public void DisableCameraControl()
    {
        if (playerMovement != null)
        {
            playerMovement.canControlCamera = false;
            
            // Update tracking variable
            isCameraControlEnabled = false;
            
            // Invoke the UnityEvent
            OnCameraControlDisabled?.Invoke();
            
            Debug.Log("Camera control disabled");
        }
    }
    
    /// <summary>
    /// Toggle the player camera control state
    /// </summary>
    public void ToggleCameraControl()
    {
        if (isCameraControlEnabled)
        {
            DisableCameraControl();
        }
        else
        {
            EnableCameraControl();
        }
    }
    
    #endregion
    
    #region Complete Control Methods
    
    /// <summary>
    /// Enable all player input (movement and camera control)
    /// </summary>
    public void EnableAllInput()
    {
        EnableMovement();
        EnableCameraControl();
    }
    
    /// <summary>
    /// Disable all player input (movement and camera control)
    /// </summary>
    public void DisableAllInput()
    {
        DisableMovement();
        DisableCameraControl();
    }
    
    #endregion
}