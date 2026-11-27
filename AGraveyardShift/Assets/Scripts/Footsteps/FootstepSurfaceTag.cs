using UnityEngine;

/// <summary>
/// Forces the footstep sound for this object and everything under it, ahead of the automatic rules in
/// Resources/Footsteps/FootstepSettings. Use it on a floor that sounds wrong (a rug, a wooden porch, a metal grate).
/// </summary>
public class FootstepSurfaceTag : MonoBehaviour
{
    public FootSurface surface = FootSurface.Stone;
}
