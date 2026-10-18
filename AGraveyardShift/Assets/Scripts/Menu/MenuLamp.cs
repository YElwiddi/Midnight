using System.Collections;
using UnityEngine;

/// <summary>
/// The gate lamp on the menu. Quit puts it out: a few failing flickers, then dark
/// (its LampHalo follows the light by itself).
/// </summary>
public class MenuLamp : MonoBehaviour
{
    public Light lamp;
    public AudioSource audioSource;
    public AudioClip offClip;
    [Range(0f, 1f)] public float offVolume = 0.5f;

    private static readonly float[] Flicker = { 0.35f, 0.9f, 0.18f, 0.7f, 0.06f, 0.4f };

    public IEnumerator PutOut()
    {
        if (lamp == null) yield break;
        float baseIntensity = lamp.intensity;
        foreach (float k in Flicker)
        {
            lamp.intensity = baseIntensity * k;
            yield return new WaitForSecondsRealtime(Random.Range(0.05f, 0.1f));
        }
        lamp.enabled = false;
        if (audioSource != null && offClip != null) audioSource.PlayOneShot(offClip, offVolume);
        yield return new WaitForSecondsRealtime(0.5f);
    }
}
