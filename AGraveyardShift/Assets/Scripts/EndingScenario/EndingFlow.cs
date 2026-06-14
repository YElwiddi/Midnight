using UnityEngine;

/// <summary>
/// Central entry point for "an ending happened". Records the unlock, then shows the
/// black reveal screen (title + flavor), which returns to the main menu afterwards.
///
/// Use Trigger() for the death endings (spirit / grave robber / bride).
/// For the good cinematic ending, the cinematic shows its own EndingScreenUI, so call
/// EndingsSave.Unlock(Ending.WrongfulConviction) directly at that moment instead.
/// </summary>
public static class EndingFlow
{
    public const string MenuScene = "Start Menu Scene";

    public static void Trigger(Ending ending, float revealDuration = 6f)
    {
        EndingsSave.Unlock(ending);

        // Make sure the menu/reveal is usable regardless of gameplay state.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EndingScreenUI ui = Object.FindObjectOfType<EndingScreenUI>();
        if (ui == null)
        {
            GameObject go = new GameObject("EndingScreenUI");
            ui = go.AddComponent<EndingScreenUI>();
        }

        // e.g. "Bad Ending #1\nA Wrathful Spirit", shown instantly, with the per-ending font.
        ui.Show(EndingInfo.RevealText(ending), "", revealDuration, MenuScene, EndingFonts.Get(ending));
    }
}
