#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Gives MainMixer the Music and SFX groups (under Master) with exposed "MusicVolume" and
/// "SFXVolume" parameters, and moves the mixer into Resources/Audio so AudioRouter can load it.
/// The mixer editing API is internal, so this goes through reflection; it is idempotent.
/// Run via Tools > Graveyard > Menu > Set Up Audio Mixer.
/// </summary>
public static class AudioMixerSetup
{
    const string OldPath = "Assets/MainMixer.mixer";
    const string NewPath = "Assets/Resources/Audio/MainMixer.mixer";

    [MenuItem("Tools/Graveyard/Menu/Set Up Audio Mixer")]
    public static string Run()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(OldPath) != null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio")) AssetDatabase.CreateFolder("Assets/Resources", "Audio");
            string err = AssetDatabase.MoveAsset(OldPath, NewPath);
            if (!string.IsNullOrEmpty(err)) return "move failed: " + err;
        }
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(NewPath);
        if (mixer == null) return "no mixer at " + NewPath;

        var editorAsm = typeof(Editor).Assembly;
        Type controllerType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
        Type groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
        Type pathType = editorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        Type exposedType = editorAsm.GetType("UnityEditor.Audio.ExposedAudioParameter");
        if (controllerType == null || groupType == null || pathType == null || exposedType == null)
            return "mixer API not found";

        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object master = controllerType.GetProperty("masterGroup", F).GetValue(mixer);
        var log = new System.Text.StringBuilder();

        foreach (var spec in new[] { new[] { "Music", "MusicVolume" }, new[] { "SFX", "SFXVolume" } })
        {
            string groupName = spec[0], paramName = spec[1];
            object group = mixer.FindMatchingGroups(groupName).FirstOrDefault(g => g.name == groupName);
            if (group == null)
            {
                group = controllerType.GetMethod("CreateNewGroup", F).Invoke(mixer, new object[] { groupName, false });
                controllerType.GetMethod("AddChildToParent", F).Invoke(mixer, new[] { group, master });
                var addToView = controllerType.GetMethod("AddGroupToCurrentView", F);
                if (addToView != null) addToView.Invoke(mixer, new[] { group });
                log.Append("created group " + groupName + "; ");
            }

            // Already exposed?
            float probe;
            if (mixer.GetFloat(paramName, out probe)) { log.Append(paramName + " already exposed; "); continue; }

            object volumeGuid = groupType.GetMethod("GetGUIDForVolume", F).Invoke(group, null);
            object path = Activator.CreateInstance(pathType, new[] { group, volumeGuid });
            controllerType.GetMethod("AddExposedParameter", F).Invoke(mixer, new[] { path });

            // Rename the new exposed parameter (it is added with a placeholder name).
            var exposedProp = controllerType.GetProperty("exposedParameters", F);
            var arr = (Array)exposedProp.GetValue(mixer);
            var guidField = exposedType.GetField("guid", F);
            var nameField = exposedType.GetField("name", F);
            for (int i = 0; i < arr.Length; i++)
            {
                object p = arr.GetValue(i);
                if (guidField.GetValue(p).Equals(volumeGuid))
                {
                    nameField.SetValue(p, paramName);
                    arr.SetValue(p, i);
                }
            }
            exposedProp.SetValue(mixer, arr);
            var changed = controllerType.GetMethod("OnChangedExposedParameter", F);
            if (changed != null) changed.Invoke(mixer, null);
            log.Append("exposed " + paramName + "; ");
        }

        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();

        float m, s;
        log.Append("check: MusicVolume=" + mixer.GetFloat("MusicVolume", out m) + " SFXVolume=" + mixer.GetFloat("SFXVolume", out s));
        Debug.Log("AudioMixerSetup: " + log);
        return log.ToString();
    }
}
#endif
