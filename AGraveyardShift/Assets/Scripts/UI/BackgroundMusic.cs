using UnityEngine;
using System.Collections;

public class BackgroundMusic : MonoBehaviour
{
    [Tooltip("The GameObject whose AudioSource plays the menu music.")]
    public GameObject musicObject;

    [Tooltip("Assign the disclaimer panel. If left empty, music starts immediately.")]
    public DisclaimerScreen disclaimerPanel;

    private AudioSource musicSource;

    void Start()
    {
        if (musicObject == null)
            return;

        musicSource = musicObject.GetComponent<AudioSource>();

        // IMPORTANT: control the AudioSource, not the GameObject's active state.
        // musicObject is the same GameObject this script lives on, so deactivating it
        // would also kill this script (and the coroutine below), leaving the music
        // permanently off. Stopping/playing the AudioSource keeps the object alive.
        if (disclaimerPanel != null && !DisclaimerScreen.HasShown)
        {
            SetPlaying(false);
            StartCoroutine(WaitForDisclaimer());
        }
        else
        {
            SetPlaying(true);
        }
    }

    private IEnumerator WaitForDisclaimer()
    {
        yield return new WaitUntil(() => DisclaimerScreen.HasShown);
        SetPlaying(true);
    }

    private void SetPlaying(bool play)
    {
        if (musicSource == null)
            return;

        if (play)
        {
            if (!musicSource.isPlaying)
                musicSource.Play();
        }
        else
        {
            musicSource.Stop();
        }
    }
}
