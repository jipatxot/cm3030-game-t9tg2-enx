using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class CityNavMeshBaker : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public NavMeshSurface surface;

    Coroutine bakeCo;

    void OnEnable()
    {
        if (roads != null) roads.OnGenerated += QueueRebuild;
    }

    void OnDisable()
    {
        if (roads != null) roads.OnGenerated -= QueueRebuild;
        if (bakeCo != null) StopCoroutine(bakeCo);
    }

    void QueueRebuild()
    {
        if (bakeCo != null) StopCoroutine(bakeCo);
        bakeCo = StartCoroutine(RebuildAfterSpawners());
    }

    IEnumerator RebuildAfterSpawners()
    {
        // Let all OnGenerated subscribers spawn first
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        if (surface == null) yield break;

        surface.RemoveData();
        surface.BuildNavMesh();
    }
}
