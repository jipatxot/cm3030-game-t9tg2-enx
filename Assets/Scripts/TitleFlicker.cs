using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class TitleFlicker : MonoBehaviour
{
    [Header("Base Alpha Range")]
    [SerializeField, Range(0f, 1f)] private float steadyMinAlpha = 0.85f;
    [SerializeField, Range(0f, 1f)] private float steadyMaxAlpha = 1.00f;

    [Header("Bulb Flicker Dips")]
    [SerializeField, Range(0f, 1f)] private float dipMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float dipMaxAlpha = 0.65f;

    [Header("Timing")]
    [Tooltip("Normal interval between flicker events (seconds).")]
    [SerializeField] private Vector2 idleInterval = new Vector2(0.35f, 1.40f);

    [Tooltip("Time between steps inside a flicker burst (seconds).")]
    [SerializeField] private Vector2 burstStepInterval = new Vector2(0.015f, 0.08f);

    [Tooltip("How many steps happen inside a burst.")]
    [SerializeField] private Vector2Int burstSteps = new Vector2Int(3, 12);

    [Header("Burst Chances")]
    [Tooltip("Chance that the next event is a burst vs a single flicker.")]
    [SerializeField, Range(0f, 1f)] private float burstChance = 0.75f;

    [Tooltip("Inside a burst, chance that a step is a deep dip (lightbulb 'cut').")]
    [SerializeField, Range(0f, 1f)] private float deepDipChance = 0.35f;

    [Header("Smoothing (set low for 'hard' flicker)")]
    [SerializeField] private float alphaSnapSpeed = 60f; // higher = snappier

    [Header("Optional micro scale jitter")]
    [SerializeField] private bool jitterScale = true;
    [SerializeField] private float jitterAmount = 0.008f; // ~0.8%
    [SerializeField] private float jitterLerp = 18f;

    private TMP_Text tmp;
    private Color baseColor;
    private float targetAlpha;

    private float nextEventTime;
    private bool inBurst;
    private int remainingBurstSteps;
    private float nextBurstStepTime;

    private Vector3 baseScale;

    private void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        baseColor = tmp.color;
        baseScale = transform.localScale;

        targetAlpha = baseColor.a;
        ScheduleNextEvent();
    }

    private void OnEnable()
    {
        if (tmp == null) tmp = GetComponent<TMP_Text>();
        baseColor = tmp.color;
        baseScale = transform.localScale;

        targetAlpha = baseColor.a;
        inBurst = false;
        remainingBurstSteps = 0;
        ScheduleNextEvent();
    }

    private void Update()
    {
        float t = Time.unscaledTime; // menu should flicker even if timescale changes

        // Drive events
        if (!inBurst)
        {
            if (t >= nextEventTime)
            {
                if (Random.value < burstChance)
                {
                    StartBurst();
                }
                else
                {
                    SingleFlicker();
                    ScheduleNextEvent();
                }
            }
        }
        else
        {
            if (t >= nextBurstStepTime)
            {
                BurstStep();
                remainingBurstSteps--;

                if (remainingBurstSteps <= 0)
                {
                    inBurst = false;
                    // return to steady after a burst
                    targetAlpha = Random.Range(steadyMinAlpha, steadyMaxAlpha);
                    ScheduleNextEvent();
                }
                else
                {
                    nextBurstStepTime = t + Random.Range(burstStepInterval.x, burstStepInterval.y);
                }
            }
        }

        // Apply alpha (snappy, but not fully instant unless you want it)
        Color c = tmp.color;
        float snapped = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * alphaSnapSpeed);
        tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, snapped);

        // Optional micro jitter so it feels “electrical”
        if (jitterScale)
        {
            // jitter is stronger when alpha is lower
            float strength = Mathf.InverseLerp(steadyMaxAlpha, dipMinAlpha, targetAlpha);
            float s = 1f + (Random.Range(-jitterAmount, jitterAmount) * strength);
            Vector3 targetScale = baseScale * s;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * jitterLerp);
        }
    }

    private void ScheduleNextEvent()
    {
        nextEventTime = Time.unscaledTime + Random.Range(idleInterval.x, idleInterval.y);
    }

    private void StartBurst()
    {
        inBurst = true;
        remainingBurstSteps = Random.Range(burstSteps.x, burstSteps.y + 1);
        nextBurstStepTime = Time.unscaledTime + Random.Range(0.01f, 0.08f);
    }

    private void SingleFlicker()
    {
        // A quick dip then recover.
        targetAlpha = Random.Range(dipMaxAlpha, steadyMinAlpha);
    }

    private void BurstStep()
    {
        // Mix of steady-ish and dip steps, with occasional deep dips.
        bool deep = Random.value < deepDipChance;

        if (deep)
        {
            targetAlpha = Random.Range(dipMinAlpha, dipMaxAlpha);
        }
        else
        {
            // sometimes it spikes bright too
            float r = Random.value;
            if (r < 0.55f)
                targetAlpha = Random.Range(dipMaxAlpha, steadyMinAlpha);
            else
                targetAlpha = Random.Range(steadyMinAlpha, steadyMaxAlpha);
        }
    }
}