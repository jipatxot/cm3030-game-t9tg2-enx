using TMPro;
using UnityEngine;

public class HealthUIController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;
    public TextMeshProUGUI healthText;
    public FloatingTextSpawner floatingTextSpawner;

    [Header("Binding")]
    public float rebindInterval = 0.25f;

    [Header("Colors")]
    public Color damageColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color healColor = new Color(0.2f, 0.9f, 0.2f, 1f);

    float nextRebindTime;

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = PlayerHealth.FindPlayerHealth();
    }

    void OnEnable()
    {
        BindPlayer();
    }

    void OnDisable()
    {
        UnbindPlayer();
    }

    void Update()
    {
        if (playerHealth != null) return;
        if (Time.unscaledTime < nextRebindTime) return;

        nextRebindTime = Time.unscaledTime + Mathf.Max(0.05f, rebindInterval);
        BindPlayer();
    }

    void BindPlayer()
    {
        if (playerHealth == null)
            playerHealth = PlayerHealth.FindPlayerHealth();

        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        playerHealth.OnHealthDelta += HandleHealthDelta;

        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    void UnbindPlayer()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth.OnHealthDelta -= HandleHealthDelta;
    }

    void HandleHealthChanged(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"HP: {current}/{max}";
    }

    void HandleHealthDelta(int delta)
    {
        if (floatingTextSpawner == null) return;

        if (delta < 0)
            floatingTextSpawner.SpawnText($"{delta}", damageColor);
        else if (delta > 0)
            floatingTextSpawner.SpawnText($"+{delta}", healColor);
    }
}
