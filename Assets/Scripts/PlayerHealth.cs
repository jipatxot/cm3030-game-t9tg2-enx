using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 10f;
    public float currentHealth = 10f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float> OnHealthDelta;
    public event Action OnPlayerDied;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        float delta = currentHealth - before; // negative

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthDelta?.Invoke(delta);

        if (currentHealth <= 0f)
            OnPlayerDied?.Invoke();
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float delta = currentHealth - before; // positive

        if (delta <= 0f) return;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthDelta?.Invoke(delta);
    }

    public static PlayerHealth FindPlayerHealth()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) return ph;

            ph = player.GetComponentInChildren<PlayerHealth>(true);
            if (ph != null) return ph;
        }

#if UNITY_2023_1_OR_NEWER
        var all = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all != null && all.Length > 0) return all[0];
#else
        var all = FindObjectsOfType<PlayerHealth>(true);
        if (all != null && all.Length > 0) return all[0];
#endif

        return null;
    }
}
