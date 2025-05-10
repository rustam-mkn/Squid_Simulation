// /Users/user/Dev/Unity/Squid/Assets/Core/StatisticsManager.cs
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
