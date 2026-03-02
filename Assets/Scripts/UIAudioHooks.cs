using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Automatically adds click sounds to all UI elements in the scene.
// Attached to AudioManager object
public class UIAudioHooks : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null; // wait one frame for everything to initialise

        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            b.onClick.AddListener(() => AudioManager.instance?.PlayEffect(AudioManager.Sound.UIClick));

        foreach (var t in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            t.onValueChanged.AddListener(isOn => { if (isOn) AudioManager.instance?.PlayEffect(AudioManager.Sound.UIClick); });

        foreach (var d in FindObjectsByType<Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            d.onValueChanged.AddListener(_ => AudioManager.instance?.PlayEffect(AudioManager.Sound.UIClick));

        foreach (var d in FindObjectsByType<TMPro.TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            d.onValueChanged.AddListener(_ => AudioManager.instance?.PlayEffect(AudioManager.Sound.UIClick));
    }
}