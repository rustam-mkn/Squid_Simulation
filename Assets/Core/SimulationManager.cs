// /Users/user/Dev/Unity/Squid/Assets/Core/SimulationManager.cs
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
