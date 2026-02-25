using UnityEngine;

// To be attached to the enemy prefab
// Plays the enemy growl sound from the enemy's position as soon as it appears in the scene
// Called by MonsterWander when the enemy starts chasing the player in SmartChase().
public class EnemySound : MonoBehaviour
{
    // Start runs once on the first frame the object exists
    public void OnStartChasing()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.PlayEffectAt(AudioManager.Sound.EnemyChasing, transform.position);
    }
}