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

    void Awake()
    {
        if (roads == null)
            roads = FindFirstObjectByType<RoadPathGenerator>();

    }

    void Start()
    {
        spawnRoutine = StartCoroutine(SpawnWhenReady());
    }

    void HandleRoadsGenerated()
    {
        SpawnNow();
    }

    public void SpawnNow()
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
        var existing = FindExistingPlayer();

        if (existing != null)
        {
            existing.transform.position = center;
            existing.SetActive(true);
            existing.tag = "Player";

            EnsurePlayerHealth(existing);
            RegisterToUI(existing);

            if (!respawnOnRegenerate)
                return;

            // If this existing player is the configured playerRoot object, reuse it.
            // This supports workflows where users place their character model under playerRoot.
            if (playerRoot != null && existing == playerRoot.gameObject)
                return;

            Destroy(existing);
        }

        // If the inspector root is inactive, parenting will hide the player.
        Transform safeRoot = GetSafeRoot(existing);

        var player = playerPrefab != null
            ? Instantiate(playerPrefab, center, Quaternion.identity, safeRoot)
            : BuildSimplePlayer(center, safeRoot);

        player.tag = "Player";
        EnsurePlayerHealth(player);
        RegisterToUI(player);
    }


    GameObject FindExistingPlayer()
    {
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged;

        if (playerRoot != null && LooksLikePlayerObject(playerRoot.gameObject))
            return playerRoot.gameObject;

        return null;
    }

    void EnsurePlayerHealth(GameObject player)
    {
        if (player == null) return;

        var ph = player.GetComponent<PlayerHealth>();
        if (ph == null) ph = player.GetComponentInChildren<PlayerHealth>(true);
        if (ph == null) player.AddComponent<PlayerHealth>();
    }

    Transform GetSafeRoot(GameObject existingPlayer)
    {
        if (playerRoot == null)
            return null; // no parent

        if (existingPlayer != null && (playerRoot == existingPlayer.transform || playerRoot.IsChildOf(existingPlayer.transform)))
            return null;

        if (!playerRoot.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("PlayerSpawner: playerRoot is inactive in hierarchy. Spawning player without parent so it stays visible.");
            return null;
        }

        return playerRoot;
    }

    bool LooksLikePlayerObject(GameObject go)
    {
        if (go == null) return false;
        if (go.CompareTag("Player")) return true;
        if (go.GetComponent<PlayerMovement>() != null) return true;
        if (go.GetComponent<PlayerHealth>() != null) return true;
        if (go.GetComponent<CharacterController>() != null) return true;
        return false;
    }

    void RegisterToUI(GameObject player)
    {
        var ui = FindFirstObjectByType<GameUIController>();
        if (ui == null) return;

        var ph = player.GetComponent<PlayerHealth>();
        if (ph == null) ph = player.GetComponentInChildren<PlayerHealth>(true);

        ui.RegisterPlayer(ph);
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

    GameObject BuildSimplePlayer(Vector3 position, Transform parent)
    {
        var root = new GameObject("Player");

        if (parent != null)
            root.transform.SetParent(parent, false);

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
