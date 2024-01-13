using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowCursor : MonoBehaviour
{
  public float rotationSpeed = 5f;

    void Update()
    {
        // Get the screen position of the cursor
        Vector3 mousePos = Input.mousePosition;

        // Create a ray from the camera to the cursor position
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // Declare a variable to store the hit information
        RaycastHit hit;

        // Check if the ray hits something in the scene
        if (Physics.Raycast(ray, out hit))
        {
            // Get the direction from the camera to the hit point
            Vector3 targetDirection = hit.point - transform.position;

            // Calculate the rotation to look at the hit point
            Quaternion rotation = Quaternion.LookRotation(targetDirection);

            // Smoothly rotate the camera towards the hit point
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        }
    }
}
