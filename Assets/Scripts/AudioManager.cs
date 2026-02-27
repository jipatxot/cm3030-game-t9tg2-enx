using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------
// The following sounds need to be hooked up in other scripts:
//
// PlayerDamage — add wherever the player takes damage:
//     AudioManager.instance?.PlayEffect(AudioManager.Sound.PlayerDamage);
//
// Death — add wherever player death is handled:
//     AudioManager.instance?.PlayEffect(AudioManager.Sound.Death);
//
// Sunrise — add wherever the session end / win condition is triggered:
//     AudioManager.instance?.PlayEffect(AudioManager.Sound.Sunrise);
//
// HealthPickup — add wherever a pickup is collected:
//     AudioManager.instance?.PlayEffect(AudioManager.Sound.PickupItem);
//
// UIClick — add wherever UI button presses are handled:
//     AudioManager.instance?.PlayEffect(AudioManager.Sound.UIClick);
// ---------------------------------------------------------------

// Ensures this GameObject always has an AudioSource component attached
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    // Defines the available SFX events that can be used in other scripts
    // without the need to refer to the file names
    public enum Sound
    {
        PowerRestored,
        EnemyChasing,
        PlayerDamage,
        Sunrise,
        Death,
        UIClick,
        PowerLost,
        HealthPickup
    }

    // One shared instance accessible from any script via AudioManager.instance
    public static AudioManager instance;

    // Maps a sound event with audio asset and volume setting
    // that can be set up as a list in the Inspector
    [Serializable]
    public struct GameSound
    {
        public Sound soundName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    public List<GameSound> library;

    private AudioSource sfxSource;

// Stores a background audio clip and its volume for ambient and music tracks
    [Serializable]
    public struct BackgroundSound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Ambient")] public BackgroundSound ambient;
    private AudioSource ambientSource;

    [Header("Music")] public BackgroundSound calmMusic;
    public BackgroundSound intenseMusic;
    private AudioSource musicSource;

    private void Awake()
    {
        // Keep only one AudioManager alive
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        sfxSource = GetComponent<AudioSource>();

        // Ambient runs on its own AudioSource,
        // so its volume can be controlled independently of SFX
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.volume = ambient.volume;
        if (ambient.clip != null)
        {
            ambientSource.clip = ambient.clip;
            ambientSource.Play();
        }
    }

    // Finds and plays the audio clip in the library
    public void PlayEffect(Sound soundName)
    {
        var s = library.Find(x => x.soundName == soundName);

        // Play if the audio clip exists
        if (s.clip != null)
        {
            sfxSource.PlayOneShot(s.clip, s.volume);
            Debug.Log($"AUDIO PLAYING: {soundName}");
        }
        else
        {
            // If the audio file is missing, log to the console
            Debug.LogWarning($"AUDIO TRIGGERED (ASSET MISSING): {soundName}");
        }
    }

    // Plays a sound at a specific position in the world
    public void PlayEffectAt(Sound soundName, Vector3 worldPosition)
    {
        var s = library.Find(x => x.soundName == soundName);
        if (s.clip != null)
        {
            // Creates a temporary object at worldPosition, plays the clip, then destroys itself
            AudioSource.PlayClipAtPoint(s.clip, worldPosition, s.volume);
            Debug.Log($"AUDIO PLAYING: {soundName} at {worldPosition.ToString("F2")}");
        }
        else
        {
            // If the audio file is missing, log to the console
            Debug.LogWarning($"AUDIO TRIGGERED (ASSET MISSING): {soundName}");
        }
    }
}