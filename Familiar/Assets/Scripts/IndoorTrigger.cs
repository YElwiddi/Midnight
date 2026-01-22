using UnityEngine;

/// <summary>
/// Attach this to a trigger collider that covers an indoor area.
/// When the player enters, ambient sound will lower. When they exit, it restores.
/// </summary>
public class IndoorTrigger : MonoBehaviour
{
    [Tooltip("Tag used to identify the player")]
    public string playerTag = "Player";

    [Tooltip("Show the trigger bounds in the editor")]
    public bool showGizmo = true;

    [Tooltip("Color of the gizmo in the editor")]
    public Color gizmoColor = new Color(0f, 0.5f, 1f, 0.3f);

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            AmbientSoundManager.Instance?.EnterIndoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            AmbientSoundManager.Instance?.ExitIndoor();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Collider col = GetComponent<Collider>();

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
}
