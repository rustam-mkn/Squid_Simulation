// /Users/user/Dev/Unity/Squid/Assets/Core/SimulationManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SimulationManager : MonoBehaviour
{
    [Header("Prefabs & Scene References")]
    public GameObject squidAgentPrefab;
    public FoodSpawner foodSpawner;
    public StatisticsManager statisticsManager;
    public GeneticAlgorithmManager gaManager;
    public UIManager uiManager;

    [Header("Simulation Settings")]
    public int initialPopulationSize = 15;
    public float generationTime = 75f;
    private float currentGenerationTimer;
    
    // --- ПОЛЯ ДЛЯ НАСТРОЙКИ В ИНСПЕКТОРЕ ---
    [Tooltip("Global multiplier for base energy drain. Affects new agents. 1.0 = normal, >1 = faster drain, <1 = slower drain.")]
    [Range(0.1f, 5f)]
    public float baseEnergyDrainMultiplier = 1.0f;

    [Tooltip("Energy level (as a factor of max energy, e.g., 0.9 = 90%) an agent needs to reach to be able to reproduce. Affects new agents.")]
    [Range(0.5f, 0.99f)]
    public float reproductionEnergyThresholdSetting = 0.90f; // <<< ВОТ ЭТО ПОЛЕ БЫЛО ПРОПУЩЕНО РАНЕЕ
    // ----------------------------------------------------

    private List<SquidAgent> agents = new List<SquidAgent>();
    private List<Genome> genomesForNextGeneration = new List<Genome>();

    [Header("Neural Network Genome Params")]
    public int numInputNodes = 20;
    public int numHiddenNodes = 20;
    public int numOutputNodes = 4;

    public bool isRunning { get; private set; } = false;
    public bool isPaused { get; private set; } = false;
    public int currentGenerationNumber { get; private set; } = 0;
    private float timeScaleBeforePause = 1f;

    void Start()
    {
        bool referencesOk = true;
        if (foodSpawner == null) { foodSpawner = FindFirstObjectByType<FoodSpawner>(); if (foodSpawner == null) { Debug.LogError("FoodSpawner not found!"); referencesOk = false; } }
        if (statisticsManager == null) { statisticsManager = FindFirstObjectByType<StatisticsManager>(); if (statisticsManager == null) { Debug.LogError("StatisticsManager not found!"); referencesOk = false; } }
        if (gaManager == null) { gaManager = FindFirstObjectByType<GeneticAlgorithmManager>(); if (gaManager == null) { Debug.LogError("GeneticAlgorithmManager not found!"); referencesOk = false; } }
        if (uiManager == null) { uiManager = FindFirstObjectByType<UIManager>(); if (uiManager == null) { Debug.LogError("UIManager not found!"); referencesOk = false; } }
        if (squidAgentPrefab == null) { Debug.LogError("SquidAgentPrefab not assigned!"); referencesOk = false; }

        if (!referencesOk)
        {
            Debug.LogError("SimulationManager is missing critical references! Please assign them in the Inspector. Disabling SimulationManager.");
            enabled = false;
            return;
        }
        
        Time.timeScale = 1f;
        isPaused = false;
        isRunning = false;
        // UIManager.InitializeUIValues теперь не принимает baseEnergyDrainMultiplier, так как он не управляет им
        if (uiManager) uiManager.InitializeUIValues(Time.timeScale, currentGenerationNumber);
    }

    public void RequestStartSimulation()
    {
        if (isRunning && !isPaused) {
            Debug.LogWarning("Simulation is already running. Restarting...");
            ClearPopulation();
        } else if (isRunning && isPaused) {
             Debug.LogWarning("Simulation was paused. Starting fresh (like a restart).");
             isPaused = false;
             ClearPopulation();
        }
        
        Debug.Log($"Simulation Starting/Restarting. EnergyDrainMultiplier: {baseEnergyDrainMultiplier}, ReproThreshold: {reproductionEnergyThresholdSetting:P0}");
        isRunning = true;
        isPaused = false;
        
        float requestedTimeScale = (uiManager != null ? uiManager.GetCurrentTimeScaleRequest() : 1f);
        timeScaleBeforePause = Mathf.Max(0.01f, requestedTimeScale);
        Time.timeScale = timeScaleBeforePause;

        currentGenerationNumber = 1;
        InitializeFirstGeneration();
        currentGenerationTimer = generationTime;
        
        if (statisticsManager) statisticsManager.ResetStatistics();
        if (foodSpawner) foodSpawner.SpawnInitialFood();
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
    }

    public void RequestPauseSimulation() {
        if (!isRunning || isPaused) return; isPaused = true;
        timeScaleBeforePause = Time.timeScale; Time.timeScale = 0f;
        Debug.Log("Simulation Paused.");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
    }

    public void RequestResumeSimulation() {
        if (!isRunning || !isPaused) return; isPaused = false;
        Time.timeScale = timeScaleBeforePause; if (Time.timeScale < 0.01f) Time.timeScale = 1f;
        Debug.Log($"Simulation Resumed. TimeScale: {Time.timeScale}");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
    }
    
    public void RequestAdjustTimeScale(float requestedScale) {
        float newScale = Mathf.Max(0.01f, requestedScale); timeScaleBeforePause = newScale;
        if (!isPaused) { Time.timeScale = newScale; }
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
    }

    void Update() {
        if (!isRunning || isPaused) return; currentGenerationTimer -= Time.deltaTime;
        bool allAgentsDead = agents.Count == 0;
        bool canPotentiallyEvolve = genomesForNextGeneration.Count > 0 || agents.Count > 0;
        if (currentGenerationTimer <= 0 || (allAgentsDead && canPotentiallyEvolve && currentGenerationNumber > 0) ) {
            PrepareAndStartNewGeneration();
        }
    }

    void InitializeFirstGeneration()
    {
        ClearPopulation();
        if (initialPopulationSize > 0) {
            for (int i = 0; i < initialPopulationSize; i++)
            {
                Genome newGenome = new Genome(numInputNodes, numHiddenNodes, numOutputNodes);
                newGenome.InitializeRandomPhysicalGenes();
                
                // Применяем глобальные настройки из SimulationManager к геному новых агентов
                newGenome.metabolismRateFactor = newGenome.metabolismRateFactor * this.baseEnergyDrainMultiplier;
                newGenome.metabolismRateFactor = Mathf.Clamp(newGenome.metabolismRateFactor, 0.1f, 10f);
                
                newGenome.energyToReproduceThresholdFactor = this.reproductionEnergyThresholdSetting;
                // energyCostOfReproductionFactor остается случайным из генома, но можно тоже сделать настраиваемым
                
                SpawnAgent(newGenome);
            }
        }
        Debug.Log($"Initialized first generation ({currentGenerationNumber}) with {agents.Count} agents. EnergyDrainMultiplier: {baseEnergyDrainMultiplier:F2}, ReproThreshold: {reproductionEnergyThresholdSetting:P0}");
    }

    void PrepareAndStartNewGeneration()
    {
        if (!isRunning) return;

        currentGenerationNumber++;
        Debug.Log($"Preparing new generation #{currentGenerationNumber}...");
        
        List<Genome> genomesToEvolve = new List<Genome>();
        genomesToEvolve.AddRange(genomesForNextGeneration);
        genomesForNextGeneration.Clear();

        foreach (var agent in agents) {
            if (agent != null && agent.isInitialized && agent.genome != null) {
                 if(agent.TryGetComponent<SquidMetabolism>(out var meta)) {
                     agent.genome.fitness = meta.Age + (meta.CurrentEnergy / meta.maxEnergyGeno) * 25f;
                 }
                 genomesToEvolve.Add(new Genome(agent.genome));
            }
        }

        ClearAgentsVisuals();

        if (genomesToEvolve.Count == 0) {
            Debug.LogWarning("No genomes to evolve from for new generation.");
            if (initialPopulationSize > 0) {
                Debug.LogWarning("Re-initializing population as first generation.");
                currentGenerationNumber = 1;
                InitializeFirstGeneration();
            } else {
                Debug.LogError("No genomes to evolve AND initialPopulationSize is 0. Simulation will stop progressing.");
                isRunning = false;
            }
            currentGenerationTimer = generationTime;
            if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
            return;
        }

        List<Genome> newGenerationGenomes = gaManager.EvolvePopulation(genomesToEvolve);

        if (newGenerationGenomes.Count == 0 && initialPopulationSize > 0) {
             Debug.LogWarning("GA returned 0 genomes, but initialPopulationSize > 0. Re-initializing to avoid empty state.");
             InitializeFirstGeneration();
        } else if (newGenerationGenomes.Count > 0) {
            foreach (Genome genome in newGenerationGenomes)
            {
                genome.fitness = 0;
                // Применяем глобальные настройки из SimulationManager к геному потомков
                genome.metabolismRateFactor = genome.metabolismRateFactor * this.baseEnergyDrainMultiplier;
                genome.metabolismRateFactor = Mathf.Clamp(genome.metabolismRateFactor, 0.1f, 10f);
                
                genome.energyToReproduceThresholdFactor = this.reproductionEnergyThresholdSetting;

                SpawnAgent(genome);
            }
        } else {
             Debug.LogError("GA returned 0 genomes and initialPopulationSize is 0. Simulation cannot continue.");
             isRunning = false;
        }

        currentGenerationTimer = generationTime;
        Debug.Log($"Spawned generation #{currentGenerationNumber} with {agents.Count} agents.");
        if (uiManager) uiManager.UpdateSimulationStateUI(isRunning, isPaused, Time.timeScale, currentGenerationNumber);
    }

    void SpawnAgent(Genome genome, Vector3? position = null) {
        if (squidAgentPrefab == null) { Debug.LogError("SquidAgent Prefab is not set!"); return; }
        Vector3 spawnPos = position ?? GetRandomSpawnPosition();
        GameObject agentGO = Instantiate(squidAgentPrefab, spawnPos, Quaternion.identity);
        SquidAgent squidAgent = agentGO.GetComponent<SquidAgent>();
        if (squidAgent != null) { squidAgent.Initialize(genome, this); agents.Add(squidAgent); }
        else { Debug.LogError($"Failed to get SquidAgent component from {agentGO.name}. Destroying."); Destroy(agentGO); }
    }

    Vector3 GetRandomSpawnPosition() {
        WorldBounds wb = FindFirstObjectByType<WorldBounds>();
        if (wb != null) { return new Vector3(Random.Range(wb.xMin + 1f, wb.xMax - 1f), Random.Range(wb.yMin + 1f, wb.yMax - 1f), 0); }
        float spawnRadius = (foodSpawner != null) ? foodSpawner.spawnRadius : 20f;
        return new Vector3(Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius), 0);
    }

    public void ReportAgentDeath(SquidAgent agent, Genome finalGenome) {
        if (agents.Contains(agent)) {
            agents.Remove(agent); genomesForNextGeneration.Add(finalGenome);
            if (foodSpawner) foodSpawner.SpawnFoodAt(foodSpawner.meatFoodPrefab, agent.transform.position);
            EventLogPanel.Instance?.AddLogMessage($"Squid {agent.gameObject.name.Split('_').LastOrDefault()} died. Fit: {finalGenome.fitness:F1}");
        }
    }
    
    public void ReportAgentReproduction(SquidAgent parent, Genome offspringGenome) {
        if (agents.Count < 150) { SpawnAgent(offspringGenome, parent.transform.position + (Vector3)Random.insideUnitCircle * 1.5f); }
    }

    void ClearPopulation() { ClearAgentsVisuals(); genomesForNextGeneration.Clear(); }
    
    void ClearAgentsVisuals() {
        List<SquidAgent> agentsToDestroy = new List<SquidAgent>(agents);
        foreach (var agent in agentsToDestroy) { if (agent != null) Destroy(agent.gameObject); }
        agents.Clear();
    }
}
