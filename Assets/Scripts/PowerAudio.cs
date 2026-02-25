using UnityEngine;

// To be attached to light prefabs
// Methods for the power system to trigger audio on state changes

// To connect this to the power system, add the following calls to LightPowerDecay.cs:
//
// In Update(), after CurrentPower is set to 0:
//     GetComponent<PowerAudio>()?.OnPowerLost();
//
// In RestoreToFull(), after CurrentPower is set:
//     GetComponent<PowerAudio>()?.OnPowerRestored();
//
// In AddPower(), after CurrentPower is set:
//     GetComponent<PowerAudio>()?.OnPowerRestored();
public class PowerAudio : MonoBehaviour
{
    // Called when the building loses power
    public void OnPowerLost()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.PlayEffectAt(AudioManager.Sound.PowerLost, transform.position);
    }

    // Called when the player restores the power
    public void OnPowerRestored()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.PlayEffectAt(AudioManager.Sound.PowerRestored, transform.position);
    }
}