using UnityEngine;

// To be attached to the enemy prefab
// Plays the enemy growl sound from the enemy's position as soon as it appears in the scene

// Add wherever the enemy starts chasing the player:
// GetComponent<EnemySound>()?.OnStartChasing();
public class EnemySound : MonoBehaviour
{
    public void OnStartChasing()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.PlayEffectAt(AudioManager.Sound.EnemyChasing, transform.position);
    }
}