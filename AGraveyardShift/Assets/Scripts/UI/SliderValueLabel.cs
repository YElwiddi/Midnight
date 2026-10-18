using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a slider's value next to it ("75%" for volumes, "2.0" for sensitivity).</summary>
public class SliderValueLabel : MonoBehaviour
{
    public Slider slider;
    public TMP_Text label;
    [Tooltip("Show value x 100 with a % sign (0..1 sliders).")]
    public bool percent = true;

    private void OnEnable()
    {
        if (slider != null) slider.onValueChanged.AddListener(Show);
        Show(slider != null ? slider.value : 0f);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Show);
    }

    private void LateUpdate()
    {
        // Values loaded with SetValueWithoutNotify don't raise onValueChanged.
        if (slider != null) Show(slider.value);
    }

    private void Show(float v)
    {
        if (label == null) return;
        string s = percent ? Mathf.RoundToInt(v * 100f) + "%" : v.ToString("0.0");
        if (label.text != s) label.text = s;
    }
}
