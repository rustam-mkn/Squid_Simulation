#!/bin/bash

# ==============================================================================
# ЗАМЕНИТЕ ЭТОТ ПУТЬ НА ВАШ РЕАЛЬНЫЙ ПУТЬ К ПАПКЕ "Assets" ВАШЕГО ПРОЕКТА UNITY
ASSETS_PATH="/Users/user/Dev/Unity/Squid/Assets" # Пример!
# ==============================================================================

# --- Переход в директорию Assets ---
# Важно, чтобы пути к файлам внутри cat <<EOF были относительны ASSETS_PATH
# или скрипт должен сначала перейти в ASSETS_PATH.
# Для простоты, будем использовать полный путь в команде `cat`.

echo "Обновление/создание C# файлов в: $ASSETS_PATH"

# ==============================================================================
# --- СОЗДАНИЕ C# ФАЙЛОВ С ИСПРАВЛЕННЫМ И УЛУЧШЕННЫМ НАЧАЛЬНЫМ КОДОМ ---
# ==============================================================================

# --- Assets/Core ---
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
    public UIManager uiManager; // Ссылка на UI менеджер

    [Header("Simulation Settings")]
    public int initialPopulationSize = 20;
    public float generationTime = 60f; // Секунд
    private float currentGenerationTimer;

    private List<SquidAgent> agents = new List<SquidAgent>();
    private List<Genome> genomesForNextGeneration = new List<Genome>(); // Геномы, которые пойдут в ГА

    [Header("Neural Network Genome Params")]
    public int numInputNodes = 10;
    public int numHiddenNodes = 15;
    public int numOutputNodes = 8;

    public bool isRunning { get; private set; } = false;
    public int currentGenerationNumber { get; private set; } = 0;


    void Start()
    {
        // Попытка найти объекты, если они не назначены в инспекторе
        if (foodSpawner == null) foodSpawner = FindFirstObjectByType<FoodSpawner>();
        if (statisticsManager == null) statisticsManager = FindFirstObjectByType<StatisticsManager>();
        if (gaManager == null) gaManager = FindFirstObjectByType<GeneticAlgorithmManager>();
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();

        if (foodSpawner == null || statisticsManager == null || gaManager == null || uiManager == null || squidAgentPrefab == null)
        {
            Debug.LogError("SimulationManager is missing critical references! Please assign them in the Inspector.");
            enabled = false; // Отключаем компонент, если что-то важное не настроено
            return;
        }
        // Начальное состояние - симуляция не запущена. Запуск через UI.
        Time.timeScale = 1f; // Убедимся, что время идет
    }

    public void RequestStartSimulation()
    {
        if (isRunning)
        {
            Debug.LogWarning("Simulation is already running.");
            return;
        }
        Debug.Log("Simulation Starting...");
        isRunning = true;
        Time.timeScale = 1f;
        currentGenerationNumber = 1;
        InitializeFirstGeneration();
        currentGenerationTimer = generationTime;
        if (statisticsManager) statisticsManager.ResetStatistics();
        if (foodSpawner) foodSpawner.SpawnInitialFood(); // Спавним еду при старте
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }

    public void RequestPauseSimulation()
    {
        if (!isRunning) return;
        isRunning = false; // Логическая пауза
        Time.timeScale = 0f; // Физическая остановка времени
        Debug.Log("Simulation Paused.");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }

    public void RequestResumeSimulation()
    {
        if (isRunning && Time.timeScale == 0f) // Если была на паузе
        {
            Time.timeScale = uiManager != null ? uiManager.GetCurrentTimeScaleRequest() : 1f; // Восстанавливаем запрошенную скорость
            isRunning = true; // Логическое возобновление
            Debug.Log("Simulation Resumed.");
        } else if (!isRunning && Time.timeScale > 0f) { // Если была остановлена, но время шло (редко)
             isRunning = true;
             Debug.Log("Simulation Resumed (logical).");
        }
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }
    
    public void RequestAdjustTimeScale(float scale)
    {
        if (isRunning) // Меняем скорость только если симуляция активна
        {
            Time.timeScale = Mathf.Max(0.1f, scale);
            Debug.Log($"Time scale set to: {Time.timeScale}");
        }
        if (uiManager) uiManager.UpdateTimeScaleSliderValue(Time.timeScale); // Обновляем UI даже если не запущено, чтобы запомнить значение
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
    }

    void Update()
    {
        if (!isRunning || Time.timeScale == 0f) return; // Если на физической паузе или не запущена

        currentGenerationTimer -= Time.deltaTime;
        if (currentGenerationTimer <= 0 || (agents.Count == 0 && genomesForNextGeneration.Count > 0))
        {
            PrepareAndStartNewGeneration();
        }

        if (statisticsManager)
        {
            // Статистика будет собираться в своем Update
        }
    }

    void InitializeFirstGeneration()
    {
        ClearPopulation();
        genomesForNextGeneration.Clear();
        for (int i = 0; i < initialPopulationSize; i++)
        {
            Genome newGenome = new Genome(numInputNodes, numHiddenNodes, numOutputNodes);
            newGenome.InitializeRandomPhysicalGenes(); // Важно инициализировать физ. гены
            // genomesForNextGeneration.Add(newGenome); // Не добавляем сразу, они спавнятся
            SpawnAgent(newGenome);
        }
        Debug.Log($"Initialized first generation with {agents.Count} agents.");
    }

    void PrepareAndStartNewGeneration()
    {
        Debug.Log("Preparing new generation...");
        currentGenerationNumber++;
        
        // Сбор геномов для эволюции. Фитнес должен быть присвоен геному ДО этого момента.
        // SquidMetabolism должен обновлять genome.fitness по мере жизни или при смерти.
        List<Genome> evaluatedGenomes = new List<Genome>(genomesForNextGeneration); // Берем собранные геномы (умерших и выживших в конце)
        genomesForNextGeneration.Clear(); // Очищаем для следующего сбора

        // Если все агенты умерли до окончания таймера поколения,
        // evaluatedGenomes уже должен быть заполнен их геномами с фитнесом.
        // Если таймер сработал, а агенты еще живы, их геномы тоже нужно добавить.
        foreach (var agent in agents) {
            if (agent != null && agent.genome != null) {
                 // Убеждаемся, что фитнес актуален (например, время жизни)
                 if(agent.TryGetComponent<SquidMetabolism>(out var meta)) agent.genome.fitness = meta.Age;
                 // Добавляем только если такого генома еще нет (избегаем дубликатов, если агент умер и его геном уже в genomesForNextGeneration)
                 if (!evaluatedGenomes.Exists(g => g == agent.genome)) // Сравнение по ссылке, если геном один и тот же объект
                 {
                    evaluatedGenomes.Add(new Genome(agent.genome)); // Добавляем КОПИЮ генома выжившего
                 }
            }
        }

        ClearAgentsVisuals(); // Уничтожаем GameObjects старых агентов

        if (evaluatedGenomes.Count == 0) {
            Debug.LogWarning("No genomes to evolve from. Re-initializing first generation.");
            InitializeFirstGeneration(); // Начать заново, если совсем нет генов
            currentGenerationTimer = generationTime;
            if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, Time.timeScale, currentGenerationNumber);
            return;
        }

        List<Genome> newGenerationGenomes = gaManager.EvolvePopulation(evaluatedGenomes);

        foreach (Genome genome in newGenerationGenomes)
        {
            genome.fitness = 0; // Сброс фитнеса для нового поколения
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
            Destroy(agentGO); // Уничтожаем, если не удалось инициализировать
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        float spawnRadius = (foodSpawner != null) ? foodSpawner.spawnRadius : 20f;
        // Убедимся, что спавн в пределах WorldBounds, если они есть
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
            genomesForNextGeneration.Add(finalGenome); // Добавляем геном с финальным фитнесом
            if (foodSpawner) foodSpawner.SpawnFoodAt(foodSpawner.meatFoodPrefab, agent.transform.position);
            Debug.Log($"Agent {agent.gameObject.name} died. Fitness: {finalGenome.fitness:F2}. Remaining: {agents.Count}");
        }
    }
    
    public void ReportAgentReproduction(SquidAgent parent, Genome offspringGenome)
    {
        // offspringGenome уже должен быть мутирован и готов к спавну
        // (мутация и скрещивание - ответственность GA Manager, вызываемого из SquidMetabolism -> Reproduce)
        SpawnAgent(offspringGenome, parent.transform.position + (Vector3)Random.insideUnitCircle * 2f);
        Debug.Log($"Agent {parent.gameObject.name} reproduced.");
    }

    void ClearPopulation() // Полная очистка, включая геномы
    {
        ClearAgentsVisuals();
        genomesForNextGeneration.Clear();
    }
    
    void ClearAgentsVisuals() // Только GameObjects
    {
        foreach (var agent in agents)
        {
            if (agent != null) Destroy(agent.gameObject);
        }
        agents.Clear();
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Core/StatisticsManager.cs"
// ${ASSETS_PATH}/Core/StatisticsManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StatisticsManager : MonoBehaviour
{
    public SimulationManager simManager; // Для проверки, запущена ли симуляция

    [Header("Data Lists")]
    public List<float> populationHistory = new List<float>();
    public List<float> averageEnergyHistory = new List<float>();
    public List<float> averageAgeHistory = new List<float>();
    // TODO: Добавить другие метрики (например, средние значения ключевых генов)
    public List<float> averageMantleLengthHistory = new List<float>();


    [Header("Current Values")]
    public int currentPopulationCount;
    public float currentAverageEnergy;
    public float currentAverageAge;
    public float currentAverageMantleLength;
    public float maxFitnessLastGeneration;


    [Header("Settings")]
    public float dataRecordInterval = 1f; // Как часто записывать данные
    private float recordTimer;
    public int maxHistoryPoints = 200; // Ограничение размера истории

    // Связь с UI
    public StatisticsPanel statsPanelUI;

    void Start()
    {
        if (simManager == null) simManager = FindFirstObjectByType<SimulationManager>();
        if (statsPanelUI == null) statsPanelUI = FindFirstObjectByType<StatisticsPanel>();

        recordTimer = dataRecordInterval;
        if (statsPanelUI) statsPanelUI.Initialize(this);
        else Debug.LogWarning("StatisticsPanelUI not found or assigned in StatisticsManager.");
    }

    void Update()
    {
        if (simManager == null || !simManager.isRunning || Time.timeScale == 0f) return;

        recordTimer -= Time.deltaTime;
        if (recordTimer <= 0)
        {
            CollectCurrentFrameData();
            recordTimer = dataRecordInterval;
            if (statsPanelUI) statsPanelUI.UpdatePanel();
        }
    }

    public void ResetStatistics()
    {
        populationHistory.Clear();
        averageEnergyHistory.Clear();
        averageAgeHistory.Clear();
        averageMantleLengthHistory.Clear();

        currentPopulationCount = 0;
        currentAverageEnergy = 0;
        currentAverageAge = 0;
        currentAverageMantleLength = 0;
        maxFitnessLastGeneration = 0;

        Debug.Log("Statistics Reset.");
        if (statsPanelUI) statsPanelUI.ClearGraphs(); // Сообщаем UI очистить графики
    }

    public void RecordMaxFitness(List<Genome> genomes)
    {
        if (genomes == null || genomes.Count == 0) {
            maxFitnessLastGeneration = 0;
            return;
        }
        maxFitnessLastGeneration = genomes.Max(g => g.fitness);
    }


    void CollectCurrentFrameData()
    {
        // Используем список агентов из SimulationManager, если он доступен и актуален
        // Либо ищем заново, но это менее эффективно
        SquidAgent[] currentAgentComponents = FindObjectsByType<SquidAgent>(FindObjectsSortMode.None);
        currentPopulationCount = currentAgentComponents.Length;
        populationHistory.Add(currentPopulationCount);

        if (currentPopulationCount == 0)
        {
            currentAverageEnergy = 0;
            currentAverageAge = 0;
            currentAverageMantleLength = 0;
        }
        else
        {
            float totalEnergy = 0;
            float totalAge = 0;
            float totalMantleLength = 0;
            foreach (SquidAgent agent in currentAgentComponents)
            {
                if (agent.TryGetComponent<SquidMetabolism>(out var meta)) {
                    totalEnergy += meta.CurrentEnergy;
                    totalAge += meta.Age;
                }
                if (agent.genome != null) {
                    totalMantleLength += agent.genome.mantleLength;
                }
            }
            currentAverageEnergy = totalEnergy / currentPopulationCount;
            currentAverageAge = totalAge / currentPopulationCount;
            currentAverageMantleLength = totalMantleLength / currentPopulationCount;
        }

        averageEnergyHistory.Add(currentAverageEnergy);
        averageAgeHistory.Add(currentAverageAge);
        averageMantleLengthHistory.Add(currentAverageMantleLength);

        TrimHistoryList(populationHistory);
        TrimHistoryList(averageEnergyHistory);
        TrimHistoryList(averageAgeHistory);
        TrimHistoryList(averageMantleLengthHistory);
    }

    void TrimHistoryList(List<float> list)
    {
        while (list.Count > maxHistoryPoints)
        {
            list.RemoveAt(0);
        }
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Core/GeneticAlgorithmManager.cs"
// ${ASSETS_PATH}/Core/GeneticAlgorithmManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GeneticAlgorithmManager : MonoBehaviour
{
    [Header("GA Parameters")]
    public float mutationRate = 0.05f;
    public float mutationAmount = 0.1f;
    public int tournamentSize = 3;
    public int elitismCount = 1;

    private StatisticsManager statsManager;

    void Start() {
        statsManager = FindFirstObjectByType<StatisticsManager>();
    }

    public List<Genome> EvolvePopulation(List<Genome> parentGenomes)
    {
        if (statsManager != null) statsManager.RecordMaxFitness(parentGenomes); // Записываем лучший фитнес до отбора

        List<Genome> newPopulation = new List<Genome>();
        int populationSize = parentGenomes.Count;

        if (populationSize == 0)
        {
            Debug.LogWarning("Parent population for GA is empty!");
            return newPopulation;
        }

        List<Genome> sortedParents = parentGenomes.OrderByDescending(g => g.fitness).ToList();

        // 1. Элитизм
        for (int i = 0; i < Mathf.Min(elitismCount, sortedParents.Count); i++)
        {
            newPopulation.Add(new Genome(sortedParents[i])); // Копия лучшего генома
        }

        // 2. Заполнение остальной популяции
        while (newPopulation.Count < populationSize)
        {
            Genome parent1 = TournamentSelection(sortedParents);
            Genome parent2 = TournamentSelection(sortedParents);

            Genome offspring = Crossover(parent1, parent2);
            Mutate(offspring);

            newPopulation.Add(offspring);
        }
        Debug.Log($"GA: Evolved {newPopulation.Count} new genomes.");
        return newPopulation;
    }

    Genome TournamentSelection(List<Genome> population)
    {
        if (population == null || population.Count == 0) {
             Debug.LogError("TournamentSelection called with empty population!");
             // Возвращаем случайный новый геном, чтобы избежать NullReferenceException, но это плохая ситуация
             SimulationManager sm = FindFirstObjectByType<SimulationManager>();
             return new Genome(sm.numInputNodes, sm.numHiddenNodes, sm.numOutputNodes);
        }

        Genome bestInTournament = population[Random.Range(0, population.Count)]; // Инициализируем случайным
        for (int i = 1; i < tournamentSize; i++) // Начинаем с 1, т.к. уже выбрали одного
        {
            Genome candidate = population[Random.Range(0, population.Count)];
            if (candidate.fitness > bestInTournament.fitness)
            {
                bestInTournament = candidate;
            }
        }
        return new Genome(bestInTournament); // Возвращаем копию
    }

    Genome Crossover(Genome parent1, Genome parent2)
    {
        // Используем геном родителя1 как основу для потомка
        Genome offspring = new Genome(parent1); // Копирующий конструктор уже есть
        
        // Скрещивание весов НС (одноточечное)
        if (parent1.nnWeights.Count == parent2.nnWeights.Count) { // Убедимся, что структуры НС одинаковы
            int crossoverPointWeights = Random.Range(0, parent1.nnWeights.Count);
            for (int i = crossoverPointWeights; i < parent1.nnWeights.Count; i++) // Начинаем с точки кроссовера
            {
                offspring.nnWeights[i] = parent2.nnWeights[i];
            }
        } else {
            Debug.LogWarning("NN weights count mismatch during crossover. Offspring inherits parent1's weights fully.");
        }


        // Скрещивание физических генов (пример: усреднение или 50/50 шанс)
        // Для каждого гена решаем, от какого родителя он будет, или смешиваем
        if (Random.value < 0.5f) offspring.mantleLength = parent2.mantleLength;
        offspring.mantleMaxDiameter = (parent1.mantleMaxDiameter + parent2.mantleMaxDiameter) / 2f; // Усреднение
        if (Random.value < 0.5f) offspring.mantleColor = parent2.mantleColor; // 50/50 шанс
        
        if (Random.value < 0.5f) offspring.baseSwimTentacleLength = parent2.baseSwimTentacleLength;
        offspring.swimTentacleThickness = (parent1.swimTentacleThickness + parent2.swimTentacleThickness) / 2f;
        
        offspring.baseGraspTentacleLength = (parent1.baseGraspTentacleLength + parent2.baseGraspTentacleLength) / 2f;
        if (Random.value < 0.5f) offspring.maxGraspTentacleLength = parent2.maxGraspTentacleLength;
        if (Random.value < 0.5f) offspring.graspTentacleThickness = parent2.graspTentacleThickness;
        
        offspring.eyeSize = (parent1.eyeSize + parent2.eyeSize) / 2f;
        if (Random.value < 0.5f) offspring.metabolismRate = parent2.metabolismRate;
        if (Random.value < 0.5f) offspring.maxAge = parent2.maxAge;
        
        offspring.aggression = (parent1.aggression + parent2.aggression) / 2f;
        if (Random.value < 0.5f) offspring.foodPreference = parent2.foodPreference;

        return offspring;
    }

    public void Mutate(Genome genome) // Сделал public, чтобы можно было вызвать из SquidMetabolism
    {
        // Мутация весов НС
        for (int i = 0; i < genome.nnWeights.Count; i++)
        {
            if (Random.value < mutationRate)
            {
                genome.nnWeights[i] += Random.Range(-mutationAmount, mutationAmount);
                genome.nnWeights[i] = Mathf.Clamp(genome.nnWeights[i], -1f, 1f);
            }
        }

        // Мутация физических генов
        if (Random.value < mutationRate) genome.mantleLength += Random.Range(-mutationAmount * 0.2f, mutationAmount * 0.2f);
        genome.mantleLength = Mathf.Max(0.2f, genome.mantleLength);

        if (Random.value < mutationRate) genome.mantleMaxDiameter += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        genome.mantleMaxDiameter = Mathf.Max(0.1f, genome.mantleMaxDiameter);
        
        if (Random.value < mutationRate) genome.baseSwimTentacleLength += Random.Range(-mutationAmount * 0.15f, mutationAmount * 0.15f);
        genome.baseSwimTentacleLength = Mathf.Max(0.1f, genome.baseSwimTentacleLength);

        // ... и так далее для других физических генов
        if (Random.value < mutationRate) genome.eyeSize += Random.Range(-mutationAmount * 0.05f, mutationAmount * 0.05f);
        genome.eyeSize = Mathf.Max(0.05f, genome.eyeSize);

        if (Random.value < mutationRate)
            genome.mantleColor = new Color(
                Mathf.Clamp01(genome.mantleColor.r + Random.Range(-mutationAmount * 2f, mutationAmount * 2f)), // Цвет может меняться сильнее
                Mathf.Clamp01(genome.mantleColor.g + Random.Range(-mutationAmount * 2f, mutationAmount * 2f)),
                Mathf.Clamp01(genome.mantleColor.b + Random.Range(-mutationAmount * 2f, mutationAmount * 2f))
            );
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Core/InputManager.cs"
// ${ASSETS_PATH}/Core/InputManager.cs
using UnityEngine;

public class InputManager : MonoBehaviour
{
    private SimulationManager simManager;
    private Camera mainCamera;
    private UIManager uiManager; // Для взаимодействия с UI при кликах

    [Header("Camera Controls")]
    public float panSpeed = 20f;
    public float scrollSpeed = 20f;
    public float minZoomOrthographic = 2f;
    public float maxZoomOrthographic = 50f;

    [Header("Divine Tools (Example)")]
    public GameObject plantFoodPrefabToSpawn; // Назначить в инспекторе
    public bool divineToolsEnabled = false; // Включать/выключать через UI

    void Start()
    {
        simManager = FindFirstObjectByType<SimulationManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        mainCamera = Camera.main;

        if (mainCamera == null) {
            Debug.LogError("Main Camera not found by InputManager!");
            enabled = false;
        }
    }

    void Update()
    {
        HandleCameraControls();
        // HandleSimulationControls(); // Перенесено в UIManager для кнопок
        HandleAgentSelectionAndDivineTools();
    }

    void HandleCameraControls()
    {
        if (mainCamera == null) return;

        // Проверяем, не находится ли курсор над UI элементом, чтобы не двигать камеру при кликах по UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        // Используем стандартные оси, если они настроены, или прямые клавиши
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;


        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            // Используем Time.unscaledDeltaTime, чтобы камера двигалась даже на паузе
            Vector3 move = new Vector3(horizontal, vertical, 0) * panSpeed * Time.unscaledDeltaTime;
            mainCamera.transform.Translate(move, Space.World);
        }
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (mainCamera.orthographic && Mathf.Abs(scroll) > 0.01f)
        {
            mainCamera.orthographicSize -= scroll * scrollSpeed * Time.unscaledDeltaTime * 10f;
            mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize, minZoomOrthographic, maxZoomOrthographic);
        }
    }

    void HandleAgentSelectionAndDivineTools()
    {
        if (mainCamera == null) return;

        // Не обрабатывать клики мыши, если курсор над UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0)) // Левая кнопка - выбор агента
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            if (hit.collider != null)
            {
                SquidAgent agent = hit.collider.GetComponentInParent<SquidAgent>(); // GetComponentInParent на случай, если кликнули по щупальцу
                if (agent != null && uiManager != null)
                {
                    uiManager.SelectAgentForInspector(agent);
                }
            } else {
                 if (uiManager != null) uiManager.DeselectAgentForInspector();
            }
        }

        if (divineToolsEnabled && plantFoodPrefabToSpawn != null) {
            if (Input.GetMouseButtonDown(1)) // Правая кнопка - спавн еды (пример)
            {
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0;
                Instantiate(plantFoodPrefabToSpawn, mouseWorldPos, Quaternion.identity);
                if(FindFirstObjectByType<EventLogPanel>() != null)
                    FindFirstObjectByType<EventLogPanel>().AddLogMessage("Divine intervention: Plant food spawned.");
            }
        }
    }
}
EOF

# --- Assets/Agents ---
cat <<EOF > "${ASSETS_PATH}/Agents/SquidAgent.cs"
// ${ASSETS_PATH}/Agents/SquidAgent.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // Добавим сразу
[RequireComponent(typeof(SquidBrain), typeof(SquidSenses), typeof(SquidMovement))]
[RequireComponent(typeof(SquidMetabolism), typeof(SquidMeshGenerator), typeof(TentacleController))]
public class SquidAgent : MonoBehaviour
{
    public Genome genome { get; private set; } // Сделал private set для большей инкапсуляции
    private SimulationManager simManager;

    // Компоненты кальмара
    private SquidBrain brain;
    private SquidSenses senses;
    private SquidMovement movement;
    private SquidMetabolism metabolism;
    private SquidMeshGenerator meshGenerator;
    private TentacleController tentacleController;
    private Rigidbody2D rb;


    public bool isInitialized { get; private set; } = false;

    void Awake() // Получаем компоненты в Awake, чтобы они были доступны для Initialize
    {
        brain = GetComponent<SquidBrain>();
        senses = GetComponent<SquidSenses>();
        movement = GetComponent<SquidMovement>();
        metabolism = GetComponent<SquidMetabolism>();
        meshGenerator = GetComponent<SquidMeshGenerator>();
        tentacleController = GetComponent<TentacleController>();
        rb = GetComponent<Rigidbody2D>();

        if (brain == null || senses == null || movement == null || metabolism == null || meshGenerator == null || tentacleController == null || rb == null)
        {
            Debug.LogError($"SquidAgent {gameObject.name} is missing one or more required components in Awake!");
            enabled = false; // Отключаем, если что-то не так
        }
    }


    public void Initialize(Genome agentGenome, SimulationManager manager)
    {
        if (agentGenome == null || manager == null) {
            Debug.LogError($"Initialization failed for {gameObject.name}: Genome or SimManager is null.");
            Destroy(gameObject); // Уничтожаем, если не можем инициализировать
            return;
        }

        this.genome = agentGenome;
        this.simManager = manager;
        
        // Инициализация компонентов с передачей генома и менеджера
        // Порядок может быть важен, если компоненты зависят друг от друга при инициализации

        // Сначала те, кто не зависит от других или нужен другим для инициализации мешей
        meshGenerator.Initialize(this.transform); // Передаем transform для создания дочерних объектов мешей
        meshGenerator.GenerateInitialMeshes(genome); // Генерируем меши ДО инициализации контроллеров, которые могут их использовать

        // Затем остальные
        brain.Initialize(genome);
        senses.Initialize(genome, transform); // Передаем transform для расчетов направления
        movement.Initialize(genome, rb);
        metabolism.Initialize(genome, simManager, this);
        tentacleController.Initialize(genome, this.transform, meshGenerator); // После генерации мешей щупалец

        isInitialized = true;
        gameObject.name = "Squid_" + GetInstanceID();
    }

    void FixedUpdate() // Используем FixedUpdate для физики и основной логики, если движение физическое
    {
        if (!isInitialized || simManager == null || !simManager.isRunning || Time.timeScale == 0f) return;

        // 1. Сбор сенсорной информации
        var sensoryInput = senses.GatherSenses();
        
        // 2. Принятие решения Нейросетью
        var brainOutput = brain.ProcessInputs(sensoryInput);

        // 3. Выполнение действий
        movement.ExecuteMovement(brainOutput);
        tentacleController.UpdateAllTentacles(brainOutput, senses.GetTargetInfo());

        // 4. Метаболизм
        metabolism.UpdateMetabolism(); // Расход энергии, старение, проверка на смерть/размножение

        // 5. Взаимодействие с едой/атака
        HandleInteractions(brainOutput);

        // 6. Обновление Визуализации (если меши динамические)
        // meshGenerator.UpdateMeshes(genome, brainOutput); // Раскомментировать, если есть динамическое обновление мешей
    }

    void HandleInteractions(SquidBrain.BrainOutput output)
    {
        if (output.shouldEat && tentacleController.IsHoldingFood())
        {
            Food foodItem = tentacleController.ConsumeHeldFood(); // Щупальце отдает еду
            if (foodItem != null) // foodItem уже уничтожен в TentacleController
            {
                metabolism.Eat(foodItem.energyValue, foodItem.type); // Передаем значение и тип
            }
        }
        
        // TODO: Логика атаки
        // if (output.graspTentacle1TryAttack && tentacleController.CanTentacleAttack(0)) {
        //     SquidAgent targetAgent = tentacleController.AttemptAttack(0);
        //     if (targetAgent != null) {
        //         targetAgent.GetComponent<SquidMetabolism>().TakeDamage(genome.attackPower);
        //     }
        // }
    }
    
    public void ReportDeath() // Вызывается из SquidMetabolism
    {
        if (!isInitialized) return; // Избегаем двойного вызова или вызова на неинициализированном
        isInitialized = false; // Предотвращаем дальнейшие действия

        // Фитнес должен быть обновлен в genome до этого момента (в SquidMetabolism)
        simManager.ReportAgentDeath(this, new Genome(genome)); // Передаем копию генома с финальным фитнесом
        Destroy(gameObject);
    }

    public void ReportReproduction(Genome offspringGenome) // Вызывается из SquidMetabolism
    {
        simManager.ReportAgentReproduction(this, offspringGenome);
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/Genome.cs"
// ${ASSETS_PATH}/Agents/Genome.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Genome
{
    // --- Neural Network Structure ---
    public int inputNodes;
    public int hiddenNodes;
    public int outputNodes;
    public List<float> nnWeights;

    // --- Physical Traits (Examples) ---
    [Header("Physical Traits")]
    public float mantleLength = 1f;
    public float mantleMaxDiameter = 0.4f;
    public Color mantleColor = Color.grey;

    public float baseSwimTentacleLength = 0.8f;
    public float swimTentacleThickness = 0.08f;
    // public const int numSwimTentacles = 8; // Константа

    public float baseGraspTentacleLength = 1.0f;
    public float maxGraspTentacleLengthFactor = 2.0f; // Множитель к базовой длине
    public float graspTentacleThickness = 0.05f;
    // public const int numGraspTentacles = 2; // Константа

    public float eyeSize = 0.15f;
    // public Color eyeColor = Color.black;

    // --- Metabolic & Lifespan Traits ---
    [Header("Metabolic Traits")]
    public float metabolismRateFactor = 1f; // Множитель к базовому расходу
    public float maxAge = 100f; // Секунд
    public float energyToReproduceThresholdFactor = 0.7f; // % от макс. энергии
    public float energyCostOfReproductionFactor = 0.4f; // % от макс. энергии

    // --- Behavioral Traits ---
    [Header("Behavioral Traits")]
    public float aggression = 0.2f; // 0-1
    public float foodPreference = 0.5f; // 0 (растения) - 1 (мясо)
    // public float cautionFactor = 0.5f; // Насколько осторожен


    public float fitness; // Оценка приспособленности

    // Конструктор для НС и дефолтных физических параметров
    public Genome(int inputs, int hiddens, int outputs)
    {
        inputNodes = inputs;
        hiddenNodes = hiddens;
        outputNodes = outputs;
        nnWeights = new List<float>();
        InitializeRandomNNWeights();
        // InitializeRandomPhysicalGenes(); // Вызывать явно после создания объекта, чтобы не перезаписывать при копировании
        fitness = 0f;
    }

    // Копирующий конструктор
    public Genome(Genome parentGenome)
    {
        inputNodes = parentGenome.inputNodes;
        hiddenNodes = parentGenome.hiddenNodes;
        outputNodes = parentGenome.outputNodes;
        nnWeights = new List<float>(parentGenome.nnWeights);

        // Копирование всех остальных генов
        mantleLength = parentGenome.mantleLength;
        mantleMaxDiameter = parentGenome.mantleMaxDiameter;
        mantleColor = parentGenome.mantleColor;
        baseSwimTentacleLength = parentGenome.baseSwimTentacleLength;
        swimTentacleThickness = parentGenome.swimTentacleThickness;
        baseGraspTentacleLength = parentGenome.baseGraspTentacleLength;
        maxGraspTentacleLengthFactor = parentGenome.maxGraspTentacleLengthFactor;
        graspTentacleThickness = parentGenome.graspTentacleThickness;
        eyeSize = parentGenome.eyeSize;
        metabolismRateFactor = parentGenome.metabolismRateFactor;
        maxAge = parentGenome.maxAge;
        energyToReproduceThresholdFactor = parentGenome.energyToReproduceThresholdFactor;
        energyCostOfReproductionFactor = parentGenome.energyCostOfReproductionFactor;
        aggression = parentGenome.aggression;
        foodPreference = parentGenome.foodPreference;
        
        fitness = 0f; // Фитнес не наследуется напрямую
    }

    void InitializeRandomNNWeights()
    {
        int expectedWeights = (inputNodes * hiddenNodes) + hiddenNodes + (hiddenNodes * outputNodes) + outputNodes;
        if (nnWeights.Count != expectedWeights && nnWeights.Count != 0) { // Если уже есть веса, но не то кол-во
            Debug.LogWarning($"NNWeights count ({nnWeights.Count}) differs from expected ({expectedWeights}). Clearing and reinitializing.");
            nnWeights.Clear();
        }
        if(nnWeights.Count == 0) { // Только если список пуст
            for (int i = 0; i < expectedWeights; i++)
            {
                nnWeights.Add(Random.Range(-1f, 1f));
            }
        }
    }

    // Метод для инициализации физических генов случайными значениями (вызывать для первого поколения)
    public void InitializeRandomPhysicalGenes()
    {
        mantleLength = Random.Range(0.7f, 1.5f);
        mantleMaxDiameter = Random.Range(0.3f, 0.7f) * mantleLength * 0.5f; // Диаметр зависит от длины
        mantleColor = new Color(Random.value, Random.value, Random.value, 1f);

        baseSwimTentacleLength = Random.Range(0.4f, 0.8f) * mantleLength;
        swimTentacleThickness = Random.Range(0.05f, 0.1f) * mantleMaxDiameter * 0.3f;

        baseGraspTentacleLength = Random.Range(0.6f, 1.2f) * mantleLength;
        maxGraspTentacleLengthFactor = Random.Range(1.5f, 3.5f);
        graspTentacleThickness = Random.Range(0.03f, 0.08f) * mantleMaxDiameter * 0.25f;

        eyeSize = Random.Range(0.1f, 0.2f) * mantleMaxDiameter;
        metabolismRateFactor = Random.Range(0.8f, 1.3f);
        maxAge = Random.Range(60f, 180f);
        energyToReproduceThresholdFactor = Random.Range(0.6f, 0.85f);
        energyCostOfReproductionFactor = Random.Range(0.3f, 0.5f);

        aggression = Random.Range(0.05f, 0.6f);
        foodPreference = Random.value; // 0 до 1
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/SquidBrain.cs"
// ${ASSETS_PATH}/Agents/SquidBrain.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidBrain : MonoBehaviour
{
    private Genome genome;
    private NeuralNetwork nn;

    // Структура для типизированного вывода НС
    public struct BrainOutput
    {
        // Движение
        public float moveForward;    // -1 (назад) to 1 (вперед)
        public float turn;           // -1 (влево) to 1 (вправо)
        // public float moveIntensity;  // 0 to 1 (если нужно отдельное управление силой)

        // Хватательные щупальца (0 - левое, 1 - правое)
        public Vector2 graspTentacleTargetDir0; // Нормализованное направление от кальмара
        public float graspTentacleExtend0;      // 0 (втянуто) to 1 (макс вытянуто)
        public bool graspTentacleTryGrasp0;
        public bool graspTentacleTryAttack0; // TODO: Для атаки

        public Vector2 graspTentacleTargetDir1;
        public float graspTentacleExtend1;
        public bool graspTentacleTryGrasp1;
        public bool graspTentacleTryAttack1; // TODO: Для атаки
        
        public bool shouldEat;
        public bool shouldReproduce;
    }

    public void Initialize(Genome agentGenome)
    {
        if (agentGenome == null) {
            Debug.LogError("SquidBrain initialized with null genome!");
            enabled = false; return;
        }
        this.genome = agentGenome;
        this.nn = new NeuralNetwork(genome); // Передаем весь геном, НС сама возьмет нужные параметры
    }

    public BrainOutput ProcessInputs(List<float> inputs)
    {
        if (nn == null)
        {
            Debug.LogError("Neural Network in SquidBrain is not initialized!");
            return new BrainOutput();
        }
        if (inputs == null) {
            Debug.LogError("SquidBrain received null inputs!");
            return new BrainOutput();
        }


        float[] nnOutputsArray = nn.FeedForward(inputs.ToArray());
        BrainOutput output = new BrainOutput();

        // Распределение выходов НС по полям BrainOutput
        // Это должно строго соответствовать genome.outputNodes и их назначению
        int outIdx = 0;
        // Движение (2 нейрона)
        if (nnOutputsArray.Length > outIdx) output.moveForward = nnOutputsArray[outIdx++];
        if (nnOutputsArray.Length > outIdx) output.turn = nnOutputsArray[outIdx++];
        // output.moveIntensity = Mathf.Clamp01((output.moveForward + 1f)/2f); // Пример: интенсивность от движения вперед

        // Щупальце 0 (3 или 4 нейрона)
        if (nnOutputsArray.Length > outIdx + 2) { // dirX, dirY, extend
            output.graspTentacleTargetDir0 = new Vector2(nnOutputsArray[outIdx++], nnOutputsArray[outIdx++]).normalized;
            output.graspTentacleExtend0 = Mathf.Clamp01((nnOutputsArray[outIdx++] + 1f) / 2f); // Tanh -1..1 -> 0..1
        }
        if (nnOutputsArray.Length > outIdx) output.graspTentacleTryGrasp0 = nnOutputsArray[outIdx++] > 0.0f; // Порог для Tanh
        // if (nnOutputsArray.Length > outIdx) output.graspTentacleTryAttack0 = nnOutputsArray[outIdx++] > 0.5f;


        // Щупальце 1 (3 или 4 нейрона)
         if (nnOutputsArray.Length > outIdx + 2) {
            output.graspTentacleTargetDir1 = new Vector2(nnOutputsArray[outIdx++], nnOutputsArray[outIdx++]).normalized;
            output.graspTentacleExtend1 = Mathf.Clamp01((nnOutputsArray[outIdx++] + 1f) / 2f);
        }
        if (nnOutputsArray.Length > outIdx) output.graspTentacleTryGrasp1 = nnOutputsArray[outIdx++] > 0.0f;
        // if (nnOutputsArray.Length > outIdx) output.graspTentacleTryAttack1 = nnOutputsArray[outIdx++] > 0.5f;

        // Другие действия
        if (nnOutputsArray.Length > outIdx) output.shouldEat = nnOutputsArray[outIdx++] > 0.5f;
        if (nnOutputsArray.Length > outIdx) output.shouldReproduce = nnOutputsArray[outIdx++] > 0.7f; // Более высокий порог для размножения

        return output;
    }
}

// NeuralNetwork класс (оставляем здесь для простоты, можно вынести)
public class NeuralNetwork
{
    private List<float> weights;
    private int numInputs, numHidden, numOutputs;

    public NeuralNetwork(Genome genome) // Принимает весь геном
    {
        if (genome == null) {
             Debug.LogError("NeuralNetwork created with null Genome!");
             return;
        }
        numInputs = genome.inputNodes;
        numHidden = genome.hiddenNodes;
        numOutputs = genome.outputNodes;
        
        // Проверка и инициализация весов
        int expectedWeights = (numInputs * numHidden) + numHidden + (numHidden * numOutputs) + numOutputs;
        if (genome.nnWeights == null || genome.nnWeights.Count != expectedWeights)
        {
            Debug.LogWarning($"NN Weight mismatch or null: Expected {expectedWeights}, Got {(genome.nnWeights == null ? "null" : genome.nnWeights.Count.ToString())}. Reinitializing weights for this NN instance.");
            weights = new List<float>();
            for(int i=0; i<expectedWeights; ++i) weights.Add(Random.Range(-1f, 1f));
            // Важно: это не меняет геном, только локальные веса этой НС. Геном должен быть корректен.
            // Если геном изначально неверен, он будет таким для всех НС, использующих его.
        } else {
             weights = new List<float>(genome.nnWeights); // Копируем веса из генома
        }
    }

    public float[] FeedForward(float[] inputs)
    {
        if (weights == null || weights.Count == 0) {
            Debug.LogError("NN FeedForward called with uninitialized weights!");
            return new float[numOutputs];
        }
        if (inputs.Length != numInputs)
        {
            Debug.LogError($"NN input size mismatch! Expected {numInputs}, Got {inputs.Length}");
            return new float[numOutputs];
        }

        float[] hiddenOutputs = new float[numHidden];
        float[] finalOutputs = new float[numOutputs];
        int weightIndex = 0;

        // Входной -> Скрытый слой
        for (int i = 0; i < numHidden; i++)
        {
            float sum = 0;
            for (int j = 0; j < numInputs; j++)
            {
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (input to hidden)"); return finalOutputs; }
                sum += inputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden bias)"); return finalOutputs; }
            sum += weights[weightIndex++]; // Смещение (bias)
            hiddenOutputs[i] = Tanh(sum);
        }

        // Скрытый -> Выходной слой
        for (int i = 0; i < numOutputs; i++)
        {
            float sum = 0;
            for (int j = 0; j < numHidden; j++)
            {
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden to output)"); return finalOutputs; }
                sum += hiddenOutputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (output bias)"); return finalOutputs; }
            sum += weights[weightIndex++]; // Смещение (bias)
            finalOutputs[i] = Tanh(sum);
        }
        return finalOutputs;
    }

    private float Tanh(float x) { return (float)System.Math.Tanh(x); }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/SquidSenses.cs"
// ${ASSETS_PATH}/Agents/SquidSenses.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SquidSenses : MonoBehaviour
{
    [Header("Base Sensory Parameters")]
    public float baseSightRadius = 10f;
    public float baseSightAngle = 120f; // Угол обзора в градусах
    
    [Header("Layers")]
    public LayerMask foodLayer;
    public LayerMask agentLayer;
    public LayerMask obstacleLayer;

    private Genome genome;
    private Transform agentTransform; // Кэшируем для производительности

    // Динамические параметры сенсоров, зависящие от генома
    private float currentSightRadius;
    private float currentSightAngle;

    // Для отладки и передачи информации о целях
    private List<Transform> visibleFoodDebug = new List<Transform>();
    private List<Transform> visibleAgentsDebug = new List<Transform>();
    private Dictionary<GameObject, float> targetPriorities = new Dictionary<GameObject, float>();


    public void Initialize(Genome agentGenome, Transform ownerTransform)
    {
        this.genome = agentGenome;
        this.agentTransform = ownerTransform;

        if (genome == null || agentTransform == null) {
            Debug.LogError("SquidSenses initialized with null genome or transform!");
            enabled = false; return;
        }

        // Настройка сенсоров на основе генома (например, размер глаз влияет на радиус/угол)
        currentSightRadius = baseSightRadius * (1f + genome.eyeSize * 2f); // Глаза влияют на радиус
        currentSightAngle = baseSightAngle * (1f + genome.eyeSize * 0.5f); // И немного на угол
    }

    public List<float> GatherSenses()
    {
        if (!enabled) return new List<float>(); // Если не инициализирован

        List<float> inputs = new List<float>();
        visibleFoodDebug.Clear();
        visibleAgentsDebug.Clear();
        targetPriorities.Clear();

        // 1. Сенсоры еды (N ближайших в поле зрения)
        // Параметр count - сколько ближайших объектов каждого типа мы хотим передать в НС
        AddTargetSensorInputs(inputs, foodLayer, 2, ref visibleFoodDebug, "Food");

        // 2. Сенсоры других агентов (N ближайших в поле зрения)
        AddTargetSensorInputs(inputs, agentLayer, 1, ref visibleAgentsDebug, "Agent");

        // 3. Сенсоры стен/препятствий (например, 3 луча: вперед, вперед-влево, вперед-вправо)
        AddObstacleSensorInputs(inputs);

        // 4. Собственные параметры
        if (TryGetComponent<SquidMetabolism>(out var metabolism))
        {
            inputs.Add(Mathf.Clamp01(metabolism.CurrentEnergy / metabolism.maxEnergyGeno));
            inputs.Add(Mathf.Clamp01(metabolism.Age / genome.maxAge));
        } else {
            inputs.Add(0.5f); inputs.Add(0f); // Default if no metabolism
        }
        
        // 5. Состояние щупалец (например, занято ли щупальце, держит ли еду)
        // TODO: Это потребует методов в TentacleController
        // TentacleController tc = GetComponent<TentacleController>();
        // if (tc != null && tc.graspingTentacles.Count > 0) {
        //    inputs.Add(tc.graspingTentacles[0].IsBusy() ? 1f : 0f);
        //    inputs.Add(tc.graspingTentacles[0].IsHoldingFoodItem() ? 1f : 0f);
        // } else { inputs.Add(0f); inputs.Add(0f); }
        // if (tc != null && tc.graspingTentacles.Count > 1) {
        //    inputs.Add(tc.graspingTentacles[1].IsBusy() ? 1f : 0f);
        //    inputs.Add(tc.graspingTentacles[1].IsHoldingFoodItem() ? 1f : 0f);
        // } else { inputs.Add(0f); inputs.Add(0f); }


        // Убедимся, что количество входов соответствует ожиданиям НС (из генома)
        int expectedInputs = genome.inputNodes;
        while (inputs.Count < expectedInputs)
        {
            inputs.Add(0f); // Дополняем нулями (означает "ничего не обнаружено" для этих сенсоров)
        }
        if (inputs.Count > expectedInputs)
        {
            Debug.LogWarning($"Senses generated {inputs.Count} inputs, but NN expects {expectedInputs}. Truncating.");
            inputs = inputs.Take(expectedInputs).ToList();
        }

        return inputs;
    }
    
    // Для TentacleController, чтобы он знал о целях
    public Dictionary<GameObject, float> GetTargetInfo()
    {
        return targetPriorities;
    }

    void AddTargetSensorInputs(List<float> inputs, LayerMask layer, int count, ref List<Transform> visibleDebugList, string targetType)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(agentTransform.position, currentSightRadius, layer);
        List<(Transform item, float dist, Vector2 dirRel, float priority)> sortedTargets =
            new List<(Transform, float, Vector2, float)>();

        foreach (var col in colliders)
        {
            if (targetType == "Agent" && col.gameObject == this.gameObject) continue; // Агент не видит себя

            Vector3 directionToTargetWorld = col.transform.position - agentTransform.position;
            float angleToTarget = Vector2.Angle(agentTransform.up, directionToTargetWorld.normalized);

            if (angleToTarget < currentSightAngle / 2f) // В поле зрения
            {
                float distance = directionToTargetWorld.magnitude;
                if (distance < currentSightRadius && distance > 0.01f) // Проверка дистанции и не нулевой дистанции
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
        // Сортируем по приоритету (убывающий), затем по дистанции (возрастающей)
        sortedTargets = sortedTargets.OrderByDescending(t => t.priority).ThenBy(t => t.dist).ToList();

        for (int i = 0; i < count; i++)
        {
            if (i < sortedTargets.Count)
            {
                inputs.Add(Mathf.Clamp01(sortedTargets[i].dist / currentSightRadius)); // Норм. расстояние
                inputs.Add(sortedTargets[i].dirRel.x); // -1 (слева) to 1 (справа)
                inputs.Add(sortedTargets[i].dirRel.y); // -1 (сзади) to 1 (впереди)
                // Дополнительный вход: тип цели или ее "ценность" (1 для еды, 0.5 для другого агента и т.д.)
                inputs.Add(targetType == "Food" ? 1.0f : (targetType == "Agent" ? 0.5f : 0f) );
                if (visibleDebugList != null) visibleDebugList.Add(sortedTargets[i].item);
            }
            else // Если целей меньше, чем count
            {
                inputs.Add(1f); // Макс. расстояние
                inputs.Add(0f); inputs.Add(0f); // Нет направления
                inputs.Add(0f); // Нет типа цели
            }
        }
    }
    
    float CalculatePriority(GameObject target, float distance, string type)
    {
        float basePriority = 1.0f / (distance + 0.1f); // Ближе - выше приоритет
        if (type == "Food") {
            Food foodComp = target.GetComponent<Food>();
            if (foodComp != null) {
                 // Предпочтение типа пищи из генома: 0 (растения) - 1 (мясо)
                 float preferenceFactor = (foodComp.type == FoodType.Plant) ? (1f - genome.foodPreference) : genome.foodPreference;
                 basePriority *= (1f + preferenceFactor); // Увеличиваем приоритет для предпочитаемой еды
                 basePriority *= (foodComp.energyValue / 10f); // Более питательная еда приоритетнее
            }
        } else if (type == "Agent") {
            // TODO: Приоритет для других агентов (атака/избегание)
            // Можно учитывать их размер, энергию, агрессию (если видна)
            // basePriority *= (1f - genome.cautionFactor); // Менее осторожные более склонны рассматривать других агентов
        }
        return basePriority;
    }


    void AddObstacleSensorInputs(List<float> inputs)
    {
        float rayLength = currentSightRadius * 0.3f; // Лучи короче основного зрения
        // Вперед
        inputs.Add(CastObstacleRay(agentTransform.up, rayLength));
        // Вперед-Влево (например, 30 градусов)
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,30) * agentTransform.up, rayLength));
        // Вперед-Вправо (например, 30 градусов)
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,-30) * agentTransform.up, rayLength));
    }

    float CastObstacleRay(Vector2 direction, float length)
    {
        // Начинаем луч чуть впереди от центра, чтобы не задевать свой коллайдер
        Vector2 rayOrigin = (Vector2)agentTransform.position + direction * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, length, obstacleLayer);
        if (hit.collider != null)
        {
            return Mathf.Clamp01(hit.distance / length); // 0 (близко) to 1 (далеко)
        }
        return 1f; // Нет препятствия (макс. расстояние)
    }

    void OnDrawGizmosSelected()
    {
        if (agentTransform == null) agentTransform = transform; // Для работы в редакторе до инициализации

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(agentTransform.position, currentSightRadius);

        Vector3 forward = agentTransform.up;
        Vector3 leftFOVLine = Quaternion.AngleAxis(-currentSightAngle / 2, Vector3.forward) * forward;
        Vector3 rightFOVLine = Quaternion.AngleAxis(currentSightAngle / 2, Vector3.forward) * forward;
        Gizmos.DrawRay(agentTransform.position, leftFOVLine * currentSightRadius);
        Gizmos.DrawRay(agentTransform.position, rightFOVLine * currentSightRadius);

        Gizmos.color = Color.red;
        float rayLength = currentSightRadius * 0.3f;
        Gizmos.DrawRay(agentTransform.position, agentTransform.up * rayLength);
        Gizmos.DrawRay(agentTransform.position, Quaternion.Euler(0,0,30) * agentTransform.up * rayLength);
        Gizmos.DrawRay(agentTransform.position, Quaternion.Euler(0,0,-30) * agentTransform.up * rayLength);

        if (Application.isPlaying) { // Только во время игры
            Gizmos.color = Color.green;
            foreach(var food in visibleFoodDebug) if(food != null) Gizmos.DrawLine(agentTransform.position, food.position);
            Gizmos.color = Color.blue;
            foreach(var agent in visibleAgentsDebug) if(agent != null) Gizmos.DrawLine(agentTransform.position, agent.position);
        }
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/SquidMovement.cs"
// ${ASSETS_PATH}/Agents/SquidMovement.cs
using UnityEngine;

public class SquidMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Genome genome;

    [Header("Movement Parameters (Base Values)")]
    public float baseForwardForce = 10f; // Сила для движения вперед/назад
    public float turnTorque = 5f;      // Крутящий момент для поворота
    public float maxSpeedBase = 3f;    // Базовая максимальная скорость

    // Динамические параметры, зависящие от генома
    private float currentMoveForce;
    private float currentTurnTorque;
    private float currentMaxSpeed;
    
    // public Transform siphonTransform; // Для визуализации сифона, если будет

    public void Initialize(Genome agentGenome, Rigidbody2D rigidBody)
    {
        this.genome = agentGenome;
        this.rb = rigidBody;

        if (genome == null || rb == null) {
            Debug.LogError("SquidMovement initialized with null genome or Rigidbody2D!");
            enabled = false; return;
        }

        // Настройка сил/моментов на основе генома
        // Размер мантии может влиять на силу и макс. скорость
        float sizeFactor = Mathf.Clamp(genome.mantleLength, 0.5f, 2.0f); // Ограничиваем влияние размера
        currentMoveForce = baseForwardForce * sizeFactor;
        currentTurnTorque = turnTorque / Mathf.Sqrt(sizeFactor); // Большие медленнее поворачивают
        currentMaxSpeed = maxSpeedBase * Mathf.Sqrt(sizeFactor); // Большие могут быть чуть быстрее
        
        rb.drag = 1f; // Линейное сопротивление для более плавного замедления
        rb.angularDrag = 2f; // Угловое сопротивление
    }

    public void ExecuteMovement(SquidBrain.BrainOutput brainOutput)
    {
        if (!enabled || rb == null) return;

        // Движение вперед/назад
        // brainOutput.moveForward: -1 (назад) to 1 (вперед)
        Vector2 forceDirection = transform.up; // "Вперед" для кальмара - это transform.up
        rb.AddForce(forceDirection * brainOutput.moveForward * currentMoveForce * Time.fixedDeltaTime, ForceMode2D.Force); // ForceMode2D.Force для постоянного ускорения

        // Поворот
        // brainOutput.turn: -1 (влево) to 1 (вправо)
        rb.AddTorque(-brainOutput.turn * currentTurnTorque * Time.fixedDeltaTime, ForceMode2D.Force); // Знак минус, т.к. AddTorque вращает по Z против часовой

        // Ограничение максимальной скорости
        if (rb.velocity.magnitude > currentMaxSpeed)
        {
            rb.velocity = rb.velocity.normalized * currentMaxSpeed;
        }
        
        // TODO: Анимация сифона, если он есть и управляется отдельно
        // if (siphonTransform != null) {
        //    // Поворот сифона может быть основан на brainOutput.turn или отдельном выходе НС
        //    float siphonAngle = -brainOutput.turn * 45f; // Пример: сифон поворачивается до 45 градусов
        //    siphonTransform.localRotation = Quaternion.Euler(0, 0, siphonAngle);
        // }
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/TentacleController.cs"
// ${ASSETS_PATH}/Agents/TentacleController.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TentacleController : MonoBehaviour
{
    public const int NUM_SWIMMING_TENTACLES = 8;
    public const int NUM_GRASPING_TENTACLES = 2;

    public List<SwimmingTentacle> swimmingTentacles { get; private set; } = new List<SwimmingTentacle>();
    public List<GraspingTentacle> graspingTentacles { get; private set; } = new List<GraspingTentacle>();

    private Genome genome;
    private Transform agentTransform;
    private SquidMeshGenerator meshGenerator;

    private Dictionary<GameObject, float> currentSenseTargets; // Цели от SquidSenses

    public void Initialize(Genome agentGenome, Transform parentTransform, SquidMeshGenerator meshGen)
    {
        this.genome = agentGenome;
        this.agentTransform = parentTransform;
        this.meshGenerator = meshGen;

        if (genome == null || agentTransform == null || meshGenerator == null) {
            Debug.LogError("TentacleController initialized with null dependencies!");
            enabled = false; return;
        }

        // Очищаем списки на случай реинициализации (хотя обычно это не происходит)
        swimmingTentacles.Clear();
        graspingTentacles.Clear();

        // Создание и инициализация плавательных щупалец
        for (int i = 0; i < NUM_SWIMMING_TENTACLES; i++)
        {
            if (i < meshGenerator.swimmingTentacleObjects.Count && meshGenerator.swimmingTentacleObjects[i] != null)
            {
                GameObject tentacleGO = meshGenerator.swimmingTentacleObjects[i];
                SwimmingTentacle st = tentacleGO.GetComponent<SwimmingTentacle>();
                if (st == null) st = tentacleGO.AddComponent<SwimmingTentacle>(); // Добавляем, если нет
                
                st.Initialize(genome, i, tentacleGO.GetComponent<MeshFilter>(), agentTransform);
                swimmingTentacles.Add(st);
            } else {
                Debug.LogWarning($"Swimming tentacle GO missing or null for index {i}");
            }
        }

        // Создание и инициализация хватательных щупалец
        for (int i = 0; i < NUM_GRASPING_TENTACLES; i++)
        {
             if (i < meshGenerator.graspingTentacleObjects.Count && meshGenerator.graspingTentacleObjects[i] != null)
            {
                GameObject tentacleGO = meshGenerator.graspingTentacleObjects[i];
                GraspingTentacle gt = tentacleGO.GetComponent<GraspingTentacle>();
                if (gt == null) gt = tentacleGO.AddComponent<GraspingTentacle>();

                gt.Initialize(genome, i, agentTransform); // MeshFilter для хватательных может быть не нужен, если они LineRenderer
                graspingTentacles.Add(gt);
            } else {
                 Debug.LogWarning($"Grasping tentacle GO missing or null for index {i}");
            }
        }
    }

    public void UpdateAllTentacles(SquidBrain.BrainOutput brainOutput, Dictionary<GameObject, float> sensedTargets)
    {
        if (!enabled) return;
        this.currentSenseTargets = sensedTargets;

        // Обновление плавательных щупалец (для анимации волны и т.д.)
        float moveIntensity = Mathf.Abs(brainOutput.moveForward) + Mathf.Abs(brainOutput.turn) * 0.5f;
        foreach (var st in swimmingTentacles)
        {
            st.UpdateMovement(Mathf.Clamp01(moveIntensity));
        }

        // Обновление хватательных щупалец
        if (graspingTentacles.Count > 0)
        {
            GameObject target0 = SelectTargetForGraspingTentacle(0);
            graspingTentacles[0].UpdateLogic(
                target0,
                brainOutput.graspTentacleTargetDir0,
                brainOutput.graspTentacleExtend0,
                brainOutput.graspTentacleTryGrasp0,
                brainOutput.graspTentacleTryAttack0 // TODO: attack
            );
        }
        if (graspingTentacles.Count > 1)
        {
            GameObject target1 = SelectTargetForGraspingTentacle(1);
             graspingTentacles[1].UpdateLogic(
                target1,
                brainOutput.graspTentacleTargetDir1,
                brainOutput.graspTentacleExtend1,
                brainOutput.graspTentacleTryGrasp1,
                brainOutput.graspTentacleTryAttack1 // TODO: attack
            );
        }
    }

    GameObject SelectTargetForGraspingTentacle(int tentacleIndex) {
        if (currentSenseTargets == null || currentSenseTargets.Count == 0) return null;

        // Простая логика: каждое щупальце пытается взять самую приоритетную цель,
        // если она еще не захвачена другим щупальцем этого же агента.
        var sortedTargets = currentSenseTargets
            .Where(pair => pair.Key != null) // Убираем уничтоженные цели
            .OrderByDescending(pair => pair.Value)
            .ToList();

        int assignedTargetCount = 0;
        foreach(var targetEntry in sortedTargets)
        {
            bool alreadyTargetedByOther = false;
            for(int i=0; i < graspingTentacles.Count; ++i) {
                if (i != tentacleIndex && graspingTentacles[i].currentTargetGO == targetEntry.Key) {
                    alreadyTargetedByOther = true;
                    break;
                }
            }
            if (!alreadyTargetedByOther) {
                if (assignedTargetCount == tentacleIndex) { // Если это N-ая доступная цель для N-го щупальца
                    return targetEntry.Key;
                }
                assignedTargetCount++;
            }
        }
        return null; // Нет подходящей уникальной цели
    }

    public bool IsHoldingFood()
    {
        foreach (var gt in graspingTentacles)
        {
            if (gt.IsHoldingFoodItem()) return true;
        }
        return false;
    }

    public Food ConsumeHeldFood() // Потребляет еду из ПЕРВОГО щупальца, которое держит еду
    {
        foreach (var gt in graspingTentacles)
        {
            if (gt.IsHoldingFoodItem()) {
                return gt.RetrieveAndReleaseHeldFood(); // Возвращаем Food компонент и освобождаем щупальце
            }
        }
        return null;
    }
}


public class SwimmingTentacle : MonoBehaviour // Должен быть на объекте щупальца
{
    private Genome genome;
    private MeshFilter meshFilter; // Если анимируем меш
    private Transform rootTransform; // Корень щупальца (его собственный transform)
    private Transform agentBodyTransform; // Transform тела кальмара

    // Параметры для анимации волны
    private float wavePhaseOffset;
    private float waveFrequency = 5f;
    private float waveAmplitudeFactor = 0.1f; // % от длины щупальца

    private Vector3[] originalVertices; // Для сброса/расчета анимации

    public void Initialize(Genome agentGenome, int index, MeshFilter mf, Transform agentTF)
    {
        this.genome = agentGenome;
        this.meshFilter = mf;
        this.rootTransform = transform;
        this.agentBodyTransform = agentTF;

        this.wavePhaseOffset = Random.value * Mathf.PI * 2f;
        
        if (meshFilter != null && meshFilter.sharedMesh != null) { // Используем sharedMesh для получения оригинальных вершин
            originalVertices = meshFilter.sharedMesh.vertices;
            // Создаем уникальный экземпляр меша для анимации, если еще не сделано
            if (meshFilter.mesh == meshFilter.sharedMesh) {
                 meshFilter.mesh = Instantiate<Mesh>(meshFilter.sharedMesh);
            }
        } else {
            // Debug.LogWarning($"SwimmingTentacle {name} has no mesh filter or mesh for animation.");
        }
    }

    public void UpdateMovement(float moveIntensity)
    {
        if (meshFilter == null || meshFilter.mesh == null || originalVertices == null || originalVertices.Length == 0) return;
        
        // Процедурная анимация волнообразного движения
        // Смещаем вершины меша щупальца
        float time = Time.time * waveFrequency * (0.2f + moveIntensity * 0.8f); // Скорость волны зависит от интенсивности движения
        float actualAmplitude = genome.baseSwimTentacleLength * waveAmplitudeFactor;

        Vector3[] currentVertices = meshFilter.mesh.vertices; // Получаем текущие или работаем с копией originalVertices
        if (currentVertices.Length != originalVertices.Length) currentVertices = (Vector3[])originalVertices.Clone();


        for(int i=0; i < originalVertices.Length; ++i) {
            Vector3 originalVert = originalVertices[i];
            // Предполагаем, что Y - это ось вдоль Pщупальца от основания (0) к кончику (length)
            // И X - ось для волнообразного смещения
            float normalizedYPos = originalVert.y / genome.baseSwimTentacleLength; // От 0 до 1
            float phase = normalizedYPos * 5f + time + wavePhaseOffset; // 5f - количество "волн" на щупальце
            
            // Смещение только по X (или другой поперечной оси)
            // Направление смещения должно быть перпендикулярно оси щупальца и оси вращения кальмара
            // Для простоты, если щупальце выровнено по Y, смещаем по X
            currentVertices[i].x = originalVert.x + Mathf.Sin(phase) * actualAmplitude * (1f - normalizedYPos); // Амплитуда затухает к кончику
        }
        meshFilter.mesh.vertices = currentVertices;
        meshFilter.mesh.RecalculateNormals();
    }
}


public class GraspingTentacle : MonoBehaviour // Должен быть на объекте щупальца
{
    private Genome genome;
    private int tentacleId; // 0 или 1
    private Transform rootTransform; // Его собственный transform (точка крепления)
    private Transform agentBodyTransform;

    private LineRenderer lineRenderer;

    private enum State { Idle, Targeting, Extending, GraspingFood, Retracting }
    private State currentState = State.Idle;

    public GameObject currentTargetGO { get; private set; }
    private Food heldFoodItemComponent; // Компонент еды, которую держим
    private float currentExtensionRatio = 0f; // 0 (втянуто) to 1 (макс вытянуто)
    
    private float extensionRetractionSpeed = 2.5f; // Единиц (отношение к макс длине) в секунду

    public void Initialize(Genome agentGenome, int id, Transform agentTF)
    {
        this.genome = agentGenome;
        this.tentacleId = id;
        this.rootTransform = transform;
        this.agentBodyTransform = agentTF;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        lineRenderer.positionCount = 20; // Больше точек для плавного изгиба (если будем делать изгиб)
                                         // Для прямой линии достаточно 2
        lineRenderer.startWidth = genome.graspTentacleThickness * 0.8f;
        lineRenderer.endWidth = genome.graspTentacleThickness * 0.4f;
        // Настроить материал для LineRenderer в инспекторе или здесь:
        if (lineRenderer.sharedMaterial == null) {
             // lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
             // Лучше создать материал в проекте и назначить
             var renderer = GetComponentInParent<SquidMeshGenerator>()?.tentacleMaterial;
             if (renderer != null) lineRenderer.material = renderer;
             else lineRenderer.material = new Material(Shader.Find("Unlit/Color")); // Простой цветной
        }
        lineRenderer.startColor = genome.mantleColor * 0.9f;
        lineRenderer.endColor = genome.mantleColor * 0.7f;
        lineRenderer.enabled = false;
    }

    public void UpdateLogic(GameObject target, Vector2 targetDirSignal, float extendSignal, bool tryGraspSignal, bool tryAttackSignal)
    {
        HandleStateTransitions(target, targetDirSignal, extendSignal, tryGraspSignal, tryAttackSignal);
        PerformStateActions(targetDirSignal); // Передаем направление от НС для наведения
        UpdateVisuals();
    }

    void HandleStateTransitions(GameObject target, Vector2 targetDir, float extend, bool grasp, bool attack) {
        switch (currentState)
        {
            case State.Idle:
                if (target != null && extend > 0.1f && (grasp || attack)) {
                    currentTargetGO = target;
                    currentState = State.Targeting;
                } else if (extend > 0.1f) { // Просто вытягивание без цели
                    currentTargetGO = null; // Цели нет, но есть команда вытянуться
                    currentState = State.Extending;
                }
                break;
            case State.Targeting:
                if (currentTargetGO == null || !currentTargetGO.activeInHierarchy) { // Цель исчезla
                    currentState = State.Retracting; break;
                }
                if (extend < 0.1f) { currentState = State.Retracting; break; } // Команда втянуть
                // Если цель близко и есть команда на захват/атаку - переходим к вытягиванию
                currentState = State.Extending;
                break;
            case State.Extending:
                if (currentTargetGO == null && extend < 0.1f) { currentState = State.Retracting; break; }
                if (currentTargetGO != null && (!currentTargetGO.activeInHierarchy || extend < 0.1f)) {
                     currentState = State.Retracting; break;
                }
                // Проверка на достижение цели
                if (currentTargetGO != null && CheckTargetReached()) {
                    if (grasp) TryGraspTarget(); else if (attack) { /* TODO: TryAttackTarget(); */ }
                    // Если не удалось схватить/атаковать, но цель достигнута, можно остаться в Extending или Retracting
                    if (!IsHoldingFoodItem()) currentState = State.Retracting; // Не схватил, втягиваем
                }
                break;
            case State.GraspingFood:
                // Если дана команда втянуть или еда исчезла (не должно быть, т.к. мы ее "уничтожили")
                if (extend < 0.5f || heldFoodItemComponent == null) { // Втягиваем с едой или если еда пропала
                    currentState = State.Retracting;
                }
                break;
            case State.Retracting:
                if (currentExtensionRatio < 0.01f) {
                    currentState = State.Idle;
                    if (heldFoodItemComponent != null) {
                        // Еда "доставлена", но не съедена. SquidAgent решает, когда есть.
                        // Здесь можно просто сбросить heldFoodItemComponent, если агент не успел забрать.
                        // Debug.LogWarning($"Tentacle retracted with food, but agent didn't consume it yet.");
                    }
                    currentTargetGO = null; // Сбрасываем цель
                }
                break;
        }
    }

    void PerformStateActions(Vector2 targetDirSignal) {
        float targetExtension = 0f;
        Vector3 tipTargetWorldPos = rootTransform.position; // Куда стремится кончик щупальца

        if (currentState == State.Targeting || currentState == State.Extending) {
            targetExtension = 1f; // Пытаемся полностью вытянуться к цели или в указанном направлении
            if (currentTargetGO != null) {
                tipTargetWorldPos = currentTargetGO.transform.position;
            } else { // Если нет конкретной цели, используем направление от НС
                Vector3 worldTargetDir = agentBodyTransform.TransformDirection(targetDirSignal);
                tipTargetWorldPos = rootTransform.position + worldTargetDir * GetMaxPossibleLength();
            }
        } else if (currentState == State.GraspingFood) {
            targetExtension = currentExtensionRatio; // Остаемся на текущей длине или чуть втягиваем к "рту"
            // Можно добавить логику поднесения ко рту (цель - точка у рта агента)
            tipTargetWorldPos = agentBodyTransform.position + agentBodyTransform.up * 0.3f; // Примерная точка "рта"
        } else if (currentState == State.Retracting) {
            targetExtension = 0f;
            tipTargetWorldPos = rootTransform.position; // Втягиваем к основанию
        }

        currentExtensionRatio = Mathf.MoveTowards(currentExtensionRatio, targetExtension,
                                                  extensionRetractionSpeed * Time.deltaTime);
        
        // Обновляем LineRenderer или меш на основе currentExtensionRatio и tipTargetWorldPos
        // Для LineRenderer:
        if (lineRenderer != null) {
             if (currentState == State.Idle && currentExtensionRatio < 0.01f) {
                 lineRenderer.enabled = false;
             } else {
                 lineRenderer.enabled = true;
                 Vector3 startPos = rootTransform.position;
                 Vector3 endPos = CalculateTipPosition(tipTargetWorldPos);
                 
                 // Простая прямая линия
                 // lineRenderer.positionCount = 2;
                 // lineRenderer.SetPosition(0, startPos);
                 // lineRenderer.SetPosition(1, endPos);

                 // Изогнутая линия (пример с одной промежуточной точкой)
                 int pointCount = lineRenderer.positionCount;
                 Vector3 controlPoint = startPos + (endPos - startPos) * 0.5f + rootTransform.right * (endPos - startPos).magnitude * 0.1f * Mathf.Sin(Time.time*2f + tentacleId); // Небольшой изгиб

                 for (int i=0; i < pointCount; ++i) {
                    float t = (float)i / (pointCount-1);
                    // Квадратичная кривая Безье
                    Vector3 p = (1-t)*(1-t)*startPos + 2*(1-t)*t*controlPoint + t*t*endPos;
                    lineRenderer.SetPosition(i, p);
                 }
             }
        }
    }

    Vector3 CalculateTipPosition(Vector3 worldTargetPos) {
        Vector3 directionToTarget = (worldTargetPos - rootTransform.position);
        float distToTarget = directionToTarget.magnitude;
        float currentMaxLength = GetMaxPossibleLength() * currentExtensionRatio;
        
        if (distToTarget > currentMaxLength) {
            return rootTransform.position + directionToTarget.normalized * currentMaxLength;
        }
        return worldTargetPos; // Можем достать до цели
    }

    bool CheckTargetReached() {
        if (currentTargetGO == null) return false;
        float distance = Vector3.Distance(CalculateTipPosition(currentTargetGO.transform.position), currentTargetGO.transform.position);
        // Учитываем размер цели (например, радиус коллайдера)
        Collider2D targetCollider = currentTargetGO.GetComponent<Collider2D>();
        float targetRadius = targetCollider != null ? (targetCollider.bounds.size.x + targetCollider.bounds.size.y) / 4f : 0.1f;
        return distance < targetRadius + 0.1f; // Небольшой допуск
    }

    void TryGraspTarget() {
        if (currentTargetGO == null || heldFoodItemComponent != null) return; // Уже что-то держим или нет цели

        Food food = currentTargetGO.GetComponent<Food>();
        if (food != null) {
            heldFoodItemComponent = food; // Запоминаем компонент
            Destroy(currentTargetGO); // Уничтожаем GameObject еды со сцены
            currentState = State.GraspingFood;
            if(FindFirstObjectByType<EventLogPanel>() != null)
                 FindFirstObjectByType<EventLogPanel>().AddLogMessage($"{agentBodyTransform.name} grasped {food.type} food.");
        }
    }
    
    float GetMaxPossibleLength() {
        return genome.baseGraspTentacleLength * genome.maxGraspTentacleLengthFactor;
    }

    public bool IsHoldingFoodItem() { return heldFoodItemComponent != null; }
    
    public Food RetrieveAndReleaseHeldFood() {
        Food itemToReturn = null;
        if (heldFoodItemComponent != null) {
            itemToReturn = heldFoodItemComponent; // Это ссылка на компонент, GameObject уже уничтожен
            heldFoodItemComponent = null; // Освобождаем "руку"
            currentState = State.Retracting; // Начинаем втягивать пустое щупальце
        }
        return itemToReturn; // Возвращаем только данные о еде
    }
    
    void UpdateVisuals() { // Вызывается из PerformStateActions или если нужно отдельно
        // Логика обновления LineRenderer уже в PerformStateActions
    }
    public bool IsBusy() { return currentState != State.Idle; }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/SquidMetabolism.cs"
// ${ASSETS_PATH}/Agents/SquidMetabolism.cs
using UnityEngine;

public class SquidMetabolism : MonoBehaviour
{
    private Genome genome;
    private SimulationManager simManager;
    private SquidAgent agent; // Ссылка на главный компонент агента

    public float CurrentEnergy { get; private set; }
    public float Age { get; private set; }
    public float maxEnergyGeno { get; private set; } // Максимальная энергия, зависящая от генома

    // Базовые значения, которые могут быть модифицированы геномом
    private float baseStartingEnergyFactor = 0.7f; // % от maxEnergyGeno
    private float baseEnergyPerSecond = 1.0f;
    
    private GeneticAlgorithmManager gaManager; // Для мутации потомка

    public void Initialize(Genome agentGenome, SimulationManager manager, SquidAgent ownerAgent)
    {
        this.genome = agentGenome;
        this.simManager = manager;
        this.agent = ownerAgent;
        this.gaManager = manager.gaManager; // Получаем ссылку из SimManager

        if (genome == null || simManager == null || agent == null || gaManager == null) {
            Debug.LogError("SquidMetabolism initialized with null dependencies!");
            enabled = false; return;
        }

        // Максимальная энергия зависит от размера (длины мантии)
        maxEnergyGeno = 50f + 50f * Mathf.Clamp(genome.mantleLength, 0.5f, 2.0f);
        CurrentEnergy = maxEnergyGeno * baseStartingEnergyFactor;
        Age = 0f;
    }

    public void UpdateMetabolism()
    {
        if (!enabled) return;

        Age += Time.deltaTime;
        genome.fitness = Age; // Простейший фитнес - время жизни. Может быть дополнен.

        // Расход энергии
        float energyDrainThisFrame = baseEnergyPerSecond * genome.metabolismRateFactor;
        // TODO: Добавить расход энергии на активные действия (движение, использование щупалец)
        // Например, если Rigidbody2D имеет скорость:
        if (agent.TryGetComponent<Rigidbody2D>(out var rb)) {
             energyDrainThisFrame += rb.velocity.magnitude * 0.1f; // Примерный расход на движение
        }
        // Расход на щупальца (если они активны)
        // ...

        CurrentEnergy -= energyDrainThisFrame * Time.deltaTime;

        // Проверка на смерть
        if (CurrentEnergy <= 0 || Age >= genome.maxAge)
        {
            Die();
            return;
        }

        // Проверка на размножение
        float reproductionThreshold = maxEnergyGeno * genome.energyToReproduceThresholdFactor;
        if (CurrentEnergy >= reproductionThreshold)
        {
            TryReproduce();
        }
    }

    public void Eat(float energyValue, FoodType foodType) // Принимаем уже извлеченное значение
    {
        CurrentEnergy += energyValue;
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0, maxEnergyGeno);
        genome.fitness += energyValue * 0.2f; // Бонус к фитнесу за еду
        // Debug.Log($"{agent.name} ate {foodType}, energy: {CurrentEnergy:F1}");
    }

    void TryReproduce()
    {
        float costOfReproduction = maxEnergyGeno * genome.energyCostOfReproductionFactor;
        if (CurrentEnergy < costOfReproduction + 10) return; // Не размножаемся, если останется мало энергии

        CurrentEnergy -= costOfReproduction;
        genome.fitness += costOfReproduction * 0.3f; // Бонус к фитнесу за успешное размножение

        Genome offspringGenome = new Genome(genome); // Копируем родительский геном
        
        // Скрещивание (если есть второй родитель) - пока пропускаем, т.к. асексуальное
        // Если бы было скрещивание, то оно происходило бы в GAManager,
        // а сюда бы передавался уже "смешанный" геном.
        // Для асексуального, просто мутируем копию.
        
        if (gaManager != null) {
            gaManager.Mutate(offspringGenome); // Мутируем потомка
        } else {
            Debug.LogWarning("GAManager not found for mutation during reproduction.");
        }
        
        // Физические гены потомка могут быть либо унаследованы с мутациями (уже сделано),
        // либо дополнительно рандомизированы, если мы хотим больше разнообразия
        // offspringGenome.InitializeRandomPhysicalGenes(); // Это перезапишет унаследованные гены

        agent.ReportReproduction(offspringGenome);
    }

    void Die()
    {
        // Финальный фитнес уже накоплен (Age + бонусы)
        agent.ReportDeath(); // Сообщаем главному компоненту о смерти
        enabled = false; // Отключаем метаболизм
    }

    // TODO: Метод для получения урона, если будет хищничество
    // public void TakeDamage(float amount) { CurrentEnergy -= amount; }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Agents/SquidMeshGenerator.cs"
// ${ASSETS_PATH}/Agents/SquidMeshGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidMeshGenerator : MonoBehaviour
{
    private Genome genome;
    private Transform agentTransform;

    // --- Ссылки на созданные GameObjects с мешами ---
    public GameObject mantleObject { get; private set; }
    public List<GameObject> swimmingTentacleObjects { get; private set; } = new List<GameObject>();
    public List<GameObject> graspingTentacleObjects { get; private set; } = new List<GameObject>();
    public GameObject eyesRootObject { get; private set; } // Родительский для глаз

    // --- Материалы (назначаются в инспекторе префаба SquidAgent) ---
    [Header("Materials (Assign in Prefab)")]
    public Material mantleMaterial;
    public Material tentacleMaterial; // Общий для всех _щупалец
    public Material eyeMaterial;    // Для "белка" глаза
    public Material pupilMaterial;  // Для зрачка

    private bool isInitialized = false;

    public void Initialize(Transform parentTransform)
    {
        this.agentTransform = parentTransform;
        // Проверка материалов (должны быть назначены на префабе агента)
        if (mantleMaterial == null || tentacleMaterial == null || eyeMaterial == null || pupilMaterial == null) {
            Debug.LogError($"SquidMeshGenerator on {agentTransform.name} is missing one or more materials. Please assign them on the Agent prefab.");
            enabled = false; return;
        }
        isInitialized = true;
    }

    public void GenerateInitialMeshes(Genome agentGenome)
    {
        if (!isInitialized || agentGenome == null) {
             Debug.LogError($"SquidMeshGenerator cannot generate meshes: Not initialized or genome is null. Agent: {agentTransform.name}");
             return;
        }
        this.genome = agentGenome;

        // Очистка старых объектов, если они были (на случай регенерации)
        ClearExistingMeshObjects();

        // 1. Создание меша мантии
        mantleObject = CreateMeshHolder("Mantle", agentTransform, mantleMaterial, genome.mantleColor);
        Mesh mantleMesh = GenerateMantleProceduralMesh(genome.mantleLength, genome.mantleMaxDiameter);
        if (mantleObject.TryGetComponent<MeshFilter>(out var mantleMF)) mantleMF.mesh = mantleMesh;
        
        // 2. Создание объектов для плавательных щупалец (8 штук)
        for (int i = 0; i < TentacleController.NUM_SWIMMING_TENTACLES; i++)
        {
            GameObject tentacleGO = CreateMeshHolder($"SwimmingTentacle_{i}", agentTransform, tentacleMaterial, genome.mantleColor * 0.85f);
            float angle = i * (360f / TentacleController.NUM_SWIMMING_TENTACLES) + 180f; // Начинаем сзади и по кругу
            // Располагаем у "основания" мантии (ближе к хвосту)
            Vector3 baseOffset = new Vector3(0, -genome.mantleLength * 0.4f, 0);
            tentacleGO.transform.localPosition = baseOffset + Quaternion.Euler(0,0,angle) * Vector3.up * (genome.mantleMaxDiameter * 0.45f);
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0,angle);

            // Генерируем меш для плавательного щупальца
            // Для MVP можно использовать простой цилиндр или даже не генерировать меш, а просто объект-заглушку
            Mesh swimTentacleMesh = GenerateProceduralTentacleMesh(genome.baseSwimTentacleLength, genome.swimTentacleThickness, 10, 6, false);
            if (tentacleGO.TryGetComponent<MeshFilter>(out var swimMF)) swimMF.mesh = swimTentacleMesh;
            swimmingTentacleObjects.Add(tentacleGO);
        }

        // 3. Создание объектов для хватательных щупалец (2 штуки)
        for (int i = 0; i < TentacleController.NUM_GRASPING_TENTACLES; i++)
        {
            // Хватательные щупальца будут использовать LineRenderer, управляемый из GraspingTentacle.cs
            // Поэтому здесь создаем только пустой GameObject как точку крепления и для скрипта.
            GameObject tentacleGO = new GameObject($"GraspingTentacle_{i}");
            tentacleGO.transform.SetParent(agentTransform);
            // Располагаем их чуть более вентрально и ближе к "голове"
            float sideOffset = (i == 0 ? -1f : 1f) * genome.mantleMaxDiameter * 0.2f;
            tentacleGO.transform.localPosition = new Vector3(sideOffset, -genome.mantleLength * 0.25f, -0.01f); // Чуть впереди по Z для видимости
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0, (i == 0 ? 15f : -15f)); // Небольшой начальный разворот
            graspingTentacleObjects.Add(tentacleGO);
            // MeshFilter и MeshRenderer не нужны, если LineRenderer будет в GraspingTentacle.cs
        }

        // 4. Создание "глаз"
        eyesRootObject = new GameObject("EyesRoot");
        eyesRootObject.transform.SetParent(agentTransform);
        // Располагаем на "головной" части мантии
        eyesRootObject.transform.localPosition = new Vector3(0, genome.mantleLength * 0.3f, -0.02f); // Z, чтобы были поверх мантии
        
        CreateEye("LeftEye", eyesRootObject.transform, -genome.mantleMaxDiameter * 0.25f, genome.eyeSize);
        CreateEye("RightEye", eyesRootObject.transform, genome.mantleMaxDiameter * 0.25f, genome.eyeSize);
    }

    GameObject CreateMeshHolder(string name, Transform parent, Material material, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.material = material;
        mr.material.color = color; // Устанавливаем цвет копии материала
        return obj;
    }
    
    void CreateEye(string name, Transform parent, float xOffset, float eyeRadius)
    {
        // Белок глаза
        GameObject eyeWhiteObj = CreateMeshHolder(name, parent, eyeMaterial, Color.white);
        eyeWhiteObj.transform.localPosition = new Vector3(xOffset, 0, 0);
        Mesh eyeWhiteMesh = Generate2DCircleMesh(eyeRadius, 16);
        if (eyeWhiteObj.TryGetComponent<MeshFilter>(out var eyeMF)) eyeMF.mesh = eyeWhiteMesh;

        // Зрачок
        GameObject pupilObj = CreateMeshHolder("Pupil", eyeWhiteObj.transform, pupilMaterial, Color.black);
        pupilObj.transform.localPosition = new Vector3(0, 0, -0.01f); // Чуть впереди белка
        float pupilRadius = eyeRadius * 0.5f;
        Mesh pupilMesh = Generate2DCircleMesh(pupilRadius, 12);
        if (pupilObj.TryGetComponent<MeshFilter>(out var pupilMF)) pupilMF.mesh = pupilMesh;
        // TODO: Анимация зрачка (размер, положение)
    }


    Mesh GenerateMantleProceduralMesh(float length, float diameter)
    {
        Mesh mesh = new Mesh { name = "ProceduralMantle" };
        // Упрощенный вариант: вытянутая сфера (капсула без полусфер на концах, а скорее эллипсоид вращения)
        // Для MVP: можно взять примитив Unity "Capsule" и отмасштабировать, но это не генерация.
        // Давайте сделаем простой цилиндр, сужающийся к одному концу.
        int segmentsAround = 12; // Количество сегментов по окружности
        int segmentsAlong = 2;  // Всего 3 кольца вершин (0, 1, 2)
        float radius = diameter / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        for (int y = 0; y <= segmentsAlong; y++)
        {
            float currentY = -length / 2f + (length / segmentsAlong) * y;
            float currentRadius = radius;
            if (y == segmentsAlong) currentRadius *= 0.1f; // Заостренный "хвост"
            else if (y == 0) currentRadius *= 0.7f; // "Голова" чуть уже основания

            for (int i = 0; i <= segmentsAround; i++) // <= для замыкания UV
            {
                float angle = (float)i / segmentsAround * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentY, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2((float)i/segmentsAround, (float)y/segmentsAlong));
            }
        }
        
        // Вершина для "хвостового" полюса
        int tipPoleIndex = vertices.Count;
        vertices.Add(new Vector3(0, length / 2f + length * 0.05f, 0)); // Чуть дальше для остроты
        uvs.Add(new Vector2(0.5f, 1f));
        // Вершина для "головного" полюса
        int headPoleIndex = vertices.Count;
        vertices.Add(new Vector3(0, -length / 2f - length * 0.05f, 0));
        uvs.Add(new Vector2(0.5f, 0f));


        for (int y = 0; y < segmentsAlong; y++)
        {
            for (int i = 0; i < segmentsAround; i++)
            {
                int v0 = y * (segmentsAround + 1) + i;
                int v1 = y * (segmentsAround + 1) + (i + 1);
                int v2 = (y + 1) * (segmentsAround + 1) + i;
                int v3 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v0, v2, v1 });
                triangles.AddRange(new int[] { v1, v2, v3 });
            }
        }
        
        // "Закрываем" хвост
        int lastRingStartIdx = segmentsAlong * (segmentsAround + 1);
        for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { tipPoleIndex, lastRingStartIdx + i, lastRingStartIdx + i + 1 });
        }
        // "Закрываем" голову
        int firstRingStartIdx = 0;
         for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { headPoleIndex, firstRingStartIdx + i + 1, firstRingStartIdx + i });
        }


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
            float y_pos = (length / segmentsAlong) * y_idx;
            float currentRadius = radius;
            if (y_idx == 0) currentRadius *= 0.8f; // У основания чуть тоньше
            if (bulbousTip && y_idx == segmentsAlong) currentRadius *= 1.8f; // Утолщение на конце
            else if (y_idx > segmentsAlong / 2) currentRadius = Mathf.Lerp(radius, radius * (bulbousTip ? 0.7f : 0.3f) , (float)(y_idx - segmentsAlong / 2) / (segmentsAlong / 2));

            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++) { // <= для UV
                float angle = (float)i_idx / segmentsAround * Mathf.PI * 2f;
                // Ось Y - вдоль щупальца
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, y_pos, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2((float)i_idx/segmentsAround, (float)y_idx/segmentsAlong));
            }
        }
        // Полюс на кончике
        int tipPole = vertices.Count;
        vertices.Add(new Vector3(0, length + length * (bulbousTip ? 0.05f : 0.02f), 0));
        uvs.Add(new Vector2(0.5f,1f));


        for (int y = 0; y < segmentsAlong; y++) {
            for (int i = 0; i < segmentsAround; i++) {
                int v0 = y * (segmentsAround + 1) + i;
                int v1 = y * (segmentsAround + 1) + (i + 1);
                int v2 = (y + 1) * (segmentsAround + 1) + i;
                int v3 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v0, v2, v1 });
                triangles.AddRange(new int[] { v1, v2, v3 });
            }
        }
        // Закрываем кончик
        int lastRingStart = segmentsAlong * (segmentsAround + 1);
        for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { tipPole, lastRingStart + i, lastRingStart + i + 1 });
        }
        // TODO: Закрыть основание (не так критично, т.к. оно "в теле")

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
        List<Vector3> vertices = new List<Vector3> { Vector3.zero }; // Центр
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

        for (int i = 0; i <= segments; i++) {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
            uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            if (i > 0) { // Формируем треугольники
                triangles.AddRange(new int[] { 0, i, i + 1 });
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
        if (eyesRootObject != null) Destroy(eyesRootObject); // Уничтожит и дочерние глаза
        foreach (var go in swimmingTentacleObjects) if (go != null) Destroy(go);
        foreach (var go in graspingTentacleObjects) if (go != null) Destroy(go);
        swimmingTentacleObjects.Clear();
        graspingTentacleObjects.Clear();
    }

    public void UpdateDynamicMeshes(SquidBrain.BrainOutput brainOutput) // Вызывать из SquidAgent, если нужна анимация мешей
    {
        if (!isInitialized || genome == null) return;

        // Пример: Пульсация мантии через изменение вершин (сложно) или scale (проще)
        if (mantleObject != null)
        {
            float pulseFactor = 1f + Mathf.Sin(Time.time * 5f * (0.1f + brainOutput.moveForward * 0.9f)) * 0.05f * genome.mantleLength;
            mantleObject.transform.localScale = new Vector3(pulseFactor, genome.mantleLength, pulseFactor); // Грубая пульсация
            // Обновление цвета, если он меняется не только по геному
            if (mantleObject.TryGetComponent<MeshRenderer>(out var mr)) mr.material.color = genome.mantleColor;
        }
        
        // Анимация плавательных щупалец (если они мешевые и анимируются здесь)
        // уже делается в SwimmingTentacle.UpdateMovement
    }
}
EOF

# --- Assets/Environment ---
cat <<EOF > "${ASSETS_PATH}/Environment/Food.cs"
// ${ASSETS_PATH}/Environment/Food.cs
using UnityEngine;

public enum FoodType { Plant, Meat }

public class Food : MonoBehaviour
{
    public FoodType type = FoodType.Plant;
    public float energyValue = 10f;
    public float lifetime = 60f;

    private bool isConsumed = false; // Чтобы не потребить дважды

    void Start()
    {
        if (lifetime > 0) Destroy(gameObject, lifetime);
        
        // Простая визуализация цветом, если нет SpriteRenderer
        // Предполагаем, что на префабе еды есть MeshRenderer и простой материал
        if (TryGetComponent<MeshRenderer>(out var mr)) {
            // Создаем экземпляр материала, чтобы изменение цвета не влияло на другие объекты с тем же материалом
            if (mr.material != null) { // Проверка на случай, если материала нет
                 mr.material = new Material(mr.material);
                 mr.material.color = (type == FoodType.Plant) ? new Color(0.1f,0.7f,0.1f) : new Color(0.7f,0.1f,0.1f);
            }
        } else if (TryGetComponent<SpriteRenderer>(out var sr)) { // Если все же спрайт
            sr.color = (type == FoodType.Plant) ? Color.green : Color.red;
        }
    }

    // Вызывается, когда щупальце "схватило" еду
    // Возвращает себя, чтобы щупальце могло получить данные и затем сообщить агенту
    public Food TryConsume()
    {
        if (isConsumed) return null;
        isConsumed = true;
        // Не уничтожаем здесь, щупальце или агент сделает это после получения данных
        // Destroy(gameObject, 0.1f); // Небольшая задержка, чтобы щупальце успело обработать
        return this;
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Environment/FoodSpawner.cs"
// ${ASSETS_PATH}/Environment/FoodSpawner.cs
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject plantFoodPrefab;
    public GameObject meatFoodPrefab;

    [Header("Spawning Settings")]
    public int initialPlantFoodCount = 50;
    public float spawnRadius = 30f;
    public float plantSpawnInterval = 5f;
    public int plantsPerInterval = 5;

    private float plantSpawnTimer;
    private SimulationManager simManager;

    void Start()
    {
        simManager = FindFirstObjectByType<SimulationManager>();
        if (simManager == null) {
            Debug.LogError("FoodSpawner could not find SimulationManager!");
            enabled = false; return;
        }
        // Начальный спавн еды будет вызван из SimulationManager при старте симуляции
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
            return new Vector3(Random.Range(wb.xMin + 0.5f, wb.xMax - 0.5f), Random.Range(wb.yMin + 0.5f, wb.yMax - 0.5f), 0);
        }
        // Fallback если нет WorldBounds
        Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
        return new Vector3(randomPos.x, randomPos.y, 0);
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/Environment/WorldBounds.cs"
// ${ASSETS_PATH}/Environment/WorldBounds.cs
using UnityEngine;

public class WorldBounds : MonoBehaviour
{
    public float xMin = -30f, xMax = 30f;
    public float yMin = -30f, yMax = 30f;

    // Можно добавить BoxCollider2D по периметру и настроить физический материал
    // чтобы агенты отскакивали или останавливались.
    // Если нет, то агенты должны сами проверять границы.

    void Start() {
        // Создаем физические границы, если их нет
        CreateBoundaryColliders();
    }

    void CreateBoundaryColliders() {
        // Убедимся, что на этом объекте есть Rigidbody2D (static) и коллайдеры
        if (GetComponent<Rigidbody2D>() == null) {
            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }

        float thickness = 1f; // Толщина стен
        // Нижняя стена
        CreateBoundaryWall("BottomWall", new Vector2((xMin+xMax)/2, yMin - thickness/2), new Vector2(xMax-xMin + thickness*2, thickness));
        // Верхняя стена
        CreateBoundaryWall("TopWall", new Vector2((xMin+xMax)/2, yMax + thickness/2), new Vector2(xMax-xMin + thickness*2, thickness));
        // Левая стена
        CreateBoundaryWall("LeftWall", new Vector2(xMin - thickness/2, (yMin+yMax)/2), new Vector2(thickness, yMax-yMin));
        // Правая стена
        CreateBoundaryWall("RightWall", new Vector2(xMax + thickness/2, (yMin+yMax)/2), new Vector2(thickness, yMax-yMin));
    }

    void CreateBoundaryWall(string name, Vector2 position, Vector2 size) {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(transform);
        wall.transform.position = position;
        BoxCollider2D bc = wall.AddComponent<BoxCollider2D>();
        bc.size = size;
        wall.layer = LayerMask.NameToLayer("Obstacle"); // Убедитесь, что слой "Obstacle" существует
    }


    // Если не используем физические границы, а агент сам проверяет:
    public bool IsOutOfBounds(Vector3 position) {
        return position.x < xMin || position.x > xMax || position.y < yMin || position.y > yMax;
    }

    public Vector3 ClampPosition(Vector3 position) {
        return new Vector3(
            Mathf.Clamp(position.x, xMin, xMax),
            Mathf.Clamp(position.y, yMin, yMax),
            position.z
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 p1 = new Vector3(xMin, yMin, 0);
        Vector3 p2 = new Vector3(xMax, yMin, 0);
        Vector3 p3 = new Vector3(xMax, yMax, 0);
        Vector3 p4 = new Vector3(xMin, yMax, 0);
        Gizmos.DrawLine(p1,p2); Gizmos.DrawLine(p2,p3);
        Gizmos.DrawLine(p3,p4); Gizmos.DrawLine(p4,p1);
    }
}
EOF

# --- Assets/UI ---
cat <<EOF > "${ASSETS_PATH}/UI/UIManager.cs"
// ${ASSETS_PATH}/UI/UIManager.cs
using UnityEngine;
using UnityEngine.UI; // Для работы с UI элементами (Button, Text, Slider)
using TMPro; // Если используете TextMeshPro

public class UIManager : MonoBehaviour
{
    public SimulationManager simManager;
    // StatisticsManager и EventLogPanel могут быть найдены или назначены

    [Header("Control Panel Elements")]
    public Button startButton;
    public Button pauseButton;
    public Button resumeButton;
    public Slider timeScaleSlider;
    public TMP_Text timeScaleText; // Используем TMP_Text для TextMeshPro
    public TMP_Text generationText;
    public Toggle divineToolsToggle; // Для включения инструментов "бога"

    [Header("Agent Inspector Panel")]
    public GameObject agentInspectorPanelGO;
    public TMP_Text agentNameText;
    public TMP_Text agentEnergyText;
    public TMP_Text agentAgeText;
    public TMP_Text agentFitnessText;
    public TMP_Text agentGenomeText; // Для отображения части генома
    public Button closeInspectorButton;

    private SquidAgent selectedAgentForUI;
    private InputManager inputManager; // Для управления divine tools

    void Start()
    {
        if (simManager == null) simManager = FindFirstObjectByType<SimulationManager>();
        inputManager = FindFirstObjectByType<InputManager>();

        if (simManager == null) {
            Debug.LogError("UIManager could not find SimulationManager!");
            enabled = false; return;
        }
        
        // Назначение обработчиков на кнопки
        if (startButton) startButton.onClick.AddListener(simManager.RequestStartSimulation);
        else Debug.LogWarning("StartButton not assigned in UIManager.");

        if (pauseButton) pauseButton.onClick.AddListener(simManager.RequestPauseSimulation);
        else Debug.LogWarning("PauseButton not assigned in UIManager.");
        
        if (resumeButton) resumeButton.onClick.AddListener(simManager.RequestResumeSimulation);
        else Debug.LogWarning("ResumeButton not assigned in UIManager.");

        if (timeScaleSlider) timeScaleSlider.onValueChanged.AddListener(simManager.RequestAdjustTimeScale);
        else Debug.LogWarning("TimeScaleSlider not assigned in UIManager.");

        if (divineToolsToggle && inputManager != null) {
            divineToolsToggle.onValueChanged.AddListener((value) => inputManager.divineToolsEnabled = value);
        } else if (inputManager == null) Debug.LogWarning("InputManager not found for DivineToolsToggle.");
        
        if (closeInspectorButton && agentInspectorPanelGO) {
             closeInspectorButton.onClick.AddListener(() => agentInspectorPanelGO.SetActive(false));
        }

        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
        
        InitializeUIValues();
    }
    
    void InitializeUIValues() {
        if (timeScaleSlider) {
            timeScaleSlider.minValue = 0.1f; timeScaleSlider.maxValue = 5f; timeScaleSlider.value = 1f;
        }
        UpdateTimeScaleTextValue(1f);
        UpdateGenerationText(0);
    }

    public float GetCurrentTimeScaleRequest() {
        return timeScaleSlider != null ? timeScaleSlider.value : 1f;
    }
    
    public void UpdateTimeScaleSliderValue(float currentTimeScale) {
        if (timeScaleSlider) timeScaleSlider.value = currentTimeScale;
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
        if (pauseButton) pauseButton.interactable = isRunning && currentTimeScale > 0;
        if (resumeButton) resumeButton.interactable = !isRunning || currentTimeScale == 0; // Активна если не запущена или на паузе
        
        UpdateTimeScaleSliderValue(currentTimeScale); // Обновит и текст
        UpdateGenerationText(generationNum);
    }


    public void SelectAgentForInspector(SquidAgent agent)
    {
        selectedAgentForUI = agent;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(true);
        UpdateAgentInspectorUI();
    }
    public void DeselectAgentForInspector() {
        selectedAgentForUI = null;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
    }

    void Update() { // Обновляем инспектор только если он активен и агент выбран
        if (selectedAgentForUI != null && agentInspectorPanelGO != null && agentInspectorPanelGO.activeSelf) {
            UpdateAgentInspectorUI();
        }
    }

    void UpdateAgentInspectorUI()
    {
        if (selectedAgentForUI == null || !selectedAgentForUI.isInitialized) {
            if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
            return;
        }

        if (agentNameText) agentNameText.text = "Name: " + selectedAgentForUI.gameObject.name;
        
        if (selectedAgentForUI.TryGetComponent<SquidMetabolism>(out var meta)) {
             if (agentEnergyText) agentEnergyText.text = $"Energy: {meta.CurrentEnergy:F1} / {meta.maxEnergyGeno:F1}";
             if (agentAgeText) agentAgeText.text = $"Age: {meta.Age:F1} / {selectedAgentForUI.genome.maxAge:F1}";
        }
        if (agentFitnessText) agentFitnessText.text = $"Fitness: {selectedAgentForUI.genome.fitness:F2}";
        
        if (agentGenomeText && selectedAgentForUI.genome != null) {
            // Отображаем только часть генома для краткости
            Genome g = selectedAgentForUI.genome;
            agentGenomeText.text = $"Mantle: L{g.mantleLength:F2} D{g.mantleMaxDiameter:F2}\n" +
                                   $"GraspTent: L{g.baseGraspTentacleLength:F2} Factor{g.maxGraspTentacleLengthFactor:F1}\n" +
                                   $"Eyes: {g.eyeSize:F2}  Metab: {g.metabolismRateFactor:F2}\n" +
                                   $"Aggro: {g.aggression:F2} FoodPref: {g.foodPreference:F2}";
        }
    }
}
EOF

# --- Assets/UI/Panels ---
cat <<EOF > "${ASSETS_PATH}/UI/Panels/StatisticsPanel.cs"
// ${ASSETS_PATH}/UI/Panels/StatisticsPanel.cs
using UnityEngine;
using TMPro; // Для TextMeshPro
using System.Collections.Generic;


public class StatisticsPanel : MonoBehaviour
{
    private StatisticsManager statsManager;

    [Header("Text Fields")]
    public TMP_Text populationCountText;
    public TMP_Text averageEnergyText;
    public TMP_Text averageAgeText;
    public TMP_Text averageMantleLengthText;
    public TMP_Text maxFitnessText;


    [Header("Graph Renderers (Assign LineRenderer GameObjects)")]
    public GraphRenderer populationGraph;
    public GraphRenderer energyGraph;
    public GraphRenderer ageGraph;
    public GraphRenderer mantleLengthGraph;


    public void Initialize(StatisticsManager manager)
    {
        this.statsManager = manager;
        if (statsManager == null) Debug.LogError("StatisticsManager not provided to StatisticsPanel!");
        ClearGraphs(); // Очищаем графики при инициализации
    }

    public void UpdatePanel()
    {
        if (statsManager == null) return;

        if (populationCountText) populationCountText.text = "Population: " + statsManager.currentPopulationCount;
        if (averageEnergyText) averageEnergyText.text = $"Avg Energy: {statsManager.currentAverageEnergy:F1}";
        if (averageAgeText) averageAgeText.text = $"Avg Age: {statsManager.currentAverageAge:F1}";
        if (averageMantleLengthText) averageMantleLengthText.text = $"Avg Mantle L: {statsManager.currentAverageMantleLength:F2}";
        if (maxFitnessText) maxFitnessText.text = $"Max Fitness (Prev Gen): {statsManager.maxFitnessLastGeneration:F2}";


        // Обновление графиков
        if (populationGraph) populationGraph.DrawGraph(statsManager.populationHistory);
        if (energyGraph) energyGraph.DrawGraph(statsManager.averageEnergyHistory);
        if (ageGraph) ageGraph.DrawGraph(statsManager.averageAgeHistory);
        if (mantleLengthGraph) mantleLengthGraph.DrawGraph(statsManager.averageMantleLengthHistory);
    }
    
    public void ClearGraphs() {
        if (populationGraph) populationGraph.DrawGraph(new List<float>());
        if (energyGraph) energyGraph.DrawGraph(new List<float>());
        if (ageGraph) ageGraph.DrawGraph(new List<float>());
        if (mantleLengthGraph) mantleLengthGraph.DrawGraph(new List<float>());
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/UI/Panels/EventLogPanel.cs"
// ${ASSETS_PATH}/UI/Panels/EventLogPanel.cs
using UnityEngine;
using UnityEngine.UI; // Для ScrollRect
using TMPro; // Для TextMeshPro
using System.Collections.Generic;
using System.Linq; // Для Enumerable.Reverse

public class EventLogPanel : MonoBehaviour
{
    public TMP_Text logTextDisplay; // Используем TMP_Text
    public ScrollRect scrollRect;
    public int maxLogMessages = 30;
    private List<string> logMessages = new List<string>(); // Используем List для удобного добавления в начало

    private static EventLogPanel instance;
    public static EventLogPanel Instance { get { return instance; } }

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);

        // Application.logMessageReceivedThreaded += HandleUnityLog; // Для системных логов Unity (безопасно для потоков)
        if (logTextDisplay) logTextDisplay.text = ""; // Очищаем при старте
    }

    // void OnDestroy()
    // {
    //    Application.logMessageReceivedThreaded -= HandleUnityLog;
    // }

    public void AddLogMessage(string message)
    {
        if (logMessages.Count >= maxLogMessages)
        {
            logMessages.RemoveAt(logMessages.Count -1); // Удаляем самое старое (последнее в списке)
        }
        // Добавляем в начало списка, чтобы новые сообщения были сверху
        logMessages.Insert(0, $"[{System.DateTime.Now:HH:mm:ss}] {message}");
        UpdateLogText();
    }

    // void HandleUnityLog(string logString, string stackTrace, LogType type)
    // {
    //     // Этот метод будет вызван из другого потока, нужна синхронизация или отправка в главный поток
    //     // Для простоты, пока не будем обрабатывать системные логи здесь
    // }

    void UpdateLogText()
    {
        if (logTextDisplay != null)
        {
            logTextDisplay.text = string.Join("\n", logMessages); // Новые вверху
            // Автопрокрутка вверх (к новым сообщениям)
            if (scrollRect != null) {
                 Canvas.ForceUpdateCanvases();
                 scrollRect.normalizedPosition = new Vector2(0, 1); // Прокрутка в самый верх
            }
        }
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/UI/GraphRenderer.cs"
// ${ASSETS_PATH}/UI/GraphRenderer.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class GraphRenderer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    public RectTransform graphAreaRect; // Прямоугольник UI Canvas, в котором рисуется график

    [Header("Graph Style")]
    public Color graphColor = Color.green;
    public float lineWidth = 2f; // Толщина линии в пикселях UI
    public int maxPointsToDisplay = 100; // Макс. кол-во точек на графике для производительности

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) {
            Debug.LogError("GraphRenderer requires a LineRenderer component!");
            enabled = false; return;
        }
        
        // Настройка LineRenderer
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); // Простой материал
        // Или используйте Unlit/Color для простого цвета без прозрачности
        // lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.startColor = graphColor;
        lineRenderer.endColor = graphColor;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = false; // Важно для UI! Рисуем в локальных координатах RectTransform
        lineRenderer.sortingOrder = 10; // Чтобы был поверх других UI элементов, если нужно
    }

    public void DrawGraph(List<float> dataPoints, float historicalMaxValueOverride = -1f)
    {
        if (!enabled || dataPoints == null) {
            if(lineRenderer != null) lineRenderer.positionCount = 0;
            return;
        }

        // Ограничиваем количество точек для отображения
        List<float> pointsToDraw = dataPoints;
        if (dataPoints.Count > maxPointsToDisplay) {
            pointsToDraw = dataPoints.GetRange(dataPoints.Count - maxPointsToDisplay, maxPointsToDisplay);
        }

        if (pointsToDraw.Count < 2) {
            lineRenderer.positionCount = 0;
            return;
        }
        
        lineRenderer.positionCount = pointsToDraw.Count;
        
        float maxValue = 0;
        if (historicalMaxValueOverride > 0.001f) { // Используем переданное макс. значение
            maxValue = historicalMaxValueOverride;
        } else { // Иначе ищем максимум в текущих данных
            foreach (float point in pointsToDraw) if (point > maxValue) maxValue = point;
        }
        if (maxValue < 0.001f && maxValue > -0.001f) maxValue = 1; // Избегаем деления на ноль, если все значения ~0

        if (graphAreaRect == null) {
            Debug.LogWarning("GraphArea RectTransform not assigned to GraphRenderer: " + gameObject.name);
            // Пытаемся взять RectTransform родителя, если это UI элемент
            graphAreaRect = transform.parent as RectTransform;
            if (graphAreaRect == null) {
                lineRenderer.positionCount = 0;
                return;
            }
        }

        Rect rect = graphAreaRect.rect; // Локальные размеры RectTransform
        float graphWidth = rect.width;
        float graphHeight = rect.height;

        for (int i = 0; i < pointsToDraw.Count; i++)
        {
            // X распределяется по всей ширине графика
            float x = (float)i / (pointsToDraw.Count - 1) * graphWidth;
            // Y нормализуется относительно maxValue и масштабируется по высоте графика
            float y = (pointsToDraw[i] / maxValue) * graphHeight;
            y = Mathf.Clamp(y, 0, graphHeight); // Ограничиваем, чтобы не выходило за пределы
            
            // Позиции в локальных координатах RectTransform (центр = 0,0)
            // Смещаем, чтобы 0,0 графика был в левом нижнем углу graphAreaRect
            lineRenderer.SetPosition(i, new Vector3(x - graphWidth * 0.5f, y - graphHeight * 0.5f, 0));
        }
    }
}
EOF

# --- Файлы в Assets/UI/Panels (оставляем заглушки, т.к. их логика сильно зависит от конкретного UI) ---
# UIManager и StatisticsPanel уже более детальные, панели можно оставить простыми

cat <<EOF > "${ASSETS_PATH}/UI/Panels/SimulationControlPanel.cs"
// ${ASSETS_PATH}/UI/Panels/SimulationControlPanel.cs
using UnityEngine;
// Этот файл служит больше как маркер для организации.
// Основная логика управления симуляцией через UI находится в UIManager.
public class SimulationControlPanel : MonoBehaviour
{
    void Start()
    {
        // Если здесь будут специфичные элементы управления, их можно инициализировать.
    }
}
EOF

cat <<EOF > "${ASSETS_PATH}/UI/Panels/AgentInspectorPanel.cs"
// ${ASSETS_PATH}/UI/Panels/AgentInspectorPanel.cs
using UnityEngine;
// Аналогично SimulationControlPanel. Логика отображения в UIManager.
public class AgentInspectorPanel : MonoBehaviour
{
    void Start()
    {
        // Если будут сложные интерактивные элементы в инспекторе, их логика здесь.
    }
}
EOF


echo "C# файлы обновлены/созданы в $ASSETS_PATH."
echo ""
echo "=============================================================================="
echo "ЗАВЕРШЕНО ОБНОВЛЕНИЕ СКРИПТОВ!"
echo "Путь к Assets: $ASSETS_PATH"
echo "ВАЖНО:"
echo "1. Откройте проект в Unity Hub. Unity перекомпилирует скрипты."
echo "2. Проверьте консоль на наличие ошибок компиляции. Исправьте их."
echo "3. Вам все еще нужно будет создать префабы, сцену, материалы и настроить"
echo "   все связи в Инспекторе Unity, как обсуждалось ранее для MVP."
echo "4. Этот скрипт перезаписал C# файлы. Если у вас были свои изменения в них,"
echo "   они утеряны (надеюсь, вы сделали бэкап)."
echo "5. Сфокусируйтесь на настройке префаба SquidAgent и сцены, чтобы получить"
echo "   работающий базовый цикл симуляции."
echo "УДАЧИ!"
echo "=============================================================================="
