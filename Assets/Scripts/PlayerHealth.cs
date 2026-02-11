using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 10;
    public int currentHealth = 10;

    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnHealthDelta;
    public event Action OnPlayerDied;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;
        if (currentHealth <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        int delta = currentHealth - before;

        if (delta == 0) return;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthDelta?.Invoke(delta);

        if (currentHealth <= 0)
            OnPlayerDied?.Invoke();
    }

    public void RestoreHealth(int amount)
    {
        if (amount <= 0) return;
        if (currentHealth <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        int delta = currentHealth - before;

        if (delta <= 0) return;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthDelta?.Invoke(delta);
    }

    public static PlayerHealth FindPlayerHealth()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.GetComponent<PlayerHealth>();

        return FindFirstObjectByType<PlayerHealth>();
    }
}
