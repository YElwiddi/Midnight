using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shows a tablet's ending line under the row while the pointer is on it (EndingsMenuDisplay).</summary>
public class EndingTabletHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public EndingsMenuDisplay display;
    public int index;

    public void OnPointerEnter(PointerEventData e) { if (display != null) display.ShowDescription(index); }
    public void OnPointerExit(PointerEventData e) { if (display != null) display.ShowDescription(-1); }
}
