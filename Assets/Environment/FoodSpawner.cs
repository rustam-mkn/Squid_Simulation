using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject plantFoodPrefab;
    public GameObject meatFoodPrefab;

    [Header("Spawning Settings")]
    public int initialPlantFoodCount = 10;      // было 20
    public float spawnRadius = 30f;
    public float plantSpawnInterval = 30f;      // было 20f — реже
    public int plantsPerInterval = 2;           // было 3 — меньше за раз
    public int maxFoodOnScene = 100;            // 🔥 Ограничение количества еды

    private float plantSpawnTimer;
    private SimulationManager simManager;

    void Start()
    {
        simManager = FindFirstObjectByType<SimulationManager>();
        if (simManager == null) {
            Debug.LogError("FoodSpawner could not find SimulationManager!");
            enabled = false; return;
        }
        plantSpawnTimer = plantSpawnInterval;
    }

    public void SpawnInitialFood() {
        if (plantFoodPrefab == null) {
            Debug.LogError("PlantFoodPrefab not assigned in FoodSpawner!");
            return;
        }
        for (int i = 0; i < initialPlantFoodCount; i++)
        {
            SpawnFoodItem(plantFoodPrefab);
        }
        Debug.Log($"Spawned initial {initialPlantFoodCount} plant food items.");
    }

    void Update()
    {
        if (!simManager.isRunning || Time.timeScale == 0f) return;

        plantSpawnTimer -= Time.deltaTime;
        if (plantSpawnTimer <= 0)
        {
            // 🔍 Проверка: не спавним, если еда превышает лимит
            Food[] currentFoodItems = FindObjectsByType<Food>(FindObjectsSortMode.None);
            if (currentFoodItems.Length >= maxFoodOnScene) {
                plantSpawnTimer = plantSpawnInterval; // сбрасываем таймер без спавна
                return;
            }

            if (plantFoodPrefab != null) {
                for(int i = 0; i < plantsPerInterval; i++)
                {
                    SpawnFoodItem(plantFoodPrefab);
                }
            }
            plantSpawnTimer = plantSpawnInterval;
        }
    }

    void SpawnFoodItem(GameObject prefab)
    {
        Vector3 spawnPos = GetRandomSpawnPositionInBounds();
        Instantiate(prefab, spawnPos, Quaternion.identity, transform);
    }

    public void SpawnFoodAt(GameObject prefab, Vector3 position)
    {
        if (prefab == null) {
            Debug.LogWarning($"Attempted to spawn null food prefab at {position}.");
            return;
        }
        Instantiate(prefab, position, Quaternion.identity, transform);
    }

    Vector3 GetRandomSpawnPositionInBounds()
    {
        WorldBounds wb = FindFirstObjectByType<WorldBounds>();
        if (wb != null) {
            return new Vector3(
                Random.Range(wb.xMin + 0.5f, wb.xMax - 0.5f),
                Random.Range(wb.yMin + 0.5f, wb.yMax - 0.5f),
                0
            );
        }
        Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
        return new Vector3(randomPos.x, randomPos.y, 0);
    }
}
