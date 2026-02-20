using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float baseMaxHealth = 10f;

    [Tooltip("Runtime max health. Starts at baseMaxHealth.")]
    public float maxHealth = 10f;

    public float currentHealth = 10f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float> OnHealthDelta;
    public event Action OnPlayerDied;

    void Awake()
    {
        baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
        if (maxHealth <= 0f) maxHealth = baseMaxHealth;

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetBaseMaxHealth()
    {
        return Mathf.Max(1f, baseMaxHealth);
    }

    public bool IsBoostedMaxActive()
    {
        return maxHealth > GetBaseMaxHealth() + 0.001f;
    }

    public void SetMaxHealthTemporary(float newMax, bool healToNewMax)
    {
        maxHealth = Mathf.Max(1f, newMax);

        if (healToNewMax)
        {
            float before = currentHealth;
            currentHealth = maxHealth;
            float delta = currentHealth - before;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (Mathf.Abs(delta) > 0.0001f) OnHealthDelta?.Invoke(delta);
            return;
        }

        currentHealth = Mathf.Min(currentHealth, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ResetMaxHealthToBaseAndClamp()
    {
        maxHealth = Mathf.Max(1f, baseMaxHealth);

        if (currentHealth > maxHealth)
        {
            float before = currentHealth;
            currentHealth = maxHealth;
            float delta = currentHealth - before;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (Mathf.Abs(delta) > 0.0001f) OnHealthDelta?.Invoke(delta);
            return;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        float delta = currentHealth - before;

        // If boosted and you drop to base or below, boost ends immediately
        if (IsBoostedMaxActive() && currentHealth <= GetBaseMaxHealth() + 0.0001f)
        {
            ResetMaxHealthToBaseAndClamp();
        }
        else
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        OnHealthDelta?.Invoke(delta);

        if (currentHealth <= 0f)
            OnPlayerDied?.Invoke();
    }

    public void RestoreHealth(float amount)
    {
        // Kept for health packs and any future full-heal sources
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float delta = currentHealth - before;

        if (delta <= 0f) return;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthDelta?.Invoke(delta);
    }

    public void RestoreHealthCapped(float amount, float cap)
    {
        // Used by lamp healing: cap will be baseMaxHealth
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        float limit = Mathf.Max(0f, cap);

        float before = currentHealth;
        currentHealth = Mathf.Min(limit, currentHealth + amount);
        float delta = currentHealth - before;

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
