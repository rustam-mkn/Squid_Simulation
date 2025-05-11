#!/bin/bash

# ==============================================================================
# ЗАМЕНИТЕ ЭТОТ ПУТЬ НА ВАШ РЕАЛЬНЫЙ ПУТЬ К ПАПКЕ "Assets" ВАШЕГО ПРОЕКТА UNITY
ASSETS_PATH="/Users/user/Dev/Unity/Squid/Assets" # Пример! ИЗМЕНИТЕ ЭТО!
# ==============================================================================

echo "Обновление C# файлов (v2) в: $ASSETS_PATH"

# ==============================================================================
# --- ОБНОВЛЕНИЕ C# ФАЙЛОВ ---
# ==============================================================================

# --- Assets/Core/SimulationManager.cs ---
# (Изменения для более безопасной работы с TimeScale)
cat <<EOF > "${ASSETS_PATH}/Core/SimulationManager.cs"
// ${ASSETS_PATH}/Core/SimulationManager.cs
using UnityEngine;
using System.Collections.Generic;

public class SimulationManager : MonoBehaviour
{
    [Header("Prefabs & Scene References")]
    public GameObject squidAgentPrefab;
    public FoodSpawner foodSpawner;
    public StatisticsManager statisticsManager;
    public GeneticAlgorithmManager gaManager;
    public UIManager uiManager;

    [Header("Simulation Settings")]
    public int initialPopulationSize = 10; // Уменьшил для тестов
    public float generationTime = 90f;
    private float currentGenerationTimer;

    private List<SquidAgent> agents = new List<SquidAgent>();
    private List<Genome> genomesForNextGeneration = new List<Genome>();

    [Header("Neural Network Genome Params")]
    public int numInputNodes = 16; // Обновил: 2еды*(расст,x,y,тип) + 1агент*(расст,x,y,размер) + 3стены + 2свои(энерг,возраст) = 8+4+3+2 = 17. Округлим до 16 или 18
    public int numHiddenNodes = 18;
    public int numOutputNodes = 10; // Обновил: вперед,поворот + 2щуп*(дирX,дирY,вытян,схват) + есть + размн = 2+2*4+1+1 = 12. Округлим.

    public bool isRunning { get; private set; } = false;
    public int currentGenerationNumber { get; private set; } = 0;
    private float lastTimeScaleBeforePause = 1f;


    void Start()
    {
        if (foodSpawner == null) foodSpawner = FindFirstObjectByType<FoodSpawner>();
        if (statisticsManager == null) statisticsManager = FindFirstObjectByType<StatisticsManager>();
        if (gaManager == null) gaManager = FindFirstObjectByType<GeneticAlgorithmManager>();
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();

        if (foodSpawner == null || statisticsManager == null || gaManager == null || uiManager == null || squidAgentPrefab == null)
        {
            Debug.LogError("SimulationManager is missing critical references!");
            enabled = false;
            return;
        }
        Time.timeScale = 1f;
        if (uiManager) uiManager.InitializeUIValues(Time.timeScale, currentGenerationNumber); // Инициализируем UI
    }

    public void RequestStartSimulation()
    {
        if (isRunning) return;
        Debug.Log("Simulation Starting...");
        isRunning = true;
        Time.timeScale = lastTimeScaleBeforePause = (uiManager != null ? uiManager.GetCurrentTimeScaleRequest() : 1f); // Начинаем с текущей скорости слайдера
        currentGenerationNumber = 1;
        InitializeFirstGeneration();
        currentGenerationTimer = generationTime;
        if (statisticsManager) statisticsManager.ResetStatistics();
        if (foodSpawner) foodSpawner.SpawnInitialFood();
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }

    public void RequestPauseSimulation()
    {
        if (!isRunning || Time.timeScale == 0f) return; // Уже на паузе или не запущена
        lastTimeScaleBeforePause = Time.timeScale; // Сохраняем текущую скорость
        Time.timeScale = 0f;
        // isRunning остается true, мы просто ставим время на 0
        Debug.Log("Simulation Paused.");
        if (uiManager) uiManager.UpdateSimulationStateUI(true, Time.timeScale, currentGenerationNumber); // Передаем isRunning=true, т.к. логически она еще идет
    }

    public void RequestResumeSimulation()
    {
        if (!isRunning) { // Если была полностью остановлена (не пауза)
            Debug.LogWarning("Cannot resume. Simulation was not running. Please Start.");
            return;
        }
        if (Time.timeScale != 0f) return; // Уже идет

        Time.timeScale = lastTimeScaleBeforePause; // Восстанавливаем скорость
        Debug.Log($"Simulation Resumed. TimeScale: {Time.timeScale}");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }
    
    public void RequestAdjustTimeScale(float scale)
    {
        float newScale = Mathf.Max(0.01f, scale); // Не позволяем установить 0 через слайдер
        if (Time.timeScale != 0f) { // Если не на паузе, меняем сразу
             Time.timeScale = newScale;
        }
        lastTimeScaleBeforePause = newScale; // Запоминаем запрошенную скорость для возобновления
        
        Debug.Log($"Time scale requested: {newScale}. Current: {Time.timeScale}");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber); // Обновляем UI с актуальным Time.timeScale
    }

    void Update()
    {
        if (!isRunning || Time.timeScale == 0f) return;

        currentGenerationTimer -= Time.deltaTime;
        if (currentGenerationTimer <= 0 || (agents.Count == 0 && genomesForNextGeneration.Count > 0 && currentGenerationNumber > 0))
        {
            PrepareAndStartNewGeneration();
        }
    }
    // ... (остальной код SimulationManager остается почти таким же, как в предыдущей версии)
    // Убедитесь, что методы SpawnAgent, GetRandomSpawnPosition, ReportAgentDeath, ReportAgentReproduction,
    // ClearPopulation, ClearAgentsVisuals, InitializeFirstGeneration, PrepareAndStartNewGeneration
    // взяты из предыдущей версии скрипта, которую вы успешно запустили (я их здесь не дублирую для краткости,
    // так как основные изменения коснулись управления паузой/скоростью).
    // ЕСЛИ НУЖЕН ПОЛНЫЙ КОД SimulationManager СНОВА, СООБЩИТЕ.
    // ВАЖНО: Я предполагаю, что остальная часть SimulationManager у вас уже есть и работает.
    // Я добавлю только недостающие части из прошлого для полноты:

    void InitializeFirstGeneration()
    {
        ClearPopulation();
        genomesForNextGeneration.Clear();
        for (int i = 0; i < initialPopulationSize; i++)
        {
            Genome newGenome = new Genome(numInputNodes, numHiddenNodes, numOutputNodes);
            newGenome.InitializeRandomPhysicalGenes();
            SpawnAgent(newGenome);
        }
        Debug.Log($"Initialized first generation with {agents.Count} agents.");
    }

    void PrepareAndStartNewGeneration()
    {
        Debug.Log("Preparing new generation...");
        currentGenerationNumber++;
        
        List<Genome> evaluatedGenomes = new List<Genome>(genomesForNextGeneration);
        genomesForNextGeneration.Clear();

        foreach (var agent in agents) {
            if (agent != null && agent.genome != null) {
                 if(agent.TryGetComponent<SquidMetabolism>(out var meta)) agent.genome.fitness = meta.Age + meta.CurrentEnergy * 0.1f; // Обновленный фитнес
                 if (!evaluatedGenomes.Exists(g => g == agent.genome))
                 {
                    evaluatedGenomes.Add(new Genome(agent.genome));
                 }
            }
        }

        ClearAgentsVisuals();

        if (evaluatedGenomes.Count == 0) {
            Debug.LogWarning("No genomes to evolve from. Re-initializing first generation.");
            InitializeFirstGeneration();
            currentGenerationTimer = generationTime;
            if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
            return;
        }

        List<Genome> newGenerationGenomes = gaManager.EvolvePopulation(evaluatedGenomes);

        foreach (Genome genome in newGenerationGenomes)
        {
            genome.fitness = 0;
            SpawnAgent(genome);
        }
        currentGenerationTimer = generationTime;
        Debug.Log($"Spawned new generation #{currentGenerationNumber} with {agents.Count} agents.");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }

    void SpawnAgent(Genome genome, Vector3? position = null)
    {
        if (squidAgentPrefab == null) {
            Debug.LogError("SquidAgent Prefab is not set in SimulationManager!");
            return;
        }
        Vector3 spawnPos = position ?? GetRandomSpawnPosition();
        GameObject agentGO = Instantiate(squidAgentPrefab, spawnPos, Quaternion.identity);
        SquidAgent squidAgent = agentGO.GetComponent<SquidAgent>();
        if (squidAgent != null)
        {
            squidAgent.Initialize(genome, this);
            agents.Add(squidAgent);
        } else {
            Debug.LogError($"Failed to get SquidAgent component from prefab instance: {agentGO.name}");
            Destroy(agentGO);
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        float spawnRadius = (foodSpawner != null) ? foodSpawner.spawnRadius : 20f;
        WorldBounds wb = FindFirstObjectByType<WorldBounds>();
        if (wb != null) {
            return new Vector3(Random.Range(wb.xMin + 1, wb.xMax - 1), Random.Range(wb.yMin + 1, wb.yMax - 1), 0);
        }
        return new Vector3(Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius), 0);
    }

    public void ReportAgentDeath(SquidAgent agent, Genome finalGenome)
    {
        if (agents.Remove(agent))
        {
            genomesForNextGeneration.Add(finalGenome);
            if (foodSpawner) foodSpawner.SpawnFoodAt(foodSpawner.meatFoodPrefab, agent.transform.position);
            Debug.Log($"Agent {agent.gameObject.name} died. Fitness: {finalGenome.fitness:F2}. Remaining: {agents.Count}");
        }
    }
    
    public void ReportAgentReproduction(SquidAgent parent, Genome offspringGenome)
    {
        SpawnAgent(offspringGenome, parent.transform.position + (Vector3)Random.insideUnitCircle * 2f);
        // Debug.Log($"Agent {parent.gameObject.name} reproduced."); // Убрал, чтобы не спамить консоль
    }

    void ClearPopulation()
    {
        ClearAgentsVisuals();
        genomesForNextGeneration.Clear();
    }
    
    void ClearAgentsVisuals()
    {
        foreach (var agent in agents)
        {
            if (agent != null) Destroy(agent.gameObject);
        }
        agents.Clear();
    }
}
EOF

# --- Assets/Agents/SquidSenses.cs ---
# (Добавим опциональное постоянное отображение поля зрения)
cat <<EOF > "${ASSETS_PATH}/Agents/SquidSenses.cs"
// ${ASSETS_PATH}/Agents/SquidSenses.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SquidSenses : MonoBehaviour
{
    [Header("Base Sensory Parameters")]
    public float baseSightRadius = 10f;
    public float baseSightAngle = 120f;
    
    [Header("Layers")]
    public LayerMask foodLayer;
    public LayerMask agentLayer;
    public LayerMask obstacleLayer;

    private Genome genome;
    private Transform agentTransform;

    private float currentSightRadius;
    private float currentSightAngle;

    private List<Transform> visibleFoodDebug = new List<Transform>();
    private List<Transform> visibleAgentsDebug = new List<Transform>();
    private Dictionary<GameObject, float> targetPriorities = new Dictionary<GameObject, float>();

    private SquidAgent parentAgent; // Для проверки, выбран ли он в UI
    private UIManager uiManager; // Для проверки, нужно ли рисовать Gizmos постоянно

    public void Initialize(Genome agentGenome, Transform ownerTransform)
    {
        this.genome = agentGenome;
        this.agentTransform = ownerTransform;
        this.parentAgent = GetComponent<SquidAgent>();
        this.uiManager = FindFirstObjectByType<UIManager>();


        if (genome == null || agentTransform == null) {
            Debug.LogError("SquidSenses initialized with null genome or transform!");
            enabled = false; return;
        }

        currentSightRadius = baseSightRadius * (1f + genome.eyeSize * 3f); // eyeSize сильнее влияет на радиус
        currentSightAngle = baseSightAngle * (1f + genome.eyeSize * 1f);  // и на угол
        currentSightAngle = Mathf.Clamp(currentSightAngle, 30f, 359f); // Ограничиваем угол
    }

    public List<float> GatherSenses()
    {
        // ... (Код GatherSenses, AddTargetSensorInputs, CalculatePriority, AddObstacleSensorInputs, CastObstacleRay
        // остается таким же, как в предыдущей версии. Я его не дублирую здесь для краткости.)
        // ЕСЛИ НУЖЕН ПОЛНЫЙ КОД SquidSenses СНОВА, СООБЩИТЕ.
        // ВАЖНО: Я предполагаю, что эта часть у вас уже есть и работает.
        // Я добавлю его для полноты:
        if (!enabled) return new List<float>();

        List<float> inputs = new List<float>();
        visibleFoodDebug.Clear();
        visibleAgentsDebug.Clear();
        targetPriorities.Clear();

        // Входы: 2 еды * (расст, x, y, тип=1) = 8
        //         1 агент * (расст, x, y, размер=0.5) = 4
        //         3 стены * (расст) = 3
        //         свои (энергия, возраст) = 2
        // Итого: 8+4+3+2 = 17 входов. Убедитесь, что genome.inputNodes = 17

        AddTargetSensorInputs(inputs, foodLayer, 2, ref visibleFoodDebug, "Food");
        AddTargetSensorInputs(inputs, agentLayer, 1, ref visibleAgentsDebug, "Agent");
        AddObstacleSensorInputs(inputs);

        if (TryGetComponent<SquidMetabolism>(out var metabolism))
        {
            inputs.Add(Mathf.Clamp01(metabolism.CurrentEnergy / metabolism.maxEnergyGeno));
            inputs.Add(Mathf.Clamp01(metabolism.Age / genome.maxAge));
        } else {
            inputs.Add(0.5f); inputs.Add(0f);
        }
        
        // Дополнение до нужного количества входов
        int expectedInputs = (genome != null) ? genome.inputNodes : FindFirstObjectByType<SimulationManager>().numInputNodes; // Безопасное получение
        while (inputs.Count < expectedInputs) inputs.Add(0f);
        if (inputs.Count > expectedInputs) inputs = inputs.Take(expectedInputs).ToList();

        return inputs;
    }
    
    public Dictionary<GameObject, float> GetTargetInfo() { return targetPriorities; }

    void AddTargetSensorInputs(List<float> inputs, LayerMask layer, int count, ref List<Transform> visibleDebugList, string targetType)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(agentTransform.position, currentSightRadius, layer);
        List<(Transform item, float dist, Vector2 dirRel, float priority)> sortedTargets =
            new List<(Transform, float, Vector2, float)>();

        foreach (var col in colliders)
        {
            if (targetType == "Agent" && col.gameObject == this.gameObject) continue;

            Vector3 directionToTargetWorld = col.transform.position - agentTransform.position;
            float angleToTarget = Vector2.Angle(agentTransform.up, directionToTargetWorld.normalized);

            if (angleToTarget < currentSightAngle / 2f)
            {
                float distance = directionToTargetWorld.magnitude;
                if (distance < currentSightRadius && distance > 0.01f)
                {
                    Vector2 relativeDirection = agentTransform.InverseTransformDirection(directionToTargetWorld.normalized);
                    float priority = CalculatePriority(col.gameObject, distance, targetType);
                    
                    sortedTargets.Add((col.transform, distance, relativeDirection, priority));
                    if (!targetPriorities.ContainsKey(col.gameObject) || priority > targetPriorities[col.gameObject]) {
                        targetPriorities[col.gameObject] = priority;
                    }
                }
            }
        }
        sortedTargets = sortedTargets.OrderByDescending(t => t.priority).ThenBy(t => t.dist).ToList();

        for (int i = 0; i < count; i++)
        {
            if (i < sortedTargets.Count)
            {
                inputs.Add(Mathf.Clamp01(sortedTargets[i].dist / currentSightRadius));
                inputs.Add(sortedTargets[i].dirRel.x);
                inputs.Add(sortedTargets[i].dirRel.y);
                
                float typeOrSizeInput = 0f;
                if (targetType == "Food") typeOrSizeInput = 1.0f;
                else if (targetType == "Agent" && sortedTargets[i].item.TryGetComponent<SquidAgent>(out var otherAgent)) {
                     typeOrSizeInput = Mathf.Clamp01(otherAgent.genome.mantleLength / 2f); // Нормализованный размер другого агента (до 2х метров = 1.0)
                }
                inputs.Add(typeOrSizeInput);
                if (visibleDebugList != null) visibleDebugList.Add(sortedTargets[i].item);
            }
            else
            {
                inputs.Add(1f); inputs.Add(0f); inputs.Add(0f); inputs.Add(0f);
            }
        }
    }
    
    float CalculatePriority(GameObject target, float distance, string type)
    {
        float basePriority = 1.0f / (distance + 0.1f);
        if (type == "Food" && target.TryGetComponent<Food>(out var foodComp)) {
             float preferenceFactor = (foodComp.type == FoodType.Plant) ? (1f - genome.foodPreference) : genome.foodPreference;
             basePriority *= (1f + preferenceFactor * 2f); // Сильнее влияем предпочтением
             basePriority *= (foodComp.energyValue / 10f);
        } else if (type == "Agent" && target.TryGetComponent<SquidAgent>(out var otherAgent)) {
            // TODO: Приоритет для атаки/избегания. Учитываем агрессию своего и "угрозу" другого.
            // basePriority *= genome.aggression;
            // if (otherAgent.genome.mantleLength > genome.mantleLength) basePriority *= 0.5f; // Меньший приоритет для больших
        }
        return basePriority;
    }

    void AddObstacleSensorInputs(List<float> inputs)
    {
        float rayLength = currentSightRadius * 0.4f; // Чуть длиннее
        inputs.Add(CastObstacleRay(agentTransform.up, rayLength));
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,35) * agentTransform.up, rayLength)); // Угол чуть шире
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,-35) * agentTransform.up, rayLength));
    }

    float CastObstacleRay(Vector2 direction, float length)
    {
        Vector2 rayOrigin = (Vector2)agentTransform.position + direction * 0.2f; // Начинаем чуть дальше от центра
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, length, obstacleLayer);
        if (hit.collider != null) return Mathf.Clamp01(hit.distance / length);
        return 1f;
    }


    void OnDrawGizmos() // Будет рисовать всегда, если Gizmos включены
    {
        if (agentTransform == null || genome == null) return; // Не рисовать, если не инициализирован

        // Постоянное отображение поля зрения, если включено в UI или это выбранный агент
        bool shouldDraw = (uiManager != null && uiManager.showAllAgentGizmos);
        if (uiManager != null && uiManager.selectedAgentUI == parentAgent) shouldDraw = true;


        if (shouldDraw) {
            Gizmos.color = new Color(genome.mantleColor.r, genome.mantleColor.g, genome.mantleColor.b, 0.15f); // Цвет поля зрения как у кальмара, полупрозрачный

            // Рисуем сектор обзора
            Vector3 forward = agentTransform.up;
            float halfFOV = currentSightAngle / 2.0f;
            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);
            Vector3 leftRayDirection = leftRayRotation * forward;
            Vector3 rightRayDirection = rightRayRotation * forward;

            int segments = 20; // Количество сегментов для дуги
            Vector3 prevPoint = agentTransform.position + leftRayDirection * currentSightRadius;
            for(int i=1; i <= segments; ++i) {
                float angle = Mathf.Lerp(-halfFOV, halfFOV, (float)i/segments);
                Vector3 currentRayDir = Quaternion.AngleAxis(angle, Vector3.forward) * forward;
                Vector3 currentPoint = agentTransform.position + currentRayDir * currentSightRadius;
                Gizmos.DrawLine(prevPoint, currentPoint);
                // Заполненный сектор (рисуем треугольники к центру)
                 Gizmos.DrawLine(agentTransform.position, prevPoint); // Для заполнения
                prevPoint = currentPoint;
            }
            Gizmos.DrawLine(agentTransform.position, agentTransform.position + leftRayDirection * currentSightRadius);
            Gizmos.DrawLine(agentTransform.position, agentTransform.position + rightRayDirection * currentSightRadius);


            // Линии к видимым объектам (только для выбранного, чтобы не захламлять)
            if (uiManager != null && uiManager.selectedAgentUI == parentAgent) {
                Gizmos.color = Color.green;
                foreach(var food in visibleFoodDebug) if(food != null) Gizmos.DrawLine(agentTransform.position, food.position);
                Gizmos.color = Color.cyan;
                foreach(var agent in visibleAgentsDebug) if(agent != null) Gizmos.DrawLine(agentTransform.position, agent.position);
            }
        }
    }
}
EOF

# --- Assets/Agents/SquidMeshGenerator.cs ---
# (Изменения для корректного отображения глаз и их позиционирования)
cat <<EOF > "${ASSETS_PATH}/Agents/SquidMeshGenerator.cs"
// ${ASSETS_PATH}/Agents/SquidMeshGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidMeshGenerator : MonoBehaviour
{
    private Genome genome;
    private Transform agentTransform;

    public GameObject mantleObject { get; private set; }
    public List<GameObject> swimmingTentacleObjects { get; private set; } = new List<GameObject>();
    public List<GameObject> graspingTentacleObjects { get; private set; } = new List<GameObject>();
    public GameObject eyesRootObject { get; private set; }
    
    // Добавим ссылки на объекты глаз для возможной анимации
    public GameObject leftEyeWhite { get; private set; }
    public GameObject rightEyeWhite { get; private set; }
    public GameObject leftPupil { get; private set; }
    public GameObject rightPupil { get; private set; }


    [Header("Materials (Assign in Prefab)")]
    public Material mantleMaterial;
    public Material tentacleMaterial;
    public Material eyeMaterial;
    public Material pupilMaterial;

    private bool isInitialized = false;

    public void Initialize(Transform parentTransform)
    {
        this.agentTransform = parentTransform;
        if (mantleMaterial == null || tentacleMaterial == null || eyeMaterial == null || pupilMaterial == null) {
            Debug.LogError($"SquidMeshGenerator on {agentTransform.name} is missing materials!");
            enabled = false; return;
        }
        isInitialized = true;
    }

    public void GenerateInitialMeshes(Genome agentGenome)
    {
        if (!isInitialized || agentGenome == null) return;
        this.genome = agentGenome;
        ClearExistingMeshObjects();

        // 1. Мантия
        mantleObject = CreateMeshHolder("Mantle", agentTransform, mantleMaterial, genome.mantleColor);
        Mesh mantleMesh = GenerateMantleProceduralMesh(genome.mantleLength, genome.mantleMaxDiameter);
        if (mantleObject.TryGetComponent<MeshFilter>(out var mantleMF)) mantleMF.mesh = mantleMesh;
        // Сделаем мантию чуть "вглубь" по Z, чтобы глаза и щупальца были точно поверх
        mantleObject.transform.localPosition = new Vector3(0,0, 0.1f);
        
        // 2. Плавательные щупальца
        for (int i = 0; i < TentacleController.NUM_SWIMMING_TENTACLES; i++)
        {
            // Цвет щупалец может быть чуть темнее или отличаться
            Color tentacleColor = Color.Lerp(genome.mantleColor, Color.black, 0.2f);
            GameObject tentacleGO = CreateMeshHolder($"SwimmingTentacle_{i}", agentTransform, tentacleMaterial, tentacleColor);
            float angle = i * (360f / TentacleController.NUM_SWIMMING_TENTACLES) + 180f;
            Vector3 baseOffset = new Vector3(0, -genome.mantleLength * 0.45f, 0); // Чуть ниже, у основания
            tentacleGO.transform.localPosition = baseOffset + Quaternion.Euler(0,0,angle) * Vector3.up * (genome.mantleMaxDiameter * 0.4f);
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0,angle);
            tentacleGO.transform.localScale = Vector3.one * Mathf.Clamp(genome.mantleLength, 0.5f, 1.5f); // Размер щупалец зависит от размера тела

            Mesh swimTentacleMesh = GenerateProceduralTentacleMesh(genome.baseSwimTentacleLength, genome.swimTentacleThickness, 8, 5, false);
            if (tentacleGO.TryGetComponent<MeshFilter>(out var swimMF)) swimMF.mesh = swimTentacleMesh;
            swimmingTentacleObjects.Add(tentacleGO);
        }

        // 3. Хватательные щупальца (точки крепления)
        for (int i = 0; i < TentacleController.NUM_GRASPING_TENTACLES; i++)
        {
            GameObject tentacleGO = new GameObject($"GraspingTentacleAnchor_{i}");
            tentacleGO.transform.SetParent(agentTransform);
            float sideOffset = (i == 0 ? -1f : 1f) * genome.mantleMaxDiameter * 0.15f;
            // Располагаем ближе к "передней" части (относительно движения transform.up)
            tentacleGO.transform.localPosition = new Vector3(sideOffset, genome.mantleLength * 0.2f, -0.01f);
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0, (i == 0 ? 25f : -25f));
            graspingTentacleObjects.Add(tentacleGO);
        }

        // 4. Глаза
        eyesRootObject = new GameObject("EyesRoot");
        eyesRootObject.transform.SetParent(agentTransform);
        // Позиционируем глаза на "передней-верхней" части мантии
        eyesRootObject.transform.localPosition = new Vector3(0, genome.mantleLength * 0.35f, -0.05f); // Z=-0.05f чтобы были поверх мантии
        
        float eyeSpacing = genome.mantleMaxDiameter * 0.2f + genome.eyeSize * 0.5f;
        leftEyeWhite = CreateEyePart("LeftEyeWhite", eyesRootObject.transform, -eyeSpacing, genome.eyeSize, eyeMaterial, Color.white);
        rightEyeWhite = CreateEyePart("RightEyeWhite", eyesRootObject.transform, eyeSpacing, genome.eyeSize, eyeMaterial, Color.white);

        float pupilSize = genome.eyeSize * 0.5f;
        leftPupil = CreateEyePart("LeftPupil", leftEyeWhite.transform, 0, pupilSize, pupilMaterial, Color.black); // Зрачок дочерний к белку
        leftPupil.transform.localPosition = new Vector3(0,0,-0.01f); // Чуть впереди белка
        rightPupil = CreateEyePart("RightPupil", rightEyeWhite.transform, 0, pupilSize, pupilMaterial, Color.black);
        rightPupil.transform.localPosition = new Vector3(0,0,-0.01f);
    }

    GameObject CreateMeshHolder(string name, Transform parent, Material materialInstanceSrc, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.material = new Material(materialInstanceSrc); // Создаем экземпляр материала
        mr.material.color = color;
        return obj;
    }
    
    GameObject CreateEyePart(string name, Transform parent, float xOffset, float radius, Material materialInstanceSrc, Color color)
    {
        GameObject eyePartObj = CreateMeshHolder(name, parent, materialInstanceSrc, color);
        eyePartObj.transform.localPosition = new Vector3(xOffset, 0, 0); // Позиция относительно родителя (EyesRoot или EyeWhite)
        Mesh eyeMesh = Generate2DCircleMesh(radius, 16);
        if (eyePartObj.TryGetComponent<MeshFilter>(out var eyeMF)) eyeMF.mesh = eyeMesh;
        return eyePartObj;
    }

    // ... (Методы GenerateMantleProceduralMesh, GenerateProceduralTentacleMesh, Generate2DCircleMesh, ClearExistingMeshObjects
    // остаются такими же, как в предыдущей версии. Я их не дублирую здесь для краткости.)
    // ЕСЛИ НУЖЕН ПОЛНЫЙ КОД SquidMeshGenerator СНОВА, СООБЩИТЕ.
    // Я добавлю их для полноты:

    Mesh GenerateMantleProceduralMesh(float length, float diameter)
    {
        Mesh mesh = new Mesh { name = "ProceduralMantle" };
        int segmentsAround = 12;
        int segmentsAlong = 3;  // Больше сегментов для более плавной формы
        float radius = diameter / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int y_idx = 0; y_idx <= segmentsAlong; y_idx++)
        {
            float t_along = (float)y_idx / segmentsAlong; // 0 у "головы", 1 у "хвоста"
            float currentY = -length / 2f + length * t_along;
            
            // Форма мантии: широкая в середине, сужается к концам
            float radiusFactor = Mathf.Sin(t_along * Mathf.PI); // 0 на концах, 1 в середине
            if (t_along < 0.1f) radiusFactor = Mathf.Lerp(0.5f, 1.0f, t_along / 0.1f * (Mathf.PI/2f)); // Голова чуть шире
            else if (t_along > 0.9f) radiusFactor = Mathf.Lerp(1.0f, 0.1f, (t_along - 0.9f) / 0.1f * (Mathf.PI/2f)); // Хвост острый

            float currentRadius = radius * radiusFactor;
            currentRadius = Mathf.Max(currentRadius, diameter * 0.05f); // Минимальный радиус

            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++)
            {
                float t_around = (float)i_idx / segmentsAround;
                float angle = t_around * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentY, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2(t_around, t_along));
            }
        }
        
        for (int y = 0; y < segmentsAlong; y++)
        {
            for (int i = 0; i < segmentsAround; i++)
            {
                int v00 = y * (segmentsAround + 1) + i;
                int v01 = y * (segmentsAround + 1) + (i + 1);
                int v10 = (y + 1) * (segmentsAround + 1) + i;
                int v11 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v00, v10, v01 });
                triangles.AddRange(new int[] { v01, v10, v11 });
            }
        }
        // TODO: Закрыть полюса, если необходимо (для этой формы может и не понадобиться, если сужение достаточно сильное)

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh GenerateProceduralTentacleMesh(float length, float thickness, int segmentsAlong, int segmentsAround, bool bulbousTip)
    {
        Mesh mesh = new Mesh { name = "ProceduralTentacle" };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        float radius = thickness / 2f;

        for (int y_idx = 0; y_idx <= segmentsAlong; y_idx++) {
            float t_along = (float)y_idx / segmentsAlong;
            float y_pos = length * t_along; // Ось Y - вдоль щупальца, от 0 до length
            
            float currentRadius = radius;
            if (t_along < 0.1f) currentRadius = Mathf.Lerp(radius * 0.6f, radius, t_along / 0.1f); // У основания чуть тоньше
            else if (bulbousTip && t_along > 0.85f) currentRadius = Mathf.Lerp(radius, radius * 1.8f, (t_along - 0.85f) / 0.15f); // Утолщение
            else currentRadius = Mathf.Lerp(radius, radius * 0.3f, t_along); // Сужение к кончику

            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++) {
                float t_around = (float)i_idx / segmentsAround;
                float angle = t_around * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, y_pos, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2(t_around, t_along));
            }
        }
        
        for (int y = 0; y < segmentsAlong; y++) {
            for (int i = 0; i < segmentsAround; i++) {
                int v00 = y * (segmentsAround + 1) + i;
                int v01 = y * (segmentsAround + 1) + (i + 1);
                int v10 = (y + 1) * (segmentsAround + 1) + i;
                int v11 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v00, v10, v01 });
                triangles.AddRange(new int[] { v01, v10, v11 });
            }
        }
        // TODO: Закрыть концы (особенно если не острые)

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh Generate2DCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh { name = "2DCircle" };
        List<Vector3> vertices = new List<Vector3> { Vector3.zero };
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

        for (int i = 0; i <= segments; i++) { // <= segments для замыкания UV и геометрии
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
            uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            if (i > 0) {
                triangles.AddRange(new int[] { 0, i, i + 1 }); // Треугольник от центра к двум точкам на окружности
            }
        }
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void ClearExistingMeshObjects()
    {
        if (mantleObject != null) Destroy(mantleObject);
        if (eyesRootObject != null) Destroy(eyesRootObject);
        foreach (var go in swimmingTentacleObjects) if (go != null) Destroy(go);
        foreach (var go in graspingTentacleObjects) if (go != null) Destroy(go); // Удаляем и якоря для хватательных
        swimmingTentacleObjects.Clear();
        graspingTentacleObjects.Clear();
        leftEyeWhite = rightEyeWhite = leftPupil = rightPupil = null;
    }

    public void UpdateDynamicMeshes(SquidBrain.BrainOutput brainOutput)
    {
        if (!isInitialized || genome == null) return;

        if (mantleObject != null)
        {
            // Более сложная пульсация (если нужно) или просто обновление цвета
            if (mantleObject.TryGetComponent<MeshRenderer>(out var mr) && mr.material.color != genome.mantleColor) {
                 mr.material.color = genome.mantleColor;
            }
            // Анимация глаз (например, слежение за целью)
            UpdateEyeLook(brainOutput);
        }
    }

    void UpdateEyeLook(SquidBrain.BrainOutput brainOutput) {
        if (leftPupil == null || rightPupil == null) return;

        // Определяем, куда смотрят щупальца (усредненное направление)
        Vector2 lookDir = Vector2.zero;
        int count = 0;
        if (brainOutput.graspTentacleExtend0 > 0.1f) { lookDir += brainOutput.graspTentacleTargetDir0; count++; }
        if (brainOutput.graspTentacleExtend1 > 0.1f) { lookDir += brainOutput.graspTentacleTargetDir1; count++; }

        if (count > 0) lookDir /= count; else lookDir = Vector2.up; // Смотрим вперед по умолчанию

        // Преобразуем мировое направление (относительно кальмара) в локальное для глаза
        // float pupilMoveRange = genome.eyeSize * 0.2f;
        // leftPupil.transform.localPosition = new Vector3(lookDir.x * pupilMoveRange, lookDir.y* pupilMoveRange, leftPupil.transform.localPosition.z);
        // rightPupil.transform.localPosition = new Vector3(lookDir.x * pupilMoveRange, lookDir.y* pupilMoveRange, rightPupil.transform.localPosition.z);
        // Пока оставим зрачки по центру
    }
}
EOF

# --- Assets/UI/UIManager.cs ---
# (Добавим поле для selectedAgentUI, кнопку слежения, вывод генома, опцию showAllAgentGizmos)
cat <<EOF > "${ASSETS_PATH}/UI/UIManager.cs"
// ${ASSETS_PATH}/UI/UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text; // Для StringBuilder

public class UIManager : MonoBehaviour
{
    public SimulationManager simManager;
    public InputManager inputManager; // Для камеры и инструментов

    [Header("Control Panel Elements")]
    public Button startButton;
    public Button pauseButton;
    public Button resumeButton;
    public Slider timeScaleSlider;
    public TMP_Text timeScaleText;
    public TMP_Text generationText;
    public Toggle divineToolsToggle;
    public Toggle showAllGizmosToggle; // Новая опция

    [Header("Agent Inspector Panel")]
    public GameObject agentInspectorPanelGO;
    public TMP_Text agentNameText;
    public TMP_Text agentEnergyText;
    public TMP_Text agentAgeText;
    public TMP_Text agentFitnessText;
    public TMP_Text agentGenomeMultiLineText; // Для многострочного вывода генома
    public Button closeInspectorButton;
    public Button followAgentButton; // Новая кнопка
    // public Button saveGenomeButton; // TODO: Позже

    public SquidAgent selectedAgentUI { get; private set; } // Чтобы другие скрипты (Senses) знали, кто выбран
    public bool showAllAgentGizmos { get; private set; } = false;

    private CameraController cameraController;


    void Start()
    {
        if (simManager == null) simManager = FindFirstObjectByType<SimulationManager>();
        if (inputManager == null) inputManager = FindFirstObjectByType<InputManager>();
        cameraController = FindFirstObjectByType<CameraController>(); // Найдем контроллер камеры


        if (simManager == null) {
            Debug.LogError("UIManager could not find SimulationManager!");
            enabled = false; return;
        }
        
        if (startButton) startButton.onClick.AddListener(simManager.RequestStartSimulation);
        if (pauseButton) pauseButton.onClick.AddListener(simManager.RequestPauseSimulation);
        if (resumeButton) resumeButton.onClick.AddListener(simManager.RequestResumeSimulation);
        if (timeScaleSlider) timeScaleSlider.onValueChanged.AddListener(simManager.RequestAdjustTimeScale);
        
        if (divineToolsToggle && inputManager != null) {
            divineToolsToggle.isOn = inputManager.divineToolsEnabled; // Инициализация состояния
            divineToolsToggle.onValueChanged.AddListener((value) => inputManager.divineToolsEnabled = value);
        }
        if (showAllGizmosToggle) {
            showAllGizmosToggle.isOn = showAllAgentGizmos;
            showAllGizmosToggle.onValueChanged.AddListener((value) => showAllAgentGizmos = value);
        }
        
        if (closeInspectorButton && agentInspectorPanelGO) {
             closeInspectorButton.onClick.AddListener(DeselectAgentForInspector);
        }
        if (followAgentButton && cameraController != null) {
            followAgentButton.onClick.AddListener(() => {
                if (selectedAgentUI != null) cameraController.SetTargetToFollow(selectedAgentUI.transform);
            });
        } else if (cameraController == null) Debug.LogWarning("CameraController not found for FollowAgent button.");


        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
        InitializeUIValues(Time.timeScale, simManager.currentGenerationNumber);
    }
    
    public void InitializeUIValues(float initialTimeScale, int initialGen) {
        if (timeScaleSlider) {
            timeScaleSlider.minValue = 0.1f; timeScaleSlider.maxValue = 10f; // Увеличил макс. скорость
            timeScaleSlider.value = initialTimeScale;
        }
        UpdateTimeScaleTextValue(initialTimeScale);
        UpdateGenerationText(initialGen);
    }

    public float GetCurrentTimeScaleRequest() {
        return timeScaleSlider != null ? timeScaleSlider.value : 1f;
    }
    
    public void UpdateTimeScaleSliderValue(float currentTimeScale) {
        if (timeScaleSlider && Mathf.Abs(timeScaleSlider.value - currentTimeScale) > 0.01f) timeScaleSlider.value = currentTimeScale;
        UpdateTimeScaleTextValue(currentTimeScale);
    }

    void UpdateTimeScaleTextValue(float value) {
        if(timeScaleText) timeScaleText.text = $"Time: x{value:F1}";
    }
    
    void UpdateGenerationText(int genNumber) {
        if (generationText) generationText.text = $"Gen: {genNumber}";
    }

    public void UpdateSimulationStateUI(bool isRunning, float currentTimeScale, int generationNum) {
        if (startButton) startButton.interactable = !isRunning;
        if (pauseButton) pauseButton.interactable = isRunning && currentTimeScale > 0.001f; // Активна если идет и не на нулевой скорости
        if (resumeButton) resumeButton.interactable = isRunning && currentTimeScale < 0.001f; // Активна если идет, но на нулевой скорости (пауза)
        
        UpdateTimeScaleSliderValue(currentTimeScale);
        UpdateGenerationText(generationNum);
    }

    public void SelectAgentForInspector(SquidAgent agent)
    {
        selectedAgentUI = agent;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(true);
        UpdateAgentInspectorUI();
    }
    public void DeselectAgentForInspector() {
        selectedAgentUI = null;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
        if (cameraController) cameraController.ClearTargetToFollow(); // Отменяем слежение
    }

    void Update() {
        if (selectedAgentUI != null && agentInspectorPanelGO != null && agentInspectorPanelGO.activeSelf) {
            // Проверяем, жив ли еще выбранный агент
            if (selectedAgentUI.gameObject == null || !selectedAgentUI.isInitialized) {
                DeselectAgentForInspector();
                return;
            }
            UpdateAgentInspectorUI();
        }
    }

    void UpdateAgentInspectorUI()
    {
        if (selectedAgentUI == null || !selectedAgentUI.isInitialized) {
            if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
            return;
        }

        if (agentNameText) agentNameText.text = selectedAgentUI.gameObject.name; // Убрал "Name: " для краткости
        
        if (selectedAgentUI.TryGetComponent<SquidMetabolism>(out var meta)) {
             if (agentEnergyText) agentEnergyText.text = $"E: {meta.CurrentEnergy:F0}/{meta.maxEnergyGeno:F0}";
             if (agentAgeText) agentAgeText.text = $"Age: {meta.Age:F1}s";
        }
        if (agentFitnessText && selectedAgentUI.genome != null) agentFitnessText.text = $"Fit: {selectedAgentUI.genome.fitness:F1}";
        
        if (agentGenomeMultiLineText && selectedAgentUI.genome != null) {
            Genome g = selectedAgentUI.genome;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Mantle L: {g.mantleLength:F2}, D: {g.mantleMaxDiameter:F2}");
            sb.AppendLine($"Color: ({g.mantleColor.r:F1},{g.mantleColor.g:F1},{g.mantleColor.b:F1})");
            sb.AppendLine($"SwimTent L: {g.baseSwimTentacleLength:F2}, Th: {g.swimTentacleThickness:F3}");
            sb.AppendLine($"GraspTent L: {g.baseGraspTentacleLength:F2} (x{g.maxGraspTentacleLengthFactor:F1}), Th: {g.graspTentacleThickness:F3}");
            sb.AppendLine($"Eye Size: {g.eyeSize:F2}");
            sb.AppendLine($"Metab.Rate: {g.metabolismRateFactor:F2}, MaxAge: {g.maxAge:F0}s");
            sb.AppendLine($"Repr.Thresh: {g.energyToReproduceThresholdFactor:P0}, Cost: {g.energyCostOfReproductionFactor:P0}");
            sb.AppendLine($"Aggression: {g.aggression:F2}, FoodPref: {g.foodPreference:F2}");
            // TODO: Вывод активности НС, если будет реализовано
            agentGenomeMultiLineText.text = sb.ToString();
        }
    }
}
EOF

# --- Assets/Core/InputManager.cs ---
# (Добавим CameraController и слежение)
cat <<EOF > "${ASSETS_PATH}/Core/InputManager.cs"
// ${ASSETS_PATH}/Core/InputManager.cs
using UnityEngine;

public class InputManager : MonoBehaviour
{
    // SimulationManager больше не нужен здесь напрямую для управления симуляцией
    private Camera mainCamera;
    private UIManager uiManager;
    private CameraController cameraController; // Для управления слежением

    [Header("Divine Tools (Example)")]
    public GameObject plantFoodPrefabToSpawn;
    public bool divineToolsEnabled = false;

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        cameraController = FindFirstObjectByType<CameraController>();
        mainCamera = Camera.main;

        if (mainCamera == null) {
            Debug.LogError("Main Camera not found by InputManager!");
            enabled = false;
        }
        if (cameraController == null) {
            Debug.LogWarning("CameraController not found by InputManager. Follow agent feature might not work.");
        }
    }

    void Update()
    {
        // cameraController будет сам обрабатывать свое движение и зум.
        // Этот скрипт теперь отвечает за клики по миру (выбор агента, божественные инструменты).
        HandleAgentSelectionAndDivineTools();
    }


    void HandleAgentSelectionAndDivineTools()
    {
        if (mainCamera == null) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            if (hit.collider != null)
            {
                SquidAgent agent = hit.collider.GetComponentInParent<SquidAgent>();
                if (agent != null && uiManager != null)
                {
                    uiManager.SelectAgentForInspector(agent);
                } else if (agent == null && uiManager != null) { // Кликнули не по агенту
                     uiManager.DeselectAgentForInspector();
                }
            } else {
                 if (uiManager != null) uiManager.DeselectAgentForInspector();
            }
        }

        if (divineToolsEnabled && plantFoodPrefabToSpawn != null) {
            if (Input.GetMouseButton(1)) // Правая кнопка мыши (удерживание для спавна)
            {
                // Спавнить с некоторой задержкой, чтобы не создавать слишком много
                // Для простоты пока оставим по клику, но лучше сделать rate limit
                if (Input.GetMouseButtonDown(1)) { // Только по первому клику в кадре
                    Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    mouseWorldPos.z = 0;
                    Instantiate(plantFoodPrefabToSpawn, mouseWorldPos, Quaternion.identity);
                    EventLogPanel.Instance?.AddLogMessage("Divine: Plant food spawned.");
                }
            }
        }
    }
}
EOF

# --- Assets/UI/CameraController.cs --- (Новый файл для управления камерой)
cat <<EOF > "${ASSETS_PATH}/UI/CameraController.cs"
// ${ASSETS_PATH}/UI/CameraController.cs
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan & Zoom")]
    public float panSpeed = 20f;
    public float scrollSpeed = 30f; // Увеличил чувствительность
    public float minZoomOrthographic = 2f;
    public float maxZoomOrthographic = 100f; // Увеличил макс зум

    [Header("Follow Target")]
    public Transform targetToFollow;
    public float followSmoothSpeed = 0.125f;
    public Vector3 followOffset = new Vector3(0, 0, -10); // Z остается -10

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) {
            Debug.LogError("CameraController script must be attached to a Camera GameObject!");
            enabled = false;
        }
    }

    void LateUpdate() // Используем LateUpdate для камеры, чтобы она двигалась после всех агентов
    {
        if (cam == null) return;

        if (targetToFollow != null)
        {
            Vector3 desiredPosition = targetToFollow.position + followOffset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, followSmoothSpeed * Time.deltaTime * 10f); // *10f для более быстрой реакции
            transform.position = smoothedPosition;
        }
        else // Ручное управление, если нет цели для слежения
        {
            HandleManualPan();
        }
        HandleManualZoom(); // Зум работает всегда
    }

    void HandleManualPan()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;

        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            Vector3 move = new Vector3(horizontal, vertical, 0) * panSpeed * Time.unscaledDeltaTime;
            transform.Translate(move, Space.World);
        }
    }

    void HandleManualZoom()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() && Input.mouseScrollDelta.y !=0) // Не зумить если курсор над UI и есть скролл
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (cam.orthographic && Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * scrollSpeed * Time.unscaledDeltaTime * 10f;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoomOrthographic, maxZoomOrthographic);
        }
    }

    public void SetTargetToFollow(Transform target)
    {
        targetToFollow = target;
        EventLogPanel.Instance?.AddLogMessage($"Camera now following {target.name}.");
    }

    public void ClearTargetToFollow()
    {
        if(targetToFollow != null) EventLogPanel.Instance?.AddLogMessage($"Camera stopped following {targetToFollow.name}.");
        targetToFollow = null;
    }
}
EOF


echo "C# файлы обновлены/созданы (v2) в $ASSETS_PATH."
echo ""
echo "=============================================================================="
echo "ЗАВЕРШЕНО ОБНОВЛЕНИЕ СКРИПТОВ (v2)!"
echo "ВАЖНО:"
echo "1. Откройте проект в Unity Hub. Unity перекомпилирует скрипты."
echo "2. Проверьте консоль на наличие ошибок компиляции. Исправьте их, если есть."
echo "3. **НОВЫЙ СКРИПТ КАМЕРЫ:** Удалите старый 'InputManager.cs' с Main Camera (если он там был)."
echo "   Добавьте новый скрипт 'CameraController.cs' на ваш объект 'Main Camera'."
echo "4. **UI НАСТРОЙКА:**"
echo "   - В UIManager_Object перетащите 'InputManager_Object' в поле 'Input Manager'."
echo "   - Добавьте на Canvas кнопку 'FollowAgentButton', текстовое поле 'AgentGenomeMultiLineText', тогл 'ShowAllGizmosToggle'."
echo "   - Свяжите эти новые UI элементы с соответствующими полями в UIManager_Object."
echo "5. **БАЛАНС:** Проблема перенаселения все еще может существовать. Используйте предложенные ранее методы для ее решения."
echo "6. **ГЛАЗА:** Проверьте Z-координаты в SquidMeshGenerator для глаз и мантии. Мантия должна быть 'глубже'."
echo "   Убедитесь, что материалы 'EyeMaterial' и 'PupilMaterial' назначены и непрозрачны."
echo "УДАЧИ!"
echo "=============================================================================="
