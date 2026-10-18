using System;
using UnityEngine;

/// <summary>
/// Keeps this character off the CCTV gate feeds: its renderers switch off for each feed camera's
/// render and straight back on for every other camera (built-in pipeline per-camera callbacks).
/// Built for Maribel, the sister who lies — the cabin TV shows an empty gate while she stands at
/// it ("The gate camera has never once recorded her").
/// </summary>
public class HiddenFromCCTV : MonoBehaviour
{
    private Renderer[] renderers;
    private bool[] wasEnabled;
    private bool hidden;
    private CCTVMonitor monitor;
    private bool searchedForMonitor;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        wasEnabled = new bool[renderers.Length];
    }

    private void OnEnable()
    {
        Camera.onPreCull += HandlePreCull;
        Camera.onPostRender += HandlePostRender;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;
        Camera.onPostRender -= HandlePostRender;
        Restore();
    }

    private void HandlePreCull(Camera cam)
    {
        if (hidden || !IsFeedCamera(cam)) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            wasEnabled[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
        hidden = true;
    }

    private void HandlePostRender(Camera cam)
    {
        if (hidden && IsFeedCamera(cam)) Restore();
    }

    private void Restore()
    {
        if (!hidden) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = wasEnabled[i];
        }
        hidden = false;
    }

    private bool IsFeedCamera(Camera cam)
    {
        if (cam == null) return false;
        if (monitor == null)
        {
            // Look once per lifetime; these callbacks run for every camera every frame
            if (searchedForMonitor) return false;
            searchedForMonitor = true;
            monitor = FindFirstObjectByType<CCTVMonitor>();
            if (monitor == null) return false;
        }
        return monitor.feedCameras != null && Array.IndexOf(monitor.feedCameras, cam) >= 0;
    }
}
