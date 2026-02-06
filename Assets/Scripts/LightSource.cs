using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> ActiveSources = new List<LightSource>();

    [Header("Type")]
    public LightSourceType sourceType = LightSourceType.Building;

    [Header("Power")]
    public bool startLit = true;
    public float minSecondsToBlackout = 25f;
    public float maxSecondsToBlackout = 65f;

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

        ResetDecayRate();
        SetLit(startLit, false);
    }

    void OnDisable()
    {
        ActiveSources.Remove(this);
    }

    void Update()
    {
        if (isLit)
        {
            power01 -= Time.deltaTime * decayPerSecond;
            if (power01 <= 0f)
                SetLit(false, false);
        }
        else
        {
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, transform.position);
                if (dist <= restoreRadius)
                    RestoreFromPlayer();
            }
        }
    }

    public void SetLit(bool lit, bool triggeredByPlayer)
    {
        isLit = lit;
        power01 = lit ? 1f : 0f;

        if (lit)
            ResetDecayRate();

        if (controlledLights != null)
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

        if (lit && triggeredByPlayer)
        {
            var health = PlayerHealth.FindPlayerHealth();
            if (health != null) health.RestoreHealth(healthRestoreAmount);
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
