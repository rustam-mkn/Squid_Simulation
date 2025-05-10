// /Users/user/Dev/Unity/Squid/Assets/UI/Panels/StatisticsPanel.cs
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
