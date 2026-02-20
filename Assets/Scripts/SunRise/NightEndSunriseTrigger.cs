using System.Reflection;
using UnityEngine;


/// Triggers SunriseFinaleEffect when the night timer ends, BUT only if the player has NOT lost.
/// 
/// "Winning" in your game is typically "survive until sessionLengthSeconds".
/// This script supports several ways to detect a loss / win without adding any new UI:
/// 
/// (A) Assign loseIndicatorObject (e.g., GameOver panel). If it's active -> block sunrise.
/// (B) Assign winIndicatorObject (e.g., "You survived" panel). If onlyTriggerOnWin is true,
///     sunrise triggers only when this object is active.
/// (C) Assign outcomeProvider + member names (bool field/property) for HasWon / HasLost.
/// 
/// If you only assign (A), then sunrise triggers when time ends AND not lost.
/// If you only assign (B), then sunrise triggers when time ends AND win panel is active.
/// If nothing is assigned, sunrise triggers purely on time end (not recommended if you have failure states).
public class NightEndSunriseTrigger : MonoBehaviour
{
    public PowerDecayManager manager;
    public SunriseFinaleEffect sunriseEffect;

    [Tooltip("Trigger when elapsed >= sessionLengthSeconds - offset.")]
    public float triggerOffsetSeconds = 0f;

    [Header("Gate by Outcome")]
    [Tooltip("If true, sunrise will not play if the player has already lost.")]
    public bool blockIfLost = true;

    [Tooltip("If true, sunrise will ONLY play if we detect a 'win' condition.")]
    public bool onlyTriggerOnWin = true;

    [Tooltip("Optional: a GameObject that becomes active when the player wins (e.g., WinPanel root).")]
    public GameObject winIndicatorObject;

    [Tooltip("Optional: a GameObject that becomes active when the player loses (e.g., GameOverPanel root).")]
    public GameObject loseIndicatorObject;

    [Tooltip("Optional: a script that stores bool flags like HasWon / HasLost.")]
    public MonoBehaviour outcomeProvider;

    [Tooltip("Bool field/property name on outcomeProvider that indicates WIN. Leave empty to ignore.")]
    public string winMemberName = "HasWon";

    [Tooltip("Bool field/property name on outcomeProvider that indicates LOSS. Leave empty to ignore.")]
    public string loseMemberName = "HasLost";

    bool _fired;

    void Awake()
    {
        if (manager == null)
            manager = PowerDecayManager.Instance;

        if (sunriseEffect == null)
        {
#if UNITY_2023_1_OR_NEWER
            sunriseEffect = FindFirstObjectByType<SunriseFinaleEffect>(FindObjectsInactive.Include);
#else
            sunriseEffect = FindObjectOfType<SunriseFinaleEffect>(true);
#endif
        }
    }

    void Update()
    {
        if (_fired) return;
        if (manager == null || sunriseEffect == null) return;

        // If player has already lost, block forever
        if (blockIfLost && IsLost())
            return;

        float t = manager.ElapsedSeconds;
        float nightLen = Mathf.Max(0.01f, manager.sessionLengthSeconds);

        if (t < (nightLen - triggerOffsetSeconds))
            return;

        // Timer reached end: decide whether we should play sunrise
        if (onlyTriggerOnWin && !IsWon())
            return;

        _fired = true;
        sunriseEffect.Play();
    }

    bool IsWon()
    {
        // 1) If we have an explicit win indicator object, use it
        if (winIndicatorObject != null)
            return winIndicatorObject.activeInHierarchy;

        // 2) If we have a provider bool, use it
        if (outcomeProvider != null && !string.IsNullOrWhiteSpace(winMemberName))
        {
            if (TryReadBool(outcomeProvider, winMemberName, out bool v))
                return v;
        }

        // 3) Fallback definition of "won": survived AND not lost
        return !IsLost();
    }

    bool IsLost()
    {
        // 1) If lose indicator object is active, lost
        if (loseIndicatorObject != null && loseIndicatorObject.activeInHierarchy)
            return true;

        // 2) If provider bool says lost, lost
        if (outcomeProvider != null && !string.IsNullOrWhiteSpace(loseMemberName))
        {
            if (TryReadBool(outcomeProvider, loseMemberName, out bool v))
                return v;
        }

        // 3) Unknown -> treat as not lost
        return false;
    }

    static bool TryReadBool(MonoBehaviour provider, string memberName, out bool value)
    {
        value = false;
        if (provider == null || string.IsNullOrWhiteSpace(memberName)) return false;

        var t = provider.GetType();

        // Field
        var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(bool))
        {
            value = (bool)f.GetValue(provider);
            return true;
        }

        // Property
        var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(bool) && p.GetIndexParameters().Length == 0)
        {
            value = (bool)p.GetValue(provider, null);
            return true;
        }

        return false;
    }
}
