using UnityEngine;

/// <summary>
/// Gives one text on a <see cref="PixelCanvas"/> a finer pixel grid than the canvas default.
/// Big carved capitals read fine at the default 12 virtual pixels per em, but sentences in a
/// fine serif (the crypt fiend lore, for example) break up at that density, so reading text sets
/// a higher minimum and moves to a finer grid. The text still renders as pixels, just smaller ones.
/// </summary>
[DisallowMultipleComponent]
public class PixelTextDensity : MonoBehaviour
{
    [Tooltip("Smallest em, in virtual pixels, this text is drawn at (the canvas default is 12). " +
             "18 puts 36-44 px sentences on a grid of 2 x 2 screen pixels at 1080p instead of 3 x 3.")]
    [Min(1f)]
    public float minPixelsPerEm = 18f;
}
