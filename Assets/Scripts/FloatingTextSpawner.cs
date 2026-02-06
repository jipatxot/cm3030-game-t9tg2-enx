using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingTextSpawner : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform spawnRoot;
    public TextMeshProUGUI floatingTextPrefab;

    [Header("Animation")]
    public float floatDistance = 40f;
    public float duration = 1.1f;
    public Vector2 startOffset = new Vector2(10f, -10f);

    public void SpawnText(string message, Color color)
    {
        var text = CreateInstance();
        if (text == null) return;

        text.text = message;
        text.color = color;

        StartCoroutine(AnimateText(text));
    }

    TextMeshProUGUI CreateInstance()
    {
        if (floatingTextPrefab != null)
        {
            var instance = Instantiate(floatingTextPrefab, GetSpawnRoot());
            instance.gameObject.SetActive(true);
            return instance;
        }

        var go = new GameObject("FloatingText");
        go.transform.SetParent(GetSpawnRoot(), false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;
        return text;
    }

    RectTransform GetSpawnRoot()
    {
        if (spawnRoot != null) return spawnRoot;

        if (TryGetComponent(out RectTransform rect))
            spawnRoot = rect;
        else
            spawnRoot = transform as RectTransform;

        return spawnRoot;
    }

    IEnumerator AnimateText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        RectTransform rect = text.rectTransform;
        Vector2 start = startOffset;
        Vector2 end = startOffset + Vector2.up * floatDistance;
        float elapsed = 0f;

        rect.anchoredPosition = start;

        while (elapsed < duration)
        {
            if (text == null) yield break;

            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(start, end, t);
            text.alpha = 1f - t;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (text != null)
            Destroy(text.gameObject);
    }
}
