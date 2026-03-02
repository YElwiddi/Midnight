using UnityEngine;
using System.Collections;

public class BackgroundMusic : MonoBehaviour
{
    [Tooltip("The GameObject with the AudioSource to enable/disable.")]
    public GameObject musicObject;

    [Tooltip("Assign the disclaimer panel. If left empty, music starts immediately.")]
    public DisclaimerScreen disclaimerPanel;

    void Start()
    {
        if (musicObject == null)
            return;

        if (disclaimerPanel != null && !DisclaimerScreen.HasShown)
        {
            musicObject.SetActive(false);
            StartCoroutine(WaitForDisclaimer());
        }
        else
        {
            musicObject.SetActive(true);
        }
    }

    private IEnumerator WaitForDisclaimer()
    {
        yield return new WaitUntil(() => DisclaimerScreen.HasShown);
        musicObject.SetActive(true);
    }
}
