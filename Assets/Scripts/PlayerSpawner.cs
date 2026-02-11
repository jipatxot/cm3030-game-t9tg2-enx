using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Refs")]
    public RoadPathGenerator roads;
    public Transform playerRoot;
    public GameObject playerPrefab;

    [Header("Spawn")]
    public float spawnYOffset = 0.1f;
    public bool respawnOnRegenerate = true;
    public string walkableAreaName = "Walkable";

    [Header("Simple Player Visual")]
    public Color bodyColor = new Color(0.1f, 0.7f, 0.2f, 1f);
    public Color headColor = new Color(0.95f, 0.8f, 0.7f, 1f);

    Coroutine spawnRoutine;

    void OnEnable()
    {
        if (roads != null) roads.OnGenerated += HandleRoadsGenerated;
    }

    void OnDisable()
    {
        if (roads != null) roads.OnGenerated -= HandleRoadsGenerated;
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
    }

    void Start()
    {
        spawnRoutine = StartCoroutine(SpawnWhenReady());
    }

    void HandleRoadsGenerated()
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnWhenReady());
    }

    IEnumerator SpawnWhenReady()
    {
        while (roads == null || roads.Map == null)
            yield return null;

        yield return null;
        yield return null;

        SpawnOrMovePlayer();
    }

    void SpawnOrMovePlayer()
    {
        Vector3 center = GetCityCenter();

        var existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null && !respawnOnRegenerate)
        {
            existing.transform.position = center;
            return;
        }

        if (existing != null && respawnOnRegenerate)
            Destroy(existing);

        var player = playerPrefab != null
            ? Instantiate(playerPrefab, center, Quaternion.identity, playerRoot)
            : BuildSimplePlayer(center);

        player.tag = "Player";

        EnsurePlayerHealthUi(player);
        EnsurePlayerWorldHealthBar(player);
    }

    Vector3 GetCityCenter()
    {
        int cx = Mathf.Clamp(roads.Width / 2, 0, roads.Width - 1);
        int cy = Mathf.Clamp(roads.Height / 2, 0, roads.Height - 1);

        Vector3 basePos = roads.GridToWorld(cx, cy);
        basePos.y += spawnYOffset;

        int area = NavMesh.GetAreaFromName(walkableAreaName);
        int mask = area >= 0 ? 1 << area : NavMesh.AllAreas;

        if (NavMesh.SamplePosition(basePos, out NavMeshHit hit, 4f, mask))
            return hit.position + Vector3.up * spawnYOffset;

        return basePos;
    }


    void EnsurePlayerHealthUi(GameObject player)
    {
        if (player == null) return;

        var ui = FindFirstObjectByType<HealthUIController>();
        if (ui == null)
        {
            var uiGo = new GameObject("Health UI Controller");
            ui = uiGo.AddComponent<HealthUIController>();
        }

        var health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        ui.playerHealth = health;
        ui.autoCreateHudIfMissing = true;
        ui.autoAttachWorldHealthBar = true;
    }

    void EnsurePlayerWorldHealthBar(GameObject player)
    {
        if (player == null) return;

        var health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        var worldBar = player.GetComponent<PlayerWorldHealthBar>();
        if (worldBar == null)
            worldBar = player.AddComponent<PlayerWorldHealthBar>();

        worldBar.playerHealth = health;
    }
    GameObject BuildSimplePlayer(Vector3 position)
    {
        if (playerRoot == null) playerRoot = transform;

        var root = new GameObject("Player");
        root.transform.SetParent(playerRoot, false);
        root.transform.position = position;

        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        root.AddComponent<PlayerMovement>();
        if (root.GetComponent<PlayerHealth>() == null)
            root.AddComponent<PlayerHealth>();

        var visualRoot = new GameObject("Visuals");
        visualRoot.transform.SetParent(root.transform, false);
        visualRoot.transform.localPosition = Vector3.zero;

        Material bodyMat = BuildMaterial(bodyColor);
        Material headMat = BuildMaterial(headColor);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visualRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        ApplyMaterial(body, bodyMat);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visualRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        head.transform.localScale = Vector3.one * 0.45f;
        ApplyMaterial(head, headMat);

        var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftArm.name = "ArmLeft";
        leftArm.transform.SetParent(visualRoot.transform, false);
        leftArm.transform.localPosition = new Vector3(-0.45f, 1.15f, 0f);
        leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        leftArm.transform.localScale = new Vector3(0.15f, 0.35f, 0.15f);
        ApplyMaterial(leftArm, bodyMat);

        var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightArm.name = "ArmRight";
        rightArm.transform.SetParent(visualRoot.transform, false);
        rightArm.transform.localPosition = new Vector3(0.45f, 1.15f, 0f);
        rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        rightArm.transform.localScale = new Vector3(0.15f, 0.35f, 0.15f);
        ApplyMaterial(rightArm, bodyMat);

        RemoveColliders(body, head, leftArm, rightArm);

        return root;
    }

    Material BuildMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        return material;
    }

    void ApplyMaterial(GameObject target, Material material)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    void RemoveColliders(params GameObject[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null) continue;
            var col = objects[i].GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}
