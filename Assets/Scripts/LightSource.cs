using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> ActiveSources = new List<LightSource>();

    [Header("Type")]
    public LightSourceType sourceType = LightSourceType.Building;

    [Header("Power (Legacy / Non-PowerDecay)")]
    public bool startLit = true;
    public float minSecondsToBlackout = 25f;
    public float maxSecondsToBlackout = 65f;

    [Header("Integration (PowerDecay)")]
    [Tooltip("If this GameObject also has LightPowerDecay, let LightPowerDecay control light brightness/enable state.")]
    public bool deferToPowerDecayIfPresent = true;

    [Tooltip("If LightPowerDecay.CurrentPower <= this, we consider the source unlit (safe zone off).")]
    public float powerDecayOffThreshold = 0.001f;

    [Header("Proximity")]
    public float restoreRadius = 2.5f;
    public float safeRadius = 3f;

    [Header("Health")]
    public int healthRestoreAmount = 2;

    [Header("Auto find targets")]
    public bool autoFindLights = true;
    public bool autoFindLitVolumes = true;

    [Header("Targets")]
    public Light[] controlledLights;
    public Behaviour[] litBehaviours;
    public GameObject[] litVisuals;

    float decayPerSecond;
    float power01;
    bool isLit;
    Transform playerTransform;

    // PowerDecay integration
    LightPowerDecay powerDecay;
    bool usingPowerDecay;

    public enum LightSourceType
    {
        Building,
        LampPost
    }

    public bool IsLit => isLit;

    void OnEnable()
    {
        if (!ActiveSources.Contains(this))
            ActiveSources.Add(this);

        // Cache PowerDecay component (if any)
        powerDecay = GetComponent<LightPowerDecay>();
        usingPowerDecay = deferToPowerDecayIfPresent && powerDecay != null;

        if (autoFindLights && (controlledLights == null || controlledLights.Length == 0))
            controlledLights = GetComponentsInChildren<Light>(true);

        if (autoFindLitVolumes && (litBehaviours == null || litBehaviours.Length == 0))
        {
            var volumes = GetComponentsInChildren<NavMeshModifierVolume>(true);
            var list = new List<Behaviour>(volumes.Length);
            for (int i = 0; i < volumes.Length; i++)
                list.Add(volumes[i]);
            litBehaviours = list.ToArray();
        }

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (usingPowerDecay)
        {
            // Subscribe so we sync as soon as LightPowerDecay initializes in Start()
            LightPowerDecay.OnAnyPowerChanged += OnAnyPowerChanged;
            // Don't run legacy blackout timer; state will mirror powerDecay.
            SyncFromPowerDecay(forceApply: true);
        }
        else
        {
            ResetDecayRate();
            SetLit(startLit, false);
        }
    }

    void OnDisable()
    {
        ActiveSources.Remove(this);

        if (usingPowerDecay)
            LightPowerDecay.OnAnyPowerChanged -= OnAnyPowerChanged;
    }

    void Update()
    {
        if (usingPowerDecay)
        {
            // Mirror lit state from power decay; no sudden off from legacy timer.
            SyncFromPowerDecay(forceApply: false);

            // If unlit, allow proximity restore (this will restore power via SetLit(true,...))
            if (!isLit)
            {
                EnsurePlayerTransform();
                if (playerTransform != null)
                {
                    float dist = Vector3.Distance(playerTransform.position, transform.position);
                    if (dist <= restoreRadius)
                        RestoreFromPlayer();
                }
            }

            return;
        }

        // ---- Legacy behavior (no LightPowerDecay attached) ----
        if (isLit)
        {
            power01 -= Time.deltaTime * decayPerSecond;
            if (power01 <= 0f)
                SetLit(false, false);
        }
        else
        {
            EnsurePlayerTransform();

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, transform.position);
                if (dist <= restoreRadius)
                    RestoreFromPlayer();
            }
        }
    }

    void EnsurePlayerTransform()
    {
        if (playerTransform != null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    // Called for ALL LightPowerDecay objects; filter to ours.
    void OnAnyPowerChanged(LightPowerDecay item, float currentPower, float normalized01)
    {
        if (!usingPowerDecay) return;
        if (item == null || item != powerDecay) return;

        SyncFromPowerDecay(forceApply: true);
    }

    void SyncFromPowerDecay(bool forceApply)
    {
        if (!usingPowerDecay || powerDecay == null) return;

        bool litNow = powerDecay.CurrentPower > powerDecayOffThreshold;

        if (forceApply || litNow != isLit)
        {
            isLit = litNow;
            power01 = litNow ? 1f : 0f;

            // IMPORTANT (Scheme A):
            // - Do NOT toggle controlledLights here; LightPowerDecay owns the Light.enabled/intensity behavior.
            // - We still toggle litBehaviours/litVisuals so safe zones match "lit" state.
            ApplyOutputs(litNow, controlLights: false);
        }
    }

    public void SetLit(bool lit, bool triggeredByPlayer)
    {
        if (usingPowerDecay && powerDecay != null)
        {
            // Scheme A: delegate *visual* control to LightPowerDecay.
            // When player restores, refill power; when turning off, only affects safe-zone visuals/volumes.
            if (lit)
                powerDecay.RestoreToFull();

            isLit = lit;
            power01 = lit ? 1f : 0f;

            ApplyOutputs(lit, controlLights: false);

            if (lit && triggeredByPlayer)
            {
                var health = PlayerHealth.FindPlayerHealth();
                if (health != null) health.RestoreHealth(healthRestoreAmount);
            }

            return;
        }

        // ---- Legacy behavior ----
        isLit = lit;
        power01 = lit ? 1f : 0f;

        if (lit)
            ResetDecayRate();

        ApplyOutputs(lit, controlLights: true);

        if (lit && triggeredByPlayer)
        {
            var health = PlayerHealth.FindPlayerHealth();
            if (health != null) health.RestoreHealth(healthRestoreAmount);
        }
    }

    void ApplyOutputs(bool lit, bool controlLights)
    {
        if (controlLights && controlledLights != null)
        {
            for (int i = 0; i < controlledLights.Length; i++)
                if (controlledLights[i] != null) controlledLights[i].enabled = lit;
        }

        if (litBehaviours != null)
        {
            for (int i = 0; i < litBehaviours.Length; i++)
                if (litBehaviours[i] != null) litBehaviours[i].enabled = lit;
        }

        if (litVisuals != null)
        {
            for (int i = 0; i < litVisuals.Length; i++)
                if (litVisuals[i] != null) litVisuals[i].SetActive(lit);
        }
    }

    void RestoreFromPlayer()
    {
        if (isLit) return;
        SetLit(true, true);
    }

    void ResetDecayRate()
    {
        float min = Mathf.Max(0.1f, minSecondsToBlackout);
        float max = Mathf.Max(min, maxSecondsToBlackout);
        float seconds = Random.Range(min, max);
        decayPerSecond = 1f / Mathf.Max(0.01f, seconds);
    }

    public bool IsPositionSafe(Vector3 position)
    {
        if (!isLit) return false;
        return Vector3.Distance(position, transform.position) <= safeRadius;
    }

    public static bool IsPositionInAnySafeZone(Vector3 position)
    {
        for (int i = 0; i < ActiveSources.Count; i++)
        {
            var source = ActiveSources[i];
            if (source == null) continue;
            if (source.IsPositionSafe(position)) return true;
        }

        return false;
    }
}
